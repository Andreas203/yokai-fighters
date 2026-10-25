# Bamboo grove at dusk (V9) - stage generation spec

Ticket: YOK-29. Status: APPROVED first pass (YOK-30); retakes need new approval.
Gameplay is a flat plane (F4): only the band behind the fighters needs detail. Layers back to front: backdrop image, midground bamboo props, lantern props, ground strip.

## Prompts (after the style block)

Backdrop (`meshy_text_to_image`, `nano-banana-pro`, `aspect_ratio: 16:9`):
> Wide stage backdrop, a bamboo grove at dusk seen from a low flat viewpoint, dense pine-green bamboo stalks in layered depth fading into indigo mist, persimmon-orange afterglow low in the sky between the stalks, a faint stone path, empty and uncluttered in the lower third, no characters, no text. Flat cel colours, soft ink-brush edges. Palette: pine #34483B, indigo #2D3A5E, persimmon #D8632C, rice paper #F1E8D4. Avoid: [negative prompt].

Bamboo cluster (`nano-banana`, 1:1):
> A cluster of five tall pine-green bamboo stalks with leaf tufts, isolated on plain white, flat cel shading, even lighting. Avoid: [negative prompt].

Stone lantern (`nano-banana`, 1:1):
> A small weathered stone garden lantern on a plain white background, grey-indigo stone with moss-green top, simple blocky shape, flat cel shading. Avoid: [negative prompt].

Ground strip (`nano-banana`, 16:9):
> Top-down tileable strip of packed earth with scattered bamboo leaves, muted brown and pine green, flat colours, no shadows. Avoid: [negative prompt].

## Job chain

| # | Tool | Settings | Credits |
|---|---|---|---|
| S1 | `meshy_text_to_image` | backdrop, `nano-banana-pro`, 16:9 | 9 |
| S2 | `meshy_text_to_image` | bamboo cluster, `nano-banana`, 1:1 | 3 |
| S3 | `meshy_image_to_3d` | `input_task_id`: S2, `meshy-6-lite`, `target_polycount: 3000`, `should_remesh: true`, `texture_resolution: 2k`, `enable_pbr: false`, no pose_mode | 15 |
| S4 | `meshy_text_to_image` | lantern, `nano-banana`, 1:1 | 3 |
| S5 | `meshy_image_to_3d` | `input_task_id`: S4, same settings as S3 | 15 |
| S6 | `meshy_text_to_image` | ground strip, `nano-banana`, 16:9 | 3 |

First pass: 48. Take cap 3 per asset.

## Assembly (Godot, not generation)

Backdrop on a quad well behind the fight plane; bamboo cluster instanced 5-8 times at midground depths with varied scale and rotation; lantern 1-2 times at the edges, outside the fighter band; ground strip tiled on the floor plane. Dusk lighting is a Godot light/colour setting, not baked.

## Acceptance criteria

- Palette matches (pine, indigo, persimmon); no baked shadows; no characters or text.
- Backdrop lower third calm, so fighters read at 720p; checked by screenshot with both fighters placed.
- Props: single clean mesh, <= 3k tris, origin at base, no floating geometry.
- Ground strip tiles without a visible seam.
- Look consistent with Ryo and Kitsune once the outline pass is applied.
- Licence: plan tier unknown, flagged.

## Takes
Take 1, 2026-10-05 (approved first pass).

| Job | Task id | Credits (est/actual) |
|---|---|---|
| S1 backdrop | 01a10e15-8773-723d-b136-1d23063c0abb | 9/9 |
| S2 bamboo image | 01a10e15-8892-71a5-ba90-3fce2857b258 | 3/3 |
| S3 bamboo mesh | 01a10e18-6da0-7768-890d-83095f28f92b | 15/15 |
| S4 lantern image | 01a10e15-8e05-75b8-abda-39423e71b9bd | 3/3 |
| S5 lantern mesh | 01a10e18-7297-74ea-99c7-564eeba84286 | 15/15 |
| S6 ground strip | 01a10e15-91b3-7043-817f-24196649546d | 3/3 |

Total 48/48. Outputs in `game/assets/generated/stage/bamboo-grove/`: `backdrop.png`, `bamboo-ref.png`, `bamboo-cluster.glb`, `lantern-ref.png`, `lantern.glb`, `ground-strip.png`.

## Acceptance
- S1 backdrop: **PASS after crop**. Palette right (pine, indigo mist, persimmon glow), calm lower third with a stone path. The image has a grey letterbox border around the 16:9 art (about x 110-1266, y 75-693 of 1376x768); crop before use.
- S2/S3 bamboo cluster: **PASS**. 3,126 tris (spec <= 3k, 4% over, accepted), one mesh, one 2K texture. Origin is the mesh centre (bbox ~+-0.95 m), not the base: re-pivot and scale in Godot.
- S4/S5 lantern: **PASS**. 2,788 tris, same pivot note.
- S6 ground strip: **RETAKE recommended (3 credits, not approved)**: gritty photographic texture, off-style against the flat cel look; grey letterbox border (art about x 103-1240, y 173-595); tiling seam unverified. Suggested prompt change: add "flat vector cel shapes, no grit, no noise, seamless". Usable as a placeholder after crop.
- Files under `game/assets/generated/`. Licence: Meshy plan tier and commercial terms of generated output are NOT verifiable from the MCP (balance call only); UNKNOWN, designer to confirm on the plan before shipping.

## Retake R1: lit lantern path to a river glimpse (YOK-209)

Status: **PROPOSED, not approved, nothing run, 0 credits spent.** Designer request 2026-10-25: "in the background of the bamboo scene can we make the lanterns appear lit and possibly lead to a river". Replaces S1 only; props S2-S6 stay. Take cap 3 for the backdrop (take 1 above is used for the original; the cap counts retakes of this request, as for YOK-151).

### Intent
Same stage identity (V9, Kitsune's bamboo grove at dusk). Added: a stone path winding back through the bamboo, lined with small stone lanterns lit persimmon, ending at a small far glimpse of a river at dusk.

### Water colour
Muted indigo `#2D3A5E` with persimmon afterglow reflected in flat bands, NOT water teal `#3E7F86`. Why: teal is Kappa's colour (V5 spark, riverbank water), so teal here would make the grove read as the riverbank stage. Indigo water also stays inside the grove's own palette (pine, indigo, persimmon, rice paper).

### How the two stages stay distinct
| | Bamboo grove (this) | Lantern riverbank (YOK-151) |
|---|---|---|
| Subject | Bamboo, path, river a far small glimpse | River is the whole band behind the fighters |
| Lanterns | Fixed square stone lanterns on the path, small persimmon window | Round paper lanterns floating on the water |
| Light | Dusk afterglow: persimmon low in the sky, pine and indigo mist | Deep indigo night with stars |
| Water | Muted indigo, under 10% of the frame | Teal `#3E7F86`, most of the frame |
| Ground | Stone path through bamboo | Bank, reeds, mooring post |
Rule for the take: no floating or round paper lanterns, no teal, no stars, no bridge, no reeds.

### Placement (for the YOK-208 3D lanterns)
Fractions of image width and height, 16:9 full frame (the quad shows the backdrop whole; if the camera crops it, re-measure on the take).
- Path: enters bottom edge at about 0.40-0.60 W (calm lower third, kept plain), winds slightly right then left, narrows to a vanishing point at about 0.50 W, 0.52 H (horizon).
- Painted lanterns: 4-5 per side, pairs spaced evenly along the path edges, nearest pair about 0.30 W and 0.70 W at about 0.62 H, farthest pair at about 0.47 W and 0.53 W at 0.54 H. Small and in the upper-middle band so they never sit under the fighters. The real 3D lanterns stand in front, so painted ones should be smaller and fainter than the near ones.
- River glimpse: a small horizontal sliver centred about 0.46-0.56 W, between 0.46 and 0.52 H, at the end of the path, framed by bamboo on both sides. Under 10% of the frame area.
- Persimmon afterglow sky stays above the river, centred near 0.50 W.
- Lower third (below 0.67 H): calm, low contrast, no lanterns, no water.

### Prompt (after the style block)
Backdrop R1 (`meshy_text_to_image`, `nano-banana-pro`, `aspect_ratio: 16:9`):
> Wide stage backdrop, a bamboo grove at dusk seen from a low flat viewpoint, dense pine-green bamboo stalks in layered depth fading into indigo mist, a pale stone path entering at the bottom centre and winding back through the bamboo to a vanishing point at the middle of the frame, small square stone garden lanterns evenly spaced on both sides of the path with their windows glowing flat persimmon orange, the lanterns getting smaller with distance, at the far end of the path a small narrow glimpse of a calm river in muted indigo with flat persimmon reflection bands, persimmon-orange dusk afterglow low in the sky above the river between the stalks, empty calm and uncluttered in the lower third, no characters, no text, no stars, no paper lanterns, full-bleed to the image edges with no border, frame or letterbox. Flat cel colours, soft ink-brush edges, lantern glow as a flat orange fill with no bloom or halo. Palette: pine #34483B, indigo #2D3A5E, persimmon #D8632C, rice paper #F1E8D4. Avoid: [negative prompt], teal water, bright blue sky, bridge, boats.

Alternative A2 (`meshy_image_to_image`, `nano-banana-pro`, reference `game/assets/generated/stage/bamboo-grove/backdrop.png` cropped of its grey border first, reference strength per tool default, 16:9): same prompt prefixed with "Keep the bamboo layering, palette and composition of the reference; add" and the lantern, path and river clauses above. Keeps the accepted look. Risk: the old letterbox or its faint path may carry over, and the path rarely moves far from the reference.

### Job chain and cost
| # | Tool | Settings | Credits |
|---|---|---|---|
| R1 | `meshy_text_to_image` | backdrop R1, `nano-banana-pro`, 16:9 | 9 |
| A2 | `meshy_image_to_image` | alternative, `nano-banana-pro`, 16:9, reference = cropped current backdrop | 9 (assumed same tier as R1; verify against the tool's published cost before running) |

| Plan | Credits |
|---|---|
| Recommended: R1 take 1 | 9 |
| Worst case, R1 three takes (cap) | 27 |
| Optional A2 one take on top | +9 |
Grove spend precedent: S1 cost 9 per take and "Retakes need a new approval" (docs/decision-log.md, YOK-30 first pass 163 credits; running stage total cap 195 across YOK-150/151/152 does not apply to this retake, which is new work, so the designer sets its own cap). Props, rigs and clips: 0.

### Recommendation
R1 (text-to-image). The composition changes substantially (path, lanterns, river); image-to-image would anchor to the old layout. Run A2 only if R1 take 1 loses the grove's look.

### Acceptance criteria (checked after each take, written under `## Acceptance`)
- Full-bleed: no grey letterbox or border; check edge pixels of all four sides. Otherwise crop as in take 1.
- Palette within tolerance; lantern glow flat orange with no bloom; water indigo, no teal; no stars, paper lanterns, bridge, characters or text.
- Path and lanterns sit within the placement fractions above (about 0.05 W tolerance); river under 10% of frame.
- Lower third calm; checked by screenshot at 720p with both fighters and the YOK-208 lanterns in place.
- Reads as the grove, not the riverbank (table above); toon shader and ink outline hold up, no painterly noise.
- Licence: plan tier unknown, flagged.

### Takes (R1)
None run. Balance not checked.
