#!/usr/bin/env python3
"""Regenerate shared/Infrastructure/Data/london-stations.json from the TfL Unified API.

Pulls the StopPoint/Mode listing for each London-scoped mode, keeps station-level
points only, and emits one row per (physical station, mode). The importer
(StationImportService) collapses rows sharing a `code` into a single physical
station and picks a primary mode by precedence.

Physical code = hubNaptanCode (multi-mode interchange) when present, else the
station's naptanId. This makes interchanges such as Stratford share one code
across their modes.

Usage:  python scripts/build-station-dataset.py
Requires: Python 3, network access to https://api.tfl.gov.uk (no app key needed).
"""

from __future__ import annotations

import json
import os
import re
import ssl
import sys
import urllib.request
from collections import OrderedDict

# TfL mode id -> TransportMode enum name (see shared/Domain/Enums/TransportMode.cs)
MODES = OrderedDict(
    [
        ("tube", "Underground"),
        ("elizabeth-line", "ElizabethLine"),
        ("overground", "Overground"),
        ("dlr", "Dlr"),
    ]
)

STATION_STOP_TYPES = {"NaptanMetroStation", "NaptanRailStation"}

SUFFIXES = [
    " Underground Station",
    " DLR Station",
    " Rail Station",
    " Overground Station",
    " Station",
]

OUT_PATH = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "shared",
    "Infrastructure",
    "Data",
    "london-stations.json",
)

# The corporate proxy in some environments performs TLS inspection; allow the
# generator to run there. The data is public and read-only.
_SSL_CTX = ssl.create_default_context()
_SSL_CTX.check_hostname = False
_SSL_CTX.verify_mode = ssl.CERT_NONE


def clean_name(name: str) -> str:
    name = re.sub(r"\s*\(for [^)]*\)", "", name)
    for suffix in SUFFIXES:
        if name.endswith(suffix):
            name = name[: -len(suffix)]
            break
    return name.strip()


def fetch_mode(mode: str) -> list[dict]:
    url = f"https://api.tfl.gov.uk/StopPoint/Mode/{mode}"
    req = urllib.request.Request(url, headers={"User-Agent": "PropertySearch-station-import"})
    with urllib.request.urlopen(req, timeout=120, context=_SSL_CTX) as resp:
        return json.load(resp).get("stopPoints", [])


def main() -> int:
    rows: "OrderedDict[tuple[str, str], dict]" = OrderedDict()
    for mode_id, mode_enum in MODES.items():
        points = fetch_mode(mode_id)
        kept = 0
        for p in points:
            if p.get("stopType") not in STATION_STOP_TYPES:
                continue
            naptan = p.get("naptanId")
            lat, lon = p.get("lat"), p.get("lon")
            if not naptan or lat is None or lon is None:
                continue
            code = p.get("hubNaptanCode") or naptan
            key = (code, mode_enum)
            if key in rows:
                continue
            rows[key] = {
                "code": code,
                "name": clean_name(p.get("commonName", "")),
                "latitude": round(float(lat), 6),
                "longitude": round(float(lon), 6),
                "mode": mode_enum,
            }
            kept += 1
        print(f"  {mode_id:<15} {len(points):>4} points -> {kept:>3} station rows", file=sys.stderr)

    data = sorted(rows.values(), key=lambda r: (r["name"], r["mode"]))
    distinct_stations = len({r["code"] for r in data})

    with open(OUT_PATH, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")

    print(f"Wrote {len(data)} rows ({distinct_stations} distinct stations) to {OUT_PATH}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
