# Tanuki's tea house (V9, final boss stage) - stage generation spec

Ticket: YOK-152. Status: **APPROVED first pass, S1-S10, 60 credits.** Approved by the designer 2026-10-20 (recorded on the Linear ticket): S1-S10, 60 credits. Seasons change on beats, round start only (option B); the wiring is a gameplay follow-up, not asset-smith's; art cost is unchanged. Generator: Meshy for everything. **Not yet run: the Meshy MCP tools were not available in the session that was asked to run it, so no balance check was made and no credits were spent.** Take cap 3 per asset; running total across YOK-150/151/152 must not exceed 195. Method: `assets/specs/stages/bamboo-grove-dusk.md`, style block `assets/specs/style.md`. Generator: Meshy MCP, as the grove (Tripo for the meshes possible; would need re-costing).

Gameplay is a flat plane (F4). E1: stage 3,840 units (19.2 m) wide; camera shows 1,060 to about 1,900 units. The Tanuki fights as Ryo with Ryo's model (T0), so the stage must hold two Ryo-shaped silhouettes (indigo coats): keep the back wall warm and light so indigo reads against it.

Stage mood: a warm lamplit tea house interior, wooden beams, tatami floor, a row of tall paper screens along the back wall whose scenery changes between the four seasons. Cheerful and slightly too welcoming (the first-loss card: "the paper screens fold shut").

## Palette
Style palette: rice paper `#F1E8D4` (screens, walls), persimmon `#D8632C` (lamp glow, autumn), brass/straw `#B08A48` (tatami, beams), shrine red `#B5332B` (accents), pine `#34483B` (summer leaves), indigo `#2D3A5E` (shadow tones, night-blue winter sky), ink `#1D1B21`. Tanuki brown `#6E5A48` is confirmed (2026-10-20, in `style.md`); used here only for beams and furniture. **Season accents (confirmed 2026-10-20, in `style.md`):** spring blossom pink `#E8A6B0`, winter snow shade `#A9B4C8` (same as YOK-150). Designer to confirm.

## Design: how the seasons work (generation side)
The back wall is one backdrop image with the screen openings left as flat blank rice-paper panels. Four season images are generated for those panels (spring, summer, autumn, winter) with identical layout, so Godot can cross-fade between them on the same quad. The screens carry the only motion in the stage; nothing in the textures is animated or baked as a flicker. Timing and trigger are logic (see the open question), not art, and the art cost is the same either way.

## Lessons from the grove, applied
- Crop the grey letterbox on Meshy 16:9 outputs (about x 110-1266, y 75-693 of 1376x768); prompts ask for full-bleed.
- Meshy props pivot at the mesh centre: re-pivot to the base in Godot.
- Strips come back gritty unless told "flat vector cel shapes, no grit, no noise, seamless".
- Props at or under about 3k tris.
- Do not draw a kettle transformation or any tanuki figure in the art (`characters/tanuki.md`: no kettle transformation drawn; the merchant and true form are separate art). A plain tea kettle as furniture is fine.

## Prompts (after the style block)

Interior backdrop (S1, `meshy_text_to_image`, `nano-banana-pro`, `aspect_ratio: 16:9`):
> Wide stage backdrop, the interior of a warm Japanese tea house seen from a low flat viewpoint, dark wooden beams and posts, a long back wall made of five tall shoji screens with blank rice-paper panels evenly spaced (panels completely empty and plain pale paper, no drawing on them), a low wooden ceiling with two hanging paper lanterns glowing persimmon, tatami floor visible in the lower third, empty and uncluttered in the lower third, no characters, no text, full-bleed to the image edges with no border or frame. Flat cel colours, soft ink-brush edges. Palette: rice paper #F1E8D4, straw #B08A48, brown #6E5A48, persimmon #D8632C, shrine red #B5332B. Avoid: [negative prompt].

Season screens (one tall screen panel each, 2:3, all four with the same composition, a low horizon line and a single large tree on the left):

Spring (S2, `meshy_text_to_image`, `nano-banana`, 2:3):
> A tall painted paper screen panel, a single flowering cherry tree with pink blossoms on the left, a pale peach sky and a low green hill, falling petals as flat shapes, ink-brush outline, flat cel colours, the painting fills the whole image edge to edge. Palette: blossom pink #E8A6B0, rice paper #F1E8D4, pine #34483B, ink #1D1B21. Avoid: [negative prompt].

Summer (S3, `meshy_image_to_image`, `nano-banana`, reference = S2, reference strength as high as the tool allows):
> The same screen painting and composition, now summer: the same tree in full pine-green leaves, a bright pale blue-green sky, a low green hill, a few flat dragonfly shapes. Same outline, same layout, same tree position. Avoid: [negative prompt].

Autumn (S4, `meshy_image_to_image`, `nano-banana`, reference = S2):
> The same screen painting and composition, now autumn: the same tree in persimmon orange and shrine red leaves, a warm pale gold sky, a low brown hill, a few flat falling leaves. Same outline, same layout, same tree position. Avoid: [negative prompt].

Winter (S5, `meshy_image_to_image`, `nano-banana`, reference = S2):
> The same screen painting and composition, now winter: the same tree bare with snow on its branches, a pale blue-grey sky, a low snow-covered hill, a few flat snowflakes. Same outline, same layout, same tree position. Palette: snow shade #A9B4C8, rice paper #F1E8D4, indigo #2D3A5E, ink #1D1B21. Avoid: [negative prompt].

Low tea table (S6, `nano-banana`, 1:1):
> A low round wooden tea table with a small iron teapot, two small tea cups and a plate of round rice sweets, isolated on plain white, brown wood and rice paper tones, simple shapes, flat cel shading. Avoid: [negative prompt].

Hanging paper lantern (S8, `nano-banana`, 1:1):
> A single round hanging paper lantern with thin ink ribs, rice paper body glowing persimmon orange from inside, small brass cap and a short cord, isolated on plain white, flat cel shading, no flame, no glow halo. Avoid: [negative prompt].

Tatami floor strip (S10, `nano-banana`, 16:9):
> Top-down tileable strip of tatami mats with dark cloth borders, straw-gold #B08A48 with thin darker weave lines, flat vector cel shapes, no grit, no noise, seamless left and right, no shadows. Avoid: [negative prompt].

## Job chain

| # | Tool | Settings | Credits |
|---|---|---|---|
| S1 | `meshy_text_to_image` | interior backdrop, `nano-banana-pro`, 16:9 | 9 |
| S2 | `meshy_text_to_image` | spring screen, `nano-banana`, 2:3 | 3 |
| S3 | `meshy_image_to_image` | summer, `nano-banana`, reference S2 | 3 |
| S4 | `meshy_image_to_image` | autumn, `nano-banana`, reference S2 | 3 |
| S5 | `meshy_image_to_image` | winter, `nano-banana`, reference S2 | 3 |
| S6 | `meshy_text_to_image` | tea table, `nano-banana`, 1:1 | 3 |
| S7 | `meshy_image_to_3d` | `input_task_id`: S6, `meshy-6-lite`, `target_polycount: 3000`, `should_remesh: true`, `texture_resolution: 2k`, `enable_pbr: false`, no pose_mode | 15 |
| S8 | `meshy_text_to_image` | hanging lantern, `nano-banana`, 1:1 | 3 |
| S9 | `meshy_image_to_3d` | `input_task_id`: S8, same settings as S7, `target_polycount: 1500` | 15 |
| S10 | `meshy_text_to_image` | tatami strip, `nano-banana`, 16:9 | 3 |

| Cost | Credits |
|---|---|
| Backdrop (S1) | 9 |
| Four season screens (S2-S5) | 12 |
| Tea table (S6+S7) | 18 |
| Hanging lantern (S8+S9) | 18 |
| Tatami strip (S10) | 3 |
| **First pass (S1-S10)** | **60** |

Take cap 3 per asset. If the season screens drift out of layout, retake only the drifting one (3 credits). Run `meshy_check_balance` first. Run only after approval.

## Assembly (Godot, not generation)
Backdrop on a quad behind the fight plane (crop the letterbox). The four season images are placed over the blank panels by a shader or four stacked quads at the same position, each opacity set by the controller; if the tall 2:3 image is too tall for a panel it is cropped identically for all four. Hanging lantern mesh 2-4 times at ceiling height with a slow tick-driven sway; tea table 1 time at an edge outside the fighter band. Tatami strip tiled on the floor plane. Interior lighting is a Godot setting.

**Determinism:** the season state is a function of the sim tick (or of fight events), never of wall-clock time or an `AnimationPlayer` free-running, so replays and the harness see the same stage (T-rules; deterministic 60-tick loop).

## Acceptance criteria
- Palette matches; no baked shadows; no characters, text or kanji; no letterbox after crop; panels in S1 are blank (a drawing there is a RETAKE: it would double up with the season images).
- The four season images share layout: tree position and horizon within a few pixels of each other (checked by flicking between them), so a cross-fade reads as a season change, not a scene change.
- Each season reads at 720p from its dominant colour (pink, green, orange, blue-grey) when a Ryo-shaped fighter stands in front; indigo coats still read against the light wall.
- Props: single clean mesh, at or under the tri target (flag +10%), one 2K texture, no PBR maps, no floating geometry, origin noted.
- Tatami strip tiles without a visible seam (tile 3x).
- Look consistent with Ryo, Kitsune and the grove once the outline pass is applied.
- Licence: plan tier unknown, flagged (AM5, YOK-115).

## DECIDED 2026-10-20 (was an open question): option B, seasons change on beats, round start only. Gameplay follow-up wires it; kept below for the record.
**Do the screens flicker through the seasons all fight long, or only on beats?**

| Option | What it means | Consequence I can see |
|---|---|---|
| A. All fight | A continuous cycle (for example one season per N seconds of sim ticks) with a cross-fade, running the whole fight | Always alive, matches "season-flickering screens" (V9) literally; busiest background behind the fighters, so readability at 720p is the risk; photosensitivity needs a hard limit on change rate and cross-fades instead of hard cuts (well under 3 changes per second; I would suggest one change every several seconds if A is chosen) |
| B. On beats | Seasons change only on chosen events (candidates: round start, a copied special landing, a knockdown, the final round, or a music beat) and hold between them | Calmer, readability safer, the change carries meaning; needs the designer to pick which events (and whether the season maps to something such as round number or the copied move's level), and the gameplay programmer wires them |

Art and generation cost are the same either way (the same four images, 12 credits). This is a design and presentation question, so it goes to the designer; no agent should pick it. If B is chosen, the beats list is a data file for gameplay-programmer, not for this spec.

Other open points:
1. Brown `#6E5A48`, spring pink `#E8A6B0` and snow shade `#A9B4C8` are proposed, not in `style.md`: confirm?
2. Generator: Meshy as the grove (costed above), or Tripo for the meshes?

## Takes
None yet. Ledger: approved 60, spent 0, balance not checked.

