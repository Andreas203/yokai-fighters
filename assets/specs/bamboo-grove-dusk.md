# Bamboo grove at dusk (V9) - stage generation spec

Ticket: YOK-29. Status: SPEC, awaiting designer approval. Nothing run.
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
(none run)

## Acceptance
(not yet run)
