"""YOK-finish-qa: contact sheets from the stills of game/tests/captures/FinishQaCapture.cs.

One sheet per fighter and page: a row per clip (worst numeric score first), columns = start, active, recovery, worst
frame, each from the fight camera's side ("cam") and from the fighter's front ("side"). Every cell carries an id
(row number + column letter, e.g. "3F"), the frame and the view; the red line is the floor (y = 0), drawn over the body.
Writes <out>/<fighter>-p<n>.png and <out>/<fighter>-p<n>.json (cell id -> clip, frame, view).
Usage: python tools/rig_qa/sheets.py <capture dir> <out dir> [--rows 10]
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

PICKS = ["start", "active", "recovery", "worst"]
VIEWS = ["cam", "side"]
SCALE = 0.62
LABEL_W = 190


def font(size):
    for f in ("arialbd.ttf", "arial.ttf", "DejaVuSans-Bold.ttf"):
        try:
            return ImageFont.truetype(f, size)
        except OSError:
            pass
    return ImageFont.load_default()


def main():
    cap, out = Path(sys.argv[1]), Path(sys.argv[2])
    rows_per = int(sys.argv[sys.argv.index("--rows") + 1]) if "--rows" in sys.argv else 10
    out.mkdir(parents=True, exist_ok=True)
    summary = json.loads((cap / "summary.json").read_text())
    f_big, f_small = font(17), font(13)
    for body in ["ryo", "tanuki", "kitsune", "oni", "kappa"]:
        rows = [r for r in summary if r["body"] == body]
        for page in range((len(rows) + rows_per - 1) // rows_per):
            chunk = rows[page * rows_per:(page + 1) * rows_per]
            sample = Image.open(next((cap / "cells").glob(f"{body}__*__cam.png")))
            cw, ch = int(sample.width * SCALE), int(sample.height * SCALE)
            W = LABEL_W + cw * len(PICKS) * len(VIEWS)
            H = 40 + 22 + ch * len(chunk)
            sheet = Image.new("RGB", (W, H), "white")
            d = ImageDraw.Draw(sheet)
            d.text((8, 8), f"{body} page {page + 1}: game pose (root motion stripped, hip scale); red line = floor y=0; cam = fight camera, side = facing the fighter", fill="black", font=f_big)
            cells = {}
            for ci, (pick, view) in enumerate((p, v) for p in PICKS for v in VIEWS):
                d.text((LABEL_W + ci * cw + 4, 42), f"{chr(65 + ci)}: {pick} {view}", fill="black", font=f_small)
            for ri, r in enumerate(chunk):
                y = 62 + ri * ch
                d.rectangle([0, y, LABEL_W - 2, y + ch - 1], outline="grey")
                d.text((6, y + 6), f"{ri + 1}", fill="black", font=f_big)
                words, line, lines = r["clip"].split("-"), "", []
                for w in words:
                    if len(line) + len(w) > 17:
                        lines.append(line); line = ""
                    line += ("" if not line else "-") + w
                lines.append(line)
                for li, t in enumerate(lines):
                    d.text((30, y + 6 + li * 18), t, fill="black", font=f_small)
                d.text((30, y + 10 + len(lines) * 18), f"{r['frames']} frames", fill="grey", font=f_small)
                if r["hit"]:
                    d.text((30, y + 28 + len(lines) * 18), f"hit {r['hit'][0]}-{r['hit'][1]}", fill="grey", font=f_small)
                for ci, (pick, view) in enumerate((p, v) for p in PICKS for v in VIEWS):
                    f = r["picks"][pick]
                    path = cap / "cells" / f"{body}__{r['clip']}__{pick}-f{f:03d}__{view}.png"
                    x = LABEL_W + ci * cw
                    if path.exists():
                        im = Image.open(path).convert("RGB").resize((cw, ch), Image.LANCZOS)
                        sheet.paste(im, (x, y))
                    cid = f"{ri + 1}{chr(65 + ci)}"
                    d.rectangle([x, y, x + cw - 1, y + ch - 1], outline="grey")
                    d.text((x + 4, y + 3), f"{cid} f{f}", fill="blue", font=f_small)
                    cells[cid] = {"clip": r["clip"], "frame": f, "pick": pick, "view": view}
            name = f"{body}-p{page + 1}"
            sheet.save(out / f"{name}.png")
            (out / f"{name}.json").write_text(json.dumps(cells, indent=0))
            print(out / f"{name}.png", sheet.size)


if __name__ == "__main__":
    main()
