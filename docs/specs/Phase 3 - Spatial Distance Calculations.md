# Phase 3 - Spatial Distance Calculations — Spec

**Status:** Ready for implementation
**Source plan:** `docs/plans/Implementation Plan.md` (Phase 3)
**Depends on:** Phase 1 (complete — `stations.location geometry(Point,4326)` + GiST index `ix_stations_location`), Phase 2 (complete — `stations` populated with 300+ rows)
**Date:** 2026-06-28

## Goal

Prove that nearest-station calculations work. Deliver a **reusable, injectable
spatial service** that, given a latitude/longitude, returns the **single nearest
station** and the **distance to it in metres**, computed in PostgreSQL using the
GiST-indexed `location` column that Phase 1 created specifically for this phase.

Phase 3 delivers: an `INearestStationFinder` interface + a PostGIS-backed
implementation in `shared/Infrastructure`, and integration tests proving correct
nearest-station selection and accurate metre distances against known London
coordinates.

No persistence changes, no schema changes, no enrichment of `Property`/`Listing`,
no API, no UI, no scoring.

## Decisions (resolved with the user)

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **Database-side PostGIS computation**, not in-memory C# Haversine. | The Phase 1 migration deliberately created the generated `location` geometry columns and GiST indexes "to back the nearest-station / distance queries from Phase 3". DB-side reuses that infrastructure and scales to bulk per-property lookups in Phase 8 (one indexed KNN lookup beats reloading all stations). |
| 2 | **Order by the indexed KNN operator `<->`, then measure metres with `ST_Distance(::geography)`.** Fetch the top **k = 5** candidates by planar `<->`, compute true spheroidal metres for each, return the minimum. | The column is `geometry(Point,4326)`, so `ST_Distance`/`<->` on it return **degrees**, not metres, and planar `<->` ordering can rarely mis-rank near-equidistant stations. `<->` uses the GiST index (fast candidate fetch); the `::geography` cast yields spheroid-accurate metres; re-ranking the top 5 by geography distance makes the result airtight. No schema change (no geography column added). |
| 3 | **Lives in `shared/Infrastructure/Spatial/`** as `INearestStationFinder` + `NearestStationFinder`, following the existing per-feature folder convention (`Stations/`). Not a separate project. | It is DB-backed (needs `PropertyDbContext`/Npgsql), so it cannot be an infrastructure-free standalone library; a separate project would add a reference hop for no isolation benefit. The "reusable library" intent is met by a clean injectable interface in the shared assembly. |
| 4 | **Raw parameterised SQL** (via EF `Database.SqlQuery<T>` / `FromSql`), no NetTopologySuite dependency. | Expresses the `<->`-then-`::geography` pattern directly. NTS/LINQ maps `.Distance()` on a `geometry(4326)` column to degree-based `ST_Distance`, making metres awkward and pushing toward a geography column — fighting the abstraction. Existing tests already drop to SQL for the `location` column, so this matches precedent. Parameters are passed safely (no string interpolation into SQL). |
| 5 | **Returns the domain `Station` entity + metres**, via `NearestStationResult(Station Station, double DistanceMeters)`. Returns **`null`** when no stations exist. | Simplest useful shape; Phase 8 (the consumer, in the processor) works with entities anyway. `null` is the defined empty-table behaviour. |
| 6 | **Single nearest only — no mode filtering, no k-nearest-N, no per-mode nearest.** | YAGNI until a consumer needs them. Keeps the phase to its narrow acceptance criterion. |
| 7 | **Read-only: no writes, no new columns, no migration.** Out-of-range coordinates are not validated (PostGIS handles them); the finder just computes. | Persisting nearest-station onto `Property` is Phase 8; scoring is Phase 9; API is Phase 10. Phase 3 only proves the calculation. |
| 8 | **Connection config reuses the Phase 1/2 contract** (`UsePropertySearchNpgsql`, `PROPERTYSEARCH_DB`). Tests use the existing `PostgresFixture` (Testcontainers PostGIS + Respawn). | One connection-config path everywhere; tests already have the spatial-capable container and reset machinery. |

## Query design

For a query point built once as `ST_SetSRID(ST_MakePoint(@lon, @lat), 4326)`:

```sql
SELECT id,
       ST_Distance(location::geography, ST_SetSRID(ST_MakePoint(@lon, @lat), 4326)::geography) AS distance_m
FROM stations
ORDER BY location <-> ST_SetSRID(ST_MakePoint(@lon, @lat), 4326)   -- indexed planar KNN, cheap candidate fetch
LIMIT 5;
```

The service then:
1. Reads the `(id, distance_m)` candidate rows.
2. If none, returns `null`.
3. Picks the row with the **minimum `distance_m`** (geography re-rank, corrects any
   planar `<->` mis-rank among the top 5).
4. Materialises that `Station` via `PropertyDbContext` and returns
   `new NearestStationResult(station, distanceM)`.

`@lon`/`@lat` are bound as parameters (never interpolated into the SQL text).

## Deliverables

### 1. Infrastructure (`shared/Infrastructure`)
- `Spatial/INearestStationFinder.cs`:
  ```csharp
  public interface INearestStationFinder
  {
      Task<NearestStationResult?> FindNearestAsync(
          double latitude, double longitude, CancellationToken cancellationToken = default);
  }

  public sealed record NearestStationResult(Station Station, double DistanceMeters);
  ```
- `Spatial/NearestStationFinder.cs` — PostGIS implementation per **Query design**,
  taking `PropertyDbContext` via constructor injection.
- (Internal) a small `(long Id, double DistanceMeters)` projection type for the
  candidate query if needed by `Database.SqlQuery<T>`.

No domain change, no configuration change, no migration, no new package.

### 2. Tests (`tests/PropertySearch.Infrastructure.Tests`)
Integration tests using the existing `PostgresFixture` (Respawn-reset):

**Primary — seeded known stations** (a handful inserted directly with real coordinates,
e.g. King's Cross St Pancras, Euston, Angel, Waterloo):
  1. A query point at/near King's Cross returns **King's Cross St Pancras**.
  2. The returned `DistanceMeters` matches the expected great-circle distance
     within tolerance (`BeApproximately`, a few metres — spheroid `ST_Distance`
     won't exactly equal a hand-computed Haversine).
  3. A point positioned to be only marginally closer to one of two nearby stations
     returns the **truly closer** one (exercises the geography re-rank vs planar `<->`).
  4. An **empty `stations` table returns `null`**.

**Smoke — real dataset:** run the real `StationImportService` over the embedded
`london-stations.json`, query a known London coordinate (e.g. King's Cross), and
assert a result is returned with a **sensibly small** distance (no brittle name
assertion) — proving the finder works at full ~300-station scale.

### 3. Solution & docs
- No new project. (`Infrastructure.Tests` already references Infrastructure.)
- README: a short **"Nearest station"** note pointing at `INearestStationFinder`
  as the Phase 3 spatial entry point (optional, brief).

## Acceptance Criteria

1. Given a latitude/longitude, `FindNearestAsync` returns the nearest `Station`
   and a **distance in metres** computed on the WGS84 spheroid.
2. For a King's-Cross query against seeded data, the result is
   **King's Cross St Pancras** with distance accurate to within a few metres.
3. When two stations are near-equidistant, the **truly closer** one is returned
   (geography re-rank, not raw planar `<->`).
4. An empty `stations` table yields **`null`**.
5. The smoke test against the real imported dataset returns a sensible nearest
   result for a known coordinate.
6. All unit + integration tests pass (`dotnet test`).
7. `dotnet build` succeeds with **zero warnings** (warnings-as-errors).

## Verification

```bash
# 1. Schema + data present
docker compose up -d
dotnet ef database update --project shared/Infrastructure
dotnet run --project src/StationImporter            # 300+ stations (Phase 2)

# 2. Tests (seeded + real-dataset smoke) + clean build
dotnet test
dotnet build

# 3. (Optional) sanity-check the query directly against the compose DB:
#    nearest station to King's Cross (lon -0.1238, lat 51.5308), distance in metres
docker compose exec db psql -U propertysearch -d propertysearch -c \
  "SELECT name,
          ST_Distance(location::geography,
                      ST_SetSRID(ST_MakePoint(-0.1238, 51.5308), 4326)::geography) AS distance_m
   FROM stations
   ORDER BY location <-> ST_SetSRID(ST_MakePoint(-0.1238, 51.5308), 4326)
   LIMIT 5;"
```

## Explicit Non-Goals (deferred)

- Persisting nearest station / distance onto `Property` or `Listing` → Phase 8
- Using distance in scoring → Phase 9
- Exposing distance via API → Phase 10
- Mode filtering (nearest Underground), k-nearest-N, per-mode nearest → not needed yet
- Adding a `geography` column / new migration → not needed (geometry + cast suffices)
- NetTopologySuite mapping of the `location` column → not needed (raw SQL)
- Commute-time / journey calculations → Phase 15

## Proposed file tree after Phase 3 (additions)

```text
PropertySearch/
└── shared/
    └── Infrastructure/
        └── Spatial/                       (new)
            ├── INearestStationFinder.cs   (+ NearestStationResult record)
            └── NearestStationFinder.cs
└── tests/
    └── PropertySearch.Infrastructure.Tests/
        └── NearestStationFinderTests.cs   (new)
```
