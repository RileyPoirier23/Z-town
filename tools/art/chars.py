"""Layered people (Zomboid-style paper doll).

Every character is drawn as layers that share one frame grid:
    body (skin, tinted) -> bottom -> shoes -> top -> outer -> hair -> hat -> held item
Each garment is rendered in neutral light grey so the game can tint it any colour, and each
layer is rendered with the body as an invisible occluder, so arms and legs crossing in front
cut the right holes and layers stack correctly in any combination.

Sheets: game/assets/gen/characters/<layer>_<anim>.png
  rows = 8 facings (world angle k*45°, k=0..7, 0 = +x), columns = frames.
  Frame 192 x 224, feet at (96, 196).
"""
import math

import numpy as np
from PIL import Image

from splat import Scene, rot_z

FW, FH = 192, 224
FEET = (96, 196)
GREY = np.array([212.0, 212.0, 212.0])   # garments: tinted in game
SKIN = np.array([236.0, 226.0, 220.0])   # body: tinted by skin tone in game
DARK = np.array([60.0, 54.0, 50.0])

# slot -> garments. Order here is draw order.
GARMENTS = {
    "bottom": ["pants", "shorts"],
    "shoes": ["sneakers", "boots"],
    "top": ["tshirt", "sweater", "hoodie"],
    "outer": ["jacket"],
    "hair": ["hair_short", "hair_long", "hair_bun", "hair_buzz"],
    "hat": ["toque", "ballcap"],
    "held": ["bat", "cigarette", "can"],
}

ANIMS = {
    # everyone who walks around (player, Dad, survivors)
    "idle": 8, "walk": 8, "run": 8, "sneak": 8, "swing": 8,
    # zombies (same clothes, different body language)
    "z_idle": 8, "shamble": 8, "attack": 8, "dead": 1,
    # sitting (memere's calm states only)
    "idle_chair": 8, "watch_tv": 8, "nap_chair": 8, "smoke": 8, "drink_mepsi": 8,
}
# held items only exist where they're used
HELD_ANIMS = {"bat": ["swing"], "cigarette": ["smoke"], "can": ["drink_mepsi"]}


def pose(anim, frame, nframes):
    """Joint angles for a frame. Local space: +x forward, +y left, +z up."""
    t = frame / nframes * 2 * math.pi
    p = dict(hipL=0.0, hipR=0.0, kneeL=0.0, kneeR=0.0, shL=0.0, shR=0.0, elL=0.15, elR=0.15,
             shLout=0.08, shRout=0.08, lean=0.0, bob=0.0, crouch=0.0, headTilt=0.0, headTurn=0.0,
             sit=False, lie=False, armRaiseL=0.0, armRaiseR=0.0)
    if anim == "idle":
        p["bob"] = 0.006 * math.sin(t)
        p["shL"], p["shR"] = 0.04 * math.sin(t), -0.04 * math.sin(t)
    elif anim in ("walk", "run", "sneak", "shamble"):
        amp = {"walk": 0.42, "run": 0.75, "sneak": 0.3, "shamble": 0.28}[anim]
        p["hipL"], p["hipR"] = amp * math.sin(t), -amp * math.sin(t)
        p["kneeL"] = max(0.0, -math.sin(t + 0.6)) * amp * 1.4
        p["kneeR"] = max(0.0, math.sin(t + 0.6)) * amp * 1.4
        p["bob"] = abs(math.sin(t)) * (0.035 if anim == "run" else 0.02)
        if anim == "shamble":
            p["armRaiseL"] = p["armRaiseR"] = 1.25
            p["shL"], p["shR"] = 0.08 * math.sin(t), -0.08 * math.sin(t + 0.4)
            p["lean"], p["headTilt"] = 0.22, 0.25
        else:
            p["shL"], p["shR"] = -amp * 0.8 * math.sin(t), amp * 0.8 * math.sin(t)
            if anim == "run":
                p["elL"] = p["elR"] = 1.3
                p["lean"] = 0.18
            if anim == "sneak":
                p["crouch"] = 0.22
                p["lean"] = 0.3
                p["elL"] = p["elR"] = 0.9
                p["armRaiseL"] = p["armRaiseR"] = 0.4
    elif anim == "swing":
        k = frame / (nframes - 1)
        sw = min(1, max(0, (k - 0.25) / 0.45))
        p["armRaiseR"] = 1.9 - 1.9 * sw
        p["shRout"] = 0.5 - 0.9 * sw
        p["armRaiseL"] = p["armRaiseR"] * 0.8
        p["shLout"] = -0.2
        p["lean"] = 0.12 * math.sin(k * math.pi)
    elif anim == "z_idle":
        p["bob"] = 0.008 * math.sin(t)
        p["armRaiseL"], p["armRaiseR"] = 0.35 + 0.05 * math.sin(t), 0.2
        p["lean"], p["headTilt"] = 0.15, 0.3 + 0.05 * math.sin(t * 0.5)
    elif anim == "attack":
        k = math.sin(frame / nframes * math.pi)
        p["armRaiseL"] = p["armRaiseR"] = 1.3 + 0.3 * k
        p["lean"], p["headTilt"] = 0.2 + 0.25 * k, 0.2
    elif anim == "dead":
        p["lie"] = True
    elif anim in ("idle_chair", "watch_tv", "nap_chair", "smoke", "drink_mepsi"):
        p["sit"] = True
        p["bob"] = 0.004 * math.sin(t)
        if anim == "nap_chair":
            p["headTilt"], p["headTurn"] = 0.45, 0.3
        if anim == "smoke":
            up = max(0.0, math.sin(t))
            p["armRaiseR"], p["elR"] = 0.5 + 1.2 * up, 0.4 + 1.9 * up
        if anim == "drink_mepsi":
            up = max(0.0, math.sin(t))
            p["armRaiseR"], p["elR"] = 0.5 + 1.0 * up, 0.4 + 1.6 * up
    return p


def limb(origin, raise_fwd, out, length, bend, down=True):
    d1 = np.array([math.sin(raise_fwd), 0.0, -math.cos(raise_fwd)])
    if origin[1] != 0:
        d1 = d1 * math.cos(out) + np.array([0, math.copysign(1, origin[1]) * math.sin(out), 0])
    mid = origin + d1 * length[0]
    ang = raise_fwd + (bend if down else -bend)
    end = mid + np.array([math.sin(ang), 0.0, -math.cos(ang)]) * length[1]
    return mid, end


def skeleton(anim, frame, nframes):
    p = pose(anim, frame, nframes)
    j = {"p": p}
    if p["sit"]:
        pelvis = np.array([0.0, 0.0, 0.52])
        j["hipL"], j["hipR"] = pelvis + [0, 0.1, -0.02], pelvis + [0, -0.1, -0.02]
        j["kneeL"], j["kneeR"] = j["hipL"] + [0.42, 0.02, 0.0], j["hipR"] + [0.42, -0.02, 0.0]
        j["ankL"], j["ankR"] = j["kneeL"] + [0.05, 0, -0.42], j["kneeR"] + [0.05, 0, -0.42]
    else:
        pelvis = np.array([0.0, 0.0, 0.94 + p["bob"] - p["crouch"]])
        j["hipL"], j["hipR"] = pelvis + [0, 0.1, -0.03], pelvis + [0, -0.1, -0.03]
        bendL = p["kneeL"] + p["crouch"] * 1.6
        bendR = p["kneeR"] + p["crouch"] * 1.6
        j["kneeL"], j["ankL"] = limb(j["hipL"], p["hipL"] + p["crouch"] * 0.8, 0.0, (0.44, 0.44), bendL, down=False)
        j["kneeR"], j["ankR"] = limb(j["hipR"], p["hipR"] + p["crouch"] * 0.8, 0.0, (0.44, 0.44), bendR, down=False)
        # keep feet on the ground when crouching
        if p["crouch"]:
            drop = min(j["ankL"][2], j["ankR"][2]) - 0.06
            for k in ("hipL", "hipR", "kneeL", "kneeR", "ankL", "ankR"):
                j[k] = j[k] - [0, 0, drop]
            pelvis = pelvis - [0, 0, drop]
    j["pelvis"] = pelvis
    lean = p["lean"]
    j["chest"] = pelvis + [0.32 * math.sin(lean), 0, 0.34 * math.cos(lean)]
    j["neck"] = pelvis + [0.52 * math.sin(lean), 0, 0.53 * math.cos(lean)]
    j["head"] = j["neck"] + [0.04 + 0.08 * math.sin(p["headTilt"]), 0.08 * math.sin(p["headTurn"]), 0.13]
    for side, key in ((1, "L"), (-1, "R")):
        sh = j["neck"] + [0, 0.19 * side, -0.08]
        raise_ = (p["armRaiseL"] if side == 1 else p["armRaiseR"]) + (p["shL"] if side == 1 else p["shR"])
        out = p["shLout"] if side == 1 else p["shRout"]
        el = p["elL"] if side == 1 else p["elR"]
        if p["sit"] and (side == 1 or (anim not in ("smoke", "drink_mepsi"))):
            raise_, el = 0.55, 0.6  # hands resting
        j["sh" + key] = sh
        j["el" + key], j["hand" + key] = limb(sh, raise_, out, (0.29, 0.27), el)
    return j


def legs(scene, W, j, r_thigh, r_shin, upto=1.0, color=GREY, noise=0.05, occ=False):
    for s in "LR":
        hip, knee, ank = j["hip" + s], j["knee" + s], j["ank" + s]
        scene.capsule(W(hip), W(knee), r_thigh, r_thigh * 0.8, color, noise, occluder=occ)
        if upto > 0.5:
            end = knee + (ank - knee) * min(1.0, (upto - 0.5) * 2)
            scene.capsule(W(knee), W(end), r_shin, r_shin * 0.85, color, noise, occluder=occ)


def torso(scene, W, j, r, color=GREY, noise=0.05, occ=False, low=0.0):
    pel, chest, neck = j["pelvis"], j["chest"], j["neck"]
    for side in (-1, 0, 1):
        off = np.array([0, 0.07 * side, 0])
        scene.capsule(W(pel + off + [0, 0, low]), W(chest + off), r, r + 0.01, color, noise, occluder=occ)
        scene.capsule(W(chest + off), W(neck + off * 0.8 + [0, 0, -0.06]), r + 0.01, r - 0.02, color, noise, occluder=occ)
    if j["p"]["sit"]:
        scene.sphere(W(pel + [0.05, 0, 0.02]), r + 0.01, color, noise, occluder=occ)


def arms(scene, W, j, r, length=1.0, color=GREY, noise=0.05, occ=False):
    for s in "LR":
        sh, el, hand = j["sh" + s], j["el" + s], j["hand" + s]
        if length <= 0.5:
            scene.capsule(W(sh), W(sh + (el - sh) * length * 2), r, r * 0.95, color, noise, occluder=occ)
        else:
            scene.capsule(W(sh), W(el), r, r * 0.92, color, noise, occluder=occ)
            scene.capsule(W(el), W(el + (hand - el) * (length - 0.5) * 2), r * 0.92, r * 0.82, color, noise, occluder=occ)


def body(scene, W, j, occ=False):
    """Bare body (tinted by skin tone): legs, torso, arms, hands, neck, head with a face."""
    c = SKIN
    legs(scene, W, j, 0.072, 0.052, color=c, noise=0.02, occ=occ)
    for s in "LR":
        ank = j["ank" + s]
        scene.capsule(W(ank + [0.0, 0, -0.02]), W(ank + [0.13, 0, -0.04]), 0.04, 0.036, c, 0.02, occluder=occ)
    torso(scene, W, j, 0.12, c, 0.02, occ)
    arms(scene, W, j, 0.048, 1.0, c, 0.02, occ)
    for s in "LR":
        scene.sphere(W(j["hand" + s]), 0.043, c, 0.02, occluder=occ)
    neck = j["neck"]
    scene.capsule(W(neck + [0, 0, -0.04]), W(neck + [0, 0, 0.05]), 0.05, 0.05, c, 0.02, occluder=occ)
    head = j["head"]
    scene.sphere(W(head), 0.105, c, 0.02, occluder=occ)
    if occ:
        return
    scene.sphere(W(head + [0.1, 0, -0.01]), 0.022, c * 0.95)
    for side in (-1, 1):
        scene.sphere(W(head + [0.088, 0.04 * side, 0.02]), 0.016, DARK * 0.6)
        scene.capsule(W(head + [0.09, 0.02 * side, 0.045]), W(head + [0.085, 0.06 * side, 0.047]), 0.01, 0.01, c * 0.55)
    # underwear so nobody is ever shown bare
    scene.capsule(W(j["pelvis"] + [0, 0, -0.05]), W(j["pelvis"] + [0, 0, 0.05]), 0.135, 0.135, c * 0.82, 0.02)


def garment(scene, W, j, gid):
    """Adds one garment's geometry (neutral grey) to the scene."""
    p = j["p"]
    if gid == "pants":
        torso(scene, W, j, 0.135, low=-0.06) if False else None
        scene.capsule(W(j["pelvis"] + [0, 0, -0.06]), W(j["pelvis"] + [0, 0, 0.1]), 0.15, 0.142, GREY, 0.06)
        legs(scene, W, j, 0.086, 0.064)
    elif gid == "shorts":
        scene.capsule(W(j["pelvis"] + [0, 0, -0.06]), W(j["pelvis"] + [0, 0, 0.1]), 0.15, 0.142, GREY, 0.06)
        legs(scene, W, j, 0.088, 0.0, upto=0.45)
    elif gid in ("sneakers", "boots"):
        for s in "LR":
            ank = j["ank" + s]
            if gid == "boots":
                knee = j["knee" + s]
                scene.capsule(W(ank), W(ank + (knee - ank) * 0.35), 0.062, 0.066, GREY, 0.05)
            scene.capsule(W(ank + [-0.01, 0, -0.02]), W(ank + [0.15, 0, -0.035]), 0.052, 0.046, GREY, 0.04)
    elif gid in ("tshirt", "sweater", "hoodie"):
        torso(scene, W, j, 0.138 if gid != "hoodie" else 0.145, noise=0.05)
        arms(scene, W, j, 0.062 if gid == "tshirt" else 0.06, 0.32 if gid == "tshirt" else 0.95)
        if gid == "hoodie":
            neck, head = j["neck"], j["head"]
            scene.capsule(W(neck + [-0.08, 0, -0.04]), W(head + [-0.1, 0, -0.06]), 0.1, 0.09, GREY, 0.06)  # hood
            scene.sphere(W(j["chest"] + [0.12, 0, -0.18]), 0.07, GREY * 0.93, 0.05)                          # pocket
    elif gid == "jacket":
        torso(scene, W, j, 0.162, noise=0.05)
        arms(scene, W, j, 0.074, 0.97)
        scene.capsule(W(j["neck"] + [0, 0.06, -0.05]), W(j["neck"] + [0, -0.06, -0.05]), 0.07, 0.07, GREY, 0.05)  # collar
    elif gid.startswith("hair"):
        head = j["head"]
        style = gid[5:]
        cap = [(-0.03, 0.04, 0.1), (-0.06, 0.0, 0.092), (0.0, 0.06, 0.088), (0.03, 0.05, 0.08)]
        if style == "buzz":
            cap = [(-0.02, 0.03, 0.1), (0.0, 0.05, 0.088)]
        for dx, dz, r in cap:
            scene.sphere(W(head + [dx, 0, dz]), r if style != "buzz" else r * 0.98, GREY, 0.12)
        if style == "long":
            for k in range(6):
                for side in (-1, 1):
                    scene.sphere(W(head + [-0.06 - 0.01 * k, 0.06 * side, -0.04 - 0.05 * k]), 0.06, GREY, 0.12)
        if style == "bun":
            scene.sphere(W(head + [-0.1, 0, 0.06]), 0.06, GREY, 0.12)
    elif gid == "toque":
        head = j["head"]
        # sits on the crown, above the brows
        scene.sphere(W(head + [-0.035, 0, 0.065]), 0.098, GREY, 0.08)
        scene.capsule(W(head + [-0.02, 0.0, 0.045]), W(head + [-0.04, 0, 0.06]), 0.1, 0.1, GREY * 0.9, 0.08)
        scene.sphere(W(head + [-0.04, 0, 0.17]), 0.035, GREY, 0.1)  # pompom
    elif gid == "ballcap":
        head = j["head"]
        scene.sphere(W(head + [-0.035, 0, 0.07]), 0.096, GREY, 0.05)
        scene.capsule(W(head + [0.06, 0.0, 0.075]), W(head + [0.16, 0.0, 0.07]), 0.04, 0.04, GREY, 0.05)  # brim
    elif gid == "bat":
        hand = j["handR"]
        d = np.array([math.sin(p["armRaiseR"] + 0.9), -0.3 * p["shRout"], -math.cos(p["armRaiseR"] + 0.9)])
        d /= np.linalg.norm(d)
        scene.capsule(W(hand), W(hand + d * 0.8), 0.022, 0.042, np.array([170, 128, 82.0]), 0.05)
    elif gid == "cigarette":
        hand = j["handR"]
        scene.capsule(W(hand + [0.02, 0, 0.0]), W(hand + [0.09, 0, 0.01]), 0.008, 0.008, np.array([238, 235, 228.0]))
        scene.sphere(W(hand + [0.095, 0, 0.012]), 0.01, np.array([235, 120, 45.0]))
    elif gid == "can":
        hand = j["handR"]
        scene.capsule(W(hand + [0, 0, -0.03]), W(hand + [0, 0, 0.07]), 0.035, 0.035, np.array([180, 42, 40.0]))
        scene.sphere(W(hand + [0, 0, 0.075]), 0.03, np.array([200, 200, 200.0]))


def world_fn(j, facing):
    p = j["p"]

    def W(pt):
        pt = np.asarray(pt, dtype=float)
        if p["lie"]:
            pt = np.array([-(pt[2]) + 0.9, pt[1], 0.12 + pt[0] * 0.25])
        return rot_z(pt, facing)
    return W


def sheet(layer, anim, frames):
    img = Image.new("RGBA", (FW * frames, FH * 8), (0, 0, 0, 0))
    for row in range(8):
        facing = row * math.pi / 4
        for f in range(frames):
            j = skeleton(anim, f, frames)
            W = world_fn(j, facing)
            sc = Scene(FW, FH, FEET)
            if layer == "body":
                body(sc, W, j)
            else:
                body(sc, W, j, occ=True)   # the body hides what's behind it
                garment(sc, W, j, layer)
            img.alpha_composite(sc.render(seed=row * 31 + f, outline_alpha=130), (f * FW, row * FH))
    return img


def layers():
    """(layer id, slot) for every sheet to generate."""
    out = [("body", "body")]
    for slot, ids in GARMENTS.items():
        out += [(g, slot) for g in ids]
    return out
