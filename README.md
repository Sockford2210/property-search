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

## Repository layout

```text
PropertySearch.slnx        Solution (.slnx format)
Directory.Build.props      Shared MSBuild settings (net10.0, nullable, etc.)
Directory.Packages.props   Central Package Management
docker-compose.yml         Local infrastructure (PostgreSQL + PostGIS)
db/init/                   First-run database init scripts
shared/Domain/             Business entities (empty until Phase 1)
shared/Contracts/          DTOs / message contracts (empty until Phase 1)
docs/                      Plans and specs
```
