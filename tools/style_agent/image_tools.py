"""What code can measure on a generated image, for the visual Evaluator (Pillow only).

Facts, not verdicts: the size and aspect ratio, a border or letterbox at the edges, the
dominant colours and how far each is from the style.md palette, how flat the colour is, and
how busy the lower third (the fighter band) is against the rest. Only the clear-cut ones
(aspect ratio, size, a border) are flags that block acceptance; the Evaluator judges the rest.
"""
from __future__ import annotations

import base64
import io
import math
from pathlib import Path

from PIL import Image, ImageFilter, ImageStat

from visual_guide import PALETTE

ASPECT = 16 / 9
ASPECT_TOLERANCE = 0.03   # 3%: 1672x941 and 1376x768 (the accepted grove) both pass
MIN_WIDTH = 1280
STORE_WIDTH = 1280        # takes are kept in the repo at this width; every measurement reads the kept file
NEAR = 20.0               # CIE76 distance under which a colour counts as on the palette (the grove's colours sit at 9-16)


def _lab(rgb) -> tuple[float, float, float]:
    def lin(c):
        c /= 255
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = (lin(float(c)) for c in rgb)
    x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047
    y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883

    def f(t):
        return t ** (1 / 3) if t > 0.008856 else 7.787 * t + 16 / 116
    fx, fy, fz = f(x), f(y), f(z)
    return 116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz)


def _hex(rgb) -> str:
    return "#{:02X}{:02X}{:02X}".format(*rgb)


def _rgb(hexv: str) -> tuple[int, int, int]:
    return tuple(int(hexv[i:i + 2], 16) for i in (1, 3, 5))


PALETTE_LAB = {name: _lab(_rgb(h)) for name, h in PALETTE.items()}


def nearest_palette(rgb) -> tuple[str, float]:
    lab = _lab(rgb)
    name, ref = min(PALETTE_LAB.items(), key=lambda kv: math.dist(kv[1], lab))
    return name, math.dist(ref, lab)


def store_take(src: Path, dest: Path) -> Path:
    """Keep a take in the repo at STORE_WIDTH, as RGB PNG. Measurements always read the kept file."""
    im = Image.open(src).convert("RGB")
    if im.width > STORE_WIDTH:
        im = im.resize((STORE_WIDTH, round(im.height * STORE_WIDTH / im.width)), Image.LANCZOS)
    dest.parent.mkdir(parents=True, exist_ok=True)
    im.save(dest, "PNG", optimize=True)
    return dest


def _edge_band(im: Image.Image) -> dict:
    """Uniform rows or columns at the edges (letterbox, frame) that differ from the picture inside."""
    g = im.convert("L")
    w, h = g.size
    px = g.load()

    def flat_run(lines):
        n = 0
        for line in lines:
            vals = [px[x, y] for x, y in line]
            mean = sum(vals) / len(vals)
            if math.sqrt(sum((v - mean) ** 2 for v in vals) / len(vals)) > 6:
                break
            n += 1
        return n
    step = max(1, w // 200)
    rows = lambda ys: ([(x, y) for x in range(0, w, step)] for y in ys)        # noqa: E731
    cols = lambda xs: ([(x, y) for y in range(0, h, step)] for x in xs)        # noqa: E731
    return {"top": flat_run(rows(range(h))), "bottom": flat_run(rows(range(h - 1, -1, -1))),
            "left": flat_run(cols(range(w))), "right": flat_run(cols(range(w - 1, -1, -1)))}


def measure_image(path: Path) -> dict:
    im = Image.open(path).convert("RGB")
    w, h = im.size
    small = im.copy()
    small.thumbnail((320, 320))

    # Dominant colours: 12-colour median cut, each with its share and nearest palette entry.
    q = small.quantize(colors=12, method=Image.Quantize.MEDIANCUT)
    pal = q.getpalette()
    counts = sorted(q.getcolors(), reverse=True)
    total = sum(c for c, _ in counts)
    dominant, on_palette = [], 0.0
    for count, idx in counts:
        rgb = tuple(pal[idx * 3: idx * 3 + 3])
        share = count / total
        name, dist = nearest_palette(rgb)
        if dist <= NEAR:
            on_palette += share
        if share >= 0.03:
            dominant.append({"hex": _hex(rgb), "share": round(share, 3), "nearest": name, "distance": round(dist, 1)})

    # Flatness: share of pixels held by the 16 commonest colours of a 64-colour cut. Cel art scores high, a render low.
    q64 = small.quantize(colors=64, method=Image.Quantize.MEDIANCUT)
    c64 = sorted((c for c, _ in q64.getcolors()), reverse=True)
    flatness = sum(c64[:16]) / sum(c64)

    # Busyness: mean edge strength, lower third against the upper two thirds.
    edges = small.convert("L").filter(ImageFilter.FIND_EDGES)
    sw, sh = edges.size
    lower = ImageStat.Stat(edges.crop((0, sh * 2 // 3, sw, sh))).mean[0]
    upper = ImageStat.Stat(edges.crop((0, 0, sw, sh * 2 // 3))).mean[0]

    hsv = small.convert("HSV")
    s_vals, v_vals = hsv.getchannel("S").getdata(), hsv.getchannel("V").getdata()
    neon = sum(1 for s, v in zip(s_vals, v_vals) if s > 200 and v > 200) / len(s_vals)
    white = sum(1 for p in small.getdata() if min(p) > 245) / len(s_vals)

    band = _edge_band(im)
    m = {
        "width": w, "height": h, "aspect": round(w / h, 3),
        "border_px": band,
        "dominant_colours": dominant,
        "on_palette_share": round(on_palette, 3),
        "flatness": round(flatness, 3),
        "lower_third_edge": round(lower, 1), "upper_edge": round(upper, 1),
        "neon_share": round(neon, 3), "pure_white_share": round(white, 3),
    }
    flags = []
    if abs(m["aspect"] - ASPECT) / ASPECT > ASPECT_TOLERANCE:
        flags.append(f"S1: aspect ratio {m['aspect']} ({w}x{h}), not 16:9 ({ASPECT:.3f})")
    if w < MIN_WIDTH:
        flags.append(f"S1: {w} px wide, under the {MIN_WIDTH} px minimum")
    # A letterbox or frame is the same flat colour on opposite edges. One flat edge, or a flat sky over flat ground in two
    # different colours, is ordinary cel art (S4 wants a calm ground band), so only a matching pair counts.
    hit = {side for side, n in band.items() if n >= max(4, round((h if side in ('top', 'bottom') else w) * 0.01))}
    boxes = {"top": lambda n: (0, 0, w, n), "bottom": lambda n: (0, h - n, w, h),
             "left": lambda n: (0, 0, n, h), "right": lambda n: (w - n, 0, w, h)}
    colour = {side: tuple(ImageStat.Stat(im.crop(boxes[side](band[side]))).mean) for side in hit}
    edges_hit = {side: band[side] for pair in (("top", "bottom"), ("left", "right"))
                 if set(pair) <= hit and math.dist(_lab(colour[pair[0]]), _lab(colour[pair[1]])) < 10 for side in pair}
    if edges_hit:
        flags.append("S1: flat band at the edges (border, frame or letterbox): "
                     + ", ".join(f"{side} {n} px" for side, n in edges_hit.items()))
    m["flags"] = flags
    return m


def format_image_measurements(m: dict) -> str:
    rows = [
        f"- size: {m['width']}x{m['height']}, aspect {m['aspect']} (16:9 is 1.778)",
        f"- flat band at each edge, in px (a border or letterbox shows here): " + ", ".join(f"{k} {v}" for k, v in m["border_px"].items()),
        f"- share of pixels within distance {NEAR:.0f} of a palette colour: {m['on_palette_share']:.0%}",
        "- dominant colours: " + "; ".join(f"{d['hex']} {d['share']:.0%} (nearest {d['nearest']}, distance {d['distance']})"
                                         for d in m["dominant_colours"]),
        f"- flatness (share held by the 16 commonest of 64 colours; flat cel art is high, a photo or painterly render low): {m['flatness']:.0%}",
        f"- edge strength, lower third {m['lower_third_edge']} against upper two thirds {m['upper_edge']} (the lower third should be calmer)",
        f"- neon share (very saturated and bright pixels): {m['neon_share']:.1%}; pure white share: {m['pure_white_share']:.1%}",
    ]
    return "\n".join(rows)


def image_block(path: Path, max_side: int = 1024) -> dict:
    """An image content block for the model, downscaled so the call stays small."""
    im = Image.open(path).convert("RGB")
    im.thumbnail((max_side, max_side))
    buf = io.BytesIO()
    im.save(buf, "PNG")
    return {"type": "image", "source": {"type": "base64", "media_type": "image/png",
                                        "data": base64.b64encode(buf.getvalue()).decode()}}
