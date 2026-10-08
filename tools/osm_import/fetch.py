"""Fetches raw OpenStreetMap data for each area in areas.json via the Overpass API.

    python tools/osm_import/fetch.py            # fetch any area not cached yet
    python tools/osm_import/fetch.py --force    # refetch all

Writes game/data/osm/<name>.json (Overpass JSON with inline geometry). Runs in CI
(.github/workflows/osm-fetch.yml) because the dev container can't reach OSM servers.
Map data © OpenStreetMap contributors, available under the ODbL.
"""
import json
import os
import sys
import time
import urllib.parse
import urllib.request

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "game", "data", "osm")
ENDPOINTS = ["https://overpass-api.de/api/interpreter", "https://overpass.private.coffee/api/interpreter",
             "https://overpass.kumi.systems/api/interpreter"]

QUERY = """
[out:json][timeout:120];
(
  way["highway"]({b});
  way["building"]({b});
  relation["building"]({b});
  way["landuse"]({b});
  way["natural"]({b});
  way["leisure"]({b});
  way["amenity"]({b});
  way["waterway"]({b});
  way["barrier"]({b});
  relation["natural"="water"]({b});
  relation["landuse"]({b});
  node["shop"]({b});
  node["amenity"]({b});
  node["natural"="tree"]({b});
);
out geom;
"""


def fetch(bbox):
    b = ",".join(str(v) for v in bbox)
    data = urllib.parse.urlencode({"data": QUERY.format(b=b)}).encode()
    last = None
    for attempt in range(4):
        for url in ENDPOINTS:
            try:
                req = urllib.request.Request(url, data=data, headers={"User-Agent": "z-town-map-import (github.com/RileyPoirier23/Z-town)"})
                with urllib.request.urlopen(req, timeout=180) as r:
                    return json.loads(r.read())
            except Exception as e:  # noqa: BLE001
                last = e
                print(f"  {url}: {e}")
        time.sleep(20 * (attempt + 1))
    raise RuntimeError(f"Overpass failed: {last}")


def main():
    force = "--force" in sys.argv
    areas = json.load(open(os.path.join(os.path.dirname(__file__), "areas.json")))["areas"]
    os.makedirs(OUT, exist_ok=True)
    for a in areas:
        path = os.path.join(OUT, a["name"] + ".json")
        if os.path.exists(path) and not force:
            print(f"{a['name']}: cached")
            continue
        print(f"{a['name']}: fetching {a['bbox']}")
        try:
            d = fetch(a["bbox"])
        except RuntimeError as e:
            # keep what we got; the next run picks this one up again
            print(f"::warning::{a['name']} not fetched this time: {e}")
            continue
        d["ztown"] = {"name": a["name"], "bbox": a["bbox"], "fetched": time.strftime("%Y-%m-%d"),
                      "attribution": "© OpenStreetMap contributors, ODbL 1.0"}
        with open(path, "w", encoding="utf-8") as f:
            json.dump(d, f, separators=(",", ":"))
        print(f"  {len(d.get('elements', []))} elements -> {os.path.relpath(path, ROOT)}")
        time.sleep(5)


if __name__ == "__main__":
    main()
