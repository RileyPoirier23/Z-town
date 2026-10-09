"""Era overlays: how the town ages over the years (tall grass, weeds in cracks, cracks, litter,
dead leaves, vines on walls). All on the standard 128 x 256 tile canvas so the game draws
them exactly like floors and walls. How much of each shows is set per chapter
(game/data/chapters.json, "era").
"""
import math

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

import paint as P
from iso import CANVAS_H, CANVAS_W, STOREY_PX, tile_to_canvas
from tiles import EAST, SOUTH, WALL_TEX_H, put_floor, wall_quad

SS = 2  # supersampling for the standing overlays


def _blades(d, rng, count, area, hmin, hmax, palette, lean=0.18, width=(2.2, 4.0), taper=0.0):
    """Draws grass blades (back to front) on a supersampled canvas. taper > 0 makes blades
    shorter toward the edge of the area, so neighbouring clumps blend instead of reading as
    boxes."""
    blades = []
    (u0, u1), (v0, v1) = area
    cu, cv, ru, rv = (u0 + u1) / 2, (v0 + v1) / 2, (u1 - u0) / 2, (v1 - v0) / 2
    for _ in range(count):
        u, v = rng.uniform(u0, u1), rng.uniform(v0, v1)
        r = min(1.0, math.hypot((u - cu) / ru, (v - cv) / rv))
        if taper and rng.random() < r ** 3 * taper:
            continue
        h = rng.uniform(hmin, hmax) * (1 - taper * 0.6 * r ** 2)
        a = rng.uniform(0, 2 * math.pi)
        l = rng.uniform(0.2, 1.0) * lean
        blades.append((u + v, u, v, h, math.cos(a) * l, math.sin(a) * l))
    blades.sort()
    for _, u, v, h, lx, ly in blades:
        col = np.array(palette[rng.integers(0, len(palette))], dtype=float)
        # light from the front-left: blades leaning toward the camera catch more of it
        lit = 0.75 + 0.35 * max(0.0, (lx + ly) / (2 * lean + 1e-6)) + rng.uniform(-0.08, 0.08)
        b = np.array(tile_to_canvas(u, v, 0)) * SS
        m = np.array(tile_to_canvas(u + lx * 0.4, v + ly * 0.4, h * 0.55)) * SS
        t = np.array(tile_to_canvas(u + lx, v + ly, h)) * SS
        w = rng.uniform(*width)
        dark = tuple(int(c) for c in np.clip(col * lit * 0.62, 0, 255)) + (255,)
        light = tuple(int(c) for c in np.clip(col * lit, 0, 255)) + (255,)
        d.polygon([tuple(b - (w, 0)), tuple(b + (w, 0)), tuple(m + (w * 0.6, 0)), tuple(m - (w * 0.6, 0))], fill=dark)
        d.polygon([tuple(m - (w * 0.6, 0)), tuple(m + (w * 0.6, 0)), tuple(t)], fill=light)


def _finish(big, outline=60):
    img = big.resize((CANVAS_W, CANVAS_H), Image.LANCZOS)
    return P.outline(img, alpha=outline) if outline else img


def _ground_shadow(d, cx, cy, rx, alpha):
    """Soft darkening under a clump (cx, cy in tile space, rx in tiles)."""
    pts = [tuple(np.array(tile_to_canvas(cx + math.cos(a) * rx, cy + math.sin(a) * rx)) * SS) for a in np.linspace(0, 2 * math.pi, 24)]
    d.polygon(pts, fill=(20, 24, 12, alpha))


GREENS = [(96, 118, 58), (84, 106, 50), (110, 124, 64), (72, 94, 46), (122, 128, 70)]
STRAW = [(150, 138, 86), (134, 122, 76), (160, 150, 98)]


def grass_tall(seed):
    rng = np.random.default_rng(seed)
    big = Image.new("RGBA", (CANVAS_W * SS, CANVAS_H * SS), (0, 0, 0, 0))
    sh = Image.new("RGBA", big.size, (0, 0, 0, 0))
    _ground_shadow(ImageDraw.Draw(sh), 0.5, 0.5, 0.5, 40)
    big.alpha_composite(sh.filter(ImageFilter.GaussianBlur(10)))
    d = ImageDraw.Draw(big)
    pal = GREENS + (STRAW if seed % 2 else [])
    top = rng.uniform(0.5, 0.8)
    # a few clumps of different heights, spilling a little past the tile so the field reads as one
    for _ in range(3):
        cu, cv = rng.uniform(0.25, 0.75, 2)
        s = rng.uniform(0.4, 0.62)
        _blades(d, rng, 110, ((cu - s, cu + s), (cv - s, cv + s)), 0.15, top * rng.uniform(0.75, 1.0), pal, taper=1.0)
    return _finish(big)


def weeds(seed):
    """A few small clumps, for cracks in roads and sidewalks."""
    rng = np.random.default_rng(seed)
    big = Image.new("RGBA", (CANVAS_W * SS, CANVAS_H * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    for _ in range(int(rng.integers(2, 4))):
        cu, cv = rng.uniform(0.2, 0.8, 2)
        _blades(d, rng, 26, ((cu - 0.1, cu + 0.1), (cv - 0.1, cv + 0.1)), 0.08, 0.32, GREENS, lean=0.12, width=(1.6, 3.0))
        if rng.random() < 0.2:  # a dandelion
            p = np.array(tile_to_canvas(cu, cv, 0.3)) * SS
            d.ellipse([p[0] - 4, p[1] - 4, p[0] + 4, p[1] + 4], fill=(214, 188, 64, 255))
    return _finish(big, 50)


def _flat(seed):
    return np.random.default_rng(seed), Image.new("RGBA", (128, 128), (0, 0, 0, 0))


def cracks(seed):
    rng, tex = _flat(seed)
    d = ImageDraw.Draw(tex)
    for _ in range(int(rng.integers(1, 3))):
        x, y = rng.uniform(0, 128, 2)
        a0 = a = rng.uniform(0, 2 * math.pi)
        for _ in range(int(rng.integers(10, 22))):
            a = a0 + np.clip(a - a0 + rng.uniform(-0.45, 0.45), -0.6, 0.6)   # wanders, but keeps going one way
            nx, ny = x + math.cos(a) * rng.uniform(4, 9), y + math.sin(a) * rng.uniform(4, 9)
            d.line([(x, y), (nx, ny)], fill=(28, 26, 24, 200), width=int(rng.integers(1, 3)))
            if rng.random() < 0.2:  # a branch
                ba = a + rng.choice([-1.2, 1.2])
                d.line([(nx, ny), (nx + math.cos(ba) * 8, ny + math.sin(ba) * 8)], fill=(30, 28, 26, 150), width=1)
            x, y = nx, ny
    return put_floor(tex)


def litter(seed):
    rng, tex = _flat(seed)
    d = ImageDraw.Draw(tex)
    papers = [(222, 218, 204), (200, 196, 180), (236, 232, 220), (186, 166, 128)]
    for _ in range(int(rng.integers(2, 5))):
        x, y = rng.uniform(14, 114, 2)
        kind = rng.random()
        if kind < 0.45:  # paper / flyer
            w, h, a = rng.uniform(9, 14), rng.uniform(12, 18), rng.uniform(0, math.pi)
            pts = [(x + math.cos(a) * dx - math.sin(a) * dy, y + math.sin(a) * dx + math.cos(a) * dy) for dx, dy in ((-w, -h), (w, -h), (w, h), (-w, h))]
            d.polygon(pts, fill=papers[rng.integers(0, 4)] + (255,), outline=(90, 84, 74, 200))
        elif kind < 0.7:  # a can on its side
            c = [(170, 40, 36), (60, 90, 150), (180, 180, 186)][rng.integers(0, 3)]
            d.ellipse([x - 10, y - 5, x + 10, y + 5], fill=c + (255,), outline=(40, 36, 34, 220))
            d.ellipse([x + 6, y - 5, x + 11, y + 5], fill=(200, 200, 204, 255))
        elif kind < 0.88:  # plastic bag
            pts = [(x + math.cos(t) * rng.uniform(9, 16), y + math.sin(t) * rng.uniform(7, 12)) for t in np.linspace(0, 2 * math.pi, 9)[:-1]]
            d.polygon(pts, fill=(226, 228, 230, 220), outline=(150, 150, 156, 200))
        else:  # cardboard
            d.rectangle([x - 18, y - 12, x + 18, y + 12], fill=(164, 128, 86, 255), outline=(100, 76, 50, 220))
    return put_floor(tex)


def leaves(seed):
    rng, tex = _flat(seed)
    ov = Image.new("RGBA", tex.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    cols = [(150, 92, 40), (122, 80, 44), (170, 120, 54), (104, 70, 40), (140, 110, 60)]
    for _ in range(70):
        x, y = rng.uniform(4, 124, 2)
        r = rng.uniform(2, 4)
        d.ellipse([x - r, y - r * 0.6, x + r, y + r * 0.6], fill=cols[rng.integers(0, len(cols))] + (230,))
    tex.alpha_composite(ov)
    return put_floor(tex)


def _vine_tex(seed):
    rng = np.random.default_rng(seed)
    tex = Image.new("RGBA", (128, WALL_TEX_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(tex)
    leaf = [(70, 96, 46), (58, 84, 40), (84, 108, 52), (96, 112, 58)]
    for _ in range(int(rng.integers(4, 8))):
        x, y = rng.uniform(4, 124), WALL_TEX_H
        top = rng.uniform(0.15, 0.7) * WALL_TEX_H
        a = -math.pi / 2
        while y > top:
            a = -math.pi / 2 + np.clip(a + math.pi / 2 + rng.uniform(-0.5, 0.5), -0.8, 0.8)
            nx, ny = np.clip(x + math.cos(a) * 6, 2, 126), y + math.sin(a) * 6
            d.line([(x, y), (nx, ny)], fill=(70, 58, 40, 255), width=2)
            # leaves thin out toward the top
            for _ in range(int(rng.integers(4, 9) * (0.4 + 0.6 * (ny - top) / (WALL_TEX_H - top + 1)) + 1)):
                lx, ly, r = nx + rng.uniform(-13, 13), ny + rng.uniform(-8, 8), rng.uniform(3, 6)
                d.ellipse([lx - r, ly - r, lx + r, ly + r], fill=leaf[rng.integers(0, 4)] + (255,))
            x, y = nx, ny
    return P.brush(tex, 0.08, seed)


def vines(seed, orient, cut):
    tex = _vine_tex(seed)
    h_px = 26 if cut else STOREY_PX
    if cut:
        tex = tex.crop((0, WALL_TEX_H - int(WALL_TEX_H * 26 / STOREY_PX), 128, WALL_TEX_H))
    c = Image.new("RGBA", (CANVAS_W, CANVAS_H), (0, 0, 0, 0))
    P.warp_onto(c, P.shade(tex, SOUTH if orient == "N" else EAST), wall_quad(orient, h_px))
    return c


def all_overlays():
    """name -> list of images."""
    out = {
        "grass_tall": [grass_tall(s) for s in range(4)],
        "weeds": [weeds(10 + s) for s in range(4)],
        "cracks": [cracks(20 + s) for s in range(6)],
        "litter": [litter(30 + s) for s in range(4)],
        "leaves": [leaves(40 + s) for s in range(2)],
    }
    for o in ("N", "W"):
        for cut in (False, True):
            out[f"vines_{o}{'_cut' if cut else ''}"] = [vines(50 + s, o, cut) for s in range(3)]
    return out
