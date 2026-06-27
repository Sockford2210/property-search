# AGENTS.md

## Project Overview

This project is a property aggregation and ranking platform focused on London rental properties.

The system collects rental listings from multiple property portals, normalises the data into a common model, enriches listings with transport information, calculates custom scores, and exposes the results through an API and web application.

The primary user goal is to discover rental properties based on:

* Monthly rent (£ PCM)
* Number of bedrooms
* Number of bathrooms
* Distance to nearest station

The system should eventually support additional ranking factors such as:

* Commute time
* EPC rating
* Parking availability
* Garden availability
* Furnished status
* Property type
* Listing age
* Price reductions

The platform is intended for personal use and learning purposes.

---

# Core Design Principles

## Property Portals Are Data Sources

Property portals are treated as external data providers.

The platform should not depend on any single provider.

The ingestion layer should be designed so that additional sources can be added with minimal changes.

Examples:

* Rightmove
* Zoopla
* OnTheMarket
* Individual estate agents

---

## Normalised Internal Model

All external listings should be converted into a common internal format before storage.

The rest of the system must not contain portal-specific logic.

---

## Enrichment Over Filtering

Rather than relying on portal-provided filters, the platform should enrich listings with its own metadata.

Examples:

* Calculate nearest station
* Calculate station distance
* Calculate commute times
* Calculate property score

---

## Eventual Goal

A user should be able to define a search profile such as:

* Rent < £2,000 PCM
* 2+ bedrooms
* At least 2 bathrooms
* Station within 750m

and receive alerts whenever matching properties appear.

---

# Monorepo Structure

```text
/src

    /PropertyScraper

    /PropertyProcessor

    /PropertyApi

    /PropertyWeb

    /PropertyAlerts

/shared

    /Contracts

    /Domain

    /Infrastructure

/deploy

docker-compose.yml   (repo root)

.env / .env.example  (repo root)

/docs
```

Note: `docker-compose.yml` lives at the repository root (not under `/deploy`) so
`docker compose up` works without `-f`. The `/deploy` folder is reserved for
production/host deployment assets added later.

---

# Services

## PropertyScraper

### Purpose

Responsible for collecting listings from external property portals.

### Responsibilities

* Execute searches against property portals
* Parse listing summaries
* Extract listing details
* Capture listing updates
* Detect removed listings
* Store raw portal data

### Technology

* .NET 10 Worker Service
* HttpClient
* AngleSharp or HtmlAgilityPack
* PostgreSQL

### Notes

Raw portal responses should be preserved for troubleshooting.

The scraper should not perform scoring calculations.

The scraper should not contain business logic.

---

## PropertyProcessor

### Purpose

Converts raw scraped data into enriched property records.

### Responsibilities

* Normalise listings
* Deduplicate listings
* Geocode locations if required
* Calculate nearest station
* Calculate station distance
* Calculate property scores
* Calculate commute metrics

### Technology

* .NET 10 Worker Service
* PostgreSQL
* PostGIS

### Notes

This service owns all enrichment logic.

No UI-specific logic should exist here.

---

## PropertyApi

### Purpose

Provides a stable API for querying properties.

### Responsibilities

* Property search
* Sorting
* Filtering
* User preferences
* Saved searches

### Technology

* ASP.NET Core (.NET 10)
* OpenAPI / Swagger
* PostgreSQL

### Example Endpoints

```text
GET /properties

GET /properties/{id}

GET /search

POST /saved-searches
```

---

## PropertyWeb

### Purpose

User-facing web application.

### Responsibilities

* Property browsing
* Property comparison
* Saved searches
* Property scoring visualisation

### Technology

* React + TypeScript
* Vite

---

## PropertyAlerts (FUTURE)
This is a future addition is does not need to be implemented yet.

### Purpose

Notification service.

### Responsibilities

* Monitor saved searches
* Detect new matching properties
* Detect price reductions
* Send notifications

### Technology

* .NET 10 Worker Service
* PostgreSQL

---

# Shared Projects

## Domain

Contains business entities.

Examples:

```text
Property
PropertyListing
Station
CommuteRoute
PropertyScore
```

Domain entities must not depend on infrastructure libraries.

---

## Contracts

Contains DTOs and message contracts.

Examples:

```text
PropertyDto
PropertySearchRequest
PropertySearchResponse
```

---

## Infrastructure

Contains reusable infrastructure components.

Examples:

```text
Database configuration
Repository implementations
HTTP clients
Caching
Background job helpers
```

---

# Database

## PostgreSQL

Primary datastore.

### Extensions

```sql
postgis
```

PostGIS should be enabled from the beginning.

Spatial queries will be required for:

* Station lookups
* Distance calculations
* Commute calculations

---

# Transport Data

## Initial Source

Transport for London station data.

Store:

```text
StationId
Name
Latitude
Longitude
Mode
```

Examples of modes:

* Underground
* DLR
* Overground
* Elizabeth Line
* National Rail

---

# Property Scoring

The scoring engine should be configurable.

Initial scoring factors:

```text
Rent
Bedrooms
Bathrooms
Nearest Station Distance
```

Example:

Property Score =

40% Rent
20% Bedrooms
10% Bathrooms
30% Station Distance

Weights should be configurable.

---

# Deployment

## Initial Environment

Single-host deployment.

Target:

* Raspberry Pi
* Small VPS
* Home server

All services should run in Docker containers.

---

# Non-Goals

The following are explicitly out of scope for the initial version:

* User authentication
* Multi-tenancy
* Payment processing
* Mobile applications
* AI-based recommendations
* Machine learning ranking

Focus on reliable data collection and search first.

---

# Development Priorities

Priority 1

* Database schema
* Domain model
* Rightmove scraper
* Property normalisation

Priority 2

* Station import
* Distance calculations
* Property API

Priority 3

* Web UI
* Saved searches
* Alerts

Priority 4

* Additional property portals
* Commute-time calculations
* Advanced ranking