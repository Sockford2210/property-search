# Phase 4 - Search Discovery Scraper — Spec

**Status:** Ready for implementation
**Source plan:** `docs/plans/Implementation Plan.md` (Phase 4)
**Depends on:** Phase 0 (monorepo + central package management), Phase 1 (Domain/Infrastructure projects). No database dependency — this phase is a **pure library**.
**Date:** 2026-06-28

## Goal

Discover Rightmove rental listing **references** (external id + URL) for a given
set of search criteria. Deliver a **pure, DB-free, injectable discovery library**
in `shared/Infrastructure/Sources/Rightmove/` plus a reusable `IPageFetcher`
HTTP seam, all testable against committed HTML fixtures.

Phase 4 delivers: a `RightmoveSearchParser` (HTML → references), a
`RightmoveSearchDiscoveryService` (paginates a search via `IPageFetcher`), the
`IPageFetcher` / `HttpPageFetcher` fetch seam with Polly resilience, and tests
proving correct parsing + pagination against fixtures.

No persistence, no schema change, no detail-page parsing, no scoring, no UI.
Discovery returns **only** `(ExternalId, Url)` — authoritative field extraction
is Phase 5; persistence and the `SearchProfile`/sweep model are Phase 6.

## Decisions (resolved with the user)

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **Parse the embedded JSON model**, not CSS selectors. Rightmove search results embed a `window.jsonModel` blob in a `<script>`; AngleSharp is used **only** to locate that script node, then `System.Text.Json` deserialises it. | The JSON model carries `id` and `propertyUrl` (and would carry summaries) as structured data that survives layout/CSS-class churn. CSS scraping against Rightmove's obfuscated class names is brittle and doesn't reliably expose ids. |
| 2 | **`IPageFetcher` seam + committed HTML fixtures.** Parsing is a pure function over a string; an `HttpPageFetcher` implements live retrieval. Automated tests run against a handful of **real saved search pages committed as fixtures**. Live Rightmove is hit only via a manual, opt-in/`[Explicit]` smoke test — never in normal `dotnet test`. | Deterministic, offline, ToS-safe CI. Mirrors how the Phase 2 station tests run over embedded JSON. Separating fetch from parse makes both independently testable. |
| 3 | **Lives in `shared/Infrastructure/Sources/Rightmove/`**; the executable is a thin `src/PropertyScraper` console host (added in Phase 6). | Matches the established Stations (`StationImportService` in Infrastructure) + thin `StationImporter` host pattern. Reusable logic in the shared assembly; the host only wires DI. |
| 4 | **Input is a plain `RightmoveSearchCriteria` object** `{ LocationIdentifier, MinPrice, MaxPrice, MinBedrooms, MaxBedrooms, RadiusMiles }`. `LocationIdentifier` is a **pre-resolved Rightmove token** (e.g. `REGION^87490`). | Rightmove search requires a `locationIdentifier` token, not free text. Keeping criteria a plain object (not a DB entity yet) keeps Phase 4 DB-free; the `SearchProfile` persistence that supplies these criteria is Phase 6. Free-text→token resolution is a non-goal. |
| 5 | **Output is `SearchResultRef(string ExternalId, Uri Url)` only** — no price/beds/summary captured here. | Keeps the discovery/detail seam clean: Phase 5 re-parses authoritative fields from the detail page; Phase 6 dedupes on `(SourceId, ExternalId)`. Avoids two sources of truth (search summary vs detail page). |
| 6 | **Pagination walks `&index` by 24** and stops on the **first** of: a page returns 0 properties, `index >= resultCount` (reported in the JSON), or a configurable `MaxResults` safety cap. A politeness delay separates page requests. | Rightmove pages 24 results at a time and hard-caps at ~1000 results. The triple guard fetches everything available without over- or under-fetching and bounds load. |
| 7 | **Polly resilience** (`Microsoft.Extensions.Http.Resilience`) for retry/backoff/circuit-breaker, registered via `AddHttpClient`. The typed client also sets a **realistic `User-Agent`/`Accept`** and applies the inter-request politeness delay. | Rightmove 403/429s naked clients. Polly is the standard resilience stack; identity + pacing live on the client. Sequential (single-threaded) fetching keeps it polite. |
| 8 | **Acceptance reframed from the plan's "100+ URLs live".** The CI-grade assertions parse committed fixtures deterministically; "100+ refs from a live search" becomes the **manual smoke test**. | The plan's literal live assertion is flaky (anti-bot, changing listings) and ToS-risky in CI. |

## Request / parse design

A search request is built against the to-rent search endpoint, e.g.:

```text
GET https://www.rightmove.co.uk/property-to-rent/find.html
    ?locationIdentifier={LocationIdentifier}
    &minPrice={MinPrice}&maxPrice={MaxPrice}
    &minBedrooms={MinBedrooms}&maxBedrooms={MaxBedrooms}
    &radius={RadiusMiles}
    &index={index}        // 0, 24, 48, ...
```

The response HTML embeds `window.jsonModel = { ... }`. The parser:
1. Uses AngleSharp to find the `<script>` whose text assigns `jsonModel`.
2. Extracts the JSON object text (the right-hand side of the assignment).
3. Deserialises to a typed model exposing `resultCount` and `properties[]`
   (each with `id` and `propertyUrl`).
4. Projects each property to `SearchResultRef(id.ToString(), absolute Url)`.

All query values are passed as bound query-string parameters (never string-
interpolated into a raw request), and the URL is normalised to an absolute
`https://www.rightmove.co.uk/...` form.

## Deliverables

### 1. Infrastructure (`shared/Infrastructure`)

- `Sources/Rightmove/RightmoveSearchCriteria.cs`:
  ```csharp
  public sealed record RightmoveSearchCriteria(
      string LocationIdentifier,
      int? MinPrice, int? MaxPrice,
      int? MinBedrooms, int? MaxBedrooms,
      double RadiusMiles);
  ```
- `Sources/Rightmove/SearchResultRef.cs`:
  ```csharp
  public sealed record SearchResultRef(string ExternalId, Uri Url);
  ```
- `Sources/Rightmove/IPageFetcher.cs` + `HttpPageFetcher.cs`:
  ```csharp
  public interface IPageFetcher
  {
      Task<string> GetAsync(Uri url, CancellationToken cancellationToken = default);
  }
  ```
  `HttpPageFetcher` wraps a named `HttpClient` (UA/Accept headers + politeness
  delay), with Polly retry/backoff/circuit-breaker on the handler. Hard blocks
  surface as a typed `ScrapeBlockedException`.
- `Sources/Rightmove/RightmoveSearchParser.cs` — pure `Parse(string html)`
  returning `(int ResultCount, IReadOnlyList<SearchResultRef> Refs)`.
- `Sources/Rightmove/RightmoveSearchDiscoveryService.cs` — paginates via
  `IPageFetcher` per the **Request / parse design**, applying the triple stop
  guard and a `MaxResults` option; returns the de-duplicated set of refs.
- A small `RightmoveScraperOptions` (politeness delay, `MaxResults`, base URL)
  bound from configuration.

New package(s): `AngleSharp`, `Microsoft.Extensions.Http.Resilience` (added to
`Directory.Packages.props`; referenced by Infrastructure). No domain change, no
migration.

### 2. Tests (`tests/PropertySearch.Infrastructure.Tests`)

Pure unit tests (no Postgres fixture needed):

- **Parser fixture test:** a committed `rightmove-search-*.html` fixture →
  `Parse` returns the expected `ResultCount` and the exact list of
  `SearchResultRef` (ids + absolute URLs).
- **Pagination test:** a fake `IPageFetcher` returns successive fixture pages and
  then an empty/last page; the discovery service yields the union of refs and
  **stops** correctly on each of the three conditions (empty page, `index >=
  resultCount`, `MaxResults` cap) — one test per condition.
- **De-duplication test:** overlapping refs across pages collapse to a distinct
  set keyed by `ExternalId`.
- **Malformed page test:** a fixture missing the `jsonModel` script raises a
  clear, typed parse error.

Fixtures live under `tests/.../Fixtures/Rightmove/` and are loaded via a small
`Fixtures.Load(name)` helper.

**Manual smoke (opt-in, not in CI):** an `[Explicit]`/skipped-by-default test
runs the real `HttpPageFetcher` against a live search and asserts **100+ refs**
returned with valid `/properties/{id}` URLs.

### 3. Solution & docs

- No new project (Infrastructure + Infrastructure.Tests already exist).
- README: a short **"Search discovery"** note pointing at
  `RightmoveSearchDiscoveryService` as the Phase 4 entry point.

## Acceptance Criteria

1. `RightmoveSearchParser.Parse` extracts `resultCount` and all
   `(ExternalId, Url)` refs from a committed search-results fixture.
2. `RightmoveSearchDiscoveryService` paginates across multiple fixture pages and
   returns the de-duplicated union of refs.
3. Pagination **stops** on each of: empty page, `index >= resultCount`, and the
   `MaxResults` cap (verified independently).
4. A malformed page (no `jsonModel`) yields a clear typed error.
5. The opt-in live smoke test returns **100+** valid refs (manual run only).
6. All unit tests pass (`dotnet test`); no Postgres required for this phase's tests.
7. `dotnet build` succeeds with **zero warnings** (warnings-as-errors).

## Verification

```bash
# Deterministic, offline:
dotnet test
dotnet build

# Opt-in live smoke (manual; hits Rightmove — run sparingly, politely):
dotnet test --filter "Category=LiveSmoke"
```

## Explicit Non-Goals (deferred)

- Parsing listing detail fields (price/beds/baths/coords/description) → Phase 5
- Persisting refs or listings, dedupe storage, snapshots → Phase 6
- `SearchProfile` entity / DB-driven criteria / removal sweep → Phase 6
- Free-text location → `locationIdentifier` resolution (typeahead) → not needed
- Headless browser / proxy rotation → not needed (JSON model + polite fetch)
- Additional portals (Zoopla, OnTheMarket) → Phase 14

## Proposed file tree after Phase 4 (additions)

```text
PropertySearch/
└── shared/
    └── Infrastructure/
        └── Sources/
            └── Rightmove/                         (new)
                ├── RightmoveSearchCriteria.cs
                ├── SearchResultRef.cs
                ├── IPageFetcher.cs
                ├── HttpPageFetcher.cs             (+ ScrapeBlockedException)
                ├── RightmoveScraperOptions.cs
                ├── RightmoveSearchParser.cs
                └── RightmoveSearchDiscoveryService.cs
└── tests/
    └── PropertySearch.Infrastructure.Tests/
        ├── Fixtures/Rightmove/rightmove-search-*.html   (new)
        ├── RightmoveSearchParserTests.cs                (new)
        └── RightmoveSearchDiscoveryServiceTests.cs      (new)
```
