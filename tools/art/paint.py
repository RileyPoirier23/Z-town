"""Painterly texture helpers: noise, colour variation, projecting flat textures onto iso faces."""
import math

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

RNG = np.random.default_rng(1234)


def hexc(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def value_noise(w, h, scale, seed=0, octaves=4, persistence=0.5):
    """Smooth multi-octave noise in [0,1], tileable-ish enough for small textures."""
    rng = np.random.default_rng(seed)
    out = np.zeros((h, w))
    amp, total = 1.0, 0.0
    s = scale
    for _ in range(octaves):
        gw, gh = max(2, int(w / s) + 2), max(2, int(h / s) + 2)
        grid = rng.random((gh, gw))
        img = Image.fromarray((grid * 255).astype(np.uint8)).resize((int(gw * s), int(gh * s)), Image.BICUBIC)
        arr = np.asarray(img, dtype=float)[:h, :w] / 255.0
        out += arr * amp
        total += amp
        amp *= persistence
        s = max(1.0, s / 2)
    return np.clip(out / total, 0, 1)


def tint(base, noise, amount=0.12, hue_shift=None):
    """Colour field: base colour varied by noise (brighter/darker), optional second colour blend."""
    b = np.array(base[:3], dtype=float)
    n = (noise - 0.5)[..., None] * 2 * amount
    rgb = b[None, None, :] * (1 + n)
    if hue_shift is not None:
        t = noise[..., None]
        rgb = rgb * (1 - 0.25 * t) + np.array(hue_shift[:3], dtype=float)[None, None, :] * 0.25 * t
    return np.clip(rgb, 0, 255)


def to_img(rgb, alpha=None):
    h, w = rgb.shape[:2]
    a = np.full((h, w), 255, dtype=np.uint8) if alpha is None else alpha.astype(np.uint8)
    return Image.fromarray(np.dstack([rgb.astype(np.uint8), a]), "RGBA")


def brush(img, strength=0.06, seed=0):
    """Fine directional grain that reads as paint strokes."""
    w, h = img.size
    rng = np.random.default_rng(seed)
    g = rng.random((h, w))
    g = np.asarray(Image.fromarray((g * 255).astype(np.uint8)).filter(ImageFilter.BoxBlur(1)), dtype=float) / 255
    arr = np.asarray(img, dtype=float)
    arr[..., :3] *= (1 + (g[..., None] - 0.5) * strength * 2)
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


def shade(img, f):
    arr = np.asarray(img, dtype=float).copy()
    arr[..., :3] *= f
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


def warp_onto(canvas, tex, quad):
    """Paints a flat texture onto a parallelogram/quad on the canvas.
    quad = [top-left, top-right, bottom-right, bottom-left] in canvas pixels, matching the
    texture's corners. Uses a perspective transform (exact for parallelograms)."""
    w, h = tex.size
    coeffs = _perspective_coeffs(quad, [(0, 0), (w, 0), (w, h), (0, h)])
    warped = tex.transform(canvas.size, Image.PERSPECTIVE, coeffs, Image.BICUBIC)
    mask = Image.new("L", canvas.size, 0)
    ImageDraw.Draw(mask).polygon([tuple(p) for p in quad], fill=255)
    a = np.minimum(np.asarray(warped.split()[3]), np.asarray(mask))
    warped.putalpha(Image.fromarray(a))
    canvas.alpha_composite(warped)


def _perspective_coeffs(src, dst):
    # maps output (canvas) coords -> input (texture) coords
    m = []
    for (x, y), (u, v) in zip(src, dst):
        m.append([x, y, 1, 0, 0, 0, -u * x, -u * y])
        m.append([0, 0, 0, x, y, 1, -v * x, -v * y])
    a = np.array(m, dtype=float)
    b = np.array([c for p in dst for c in p], dtype=float)
    return np.linalg.solve(a, b).tolist()


def outline(img, color=(25, 20, 18), alpha=110, width=1):
    """Soft dark outline around opaque pixels (sprites read better, like Zomboid's)."""
    a = img.split()[3]
    grown = a.filter(ImageFilter.MaxFilter(width * 2 + 1))
    edge = np.clip(np.asarray(grown, dtype=int) - np.asarray(a, dtype=int), 0, 255) * alpha // 255
    ol = Image.new("RGBA", img.size, color + (0,))
    ol.putalpha(Image.fromarray(edge.astype(np.uint8)))
    out = ol.copy()
    out.alpha_composite(img)
    return out


def ao_edges(img, depth=6, strength=0.25):
    """Darken near the edges of a flat texture (fake ambient occlusion)."""
    w, h = img.size
    y = np.minimum(np.arange(h), np.arange(h)[::-1])[:, None]
    x = np.minimum(np.arange(w), np.arange(w)[::-1])[None, :]
    d = np.minimum(np.minimum(x, y) / depth, 1.0)
    f = 1 - strength * (1 - d)
    return shade_map(img, f)


def shade_map(img, f):
    arr = np.asarray(img, dtype=float).copy()
    arr[..., :3] *= f[..., None]
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


def vgradient(img, top=1.0, bottom=0.85):
    w, h = img.size
    f = np.linspace(top, bottom, h)[:, None] * np.ones((1, w))
    return shade_map(img, f)


def periodic_noise(w, h, beta=2.2, seed=0):
    """Tileable noise (wraps at the edges) via a 1/f^beta spectrum. Values in [0,1]."""
    rng = np.random.default_rng(seed)
    white = rng.normal(size=(h, w))
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    f = np.sqrt(fx * fx + fy * fy)
    f[0, 0] = 1
    spec = np.fft.fft2(white) / f ** (beta / 2)
    spec[0, 0] = 0
    n = np.real(np.fft.ifft2(spec))
    n = (n - n.min()) / (n.max() - n.min() + 1e-9)
    return n


def slice_tiles(big, n):
    """Cuts a (128n x 128n) texture into n*n tiles, row-major (index = x + y*n)."""
    return [big.crop((x * 128, y * 128, x * 128 + 128, y * 128 + 128)) for y in range(n) for x in range(n)]
