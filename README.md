# PropertySearch

A property aggregation and ranking platform for London rental properties. It
collects listings from property portals, normalises them into a common model,
enriches them with transport data, scores them, and exposes the results via an
API and web app. See [CLAUDE.md](CLAUDE.md) for the full design and
[docs/plans/Implementation Plan.md](docs/plans/Implementation%20Plan.md) for the
phased roadmap.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Docker Desktop](https://www.docker.com/) (Compose v2)

## Getting started

```bash
# 1. Create your local env file and set a database password
cp .env.example .env
#    then edit .env and set POSTGRES_PASSWORD

# 2. Start infrastructure (PostgreSQL + PostGIS)
docker compose up -d

# 3. Build the solution
dotnet build
```

PostgreSQL is exposed on host port **5433** (container 5432) to avoid clashing
with any system Postgres. Database/user are both `propertysearch`.

## Verify Phase 0

```bash
# Infrastructure up and healthy
docker compose up -d
docker compose ps           # 'db' should report (healthy)

# PostGIS extension is active (prints a version string)
docker compose exec db psql -U propertysearch -d propertysearch -c "SELECT postgis_version();"

# Solution compiles clean (0 warnings, warnings-as-errors is on)
dotnet build
```

Expected: the `db` container is healthy, `postgis_version()` returns something
like `3.5 USE_GEOS=1 USE_PROJ=1 USE_STATS=1`, and the build reports
`Build succeeded`.

## Database & migrations

The schema is managed with EF Core migrations in `shared/Infrastructure`. There is
no application host yet, so migrations are applied with the EF CLI.

```bash
# Install the EF CLI once (version must be >= the EF Core runtime, i.e. 10.x)
dotnet tool install --global dotnet-ef --version "10.*"

# Apply the schema to the local Compose database. The connection string comes
# from the PROPERTYSEARCH_DB env var; without it the design-time factory falls
# back to Host=localhost;Port=5433 with the default credentials. Set the password
# to match your .env (POSTGRES_PASSWORD).
PROPERTYSEARCH_DB="Host=localhost;Port=5433;Database=propertysearch;Username=propertysearch;Password=$POSTGRES_PASSWORD" \
  dotnet ef database update --project shared/Infrastructure

# Add a new migration later
dotnet ef migrations add <Name> --project shared/Infrastructure --output-dir Migrations
```

Coordinates are stored as plain `latitude`/`longitude` columns; a PostGIS
`geometry(Point,4326)` column (`location`) is **generated** from them in the
database, with a GiST index, ready for spatial queries in later phases.

## Station import

`src/StationImporter` loads London stations (~420) into the `stations` table from
a committed dataset (`shared/Infrastructure/Data/london-stations.json`, embedded
in `PropertySearch.Infrastructure`). The import is **idempotent** — it upserts by
`station_code`, so re-running changes nothing. Apply migrations first.

```bash
# Schema must be up to date (see above), then run the importer. Same
# PROPERTYSEARCH_DB env var; defaults to the local Compose database.
PROPERTYSEARCH_DB="Host=localhost;Port=5433;Database=propertysearch;Username=propertysearch;Password=$POSTGRES_PASSWORD" \
  dotnet run --project src/StationImporter
# -> Station import complete: 420 inserted, 0 updated, 0 unchanged, 420 total.
```

The dataset is generated from the [TfL Unified API](https://api.tfl.gov.uk/) by
`scripts/build-station-dataset.py` (Python 3, no app key). See
`shared/Infrastructure/Data/README.md` for provenance and how to refresh it.

## Search discovery

`RightmoveSearchDiscoveryService` (in `shared/Infrastructure/Sources/Rightmove/`) is
the Phase 4 entry point. Given a `RightmoveSearchCriteria` (location token, price
range, bedrooms, radius) it paginates Rightmove's search endpoint via an
`IPageFetcher` and returns a de-duplicated list of `SearchResultRef(ExternalId, Url)`.
The HTML is parsed by `RightmoveSearchParser`, which locates the embedded
`window.jsonModel` JSON blob using AngleSharp. `HttpPageFetcher` handles live HTTP
with Polly resilience and a configurable politeness delay; tests inject a fake
`IPageFetcher` and run offline. Register with `services.AddRightmoveScraper(config)`.

## Listing detail parsing

`RightmoveListingParser` (in `shared/Infrastructure/Sources/Rightmove/`) is the Phase 5
entry point. Given a detail-page HTML string it returns a `ParsedListing` record
`{ ExternalId, Url, DisplayAddress, RentPcm, Bedrooms, Bathrooms?, Latitude?,
Longitude?, Description? }`. The parser locates the embedded `window.PAGE_MODEL` JSON
blob using AngleSharp and deserialises it with `System.Text.Json`. Weekly rents are
converted to monthly (`amount × 52 / 12`, rounded). POA or missing required fields
raise `ListingParseException`. The parser is a pure function — no database, no HTTP.

## Tests

Integration tests live in `tests/PropertySearch.Infrastructure.Tests`. They use
[Testcontainers](https://dotnet.testcontainers.org/) to spin up a throwaway
PostGIS container, so **Docker must be running** — but `docker compose up` is not
required (the tests manage their own container).

```bash
dotnet test
```

## Repository layout

```text
PropertySearch.slnx        Solution (.slnx format)
Directory.Build.props      Shared MSBuild settings (net10.0, nullable, etc.)
Directory.Packages.props   Central Package Management
docker-compose.yml         Local infrastructure (PostgreSQL + PostGIS)
db/init/                   First-run database init scripts
src/StationImporter/       Console app: imports London stations (Phase 2)
shared/Domain/             Business entities (Property, Listing, Source, Station)
shared/Infrastructure/     EF Core DbContext, configurations, migrations, station import
shared/Contracts/          DTOs / message contracts (empty until needed)
tests/                     Integration tests (Testcontainers + PostGIS)
scripts/                   Data-prep tooling (station dataset generator)
docs/                      Plans and specs
```
