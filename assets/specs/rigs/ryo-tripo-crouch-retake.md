# Ryo (Tripo): deep-crouch retake spec (YOK-146)

Ticket: YOK-146 "Tripo redo: Ryo (pilot)". Owner: rig-specialist. Rules: F3, F5, F6, G4, T0, E15; AM1, AM5.
Status: **SPEC ONLY, waiting for the designer's approval. No job has been run and no credits have been spent.** The designer runs Tripo by hand (no Tripo MCP is installed) and drops the files in `game/assets/generated/characters/ryo/tripo-raw/` (`.gdignore`d, as for the raw `Ryo.glb` in PR #195).

## Designer decisions this spec works from (2026-10-20)
- Keep the 54k-triangle Tripo mesh, with no decimation (the 20k budget is waived for Ryo).
- The deep-crouch flag gets a **RETAKE**.
- The `YOK-146-ryo-groin-weights` branch is rejected, so the groin pleat in the floor clips (`ryo-tripo.md`, open item 3) is out of scope here.
- The raw `Ryo.glb` is committed in PR #195. This spec reads it by sha256 `c46dfd32…`.

## The flag being retaken
From `ryo-tripo.md`, rig check: **deep crouch FAIL** on `ryo-tripo-rigged.glb`. It tears 55 edges (the limit is 169), but `LeftUpLeg` volume has a mean of 0.749 (limit 0.75) and a p5 of 0.29 (limit 0.40). The squashed vertices sit 16 cm in front of the upper left thigh. They are the paper charm and the pouch, which are fused into the one body mesh across the hip crease. When the thigh flexes 110° they swing in toward the bone. The stills read clean (`stills/ryo-tripo/deep_crouch_front.png`, `deep_crouch_side.png`). Binding the props rigid to `Hips` inside the body mesh (`prop_bind=1`) made it worse, with 985 torn edges. So the props have to leave the body mesh.

## Route choice (the designer picks one)
"Keep the 54k mesh" rules out regenerating the body unless the designer reverses it. The spec therefore leads with routes that keep the body.

| Route | Body | Charm and pouch | Tripo credits (est.) | Re-opens |
|---|---|---|---|---|
| **A: cut, and new Tripo props** (recommended) | The accepted 54k mesh, with the prop faces cut out by script and the hole capped | New separate meshes from Tripo, rigid on a `Hips` `BoneAttachment3D` | **~25–60** per take | Texture re-projection (0 credits); rig and clip checks re-run (0 credits) |
| B: cut, and reuse the cut geometry | Same as A | The cut faces themselves, closed and made a rigid prop | **0** | Same as A. Not a Tripo retake; the risk is an open or thin back on the prop |
| C: full body retake | A new Tripo body without the props (**replaces the 54k mesh**, which reverses today's decision) | As in A | **~80–95** per take | Everything: texture pass, canon rig, weight repair band, 26 stopgap clips, G4 stopgap gate, mocap upload character |

No new bones are added under any route. A rigid prop on a Godot `BoneAttachment3D` on `Hips` is the Oni club and Kappa shell method (`prep_prop.py`, `prop_clearance.py`). It leaves the shared skeleton unchanged. A sway bone for the charm would change `skeleton.md`, which is the designer's call and is not proposed.

## Route A in detail (B skips step R1 and uses the cut part as the prop)

### R0. Prop input images (designer, Astra; not Tripo)
- Source: `assets/specs/texture/ryo-tripo/ryo-paint-front.png` and `ryo-paint-left.png`, the accepted paint-over, so the props match the baked texture.
- Astra edit, designer runs: "The same paper lightning charm [then: the same small pouch] from this character's hip, alone, front view, flat colours, thick black ink outline, plain white background, no body, no rope, no shadow." One image per prop, plus a side view if Tripo multiview is used.
- Cost: about 2–4 Astra generations. No Tripo credits.

### R1. Tripo jobs (designer runs; every figure is an estimate)
Tripo's plan, version and per-job credits **cannot be verified from the repo** (YOK-115 is open). The figures follow the working estimates in `assets/specs/kitsune.md` (T3, a small part at ~25–30). The designer checks each against the figure the Tripo UI shows before running.

| # | Job | Tripo settings | Est. credits |
|---|---|---|---|
| R1a | Charm: image to model | Latest standard model; single image (front), or multiview front and side if offered; texture ON, **PBR OFF**; face limit **about 2,000** triangles; triangles, not quads; no stylize preset; **no rig**; export **GLB** | ~25–30 |
| R1b | Pouch: image to model | Same settings, face limit about 2,000 | ~25–30 |
| R1 (cheaper) | Charm and pouch in one image, one job | Same settings, face limit about 4,000. Split into two meshes later by script (loose parts) | ~25–30 |

The designer records the task id, the version and the actual credits in "Takes" below. The Ryo body job's figures were never recorded.

There is **no export preset change to the body**. The body is not regenerated. For Route C only, the body job uses the `ryo-tripo.md` settings: auto-rig with the **Mixamo preset**, fingers on (stripped by script), GLB with the rig, T-pose, texture ON, PBR OFF. The face limit is left at Tripo's default, since 54k is accepted. The input is the Astra turnaround with the charm and pouch painted out and the rope sash kept. Estimates: body ~30–40, rig ~25, props ~25–60, so **~80–95** per take.

### R2. Scripted Blender steps (rig-specialist; 0 credits; F5, so nothing is keyed or hand-placed)
1. **Cut** (`tools/blender/cut_shell.py`, extended with a `--front` box mode and Ryo's colour band, or a sibling `cut_prop.py`): select the charm and pouch vertices on `ryo-tripo-rigged.glb`. The box goes in front of the left hip, roughly at the props' height around the `UpLeg` head, plus the light, low-chroma colour band that `repair_weights.py` step 4 already uses (`prop_light_min` 0.55, `prop_light_max_chroma` 0.25, at least 20 vertices). Delete them, cap the open loops with no new vertices, and give the cap the hakama's UV sample. The log records the vertices removed, the cut part's rest bounding box and both sha256s.
2. **Weights**: the remaining vertices keep their `ryo-tripo` repair weights unchanged. Re-run `repair_weights.py --fighter ryo-tripo` only if the cut leaves vertices with no weights (the log must show 0).
3. **Prop fit** (`prep_prop.py --mode fit`): scale and orient each Tripo prop into the cut part's rest bounding box, with the pivot at the sash. The script writes the `BoneAttachment3D` child transform on `Hips`. Route B instead closes the cut part by script and fits it the same way.
4. **Texture** (`texture_project.py`, then `apply_texture.py`): the mesh changed, so `apply_texture.py` alone is not enough. Re-project the four accepted Astra paintings onto the cut mesh, **with the charm and pouch masked out of the paintings** (otherwise paper colour lands on the hakama behind) and filled by the nearest-colour rule. The props keep their own Tripo texture, with `baseColorFactor` set to 1.0.
5. **Export** the skinned GLB as `ryo-tripo-rigged.glb` and the props as `characters/ryo/props/ryo-charm.glb` and `ryo-pouch.glb`. The current files are kept as `*-pre-crouch-retake.glb` until the verdict.

## Checks that must pass (rig-specialist; 0 credits)
| Check | Tool | Pass |
|---|---|---|
| Skeleton, rest, scale, up-axis, origin | `rig_check.py` | Unchanged from `ryo-tripo.md` (25 bones, 1.750 m, Y-up, feet on Y = 0) |
| Weights | `rig_check.py` | Max 4 influences, all normalised, 0 unweighted, 0 cross-side, 0 arm-leg bleed |
| **Deep crouch** | `rig_check.py` | **PASS**: `LeftUpLeg` volume mean ≥ 0.75 and p5 ≥ 0.40, and at most 169 torn edges (now 55) |
| High kick, arm raise, torso twist, grab | `rig_check.py` | No worse than now: 44 / 2 / 7 / 8 edges |
| Cap | Stills, pelvis close-up | No hole, no paper-coloured patch, reads as hakama |
| **Prop clearance** | `prop_clearance.py`, six stress poses plus arms-down | No new prop vertices more than 1 cm inside the body; the prop stays above the floor. **The main risk**: a prop held rigid on `Hips` in front of the left hip meets the thigh at 110° flexion in the crouch and the high kick. If it clips, the fix is to move the mount out and to the side (a look change, so the designer decides). A sway bone would change the skeleton |
| Clips | `skin_clip_check.py --rotations-only 1` on all 26 `clips/ryo-tripo/` clips | No clip worse than its row in `ryo-tripo.md`. The groin pleat rows (block-low, jump, knockdown, get-up) are expected unchanged and are out of scope |
| Gate clips | Walk, heavy and throw at the hit frames, plus the prop clearance | Recorded in `assets/specs/clips/retarget_gate.md`, YOK-146 section |
| Godot import | Headless import | `Armature/Skeleton3D` with 25 bones; props mount on `Hips` and follow it in `tests/model_capture.tscn only=ryo` |

Stills go to `stills/ryo-tripo/crouch-retake/`: rest, high kick, deep crouch, arm raise, torso twist and grab, front and side, plus the prop clearance views.

Verdict: **ACCEPT** turns the rig verdict from "provisional" to accepted. **RETAKE** names the exact setting change and its credits and goes back to the designer. After 3 failed takes the fallback is to accept the flag as known (as on the Meshy Ryo), or to run Route B if Route A failed, or to drop the charm and pouch from the look.

## What this re-opens (Routes A and B)
| Already done | Re-opened? | Re-run and cost |
|---|---|---|
| Texture pass, phase B | **Yes, partly.** The mesh changes, so the projection must be re-run | `texture_project.py` with the props masked, then `apply_texture.py`. 0 credits; no new Astra paint-over unless the cap fill reads wrong (then 1–2 Astra views, designer) |
| Canon rig, skeleton, weight repair | No. The skeleton and the remaining weights are unchanged | `rig_check.py` only. 0 credits |
| 26 stopgap clips (`clips/ryo-tripo/`), `data/clips` trims | No. The clips animate the skeleton, which is unchanged | Skin re-check on the new mesh. 0 credits |
| G4 stopgap gate (test-branch GO) | Re-checked, not re-opened | Re-render walk, heavy and throw with the props. 0 credits |
| Mocap heavy punch and kick spec (`clips/yok-146-ryo-heavy-mocap.md`) | No. Same skeleton | If the mocap tool bakes onto an uploaded character, upload the new GLB. 0 credits |

Route C re-opens all five rows. It needs a new canon rig and a re-calibrated repair band, 26 re-retargeted clips, a new Astra four-view paint-over (~8 generations, as last time) and a new G4 stopgap run. All of that is rig-specialist and designer time; the Tripo credits are as in R1.

## What it unblocks
- **The Tripo Ryo rig leaves "provisional"**: its one FAIL is gone. That is a precondition for the formal **G4 on the Tripo set**. On its own it does **not** close G4, which still waits for the real Tripo or mocap clips (`retarget_gate.md`, YOK-146).
- **YOK-147–149** (Kitsune, Oni and Kappa on Tripo): their acceptance bar becomes "stress poses with no volume FAIL" instead of "volume FAIL only for painted props" (`kitsune.md`, Tripo acceptance). The cut-and-mount steps become the template for their fused parts (Kitsune's bell, the Oni's club, the Kappa's shell). Their spend still waits on G4 per `kitsune.md`.

## Jobs for approval
| # | Job | Who | Credits |
|---|---|---|---|
| R0 | Astra prop crops (2–4 generations) | designer | Astra only, no Tripo credits |
| R1 | Tripo charm and pouch: one combined job (~25–30) or two jobs (~50–60) | designer | **~25–60 Tripo, an unverified estimate**. Take cap 3, so the ceiling is ~75–180 |
| R2 and checks | Cut, fit, re-projection, checks | rig-specialist | 0 |

## Takes (designer fills in the Tripo rows)
| # | Date | Job | Tripo task id | Version and settings used | Credits (est / actual) |
|---|---|---|---|---|---|
| | | | | | |
