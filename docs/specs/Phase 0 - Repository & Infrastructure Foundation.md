# Phase 0 - Repository & Infrastructure Foundation — Spec

**Status:** Ready for implementation
**Source plan:** `docs/plans/Implementation Plan.md` (Phase 0)
**Date:** 2026-06-27

## Goal

Stand up a runnable, version-controlled .NET 10 monorepo with local PostgreSQL +
PostGIS infrastructure. Phase 0 proves **plumbing only** — no domain model, no EF
Core, no service projects, no scraping. The repo must build and the database must
accept connections with PostGIS active.

## Decisions (resolved during grilling)

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **Minimal scaffolding.** Create only `shared/Domain`, `shared/Contracts`, and the root `.slnx`. `Infrastructure` and all five service projects are deferred until the phase that first needs them. | Keeps every phase's solution genuinely buildable; avoids empty placeholder projects. |
| 2 | **Git + hygiene bundle** (see below), with `TreatWarningsAsErrors=true` and **Central Package Management** set up now. | Consistent settings inherited by all future projects; cheaper now than retrofitting. |
| 3 | **`postgis/postgis:17-3.5`** image; PostGIS enabled via an **initdb SQL script** (`CREATE EXTENSION IF NOT EXISTS postgis;`). | Deterministic, version-controlled, verifiable on `docker compose up` with zero app code. Phase 1's EF model will additionally declare `HasPostgresExtension("postgis")` for idempotency. |
| 4 | Host port **5433→5432**; compose at **repo root**; db/user `propertysearch`; password via git-ignored `.env` (+ committed `.env.example`); named volume `pgdata`; `pg_isready` healthcheck; `restart: unless-stopped`. | Avoids collisions with any system Postgres; `docker compose up` works without `-f`. CLAUDE.md updated to match. |
| 5 | `Domain` and `Contracts` are **empty** (plumbing only) in Phase 0. | Entity/DTO design belongs to Phase 1, after EF/PostGIS shapes it. |
| 6 | **`.slnx`** solution format; `shared` solution folder; **`PropertySearch.*`** namespace/assembly prefix. | Greenfield repo; cleaner merges; avoids namespace collisions. |
| 7 | Verification is **README-documented + manual**. No CI, no test project, no Dockerfiles for .NET services in Phase 0. | First tests are Phase 1 integration tests; CI deferred. |

## Deliverables

### 1. Version control & repo hygiene
- `git init` with default branch `main`
- `.gitignore` (.NET): `bin/`, `obj/`, `.vs/`, `*.user`, plus `node_modules/` and `.env`
- `.editorconfig`: C# conventions — `nullable`, file-scoped namespaces, 4-space indent
- `Directory.Build.props` (root), inherited by all `.csproj`:
  - `TargetFramework=net10.0`
  - `Nullable=enable`
  - `ImplicitUsings=enable`
  - `LangVersion=latest`
  - `TreatWarningsAsErrors=true`
- `Directory.Packages.props` (root): `ManagePackageVersionsCentrally=true` (empty `<ItemGroup>` for now — no packages referenced yet)

### 2. Solution & shared projects
- `PropertySearch.slnx` at repo root
- `shared` solution folder containing:
  - `shared/Domain/PropertySearch.Domain.csproj` — class library, `net10.0`, empty (no `.cs` types)
  - `shared/Contracts/PropertySearch.Contracts.csproj` — class library, `net10.0`, empty
- Both inherit `Directory.Build.props`; no `<TargetFramework>`/`<Nullable>` duplicated in the csproj

### 3. Infrastructure (Docker Compose at repo root)
- `docker-compose.yml`:
  - Service `db`: image `postgis/postgis:17-3.5`
  - Env from `.env`: `POSTGRES_DB=propertysearch`, `POSTGRES_USER=propertysearch`, `POSTGRES_PASSWORD=${POSTGRES_PASSWORD}`
  - Ports: `5433:5432`
  - Volume: `pgdata:/var/lib/postgresql/data`
  - Init script mount: `./db/init/01-postgis.sql:/docker-entrypoint-initdb.d/01-postgis.sql:ro`
  - Healthcheck: `pg_isready -U propertysearch -d propertysearch`
  - `restart: unless-stopped`
- `db/init/01-postgis.sql`: `CREATE EXTENSION IF NOT EXISTS postgis;`
- `.env.example` (committed): documents `POSTGRES_PASSWORD=` (and any other keys)
- `.env` (git-ignored): real local dev password

### 4. Documentation
- Root `README.md` with:
  - Prerequisites (.NET 10 SDK, Docker Desktop)
  - Getting started (`cp .env.example .env`, set password, `docker compose up -d`)
  - **Verify Phase 0** section (see Acceptance below)
- CLAUDE.md updated to note compose lives at repo root (✅ done)

## Acceptance Criteria

1. `docker compose up -d` starts the `db` container; healthcheck reports healthy.
2. PostgreSQL accepts connections on host port **5433**.
3. PostGIS extension is active in the `propertysearch` database.
4. `dotnet build` succeeds with **zero warnings** (warnings-as-errors).

## Verification (manual, documented in README)

```bash
# 1. Infrastructure up
docker compose up -d

# 2. PostGIS active (prints a version string, not an error)
docker compose exec db psql -U propertysearch -d propertysearch -c "SELECT postgis_version();"

# 3. Solution compiles clean
dotnet build
```

Expected: container healthy, `postgis_version()` returns e.g. `3.5 USE_GEOS=1 ...`,
build reports `Build succeeded` with 0 warnings / 0 errors.

## Explicit Non-Goals (deferred to later phases)

- EF Core packages, `DbContext`, or migrations → Phase 1
- Any domain entities (`Property`, `Listing`, `Station`, `Source`) or DTOs → Phase 1
- `shared/Infrastructure` project → created when first needed
- Service projects (`PropertyScraper`, `PropertyProcessor`, `PropertyApi`, `PropertyWeb`, `PropertyAlerts`) → their respective phases
- Dockerfiles for .NET services (only the DB is containerised in Phase 0)
- Connection-string wiring into app code → Phase 1
- Test projects → Phase 1 (first integration tests)
- CI pipeline → deferred

## Proposed file tree after Phase 0

```text
PropertySearch/
├── .editorconfig
├── .env.example
├── .gitignore
├── CLAUDE.md
├── Directory.Build.props
├── Directory.Packages.props
├── PropertySearch.slnx
├── README.md
├── docker-compose.yml
├── db/
│   └── init/
│       └── 01-postgis.sql
├── docs/
│   ├── plans/
│   │   └── Implementation Plan.md
│   └── specs/
│       └── Phase 0 - Repository & Infrastructure Foundation.md
└── shared/
    ├── Contracts/
    │   └── PropertySearch.Contracts.csproj
    └── Domain/
        └── PropertySearch.Domain.csproj
```
