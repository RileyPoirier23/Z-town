"""Cars as two layers (body: neutral grey, tinted in game; details: glass, tires, lights),
16 headings, rendered from simple 3D boxes with the game's camera. Sheet = one row of 16
frames, frame 480 x 360, car centre on the ground at (240, 240). Heading k*22.5°, 0 = +x."""
import math

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

from iso import LIGHT, PX_PER_M

FW, FH = 480, 360
CENTER = (240, 240)
DIRS = 16
_V = np.array([math.cos(math.radians(30)) / math.sqrt(2), math.cos(math.radians(30)) / math.sqrt(2), 0.5])
_R = np.array([1 / math.sqrt(2), -1 / math.sqrt(2), 0.0])
_U = np.array([-0.3535534, -0.3535534, 0.8660254])

BODY = (205, 205, 205)
GLASS = (60, 78, 92)
TIRE = (32, 32, 34)
HUB = (140, 140, 140)
HEAD = (240, 232, 200)
TAIL = (170, 30, 26)
TRIM = (40, 40, 42)
BED = (70, 70, 72)


def model(kind):
    """Boxes: (x0,x1, y0,y1, z0,z1, colour, is_body). Car-local: +x forward, +y left, +z up."""
    b = []
    if kind == "van":
        L, W = 2.5, 1.05
        b.append((-L, L, -W, W, 0.35, 2.0, BODY, True))
        b.append((L - 0.05, L + 0.02, -W + 0.1, W - 0.1, 1.15, 1.75, GLASS, False))           # windshield
        b.append((-0.2, L - 0.4, W, W + 0.02, 1.25, 1.75, GLASS, False))                       # side windows
        b.append((-0.2, L - 0.4, -W - 0.02, -W, 1.25, 1.75, GLASS, False))
    elif kind == "pickup":
        L, W = 2.4, 0.98
        b.append((-L, L, -W, W, 0.4, 1.05, BODY, True))                                         # body
        b.append((-0.2, 1.2, -W + 0.05, W - 0.05, 1.05, 1.75, BODY, True))                      # cab
        b.append((1.18, 1.22, -W + 0.12, W - 0.12, 1.12, 1.68, GLASS, False))
        b.append((-0.1, 1.1, W - 0.04, W - 0.02, 1.15, 1.65, GLASS, False))
        b.append((-0.1, 1.1, -W + 0.02, -W + 0.04, 1.15, 1.65, GLASS, False))
        b.append((-L + 0.1, -0.25, -W + 0.12, W - 0.12, 0.9, 1.06, BED, False))                 # bed floor
    else:
        L, W = 2.2, 0.92
        b.append((-L, L, -W, W, 0.35, 0.95, BODY, True))                                        # lower body
        b.append((-1.25, 0.95, -W + 0.08, W - 0.08, 0.95, 1.42, GLASS, False))                  # glasshouse
        b.append((-1.3, 0.85, -W + 0.12, W - 0.12, 1.4, 1.48, BODY, True))                      # roof
        b.append((-1.25, -1.1, -W + 0.08, W - 0.08, 0.95, 1.42, BODY, True))                    # C pillar
        b.append((-0.2, -0.05, W - 0.1, W - 0.06, 0.95, 1.42, BODY, True))                      # B pillars
        b.append((-0.2, -0.05, -W + 0.06, -W + 0.1, 0.95, 1.42, BODY, True))
    # wheels
    for x in (L - 0.75, -L + 0.75):
        for y in (W - 0.12, -W + 0.12):
            b.append((x - 0.36, x + 0.36, y - 0.14, y + 0.14, 0.0, 0.7, TIRE, False))
            b.append((x - 0.16, x + 0.16, y - 0.15 if y < 0 else y + 0.13, y - 0.13 if y < 0 else y + 0.15, 0.2, 0.5, HUB, False))
    # lights, bumpers
    b.append((L - 0.02, L + 0.03, W - 0.4, W - 0.1, 0.62, 0.8, HEAD, False))
    b.append((L - 0.02, L + 0.03, -W + 0.1, -W + 0.4, 0.62, 0.8, HEAD, False))
    b.append((-L - 0.03, -L + 0.02, W - 0.35, W - 0.1, 0.65, 0.85, TAIL, False))
    b.append((-L - 0.03, -L + 0.02, -W + 0.1, -W + 0.35, 0.65, 0.85, TAIL, False))
    b.append((L, L + 0.08, -W + 0.05, W - 0.05, 0.3, 0.45, TRIM, False))
    b.append((-L - 0.08, -L, -W + 0.05, W - 0.05, 0.3, 0.45, TRIM, False))
    return b


def faces(box):
    x0, x1, y0, y1, z0, z1, col, body = box
    P = lambda x, y, z: np.array([x, y, z])
    return [
        ([P(x0, y0, z1), P(x1, y0, z1), P(x1, y1, z1), P(x0, y1, z1)], np.array([0, 0, 1.0])),
        ([P(x1, y0, z0), P(x1, y1, z0), P(x1, y1, z1), P(x1, y0, z1)], np.array([1.0, 0, 0])),
        ([P(x0, y0, z0), P(x0, y0, z1), P(x0, y1, z1), P(x0, y1, z0)], np.array([-1.0, 0, 0])),
        ([P(x0, y1, z0), P(x0, y1, z1), P(x1, y1, z1), P(x1, y1, z0)], np.array([0, 1.0, 0])),
        ([P(x0, y0, z0), P(x1, y0, z0), P(x1, y0, z1), P(x0, y0, z1)], np.array([0, -1.0, 0])),
    ]


def render(kind, heading):
    ss = 2
    W2, H2 = FW * ss, FH * ss
    full = Image.new("RGBA", (W2, H2), (0, 0, 0, 0))
    mask = Image.new("L", (W2, H2), 0)
    dfull, dmask = ImageDraw.Draw(full), ImageDraw.Draw(mask)
    c, s = math.cos(heading), math.sin(heading)
    rot = np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])
    polys = []
    for box in model(kind):
        for pts, n in faces(box):
            wn = rot @ n
            if wn @ _V <= 0.01:
                continue
            wp = [rot @ p for p in pts]
            depth = float(np.mean([p @ _V for p in wp]))
            scr = [(CENTER[0] * ss + (p @ _R) * PX_PER_M * ss, CENTER[1] * ss - (p @ _U) * PX_PER_M * ss) for p in wp]
            shade = 0.5 + 0.55 * max(0.0, float(wn @ LIGHT))
            if wn[2] > 0.9:
                shade = 1.0
            polys.append((depth, scr, box[6], shade, box[7]))
    # shadow under the car
    sh = [rot @ np.array([x, y, 0]) for x, y in ((-2.4, -1.1), (2.4, -1.1), (2.4, 1.1), (-2.4, 1.1))]
    shadow = Image.new("RGBA", (W2, H2), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).polygon([(CENTER[0] * ss + (p @ _R) * PX_PER_M * ss + 6, CENTER[1] * ss - (p @ _U) * PX_PER_M * ss + 6) for p in sh], fill=(0, 0, 0, 90))
    full.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(10)))
    for depth, scr, col, shade, body in sorted(polys, key=lambda p: p[0]):
        rgb = tuple(int(min(255, v * shade)) for v in col)
        dfull.polygon(scr, fill=rgb + (255,), outline=tuple(int(v * 0.55) for v in rgb) + (255,))
        dmask.polygon(scr, fill=255 if body else 0, outline=255 if body else 0)
    full = full.resize((FW, FH), Image.LANCZOS)
    mask = mask.resize((FW, FH), Image.LANCZOS)
    arr = np.asarray(full).copy()
    m = np.asarray(mask) > 127
    body = arr.copy()
    body[~m, 3] = 0
    det = arr.copy()
    det[m, 3] = 0
    return Image.fromarray(body, "RGBA"), Image.fromarray(det, "RGBA")


def sheets(kind):
    body = Image.new("RGBA", (FW * DIRS, FH), (0, 0, 0, 0))
    det = Image.new("RGBA", (FW * DIRS, FH), (0, 0, 0, 0))
    for k in range(DIRS):
        b, d = render(kind, k * 2 * math.pi / DIRS)
        body.alpha_composite(b, (k * FW, 0))
        det.alpha_composite(d, (k * FW, 0))
    return body, det


MODELS = ["sedan", "pickup", "van"]
