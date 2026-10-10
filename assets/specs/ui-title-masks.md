# Title screen yokai masks - spec

Ticket: YOK-58. Status: PROPOSED, awaiting designer approval of jobs and credits. No Meshy job has been run.

Designer request: "make the title screen more interesting with yokai masks". Flat, front-facing paper-talisman masks, one per roster yokai, cut to alpha for the title screen.

## Prompt base
Every prompt starts with the style block in `assets/specs/style.md`, then the line below, then the mask line, then the negative prompt.

> Single flat front-facing theatre mask drawn as a paper talisman, perfectly symmetrical, centred, filling 80% of the frame, rice paper #F1E8D4 base with visible ink-brush outline of even weight, a few bold brush strokes for markings, one small shrine red #B5332B seal accent, matte, flat 2D, plain pure white background, no shadow, no strings, no stand.

Negative prompt: the shared one from `style.md`, plus "readable text, real kanji or letters, 3D render, glossy plastic, photographic wood grain, hands, body, hanging cord".

## Jobs
All `meshy_text_to_image`, model `nano-banana` (as in `ui-paper-demo.md`), aspect 1:1, 3 credits each. Cleaned to alpha after download (threshold the white background). Palette colours come from `style.md`.

| # | Mask | Mask line (after prompt base) | Output | Credits |
|---|---|---|---|---|
| M1 | Kitsune | Full white fox mask, fur white #FBF6EC, pointed ears, long narrow eye slits, red markings in shrine red #B5332B along the cheeks, brow and nose bridge, foxfire #F2A23A touch on the ear tips | `game/assets/generated/ui/title/mask-kitsune.png` | 3 |
| M2 | Oni | Horned demon mask, broad brow, two short horns, heavy square jaw with two small tusks, fierce wide eyes, skin tone oni red (muted shrine red #B5332B), ink-black brows, rice paper tusks and horn tips | `game/assets/generated/ui/title/mask-oni.png` | 3 |
| M3 | Kappa | River-spirit mask, rounded beak-like mouth, wide round eyes, kappa teal skin (muted teal, flat), a shallow round dish on the crown of the head, pine #34483B shadow strokes | `game/assets/generated/ui/title/mask-kappa.png` | 3 |
| M4 (OPTIONAL) | Tanuki | Round-faced trickster mask, soft round ears, dark eye patches, broad smiling mouth, rice paper and ink brown, one leaf-shaped red seal on the forehead | `game/assets/generated/ui/title/mask-tanuki.png` | 3 |
| B1 (OPTIONAL) | Backdrop | Dusk wall of a shrine, empty wooden ema board rows and ink-wash indigo #2D3A5E sky fading to persimmon #D8632C glow at the horizon, bamboo silhouettes in pine #34483B, wide, calm, low detail, no masks, no text. Aspect 16:9 | `game/assets/generated/ui/title/backdrop-dusk.png` | 3 |

First pass: M1-M3 = 9 credits. With optional M4 and B1 = 15 credits. Take cap 3 per job. A retake costs 3 per job and needs fresh approval.

Colour notes: the Oni and Kappa looks come from `vault/01-vibe/Characters.md` (kanabo club, tusks and "oni red" sparks; river spirit with a head dish and "kappa teal" sparks). No fighter reference images exist yet for Oni or Kappa, so these masks are folklore-led and must be checked against the Oni and Kappa sheets when those land. The kitsune markings follow `assets/specs/kitsune.md` and the Characters note (full white fox mask, red markings).

## Rig plan
Not applicable. 2D UI textures. ui-designer layers the masks in Godot (idle sway or bob may be driven from the tick count, deterministic).

## Acceptance criteria (checked on import)
1. Look (V1): ink outline of even weight, flat tones, palette within tolerance of `style.md`, matte, no PBR gloss, no wood grain, no painterly noise.
2. Alpha: white background cuts cleanly with no halo; the mask is centred and not cropped.
3. No readable text or real kanji; any seal mark is abstract.
4. Consistency: the four masks share outline weight, paper tone and seal style, and read as one set at 256 px.
5. Kitsune matches the character sheet; Oni has horns and tusks; Kappa has the head dish.
6. Licence: Meshy plan tier and commercial terms of generated output are UNKNOWN (not verifiable from the MCP). Designer to confirm before shipping.

Known risk from `ui-paper-demo.md` U3: the generator drifted to glossy 3D and a real kanji on a seal. The prompt base says "flat 2D", "matte" and "abstract"; check these first.

## Designer questions
1. Approve M1-M3 (9 credits)? Optional M4 and B1 (3 each)?
2. **Tanuki spoiler:** `vault/01-vibe/Characters.md` says the merchant and the final boss are the same creature, and the Tanuki's reveal (camera push-in, V7) is a story beat. A Tanuki mask on the title screen would hint that the Tanuki exists and is a yokai. It does not by itself reveal that the merchant is the Tanuki. Recommendation: leave M4 out and let the three fighters carry the title. Your call.
3. The Oni and Kappa masks have no character sheet yet. Accept folklore-led looks for now?

## Takes
None yet. Not approved.

## Acceptance
Pending.
