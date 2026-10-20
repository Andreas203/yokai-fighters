"""Repair an Astra paper plate whose flat centre was eaten by background removal (YOK-159 ledger slip).

Fills everything inside the inner ink border with the flat paper colour, makes the paper opaque, and clears
coloured matte fringe outside the pin. Usage: python repair_slip.py <raw.png> <out.png>  (needs pillow, numpy, scipy)
"""
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

src, dst = sys.argv[1], sys.argv[2]
im = np.array(Image.open(src).convert('RGBA')).astype(np.int16)
rgb, a = im[..., :3], im[..., 3]
h, w = a.shape
lum = rgb.mean(axis=2)
sat = rgb.max(axis=2) - rgb.min(axis=2)
red = (rgb[..., 0] > 140) & (rgb[..., 1] < 100) & (a > 128)
ink = (lum < 90) & (a > 180) & (sat < 60)
barrier = ink | red
# The interior is the region reachable from the centre without crossing the inner ink line or the pin.
regions, _ = ndimage.label(~barrier)
interior = regions == regions[h // 2, w // 2]
if interior[0, 0] or interior.sum() > 0.9 * h * w:
    sys.exit('flood escaped the inner border; refusing to write')
# Flat paper colour: median of the clean paper band between the inner line and the torn edge; fall back to the light paper inside.
band = (~interior) & (a > 240) & (lum > 200) & (sat < 60)
if band.sum() < 1000:
    band = interior & (a > 240) & (lum > 200) & (sat < 60)
paper = np.median(rgb[band], axis=0).round().astype(np.int16)
if not band.any():
    sys.exit('no clean paper to sample; refusing to write')
im[interior, :3] = paper
im[interior, 3] = 255
# Astra left the paper slightly see-through (alpha 247-252): make the body of the slip opaque, keep soft edge pixels.
im[im[..., 3] > 200, 3] = 255
# Fringe: saturated pixels that are not the pin. Keep the pin's bounding box (grown by 12 px) untouched.
ys, xs = np.nonzero(red)
lab, n = ndimage.label(red)
sizes = ndimage.sum(red, lab, range(1, n + 1))
pin = lab == (int(np.argmax(sizes)) + 1)
py, px = np.nonzero(pin)
keep = np.zeros_like(red)
keep[max(py.min() - 12, 0):py.max() + 13, max(px.min() - 12, 0):px.max() + 13] = True
fringe = (sat > 70) & ~keep & ~interior & (a > 0)
im[fringe, 3] = 0
Image.fromarray(im.clip(0, 255).astype(np.uint8), 'RGBA').save(dst)
print(f'paper {tuple(paper)}, interior {interior.sum()} px ({interior.mean():.0%}), fringe cleared {fringe.sum()} px, pin box {px.min()},{py.min()}-{px.max()},{py.max()}')
