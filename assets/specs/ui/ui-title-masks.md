# Title screen yokai masks - spec

Ticket: YOK-58. Status: PROPOSED, awaiting designer approval of jobs and credits. No Meshy job has been run. NOTE: the M1-M3 approval below is stale: the prompts change in the extension at the end of this file, so the designer must re-confirm before anything runs.

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
Designer approved M1-M3 (9 credits), skipped M4. Not run yet: the Meshy MCP tools were not available in the asset-smith session (no `meshy_*` tools exposed), so no balance check and no jobs were made. 0 credits spent.

| Job | Task id | Credits (est/actual) |
|---|---|---|
| M1 Kitsune | not run | 3/0 |
| M2 Oni | not run | 3/0 |
| M3 Kappa | not run | 3/0 |
| M4 Tanuki | skipped by designer | 0/0 |
| B1 Backdrop | not approved | 0/0 |

## Acceptance
Pending (no outputs yet).

---

# Extension (screen-art pass, no ticket): shrine stage plate and mask changes

Status: PROPOSED, 2026-10-10. **No Meshy job has been run and no generating tool was called.** Target: `docs/design/screens/title.png`.

## M1-M3 need the designer to re-confirm before running
The earlier approval of M1-M3 (9 credits) was for the prompts above, and it was never run. This extension **changes those prompts** (cords, tassels, bell, horn and dish detail), so the old approval should not be treated as covering them. Nothing runs until the designer confirms the revised M1-M3 below (still 3 credits each, 9 per take, worst case 27). M4 (Tanuki) stays skipped, and no Tanuki appears anywhere in the title art.

## Mask changes the concept implies
The concept masks hang on wooden stands, with cords and tassels, and are shaded with a slight three-quarter depth. We keep the flat, front-facing, ink-outlined talisman look (V1), so **only the attachments change**. In the prompt base, replace "no strings, no stand" with "a knotted cord loop and a hanging tassel on each side of the face, the cords and tassels are part of the mask, no stand, no wall, no body" and drop "hanging cord" from the negative prompt (keep "stand" out).

| Mask | Revised mask-line additions (append to the line above) | Why |
|---|---|---|
| M1 Kitsune | Two shrine red #B5332B knotted cord loops at the lower cheeks, each with a hanging red tassel and one small brass #B08A48 bell. Eye slits dark, nose small and black. | The concept's tassels and bells; also lets M1 double as the corner ornament on the HUD, Controls, Reward, Modifier target and Defeat screens (`ui-hud-ornaments.md`). Full mask, face never shown. |
| M2 Oni | Horns and tusks in bone-ivory (rice paper with ink shading), thick dark hair fringe behind the brow, yellow #F2A23A eyes, red-brown tassel on one side. | The concept's ivory horns and dark hair. |
| M3 Kappa | Round dish with a shallow pool of water (flat pale blue-grey ring, no gloss), leaf-like fringe of teal hair around the face, yellow beak, a teal tassel on a rope loop. | The concept's dish with water, leaf fringe and beak. Water uses indigo and rice paper, no gloss. |

Output size: ask for the highest resolution `nano-banana` returns; the masks display at about 500 px tall on the title, 220 px as the corner ornament.

## New job: shrine stage plate
| # | Asset | Tool and settings | Output | Credits/take |
|---|---|---|---|---|
| S1 | Title plate: shrine stage with sun and empty mask stands | `meshy_text_to_image`, `ai_model: nano-banana-pro`, aspect 16:9, highest resolution | `game/assets/generated/ui/title/plate-shrine.png` | 9 |

Prompt: style block from `assets/specs/style.md`, then:

> Wide 16:9 key-art plate on warm rice paper #F1E8D4. Left 45%: calm and mostly empty paper with a large flat persimmon #D8632C sun disc behind soft ink-wash cloud bands at the upper left, a misty ink-wash cliff with a small distant pagoda and layered pines in pine #34483B and indigo #2D3A5E at the lower left, plenty of empty paper where a title and menu will sit. Right 55%: a shrine mask stand scene, two vermilion #B5332B torii pillars framing the top and right, a thick twisted rope with a hanging zigzag paper streamer at the top, bamboo leaves and indigo night-blue sky with soft clouds behind, a dark wooden low table in front with a red cloth draped over it carrying a rice-paper circular three-comma swirl emblem (abstract, no letters), and three EMPTY dark wooden mask stands standing in a row on the table (left, centre taller, right), nothing on them. No masks, no characters, no animals, no text, no lettering, no logo.

Avoid: the shared negative plus "readable text, real kanji or letters, masks, faces, characters, Tanuki, fox, logo, title lettering, 3D render, gloss, photographic".

Engine composes: M1 centred slightly taller on the centre stand, M2 on the left, M3 on the right (stand positions measured on import; the plate is generated before the masks so the masks can be scaled to the stands). The sun disc and any cloud decoration live in the plate, so the live menu text sits over calm paper. Menu highlight is K3 from `ui-screen-kit.md`.

### Acceptance (S1, added to the list above)
1. Left 45% really is calm paper (menu legibility at 1080p); sun, cliff and pagoda low contrast.
2. Three stands visible, empty, evenly spaced, centre one taller or higher; masks can overlay without overlap with the red cloth emblem.
3. No text, no real kanji, emblem is an abstract three-comma swirl; no Tanuki, no creature.
4. Flat cel, ink-brush edges, palette within tolerance, matte (the concept's painterly cloud noise is not a target).
5. Licence UNKNOWN, designer to confirm.

Fallback after 3 failed takes: B1 backdrop (`backdrop-dusk.png`, 3 credits, still optional above) with the K1 paper panel on the left and a Godot-drawn table and three stand rectangles.

### Credits (extension)
S1 first pass 9, worst case 27. Revised M1-M3 first pass 9, worst case 27 (same cost as before, new approval).

## Open decision for the designer: title logo lettering
The concept's "YOKAI FIGHTERS" is brush lettering with a red second line, and the other concepts use the same brush face for headings (REWARD, CONTROLS, PAUSED, WHERE DOES WILL-O'-WISP GO?). **This is not a generation job**: baked lettering would break the no-text rule, misspell, and could not be localised or edited. Options for the designer:
1. A licensed or open-licence brush-style font used as a Godot font for all headings (cheapest, editable, consistent); the title adds a K3/K4 stroke or an outline for the red line.
2. A one-off logo image made outside the generator (designer or a commissioned artist), imported as a texture.
3. A plain bold serif for the logo (matches the body text), no brush at all.
Which, and who sources the font and checks its licence? Until then ui-designer uses the bold serif body font as a placeholder.

## Takes (extension)
None. Nothing run, 0 credits spent.

| Job | Task id | Credits (est/actual) |
|---|---|---|
| S1 plate | not run | 9/0 |
| M1-M3 revised | not run (re-confirm) | 9/0 |
