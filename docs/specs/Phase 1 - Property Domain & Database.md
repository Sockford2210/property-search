# Phase 1 - Property Domain & Database — Spec

**Status:** Ready for implementation
**Source plan:** `docs/plans/Implementation Plan.md` (Phase 1)
**Depends on:** Phase 0 (complete)
**Date:** 2026-06-28

## Goal

Create the internal data model before any data is scraped. Phase 1 delivers the
EF Core domain entities, the `PropertyDbContext`, the initial migration (with
PostGIS generated geometry columns), and an integration test suite proving the
schema and relationships work against a real PostGIS database.

No scraping, no API, no enrichment, no service hosts. This phase is the **data
layer only**.

## Decisions (resolved during grilling)

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **Pure POCO Domain.** Entities are plain C# classes in `shared/Domain` with no EF attributes and no infrastructure packages. Coordinates are primitive `double Latitude/Longitude`; no `Point` type in Domain. All mapping lives in `shared/Infrastructure`. | Honors CLAUDE.md's "Domain must not depend on infrastructure libraries" literally. |
| 2 | **New projects:** `shared/Infrastructure/PropertySearch.Infrastructure.csproj` (DbContext, `IEntityTypeConfiguration`s, `Migrations/`, design-time factory) and `tests/PropertySearch.Infrastructure.Tests`. No service projects in Phase 1. | Keeps Phase 1 focused on the data layer; design-time factory lets `dotnet ef` work with no startup host. |
| 3 | **Relationship graph:** `Source 1—* Listing *—1 Property` with **nullable** `Listing.PropertyId`; `Station` standalone (no FKs). | A scraped Listing exists before normalisation links it to a canonical Property; the nearest-station link is Phase 8. |
| 4 | **Attributes duplicated.** `Listing` holds rich as-scraped values; `Property` holds lean canonical values (populated later by normalisation). Images + enrichment fields deferred. | Standard aggregation pattern; per-portal values differ from the reconciled canonical record. |
| 5 | **`long` (bigint) identity PKs** on all tables. Unique `(SourceId, ExternalId)` on `Listing`; unique `Code` on `Source`. `Station`/`Property` PK-only. | Single Postgres instance, no client-side IDs. Listing uniqueness is the Phase 6 dedup key; Station's natural key is deferred to Phase 2. |
| 6 | **Listing lifecycle now, raw payload deferred.** `Status` (string enum `Active`/`Removed`), `FirstSeenAt`, `LastSeenAt` added now. Raw `jsonb`/`RawListing` deferred to Phase 6. | Cheap, supports Phase 6 update/removal detection; raw store shape depends on the unbuilt scraper. |
| 7 | **PostGIS generated geometry columns now.** `geometry(Point,4326)` STORED columns generated from lat/long, with GiST indexes, on `Station`, `Listing`, and `Property`. Defined via raw SQL in the migration. Domain stays doubles-only. | One source of truth for coordinates (the doubles); the spatial column can't drift; Phase 3 queries an index that already exists. |
| 8 | **Generic auditing now, no soft-delete.** `CreatedAt`/`UpdatedAt` (`timestamptz`) on all tables — DB default on create, DbContext-maintained on update. | Universally useful for a data pipeline; `Listing.Status=Removed` already covers logical removal. |
| 9 | **snake_case naming** via `EFCore.NamingConventions`; plural table names (`properties`, `listings`, `sources`, `stations`). | Keeps hand-written SQL (generated columns, Phase 3 spatial queries, `psql`) idiomatic and unquoted. |
| 10 | **Testcontainers + xUnit + Respawn + FluentAssertions.** Throwaway `postgis/postgis:17-3.5` container per test run; migrations applied via `MigrateAsync()`; Respawn resets state between tests. | Real PostGIS required for generated columns/DDL; fully isolated and CI-ready; pins the production image. |
| 11 | **No seed data.** `Source` rows are introduced in Phase 14; Phase 1 tests create their own `Source`. | Keeps migrations data-free; canonical source list belongs with the multi-source phase. |
| 12 | **Money:** `numeric(10,2)`, GBP implicit (no currency column). `Listing.RentPcm` required, `Property.RentPcm` nullable. | GBP/London-only per CLAUDE.md; multi-currency is a non-goal. |
| 13 | **Connection config:** env-var-driven design-time factory (`PROPERTYSEARCH_DB`, default `Host=localhost;Port=5433;...`, password from `.env`). Local schema via `dotnet ef database update`; tests auto-migrate; no auto-migrate-on-startup. | No service host exists yet; no secrets committed. |
| 14 | **FK delete behavior:** `Listing.SourceId → Source` = `Restrict`; `Listing.PropertyId → Property` = `SetNull`. | Never orphan a portal's data; never lose scraped Listings because a derived Property was removed. |

## Data Model

### `sources`
| Column | Type | Null | Notes |
|--------|------|------|-------|
| id | bigint identity | no | PK |
| code | text | no | **unique**, slug e.g. `rightmove` |
| name | text | no | display name |
| base_url | text | yes | |
| enabled | boolean | no | |
| created_at | timestamptz | no | DB default `now()` |
| updated_at | timestamptz | no | maintained by DbContext |

### `listings`
| Column | Type | Null | Notes |
|--------|------|------|-------|
| id | bigint identity | no | PK |
| source_id | bigint | no | FK → sources (`Restrict`) |
| property_id | bigint | yes | FK → properties (`SetNull`) |
| external_id | text | no | portal's listing id; **unique with source_id** |
| url | text | no | |
| display_address | text | no | |
| rent_pcm | numeric(10,2) | no | GBP |
| bedrooms | int | no | studio = 0 |
| bathrooms | int | yes | |
| latitude | double precision | yes | |
| longitude | double precision | yes | |
| description | text | yes | |
| status | text | no | string enum `Active`/`Removed` |
| first_seen_at | timestamptz | no | |
| last_seen_at | timestamptz | no | |
| location | geometry(Point,4326) | yes | **generated** from lon/lat, GiST index |
| created_at | timestamptz | no | DB default `now()` |
| updated_at | timestamptz | no | maintained by DbContext |

Constraints: unique `(source_id, external_id)`; FK index on `source_id` and `property_id` (EF default); GiST index on `location`.

### `properties`
| Column | Type | Null | Notes |
|--------|------|------|-------|
| id | bigint identity | no | PK |
| display_address | text | yes | canonical, populated by normalisation |
| rent_pcm | numeric(10,2) | yes | |
| bedrooms | int | yes | |
| bathrooms | int | yes | |
| latitude | double precision | yes | |
| longitude | double precision | yes | |
| location | geometry(Point,4326) | yes | **generated** from lon/lat, GiST index |
| created_at | timestamptz | no | DB default `now()` |
| updated_at | timestamptz | no | maintained by DbContext |

(Enrichment fields — nearest station, distance, score — added in Phases 8–9.)

### `stations`
| Column | Type | Null | Notes |
|--------|------|------|-------|
| id | bigint identity | no | PK |
| name | text | no | |
| mode | text | no | string enum: `Underground`/`DLR`/`Overground`/`ElizabethLine`/`NationalRail` |
| latitude | double precision | no | |
| longitude | double precision | no | |
| location | geometry(Point,4326) | no | **generated** from lon/lat, GiST index |
| created_at | timestamptz | no | DB default `now()` |
| updated_at | timestamptz | no | maintained by DbContext |

(Natural/business unique key deferred to Phase 2 when the import format is known.)

### Generated geometry column (pattern, per table)

Added via raw SQL in the migration (EF cannot model generated geometry directly):

```sql
ALTER TABLE stations
  ADD COLUMN location geometry(Point, 4326)
  GENERATED ALWAYS AS (ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)) STORED;
CREATE INDEX ix_stations_location ON stations USING GIST (location);
```

`location` is a DB-side shadow column — not mapped as a writable property on the
Domain entity. (It may be declared as an EF *shadow property* later if Phase 3
needs to project it; for Phase 1 it exists purely in the database.)

## Deliverables

### 1. Domain (`shared/Domain`)
- POCO entities: `Source`, `Listing`, `Property`, `Station`, all with `long Id`,
  `CreatedAt`/`UpdatedAt`, and navigation properties matching the graph.
- Enums: `ListingStatus { Active, Removed }`, `TransportMode { Underground, Dlr, Overground, ElizabethLine, NationalRail }`.
- No package references; no EF attributes; no spatial types.

### 2. Infrastructure (`shared/Infrastructure` — new)
- `PropertyDbContext` with `DbSet`s for the four entities.
- `IEntityTypeConfiguration<T>` per entity (keys, FKs + delete behavior, unique
  indexes, column types, nullability, `numeric(10,2)`, string-enum conversions).
- `SaveChanges`/`SaveChangesAsync` override maintaining `UpdatedAt` (and `CreatedAt` on add).
- `.UseSnakeCaseNamingConvention()`.
- `Migrations/` — initial migration including the raw-SQL generated geometry
  columns + GiST indexes, and `migrationBuilder.AlterDatabase().HasPostgresExtension("postgis")` for idempotency.
- `DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PropertyDbContext>`
  reading `PROPERTYSEARCH_DB` with the localhost:5433 default.

### 3. Tests (`tests/PropertySearch.Infrastructure.Tests` — new)
- xUnit collection fixture starting a `Testcontainers.PostgreSql` container on the
  `postgis/postgis:17-3.5` image and running `MigrateAsync()`.
- Respawn-based reset between tests.
- FluentAssertions.
- Tests covering the acceptance criteria (below).

### 4. Packages (Central Package Management — `Directory.Packages.props`)
- `Microsoft.EntityFrameworkCore` / `.Design` / `.Relational`
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `EFCore.NamingConventions`
- Test: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`,
  `Testcontainers.PostgreSql`, `Respawn`, `FluentAssertions`, `Npgsql`

(Pin to the latest stable versions compatible with EF Core 10 / .NET 10 at implementation time.)

### 5. Documentation
- README: a **"Database / migrations"** section documenting
  `dotnet ef migrations add` and `dotnet ef database update`, the
  `PROPERTYSEARCH_DB` env var, and how to run the integration tests.

## Acceptance Criteria

1. `dotnet ef database update` applies the initial migration to the compose DB on
   `localhost:5433`; all four tables plus generated geometry columns and GiST
   indexes exist.
2. Integration tests pass: insert and retrieve a `Property`, a `Listing`
   (referencing a created `Source` and optionally a `Property`), and a `Station`.
3. A `Listing → Source` and `Listing → Property` relationship round-trips
   correctly (FK loads, nullable Property link works).
4. Unique constraint on `(source_id, external_id)` rejects a duplicate listing.
5. Inserting a `Station` with lat/long auto-populates its `location` geometry
   (verified via `ST_X`/`ST_Y` or `ST_AsText`).
6. `dotnet build` succeeds with zero warnings (warnings-as-errors still on).

## Verification

```bash
# 1. Apply schema to the running compose DB
docker compose up -d
dotnet ef database update --project shared/Infrastructure

# 2. Inspect schema (tables, generated column, indexes)
docker compose exec db psql -U propertysearch -d propertysearch -c "\d listings"
docker compose exec db psql -U propertysearch -d propertysearch -c "\d stations"

# 3. Run the integration test suite (spins its own Testcontainer)
dotnet test

# 4. Clean build
dotnet build
```

Expected: schema present with `location` generated columns and GiST indexes; all
integration tests green; build succeeds, 0 warnings.

## Explicit Non-Goals (deferred to later phases)

- Seed data for `Source` (Rightmove etc.) → Phase 14
- Raw portal payload storage (`jsonb`/`RawListing`) → Phase 6
- `Listing.Images` collection → Phase 5
- Station natural/business unique key → Phase 2
- Property enrichment fields (nearest station, distance, score) → Phases 8–9
- Any service host, auto-migrate-on-startup, scraping, API, or UI → their phases
- CI pipeline → deferred

## Proposed file tree after Phase 1 (additions)

```text
PropertySearch/
├── PropertySearch.slnx              (+ Infrastructure & test projects)
├── Directory.Packages.props         (+ EF/Npgsql/test package versions)
├── shared/
│   ├── Domain/
│   │   ├── Source.cs
│   │   ├── Listing.cs
│   │   ├── Property.cs
│   │   ├── Station.cs
│   │   └── Enums/ (ListingStatus, TransportMode)
│   ├── Contracts/                   (unchanged — empty)
│   └── Infrastructure/              (new)
│       ├── PropertySearch.Infrastructure.csproj
│       ├── PropertyDbContext.cs
│       ├── DesignTimeDbContextFactory.cs
│       ├── Configurations/ (SourceConfiguration, ListingConfiguration, ...)
│       └── Migrations/
└── tests/                           (new)
    └── PropertySearch.Infrastructure.Tests/
        ├── PropertySearch.Infrastructure.Tests.csproj
        ├── PostgresFixture.cs       (Testcontainers + Respawn)
        └── *Tests.cs
```
