"""Import the Astra shop plates (YOK-159) into game/assets/generated/ui/ with Pillow Lanczos.

Usage: python import_shop_plates.py <repo_root>
Steps per plate: snap near-opaque alpha (>= 246) to 255 (Astra leaves paper at 253-254), crop to the
stated box, resize with Lanczos (Pillow premultiplies RGBA, so no matte bleeds in), refuse to overwrite.
"""
import os
import sys
from PIL import Image
import numpy as np

root = sys.argv[1]
src = os.path.join(root, "docs/design/screens/astra")
dst = os.path.join(root, "game/assets/generated/ui")


def load(name, snap=True):
    im = Image.open(os.path.join(src, name)).convert("RGBA")
    if snap:
        a = np.array(im)
        a[..., 3] = np.where(a[..., 3] >= 246, 255, a[..., 3])
        im = Image.fromarray(a, "RGBA")
    return im


def save(im, name, size):
    out = os.path.join(dst, name)
    if os.path.exists(out):
        sys.exit("refusing to overwrite " + out)
    im = im.resize(size, Image.LANCZOS)
    im.save(out)
    print(name, im.size)


# S1: crop to 1333x1173 (exact 800:704 ratio, keeps alpha bbox 13..1327), then scale 0.6002
save(load("shop-cabinet.png").crop((4, 0, 1337, 1173)), "shop-cabinet.png", (800, 704))
# S2: crop full width x 740 rows (ratio 2.8, keeps alpha bbox y 88..668)
save(load("price-tag.png").crop((0, 9, 2073, 749)), "price-tag.png", (392, 140))
# S3: split the sheet at the empty column run 1771..1848; crop each piece to its bbox + padding
sheet = load("speech-bubble-sheet.png")
body = sheet.crop((0, 60, 1810, 640))
body_bb = body.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
bx0, by0, bx1, by1 = body_bb
body = body.crop((max(bx0 - 12, 0), max(by0 - 12, 0), bx1 + 12, by1 + 12))
bw = 640
bh = round(body.size[1] * bw / body.size[0])
save(body, "speech-bubble.png", (bw, bh))
tail = sheet.crop((1810, 0, 2161, 728))
tx0, ty0, tx1, ty1 = tail.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
cx = (tx0 + tx1) // 2
h = (ty1 - ty0) + 24
w = round(h * 0.8)
tail = tail.crop((cx - w // 2, ty0 - 12, cx - w // 2 + w, ty0 - 12 + h))
save(tail, "speech-bubble-tail.png", (128, 160))
# S4: whole repaired canvas (1060x1484 is exactly 400:560)
save(load("ledger-slip.png", snap=False), "ledger-slip.png", (400, 560))
# S5: whole canvas (941x1672 is exactly 432:768)
save(load("merchant-portrait.png"), "merchant-portrait.png", (432, 768))
# B1: opaque, already 1672x941; copied as is
out = os.path.join(dst, "shop-backdrop.png")
if os.path.exists(out):
    sys.exit("refusing to overwrite " + out)
Image.open(os.path.join(src, "shop-backdrop.png")).convert("RGB").save(out)
print("shop-backdrop.png (1672, 941)")
