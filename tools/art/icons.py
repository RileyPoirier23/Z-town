"""Moodle and item icons (64x64), painted with simple shapes. Moodle pictograms are white on
transparent; the game puts them on a coloured badge by severity, like Zomboid's moodles."""
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

import paint as P

S = 64


def _img():
    return Image.new("RGBA", (S * 4, S * 4), (0, 0, 0, 0))  # draw 4x, downsample for smooth edges


def _done(img, outline=True):
    img = img.resize((S, S), Image.LANCZOS)
    return P.outline(img, alpha=150) if outline else img


W = (245, 242, 235, 255)


def moodle(kind):
    im = _img()
    d = ImageDraw.Draw(im)
    k = 4
    if kind == "hungry":          # fork and knife
        d.rectangle([22 * k, 12 * k, 25 * k, 52 * k], fill=W)
        for x in (18, 23, 28):
            d.rectangle([x * k, 12 * k, (x + 1.5) * k, 24 * k], fill=W)
        d.rectangle([18 * k, 23 * k, 29.5 * k, 27 * k], fill=W)
        d.polygon([(38 * k, 12 * k), (46 * k, 18 * k), (46 * k, 32 * k), (41 * k, 32 * k), (41 * k, 52 * k), (38 * k, 52 * k)], fill=W)
    elif kind == "thirsty":       # droplet
        d.polygon([(32 * k, 10 * k), (46 * k, 34 * k), (18 * k, 34 * k)], fill=W)
        d.ellipse([18 * k, 24 * k, 46 * k, 54 * k], fill=W)
    elif kind == "tired":         # zZ
        for (x, y, s) in ((16, 30, 16), (34, 14, 12)):
            d.line([(x * k, y * k), ((x + s) * k, y * k), (x * k, (y + s) * k), ((x + s) * k, (y + s) * k)], fill=W, width=4 * k)
    elif kind == "bored":         # flat mouth face
        d.ellipse([12 * k, 12 * k, 52 * k, 52 * k], outline=W, width=4 * k)
        d.ellipse([22 * k, 24 * k, 27 * k, 29 * k], fill=W)
        d.ellipse([37 * k, 24 * k, 42 * k, 29 * k], fill=W)
        d.line([(23 * k, 40 * k), (41 * k, 40 * k)], fill=W, width=4 * k)
    elif kind == "unhappy":       # frown
        d.ellipse([12 * k, 12 * k, 52 * k, 52 * k], outline=W, width=4 * k)
        d.ellipse([22 * k, 24 * k, 27 * k, 29 * k], fill=W)
        d.ellipse([37 * k, 24 * k, 42 * k, 29 * k], fill=W)
        d.arc([22 * k, 38 * k, 42 * k, 52 * k], 200, 340, fill=W, width=4 * k)
    elif kind == "stressed":      # lightning
        d.polygon([(36 * k, 8 * k), (18 * k, 36 * k), (30 * k, 36 * k), (26 * k, 56 * k), (46 * k, 26 * k), (34 * k, 26 * k)], fill=W)
    elif kind == "injured":       # bandage cross
        d.rounded_rectangle([26 * k, 10 * k, 38 * k, 54 * k], radius=4 * k, fill=W)
        d.rounded_rectangle([10 * k, 26 * k, 54 * k, 38 * k], radius=4 * k, fill=W)
    elif kind == "infected":      # three rings
        for (x, y) in ((32, 20), (21, 40), (43, 40)):
            d.ellipse([(x - 10) * k, (y - 10) * k, (x + 10) * k, (y + 10) * k], outline=W, width=4 * k)
        d.ellipse([28 * k, 30 * k, 36 * k, 38 * k], fill=W)
    return _done(im)


def item_icon(item_id):
    im = _img()
    d = ImageDraw.Draw(im)
    k = 4

    def can(col, label=None):
        d.rounded_rectangle([20 * k, 10 * k, 44 * k, 56 * k], radius=6 * k, fill=col)
        d.ellipse([20 * k, 6 * k, 44 * k, 14 * k], fill=(200, 200, 200, 255))
        d.rectangle([20 * k, 22 * k, 44 * k, 36 * k], fill=label or (240, 240, 235, 255))
        d.rectangle([22 * k, 12 * k, 25 * k, 54 * k], fill=(255, 255, 255, 60))
    if item_id == "mepsi_can":
        can((176, 40, 40, 255), (40, 70, 160, 255))
    elif item_id == "mepsi_2l":
        d.rounded_rectangle([22 * k, 16 * k, 42 * k, 58 * k], radius=6 * k, fill=(120, 40, 36, 255))
        d.rectangle([28 * k, 6 * k, 36 * k, 16 * k], fill=(176, 40, 40, 255))
        d.rectangle([22 * k, 28 * k, 42 * k, 40 * k], fill=(40, 70, 160, 255))
    elif item_id == "mepsi_case":
        d.rectangle([8 * k, 18 * k, 56 * k, 50 * k], fill=(176, 40, 40, 255))
        d.rectangle([8 * k, 28 * k, 56 * k, 38 * k], fill=(40, 70, 160, 255))
        d.polygon([(8 * k, 18 * k), (18 * k, 10 * k), (62 * k, 10 * k), (56 * k, 18 * k)], fill=(150, 30, 30, 255))
    elif item_id.startswith("cigarettes"):
        big = item_id.endswith("carton")
        x0, x1 = (10, 54) if big else (20, 44)
        d.rectangle([x0 * k, 12 * k, x1 * k, 56 * k], fill=(230, 226, 214, 255))
        d.rectangle([x0 * k, 12 * k, x1 * k, 26 * k], fill=(150, 40, 36, 255))
        if not big:
            for x in (24, 30, 36):
                d.rectangle([x * k, 6 * k, (x + 4) * k, 14 * k], fill=(240, 236, 226, 255))
                d.rectangle([x * k, 6 * k, (x + 4) * k, 8 * k], fill=(200, 140, 70, 255))
    elif item_id == "puffer":
        d.rounded_rectangle([24 * k, 8 * k, 40 * k, 40 * k], radius=5 * k, fill=(70, 120, 180, 255))
        d.polygon([(22 * k, 38 * k), (42 * k, 38 * k), (50 * k, 50 * k), (50 * k, 58 * k), (22 * k, 58 * k)], fill=(225, 225, 220, 255))
    elif item_id == "donair":
        d.polygon([(12 * k, 50 * k), (32 * k, 10 * k), (52 * k, 50 * k)], fill=(230, 210, 160, 255))
        d.polygon([(18 * k, 42 * k), (32 * k, 16 * k), (46 * k, 42 * k)], fill=(150, 80, 50, 255))
        d.polygon([(22 * k, 40 * k), (32 * k, 22 * k), (42 * k, 40 * k)], fill=(245, 240, 225, 255))
        d.ellipse([26 * k, 28 * k, 32 * k, 34 * k], fill=(190, 60, 50, 255))
    elif item_id == "donair_meat_frozen":
        d.rectangle([10 * k, 18 * k, 54 * k, 48 * k], fill=(200, 215, 230, 255))
        d.rectangle([16 * k, 24 * k, 48 * k, 42 * k], fill=(150, 90, 70, 255))
    elif item_id in ("canned_soup", "canned_beans"):
        can((150, 150, 150, 255), (200, 60, 40, 255) if item_id == "canned_soup" else (80, 120, 60, 255))
    elif item_id == "chips_bag":
        d.polygon([(16 * k, 8 * k), (48 * k, 8 * k), (52 * k, 56 * k), (12 * k, 56 * k)], fill=(220, 170, 40, 255))
        d.ellipse([22 * k, 24 * k, 42 * k, 42 * k], fill=(200, 60, 40, 255))
    elif item_id == "chocolate_bar":
        d.rectangle([10 * k, 22 * k, 54 * k, 42 * k], fill=(90, 50, 30, 255))
        d.rectangle([10 * k, 22 * k, 30 * k, 42 * k], fill=(160, 40, 40, 255))
    elif item_id == "water_bottle":
        d.rounded_rectangle([24 * k, 14 * k, 40 * k, 58 * k], radius=6 * k, fill=(170, 210, 235, 220))
        d.rectangle([28 * k, 6 * k, 36 * k, 14 * k], fill=(60, 110, 190, 255))
    elif item_id == "milk":
        d.rectangle([20 * k, 18 * k, 44 * k, 58 * k], fill=(240, 240, 236, 255))
        d.polygon([(20 * k, 18 * k), (32 * k, 6 * k), (44 * k, 18 * k)], fill=(220, 220, 214, 255))
        d.rectangle([20 * k, 30 * k, 44 * k, 40 * k], fill=(60, 110, 190, 255))
    elif item_id == "bread":
        d.rounded_rectangle([8 * k, 22 * k, 56 * k, 50 * k], radius=12 * k, fill=(200, 150, 90, 255))
        for x in (20, 32, 44):
            d.line([(x * k, 26 * k), ((x - 4) * k, 34 * k)], fill=(150, 100, 60, 255), width=2 * k)
    elif item_id in ("baseball_bat", "crowbar", "hammer", "kitchen_knife", "frying_pan"):
        if item_id == "baseball_bat":
            d.line([(14 * k, 54 * k), (50 * k, 10 * k)], fill=(170, 128, 82, 255), width=8 * k)
            d.line([(14 * k, 54 * k), (22 * k, 44 * k)], fill=(60, 50, 40, 255), width=6 * k)
        elif item_id == "crowbar":
            d.line([(16 * k, 54 * k), (44 * k, 14 * k)], fill=(170, 40, 40, 255), width=5 * k)
            d.line([(44 * k, 14 * k), (52 * k, 12 * k)], fill=(170, 40, 40, 255), width=5 * k)
        elif item_id == "hammer":
            d.line([(18 * k, 56 * k), (38 * k, 20 * k)], fill=(150, 110, 70, 255), width=5 * k)
            d.polygon([(28 * k, 10 * k), (52 * k, 22 * k), (48 * k, 30 * k), (24 * k, 18 * k)], fill=(120, 120, 125, 255))
        elif item_id == "kitchen_knife":
            d.line([(16 * k, 52 * k), (26 * k, 40 * k)], fill=(40, 34, 30, 255), width=6 * k)
            d.polygon([(26 * k, 40 * k), (50 * k, 10 * k), (32 * k, 44 * k)], fill=(200, 205, 210, 255))
        else:
            d.ellipse([10 * k, 14 * k, 42 * k, 46 * k], fill=(50, 50, 55, 255))
            d.line([(38 * k, 38 * k), (56 * k, 56 * k)], fill=(40, 40, 44, 255), width=6 * k)
    elif item_id == "gas_can":
        d.rounded_rectangle([12 * k, 16 * k, 50 * k, 56 * k], radius=4 * k, fill=(190, 40, 34, 255))
        d.line([(44 * k, 16 * k), (54 * k, 6 * k)], fill=(220, 190, 50, 255), width=4 * k)
        d.rounded_rectangle([18 * k, 8 * k, 34 * k, 18 * k], radius=3 * k, outline=(150, 30, 26, 255), width=3 * k)
    elif item_id == "bandage":
        d.ellipse([12 * k, 16 * k, 52 * k, 48 * k], fill=(240, 236, 226, 255))
        d.ellipse([24 * k, 26 * k, 40 * k, 38 * k], fill=(210, 205, 195, 255))
    elif item_id == "disinfectant":
        d.rounded_rectangle([22 * k, 16 * k, 42 * k, 58 * k], radius=4 * k, fill=(150, 90, 50, 255))
        d.rectangle([26 * k, 8 * k, 38 * k, 16 * k], fill=(240, 240, 240, 255))
        d.rectangle([22 * k, 30 * k, 42 * k, 42 * k], fill=(240, 240, 240, 255))
    elif item_id == "flashlight":
        d.rounded_rectangle([12 * k, 26 * k, 40 * k, 38 * k], radius=3 * k, fill=(60, 60, 64, 255))
        d.polygon([(40 * k, 22 * k), (52 * k, 18 * k), (52 * k, 46 * k), (40 * k, 42 * k)], fill=(80, 80, 86, 255))
    elif item_id == "planks":
        for i, y in enumerate((18, 30, 42)):
            d.rectangle([8 * k, y * k, 56 * k, (y + 9) * k], fill=(160 - i * 10, 120 - i * 8, 80, 255))
    elif item_id == "nails_box":
        d.rectangle([14 * k, 20 * k, 50 * k, 52 * k], fill=(170, 140, 90, 255))
        for x in (20, 30, 40):
            d.line([(x * k, 10 * k), (x * k, 24 * k)], fill=(150, 150, 155, 255), width=2 * k)
    elif item_id == "reruns_box":
        d.rectangle([8 * k, 24 * k, 56 * k, 44 * k], fill=(40, 40, 44, 255))
        d.rectangle([12 * k, 30 * k, 30 * k, 34 * k], fill=(90, 200, 120, 255))
        d.ellipse([44 * k, 30 * k, 50 * k, 36 * k], fill=(200, 60, 50, 255))
    else:
        d.rounded_rectangle([14 * k, 14 * k, 50 * k, 50 * k], radius=6 * k, fill=(150, 140, 120, 255))
    return _done(im)


MOODLES = ["hungry", "thirsty", "tired", "bored", "unhappy", "stressed", "injured", "infected"]
