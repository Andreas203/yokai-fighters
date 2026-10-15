# Ryo regeneration and re-rig (spec only)

Ticket: YOK-116. Status: **SPEC. No job run, no credits spent.** Designer approves the job and its cost afterwards. Take 2 in `assets/specs/ryo.md` stays the live model until a take here is accepted.
Inputs read: `assets/specs/ryo.md` (takes 1 and 2), `docs/decision-log.md` (YOK-30 open points: Ryo's simplified outfit, retakes need new approval), `docs/design/characters/ryo/README.md`, `assets/specs/rigs/skeleton.md` (YOK-111, on its own branch), and the `tools/blender/rig_check.py` report and stills from the YOK-110 run on `ryo-rigged.glb` (rig task `01a10e47-857e-73a0-9f7a-cd74795198fb`).

## 1. What the rig check found

| Pose | Verdict | Detail |
|---|---|---|
| deep crouch (hip 110, knee 135) | **FAIL** | `Hips` mean volume ratio 0.686 (fail below 0.75), p5 0.28; 68 edges tear, mostly `LeftUpLeg` 45 and `Hips` 23 |
| high kick (105) | WARN | 35 failing edges, 43 on `RightUpLeg`, max stretch 5.4x |
| arm raise | WARN | 4 failing edges at the shoulders, max 3.15x |
| grab | WARN | 1 failing edge (right shoulder), max 2.03x |
| torso twist | PASS | max 2.54x |
| skeleton, rest pose, scale, up axis, origin, weights | PASS | all 24 bones, 1.75 m, Y-up, feet at origin, no cross-side weights |

The skeleton is correct. The fault is the mesh under the hips and the skin weights the Meshy auto-rig (R3) paints from it; we cannot edit those weights through the MCP.

## 2. Cause (from the stills and the take-2 notes)

1. **Legs fused at the crotch in the mesh.** Take 2 is one trouser surface whose inner thighs touch in the T-pose (legs 4 deg off vertical, ankles +-0.13 m). Inner-thigh, crotch and seat vertices sit within a few cm of both `UpLeg` bones, so the auto-skin splits them between `Hips`, `LeftUpLeg` and `RightUpLeg`. At 110 deg the thighs rotate forward while those shared vertices are pulled both ways: that is the collapse on `Hips` and the bulge in `deep_crouch_front.png`.
2. **Bulky cloth.** Ryo wears full-length trousers, not hakama (sheet, take 2), but the model has loose folds at the thigh and a gathered cuff. Fold geometry the skinner cannot split into thigh and shin adds volume that stretches or pinches at the groin and at the knee (135 deg).
3. **Coat hem and rope sash at hip height.** The short coat panel, rope sash and lightning-shaped paper hang at the same height as the hip joint. They are rigid on `Hips` and pile into the thighs when those flex.
4. **Turnaround pose.** T-pose with the feet almost together (take 2's R1 prompt only said "T-pose"), so R2 had no space between the legs to model. The designer's T-pose decision (YOK-30) stays; only the leg stance changes.
5. **Triangle budget (probable, not measured).** 15.5k triangles with most of the budget on face, hair and cloth folds; the groin and thigh area is coarse and uneven, so its weights come out blocky.

Not the cause: bone placement, skeleton shape. The arm raise and grab WARNs are the same family (loose sleeve volume at the shoulder, mitt hands); they only need to stay WARN or better.

## 3. Prompt and turnaround changes

Keep Ryo's identity: lean upright wide stance, black undercut with one lock over the right eye, high indigo collar over mouth and nose, indigo coat with brass buttons, rope sash and small pouch, black split-toe jika-tabi, vermilion mark under the left eye. Palette as `style.md`: indigo #2D3A5E, ink #1D1B21, persimmon #D8632C, rice paper #F1E8D4, brass #B08A48.

Changes against take 2:
- **Stance:** T-pose kept (YOK-30 decision), arms straight out, **legs apart about 12 deg each from vertical, a clear gap of daylight between thighs and knees from the crotch down, feet about 40 cm apart.** 12 deg stays under the rig_check rest-leg limit (15 deg), which is the most it allows. Not an A-pose.
- **Outfit cut:** trousers **close-fitting through the thigh, soft taper at the knee, narrow straight shin, cuff tucked into the boot**, no baggy folds, no extra pockets or ties. Sleeves fitted, cuffs at the wrist, no flared or hanging fabric.
- **Coat:** hip-length hem ending above the crotch line, **no panel crossing the legs** (the take-2 short coat). Rope sash at belly-button height, narrow, pouch at the side hip, paper strip tucked flat, no long hanging shide.
- **Hands:** open, fingers together, palms down (E15: no finger bones).
- **Cloth cue:** smooth cloth with few fold lines, so the mesh has fewer wrinkles.
- Plain white background, no held items, no energy, no text or kanji.

Turnaround prompt (nano-banana-pro; tool limit 600 characters, check the length before sending):
> Character turnaround of the same young exorcist: front, side and back views, same height and scale, T-pose, arms straight out, legs apart with a clear gap between the thighs, plain white background. Keep this art style: anime, thick consistent black ink outline, hard-edged cel shading, flat colours, smooth cloth with few folds. Indigo hip-length coat with brass buttons, fitted sleeves, high collar over mouth and nose, black undercut hair, narrow rope sash with small pouch, fitted indigo trousers tapering to the shin, black split-toe jika-tabi. No held items, no effects, no text, no logos, no kanji.

**Reference image:** the same `docs/design/characters/ryo/references/ryo-ref-e.png` as take 2, so face, outline and palette stay identical. The legs-apart pose comes from the prompt. If the first turnaround still shows touching legs, the retake edits only the stance sentence.

**Open point for the designer (not decided here):** the sheet says "coat tail cut long on one side with persimmon lining"; take 2 shipped without it (YOK-30 "Ryo's simplified outfit"). A long tail rigid on `Hips` raises the groin risk again. This spec keeps the simplified hip-length coat. A long tail would need a separate-mesh tail (rig-specialist) and a new decision.

**Who makes R1:** YOK-102 says Astra makes turnarounds that feed Meshy. Take 2 used a Meshy R1, so the cheapest path is Meshy R1 (9 credits). If the designer prefers an Astra turnaround, drop R1 and use its front view in R2; the stance and cut lines above become the Astra brief.

## 4. Generator and settings

**Meshy, through the Meshy MCP.** The shared skeleton is the 24-bone Meshy rig, and 26 Ryo clips already play on it by bone name. Tripo would need a bone map and a new retarget gate, so it is not used for this job. Run `meshy_check_balance` first and stop if it is short.

| # | Tool | Settings | Credits |
|---|---|---|---|
| R1 | `meshy_image_to_image` | `ai_model: nano-banana-pro`, `reference_file_paths: [ryo-ref-e.png]`, `generate_multi_view: true`, the prompt above | 9 |
| R2 | `meshy_image_to_3d` | `ai_model: meshy-7.1`, `file_path`: chosen front view, `model_type: standard`, `pose_mode: t-pose`, `should_remesh: true`, **`topology: quad`**, **`target_polycount: 20000`**, `should_texture: true`, `texture_resolution: 2k`, `enable_pbr: false`, `image_enhancement: false`, `origin_at: bottom`, `auto_size: true`, `target_formats: ["glb"]` | 30 |
| R3 | `meshy_rig` | `input_task_id`: R2, `height_meters: 1.75` | 5 |

Setting changes against take 2:
- **Quad topology** gives edge loops around thigh and knee that the auto-skin can follow (take 2 used triangles). Unverified that `meshy_rig` and Godot accept quad output; R3 will show. If not, fall back to `triangle` and note it.
- **Polycount 20k** (was 15k) puts more density at the groin, thigh and knee. If still short, the next lever is 30k, not more prompt changes.
- Everything else is the take-2 setting, so the look stays consistent. Anything beyond this chain (extra remesh, higher texture size) needs a new approval.

**Free option before spending: weight repair.** `rig-specialist` can try a Blender weight repair on the current `ryo-rigged.glb` (re-paint crotch and inner-thigh weights, run `rig_check.py`). If crouch reaches WARN or better, regeneration is unnecessary and Ryo's clips, the Tanuki and the portrait are untouched. I recommend asking for that first; the choice is the designer's. The regeneration here is the route if repair fails or the designer wants the outfit fixed too.

## 5. Acceptance checks

Run on import of the new rig; results go under `## Acceptance` when a take lands.
- **Rig gate:** `rig_check.py` on the new `ryo-rigged.glb`: skeleton PASS (same 24 bones, order and parents as `skeleton.md`), rest pose PASS (legs at most 15 deg), scale, up axis, origin, weights PASS. **Deep crouch must be at least WARN (`Hips` mean ratio at least 0.75, no `collapsed_bones`); target at least 0.85.** High kick, arm raise and grab no worse than WARN; torso twist stays PASS. A FAIL on any pose is a RETAKE.
- **Look (V1):** palette matches the sheet; thick outline, flat colour, no baked PBR or painterly noise; reads at 720p. Collar over mouth, rope sash, pouch, binding mark legible.
- **Consistency:** face, hair, outline weight and colours match take 2 and Kitsune; height 1.75 m. Bone heads within 3 cm of the Ryo column in `skeleton.md`, so existing clips and the hurtbox still fit.
- **Mesh:** T-pose with a visible gap between the thighs, one 2K base colour, no PBR maps, at most 20k triangles, origin at the feet, no merged inner-thigh surfaces.
- **G4 readiness:** no cloth crossing the legs; record crouch, high kick and throw risk for `rig-specialist`.
- **Licence:** Meshy plan tier and commercial terms are still unconfirmed (see take 2); flagged.
Verdict is ACCEPT or RETAKE with the exact change and its credits. After 3 failed takes: stop and propose the fallback (hand-repaired weights in Blender on the least bad take, or a separate-mesh coat).

## 6. Cost and retake budget

Prices from the take-1 and take-2 logs (R1 9, R2 30, R3 5).

| Item | Credits |
|---|---|
| Take 1, full chain R1+R2+R3 | **44** |
| Retake of R2+R3 only (turnaround fine, mesh or weights bad) | 35 |
| Retake of R1+R2+R3 (stance wrong in the turnaround) | 44 |
| Retake at 30k polycount, R2+R3 | 35 (assumes the same R2 price; unverified at 30k) |

**Ask:** approve **take 1 (44 credits)** plus a retake reserve of **two takes, up to 88 more**, ceiling **132** (the 3-take cap). I run a retake only after reporting the failed check and getting its settings approved. Balance was 941 on 2026-10-06, before clip takes; I will check it live first.

Downstream spend, **not in this ask** (separate approval after the model is accepted):
- Re-fetching Ryo's 23 clip takes on the new rig, only if the reuse check in section 7 fails: about 69 credits (derived from the roughly 3 credits per take implied by YOK-32/42's 123 credits for 41 takes; not measured).
- A repeat retarget gate for Ryo: about 9 (YOK-31 cost 18 for both fighters).
- A new HUD portrait (9) only if the face changes.

## 7. Impact

| Area | Effect | Redo? |
|---|---|---|
| Tanuki (T0) | Fights in Ryo's model and rig, so it inherits the new mesh and the fix. Its merchant look and true form are separate assets. | No new asset; re-check the Tanuki skin once |
| `game/scenes/fighters/ryo.tscn`, `game/scripts/Fight/Presentation/FighterAnimSet.cs`, `game/assets/generated/characters/ryo/*` | Reference `ryo-rigged.glb`, `ryo-mesh.glb` and the base colour PNG; new files replace them in place and Godot re-imports | Swap files; check `.import` settings and mesh path |
| Clips: `data/clips/ryo-*.json` (26) and `game/assets/generated/clips/ryo/` | Clip GLBs animate the same 24 joints; `retarget_notes` name rig `01a10e47-857e-...`. If new bone heads are within 3 cm and a retarget shows a similar planted-foot shift (take-2 notes: 2.7 and 2.1 cm), the GLBs are reused and nothing is re-bought. If not, re-fetch (about 69 credits). | Re-validate all 26; re-fetch only on failure |
| Frame data (`data/moves/`) | Frames come from clip frames, which do not change if the clips are reused. Hitboxes are 2D rectangles in move data, not mesh. A re-fetched clip with new timing means `animator` re-measures and `movesmith` re-derives that move. | Only for re-fetched clips |
| Hurtbox and spacing | T-pose span and rest width shift slightly with the leg stance; the T-pose is not used as the rest hurtbox. Height stays 1.75 m. | Check in an in-engine render |
| Known clip WARNs (idle guard turned about 50 deg; `ryo-crouch` arms near the knees, CrouchLookAroundBow) | Not changed by this job; `ryo-crouch` is the first to re-judge since the mesh change targets it | Re-judge `ryo-crouch` |
| HUD portrait (`ui-hud-ornaments.md` H1, `portrait-ryo.png`) | Made from the sheet reference, not the 3D model; a trouser cut change does not touch a bust | No, unless the face changes |
| Rig spec and gate | `rig-specialist` re-runs `rig_check.py` and updates the Ryo column in `skeleton.md` (rig task id, bone heads) | Yes |
| Kitsune | Own rig; unaffected. Her stress poses are outside this ticket. | No |
| Spec files | `assets/specs/ryo.md` gets a pointer to this file when a take is accepted | Yes |

## Takes
None. No job has been run.

## Acceptance
Pending a take.
