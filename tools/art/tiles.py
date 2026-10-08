"""Painted floor, wall and furniture tiles (128 x 256 canvases, see iso.py)."""
import math

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

import paint as P
from iso import CANVAS_H, CANVAS_W, STOREY_PX, UP_PX, WALL_M, tile_to_canvas

TOP, SOUTH, EAST = 1.0, 0.86, 0.70   # face brightness: top, +y face, +x face


def canvas():
    return Image.new("RGBA", (CANVAS_W, CANVAS_H), (0, 0, 0, 0))


def floor_quad():
    return [tile_to_canvas(0, 0), tile_to_canvas(1, 0), tile_to_canvas(1, 1), tile_to_canvas(0, 1)]


def put_floor(tex):
    c = canvas()
    (tx, ty), (rx, ry), (bx, by), (lx, ly) = floor_quad()
    e = 1.5  # bleed so neighbouring tiles overlap instead of leaving hairline seams
    P.warp_onto(c, tex, [(tx, ty - e), (rx + e, ry), (bx, by + e), (lx - e, ly)])
    return c


# ---------------------------------------------------------------- flat textures (128 x 128 = 1 m²)

GRID = 4  # seamless grounds are painted as one 4x4-tile texture and sliced


def big_grass(seed=0):
    S = 128 * GRID
    low = P.periodic_noise(S, S, 2.6, seed)
    mid = P.periodic_noise(S, S, 1.6, seed + 1)
    n = low * 0.65 + mid * 0.35
    rgb = P.tint(P.hexc("5d6c3d"), n, 0.16, hue_shift=P.hexc("7d7a45"))
    img = P.to_img(rgb)
    d = ImageDraw.Draw(img)
    rng = np.random.default_rng(seed)
    for _ in range(9000):
        x, y = rng.integers(0, S, 2)
        l = int(rng.integers(2, 7))
        k = n[y, x]
        base = np.array([66, 86, 44]) * (0.8 + 0.5 * k)
        c = tuple(int(v + rng.integers(-12, 14)) for v in base) + (255,)
        d.line([(x, y), (x + int(rng.integers(-2, 3)), y - l)], fill=c, width=1)
    ov = Image.new("RGBA", img.size, (0, 0, 0, 0))
    od = ImageDraw.Draw(ov)
    for _ in range(140):  # little clover / dry patches
        x, y = rng.integers(8, S - 8, 2)
        od.ellipse([x - 3, y - 2, x + 3, y + 2], fill=(116, 118, 70, 120) if rng.random() < 0.5 else (78, 104, 52, 140))
    img.alpha_composite(ov)
    return P.slice_tiles(P.brush(img, 0.06, seed), GRID)


def big_asphalt(seed=0, line=None):
    S = 128 * GRID
    n = P.periodic_noise(S, S, 2.0, seed) * 0.6 + P.periodic_noise(S, S, 0.8, seed + 2) * 0.4
    rgb = P.tint(P.hexc("4c4b4a"), n, 0.12)
    speck = np.random.default_rng(seed).random((S, S))
    rgb[speck > 0.975] *= 1.22
    rgb[speck < 0.02] *= 0.78
    img = P.to_img(rgb)
    ov = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    rng = np.random.default_rng(seed + 5)
    for _ in range(6):  # cracks and patch seams
        x, y = rng.integers(20, S - 20, 2)
        pts = [(int(x), int(y))]
        for _ in range(8):
            x = int(np.clip(x + rng.integers(-14, 15), 2, S - 2))
            y = int(np.clip(y + rng.integers(-14, 15), 2, S - 2))
            pts.append((x, y))
        d.line(pts, fill=(36, 35, 34, 190), width=1)
    img.alpha_composite(ov)
    tiles_ = P.slice_tiles(P.brush(img, 0.05, seed), GRID)
    if line:
        for t in tiles_:
            td = ImageDraw.Draw(t)
            if line == "dash_x":
                td.rectangle([22, 0, 106, 5], fill=(196, 168, 74, 225))
    return tiles_


def big_carpet(seed=0, color="7d6a5a"):
    S = 128 * GRID
    n = P.periodic_noise(S, S, 1.2, seed) * 0.4 + P.periodic_noise(S, S, 3.0, seed + 1) * 0.6
    return P.slice_tiles(P.brush(P.to_img(P.tint(P.hexc(color), n, 0.09)), 0.14, seed), GRID)


def big_driveway(seed=0):
    S = 128 * GRID
    n = P.periodic_noise(S, S, 1.4, seed)
    rgb = P.tint(P.hexc("6e675c"), n, 0.16)
    g = np.random.default_rng(seed).random((S, S))
    rgb[g > 0.9] *= 1.22
    return P.slice_tiles(P.brush(P.to_img(rgb), 0.08, seed), GRID)

def tex_grass(seed):
    n = P.value_noise(128, 128, 20, seed)
    rgb = P.tint(P.hexc("5b6a3c"), n, 0.18, hue_shift=P.hexc("7a7440"))
    img = P.to_img(rgb)
    d = ImageDraw.Draw(img)
    rng = np.random.default_rng(seed)
    for _ in range(420):
        x, y = rng.integers(0, 128, 2)
        l = rng.integers(2, 6)
        c = (int(70 + rng.integers(-20, 30)), int(90 + rng.integers(-20, 30)), int(48 + rng.integers(-15, 20)), 255)
        d.line([(x, y), (x + rng.integers(-1, 2), y - l)], fill=c, width=1)
    return P.brush(img, 0.08, seed)


def tex_asphalt(seed, line=None):
    n = P.value_noise(128, 128, 16, seed)
    rgb = P.tint(P.hexc("4b4a4a"), n, 0.12)
    rng = np.random.default_rng(seed)
    speck = rng.random((128, 128))
    rgb[speck > 0.97] *= 1.25
    rgb[speck < 0.03] *= 0.75
    img = P.to_img(rgb)
    d = ImageDraw.Draw(img)
    if rng.random() < 0.5:  # a crack
        x, y = rng.integers(10, 118, 2)
        pts = [(int(x), int(y))]
        for _ in range(6):
            x += rng.integers(-12, 13)
            y += rng.integers(-12, 13)
            pts.append((int(x), int(y)))
        d.line(pts, fill=(38, 36, 35, 200), width=1)
    if line == "dash_x":   # yellow centre line dash along u (drawn on the v=0 edge so it joins the next lane)
        d.rectangle([20, 0, 108, 5], fill=(196, 168, 74, 230))
    if line == "edge_x":
        d.rectangle([0, 0, 128, 3], fill=(205, 205, 195, 200))
    return P.brush(img, 0.05, seed)


def tex_sidewalk(seed):
    n = P.value_noise(128, 128, 30, seed)
    rgb = P.tint(P.hexc("9c9890"), n, 0.07)
    img = P.ao_edges(P.to_img(rgb), 5, 0.22)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, 127, 127], outline=(110, 106, 100, 255))
    return P.brush(img, 0.05, seed)


def tex_driveway(seed):
    n = P.value_noise(128, 128, 10, seed)
    rgb = P.tint(P.hexc("6e675c"), n, 0.15)
    rng = np.random.default_rng(seed)
    g = rng.random((128, 128))
    rgb[g > 0.9] = rgb[g > 0.9] * 1.25
    return P.brush(P.to_img(rgb), 0.08, seed)


def tex_hardwood(seed):
    rng = np.random.default_rng(seed)
    img = Image.new("RGBA", (128, 128))
    d = ImageDraw.Draw(img)
    plank = 16
    for i in range(0, 128, plank):
        base = np.array(P.hexc("8a5f3c")[:3]) * (0.88 + rng.random() * 0.22)
        d.rectangle([0, i, 128, i + plank - 1], fill=tuple(int(v) for v in base) + (255,))
        cut = rng.integers(10, 118)
        d.line([(cut, i), (cut, i + plank - 1)], fill=(60, 40, 28, 255))
        d.line([(0, i + plank - 1), (128, i + plank - 1)], fill=(58, 38, 26, 255))
    grain = P.value_noise(128, 128, 3, seed)
    arr = np.asarray(img, dtype=float)
    arr[..., :3] *= (0.92 + grain[..., None] * 0.14)
    return P.brush(Image.fromarray(arr.astype(np.uint8), "RGBA"), 0.06, seed)


def tex_carpet(seed, color="7d6a5a"):
    n = P.value_noise(128, 128, 6, seed, octaves=3)
    rgb = P.tint(P.hexc(color), n, 0.10)
    return P.brush(P.to_img(rgb), 0.12, seed)


def tex_linoleum(seed):
    img = Image.new("RGBA", (128, 128))
    d = ImageDraw.Draw(img)
    a, b = P.hexc("cfc6b0"), P.hexc("a99f88")
    s = 32
    for y in range(0, 128, s):
        for x in range(0, 128, s):
            d.rectangle([x, y, x + s - 1, y + s - 1], fill=a if (x + y) // s % 2 == 0 else b)
    n = P.value_noise(128, 128, 24, seed)
    arr = np.asarray(img, dtype=float)
    arr[..., :3] *= (0.9 + n[..., None] * 0.15)
    return P.brush(Image.fromarray(arr.astype(np.uint8), "RGBA"), 0.05, seed)


def tex_store(seed):
    img = Image.new("RGBA", (128, 128), P.hexc("c9c7bf"))
    d = ImageDraw.Draw(img)
    d.line([(0, 63), (128, 63)], fill=(150, 148, 140, 255))
    d.line([(63, 0), (63, 128)], fill=(150, 148, 140, 255))
    d.rectangle([0, 0, 127, 127], outline=(150, 148, 140, 255))
    n = P.value_noise(128, 128, 28, seed)
    arr = np.asarray(img, dtype=float)
    arr[..., :3] *= (0.93 + n[..., None] * 0.1)
    return P.brush(Image.fromarray(arr.astype(np.uint8), "RGBA"), 0.04, seed)


# ---------------------------------------------------------------- walls (flat texture = 128 px wide, wall-height tall)

WALL_TEX_H = 256


def tex_siding(color, seed):
    img = Image.new("RGBA", (128, WALL_TEX_H), P.hexc(color))
    d = ImageDraw.Draw(img)
    board = 14
    for y in range(0, WALL_TEX_H, board):
        d.line([(0, y), (128, y)], fill=tuple(int(c * 0.72) for c in P.hexc(color)[:3]) + (255,))
        d.line([(0, y + 1), (128, y + 1)], fill=tuple(min(255, int(c * 1.06)) for c in P.hexc(color)[:3]) + (255,))
        # shadow under each board lip
        for k in range(2, 6):
            d.line([(0, y + k), (128, y + k)], fill=tuple(int(c * (0.9 + k * 0.02)) for c in P.hexc(color)[:3]) + (255,))
    n = P.value_noise(128, WALL_TEX_H, 40, seed)
    arr = np.asarray(img, dtype=float)
    arr[..., :3] *= (0.94 + n[..., None] * 0.1)
    img = Image.fromarray(arr.astype(np.uint8), "RGBA")
    d = ImageDraw.Draw(img)
    # trim: corner boards and a sill strip at the bottom
    d.rectangle([0, WALL_TEX_H - 10, 128, WALL_TEX_H], fill=(84, 78, 70, 255))
    return P.vgradient(P.brush(img, 0.04, seed), 1.0, 0.9)


def tex_brick(seed):
    img = Image.new("RGBA", (128, WALL_TEX_H), (150, 140, 128, 255))
    d = ImageDraw.Draw(img)
    rng = np.random.default_rng(seed)
    bh, bw = 8, 24
    for row, y in enumerate(range(0, WALL_TEX_H, bh)):
        off = (row % 2) * bw // 2
        for x in range(-bw, 128 + bw, bw):
            c = np.array(P.hexc("8c4a3c")[:3]) * (0.8 + rng.random() * 0.35)
            d.rectangle([x + off + 1, y + 1, x + off + bw - 2, y + bh - 2], fill=tuple(int(v) for v in c) + (255,))
    return P.vgradient(P.brush(img, 0.08, seed), 1.0, 0.88)


def tex_wallpaper(color, accent, seed, baseboard="6b4f38"):
    img = Image.new("RGBA", (128, WALL_TEX_H), P.hexc(color))
    # faint vertical stripes, blended (ImageDraw would punch see-through holes with alpha)
    stripes = Image.new("RGBA", img.size, (0, 0, 0, 0))
    sd = ImageDraw.Draw(stripes)
    for x in range(0, 128, 16):
        sd.line([(x, 0), (x, WALL_TEX_H)], fill=P.hexc(accent, 60), width=3)
    img.alpha_composite(stripes)
    n = P.value_noise(128, WALL_TEX_H, 30, seed)
    arr = np.asarray(img, dtype=float)
    arr[..., :3] *= (0.95 + n[..., None] * 0.08)
    img = Image.fromarray(arr.astype(np.uint8), "RGBA")
    d = ImageDraw.Draw(img)
    d.rectangle([0, WALL_TEX_H - 14, 128, WALL_TEX_H], fill=P.hexc(baseboard))
    d.rectangle([0, 0, 128, 6], fill=tuple(int(c * 0.9) for c in P.hexc(color)[:3]) + (255,))
    return P.vgradient(P.brush(img, 0.04, seed), 1.0, 0.86)


def tex_fence(seed):
    img = Image.new("RGBA", (128, WALL_TEX_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    top = WALL_TEX_H - 130
    rng = np.random.default_rng(seed)
    for x in range(2, 128, 16):
        c = np.array(P.hexc("8a7458")[:3]) * (0.85 + rng.random() * 0.25)
        d.polygon([(x, top + 8), (x + 6, top), (x + 12, top + 8), (x + 12, WALL_TEX_H), (x, WALL_TEX_H)], fill=tuple(int(v) for v in c) + (255,))
    d.rectangle([0, top + 30, 128, top + 38], fill=(110, 92, 68, 255))
    d.rectangle([0, WALL_TEX_H - 40, 128, WALL_TEX_H - 32], fill=(110, 92, 68, 255))
    return P.brush(img, 0.06, seed)


def wall_quad(orient, height_px):
    """Wall on the N edge (u 0->1 at v=0) or W edge (v 0->1 at u=0) of a tile."""
    h_m = height_px / UP_PX
    e = 0.012  # bleed past the tile ends so wall sections join without seams
    if orient == "N":
        a, b = (-e, 0), (1 + e, 0)
    else:
        a, b = (0, -e), (0, 1 + e)
    tl = tile_to_canvas(a[0], a[1], h_m)
    tr = tile_to_canvas(b[0], b[1], h_m)
    br = tile_to_canvas(b[0], b[1], 0)
    bl = tile_to_canvas(a[0], a[1], 0)
    return [tl, tr, br, bl]


def cut_rect(tex, x0, x1, y0, y1):
    """Makes a hole in a flat wall texture (fractions of width/height from the top)."""
    w, h = tex.size
    arr = np.asarray(tex).copy()
    arr[int(y0 * h):int(y1 * h), int(x0 * w):int(x1 * w), 3] = 0
    return Image.fromarray(arr, "RGBA")


def wall_tile(wall_tex, orient, kind="wall", state="closed", cut=False, frame_color="e6e1d6"):
    """A wall section with an optional door or window, full height or cut down (cutaway)."""
    h_px = 26 if cut else STOREY_PX
    tex = wall_tex.copy()
    if cut:
        tex = tex.crop((0, WALL_TEX_H - int(WALL_TEX_H * 26 / STOREY_PX), 128, WALL_TEX_H))
    d = ImageDraw.Draw(tex)
    W, H = tex.size
    fr = P.hexc(frame_color)
    if kind == "door":
        # door opening: 0.18..0.82 wide, bottom 85% of the wall
        x0, x1, y0 = 0.18, 0.82, 0.18
        if cut:
            y0 = 0.0
        tex = cut_rect(tex, x0, x1, y0, 1.0)
        d = ImageDraw.Draw(tex)
        d.rectangle([x0 * W - 4, y0 * H - 4, x0 * W, H], fill=fr)
        d.rectangle([x1 * W, y0 * H - 4, x1 * W + 4, H], fill=fr)
        if not cut:
            d.rectangle([x0 * W - 4, y0 * H - 5, x1 * W + 4, y0 * H], fill=fr)
        if state == "closed":
            door = Image.new("RGBA", (int((x1 - x0) * W), int((1 - y0) * H)), P.hexc("7a5233"))
            dd = ImageDraw.Draw(door)
            dw, dh = door.size
            if dh > 40:
                dd.rectangle([6, 8, dw - 7, dh * 0.45], outline=(92, 64, 42, 255), width=3)
                dd.rectangle([6, dh * 0.52, dw - 7, dh - 10], outline=(92, 64, 42, 255), width=3)
                dd.ellipse([dw - 16, dh * 0.5 - 4, dw - 9, dh * 0.5 + 3], fill=(200, 180, 120, 255))
            tex.alpha_composite(P.brush(door, 0.06, 3), (int(x0 * W), int(y0 * H)))
        elif state == "broken":
            door = Image.new("RGBA", (int((x1 - x0) * W), int((1 - y0) * H)), (0, 0, 0, 0))
            dd = ImageDraw.Draw(door)
            dw, dh = door.size
            dd.polygon([(0, 0), (dw * 0.45, 0), (dw * 0.2, dh * 0.4), (dw * 0.5, dh * 0.7), (0, dh)], fill=(98, 66, 42, 255))
            tex.alpha_composite(door, (int(x0 * W), int(y0 * H)))
    elif kind == "window" and not cut:
        x0, x1, y0, y1 = 0.2, 0.8, 0.22, 0.62
        tex = cut_rect(tex, x0, x1, y0, y1)
        d = ImageDraw.Draw(tex)
        glass = Image.new("RGBA", (int((x1 - x0) * W), int((y1 - y0) * H)), (0, 0, 0, 0))
        gd = ImageDraw.Draw(glass)
        gw, gh = glass.size
        if state == "closed":
            for i in range(gh):
                t = i / gh
                gd.line([(0, i), (gw, i)], fill=(int(120 + 60 * t), int(150 + 50 * t), int(170 + 40 * t), 170))
            gd.line([(gw * 0.2, 4), (gw * 0.05, gh * 0.4)], fill=(255, 255, 255, 110), width=3)
            gd.line([(0, gh // 2), (gw, gh // 2)], fill=fr, width=4)
        elif state == "open":
            for i in range(gh // 2):
                gd.line([(0, i), (gw, i)], fill=(140, 170, 190, 150))
            gd.line([(0, gh // 2), (gw, gh // 2)], fill=fr, width=4)
        else:  # broken: jagged shards at the edges
            gd.polygon([(0, 0), (gw * 0.3, 0), (0, gh * 0.4)], fill=(150, 175, 190, 170))
            gd.polygon([(gw, gh), (gw * 0.6, gh), (gw, gh * 0.5)], fill=(150, 175, 190, 170))
        tex.alpha_composite(glass, (int(x0 * W), int(y0 * H)))
        d.rectangle([x0 * W - 4, y0 * H - 4, x1 * W + 4, y0 * H], fill=fr)
        d.rectangle([x0 * W - 4, y1 * H, x1 * W + 4, y1 * H + 6], fill=fr)  # sill
        d.rectangle([x0 * W - 4, y0 * H, x0 * W, y1 * H], fill=fr)
        d.rectangle([x1 * W, y0 * H, x1 * W + 4, y1 * H], fill=fr)
    c = canvas()
    tex = P.shade(tex, SOUTH if orient == "N" else EAST)
    P.warp_onto(c, tex, wall_quad(orient, h_px))
    # top edge of the wall (thickness hint)
    q = wall_quad(orient, h_px)
    ImageDraw.Draw(c).line([q[0], q[1]], fill=(60, 52, 46, 230), width=3)
    return c


# ---------------------------------------------------------------- boxes and furniture

def box(c, u0, v0, u1, v1, h0, h1, top_tex, south_tex, east_tex):
    """Draws a textured box from (u0,v0,h0) to (u1,v1,h1). Textures are flat images for each face."""
    if top_tex is not None:
        P.warp_onto(c, P.shade(top_tex, TOP), [tile_to_canvas(u0, v0, h1), tile_to_canvas(u1, v0, h1), tile_to_canvas(u1, v1, h1), tile_to_canvas(u0, v1, h1)])
    if south_tex is not None:
        P.warp_onto(c, P.shade(south_tex, SOUTH), [tile_to_canvas(u0, v1, h1), tile_to_canvas(u1, v1, h1), tile_to_canvas(u1, v1, h0), tile_to_canvas(u0, v1, h0)])
    if east_tex is not None:
        P.warp_onto(c, P.shade(east_tex, EAST), [tile_to_canvas(u1, v1, h1), tile_to_canvas(u1, v0, h1), tile_to_canvas(u1, v0, h0), tile_to_canvas(u1, v1, h0)])


def solid(color, w=64, h=64, seed=0, noise=0.08, scale=12):
    n = P.value_noise(w, h, scale, seed)
    return P.brush(P.to_img(P.tint(P.hexc(color), n, noise)), 0.05, seed)


def fabric(color, seed):
    n = P.value_noise(64, 64, 4, seed, octaves=3)
    return P.brush(P.to_img(P.tint(P.hexc(color), n, 0.12)), 0.14, seed)


def shadow(c, u0, v0, u1, v1, alpha=70):
    """Soft contact shadow on the floor under an object."""
    m = Image.new("RGBA", c.size, (0, 0, 0, 0))
    ImageDraw.Draw(m).polygon([tile_to_canvas(u0 - 0.06, v0 - 0.02), tile_to_canvas(u1 + 0.08, v0 - 0.02),
                               tile_to_canvas(u1 + 0.1, v1 + 0.12), tile_to_canvas(u0 - 0.04, v1 + 0.1)], fill=(0, 0, 0, alpha))
    c.alpha_composite(m.filter(ImageFilter.GaussianBlur(4)))


def obj_recliner():
    """Memere's chair: a recliner facing south (toward the camera / her TV)."""
    c = canvas()
    shadow(c, 0.12, 0.1, 0.88, 0.92)
    f = fabric("7d6450", 11)
    dark = fabric("6a5444", 12)
    box(c, 0.15, 0.30, 0.85, 0.88, 0.0, 0.42, f, dark, dark)            # seat base
    box(c, 0.15, 0.12, 0.85, 0.34, 0.0, 1.0, f, f, dark)                # backrest
    box(c, 0.12, 0.30, 0.27, 0.88, 0.0, 0.62, f, dark, dark)            # left arm
    box(c, 0.73, 0.30, 0.88, 0.88, 0.0, 0.62, f, dark, dark)            # right arm
    box(c, 0.27, 0.34, 0.73, 0.82, 0.42, 0.50, fabric("8a705a", 13), dark, dark)  # cushion
    return P.outline(c, alpha=90)


def obj_tv(on=False):
    c = canvas()
    shadow(c, 0.1, 0.35, 0.9, 0.75)
    wood = solid("5a4433", seed=21)
    box(c, 0.1, 0.38, 0.9, 0.72, 0.0, 0.5, wood, solid("4a372a", seed=22), solid("3e2e23", seed=23))
    screen = Image.new("RGBA", (96, 60), (24, 24, 28, 255))
    sd = ImageDraw.Draw(screen)
    if on:
        for y in range(4, 56):
            t = y / 60
            sd.line([(4, y), (91, y)], fill=(int(90 + 80 * t), int(120 + 60 * t), int(170 - 40 * t), 255))
        sd.ellipse([30, 14, 66, 44], fill=(230, 200, 140, 255))  # a game-show stage light blob
    else:
        sd.line([(14, 8), (30, 30)], fill=(70, 70, 80, 255), width=3)
    sd.rectangle([0, 0, 95, 59], outline=(14, 14, 16, 255), width=4)
    box(c, 0.16, 0.52, 0.84, 0.58, 0.5, 1.18, solid("1c1c20", seed=24), screen, solid("18181c", seed=25))
    return P.outline(c, alpha=90)


def obj_fridge():
    c = canvas()
    shadow(c, 0.1, 0.1, 0.9, 0.9)
    white = solid("e3e1da", seed=31, noise=0.04)
    front = solid("e8e6df", seed=32, noise=0.04)
    d = ImageDraw.Draw(front)
    d.line([(2, 22), (62, 22)], fill=(170, 168, 160, 255), width=2)
    d.rectangle([52, 26, 56, 46], fill=(150, 150, 150, 255))
    d.rectangle([52, 8, 56, 18], fill=(150, 150, 150, 255))
    box(c, 0.12, 0.15, 0.88, 0.85, 0.0, 1.8, white, front, solid("cfccc4", seed=33, noise=0.04))
    return P.outline(c, alpha=90)


def obj_counter(top="c8bfa8", body="8a6a48", seed=40):
    c = canvas()
    shadow(c, 0.0, 0.1, 1.0, 0.95)
    doors = solid(body, seed=seed)
    d = ImageDraw.Draw(doors)
    d.rectangle([4, 10, 29, 60], outline=tuple(int(v * 0.75) for v in P.hexc(body)[:3]) + (255,), width=2)
    d.rectangle([35, 10, 60, 60], outline=tuple(int(v * 0.75) for v in P.hexc(body)[:3]) + (255,), width=2)
    d.rectangle([24, 30, 26, 38], fill=(190, 180, 150, 255))
    d.rectangle([38, 30, 40, 38], fill=(190, 180, 150, 255))
    box(c, 0.02, 0.12, 0.98, 0.92, 0.0, 0.86, None, doors, solid(body, seed=seed + 1))
    box(c, 0.0, 0.08, 1.0, 0.95, 0.86, 0.92, solid(top, seed=seed + 2, noise=0.05), solid(top, seed=seed + 3), solid(top, seed=seed + 4))
    return P.outline(c, alpha=90)


def products(seed, rows=4, w=64, h=96, bottle=False):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rng = np.random.default_rng(seed)
    palette = [(176, 54, 48), (60, 96, 160), (220, 190, 70), (70, 140, 80), (210, 120, 60), (200, 200, 190), (120, 60, 120)]
    row_h = h // rows
    for r in range(rows):
        y1 = (r + 1) * row_h - 3
        d.rectangle([0, y1, w, y1 + 3], fill=(140, 140, 135, 255))
        x = 1
        while x < w - 4:
            pw = int(rng.integers(5, 10)) if not bottle else 6
            ph = int(rng.integers(row_h // 2, row_h - 5))
            col = palette[int(rng.integers(0, len(palette)))] if not bottle else ((176, 40, 40) if rng.random() < 0.6 else palette[int(rng.integers(0, len(palette)))])
            d.rectangle([x, y1 - ph, x + pw - 1, y1 - 1], fill=col + (255,))
            d.line([(x, y1 - ph), (x + pw - 1, y1 - ph)], fill=tuple(min(255, int(v * 1.3)) for v in col) + (255,))
            x += pw + 1
    return img


def obj_shelf(seed=50):
    c = canvas()
    shadow(c, 0.0, 0.25, 1.0, 0.8)
    metal = solid("8d8f8f", seed=seed, noise=0.05)
    back = solid("6f7272", seed=seed + 1, noise=0.05)
    box(c, 0.0, 0.3, 1.0, 0.42, 0.0, 1.6, metal, back, metal)            # back panel
    front = Image.new("RGBA", (64, 96), (0, 0, 0, 0))
    front.alpha_composite(products(seed))
    box(c, 0.0, 0.42, 1.0, 0.75, 0.0, 0.08, metal, metal, metal)         # base
    P.warp_onto(c, P.shade(front, SOUTH), [tile_to_canvas(0.0, 0.6, 1.55), tile_to_canvas(1.0, 0.6, 1.55), tile_to_canvas(1.0, 0.6, 0.08), tile_to_canvas(0.0, 0.6, 0.08)])
    box(c, 0.96, 0.3, 1.0, 0.75, 0.0, 1.6, metal, metal, metal)          # end upright
    return P.outline(c, alpha=80)


def obj_cooler():
    c = canvas()
    shadow(c, 0.05, 0.1, 0.95, 0.9)
    body = solid("d8d8d2", seed=61, noise=0.04)
    front = Image.new("RGBA", (64, 128), (225, 225, 220, 255))
    inner = Image.new("RGBA", (54, 104), (40, 52, 60, 255))
    inner.alpha_composite(products(62, rows=5, w=54, h=104, bottle=True))
    glass = Image.new("RGBA", inner.size, (170, 200, 220, 70))
    ImageDraw.Draw(glass).line([(10, 0), (0, 30)], fill=(255, 255, 255, 90), width=4)
    inner.alpha_composite(glass)
    front.alpha_composite(inner, (5, 10))
    ImageDraw.Draw(front).rectangle([0, 0, 63, 9], fill=(176, 40, 40, 255))  # red header (Mepsi cooler)
    box(c, 0.08, 0.15, 0.92, 0.85, 0.0, 2.0, body, front, solid("c8c8c2", seed=63, noise=0.04))
    return P.outline(c, alpha=90)


def obj_store_counter():
    c = canvas()
    shadow(c, 0.0, 0.15, 1.0, 0.9)
    wood = solid("5d4a3a", seed=71)
    box(c, 0.0, 0.2, 1.0, 0.9, 0.0, 1.0, solid("8b8478", seed=72, noise=0.05), wood, solid("4e3e31", seed=73))
    box(c, 0.25, 0.35, 0.65, 0.65, 1.0, 1.25, solid("2d2d30", seed=74), solid("232326", seed=75), solid("1d1d20", seed=76))  # register
    return P.outline(c, alpha=90)


def obj_crate():
    c = canvas()
    shadow(c, 0.1, 0.15, 0.9, 0.9)
    plank = Image.new("RGBA", (64, 64), P.hexc("8b7350"))
    d = ImageDraw.Draw(plank)
    for y in range(0, 64, 16):
        d.line([(0, y), (64, y)], fill=(100, 80, 55, 255), width=2)
    d.rectangle([0, 0, 63, 63], outline=(95, 75, 50, 255), width=4)
    plank = P.brush(plank, 0.1, 81)
    box(c, 0.15, 0.2, 0.85, 0.85, 0.0, 0.7, plank, plank, plank)
    return P.outline(c, alpha=90)


def obj_generator():
    c = canvas()
    shadow(c, 0.1, 0.2, 0.9, 0.85)
    yellow = solid("c99a2e", seed=91)
    side = Image.new("RGBA", (64, 40), P.hexc("c99a2e"))
    d = ImageDraw.Draw(side)
    d.rectangle([8, 8, 40, 32], fill=(40, 40, 40, 255))
    d.ellipse([44, 10, 58, 24], fill=(70, 70, 70, 255))
    box(c, 0.18, 0.28, 0.82, 0.78, 0.0, 0.55, yellow, P.brush(side, 0.05, 92), solid("a37c25", seed=93))
    frame = solid("2a2a2a", seed=94)
    box(c, 0.15, 0.25, 0.2, 0.8, 0.0, 0.62, frame, frame, frame)
    box(c, 0.8, 0.25, 0.85, 0.8, 0.0, 0.62, frame, frame, frame)
    return P.outline(c, alpha=90)


def obj_couch():
    c = canvas()
    shadow(c, 0.02, 0.1, 0.98, 0.9)
    f, d = fabric("5d6b74", 101), fabric("4c5960", 102)
    box(c, 0.04, 0.32, 0.96, 0.88, 0.0, 0.42, f, d, d)
    box(c, 0.04, 0.12, 0.96, 0.34, 0.0, 0.85, f, f, d)
    return P.outline(c, alpha=90)


def obj_rug():
    c = canvas()
    rug = Image.new("RGBA", (128, 128), P.hexc("7a3f35"))
    d = ImageDraw.Draw(rug)
    d.rectangle([8, 8, 119, 119], outline=(190, 160, 110, 255), width=4)
    d.rectangle([22, 22, 105, 105], outline=(150, 90, 60, 255), width=3)
    d.ellipse([46, 46, 82, 82], outline=(190, 160, 110, 255), width=3)
    P.warp_onto(c, P.brush(rug, 0.14, 111), [tile_to_canvas(0.05, 0.05, 0.005), tile_to_canvas(0.95, 0.05, 0.005), tile_to_canvas(0.95, 0.95, 0.005), tile_to_canvas(0.05, 0.95, 0.005)])
    return c


def obj_lamp():
    c = canvas()
    shadow(c, 0.35, 0.35, 0.65, 0.65, 50)
    pole = solid("3a3430", seed=121)
    box(c, 0.38, 0.38, 0.62, 0.62, 0.0, 0.04, pole, pole, pole)
    box(c, 0.48, 0.48, 0.52, 0.52, 0.0, 1.45, pole, pole, pole)
    shade = solid("e3d3a8", seed=122, noise=0.05)
    box(c, 0.36, 0.36, 0.64, 0.64, 1.3, 1.62, shade, shade, P.shade(shade, 0.9))
    return P.outline(c, alpha=80)


def obj_mailbox():
    c = canvas()
    post = solid("6b5a45", seed=131)
    box(c, 0.46, 0.46, 0.54, 0.54, 0.0, 1.0, post, post, post)
    red = solid("3d4f6a", seed=132)
    box(c, 0.32, 0.4, 0.68, 0.6, 1.0, 1.25, red, red, red)
    return P.outline(c, alpha=90)


def obj_streetlight():
    c = Image.new("RGBA", (CANVAS_W, CANVAS_H + 128), (0, 0, 0, 0))  # taller canvas
    pole = solid("5a5a5a", seed=141)

    def tc(u, v, h):
        x, y = tile_to_canvas(u, v, h)
        return (x, y + 128)
    for (u0, v0, u1, v1, h0, h1) in [(0.45, 0.45, 0.55, 0.55, 0, 4.2), (0.45, 0.2, 0.55, 0.55, 4.1, 4.2)]:
        P.warp_onto(c, P.shade(pole, SOUTH), [tc(u0, v1, h1), tc(u1, v1, h1), tc(u1, v1, h0), tc(u0, v1, h0)])
        P.warp_onto(c, P.shade(pole, EAST), [tc(u1, v1, h1), tc(u1, v0, h1), tc(u1, v0, h0), tc(u1, v1, h0)])
        P.warp_onto(c, pole, [tc(u0, v0, h1), tc(u1, v0, h1), tc(u1, v1, h1), tc(u0, v1, h1)])
    return P.outline(c, alpha=80)


def tex_shingles(color, seed):
    img = Image.new("RGBA", (128, 128), P.hexc(color))
    d = ImageDraw.Draw(img)
    rng = np.random.default_rng(seed)
    row = 10
    for i, y in enumerate(range(0, 128, row)):
        off = (i % 2) * 8
        for x in range(-16, 128, 16):
            c = np.array(P.hexc(color)[:3]) * (0.82 + rng.random() * 0.3)
            d.rectangle([x + off, y, x + off + 15, y + row - 2], fill=tuple(int(v) for v in c) + (255,))
        d.line([(0, y + row - 1), (128, y + row - 1)], fill=tuple(int(v * 0.55) for v in P.hexc(color)[:3]) + (255,))
    return P.brush(img, 0.1, seed)


ROOFS = {
    "shingle_grey": lambda: tex_shingles("5d5a58", 1),
    "shingle_brown": lambda: tex_shingles("6a5040", 2),
    "flat_tar": lambda: P.brush(P.to_img(P.tint(P.hexc("4a4846"), P.value_noise(128, 128, 10, 3), 0.12)), 0.08, 3),
}


FURNITURE = {
    "memere_chair": obj_recliner,
    "tv": lambda: obj_tv(False),
    "tv_on": lambda: obj_tv(True),
    "fridge": obj_fridge,
    "cupboard": obj_counter,
    "kitchen_counter": lambda: obj_counter(seed=44),
    "cooler": obj_cooler,
    "shelf": obj_shelf,
    "counter": obj_store_counter,
    "crate": obj_crate,
    "generator": obj_generator,
    "couch": obj_couch,
    "rug": obj_rug,
    "lamp": obj_lamp,
    "mailbox": obj_mailbox,
}

FLOORS = {
    "grass": lambda: big_grass(1),
    "asphalt": lambda: big_asphalt(2),
    "road_line": lambda: big_asphalt(2, "dash_x"),
    "sidewalk": [lambda s=s: tex_sidewalk(s) for s in range(3)],
    "driveway": lambda: big_driveway(3),
    "carpet": lambda: big_carpet(4),
    "hardwood": [lambda s=s: tex_hardwood(s) for s in range(3)],
    "linoleum": [lambda s=s: tex_linoleum(s) for s in range(2)],
    "tile_store": [lambda s=s: tex_store(s) for s in range(2)],
}

WALL_STYLES = {
    "siding_white": lambda: tex_siding("d8d3c4", 1),
    "siding_blue": lambda: tex_siding("7f93a3", 2),
    "siding_yellow": lambda: tex_siding("cbb88a", 3),
    "brick_red": lambda: tex_brick(4),
    "wallpaper_living": lambda: tex_wallpaper("b9a58a", "a08a70", 5),
    "wallpaper_kitchen": lambda: tex_wallpaper("d6cfa6", "c2b98a", 6, baseboard="8a7a5a"),
    "store_white": lambda: tex_wallpaper("d9d9d2", "cfcfc8", 7, baseboard="5a5a5a"),
    "fence_wood": lambda: tex_fence(8),
}
