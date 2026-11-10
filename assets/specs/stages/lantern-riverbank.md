# Lantern riverbank (V9, Kappa's stage) - stage generation spec

Ticket: YOK-151. Status: **APPROVED first pass, S1-S9, 69 credits.** Approved by the designer 2026-10-20 (recorded on the Linear ticket): S1-S9, 69 credits. Bank lanterns stay an open question; spec default kept (lanterns on the water only). Generator: Meshy for everything. **Not yet run: the Meshy MCP tools were not available in the session that was asked to run it, so no balance check was made and no credits were spent.** Take cap 3 per asset; running total across YOK-150/151/152 must not exceed 195. Method: `assets/specs/stages/bamboo-grove-dusk.md`, style block `assets/specs/style.md`. Generator: Meshy MCP, as the grove (a Tripo run for the meshes is possible if the designer prefers; it would need re-costing).

**2026-11-09 generator switch (designer): the Meshy approval above never ran and is replaced.** Backdrop: the style loop's Astra take 2, picked by the designer (no new spend). Props: Tripo H3.1 text-to-model, `smart_low_poly=true`, 30 credits each, take cap 1, the spec prompts below adapted to text-to-model. No ground/water strips generated: floors are Godot flat-colour materials. See Takes.

**2026-11-09 swamp retake (designer: "have it resemble a swamp more without the lanterns"):** the stage becomes Kappa's night swamp. Four new Astra backdrop takes were made from a swamp brief, and the designer picked take 4. All the floating lanterns, painted and 3D, are gone, and the water is murky swamp green. The lantern prompts and the lantern acceptance lines below are history. The S4/S5 lantern mesh is no longer used.

Gameplay is a flat plane (F4): the fighters stand on the bank, the river and its lanterns are the band behind them. E1: stage 3,840 units (19.2 m) wide, camera shows 1,060 to about 1,900 units.

Stage mood: a calm river at deep indigo night, paper lanterns drifting on the water and a few on the bank, reeds in the foreground edge. Warm persimmon points against cool indigo, with Kappa's teal in the water.

## Palette
Style palette: indigo `#2D3A5E` (night sky, water shade), ink `#1D1B21`, persimmon `#D8632C` (lantern glow), rice paper `#F1E8D4` (lantern paper), pine `#34483B` (reeds, far bank), brass `#B08A48` (lantern rims). **Confirmed 2026-10-20 and added to `style.md` (one colour with Kappa's spark teal):** water teal `#3E7F86`, which should match Kappa's teal hit spark (V5; that hex is also not yet in the style table). Keep the lantern glow a flat orange fill, never a bloom.

## Lessons from the grove, applied
- Crop the grey letterbox on Meshy 16:9 outputs (grove: about x 110-1266, y 75-693 of 1376x768). Prompts ask for full-bleed.
- Meshy props pivot at the mesh centre: re-pivot to the base (or, for floating lanterns, to the waterline) in Godot.
- Strips come back gritty unless told "flat vector cel shapes, no grit, no noise, seamless".
- Props at or under about 3k tris; small props lower.

## Prompts (after the style block)

Backdrop (S1, `meshy_text_to_image`, `nano-banana-pro`, `aspect_ratio: 16:9`):
> Wide stage backdrop, a calm river at night seen from a low flat viewpoint, dozens of small round paper lanterns glowing persimmon orange drifting on dark indigo water in layered depth, a far bank of pine-green reeds and low hills fading into indigo mist, a small arched wooden footbridge far in the distance, a few rice-paper-lit stars, empty and uncluttered in the lower third, no characters, no text, full-bleed to the image edges with no border or frame. Flat cel colours, soft ink-brush edges, water as flat bands. Palette: indigo #2D3A5E, persimmon #D8632C, water teal #3E7F86, pine #34483B, rice paper #F1E8D4. Avoid: [negative prompt].

Reed cluster (S2, `nano-banana`, 1:1):
> A clump of tall pine-green river reeds with brown seed heads, isolated on plain white, flat cel shading, even lighting, no ground. Avoid: [negative prompt].

Floating paper lantern (S4, `nano-banana`, 1:1):
> A single small round floating paper lantern on a small flat wooden base, rice paper body glowing persimmon orange from inside, thin ink ribs, brass rim, isolated on plain white, flat cel shading, no flame, no glow halo. Avoid: [negative prompt].

Mooring post (S6, `nano-banana`, 1:1):
> A short weathered wooden mooring post with a coil of straw rope around it, isolated on plain white, dark brown wood with indigo shadow tones, simple blocky shape, flat cel shading. Avoid: [negative prompt].

Bank ground strip (S8, `nano-banana`, 16:9):
> Top-down tileable strip of a river bank, packed earth and flat pebbles with a few reed leaves, muted brown and pine green, flat vector cel shapes, no grit, no noise, seamless left and right, no shadows. Avoid: [negative prompt].

Water strip (S9, `nano-banana`, 16:9):
> Top-down tileable strip of calm river water, flat horizontal bands of indigo and water teal with a few thin pale ripple lines, flat vector cel shapes, no grit, no reflections, seamless left and right and top to bottom. Avoid: [negative prompt].

## Job chain

| # | Tool | Settings | Credits |
|---|---|---|---|
| S1 | `meshy_text_to_image` | backdrop, `nano-banana-pro`, 16:9 | 9 |
| S2 | `meshy_text_to_image` | reeds, `nano-banana`, 1:1 | 3 |
| S3 | `meshy_image_to_3d` | `input_task_id`: S2, `meshy-6-lite`, `target_polycount: 3000`, `should_remesh: true`, `texture_resolution: 2k`, `enable_pbr: false`, no pose_mode | 15 |
| S4 | `meshy_text_to_image` | floating lantern, `nano-banana`, 1:1 | 3 |
| S5 | `meshy_image_to_3d` | `input_task_id`: S4, same settings as S3, `target_polycount: 1500` | 15 |
| S6 | `meshy_text_to_image` | mooring post, `nano-banana`, 1:1 | 3 |
| S7 | `meshy_image_to_3d` | `input_task_id`: S6, same settings as S3, `target_polycount: 1500` | 15 |
| S8 | `meshy_text_to_image` | bank ground strip, `nano-banana`, 16:9 | 3 |
| S9 | `meshy_text_to_image` | water strip, `nano-banana`, 16:9 | 3 |

| Cost | Credits |
|---|---|
| First pass (S1-S9) | **69** |
| Per asset, if run singly | backdrop 9, reeds 18, lantern 18, post 18, bank strip 3, water strip 3 |

Take cap 3 per asset. Run `meshy_check_balance` first. Run only after approval.

## Assembly (Godot, not generation)
Backdrop on a quad well behind the fight plane (crop the letterbox). Water strip on a plane between the bank and the backdrop. The floating lantern (S5) instanced 15-30 times on the water plane with varied scale, spread in depth; each bobs and drifts with `offset = A * sin(tick * w + phase_i)` from the sim tick (deterministic, no physics, no wall clock). Reeds instanced 5-8 times at the bank edge and the far side; mooring post 1-2 times at the edges, outside the fighter band. Bank strip tiled on the floor plane. Night lighting is a Godot setting; the lantern glow is an emissive flat colour, not a baked light.

## Acceptance criteria
- Palette matches (indigo, persimmon, teal, pine, rice paper); no baked shadows or glow halos; no characters or text; no letterbox after crop.
- Backdrop lower third calm, so Kappa (teal shell, sheet pending) reads against it at 720p; checked by screenshot with both fighters placed. Check Kappa's teal does not vanish into the water teal.
- Props: single clean mesh, at or under the tri target (flag +10%), one 2K texture, no PBR maps, no floating geometry; lantern origin noted for re-pivot to the waterline.
- Bank and water strips tile without a visible seam (checked by tiling 3x).
- Look consistent with Ryo, Kitsune and the grove once the outline pass is applied.
- Licence: plan tier unknown, flagged (AM5, YOK-115).

## Open questions for the designer
1. Water teal `#3E7F86` proposed: add it, and Kappa's spark teal, to `style.md` as one colour?
2. Lanterns only on the water, or also a few on the bank (more props, same S5 mesh, no extra cost)?
3. Generator: Meshy as the grove (costed above), or Tripo for the meshes?

## Takes
Take 1, 2026-11-09 (designer approved the switch, the backdrop pick and 210 Tripo credits across the three stages).

| Job | Source | Task id | Credits |
|---|---|---|---|
| S1 backdrop | Astra take 2 of the style loop, `tools/style_agent/runs/visual/images/visual-2-palette-lore-take2.png` | (Codex) | 0 new |
| S2/S3 reeds | Tripo H3.1 text-to-model, smart_low_poly, face_limit 3000 (prompt matched to the backdrop's pale plumes) | 229a0388-9354-4a48-aabb-19fb37ca8c8b | 30 |
| S4/S5 floating lantern | Tripo H3.1, face_limit 1500 | b3ebd5d7-1f33-48c8-ab15-b2dd75abc519 | 30 |
| S6/S7 mooring post | Tripo H3.1, face_limit 1500 | d6f2be10-8ab5-4741-954d-c17056606c0a | 30 |
| S8 bank strip, S9 water strip | not made: flat-colour bank material and water plane in Godot | - | 0 |

Total 90 Tripo credits. Outputs in `game/assets/generated/stage/riverbank/`: `backdrop.png`, `reeds.glb`, `floating-lantern.glb`, `mooring-post.glb`, `*-ref.png`.

Take 2, 2026-11-09: swamp retake (designer asked for retakes; 4 Astra takes, same batch size as the season screens).

| Job | Source | Session id | Cost |
|---|---|---|---|
| S1 backdrop, swamp take 1: dead moss trees, mist | Astra (gpt-6-astra via `codex exec`), refs: grove crop + shrine take 2 | 01a1c171-771c-7ae0-8709-f8d4da1e14c5 | 1 take |
| take 2: weeping willows, lotus | same | 01a1c171-8a08-7ce2-a682-5f514eddd034 | 1 take |
| take 3: open marsh, dead trunks | same | 01a1c171-771f-7d62-a177-5af8448a0e96 | 1 take |
| **take 4: root trees, mossy logs, mist (picked)** | same | 01a1c171-771c-74f1-91cd-20f424ddce31 | 1 take |

Prompts, the shared base and negative blocks, all four takes and the contact sheet are in `tools/style_agent/runs/kappa-swamp/`. The brief composes the painting as a far view only, with a level waterline at about 80% of the height and nothing below it but flat water. That way the game can use the painting nearly whole, at a sharp scale, instead of the old top third. Codex does not report Astra's credit cost.

## Acceptance
- S1 swamp backdrop (take 4): **PASS (designer pick)**.
  - 1672x941, full-bleed, no lanterns or light sources.
  - Twisted root trees with hanging moss frame both edges; a far treeline, flat mist bands, and mossy logs and a stump on the far bank.
  - Level waterline at about row 776 (v 0.825). Below it is flat swamp water, about #2B4140, with faint ripple dashes.
  - A few red autumn leaves (persimmon family) on the edge trees.
  - Installed as `game/assets/generated/stage/riverbank/backdrop.png`.
- S1 backdrop, first pass (superseded by the swamp retake): **PASS (designer pick)**. 1280x720, full-bleed. Round paper lanterns on teal water (#3E7F86), reeds framing both edges, indigo night hills; flat bank earth in the lower third. The lanterns have a soft graded glow and flat reflections, no halo; guide v2 L2 allows that, v1 docked it (8/10).
- Reeds: **PASS with WARN**. 3,743 tris against 3,000 (+25%).
- Floating lantern: **PASS with WARN**. 2,022 tris against 1,500 (+35%). Has a small top handle; the paper is pale, so it gets the flat persimmon emissive fill in the scene. Re-pivot to the waterline.
- Mooring post: **PASS with WARN**. 2,475 tris against 1,500 (+65%).
- Tripo props come normalised to a 1-unit box with the origin at the centre: re-pivot to the base and scale in Godot. Triangle counts run over target because `smart_low_poly` treats `face_limit` loosely; for background props that's no performance issue (WARN, not a retake). A Blender decimate by script is the free fix if wanted.
- Licence: unconfirmed (AM5, YOK-115).
