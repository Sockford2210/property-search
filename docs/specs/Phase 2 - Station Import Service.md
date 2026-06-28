# Phase 2 - Station Import Service — Spec

**Status:** Ready for implementation
**Source plan:** `docs/plans/Implementation Plan.md` (Phase 2)
**Depends on:** Phase 1 (complete — `stations` table, generated `location`, GiST index exist)
**Date:** 2026-06-28

## Goal

Populate the `stations` table with Greater London public-transport stations so
that Phase 3 has real data to run nearest-station / distance queries against.

Phase 2 delivers: a **committed station dataset**, a **reusable import service**
(idempotent upsert), the **first runnable executable** in the repo (a console
host), the **schema change** that resolves Phase 1's deferred station natural
key, and integration tests proving the database ends up with **300+ stations**
and that re-running the importer never duplicates rows.

No scraping, no API, no spatial maths (the generated `location` column already
computes geometry from lat/long), no UI.

## Decisions (resolved with the user)

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **Committed static dataset.** A version-controlled file derived from TfL / NaPTAN open data is the source of truth; the importer only ever reads it. No live API call at run time or test time. | Deterministic, offline, no app key, no rate limits — matches the repo's `initdb`/Testcontainers philosophy. CLAUDE.md's "TfL station data" is honoured by *provenance*, not by a runtime dependency. Refreshing the dataset is a manual, reviewable commit. |
| 2 | **Console app host under `src/`.** New `src/StationImporter` (`PropertySearch.StationImporter`) — the repo's first executable. It is a thin host: build config + DbContext, resolve the import service, run, log a summary. | A one-shot/occasional idempotent seed load does not need an always-on worker. A future worker can call the same service. |
| 3 | **Import logic lives in `shared/Infrastructure`** (`Stations/` + embedded dataset under `Data/`), not in the exe. The console app and the existing `Infrastructure.Tests` both reference it. | Keeps the logic reusable and lets the existing test project exercise it without referencing an exe. |
| 4 | **One row per physical station; single primary `Mode` by precedence.** Multi-mode interchanges (e.g. Stratford) collapse to one row. Precedence: `Underground > ElizabethLine > Overground > DLR > NationalRail`. | Cleanest input for Phase 3 nearest-station (no duplicate coordinates). Keeps the Phase 1 single-`Mode` schema. Precedence is a documented constant, easily adjusted. |
| 5 | **Natural key = `station_code`** (new `text`, `NOT NULL`, **unique** column on `stations`), populated from the dataset's stable per-physical-station identifier (NaPTAN/TfL station code). This is the **upsert key** and resolves Phase 1's deferred station unique key. | Idempotency needs a stable business key; name alone is not safe (duplicates/renames). |
| 6 | **Idempotent upsert by `station_code`.** Import groups dataset rows by code, selects primary mode, then for each: insert if new, update `Name`/`Mode`/`Latitude`/`Longitude` when changed, leave untouched otherwise. Re-running is a no-op. | Acceptance requires "run importer (again) → no duplicates". `UpdatedAt` is maintained by the DbContext; `location` recomputes automatically (generated column). |
| 7 | **JSON dataset + `System.Text.Json`**, embedded as a resource in Infrastructure. | Zero new parsing dependency (in-box `System.Text.Json`); embedding removes file-path fragility in the exe and in tests. (CSV + CsvHelper is the noted alternative if a tabular source is preferred.) |
| 8 | **No auto-migrate-on-startup** (consistent with Phase 1). The importer assumes the schema exists; `dotnet ef database update` is run first. Tests auto-migrate via the existing `PostgresFixture`. | Keeps schema application explicit and in one place. |
| 9 | **Connection config reuses the Phase 1 contract:** `PROPERTYSEARCH_DB` env var (same localhost:5433 default) via `UsePropertySearchNpgsql`. | One connection-config path everywhere; no secrets committed. |

## Dataset

**File:** `shared/Infrastructure/Data/london-stations.json` (committed, embedded
resource).

**Scope:** Greater London stations across the five modes
(`Underground`, `DLR`, `Overground`, `ElizabethLine`, `NationalRail`).

**Shape:** an array of records. The dataset may contain one row per
(station, mode) — the importer collapses them to one physical station.

```jsonc
[
  {
    "code": "940GZZLUKSX",        // stable per-physical-station identifier → station_code
    "name": "King's Cross St. Pancras",
    "latitude": 51.5302,
    "longitude": -0.1238,
    "mode": "Underground"          // one of the TransportMode names
  }
  // ...
]
```

Notes:
- `code` must be stable across dataset refreshes and identify the *physical*
  station (interchange), so all of an interchange's mode rows share one `code`.
  If a chosen source keys per-mode/per-platform, derive the physical code (e.g.
  NaPTAN station group / TfL hub id) before committing the file.
- A short `Data/README.md` records provenance (source URL, date pulled, any
  filtering) so the file can be regenerated.
- Candidate sources: TfL Unified API `StopPoint` export, TfL open-data station
  CSVs, or NaPTAN — converted once to the JSON shape above and committed.

## Schema change

New migration (e.g. `AddStationCode`) on the `stations` table:

```sql
ALTER TABLE stations ADD COLUMN station_code text NOT NULL;
CREATE UNIQUE INDEX ux_stations_station_code ON stations (station_code);
```

(No data exists in `stations` yet, so `NOT NULL` needs no backfill.) The
generated `location` column and its GiST index from Phase 1 are unchanged.

## Deliverables

### 1. Domain (`shared/Domain`)
- `Station`: add `public required string StationCode { get; set; }`.
  (No infrastructure dependency; still a POCO.)

### 2. Infrastructure (`shared/Infrastructure`)
- `Configurations/StationConfiguration`: map `StationCode` (`IsRequired`) +
  unique index `ux_stations_station_code`.
- `Stations/StationRecord.cs` — DTO matching the dataset row.
- `Stations/IStationDataSource` + `EmbeddedStationDataSource` — reads & deserialises
  the embedded `london-stations.json`.
- `Stations/StationModePrecedence.cs` — the precedence constant + a pure
  `SelectPrimaryMode(IEnumerable<TransportMode>)` function.
- `Stations/StationImportService.cs` — groups records by `code`, selects primary
  mode, performs the idempotent upsert via `PropertyDbContext`, returns a
  summary (`Inserted`, `Updated`, `Unchanged`, `Total`).
- `Migrations/` — the `AddStationCode` migration + updated model snapshot.
- `.csproj`: `<EmbeddedResource Include="Data\london-stations.json" />`.

### 3. Console host (`src/StationImporter` — new)
- `PropertySearch.StationImporter.csproj` (exe, `net10.0`, references Infrastructure).
- `Program.cs`: Generic Host (`Host.CreateApplicationBuilder`); register
  `PropertyDbContext` via `UsePropertySearchNpgsql(PROPERTYSEARCH_DB)`,
  `EmbeddedStationDataSource`, `StationImportService`; run the import; log the
  summary; exit non-zero on failure.

### 4. Tests (`tests/PropertySearch.Infrastructure.Tests`)
- Unit test for `SelectPrimaryMode` (precedence; no DB).
- Integration tests (existing `PostgresFixture`, Respawn-reset):
  1. Import populates **> 300** stations.
  2. **Idempotency:** importing twice leaves the count unchanged and creates no
     duplicates; second run reports `Inserted = 0`.
  3. A known interchange (e.g. Stratford) is a **single row** with the expected
     primary mode.
  4. A known station (e.g. King's Cross St Pancras) has expected lat/long and a
     populated generated `location` (verify via `ST_X`/`ST_Y` within tolerance).
  5. `station_code` uniqueness holds (no duplicate codes after import).

### 5. Packages (`Directory.Packages.props`)
- Add `Microsoft.Extensions.Hosting` (console host).
- (`System.Text.Json` is in-box; logging abstractions come transitively via EF Core.)

### 6. Solution & docs
- `PropertySearch.slnx`: add a `/src/` folder containing `src/StationImporter`.
- README: a **"Station import"** section — run order
  (`docker compose up -d` → `dotnet ef database update` →
  `dotnet run --project src/StationImporter`), the `PROPERTYSEARCH_DB` env var,
  and how to refresh the dataset.

## Acceptance Criteria

1. After `dotnet ef database update` + running the importer against the compose
   DB, `SELECT COUNT(*) FROM stations;` returns **> 300**.
2. Running the importer a second time produces **no new rows** and no errors
   (summary reports `Inserted = 0`).
3. Every multi-mode interchange is a single row with a primary mode per the
   precedence order; `station_code` is unique.
4. Each station's generated `location` is populated and consistent with its
   lat/long (`ST_X`/`ST_Y`).
5. All unit + integration tests pass (`dotnet test`).
6. `dotnet build` succeeds with **zero warnings** (warnings-as-errors).

## Verification

```bash
# 1. Schema up to date
docker compose up -d
dotnet ef database update --project shared/Infrastructure

# 2. Run the importer (first load)
dotnet run --project src/StationImporter

# 3. Count + idempotency
docker compose exec db psql -U propertysearch -d propertysearch -c "SELECT COUNT(*) FROM stations;"   # > 300
dotnet run --project src/StationImporter                                                              # Inserted = 0
docker compose exec db psql -U propertysearch -d propertysearch -c "SELECT COUNT(*) FROM stations;"   # unchanged

# 4. Spot-check a station + its geometry
docker compose exec db psql -U propertysearch -d propertysearch -c \
  "SELECT name, mode, ST_AsText(location) FROM stations WHERE name ILIKE '%king%cross%';"

# 5. Tests + clean build
dotnet test
dotnet build
```

## Explicit Non-Goals (deferred)

- Live TfL API integration / scheduled refresh → out of scope (manual dataset refresh)
- Nearest-station / distance calculations → Phase 3
- Station ↔ Property links → Phase 8
- Storing *all* modes per station (multi-mode set) → not needed; primary mode only
- A Worker Service host, auto-migrate-on-startup → not needed for a one-shot seed
- CI pipeline → deferred

## Proposed file tree after Phase 2 (additions)

```text
PropertySearch/
├── PropertySearch.slnx              (+ /src/ folder, StationImporter)
├── Directory.Packages.props         (+ Microsoft.Extensions.Hosting)
├── src/                             (new)
│   └── StationImporter/
│       ├── PropertySearch.StationImporter.csproj
│       └── Program.cs
├── shared/
│   ├── Domain/
│   │   └── Station.cs               (+ StationCode)
│   └── Infrastructure/
│       ├── Configurations/StationConfiguration.cs   (+ station_code + unique index)
│       ├── Data/
│       │   ├── london-stations.json (committed, embedded)
│       │   └── README.md            (provenance)
│       ├── Stations/
│       │   ├── StationRecord.cs
│       │   ├── IStationDataSource.cs
│       │   ├── EmbeddedStationDataSource.cs
│       │   ├── StationModePrecedence.cs
│       │   └── StationImportService.cs
│       └── Migrations/              (+ AddStationCode + snapshot)
└── tests/
    └── PropertySearch.Infrastructure.Tests/
        ├── StationImportTests.cs
        └── StationModePrecedenceTests.cs
```
