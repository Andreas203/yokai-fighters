"""Side-by-side contact sheet of PNGs (YOK-146): each --row is one line of images, scaled to --height px tall.

  blender -b --python tools/blender/texture_compare.py -- --out sheet.png [--height 700] \
      --row a1.png a2.png a3.png --row b1.png b2.png b3.png

Images are read raw (no colour management) and written as 8-bit PNG.
"""
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import clip_common as cc  # noqa: E402


def load(path, h):
    im = bpy.data.images.load(os.path.abspath(path))
    im.colorspace_settings.name = "Non-Color"
    w0, h0 = im.size
    px = np.empty(w0 * h0 * 4, np.float32)
    im.pixels.foreach_get(px)
    bpy.data.images.remove(im)
    a = px.reshape(h0, w0, 4)[::-1, :, :3]  # top row first
    w = max(1, int(round(w0 * h / h0)))
    yi = (np.arange(h) * h0 / h).astype(int)
    xi = (np.arange(w) * w0 / w).astype(int)
    return a[yi][:, xi]


def main():
    a = cc.script_args("texture_compare")
    out = a["--out"][0]
    h = int(a.get("--height", ["700"])[0])
    argv = sys.argv[sys.argv.index("--") + 1:]
    rows, cur = [], None
    i = 0
    while i < len(argv):
        if argv[i] == "--row":
            cur = []
            rows.append(cur)
        elif argv[i] in ("--out", "--height"):
            i += 1
        elif cur is not None:
            cur.append(argv[i])
        i += 1
    lines = [np.concatenate([load(p, h) for p in r], axis=1) for r in rows]
    W = max(l.shape[1] for l in lines)
    lines = [np.pad(l, ((0, 0), (0, W - l.shape[1]), (0, 0)), constant_values=1.0) for l in lines]
    sheet = np.concatenate(lines, axis=0)
    rgba = np.concatenate([sheet, np.ones(sheet.shape[:2] + (1,), np.float32)], axis=2)[::-1]
    im = bpy.data.images.new("sheet", sheet.shape[1], sheet.shape[0])
    im.colorspace_settings.name = "Non-Color"
    im.pixels.foreach_set(rgba.reshape(-1))
    im.filepath_raw = os.path.abspath(out)
    im.file_format = "PNG"
    im.save()
    print("texture_compare: wrote", out, sheet.shape[1], "x", sheet.shape[0])
    cc.finish(0)


main()
