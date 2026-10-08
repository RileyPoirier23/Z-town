"""Procedural stand-in audio (sound effects, ambience loops, music cues) as WAV files in
game/assets/audio/, plus manifest.json. Replace any file with a real recording/composition of
the same name and the game picks it up. Everything here is placeholder (ART_SPEC.md §8)."""
import json
import math
import os
import wave

import numpy as np

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "game", "assets", "audio")
SR = 22050
rng = np.random.default_rng(7)


def t(sec):
    return np.arange(int(sec * SR)) / SR


def env(n, a=0.005, d=0.2, shape=4.0):
    x = np.arange(n) / SR
    att = np.clip(x / max(a, 1e-4), 0, 1)
    dec = np.exp(-np.maximum(0, x - a) * shape / max(d, 1e-4))
    return att * dec


def lowpass(x, cutoff):
    # one-pole low-pass, run twice for a softer slope
    a = math.exp(-2 * math.pi * cutoff / SR)
    for _ in range(2):
        y = np.empty_like(x)
        acc = 0.0
        for i, v in enumerate(x):
            acc = (1 - a) * v + a * acc
            y[i] = acc
        x = y
    return x


def lp(x, cutoff):
    """Fast FFT low-pass (for long sounds)."""
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    X *= 1 / (1 + (f / cutoff) ** 4)
    return np.fft.irfft(X, len(x))


def hp(x, cutoff):
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    X *= 1 / (1 + (cutoff / np.maximum(f, 1)) ** 4)
    return np.fft.irfft(X, len(x))


def bp(x, lo, hi):
    return hp(lp(x, hi), lo)


def noise(sec):
    return rng.standard_normal(int(sec * SR))


def norm(x, peak=0.8):
    m = np.max(np.abs(x)) or 1
    return x / m * peak


def reverb(x, wet=0.25, room=0.35):
    out = np.copy(x)
    for delay, g in ((0.029, 0.6), (0.037, 0.55), (0.041, 0.5), (0.053, 0.45), (0.067, 0.4), (0.089, 0.35)):
        d = int(delay * SR / room * 0.35)
        buf = np.zeros(len(x) + d * 8)
        buf[: len(x)] = x
        for k in range(1, 8):
            buf[d * k: d * k + len(x)] += x * (g ** k)
        out = out + buf[: len(x)] * wet / 6
    return out


def loopify(x, fade=0.5):
    """Crossfades the end into the start so the sound loops cleanly."""
    n = int(fade * SR)
    a = x[:n].copy()
    b = x[-n:]
    w = np.linspace(0, 1, n)
    x = x[:-n].copy()
    x[:n] = a * w + b * (1 - w)
    return x


def save(name, x, peak=0.8):
    os.makedirs(OUT, exist_ok=True)
    x = norm(np.asarray(x, dtype=float), peak)
    data = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    return "res://assets/audio/" + name + ".wav"


# ------------------------------------------------------------------ effects

def footstep(kind, i):
    n = noise(0.12)
    if kind == "grass":
        x = bp(n, 300, 2500) * env(len(n), 0.004, 0.06, 5)
        x += bp(noise(0.12), 2000, 6000) * env(len(n), 0.01, 0.05, 6) * 0.3
    else:
        x = bp(n, 150, 1800) * env(len(n), 0.002, 0.035, 7)
        tt = t(0.12)
        x += np.sin(2 * np.pi * (90 + 20 * i) * tt) * env(len(tt), 0.001, 0.04, 8) * 0.8
    return x


def groan(i):
    dur = 1.1 + 0.3 * i
    tt = t(dur)
    f0 = 85 + 12 * i + 10 * np.sin(2 * np.pi * (2.2 + i) * tt) + 15 * np.sin(2 * np.pi * 0.7 * tt)
    ph = 2 * np.pi * np.cumsum(f0) / SR
    src = sum(np.sin(k * ph) / k for k in range(1, 14)) + 0.5 * noise(dur)
    # throat formants
    x = bp(src, 250, 600) * 1.0 + bp(src, 700, 1200) * 0.6 + bp(src, 2200, 3000) * 0.25
    e = np.sin(np.linspace(0, np.pi, len(tt))) ** 0.7
    return reverb(x * e, 0.2)


def thump(i):
    tt = t(0.5)
    x = np.sin(2 * np.pi * (60 + 8 * i) * tt) * env(len(tt), 0.002, 0.18, 5)
    x += bp(noise(0.5), 80, 900) * env(len(tt), 0.001, 0.08, 6) * 0.8
    x += bp(noise(0.5), 1500, 5000) * env(len(tt), 0.02, 0.15, 5) * 0.15  # rattle
    return reverb(x, 0.3)


def glass():
    x = bp(noise(1.2), 2500, 9000) * env(int(1.2 * SR), 0.001, 0.2, 5)
    tt = t(1.2)
    for _ in range(14):  # tinkles
        start = rng.uniform(0.05, 0.9)
        f = rng.uniform(2500, 6500)
        s = int(start * SR)
        seg = tt[: len(tt) - s]
        x[s:] += np.sin(2 * np.pi * f * seg) * np.exp(-seg * 30) * 0.25
    return reverb(x, 0.2)


def door(open_):
    tt = t(0.6)
    creak_f = 400 + 300 * np.sin(np.linspace(0, 2.5, len(tt))) if open_ else 300 + 100 * np.linspace(0, 1, len(tt))
    ph = 2 * np.pi * np.cumsum(creak_f) / SR
    creak = np.sign(np.sin(ph)) * 0.2 * np.sin(np.linspace(0, np.pi, len(tt)))
    x = bp(creak + 0.1 * noise(0.6), 300, 2500) * (0.5 if open_ else 0.3)
    if not open_:
        k = int(0.45 * SR)
        x[k:] += np.sin(2 * np.pi * 80 * tt[: len(tt) - k]) * env(len(tt) - k, 0.001, 0.1, 6) * 1.2
        x[k:] += bp(noise(0.6)[: len(tt) - k], 100, 1500) * env(len(tt) - k, 0.001, 0.05, 6) * 0.6
    return reverb(x, 0.25)


def swing():
    n = noise(0.3)
    x = n * np.sin(np.linspace(0, np.pi, len(n))) ** 2
    return bp(x, 400, 1800)


def hit(kind):
    tt = t(0.3)
    x = np.sin(2 * np.pi * 110 * tt) * env(len(tt), 0.001, 0.07, 6)
    x += bp(noise(0.3), 100, 1200) * env(len(tt), 0.001, 0.05, 6) * (1.2 if kind == "flesh" else 0.6)
    if kind == "wood":
        x += np.sin(2 * np.pi * 420 * tt) * env(len(tt), 0.001, 0.05, 6) * 0.5
    return x


def hammer():
    out = np.zeros(int(1.4 * SR))
    for k in range(4):
        tt = t(0.25)
        x = np.sin(2 * np.pi * 900 * tt) * env(len(tt), 0.001, 0.04, 8) + bp(noise(0.25), 1000, 5000) * env(len(tt), 0.001, 0.02, 8)
        s = int(k * 0.33 * SR)
        out[s: s + len(x)] += x
    return reverb(out, 0.2)


def generator_loop():
    dur = 4.0
    tt = t(dur)
    base = 30
    x = sum(np.sin(2 * np.pi * base * h * tt + h) / h for h in range(1, 18))
    x *= 1 + 0.15 * np.sin(2 * np.pi * 7.5 * tt)
    x += 0.25 * bp(noise(dur), 200, 2000)
    return loopify(lp(x, 1800), 0.3)


def tv_loop():
    """Muffled voices through a wall: syllable-shaped noise and a studio laugh now and then."""
    dur = 8.0
    n = int(dur * SR)
    tt = t(dur)
    syl = np.zeros(n)
    pos = 0
    while pos < n:
        L = int(rng.uniform(0.08, 0.22) * SR)
        if rng.random() < 0.82:
            syl[pos: pos + L] = np.sin(np.linspace(0, np.pi, min(L, n - pos)))
        pos += L + int(rng.uniform(0.0, 0.15) * SR)
    f0 = 140 + 40 * np.sin(2 * np.pi * 0.6 * tt)
    ph = 2 * np.pi * np.cumsum(f0) / SR
    voice = sum(np.sin(k * ph) / k for k in range(1, 10)) * 0.6 + noise(dur) * 0.4
    x = bp(voice, 200, 1400) * syl
    # applause/laughter swell
    s = int(5.2 * SR)
    x[s: s + int(1.5 * SR)] += bp(noise(1.5), 600, 3000) * np.sin(np.linspace(0, np.pi, int(1.5 * SR))) * 0.7
    return loopify(x, 0.4)


def phone_buzz():
    tt = t(0.9)
    x = np.sign(np.sin(2 * np.pi * 180 * tt)) * 0.3 + np.sin(2 * np.pi * 360 * tt) * 0.2
    gate = ((tt % 0.45) < 0.3).astype(float)
    return lp(x * gate, 900)


def pickup():
    tt = t(0.12)
    return bp(noise(0.12), 800, 4000) * env(len(tt), 0.001, 0.03, 6) + np.sin(2 * np.pi * 1200 * tt) * env(len(tt), 0.001, 0.02, 8) * 0.3


def gulp():
    tt = t(0.35)
    f = 300 - 150 * np.linspace(0, 1, len(tt))
    ph = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(ph) * env(len(tt), 0.01, 0.15, 4) * 0.8 + bp(noise(0.35), 200, 900) * env(len(tt), 0.01, 0.1, 5) * 0.3


def munch():
    out = np.zeros(int(0.6 * SR))
    for k in range(3):
        x = bp(noise(0.08), 800, 4000) * env(int(0.08 * SR), 0.002, 0.03, 6)
        s = int(k * 0.18 * SR)
        out[s: s + len(x)] += x
    return out


# ------------------------------------------------------------------ ambience

def amb_day():
    dur = 20.0
    x = lp(noise(dur), 400) * 0.6  # wind bed
    x *= 0.7 + 0.3 * np.sin(2 * np.pi * 0.07 * t(dur))
    for _ in range(26):  # birds
        s = rng.uniform(0, dur - 1)
        chirps = rng.integers(2, 6)
        f = rng.uniform(2500, 4500)
        for c in range(chirps):
            st = int((s + c * 0.12) * SR)
            tt = t(0.08)
            sweep = f + 800 * np.sin(np.linspace(0, np.pi, len(tt)))
            ph = 2 * np.pi * np.cumsum(sweep) / SR
            seg = np.sin(ph) * np.sin(np.linspace(0, np.pi, len(tt))) * 0.15
            x[st: st + len(seg)] += seg[: max(0, len(x) - st)]
    return loopify(x, 1.0)


def amb_night():
    dur = 16.0
    x = lp(noise(dur), 250) * 0.4
    tt = t(dur)
    chirp = np.sin(2 * np.pi * 4300 * tt) * ((np.sin(2 * np.pi * 28 * tt) > 0.6).astype(float)) * ((tt % 1.1) < 0.45) * 0.08
    chirp2 = np.sin(2 * np.pi * 3900 * tt) * ((np.sin(2 * np.pi * 31 * tt) > 0.6).astype(float)) * (((tt + 0.5) % 1.3) < 0.4) * 0.06
    return loopify(x + chirp + chirp2, 1.0)


def amb_indoor():
    dur = 10.0
    tt = t(dur)
    hum = np.sin(2 * np.pi * 60 * tt) * 0.25 + np.sin(2 * np.pi * 120 * tt) * 0.12 + lp(noise(dur), 300) * 0.15
    return loopify(hum, 0.5)


def amb_rain():
    dur = 12.0
    x = bp(noise(dur), 400, 7000)
    drops = np.zeros(len(x))
    for _ in range(900):
        s = rng.integers(0, len(x) - 200)
        drops[s: s + 200] += bp(noise(200 / SR), 1500, 6000) * env(200, 0.0005, 0.004, 6) * rng.uniform(0.3, 1)
    return loopify(x * 0.6 + drops, 1.0)


# ------------------------------------------------------------------ music

def tone(freq, dur, amp=1.0, bright=1.0):
    """Soft piano-ish note: harmonics with faster decay up high."""
    tt = t(dur)
    x = np.zeros(len(tt))
    for h in range(1, 7):
        x += np.sin(2 * np.pi * freq * h * tt + h) * (0.6 ** (h - 1)) * bright ** (h - 1) * np.exp(-tt * (1.2 + h * 0.9))
    x *= np.clip(tt / 0.008, 0, 1)
    return x * amp


def note(n):
    return 440 * 2 ** ((n - 69) / 12)


def music_home():
    """Slow, warm: Am - F - C - G, broken chords, a little melody. ~64 s loop."""
    bpm = 66
    beat = 60 / bpm
    prog = [(57, [57, 60, 64]), (53, [53, 57, 60]), (48, [55, 60, 64]), (55, [55, 59, 62])]
    bars = 16
    dur = bars * 4 * beat + 2
    out = np.zeros(int(dur * SR))
    mel = [76, 74, 72, 74, 72, 69, 67, 69, 72, 71, 67, 69, 64, 67, 69, 72]
    for b in range(bars):
        root, chord = prog[b % 4]
        start = b * 4 * beat
        for k, n in enumerate([root - 12] + chord + chord[1:]):
            st = int((start + k * beat * 0.66) * SR)
            x = tone(note(n), 3.5, 0.5 if k else 0.7, 0.8)
            out[st: st + len(x)] += x[: max(0, len(out) - st)]
        if b % 2 == 1 or b >= 8:
            m = mel[b % len(mel)]
            st = int((start + 2 * beat) * SR)
            x = tone(note(m), 2.5, 0.35, 1.0)
            out[st: st + len(x)] += x[: max(0, len(out) - st)]
    return loopify(reverb(out, 0.5, 0.6), 2.0)


def music_out():
    """Outside: an uneasy drone and far-off notes. ~48 s loop."""
    dur = 48.0
    tt = t(dur)
    drone = sum(np.sin(2 * np.pi * note(n) * tt + i) * a for i, (n, a) in enumerate([(38, 0.5), (45, 0.3), (50, 0.15)]))
    drone *= 0.6 + 0.4 * np.sin(2 * np.pi * 0.03 * tt) ** 2
    out = lp(drone, 700)
    for k in range(10):
        n = rng.choice([62, 65, 67, 69, 70, 74])
        st = int(rng.uniform(0, dur - 4) * SR)
        x = tone(note(n), 4, 0.25, 0.7)
        out[st: st + len(x)] += x
    return loopify(reverb(out, 0.6, 0.7), 2.0)


def music_danger():
    """Zombies close: low pulse and a cluster that won't resolve. ~16 s loop."""
    dur = 16.0
    tt = t(dur)
    pulse = np.sin(2 * np.pi * 45 * tt) * (np.exp(-((tt % 0.75) * 10)))
    cluster = sum(np.sin(2 * np.pi * note(n) * tt) for n in (50, 51, 57)) * 0.12
    cluster *= 0.5 + 0.5 * np.sin(2 * np.pi * 0.25 * tt) ** 2
    hiss = bp(noise(dur), 2000, 6000) * 0.05
    return loopify(pulse * 0.9 + lp(cluster, 1200) + hiss, 1.0)


def main():
    m = {"sfx": {}, "loops": {}, "music": {}}
    sfx = m["sfx"]
    sfx["step_grass"] = [save(f"step_grass_{i}", footstep("grass", i), 0.5) for i in range(3)]
    sfx["step_hard"] = [save(f"step_hard_{i}", footstep("hard", i), 0.5) for i in range(3)]
    sfx["groan"] = [save(f"zombie_groan_{i}", groan(i), 0.7) for i in range(4)]
    sfx["thump"] = [save(f"thump_{i}", thump(i), 0.9) for i in range(3)]
    sfx["glass"] = [save("glass_break", glass(), 0.8)]
    sfx["door_open"] = [save("door_open", door(True), 0.6)]
    sfx["door_close"] = [save("door_close", door(False), 0.7)]
    sfx["swing"] = [save("swing", swing(), 0.5)]
    sfx["hit"] = [save("hit_flesh", hit("flesh"), 0.9), save("hit_wood", hit("wood"), 0.9)]
    sfx["hammer"] = [save("hammer", hammer(), 0.8)]
    sfx["phone"] = [save("phone_buzz", phone_buzz(), 0.5)]
    sfx["pickup"] = [save("pickup", pickup(), 0.5)]
    sfx["drink"] = [save("drink", gulp(), 0.6)]
    sfx["eat"] = [save("eat", munch(), 0.6)]
    m["loops"]["generator"] = save("loop_generator", generator_loop(), 0.6)
    m["loops"]["tv"] = save("loop_tv", tv_loop(), 0.6)
    m["loops"]["amb_day"] = save("amb_day", amb_day(), 0.5)
    m["loops"]["amb_night"] = save("amb_night", amb_night(), 0.5)
    m["loops"]["amb_indoor"] = save("amb_indoor", amb_indoor(), 0.4)
    m["loops"]["rain"] = save("amb_rain", amb_rain(), 0.5)
    m["music"]["home"] = save("mus_home", music_home(), 0.6)
    m["music"]["out"] = save("mus_out", music_out(), 0.55)
    m["music"]["danger"] = save("mus_danger", music_danger(), 0.6)
    with open(os.path.join(OUT, "manifest.json"), "w") as f:
        json.dump(m, f, indent=1)
    print("audio written")


if __name__ == "__main__":
    main()
