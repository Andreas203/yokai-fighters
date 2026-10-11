# UI screen kit - spec

Status: PROPOSED, awaiting designer approval of each job and its credits. **No Meshy job has been run and no generating tool was called.** Credit costs are taken from earlier specs (`ui-paper-demo.md`, `ryo.md`: `nano-banana` 3, `nano-banana-pro` 9 per image); the balance was not checked and the per-tool prices are not re-verified against the MCP.

Targets: the 11 concepts in `docs/design/screens/` (layout targets only). Text, bars, meters, buttons, key chips and the dimmed overlays are live Godot controls. This kit is only the hand-inked paper and brush material that StyleBox work cannot fake.

## What already exists (do not generate twice)
| File | What it is now | Covers |
|---|---|---|
| `game/assets/generated/ui/paper-panel.png` (U1) | Square rice-paper fibre texture inside a pale grey border, no alpha, no torn edge | **Interior fill / tiling texture only.** Crop the inner square (about x,y 100-925 of 1024) for `StyleBoxTexture` tiling on menu rows, the pause side cards, the move list. Does NOT cover torn sheet edges (K1, K2). Tileability unverified. |
| `brush-bar.png` (U2) | Photographic thick black impasto block on white, no alpha, textured | **Not the selection highlight and not an underline** (wrong shape, heavy texture). Free fallback for K3 only: threshold to alpha, flatten, tint red in engine. |
| `red-seal.png` (U3) | Glossy 3D red disc with a real, readable kanji | **Unusable** (breaks V1 and the no-real-characters rule). Not covered. Replaced by H2 in `ui-hud-ornaments.md` (so the unapproved U3 retake need not run) and optional K6 here. |

## Prompt base (all jobs)
Style block from `assets/specs/style.md`, then the job line, then "Avoid:" the shared negative plus "readable text, real kanji or letters, 3D render, glossy, photographic paper grain, drop shadow, hands, characters".

Alpha: paper jobs are generated on a flat chroma green #00FF00 background and keyed out (paper cream and ink black do not collide with it); stroke jobs are generated black or red on pure white and converted to alpha. Strokes are delivered as **white-on-alpha masks** so Godot tints them with `modulate` (one texture serves shrine red #B5332B, persimmon #D8632C, ink #1D1B21). Cleanup is image processing on the downloaded file, not a new generation.

## Jobs
All `meshy_text_to_image`, `ai_model: nano-banana`, no reference image, 3 credits per take. Take cap 3 per job; a retake needs fresh approval.

| # | Asset | Aspect | Prompt line (after style block) | Output | Credits/take |
|---|---|---|---|---|---|
| K1 | Wide torn rice-paper sheet | 16:9 | One large flat rice-paper #F1E8D4 sheet seen straight on, filling 92% of the frame, irregular hand-torn edge along the top and bottom with fibrous deckle, slightly ragged short sides, one soft ink-wash smudge hugging the top-left edge, faint paper fibres, no folds, no text, on a flat bright green #00FF00 background | `game/assets/generated/ui/kit/sheet-wide.png` | 3 |
| K2 | Small torn paper card | 3:2 | One small rice-paper #F1E8D4 card seen straight on, filling 92% of the frame, all four edges hand-torn with deckle, a small ink-wash smudge on one corner, no folds, no text, on a flat bright green #00FF00 background | `.../kit/sheet-card.png` | 3 |
| K3 | Red brush-stroke selection highlight | 16:9 | One bold horizontal dry-brush stroke in shrine red #B5332B, long and slim (about 5:1), rough bristle-streaked ends, solid centre, flat matte, no splatter, on plain pure white | `.../kit/brush-highlight.png` | 3 |
| K4 | Black ink underline stroke | 16:9 | One thin horizontal calligraphy underline in ink black #1D1B21, tapering to a point at the right and a blunt dry-brush start at the left, a tiny skip of dry brush near the middle, flat matte, on plain pure white | `.../kit/ink-underline.png` | 3 |
| K5 (OPTIONAL) | Rough ink frame | 1:1 | A square hand-inked rectangular border frame, even slightly wobbly brush-ink line, small dry-brush breaks, empty centre, plain pure white, no corner ornaments | `.../kit/ink-frame.png` | 3 |
| K6 (OPTIONAL) | Square red seal, tomoe | 1:1 | One flat square vermilion #B5332B ink-stamp seal, slightly uneven stamped edge, an abstract three-comma swirl in rice paper colour cut out of the centre, no letters, top-down, matte, plain pure white | `.../kit/seal-square.png` | 3 |

Key-chip frame: **not a job.** A rounded 2 px ink-border rectangle with a rice-paper fill is a `StyleBoxFlat` (corner radius 4-6, border #1D1B21, fill #F1E8D4); the concept chips are clean, not hand-inked. K5 is only worth running if the designer wants the chips and card borders to wobble like the concept's rough card frames; it also gives the reward cards their coloured frames (tint indigo, pine, persimmon, red). Recommendation: skip K5, use StyleBoxFlat first and judge it in the engine.

Torn-sheet edges in the concepts also carry black ink-cloud wisps and red blossom branches. Not proposed (decoration, not needed to read the screen). Say so if wanted.

## Slicing and tiling
| Asset | Use | 9-slice / margins (px, at delivered size; confirm on import) | Notes |
|---|---|---|---|
| K1 | Controls, Reward, Modifier target, story card (stretched wide), pause left panel | Margins left/right/top/bottom about 6% of each side (tear depth), centre stretches. Keep the smudge in the top-left margin so it does not smear. | Stretching only the centre keeps the torn edge undistorted. The centre is plain paper so `paper-panel.png` can replace it if fibres look soft. |
| K2 | Story card, Defeat card, Demo complete, pause confirm dialog, pause run cards | Same 6% margins. | Story card is wider than K2's ratio, so the horizontal centre stretch must stay plain paper. |
| K3 | Selected row (Kihon), menu Start/Resume highlight, button background | Horizontal 3-slice: left cap 14%, right cap 14%, centre stretches (axis stretch H only; vertical margin 0). | White-on-alpha, `modulate` = #B5332B; for the persimmon wash behind selected cards `modulate` = #D8632C at 35% alpha. Text is a Label over it. |
| K4 | Heading underline (red and ink), horizontal rules above footers | 3-slice H: left cap 8%, right cap 22% (the taper must not stretch). | Tint #1D1B21 or #B5332B. |
| K5 | Card frame, key chip | 9-slice margins 8% each side; centre empty. | Optional. |
| K6 | Story card seal, pause corner mark, button icon | None, draw at fixed size (about 64-96 px). | Optional. |

## Rig plan
Not applicable, 2D UI textures.

## Acceptance criteria (checked on import)
1. Look (V1): flat matte tones, even ink weight, palette within tolerance of `style.md`, no baked shadow, no photographic grain (the U2 failure), no gloss.
2. Alpha: clean key with no green fringe or white halo; the torn edge keeps its fibres; strokes have a clean alpha ramp and survive tinting without a coloured fringe.
3. No readable text, no real kanji. K6 swirl is abstract.
4. Slicing: the 9-slice or 3-slice holds when stretched to 1500x700 and to a 640x360 card with no seam, smear or repeated smudge.
5. Consistency: K1, K2 and K6 share the same paper tone and ink weight; the set matches the concept paper at 1080p.
6. Licence: Meshy plan tier and commercial terms of generated output are UNKNOWN (not verifiable from the MCP). Designer to confirm before shipping.

Known risk: the generator drifted to glossy 3D and a real kanji on U3. Check K6 first for lettering. Image models often draw a faint drop shadow under torn paper (K1, K2); that is a RETAKE reason.

## Fallback after 3 failed takes per job
- K1, K2: use `paper-panel.png` (cropped) in a `StyleBoxTexture` with a jagged edge made by a procedural polygon mask (engine work, not a generated image).
- K3: threshold and tint `brush-bar.png` (free), or a flat red `StyleBoxFlat` with a skewed end.
- K4: a 2 px ink `StyleBoxFlat` line.
- K5, K6: `StyleBoxFlat` chip; K6 replaced by the corrected U3/H2 seal.

## Credit estimate
First pass K1-K4 = 12 credits; worst case at 3 takes each = 36. With optional K5 and K6: 18 per take, worst case 54.

## Takes
None. Nothing run, 0 credits spent.

| Job | Task id | Credits (est/actual) |
|---|---|---|
| K1 | not run | 3/0 |
| K2 | not run | 3/0 |
| K3 | not run | 3/0 |
| K4 | not run | 3/0 |
| K5 | not run (optional) | 3/0 |
| K6 | not run (optional) | 3/0 |

## Acceptance
Pending (no outputs yet).

## Designer questions
1. Approve K1-K4 (12 credits)? K5 and K6 (3 each), or StyleBox for chips and frames?
2. Accept one K1 reused for every wide sheet (Controls, Reward, Modifier target, Story), rather than one per screen?
