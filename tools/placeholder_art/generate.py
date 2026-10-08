"""Placeholder art generator (ART_SPEC.md §9).

Makes flat-shaded, correctly sized stand-ins for every item in game/data/items and for the
tiles, objects and character frames the game uses, each labelled with its id, so every art
slot exists at the right size before real art arrives. Also writes assets/PLACEHOLDERS.md.

    python tools/placeholder_art/generate.py
"""
import glob
import hashlib
import json
import os
import re

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
GAME = os.path.join(ROOT, "game")
OUT = os.path.join(GAME, "assets", "placeholder")
TILE_W, TILE_H, CANVAS_H = 128, 64, 256
FRAME = 256
DIRS = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"]

FLOORS = ["grass", "asphalt", "sidewalk", "carpet", "linoleum", "hardwood", "tile_store"]
WALLS = ["siding_white", "brick_red", "drywall", "fence_wood"]
WALL_STATES = {"wall": ["intact"], "door": ["closed", "open", "broken"], "window": ["closed", "open", "broken"]}
OBJECTS = ["memere_chair", "tv", "fridge", "cupboard", "cooler", "shelf", "counter", "crate", "generator", "couch", "bed"]
MOODLES = ["hungry", "thirsty", "tired", "bored", "unhappy", "stressed", "injured", "infected"]
CHAR_LAYERS = {"body": ["player", "memere", "dad", "zombie"]}
CHAR_ANIMS = {"idle": 8, "walk": 8, "run": 8}
MEMERE_ANIMS = {"idle_chair": 8, "nap_chair": 8, "watch_tv": 8, "smoke": 8, "drink_mepsi": 8}


def colour(key):
    h = hashlib.md5(key.encode()).digest()
    # muted palette: low saturation, mid value
    r, g, b = (60 + h[0] % 120, 60 + h[1] % 110, 55 + h[2] % 100)
    return (r, g, b, 255)


def font(size):
    for f in ("DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "arial.ttf"):
        try:
            return ImageFont.truetype(f, size)
        except OSError:
            pass
    return ImageFont.load_default()


def label(d, xy, text, size=11, fill=(255, 255, 255, 230)):
    d.text(xy, text, font=font(size), fill=fill, anchor="mm")


def diamond(cx, base):
    return [(cx, base - TILE_H), (cx + TILE_W // 2, base - TILE_H // 2), (cx, base), (cx - TILE_W // 2, base - TILE_H // 2)]


def save(img, *parts):
    path = os.path.join(OUT, *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    return os.path.relpath(path, GAME).replace("\\", "/")


def floor_tile(name):
    im = Image.new("RGBA", (TILE_W, CANVAS_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.polygon(diamond(TILE_W // 2, CANVAS_H), fill=colour("floor" + name), outline=(0, 0, 0, 90))
    label(d, (TILE_W // 2, CANVAS_H - TILE_H // 2), name, 10)
    return im


def wall_tile(style, kind, state, orient):
    im = Image.new("RGBA", (TILE_W, CANVAS_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    c = colour("wall" + style)
    if orient == "W":
        c = tuple(int(v * 0.82) for v in c[:3]) + (255,)
    # wall along the N (top-right) or W (top-left) edge of the diamond
    top = CANVAS_H - TILE_H
    if orient == "N":
        a, b = (TILE_W // 2, top), (TILE_W, top + TILE_H // 2)
    else:
        a, b = (0, top + TILE_H // 2), (TILE_W // 2, top)
    h = 192 - 40
    d.polygon([a, b, (b[0], b[1] - h), (a[0], a[1] - h)], fill=c, outline=(0, 0, 0, 120))
    if kind != "wall":
        mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        hole = (60, 40, 32) if kind == "door" else (120, 160, 190)
        alpha = {"closed": 255, "open": 70, "broken": 160}[state]
        d.rectangle([mx - 14, my - (110 if kind == "door" else 95), mx + 14, my - (0 if kind == "door" else 45)], fill=hole + (alpha,))
    label(d, ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2 - h / 2), f"{kind}\n{state}", 9)
    return im


def object_tile(name):
    im = Image.new("RGBA", (TILE_W, CANVAS_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    c = colour("obj" + name)
    h = 40 + int(hashlib.md5(name.encode()).digest()[3]) % 60
    base = CANVAS_H - 8
    top = [(x, y - h) for x, y in diamond(TILE_W // 2, base)]
    d.polygon(diamond(TILE_W // 2, base), fill=c)
    d.polygon([diamond(TILE_W // 2, base)[1], diamond(TILE_W // 2, base)[2], top[2], top[1]], fill=tuple(int(v * 0.8) for v in c[:3]) + (255,))
    d.polygon([diamond(TILE_W // 2, base)[2], diamond(TILE_W // 2, base)[3], top[3], top[2]], fill=tuple(int(v * 0.65) for v in c[:3]) + (255,))
    d.polygon(top, fill=tuple(min(255, int(v * 1.1)) for v in c[:3]) + (255,))
    label(d, (TILE_W // 2, base - h - 20), name, 10)
    return im


def char_sheet(layer, who, anim, frames, dirn):
    im = Image.new("RGBA", (FRAME * frames, FRAME), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    c = colour("char" + who)
    for f in range(frames):
        ox = f * FRAME
        bob = [0, -2, -3, -2, 0, 2, 3, 2][f % 8] if anim != "idle" else 0
        # feet anchor at (128, 236): ~170 px tall adult
        d.ellipse([ox + 100, 226, ox + 156, 246], fill=(0, 0, 0, 70))
        d.rounded_rectangle([ox + 108, 96 + bob, ox + 148, 236], radius=14, fill=c)
        d.ellipse([ox + 110, 62 + bob, ox + 146, 98 + bob], fill=(224, 185, 160, 255))
        label(d, (ox + 128, 40), f"{who} {anim}\n{dirn} {f + 1}/{frames}", 10)
    return im


def item_icon(item):
    im = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([4, 4, 60, 60], radius=10, fill=colour("item" + item["category"]), outline=(0, 0, 0, 120))
    words = re.sub(r"[_]", " ", item["id"]).split()
    label(d, (32, 32), "\n".join(words[:3]), 9)
    return im


def moodle_icon(name, level):
    im = Image.new("RGBA", (48, 48), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    tint = [(120, 160, 110), (200, 180, 90), (210, 130, 70), (190, 70, 60)][level - 1]
    d.ellipse([2, 2, 46, 46], fill=tint + (255,), outline=(0, 0, 0, 150), width=2)
    label(d, (24, 24), name[:4], 10)
    return im


def main():
    made = []
    for f in FLOORS:
        made.append(("tile", save(floor_tile(f), "tiles", f"tile_floor_{f}_01.png")))
    for style in WALLS:
        for kind, states in WALL_STATES.items():
            for state in states:
                for orient in ("N", "W"):
                    made.append(("tile", save(wall_tile(style, kind, state, orient), "tiles", f"tile_{kind}_{style}_01_{state}_{orient}.png")))
    for o in OBJECTS:
        made.append(("object", save(object_tile(o), "tiles", f"tile_furniture_{o}_01.png")))
    for layer, whos in CHAR_LAYERS.items():
        for who in whos:
            anims = dict(CHAR_ANIMS)
            if who == "memere":
                anims = dict(MEMERE_ANIMS)  # her rig only gets her allowed states
            for anim, frames in anims.items():
                for dirn in DIRS:
                    made.append(("character", save(char_sheet(layer, who, anim, frames, dirn), "characters", f"char_{layer}_{who}_{anim}_{dirn}.png")))
    for path in sorted(glob.glob(os.path.join(GAME, "data", "items", "*.json"))):
        with open(path, encoding="utf-8") as f:
            text = re.sub(r"^\s*//.*$", "", f.read(), flags=re.M)
        for item in json.loads(text):
            made.append(("item", save(item_icon(item), "items", f"item_{item['id']}.png")))
    for m in MOODLES:
        for lvl in range(1, 5):
            made.append(("moodle", save(moodle_icon(m, lvl), "ui", f"moodle_{m}_{lvl}.png")))

    lines = ["# Placeholder assets", "",
             "Generated by `tools/placeholder_art/generate.py`. Every file here is a stand-in at the",
             "size ART_SPEC.md sets, to be replaced by real art. Don't hand-edit; regenerate.", ""]
    by_kind = {}
    for kind, p in made:
        by_kind.setdefault(kind, []).append(p)
    for kind, paths in by_kind.items():
        lines.append(f"## {kind} ({len(paths)})")
        lines.append("")
        lines += [f"- `{p}`" for p in paths]
        lines.append("")
    with open(os.path.join(GAME, "assets", "PLACEHOLDERS.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))
    print(f"{len(made)} placeholder files")


if __name__ == "__main__":
    main()
