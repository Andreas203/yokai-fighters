"""Trim the line height of the bundled serif to Georgia Bold's (1.136 em), so layouts tuned on Georgia hold.

Usage: python tools/fonts/trim_line_height.py <upstream NotoSerif-Bold.ttf> <out.ttf>
Upstream: https://github.com/notofonts/notofonts.github.io/raw/main/fonts/NotoSerif/hinted/ttf/NotoSerif-Bold.ttf (SIL OFL 1.1).
Only the vertical metrics change; no glyph is touched. Needs `pip install fonttools`.
"""
import sys

from fontTools.ttLib import TTFont

ASCENT, DESCENT = 917, 219  # per 1000 em: Georgia Bold hhea 1878 / 449 of 2048

f = TTFont(sys.argv[1])
assert f["head"].unitsPerEm == 1000
f["hhea"].ascent, f["hhea"].descent, f["hhea"].lineGap = ASCENT, -DESCENT, 0
o = f["OS/2"]
o.sTypoAscender, o.sTypoDescender, o.sTypoLineGap = ASCENT, -DESCENT, 0
o.usWinAscent, o.usWinDescent = ASCENT, DESCENT
f.save(sys.argv[2])
