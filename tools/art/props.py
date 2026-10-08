"""Trees, bushes and other organic props (sphere-rendered)."""
import math

import numpy as np

from iso import LIGHT
from splat import Scene


def leaves(sc, rng, center, radii, count, palette, size=(0.05, 0.09), hollow=0.35):
    """Fills a crown with small leaf blobs, each pre-shaded by where it sits on the crown
    (lit side bright, underside and core dark) so the whole crown reads as one painted mass."""
    for _ in range(count):
        v = rng.normal(size=3)
        v /= np.linalg.norm(v)
        rr = rng.uniform(hollow, 1.0) ** 0.5
        # lumpy outline: push some leaves out in clumps
        lump = 1 + 0.18 * math.sin(v[0] * 5 + v[1] * 3) * math.cos(v[2] * 4)
        p = center + v * rr * radii * lump
        macro = max(0.0, float(v @ LIGHT)) * 0.6 + 0.4 * rr + rng.uniform(-0.08, 0.08)
        col = palette[rng.integers(0, len(palette))] * (0.55 + 0.6 * macro)
        sc.sphere(p, rng.uniform(*size), col, 0.25, amb=0.8)

TREE_W, TREE_H, TREE_ORIGIN = 352, 512, (176, 448)


def tree_maple(seed, autumn=False):
    rng = np.random.default_rng(seed)
    sc = Scene(TREE_W, TREE_H, TREE_ORIGIN)
    base = np.array([0.5, 0.5, 0.0])
    trunk = np.array([92, 72, 56.0])
    top = base + [rng.uniform(-0.1, 0.1), rng.uniform(-0.1, 0.1), 2.6]
    sc.capsule(base, top, 0.16, 0.1, trunk, 0.15)
    for _ in range(3):  # branches
        d = np.array([rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(0.6, 1.0)])
        sc.capsule(base + [0, 0, 1.9], base + [0, 0, 1.9] + d * 0.9, 0.07, 0.04, trunk, 0.15)
    greens = [np.array(c, dtype=float) for c in ([78, 98, 52], [92, 112, 58], [66, 86, 46], [104, 118, 62])]
    if autumn:
        greens = [np.array(c, dtype=float) for c in ([170, 92, 40], [190, 130, 50], [150, 70, 36], [120, 100, 50])]
    center = base + [0, 0, 3.3]
    leaves(sc, rng, center, np.array([1.6, 1.6, 1.2]), 2600, greens)
    return sc.render(ambient=0.38, outline_alpha=110, seed=seed)


def tree_spruce(seed):
    rng = np.random.default_rng(seed)
    sc = Scene(TREE_W, TREE_H, TREE_ORIGIN)
    base = np.array([0.5, 0.5, 0.0])
    sc.capsule(base, base + [0, 0, 1.0], 0.13, 0.11, np.array([84, 66, 52.0]), 0.15)
    greens = [np.array(c, dtype=float) for c in ([46, 72, 52], [56, 82, 58], [40, 62, 46])]
    height = rng.uniform(4.6, 5.4)
    for i in range(2400):
        h = rng.uniform(0.6, height)
        radius = (1 - (h - 0.6) / (height - 0.6)) ** 0.9 * 1.45 + 0.08
        # tiers: branches droop in layers
        tier = (h * 2.2) % 1.0
        radius *= 0.75 + 0.25 * tier
        a = rng.uniform(0, 2 * math.pi)
        rr = radius * rng.uniform(0.5, 1.0) ** 0.5
        p = base + [math.cos(a) * rr, math.sin(a) * rr, h - tier * 0.15]
        lit = max(0.0, math.cos(a) * LIGHT[0] + math.sin(a) * LIGHT[1]) * 0.5 + 0.5 * (rr / max(radius, 0.01))
        sc.sphere(p, rng.uniform(0.05, 0.08), greens[rng.integers(0, 3)] * (0.55 + 0.6 * lit), 0.25, amb=0.8)
    return sc.render(ambient=0.36, outline_alpha=110, seed=seed)


def bush(seed):
    rng = np.random.default_rng(seed)
    sc = Scene(160, 192, (80, 160))
    greens = [np.array(c, dtype=float) for c in ([70, 92, 50], [84, 104, 56], [62, 80, 44])]
    leaves(sc, rng, np.array([0.5, 0.5, 0.42]), np.array([0.55, 0.55, 0.45]), 700, greens, size=(0.04, 0.07), hollow=0.5)
    return sc.render(ambient=0.4, outline_alpha=110, seed=seed)


PROPS = {
    "tree_maple_0": (lambda: tree_maple(1), TREE_ORIGIN),
    "tree_maple_1": (lambda: tree_maple(2), TREE_ORIGIN),
    "tree_maple_autumn": (lambda: tree_maple(3, autumn=True), TREE_ORIGIN),
    "tree_spruce_0": (lambda: tree_spruce(4), TREE_ORIGIN),
    "tree_spruce_1": (lambda: tree_spruce(5), TREE_ORIGIN),
    "bush_0": (lambda: bush(6), (80, 160)),
    "bush_1": (lambda: bush(7), (80, 160)),
}
