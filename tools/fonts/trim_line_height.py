"""Trim the line height of the bundled serif to Georgia Bold's (1.136 em), so layouts tuned on Georgia hold.

Usage: python tools/fonts/trim_line_height.py <upstream NotoSerif-Bold.ttf> <out.ttf>
Upstream (pinned to commit 28b15b4b43b7bed62b5cf6e6b0b5ff5846270535, name ID 5 "Version 2.015"; the repo has no NotoSerif-v2.015 tag):
  https://github.com/notofonts/notofonts.github.io/raw/28b15b4b43b7bed62b5cf6e6b0b5ff5846270535/fonts/NotoSerif/hinted/ttf/NotoSerif-Bold.ttf (SIL OFL 1.1).
SHA-256 upstream: 96656aa5cec8f1d6fd0e804c1fad397e1a1cfa082e6642124e0bda68cd8363ce
SHA-256 output (game/assets/fonts/NotoSerif-Bold.ttf): 437443a1a5d30f6a53b6630286bcb4abd19ce209d9ae869af72052269edf72dd
Only the vertical metrics change; no glyph is touched. Needs `pip install fonttools`.
"""
import sys

from fontTools.ttLib import TTFont

ASCENT, DESCENT = 917, 219  # per 1000 em: Georgia Bold hhea 1878 / 449 of 2048

f = TTFont(sys.argv[1], recalcTimestamp=False)
assert f["head"].unitsPerEm == 1000
f["hhea"].ascent, f["hhea"].descent, f["hhea"].lineGap = ASCENT, -DESCENT, 0
o = f["OS/2"]
o.sTypoAscender, o.sTypoDescender, o.sTypoLineGap = ASCENT, -DESCENT, 0
o.usWinAscent, o.usWinDescent = ASCENT, DESCENT
f.save(sys.argv[2])
