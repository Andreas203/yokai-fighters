# Finish-VFX textures (YOK-243)

Status: approved 2026-11-12, run and accepted. Rules: V1, V5, V8, A5, F6, G3. Colour-only presets for the five new specials are already in `data/presets/` (YOK-233), so every texture below is an upgrade, never a dependency (G3: colour-only stays the fallback).

## Design rule: one white texture, tinted by the preset

- Every image is **white ink on a solid pure black (#000000) background**, high value contrast, no grey plates, no paper fill. Known Astra failure: flat pale plates lose their alpha under background removal. Here nothing depends on background removal: a script turns **luminance into alpha** and sets RGB to white. The preset then multiplies by its colour (`tint: true`, the schema default), so one image serves Lv 1, Lv 2 and the evolution (V8, A5).
- Inner "core" colour comes from the preset's `core` (default rice paper), so shapes must read as a solid body plus a thinner bright edge; no baked colour.
- Style: paper-talisman and ink-brush, flat shapes, confident tapered brush edges, no gradients beyond one soft fade at the trailing edge, no glow, no particles, no text or readable letters (seal marks are abstract strokes).
- Sources of hit-spark colour stay V5 (foxfire orange, oni red, kappa teal, ink black); these textures are move art, not sparks.

## Textures (8 takes, plus up to 2 retakes held in reserve)

Folder: `game/assets/generated/vfx/`. In preset JSON the path is relative to `game/`, e.g. `assets/generated/vfx/effects/fx-afterimage.png` (the schema's default lookup is `game/vfx/...`; the programmer maps or the files are copied, not my call).

| # | File | Size | Alpha | Shows | Used by |
|---|---|---|---|---|---|
| 1 | `projectiles/proj-ink-wave.png` | 1024x512 | luma | Crescent ink-brush wave crest facing right, ragged tail trailing left | Spirit Wave, Great Wave (projectile, scale_pct for Great Wave) |
| 2 | `projectiles/proj-fox-flame.png` | 1024x512 | luma | Teardrop foxfire flame, head right, three flame tongues trailing left | Foxfire, Piercing Foxfire (projectile) |
| 3 | `effects/fx-seal-burst.png` | 512x512 | luma | Round ofuda seal: square talisman outline, abstract stroke marks, ink rays | Rising Talisman, Heaven Seal (cast and impact); generic cast seal for all specials |
| 4 | `effects/fx-afterimage.png` | 512x1024 | luma | Upright ghost silhouette of a fox-eared figure with a big tail, torn smoke edges | Fox Mirage, Mirage Feint (afterimage and decoy) |
| 5 | `effects/fx-speed-trail.png` | 1024x256 | luma | Horizontal bundle of tapered brush streaks, thick at the right, fading left | Iron-Club Charge, Crushing Charge (charge trail); Torrent Spin forward travel |
| 6 | `effects/fx-ground-wave.png` | 1024x256 | luma | Low ground shockwave: jagged crack line with rock chips and a dust arc, flat to a horizon line | Oni Quake, Aftershock (release and second quake); Crushing Charge wall hit |
| 7 | `effects/fx-water-swirl.png` | 512x512 | luma | Top-down spiral of three thick brush swirls | River Grab, Whirlpool Grab (grab impact; larger at Lv 3) |
| 8 | `effects/fx-spin-ring.png` | 512x512 | luma | Ring of four curved motion arcs with arrowhead tapers, clockwise | Shell Spin, Torrent Spin (ring while spinning) |

Tint per level comes from the 14 presets: Lv 1 base colour, Lv 2 colour shift, evolution colour with `scale_pct` 130. Fox Mirage has no Lv 2 preset (the special names none).

## Prompts

Each prompt = style block (assets/specs/style.md) + the item line + the VFX block, then `Avoid:` = the style negative plus the VFX negative.

VFX block (all takes): "Single white ink-brush shape, pure white (#FFFFFF) on a perfectly flat solid black (#000000) background, edge to edge black with no border, no vignette. Strong contrast: white shapes only, no mid-grey fill, no paper texture. Shape centred with margin, flat cel shapes with one tapered brush edge. No colour."

VFX negative (add to every take): "colour, grey plate, paper background, texture on the background, gradient background, glow, particles, sparks, characters, faces, text, letters, kanji, border, frame."

Item lines:
1. "A wide crescent ink wave crest travelling right, solid white body with a thin gap line inside it, the trailing tail torn into brush streaks, canvas 2:1."
2. "A teardrop foxfire flame travelling right, rounded head on the right, three tapered flame tongues streaming left, a smaller inner flame cut out as a black gap, canvas 2:1."
3. "A paper talisman seal seen straight on: a square outline with a bold abstract stroke mark inside, eight short ink rays radiating outward evenly, canvas square."
4. "An upright ghost silhouette of a fox-eared standing figure with a large bushy tail, filled solid white, edges torn into smoke wisps, no face, canvas portrait 1:2."
5. "A bundle of five horizontal tapered brush streaks of different lengths, thick and solid at the right end, thinning to points toward the left, canvas 4:1."
6. "A low ground shockwave seen from the side: a jagged crack running along a flat horizon line, chunky rock chips above it and a flat dust arc rising from the crack, canvas 4:1."
7. "A top-down water whirlpool made of three thick curved brush swirls spiralling into the centre, tapered ends, canvas square."
8. "A ring made of four thick curved brush arcs chasing each other clockwise, each arc ending in a sharp arrowhead taper, empty centre, canvas square."

Rig notes: none (2D). Cost: 8 Astra takes plus up to 2 retakes; Codex does not report credits, so the designer's per-image figure applies (10 takes max).

## Run (after approval only)

Per take N (item prompt written to `<scratch>/prompt-N.txt`, parallel runs allowed):

`codex exec -m gpt-6-astra -s workspace-write -C C:/Users/Andreas/projects/wf-vfx -o <scratch>/out-N.txt - < <scratch>/prompt-N.txt`

The wrapper asks for exactly one image saved to `game/assets/generated/vfx/<folder>/<file>-raw.png` (no other edits); if saving is refused, collect the newest PNG from `~/.codex/generated_images/` by mtime. Then convert: resize to the size above, `alpha = luminance`, `RGB = white`, save as `<file>.png`, keep the `-raw.png`:

`python -I -c "from PIL import Image;import sys;i=Image.open(sys.argv[1]).convert('L').resize((int(sys.argv[3]),int(sys.argv[4])));o=Image.new('RGBA',i.size,(255,255,255,0));o.putalpha(i);o.save(sys.argv[2])" <raw> <out> <w> <h>`

## Takes

2026-11-12, designer-approved (10 takes cap). 8 takes, 0 retakes, run in parallel via `codex exec -m gpt-6-astra -s workspace-write`; Codex does not report credits (unrecorded). Raw takes kept as `*-raw.png`; luminance-to-alpha conversion as above. All 8 saved at the spec sizes.

## Acceptance (checked on import)

- Alpha: background fully 0, shape core above 240, no grey plate (measure the share of alpha 10-240 under 25%).
- Look (V1): reads white-on-clear at 720p fighting distance, flat shapes, no glow or noise; tinted by each of its presets' colours it still reads (Ink and Wood dark are the darkest tints: check against the stage backdrops).
- Licence: Astra output terms on the designer's plan allow commercial use (flag if unknown).
- Verdict per file: ACCEPT or RETAKE with the prompt change.

## Acceptance result (2026-11-12)

All 8 ACCEPT. Alpha: background 59-83% fully clear, 0.7-3.1% of pixels in the 10-240 band (limit 25%), core 15-39%. Look: flat white ink shapes with tapered brush edges, no glow or noise. Notes: `fx-ground-wave` is a thin low band (reads as a flat dust arc; scale it wide, about 3:1); the `fx-seal-burst` stroke mark is an abstract S-curve, not a letter; `fx-afterimage` is a standing figure with fox ears and a bushy tail, as asked. Licence: Astra terms on the designer's plan unconfirmed in the repo (flag, as in the icon spec).
