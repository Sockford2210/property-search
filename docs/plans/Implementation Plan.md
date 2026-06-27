# Property Aggregator - High-Level Implementation Plan

## Phase 0 - Repository & Infrastructure Foundation

### Goal

Create a runnable monorepo with local development infrastructure.

### Deliverables

* Monorepo structure
* Docker Compose environment
* PostgreSQL database
* PostGIS enabled
* Shared Domain project
* Shared Contracts project

### Acceptance Criteria

* `docker compose up` starts all infrastructure
* PostgreSQL accepts connections
* PostGIS extension installed
* Solution builds successfully

### Test

```text
Run docker compose up

Verify:
- Database container running
- PostGIS enabled
- Solution builds with no errors
```

---

# Phase 1 - Property Domain & Database

## Goal

Create the internal data model before scraping any data.

### Deliverables

Database schema:

* Properties
* Listings
* Sources
* Stations

Initial EF Core migrations.

### Acceptance Criteria

Can insert and retrieve:

* Property
* Listing
* Station

### Test

Create integration tests that:

* Create property
* Create listing
* Query listing

Verify relationships are correct.

### Output

Working database model.

---

# Phase 2 - Station Import Service

## Goal

Import London station data.

### Deliverables

Station importer.

Supported fields:

* Name
* Latitude
* Longitude
* Transport type

### Acceptance Criteria

Database populated with London stations.

### Test

Run importer.

Execute:

```sql
SELECT COUNT(*)
FROM Stations;
```

Expected:

```text
> 300 stations
```

### Output

Queryable station database.

---

# Phase 3 - Spatial Distance Calculations

## Goal

Prove that nearest-station calculations work.

### Deliverables

Distance calculation module.

### Acceptance Criteria

Given a latitude/longitude:

* Return nearest station
* Return distance in metres

### Test

Use several known London locations.

Example:

```text
King's Cross Station

Expected nearest station:
King's Cross St Pancras
```

### Output

Reusable spatial library.

---

# Phase 4 - Search Discovery Scraper

## Goal

Discover listing URLs from a property portal.

Start with Rightmove only.

### Deliverables

Search crawler.

Input:

```text
Rent range
Bedrooms
Location
```

Output:

```text
Listing URLs
```

### Acceptance Criteria

Returns listing URLs without parsing property details.

### Test

Execute search.

Verify:

```text
100+ URLs returned
```

Verify URLs are valid.

### Output

Independent URL discovery engine.

---

# Phase 5 - Listing Ingestion Scraper

## Goal

Extract structured data from a single listing.

### Deliverables

Listing parser.

Fields:

* Price
* Bedrooms
* Bathrooms
* Address
* Coordinates
* Description
* Images

### Acceptance Criteria

Can parse an individual listing URL.

### Test

Provide known URL.

Verify extracted values against webpage.

### Output

Working property parser.

---

# Phase 6 - Raw Data Persistence

## Goal

Persist scraped listings.

### Deliverables

Scraper database integration.

### Acceptance Criteria

Discovered listings are saved.

Updates modify existing records.

Duplicate records prevented.

### Test

Run scraper twice.

Verify:

```text
No duplicate listings created.
```

### Output

Persistent property dataset.

---

# Phase 7 - Property Normalisation

## Goal

Separate external listings from internal property records.

### Deliverables

Normalisation pipeline.

### Responsibilities

Convert:

```text
Portal-specific data
```

Into:

```text
Internal Property model
```

### Acceptance Criteria

Properties can be queried without knowing source portal.

### Test

Insert data from multiple sources.

Verify identical schema returned.

### Output

Source-independent data layer.

---

# Phase 8 - Property Enrichment

## Goal

Attach transport metadata.

### Deliverables

Enrichment processor.

Adds:

* Nearest station
* Distance to station

### Acceptance Criteria

All properties contain transport metadata.

### Test

Process sample properties.

Verify distances manually.

### Output

Enriched properties.

---

# Phase 9 - Property Scoring Engine

## Goal

Rank properties.

### Deliverables

Scoring service.

Inputs:

* Rent
* Bedrooms
* Bathrooms
* Station distance

Output:

```text
Property Score
```

### Acceptance Criteria

Scores generated consistently.

### Test

Create known sample properties.

Verify ranking order.

### Output

Comparable property rankings.

---

# Phase 10 - Search API

## Goal

Expose property data.

### Deliverables

REST API.

Endpoints:

```text
GET /properties

GET /properties/{id}

GET /search
```

### Acceptance Criteria

Can filter by:

* Rent
* Bedrooms
* Bathrooms
* Station distance

### Test

Run API integration tests.

### Output

Queryable service.

---

# Phase 11 - Basic Web UI

## Goal

Visualise properties.

### Deliverables

Property list page.

Property details page.

### Acceptance Criteria

Can:

* Browse properties
* Filter properties
* Sort by score

### Test

Manual verification.

### Output

Usable application.

---

# Phase 12 - Saved Searches

## Goal

Persist search criteria.

### Deliverables

Saved search functionality.

### Acceptance Criteria

Users can create:

```text
Rent < £2000
Bedrooms >= 2
Station Distance < 800m
```

### Test

Retrieve saved search.

Verify criteria persisted.

### Output

Reusable searches.

---

# Phase 13 - Alert Engine

## Goal

Notify when matching properties appear.

### Deliverables

Background alert service.

### Acceptance Criteria

Detect:

* New matching property
* Price reduction

### Test

Insert matching property.

Verify notification generated.

### Output

Working alerts.

---

# Phase 14 - Additional Property Sources

## Goal

Support additional portals.

### Deliverables

Zoopla provider.

OnTheMarket provider.

### Acceptance Criteria

Providers operate independently.

### Test

Enable provider.

Verify listings imported.

### Output

Multi-source platform.

---

# Phase 15 - Commute-Time Calculations

## Goal

Replace simple station-distance ranking.

### Deliverables

Journey-time engine.

### Acceptance Criteria

Can calculate:

```text
Property
→ Destination
→ Estimated travel time
```

### Test

Known routes compared against TfL journey planner.

### Output

Commute-aware rankings.