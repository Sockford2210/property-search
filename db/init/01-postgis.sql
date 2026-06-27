-- Runs once, on first container start against an empty data volume.
-- Enables PostGIS in the 'propertysearch' database created by POSTGRES_DB.
CREATE EXTENSION IF NOT EXISTS postgis;
