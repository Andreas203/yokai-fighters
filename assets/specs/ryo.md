# Ryo (fighter) - generation spec

Ticket: YOK-29 (feeds YOK-39 slice). Status: SPEC, awaiting designer approval. Nothing run.
Reference: `docs/design/characters/ryo/ryo-meshy-front.png` (1024x2112, A-pose). Sheet: `docs/design/characters/ryo/README.md`.

## Prompt (after the style block in `assets/specs/style.md`)

Turnaround (image-to-image, reference = ryo-meshy-front.png):
> Character turnaround sheet of the same young exorcist: front view, side view and back view, same height and scale, A-pose, plain white background. Indigo coat with spiral brass buttons closed left over right, long coat tail with persimmon lining, high indigo collar over the lower face, black undercut hair with one lock over the right eye, shimenawa rope sash with paper shide and a small pouch, indigo trousers, rice-paper shin wraps, black split-toe shoes, vermilion mark under the left eye, four rice-paper talismans wrapped on the right forearm. Palette: indigo #2D3A5E, ink #1D1B21, persimmon #D8632C, rice paper #F1E8D4, brass #B08A48. No held items, no energy effects, no extra talisman strips. Avoid: [negative prompt from style.md].

Image-to-3D: the selected front view only. No texture_prompt.

## Job chain

| # | Tool | Settings | Credits |
|---|---|---|---|
| R1 | `meshy_image_to_image` | `ai_model: nano-banana-pro`, `reference_file_paths: [ryo-meshy-front.png]`, `generate_multi_view: true` (front/side/back in one call; if views are inconsistent, retake as 3 single calls at 9 each) | 9 |
| R2 | `meshy_image_to_3d` | `ai_model: meshy-7.1`, `file_path`: chosen front view, `model_type: standard`, `pose_mode: a-pose`, `should_remesh: true`, `topology: triangle`, `target_polycount: 15000`, `should_texture: true`, `texture_resolution: 2k`, `enable_pbr: false`, `image_enhancement: false`, `origin_at: bottom`, `auto_size: true` (~1.75 m), `target_formats: ["glb"]` | 30 |
| R3 | `meshy_rig` | `input_task_id`: R2, `height_meters: 1.75`. Includes free walk + run clips | 5 |

Per-take total: 44. Take cap 3 (whole chain, or any subset the designer approves, e.g. R2+R3 = 35). Download with `meshy_download_model` (glb) into `game/`.

## Rig plan (Meshy humanoid skeleton)

| Part | Driven by | Method |
|---|---|---|
| Body, head, arms, legs | Meshy humanoid skeleton | shared clips |
| Coat tail (long side) | skeleton | baked rigid to hips/spine. If it clips legs in walk, heavy or throw, add 2 extra bones with sway computed from the tick count (deterministic, no physics) |
| Shimenawa rope, shide, pouch | skeleton | baked rigid to hips |
| Forearm ofuda wraps | skeleton | baked; no per-slot glow in the demo (sheet open question 2) |
| Collar | skeleton | baked; mouth stays covered (sheet open question 1) |

Hair is baked to the head. No secondary-motion physics anywhere (F3, determinism).

## Acceptance criteria

- Palette matches; coat tail, collar, binding mark and four forearm ofuda legible; no stray talisman strip or held ofuda.
- Silhouette reads lean and wide-stance at 720p; hands separate from the torso.
- <= ~15k triangles; one 2K texture; no PBR maps.
- Rig imports in Godot 4, skeleton intact, 1.75 m, up-axis Y, origin at feet.
- G4 readiness: coat-tail clearance for walk, heavy and throw recorded for clip-matcher.
- Licence: plan tier unknown, flagged.

## Takes
(none run)

## Acceptance
(not yet run)
