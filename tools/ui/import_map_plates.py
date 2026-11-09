"""Cut the map v2 Astra plates (YOK-238) into single sprites for the game.

Reads the raw takes in docs/design/screens/astra/ and writes trimmed RGBA sprites to
game/assets/generated/ui/map/. Sheets are split on fully transparent bands: first into rows,
then each row into items, read top-to-bottom and left-to-right, and named from the
table below. Brief: docs/design/screens/astra-brief-map-plates.md.

    python tools/ui/import_map_plates.py
"""
from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / "docs/design/screens/astra"
OUT = ROOT / "game/assets/generated/ui/map"
PAD = 6           # transparent margin kept around each sprite, in output pixels
ALPHA_CUT = 8     # alpha at or below this counts as background when finding regions

# raw take -> (sprite names in reading order, output height in pixels)
PLATES = {
    "map-masks.png": (["mask-apprentice", "mask-oni", "mask-kitsune", "mask-kappa"], 320),
    "map-elder-masks.png": (["mask-elder-oni", "mask-nine-tails", "mask-elder-kappa"], 320),
    "map-merchant-stand.png": (["node-merchant"], 360),
    "map-dojo-gate.png": (["node-dojo"], 360),
    "map-tea-house.png": (["node-tanuki"], 360),
    "map-landmarks.png": (["landmark-pines-wide", "landmark-pine-tall",
                           "landmark-bridge", "landmark-torii", "landmark-rocks"], None),
}
LANDMARK_SCALE = 0.5  # landmarks keep their relative sizes from the sheet


def runs(filled: np.ndarray, min_gap: int = 12) -> list[tuple[int, int]]:
    """Index ranges where `filled` is true, bridging gaps narrower than min_gap."""
    out: list[list[int]] = []
    for i in np.flatnonzero(filled):
        if out and i - out[-1][1] <= min_gap:
            out[-1][1] = i + 1
        else:
            out.append([i, i + 1])
    return [(a, b) for a, b in out if b - a > 16]


def regions(alpha: np.ndarray) -> list[tuple[slice, slice]]:
    """Bounding boxes of the separate items on a sheet, in reading order."""
    solid = alpha > ALPHA_CUT
    boxes = []
    for y0, y1 in runs(solid.any(axis=1)):
        for x0, x1 in runs(solid[y0:y1].any(axis=0)):
            boxes.append((slice(y0, y1), slice(x0, x1)))
    return boxes


def trim(img: Image.Image, box: tuple[slice, slice]) -> Image.Image:
    """Crop to the item's own alpha bounds (undilated) inside its region."""
    crop = img.crop((box[1].start, box[0].start, box[1].stop, box[0].stop))
    bbox = Image.fromarray((np.asarray(crop.getchannel("A")) > ALPHA_CUT).astype(np.uint8) * 255).getbbox()
    return crop.crop(bbox)


def save(sprite: Image.Image, name: str, height: int | None) -> Path:
    if height is None:
        size = (round(sprite.width * LANDMARK_SCALE), round(sprite.height * LANDMARK_SCALE))
    else:
        size = (round(sprite.width * height / sprite.height), height)
    sprite = sprite.resize(size, Image.LANCZOS)
    canvas = Image.new("RGBA", (size[0] + 2 * PAD, size[1] + 2 * PAD), (0, 0, 0, 0))
    canvas.alpha_composite(sprite, (PAD, PAD))
    path = OUT / f"{name}.png"
    canvas.save(path, optimize=True)
    return path


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    for raw, (names, height) in PLATES.items():
        img = Image.open(RAW / raw).convert("RGBA")
        boxes = regions(np.asarray(img.getchannel("A")))
        if len(boxes) != len(names):
            raise SystemExit(f"{raw}: found {len(boxes)} items, expected {len(names)} ({', '.join(names)})")
        for box, name in zip(boxes, names):
            path = save(trim(img, box), name, height)
            w, h = Image.open(path).size
            print(f"{path.relative_to(ROOT)}  {w}x{h}")


if __name__ == "__main__":
    main()
