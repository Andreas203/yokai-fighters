# Kitsune — character sketch v0.1

The fox of the bamboo grove at dusk. A zoner who holds the screen with slow foxfire, teases you into jumping, then steps through her own illusion to hit you from behind.

Status: first-pass concept, 2026-10-05. Not final art.

## Description

- **Silhouette:** fox ears, white hair tipped orange, and three tails fanned out behind her. Her outline says fox before you look at her face.
- **Mask:** a full white fox mask with red markings hides her face for the whole fight. Only her foxfire eyes show through the slits. Designer decision: the mask stays on for the 5-week build, story cards included.
- **Outfit:** cropped white haori with wide sleeves and red trim over an indigo high-neck top. Foxfire obi with an indigo cord and a suzu bell, shrine-red hakama to the knee, black tabi and geta.
- **Power:** she is the zoner: Foxfire at mid range while she advances, and she anti-airs your jumps (Y2). She holds foxfire palm-up and casts with a fox hand sign. Her orange matches the foxfire hit spark (V5). Her stage is the bamboo grove at dusk (V9).
- **Tails:** three for the common Kitsune; the Nine-Tailed elder gets nine.

## Palette

| Colour | Hex | Used on |
|---|---|---|
| Fur white | `#FBF6EC` | hair, tails, haori, mask |
| Foxfire | `#F2A23A` | fire, hair and tail tips, obi |
| Shrine red | `#B5332B` | hakama, trim, mask marks |
| Indigo | `#2D3A5E` | inner top, cord |
| Pine | `#34483B` | bamboo grove (stage) |

## Files

| File | What it is |
|---|---|
| `sheet.html` | Full model sheet: annotated front sketch, head, silhouette test, Foxfire and Fox Mirage studies, open questions. Open in a browser. |
| `kitsune-meshy-front.png` | Clean front view for Meshy image-to-3D, 1024×2112: A-pose (arms ~18° out), tails fanned low, white background, no text or effects. Try this first. |
| `kitsune-meshy-front-notails.png` | Same without tails, for when tails come out badly or are built as a separate rigged mesh. |
| `*.svg` | Vector sources of the Meshy images. |
| `..\MESHY_CONCEPT_PROMPTS.md` | Image-reference prompt and validation criteria for the final toon-shaded turnaround. Generate this before submitting to Meshy. |

The current Meshy images are flat visual references, not final concept art. Generate the constrained front/side/back turnaround in `..\MESHY_CONCEPT_PROMPTS.md` first; upload only its selected front view to Meshy. The prompt explicitly preserves the full fox mask: the face must never show in generated art. It drops the foxfire orb and the fox hand sign, and hangs the ponytail straight down. Sleeves and hakama are baked into the body, so they will not sway without added bones.

## Open questions (designer)

1. Tail count: three for the common Kitsune and nine for the elder, or one count for both with the elder told apart by colour and VFX only (a level preset)?
2. Tails and sleeves need extra bones with sway, or procedural sway driven from the tick count to stay deterministic; record the choice in Asset Smith's rig plan (`assets/specs/kitsune.md`).
3. Does the aggressive temperament get its own idle (leaning forward, tails flared), or share this patient stance?
