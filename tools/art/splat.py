"""Tiny sphere renderer for organic things (people, trees, bushes).

Shapes are built from spheres (capsules = chains of spheres) in world metres, drawn with an
orthographic camera that matches the game's 2:1 projection, a z-buffer, Lambert shading from
iso.LIGHT, 2x supersampling, then a soft outline. Output reads like a painted sprite.
"""
import math

import numpy as np
from PIL import Image, ImageFilter

from iso import LIGHT, PX_PER_M

SS = 2  # supersampling
_V = np.array([math.cos(math.radians(30)) / math.sqrt(2), math.cos(math.radians(30)) / math.sqrt(2), 0.5])  # toward camera
_R = np.array([1 / math.sqrt(2), -1 / math.sqrt(2), 0.0])                                               # screen right
_U = np.cross(_V, _R)                                                                                    # screen up
if _U[2] < 0:
    _U = -_U


class Scene:
    def __init__(self, w, h, origin):
        self.w, self.h = w, h
        self.origin = origin  # canvas px of world (0,0,0)
        self.spheres = []      # (center xyz, radius, rgb, noise, ambient override, occluder)

    def sphere(self, c, r, rgb, noise=0.0, amb=None, occluder=False):
        """occluder=True: hides things behind it but isn't drawn (used to render clothing
        layers that line up with the body underneath)."""
        self.spheres.append((np.asarray(c, dtype=float), float(r), np.asarray(rgb[:3], dtype=float), noise, amb, occluder))

    def capsule(self, a, b, ra, rb, rgb, noise=0.0, step=0.16, occluder=False):
        a, b = np.asarray(a, dtype=float), np.asarray(b, dtype=float)
        length = np.linalg.norm(b - a)
        n = max(2, int(length / (min(ra, rb) * step)) + 1)
        for i in range(n):
            t = i / (n - 1)
            self.sphere(a + (b - a) * t, ra + (rb - ra) * t, rgb, noise, occluder=occluder)

    def render(self, ambient=0.42, outline_alpha=150, seed=0):
        W, H, s = self.w * SS, self.h * SS, PX_PER_M * SS
        color = np.zeros((H, W, 3))
        depth = np.full((H, W), -1e9)
        drawn = np.zeros((H, W), dtype=bool)
        rng = np.random.default_rng(seed)
        ox, oy = self.origin[0] * SS, self.origin[1] * SS
        for c, r, rgb, noise, amb, occ in self.spheres:
            cx = ox + np.dot(c, _R) * s
            cy = oy - np.dot(c, _U) * s
            cd = np.dot(c, _V) * s
            R = r * s
            x0, x1 = int(max(0, cx - R - 1)), int(min(W, cx + R + 2))
            y0, y1 = int(max(0, cy - R - 1)), int(min(H, cy + R + 2))
            if x0 >= x1 or y0 >= y1:
                continue
            ys, xs = np.mgrid[y0:y1, x0:x1]
            a = (xs + 0.5 - cx) / R
            b = (ys + 0.5 - cy) / R
            d2 = a * a + b * b
            inside = d2 < 1
            if not inside.any():
                continue
            nz = np.sqrt(np.clip(1 - d2, 0, 1))
            z = cd + nz * R
            sub = depth[y0:y1, x0:x1]
            win = inside & (z > sub)
            if not win.any():
                continue
            # world-space normal
            n = a[..., None] * _R + (-b)[..., None] * _U + nz[..., None] * _V
            lam = np.clip(n @ LIGHT, 0, 1)
            rim = np.clip(1 - nz, 0, 1) ** 3 * 0.15
            a_ = ambient if amb is None else amb
            shade = a_ + (1 - a_) * lam + rim
            col = rgb[None, None, :] * shade[..., None]
            if noise:
                col *= 1 + (rng.random(col.shape[:2])[..., None] - 0.5) * noise
            sub[win] = z[win]
            color[y0:y1, x0:x1][win] = col[win]
            drawn[y0:y1, x0:x1][win] = not occ
        alpha = drawn.astype(np.uint8) * 255
        img = Image.fromarray(np.dstack([np.clip(color, 0, 255).astype(np.uint8), alpha]), "RGBA")
        img = img.resize((self.w, self.h), Image.LANCZOS)
        return _outline(img, outline_alpha)


def _outline(img, alpha):
    a = img.split()[3]
    grown = a.filter(ImageFilter.MaxFilter(3))
    edge = np.clip(np.asarray(grown, dtype=int) - np.asarray(a, dtype=int), 0, 255) * alpha // 255
    ol = Image.new("RGBA", img.size, (22, 18, 16, 0))
    ol.putalpha(Image.fromarray(edge.astype(np.uint8)))
    ol.alpha_composite(img)
    return ol


def rot_z(p, ang):
    c, s = math.cos(ang), math.sin(ang)
    x, y, z = p
    return np.array([x * c - y * s, x * s + y * c, z])
