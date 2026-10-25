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

## Retake R1: lit lantern path to a river glimpse (YOK-209) - SUPERSEDED by R1b

**Superseded by R1b (below), 2026-10-25.** Reason: YOK-208 measured the scene. The 3D floor meets the backdrop quad at about 0.80 H of the cropped art, and the camera horizon falls at 0.72-0.74 H. R1 put the vanishing point at 0.52 H and painted lanterns at 0.54-0.62 H, so in game they would float 3.5-5 m above the floor and fight the 3D lantern line YOK-208 now builds. R1's text is kept for the record; its Placement, Prompt and placement acceptance criteria are replaced by R1b. R1's intent, water colour and stage-distinction table still hold.

Original status: **PROPOSED, not approved, nothing run, 0 credits spent.** Designer request 2026-10-25: "in the background of the bamboo scene can we make the lanterns appear lit and possibly lead to a river". Replaces S1 only; props S2-S6 stay. Take cap 3 for the backdrop (take 1 above is used for the original; the cap counts retakes of this request, as for YOK-151).

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
None run (superseded by R1b). Balance not checked.

## Retake R1b: low horizon, no painted lanterns (YOK-209)

Status: **PROPOSED, not approved, nothing run, 0 credits spent.** Supersedes R1. The designer approved R1 at 9 credits per take, cap 27; the prompt change needs approval again. Same job chain and cost. Replaces S1 only; props S2-S6 stay.

### What changed from R1, and why
| | R1 | R1b | Why |
|---|---|---|---|
| Vanishing point | 0.50 W, 0.52 H | **0.53 W, 0.74 H** | YOK-208: floor seam at 0.80 H of the art, camera horizon 0.72-0.74 H, 3D lantern vanishing point about 0.53 W on the seam |
| River glimpse | 0.46-0.56 W, 0.46-0.52 H | **0.47-0.59 W, 0.70-0.77 H** | Must sit just above the seam to be visible and meet the path end |
| Painted lanterns | 4-5 per side at 0.54-0.62 H | **none** | The 3D line supplies them; painted ones would float and double up |
| Painted path | winds up from the bottom edge | **short pale strip at 0.75-0.80 H** | Below 0.80 H is under the floor; the path only shows where it meets the river |
| Bottom fifth | calm lower third (0.67-1.0 H) | **plain ground below 0.80 H, continuing the floor tone** | Hidden anyway; flat earth avoids a colour jump at the seam |
| Sky glow | centred 0.50 W | centred 0.53 W, about 0.30-0.66 H | Follows the new axis |

Kept from R1: dusk palette, indigo water and not teal (`#3E7F86` is Kappa's), banned list, full-bleed with no letterbox, grove identity and the stage-distinction table, calm area behind the fighters.

### Placement (fractions of the cropped 16:9 art; re-measure on the take)
- Vanishing point: **0.53 W, 0.74 H** (tolerance 0.04 W, 0.03 H). Path far end and river meet here.
- River glimpse: narrow horizontal sliver, **x 0.47-0.59 W, y 0.70-0.77 H**, framed by bamboo trunks on both sides. About 0.8% of the frame (limit 10%). Flat indigo `#2D3A5E` with two or three flat persimmon reflection bands.
- Path: pale stone strip at **0.75-0.80 H**, widening slightly downward from 0.53 W, ending at 0.80 H.
- Bamboo and mist: trunk bases at about 0.76-0.80 H; mist fades to indigo behind the river.
- Afterglow: persimmon centred near 0.53 W, 0.30-0.66 H, above the river.
- Below 0.80 H: plain flat packed-earth tone matching the floor (low contrast, no path, water, lanterns or leaves).
- Painted lanterns: none. Optional exception if the designer wants a distant hint: at most two tiny dim ones per side within 0.08 W of the vanishing point at 0.72-0.76 H, under 0.02 W wide. Why to avoid even these: the 3D line ends at the same vanishing point, so any painted lantern there must align within a few pixels or it reads as a double. Recommended: none.

### Prompt (after the style block)
Backdrop R1b (`meshy_text_to_image`, `nano-banana-pro`, `aspect_ratio: 16:9`):
> Wide stage backdrop, a bamboo grove at dusk seen from a very low flat viewpoint with a very low horizon, the ground fills only the bottom fifth of the image as plain flat packed earth with no path and no detail, dense pine-green bamboo stalks in layered depth standing on that ground and fading into indigo mist, centred just right of the middle a short pale stone path meets a small narrow glimpse of a calm river in muted indigo with flat persimmon reflection bands, the river sits just above the bottom fifth of the image and takes under one percent of the frame, bamboo trunks frame the river on both sides, a large persimmon-orange dusk afterglow fills the sky above the river between the stalks, the centre of the frame calm and uncluttered, no lanterns, no characters, no text, no stars, no paper lanterns, full-bleed to the image edges with no border, frame or letterbox. Flat cel colours, soft ink-brush edges. Palette: pine #34483B, indigo #2D3A5E, persimmon #D8632C, rice paper #F1E8D4, earth #7A4A3A. Avoid: [negative prompt], teal water, bright blue sky, bridge, boats, lanterns of any kind.

How the wording gets there: "very low horizon" and "ground fills only the bottom fifth" push the horizon to about 0.75-0.80 H; "river sits just above the bottom fifth" fixes the glimpse vertically; "centred just right of the middle" gives about 0.53 W; "no lanterns" plus the avoid list stops painted ones. The earth hex `#7A4A3A` is a placeholder: replace it with the colour sampled from the floor in the YOK-208 capture before the run. Text-to-image seldom hits fractions exactly, so the acceptance check measures, and the quad nudge below absorbs small misses.

Image-to-image from the old backdrop is not recommended: its high horizon and layout would anchor the result.

### Option: re-place the quad in Godot instead of changing the take
Keep R1 as written and scale/move the quad so the painted horizon (0.52 H) lands on the floor seam. That needs the quad about 1.5x larger (0.80 / 0.52) and crops the sides to the central 65% of the width. Costs: the 1376x768 art is enlarged 1.5x (softer, bamboo detail cropped away), the painted river and lanterns still land behind the 3D line, and bamboo prop depths need retuning. The gameplay-programmer's view: either works.

**Recommendation: R1b (change the take), with a free quad nudge as the rescue.** No resolution loss, no scene rework, the same 9 credits per take, and the art is made for the camera it is seen through. A nudge costs 0 credits, so a take with the right look that misses the fractions by up to about 0.05 H is accepted and the gameplay-programmer shifts the quad instead of the designer spending another take. Fall back to full re-placement only if all three takes miss badly.

### Job chain and cost (unchanged from R1)
| # | Tool | Settings | Credits |
|---|---|---|---|
| R1b | `meshy_text_to_image` | backdrop R1b, `nano-banana-pro`, 16:9 | 9 |

Recommended: take 1, 9 credits. Worst case, three takes: 27 (the designer's cap). Props, rigs, clips: 0. Run only after re-approval; `meshy_check_balance` first; log task ids under Takes (R1b).

### Acceptance criteria (checked after each take, written under `## Acceptance`)
- Full-bleed: no grey letterbox or border; check edge pixels of all four sides. Otherwise crop, then measure fractions on the cropped art.
- Vanishing point within 0.04 W and 0.03 H of 0.53 W, 0.74 H; river inside x 0.47-0.59 W, y 0.70-0.77 H (0.03 tolerance), under 10% of the frame.
- No lanterns of any kind (or, if the exception is kept, only tiny and dim inside the stated box); no paper lanterns, stars, bridge, boats, characters or text.
- Below 0.80 H plain ground with no path, water or objects; tone within a small step of the floor colour at the seam.
- Palette within tolerance; water indigo, no teal; flat colours, no bloom, no painterly noise.
- In-game check against the YOK-208 capture (`docs/screenshots/current/stage/mid-range.png`): the 3D lantern rows converge on the painted vanishing point and river, nothing floats, fighters read at 720p over the calm centre.
- Reads as the grove, not the riverbank (table above).
- Licence: plan tier unknown, flagged.

### Takes (R1b)
None run. Balance not checked.
