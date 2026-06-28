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
shared/Domain/             Business entities (Property, Listing, Source, Station)
shared/Infrastructure/     EF Core DbContext, configurations, migrations
shared/Contracts/          DTOs / message contracts (empty until needed)
tests/                     Integration tests (Testcontainers + PostGIS)
docs/                      Plans and specs
```
