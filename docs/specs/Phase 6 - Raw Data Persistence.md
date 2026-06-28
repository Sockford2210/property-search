# Phase 6 - Raw Data Persistence — Spec

**Status:** Ready for implementation
**Source plan:** `docs/plans/Implementation Plan.md` (Phase 6)
**Depends on:** Phase 1 (`Listing`/`Source` schema, `PropertyDbContext`, migrations), Phase 4 (discovery library + `IPageFetcher`), Phase 5 (`RightmoveListingParser` + `ParsedListing`).
**Date:** 2026-06-28

## Goal

Persist scraped Rightmove listings idempotently, preserve raw portal data, and
model search scope so the stored dataset always reflects **the properties that
currently match an active search**. Phase 6 introduces the new schema
(`SearchProfile`, `SearchProfileListing`, `ScrapeSnapshot`) and the
`RightmoveScrapeService` orchestrator that ties discovery (Phase 4) + parsing
(Phase 5) + persistence together, plus the `src/PropertyScraper` console host.

Phase 6 owns: the upsert on `(SourceId, ExternalId)`, per-profile membership
edges with seen-tracking, append-on-change raw snapshots, and the per-profile
removal sweep (soft-delete) that runs only after a cleanly-completed crawl.

No normalisation into `Property`, no enrichment, no scoring, no API/UI.

## Decisions (resolved with the user)

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **All new schema lands in one Phase 6 migration**; Phases 4 & 5 stay pure DB-free libraries. The orchestrator loads enabled profiles, maps each to a `RightmoveSearchCriteria`, runs Phase 4 discovery + Phase 5 parsing, and persists. | Maximises independent testability of discovery/parsing and matches the Stations + thin-host pattern. Persistence is concentrated where it belongs. |
| 2 | **`SearchProfile` entity is the home of search criteria** `{ Name, LocationIdentifier, MinPrice, MaxPrice, MinBedrooms, MaxBedrooms, RadiusMiles, Enabled }`. The orchestrator crawls every **`Enabled`** profile. | The stored dataset is scoped to active searches. Criteria live in the DB (not config) so removal can be scoped per profile and future API/UI can manage them. |
| 3 | **Listing↔Profile is many-to-many** via a join entity `SearchProfileListing { SearchProfileId, ListingId, FirstSeenAt, LastSeenAt }` (composite PK). Presence of an edge = "this listing currently matches this profile". | A single property can match several profiles; it must be stored **once** (preserving `(Source, ExternalId)` uniqueness) while tracking per-profile membership and seen-times. |
| 4 | **Removed-detection is run-scoped soft-delete.** After a profile crawl completes cleanly, edges of **that profile** not seen this run are deleted; a `Listing` left with **zero edges** flips to `Status = Removed` (kept, excluded from active views). "No longer matches the criteria" is a valid removal reason — the dataset reflects what the user still wants to see. | The user only wants currently-matching properties surfaced. Soft-delete (not hard-delete) preserves rows + snapshots for future listing-age / price-reduction / re-appearance features and is consistent with the `Removed` enum already in the schema. |
| 5 | **The sweep runs only if discovery paginated to completion** (`crawlCompleted` flag). On partial/aborted discovery, persist what was seen but **skip the sweep**. | A mid-pagination failure means an incomplete result set; sweeping then would wrongly mark un-fetched listings `Removed` and cause status thrash. |
| 6 | **Raw data is preserved in an append-on-change `ScrapeSnapshot`** `{ Id, ListingId, FetchedAt, RawJson (jsonb) }`. A new row is written only when the freshly extracted raw payload differs from the listing's latest snapshot (or none exists). | Satisfies CLAUDE.md's "preserve raw portal responses" and Phase 6's "store raw data" without unbounded duplicate rows. Each row marks a real change — substrate for future price-reduction/age detection. Image URLs and unmapped fields live here. |
| 7 | **Per-listing commit, skip-and-continue.** Each listing is upserted in its own save; a `ListingParseException` (Phase 5) is caught, logged with the URL, counted as `Skipped`, and the run continues. **No run-wide transaction.** | One bad/POA listing must not discard a 200-listing run; long transactions over many HTTP calls would hold locks and roll back good data. |
| 8 | **Re-fetch all discovered listings each run, with a staleness throttle.** Every discovered ref bumps its edge `LastSeenAt` (so the sweep is correct); the **detail page** is fetched + parsed + upserted only if the listing is **new or its `LastSeenAt` is older than `RefetchAfter`**. | Catches price/status changes (feeds price-reduction detection) without re-fetching freshly-seen listings every run. Edge bumping is decoupled from detail-fetching so removal stays accurate. |
| 9 | **`Source` (`rightmove`) and initial `SearchProfile`(s) are created via documented SQL inserts** (README); tests seed their own rows in fixtures. | No API/UI exists yet to manage them; explicit SQL is the smallest mechanism and keeps mutable search data out of migration history. |
| 10 | **Code in `shared/Infrastructure/Sources/Rightmove/`; `src/PropertyScraper` is a thin console host** that wires DI and runs one pass over enabled profiles. Tests use the existing `PostgresFixture` (Testcontainers PostGIS + Respawn). | Consistent with `StationImportService` / `StationImporter`. |

## Schema design (one migration)

```text
search_profiles
  id (PK), name, location_identifier,
  min_price?, max_price?, min_bedrooms?, max_bedrooms?,
  radius_miles, enabled,
  created_at, updated_at

search_profile_listings              -- the "matches" edge (M:N)
  search_profile_id (FK -> search_profiles, cascade),
  listing_id        (FK -> listings, cascade),
  first_seen_at, last_seen_at
  PK (search_profile_id, listing_id)

scrape_snapshots                     -- append-on-change raw history
  id (PK),
  listing_id (FK -> listings, cascade),
  fetched_at,
  raw_json (jsonb)
  index (listing_id, fetched_at desc)
```

`Listing` is unchanged structurally: its existing `Status`, `FirstSeenAt`,
`LastSeenAt`, and the unique `(SourceId, ExternalId)` index are reused.
`Listing.LastSeenAt` is maintained as the max across its edges' `LastSeenAt`.
Default queries for "current" listings filter `Status = Active`.

## Orchestration design (`RightmoveScrapeService`)

For each `Enabled` `SearchProfile`:

```text
runStart = now
criteria = map(profile)                     // profile columns -> RightmoveSearchCriteria
(refs, crawlCompleted) = discovery.DiscoverAll(criteria)   // Phase 4

foreach ref in refs:                        // per-listing commit
    upsert edge (profile, listing-by-ExternalId):
        new edge   -> FirstSeenAt = LastSeenAt = runStart
        existing   -> LastSeenAt = runStart           // always bumped
    if (listing is new) OR (listing.LastSeenAt < runStart - RefetchAfter):
        try:
            html   = fetcher.Get(ref.Url)             // Phase 4 IPageFetcher
            parsed = listingParser.Parse(html)        // Phase 5
            upsert listing on (SourceId, ExternalId):
                new      -> insert { ...parsed, Status=Active,
                                     FirstSeenAt=LastSeenAt=runStart }
                existing -> refresh mutable fields + LastSeenAt=runStart
                            (preserve FirstSeenAt)
            append ScrapeSnapshot if raw differs from latest
            saved/updated++
        catch ListingParseException ex:
            log.Warn(ex, ref.Url); skipped++; continue
    else:
        // fresh enough: edge bumped, detail fetch skipped

if (crawlCompleted):
    delete edges of THIS profile with LastSeenAt < runStart   // stale memberships
    listings with zero remaining edges -> Status = Removed     // soft-delete
    refresh Listing.LastSeenAt = max(edge.LastSeenAt)
else:
    log.Warn("partial crawl for {profile}; sweep skipped")

report { Saved, Updated, Skipped, Removed }
```

`RefetchAfter`, politeness delay, and `MaxResults` come from
`RightmoveScraperOptions` (config).

## Deliverables

### 1. Domain (`shared/Domain`)
- `SearchProfile.cs`, `SearchProfileListing.cs`, `ScrapeSnapshot.cs`
  (all `AuditableEntity` except the join, which uses a composite key).

### 2. Infrastructure (`shared/Infrastructure`)
- EF configurations for the three new entities (snake_case, FKs, composite PK,
  jsonb column, indexes) following the existing `*Configuration` convention.
- `DbSet`s added to `PropertyDbContext`.
- **One migration** adding `search_profiles`, `search_profile_listings`,
  `scrape_snapshots`.
- `Sources/Rightmove/RightmoveScrapeService.cs` — the orchestrator above.
- Mapping helper `SearchProfile → RightmoveSearchCriteria`.

### 3. Host (`src/PropertyScraper`)
- Thin console `Program.cs` (mirrors `StationImporter`): env-driven connection
  (`PROPERTYSEARCH_DB`, `UsePropertySearchNpgsql`), DI for `IPageFetcher`
  (named HttpClient + Polly), discovery, parser, `RightmoveScrapeService`; runs
  one pass over enabled profiles and logs `{ Saved, Updated, Skipped, Removed }`.
- `PropertySearch.PropertyScraper.csproj` added to the solution.

### 4. Tests (`tests/PropertySearch.Infrastructure.Tests`)
Integration tests using `PostgresFixture` (Respawn-reset), with a fake
`IPageFetcher` returning committed fixtures so no network is touched:

- **Insert:** a clean crawl of a profile inserts new listings, edges
  (`FirstSeenAt == LastSeenAt`), and one snapshot each.
- **Idempotent re-run (no dupes):** running the same crawl twice creates **no
  duplicate** listings/edges; `LastSeenAt` advances, `FirstSeenAt` preserved.
- **Update modifies existing:** a changed detail fixture (e.g. lower rent)
  updates the listing and appends a **new** snapshot; an unchanged re-run appends
  **no** snapshot.
- **Multi-profile sharing:** a listing matching two profiles is stored **once**
  with **two** edges.
- **Sweep marks Removed:** after a clean crawl where a previously-seen listing is
  absent, its edge is deleted and (with no edges left) `Status == Removed`; a
  listing still matching another profile stays `Active`.
- **Partial crawl skips sweep:** when discovery reports `crawlCompleted == false`,
  absent listings are **not** marked `Removed`.
- **Staleness throttle:** a freshly-seen listing has its edge bumped but its
  detail page is **not** re-fetched (assert the fake fetcher wasn't called for it).
- **Skip-and-continue:** a POA/invalid detail fixture is skipped (counted), the
  rest of the run still persists.

### 5. Docs
- README: a **"Scraper"** section — how to seed the `rightmove` `Source` and a
  `SearchProfile` (SQL inserts), and how to run `src/PropertyScraper`.

## Acceptance Criteria

1. A clean crawl persists listings, `(profile, listing)` edges, and append-on-
   change snapshots; re-running creates **no duplicates** (run-twice test).
2. Updates modify the existing listing and append a new snapshot only when the
   raw payload changed; `FirstSeenAt` is preserved, `LastSeenAt` advances.
3. A listing matching multiple profiles is stored once with one edge per profile.
4. After a **cleanly-completed** crawl, listings absent from a profile lose that
   edge and flip to `Status = Removed` only when no edge remains.
5. A **partial** crawl persists seen listings but performs **no** sweep.
6. The staleness throttle skips detail re-fetch for freshly-seen listings while
   still bumping their edge `LastSeenAt`.
7. Parse failures are skipped-and-counted without aborting the run.
8. All unit + integration tests pass (`dotnet test`).
9. `dotnet build` succeeds with **zero warnings** (warnings-as-errors).

## Verification

```bash
# Schema + infra
docker compose up -d
dotnet ef database update --project shared/Infrastructure

# Seed source + a search profile (documented SQL)
docker compose exec db psql -U propertysearch -d propertysearch -c \
  "INSERT INTO sources (code, name, base_url, enabled, created_at, updated_at)
   VALUES ('rightmove','Rightmove','https://www.rightmove.co.uk', true, now(), now())
   ON CONFLICT (code) DO NOTHING;"
docker compose exec db psql -U propertysearch -d propertysearch -c \
  "INSERT INTO search_profiles
     (name, location_identifier, min_price, max_price, min_bedrooms, max_bedrooms, radius_miles, enabled, created_at, updated_at)
   VALUES ('camden-2bed','REGION^87490', 1000, 2000, 2, 3, 0.5, true, now(), now());"

# Tests (fixtures + fake fetcher) + clean build
dotnet test
dotnet build

# Run a real scrape pass (polite; hits Rightmove)
dotnet run --project src/PropertyScraper

# Verify: no duplicates, snapshots present
docker compose exec db psql -U propertysearch -d propertysearch -c \
  "SELECT s.code, count(*) AS listings
   FROM listings l JOIN sources s ON s.id = l.source_id
   GROUP BY s.code;"
docker compose exec db psql -U propertysearch -d propertysearch -c \
  "SELECT count(*) AS snapshots FROM scrape_snapshots;"
```

## Explicit Non-Goals (deferred)

- Normalising `Listing` → canonical `Property` → Phase 7
- Nearest-station / distance enrichment → Phase 8
- Scoring → Phase 9; API → Phase 10; UI → Phase 11
- Managing `SearchProfile`s via API/UI (SQL inserts for now) → later
- Snapshot retention/pruning, full per-fetch audit log → not needed (append-on-change)
- Hard-deleting removed listings → out (soft-delete retains history)
- Continuous/scheduled running (single pass per invocation for now) → later
- Additional portals (Zoopla, OnTheMarket) → Phase 14

## Proposed file tree after Phase 6 (additions)

```text
PropertySearch/
├── shared/
│   ├── Domain/
│   │   ├── SearchProfile.cs                 (new)
│   │   ├── SearchProfileListing.cs           (new)
│   │   └── ScrapeSnapshot.cs                 (new)
│   └── Infrastructure/
│       ├── Configurations/
│       │   ├── SearchProfileConfiguration.cs         (new)
│       │   ├── SearchProfileListingConfiguration.cs  (new)
│       │   └── ScrapeSnapshotConfiguration.cs        (new)
│       ├── Migrations/
│       │   └── <timestamp>_AddScrapePersistence.cs   (new)
│       └── Sources/Rightmove/
│           └── RightmoveScrapeService.cs             (new)
├── src/
│   └── PropertyScraper/                              (new)
│       ├── Program.cs
│       └── PropertySearch.PropertyScraper.csproj
└── tests/
    └── PropertySearch.Infrastructure.Tests/
        ├── Fixtures/Rightmove/...                    (reused + add cases)
        └── RightmoveScrapeServiceTests.cs            (new)
```
