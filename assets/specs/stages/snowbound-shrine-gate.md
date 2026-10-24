# Snowbound shrine gate (V9, Oni's stage) - stage generation spec

Ticket: YOK-150. Status: **APPROVED first pass, S1-S8 only, 66 credits. S9 (snowflake sprite sheet) NOT approved: the snowfall uses plain quads.** Approved by the designer 2026-10-20 (recorded on the Linear ticket): core S1-S8, 66 credits. Generator: Meshy for everything. **Not yet run: the Meshy MCP tools were not available in the session that was asked to run it, so no balance check was made and no credits were spent.** Take cap 3 per asset; running total across YOK-150/151/152 must not exceed 195. Method: `assets/specs/stages/bamboo-grove-dusk.md` (layers back to front: backdrop image, midground props, edge props, ground strip), style block `assets/specs/style.md`. Generator: Meshy MCP (`meshy_text_to_image`, `meshy_image_to_3d`), as the grove, because Meshy props passed there. A Tripo run is possible for the meshes if the designer prefers; the settings would then need re-costing.

Gameplay is a flat plane (F4): only the band behind the fighters needs detail. E1: the stage is 3,840 units (19.2 m) wide; the camera shows 1,060 to 1,540 units (up to about 1,900 in tall-body fights), so a 16:9 backdrop quad plus instanced props covers it, as in the grove.

Stage mood: a high mountain shrine gate in snowfall at blue dusk. Cold and quiet against Oni's red. The gate frames the fight without standing in the fighter band.

## Palette

Style palette (`style.md`): indigo `#2D3A5E` (sky and shadow side), rice paper `#F1E8D4` (snow lit side), shrine red `#B5332B` (gate), pine `#34483B` (snowy pines), ink `#1D1B21`, persimmon `#D8632C` (lantern glow only, tiny). **Confirmed 2026-10-20 and added to `style.md`:** snow shade blue-grey `#A9B4C8` for the shadow side of snow, so snow reads as 2-3 tone steps and not white on white. Do not use pure white.

## Lessons from the grove, applied
- Meshy 16:9 images come back with a grey letterbox border (grove: about x 110-1266, y 75-693 of 1376x768). Plan to crop; the prompt asks for "full-bleed to the edges, no border or frame" but the crop step stays in the assembly.
- Meshy props come with their pivot at the mesh centre (bbox about +-0.95 m). Re-pivot to the base and scale in Godot.
- The ground strip came back gritty and photographic. Its prompt here adds "flat vector cel shapes, no grit, no noise, seamless".
- Tri budget: props at or under about 3k (grove: 3,126 accepted at +4%).

## Prompts (after the style block)

Backdrop (S1, `meshy_text_to_image`, `nano-banana-pro`, `aspect_ratio: 16:9`):
> Wide stage backdrop, a snowbound mountain shrine gate at blue dusk seen from a low flat viewpoint, a large shrine-red gate standing in the centre-right distance with a thick straw rope across it, snow-laden dark pine trees on both sides, layered indigo mountain ridges fading into pale mist, soft large snowflakes in the air, a faint stone path buried in snow, empty and uncluttered in the lower third, no characters, no text, full-bleed to the image edges with no border or frame. Flat cel colours, soft ink-brush edges. Palette: indigo #2D3A5E, rice paper #F1E8D4, snow shade #A9B4C8, shrine red #B5332B, pine #34483B. Avoid: [negative prompt].

Snowy pine cluster (S2, `nano-banana`, 1:1):
> A cluster of three tall dark pine trees heavy with snow on the branches, isolated on plain white, flat cel shading, even lighting, snow in two tones of rice paper and pale blue-grey, no ground. Avoid: [negative prompt].

Snow-capped stone lantern (S4, `nano-banana`, 1:1):
> A small weathered stone shrine lantern with a thick cap of snow on its roof, on a plain white background, grey-indigo stone, simple blocky shape, a tiny warm persimmon glow in its window, flat cel shading. Avoid: [negative prompt].

Shrine gate (S6, `nano-banana`, 1:1):
> A large plain shrine gate with two round shrine-red pillars and two crossbeams, a thick straw rope hung across the top beam with a few paper streamers, a light dusting of snow on the top beam, isolated on plain white, front view, flat cel shading. Avoid: [negative prompt].

Ground strip (S8, `nano-banana`, 16:9):
> Top-down tileable strip of packed snow on a stone path, flat vector cel shapes, no grit, no noise, seamless left and right, two tones of rice paper and pale blue-grey with a few scattered pine needles, no shadows. Avoid: [negative prompt].

Snowflake sprite sheet (S9, optional VFX, `nano-banana`, 1:1):
> A 4 by 4 grid of simple soft round snowflake sprites in rice paper white on a plain flat indigo #2D3A5E background, varied sizes, flat shapes, no glow, no sparkles. Avoid: [negative prompt].
(Used as a Godot particle texture: tick-driven drift, deterministic. Optional: a plain white quad particle works without it.)

## Job chain

| # | Tool | Settings | Credits |
|---|---|---|---|
| S1 | `meshy_text_to_image` | backdrop, `nano-banana-pro`, 16:9 | 9 |
| S2 | `meshy_text_to_image` | pine cluster, `nano-banana`, 1:1 | 3 |
| S3 | `meshy_image_to_3d` | `input_task_id`: S2, `meshy-6-lite`, `target_polycount: 3000`, `should_remesh: true`, `texture_resolution: 2k`, `enable_pbr: false`, no pose_mode | 15 |
| S4 | `meshy_text_to_image` | lantern, `nano-banana`, 1:1 | 3 |
| S5 | `meshy_image_to_3d` | `input_task_id`: S4, same settings as S3, `target_polycount: 2500` | 15 |
| S6 | `meshy_text_to_image` | gate, `nano-banana`, 1:1 | 3 |
| S7 | `meshy_image_to_3d` | `input_task_id`: S6, same settings as S3 | 15 |
| S8 | `meshy_text_to_image` | ground strip, `nano-banana`, 16:9 | 3 |
| S9 | `meshy_text_to_image` | snowflake sprites, `nano-banana`, 1:1 (optional) | 3 |

| Cost | Credits |
|---|---|
| Core first pass (S1-S8) | **66** |
| With optional S9 | 69 |
| Per asset, if run singly | backdrop 9, pine 18, lantern 18, gate 18, ground 3, flakes 3 |

Take cap 3 per asset. Retake costs are the asset's own line. Run `meshy_check_balance` first; stop if short. Run only after approval.

## Assembly (Godot, not generation)
Backdrop on a quad well behind the fight plane (crop the letterbox). Gate mesh (S7) instanced once behind the fight at mid depth, off-centre, scaled so it frames the fight but its pillars stay outside the fighter band. Pine cluster instanced 5-8 times at midground depths, varied scale and rotation; snow lantern 1-2 times at the edges outside the fighter band. Ground strip tiled on the floor plane. Snowfall: GPUParticles with S9 (or plain quads), driven from the tick, no wall clock. Cold dusk lighting is a Godot light and colour setting, not baked.

## Acceptance criteria
- Palette matches (indigo, rice paper, snow shade, shrine red, pine); snow reads in at least 2 tones; no baked shadows; no characters or text; no letterbox after crop.
- Backdrop lower third calm and low-contrast, so Oni (red) and the second fighter read at 720p; checked by screenshot with both fighters placed, Oni on the gate side.
- Props: single clean mesh, at or under 3k tris (flag above 3.3k), one 2K texture, no PBR maps, origin noted (re-pivot to base in Godot), no floating geometry.
- Gate: two pillars and two beams, rope present, no text or kanji on any plaque.
- Ground strip tiles without a visible seam (checked by tiling 3x).
- Look consistent with Ryo, Kitsune and the grove once the outline pass is applied.
- Licence: plan tier unknown, flagged (AM5, YOK-115).

## Open questions for the designer
1. Snow shade `#A9B4C8` is proposed here, not in `style.md`: add it to the palette?
2. Snowfall particles: generate the S9 sprite sheet (3 credits) or use plain quads (0)?
3. Generator: Meshy as the grove (costed above), or Tripo for the meshes?

## Takes
None yet. Ledger: approved 66, spent 0, balance not checked.

