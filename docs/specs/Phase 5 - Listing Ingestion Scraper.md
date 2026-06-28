# Phase 5 - Listing Ingestion Scraper — Spec

**Status:** Ready for implementation
**Source plan:** `docs/plans/Implementation Plan.md` (Phase 5)
**Depends on:** Phase 4 (complete — `IPageFetcher` seam, `Sources/Rightmove/` package, fixture/test conventions). No database dependency — this phase is a **pure library**.
**Date:** 2026-06-28

## Goal

Extract structured data from a **single** Rightmove listing detail page. Deliver a
**pure, DB-free `RightmoveListingParser`** that turns a detail-page HTML string
into a `ParsedListing` record, with deterministic field extraction proven against
committed fixtures.

No persistence, no schema change, no scoring, no UI. The parser is a pure
function `string html → ParsedListing`; mapping to the `Listing` entity,
timestamps, status, and snapshots are all Phase 6.

## Decisions (resolved with the user)

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **Parse the embedded JSON model** (`PAGE_MODEL` / `propertyData`), not CSS selectors. AngleSharp locates the `<script>` node; `System.Text.Json` deserialises. | The detail page embeds the full property model (price, bedrooms, bathrooms, `location.latitude/longitude`, description, id). Structured JSON survives layout churn and reliably exposes coordinates and ids that CSS scraping does not. Consistent with Phase 4. |
| 2 | **Parser returns a pure `ParsedListing` record** `{ ExternalId, Url, DisplayAddress, RentPcm, Bedrooms, Bathrooms?, Latitude?, Longitude?, Description? }` — **zero EF/persistence concerns**. | Mirrors Phase 3's `NearestStationResult`. Keeps parsing pure and trivially fixture-testable; Phase 6 owns `SourceId`, `FirstSeenAt`/`LastSeenAt`, `Status`, upsert, and snapshots. The parser never touches a half-built entity. |
| 3 | **Rent normalised to a whole-pound monthly `decimal`.** Read structured `amount` + `frequency`: monthly → as-is; weekly-only → `round(amount × 52 / 12)`. **POA / zero / unparseable rent → `ListingParseException`** (since `Listing.RentPcm` is non-nullable). | One canonical monthly value matches the domain. POA cannot be represented as a non-null decimal, so such listings are rejected rather than stored with a fabricated rent. |
| 4 | **Required fields:** `ExternalId`, `Url`, `DisplayAddress`, `RentPcm`, `Bedrooms`. Their absence → typed **`ListingParseException`**. **Optional fields** (`Bathrooms`, `Latitude`, `Longitude`, `Description`) simply stay `null`. | Matches the `Listing` schema's required/nullable shape (`Bathrooms`, coords, `Description` are nullable; the rest required). Studios map to `Bedrooms = 0`. |
| 5 | **Batch resilience belongs to the Phase 6 orchestrator:** it catches `ListingParseException` per listing, logs the URL, increments a `Skipped` counter, and continues — one bad listing never aborts a run. Phase 5 only defines the exception contract. | Keeps the parser a pure throwing function; skip-and-continue is an orchestration concern. |
| 6 | **Images are not a domain field and are not mapped.** Image URLs (and any other unmapped portal fields) are preserved **inside the raw JSON** persisted by Phase 6's `ScrapeSnapshot`. | The `Listing` entity has no image storage; adding one is out of scope for the scraper trilogy. The plan's "Images" field is satisfied by raw preservation, not a structured column. |
| 7 | **Tested against committed detail-page fixtures** via the Phase 4 fixture helper; no live calls in CI (one opt-in live smoke). | Deterministic, offline, ToS-safe — consistent with Phase 4. |

## Parse / mapping design

For a detail page embedding `window.PAGE_MODEL = { propertyData: { ... } }`:

| `ParsedListing` field | Source in model | Notes |
|-----------------------|-----------------|-------|
| `ExternalId` | `propertyData.id` | string form of the numeric id |
| `Url` | canonical `/properties/{id}` | absolute `https://www.rightmove.co.uk/...` |
| `DisplayAddress` | `propertyData.address.displayAddress` | required |
| `RentPcm` | `propertyData.prices` (`amount` + `frequency`) | weekly → ×52/12, rounded; POA → throw |
| `Bedrooms` | `propertyData.bedrooms` | studio = 0 |
| `Bathrooms` | `propertyData.bathrooms` | nullable |
| `Latitude` / `Longitude` | `propertyData.location.latitude/longitude` | nullable |
| `Description` | `propertyData.text.description` | nullable; HTML stripped to text |

The parser:
1. Uses AngleSharp to find the `<script>` assigning `PAGE_MODEL`.
2. Extracts and deserialises the JSON to a typed model.
3. Validates required fields, normalises rent, maps to `ParsedListing`.
4. Throws `ListingParseException(reason, url?)` on any required-field/rent failure.

## Deliverables

### 1. Infrastructure (`shared/Infrastructure`)

- `Sources/Rightmove/ParsedListing.cs`:
  ```csharp
  public sealed record ParsedListing(
      string ExternalId,
      Uri Url,
      string DisplayAddress,
      decimal RentPcm,
      int Bedrooms,
      int? Bathrooms,
      double? Latitude,
      double? Longitude,
      string? Description);
  ```
- `Sources/Rightmove/ListingParseException.cs` — typed exception carrying a
  reason and optional source URL.
- `Sources/Rightmove/RightmoveListingParser.cs` — pure
  `ParsedListing Parse(string html)` per the **Parse / mapping design**, including
  the rent-normalisation helper (monthly/weekly/POA).

No domain change, no migration, no new package (reuses AngleSharp/STJ from Phase 4).

### 2. Tests (`tests/PropertySearch.Infrastructure.Tests`)

Pure unit tests over committed detail-page fixtures:

- **Happy path (pcm):** `rightmove-listing-{id}.html` → exact `ParsedListing`
  values (rent, beds, baths, coords, address, trimmed description).
- **Weekly rent:** a weekly-quoted fixture → `RentPcm == round(amount × 52 / 12)`.
- **Studio:** a studio fixture → `Bedrooms == 0`.
- **Missing optionals:** a fixture with no bathrooms/coords → those fields `null`,
  parse still succeeds.
- **POA / no rent:** a POA fixture → throws `ListingParseException`.
- **Missing required field:** a fixture lacking `displayAddress` → throws
  `ListingParseException`.
- **Malformed page:** no `PAGE_MODEL` script → clear typed error.

**Manual smoke (opt-in):** fetch one known live listing via `HttpPageFetcher`,
parse it, and assert the values match the live page (manual run only).

### 3. Solution & docs

- No new project.
- README: extend the scraper note to point at `RightmoveListingParser` as the
  Phase 5 entry point.

## Acceptance Criteria

1. `RightmoveListingParser.Parse` returns a fully-populated `ParsedListing` from a
   committed detail-page fixture, with values matching the page.
2. Weekly-quoted rent is converted to a monthly whole-pound `decimal`
   (`×52/12`, rounded); studios yield `Bedrooms == 0`.
3. Missing optional fields (bathrooms, coordinates, description) parse to `null`
   without error.
4. POA / missing rent and any missing **required** field raise
   `ListingParseException`.
5. A malformed page (no `PAGE_MODEL`) yields a clear typed error.
6. All unit tests pass (`dotnet test`); no Postgres required.
7. `dotnet build` succeeds with **zero warnings** (warnings-as-errors).

## Verification

```bash
dotnet test
dotnet build

# Opt-in live smoke (manual; hits Rightmove):
dotnet test --filter "Category=LiveSmoke"
```

## Explicit Non-Goals (deferred)

- Mapping `ParsedListing` → `Listing`, timestamps, status, upsert → Phase 6
- Persisting raw JSON / images via `ScrapeSnapshot` → Phase 6
- Skip-and-continue batch handling (parser only defines the exception) → Phase 6
- Nearest-station enrichment of the listing → Phase 8
- Scoring → Phase 9; API → Phase 10; UI → Phase 11

## Proposed file tree after Phase 5 (additions)

```text
PropertySearch/
└── shared/
    └── Infrastructure/
        └── Sources/
            └── Rightmove/
                ├── ParsedListing.cs               (new)
                ├── ListingParseException.cs        (new)
                └── RightmoveListingParser.cs       (new)
└── tests/
    └── PropertySearch.Infrastructure.Tests/
        ├── Fixtures/Rightmove/rightmove-listing-*.html  (new)
        └── RightmoveListingParserTests.cs               (new)
```
