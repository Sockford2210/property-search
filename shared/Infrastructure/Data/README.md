# Station dataset

`london-stations.json` is the committed source of truth for the Phase 2 station
import. It is embedded into `PropertySearch.Infrastructure` and read by
`EmbeddedStationDataSource`.

## Shape

An array of one row per **(physical station, mode)**:

```json
{
  "code": "HUBSRA",
  "name": "Stratford",
  "latitude": 51.541806,
  "longitude": -0.003458,
  "mode": "Underground"
}
```

- `code` — stable per-physical-station identifier (TfL/NaPTAN **hub** code for
  multi-mode interchanges, otherwise the station's NaPTAN id). Rows for the same
  physical station share one `code`; `StationImportService` collapses them into a
  single `stations` row and picks a primary mode via `StationModePrecedence`.
- `mode` — one of the `TransportMode` enum names
  (`Underground`, `ElizabethLine`, `Overground`, `Dlr`, `NationalRail`).

## Provenance

Generated from the **TfL Unified API** (`StopPoint/Mode/{mode}`, no app key
required) for the four London-scoped rapid-transit modes: `tube`,
`elizabeth-line`, `overground`, `dlr`. Station-level points
(`NaptanMetroStation` / `NaptanRailStation`) are kept; entrances and platforms
are discarded.

Scope is the TfL-served network rather than the strict Greater London boundary,
so a few Elizabeth line / Overground termini outside the GLA boundary (e.g.
Reading, Watford) are included. National Rail is intentionally excluded for now
(its TfL mode listing is UK-wide, not London-scoped); it remains a valid
`TransportMode` for later.

## Regenerating

```bash
python scripts/build-station-dataset.py
```

Requires Python 3 and network access to `https://api.tfl.gov.uk`. Review the diff
before committing. Current dataset: ~468 rows / ~420 distinct stations.
