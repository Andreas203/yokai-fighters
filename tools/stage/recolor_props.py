"""YOK-150/151: recolour two Tripo stage props' textures toward the stage palette (assets/specs/style.md).

Godot extracts each glb's embedded texture next to it (`<prop>_Color_<id>.jpg`); this writes a recoloured PNG beside it,
which the prop's material in game/materials/stage/ uses (the glb's import settings swap that material in):
  - pine-cluster: teal needles toward pine #34483B (hue only; low-saturation snow is left alone).
  - reeds: the bright green leaves toward the dry olive-tan of the riverbank backdrop's painted reeds.
Deterministic (pure per-pixel HSV maps). Run: python tools/stage/recolor_props.py
"""
import glob
import os

import numpy as np
from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "game", "assets", "generated", "stage")


def hsv(img):
    a = np.asarray(img.convert("RGB"), dtype=np.float32) / 255.0
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx, mn = a.max(-1), a.min(-1)
    d = mx - mn
    h = np.zeros_like(mx)
    nz = d > 1e-6
    rm = nz & (mx == r)
    gm = nz & (mx == g) & ~rm
    bm = nz & ~rm & ~gm
    h[rm] = ((g - b)[rm] / d[rm]) % 6
    h[gm] = (b - r)[gm] / d[gm] + 2
    h[bm] = (r - g)[bm] / d[bm] + 4
    h *= 60.0
    s = np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0)
    return h, s, mx


def rgb(h, s, v):
    c = v * s
    x = c * (1 - np.abs((h / 60.0) % 2 - 1))
    m = v - c
    z = np.zeros_like(h)
    k = (h // 60).astype(int) % 6
    r = np.choose(k, [c, x, z, z, x, c])
    g = np.choose(k, [x, c, c, x, z, z])
    b = np.choose(k, [z, z, x, c, c, x])
    out = np.stack([r + m, g + m, b + m], -1)
    return Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB")


def source(folder, prop):
    hits = sorted(glob.glob(os.path.join(ROOT, folder, f"{prop}_Color_*.jpg")))
    if not hits:
        raise SystemExit(f"{folder}/{prop}: no extracted texture; run Godot's import first")
    return Image.open(hits[0])


def pine():
    h, s, v = hsv(source("shrine-gate", "pine-cluster"))
    m = (s > 0.18) & (h > 100) & (h < 220)
    h = np.where(m, 141.0, h)
    s = np.where(m, s * 0.65, s)
    rgb(h, s, v).save(os.path.join(ROOT, "shrine-gate", "pine-cluster_pine.png"))


def reeds():
    h, s, v = hsv(source("riverbank", "reeds"))
    m = (s > 0.25) & (h > 60) & (h < 140)
    h = np.where(m, 45.0, h)
    s = np.where(m, s * 0.6, s)
    v = np.where(m, v * 0.85, v)
    rgb(h, s, v).save(os.path.join(ROOT, "riverbank", "reeds_dry.png"))


if __name__ == "__main__":
    pine()
    reeds()
    print("recoloured: pine-cluster_pine.png, reeds_dry.png")
