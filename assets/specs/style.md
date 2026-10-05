# Shared style block (V1)

Owner: asset-smith. Every prompt for a visual asset starts with this block. Change it here first, never per spec.

## Style block (paste at the start of every prompt)

> Stylised toon-shaded game art, flat cel colour in 2-3 tone steps, clean confident ink-brush outline of even weight, muted ukiyo-e colour palette, matte surfaces, soft even lighting with no cast shadows, no photographic or painterly texture, no gloss, no baked ambient occlusion, simple readable shapes.

## Palette (V1)

| Colour | Hex | Role |
|---|---|---|
| Indigo | `#2D3A5E` | Ryo coat/trousers, Kitsune inner top, night sky |
| Ink | `#1D1B21` | outlines, hair, Ryo energy |
| Persimmon | `#D8632C` | Ryo lining/rope, dusk glow |
| Rice paper | `#F1E8D4` | ofuda, wraps, UI paper |
| Pine | `#34483B` | bamboo, stage mid-tones |
| Fur white | `#FBF6EC` | Kitsune |
| Foxfire | `#F2A23A` | Kitsune accents, fire |
| Shrine red | `#B5332B` | seals, hakama, trim |
| Brass | `#B08A48` | buttons |

## Negative prompt (append as "Avoid: ...")

Avoid: photorealism, PBR detail, metallic sheen, painterly brush noise, film grain, text, watermark, logo, signature, extra limbs, extra fingers, held props or effects not in the reference, glowing particles, cast shadows, busy background, any resemblance to an existing franchise, artist or character.

## Fighter turnaround rules

- White or flat light-grey background, full body, T-pose (arms straight out; designer decision), feet apart, no effects, no held items. Kitsune's mask stays on.
- Front, side and back views at the same scale and height. Only the chosen front view goes to Image-to-3D.

## Shared 3D settings

- Image-to-3D: `ai_model: meshy-7.1`, `model_type: standard`, `should_texture: true`, `enable_pbr: false`, `texture_resolution: 2k`, `should_remesh: true`, `topology: triangle`, `target_polycount` per spec, `pose_mode: t-pose` (designer decision for YOK-30; references are A-pose), `image_enhancement: false` (preserve flat reference styling), `target_formats: ["glb"]`.
- Colour only in the base texture. Godot applies the shared toon shader and outline pass; any baked lighting or shadow in the texture is a RETAKE reason.

## Acceptance checklist (used by every spec)

1. Look (V1): palette within tolerance of the table, flat tones, no baked PBR or painterly noise, silhouette reads at fighting distance and 720p.
2. Consistency with already accepted fighters (proportion, outline weight, palette).
3. Rig: glTF imports in Godot 4, humanoid skeleton intact, scale and up-axis right, origin at feet.
4. G4 readiness: clearance for walk, heavy and throw; risk recorded for clip-matcher.
5. Licence: commercial use cleared on the designer's plan (currently UNKNOWN, see open question in rules.md).
