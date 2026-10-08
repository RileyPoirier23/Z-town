"""Generates all procedural art into game/assets/gen and writes game/assets/gen/manifest.json.

    python tools/art/generate.py            # everything
    python tools/art/generate.py tiles      # just tiles (fast)

This is procedural stand-in art in Zomboid's direction (painted textures, soft shading,
outlines). It's meant to be replaced by commissioned art (see ART_SPEC.md §9), but every
sprite is already the right size, origin and naming so swaps are drop-in.
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))

import cars  # noqa: E402
import chars  # noqa: E402
import icons  # noqa: E402
import props  # noqa: E402
import tiles  # noqa: E402
from iso import ORIGIN  # noqa: E402

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "game", "assets", "gen")


def save(img, rel):
    path = os.path.join(OUT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, optimize=True)
    return "res://assets/gen/" + rel


def _char_job(job):
    layer, slot, anim, frames = job
    return save(chars.sheet(layer, anim, frames), f"characters/{layer}_{anim}.png")


def main(which):
    manifest_path = os.path.join(OUT, "manifest.json")
    m = json.load(open(manifest_path)) if os.path.exists(manifest_path) else {}
    m.setdefault("floors", {})
    m.setdefault("walls", {})
    m.setdefault("objects", {})
    m.setdefault("characters", {})

    if which in ("all", "tiles"):
        m["floors"], m["walls"] = {}, {}   # drop anything no longer generated
        m["floorGrid"] = {}
        for name, variants in tiles.FLOORS.items():
            if callable(variants):  # seamless ground: one big painting sliced into a grid
                texs = variants()
                m["floors"][name] = [save(tiles.put_floor(t), f"floors/{name}_{i}.png") for i, t in enumerate(texs)]
                m["floorGrid"][name] = tiles.GRID
            else:
                m["floors"][name] = [save(tiles.put_floor(v()), f"floors/{name}_{i}.png") for i, v in enumerate(variants)]
        for style, make in tiles.WALL_STYLES.items():
            tex = make()
            for orient in ("N", "W"):
                for kind, states in (("wall", ["closed"]), ("door", ["closed", "open", "broken"]), ("window", ["closed", "open", "broken"])):
                    if style == "fence_wood" and kind != "wall":
                        continue
                    for state in states:
                        for cut in (False, True):
                            key = f"{style}/{kind}_{state}_{orient}{'_cut' if cut else ''}"
                            m["walls"][key] = save(tiles.wall_tile(tex, orient, kind, state, cut), f"walls/{style}_{kind}_{state}_{orient}{'_cut' if cut else ''}.png")
        m["roofs"] = {name: save(make(), f"roofs/{name}.png") for name, make in tiles.ROOFS.items()}
        for name, make in tiles.FURNITURE.items():
            m["objects"][name] = {"file": save(make(), f"objects/{name}.png"), "origin": list(ORIGIN)}
        m["objects"]["streetlight"] = {"file": save(tiles.obj_streetlight(), "objects/streetlight.png"), "origin": [ORIGIN[0], ORIGIN[1] + 128]}

    if which in ("all", "cars"):
        m["cars"] = {"frame": {"w": cars.FW, "h": cars.FH, "center": list(cars.CENTER), "dirs": cars.DIRS}, "models": {}}
        for kind in cars.MODELS:
            body, det = cars.sheets(kind)
            m["cars"]["models"][kind] = {"body": save(body, f"cars/{kind}_body.png"), "details": save(det, f"cars/{kind}_details.png")}

    if which in ("all", "icons"):
        import glob
        import re
        m["moodles"] = {k: save(icons.moodle(k), f"ui/moodle_{k}.png") for k in icons.MOODLES}
        m["items"] = {}
        for path in sorted(glob.glob(os.path.join(ROOT, "game", "data", "items", "*.json"))):
            text = re.sub(r"^\s*//.*$", "", open(path, encoding="utf-8").read(), flags=re.M)
            for item in json.loads(text):
                m["items"][item["id"]] = save(icons.item_icon(item["id"]), f"items/{item['id']}.png")

    if which in ("all", "props"):
        for name, (make, origin) in props.PROPS.items():
            m["objects"][name] = {"file": save(make(), f"objects/{name}.png"), "origin": list(origin)}

    if which in ("all", "chars"):
        from concurrent.futures import ProcessPoolExecutor
        jobs = []
        only = sys.argv[2:] or None   # e.g. generate.py chars toque ballcap
        for layer, slot in chars.layers():
            if only and layer not in only:
                continue
            anims = chars.HELD_ANIMS.get(layer, list(chars.ANIMS))
            for anim in anims:
                jobs.append((layer, slot, anim, chars.ANIMS[anim]))
        old_layers = m.get("characters", {}).get("layers", {}) if only else {}
        m["characters"] = {"frame": {"w": chars.FW, "h": chars.FH, "feet": list(chars.FEET)},
                           "slots": {k: v for k, v in chars.GARMENTS.items()}, "anims": chars.ANIMS, "layers": old_layers}
        with ProcessPoolExecutor() as ex:
            for (layer, slot, anim, frames), path in zip(jobs, ex.map(_char_job, jobs)):
                m["characters"]["layers"].setdefault(layer, {"slot": slot, "anims": {}})["anims"][anim] = path
        print(f"   {len(jobs)} character sheets")

    m["tileOrigin"] = list(ORIGIN)
    with open(manifest_path, "w") as f:
        json.dump(m, f, indent=1, sort_keys=True)
    print("manifest written")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "all")
