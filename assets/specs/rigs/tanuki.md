# Tanuki rig

Ticket: YOK-150 (Tanuki true form redo). Owner: rig-specialist. Rules: F5, F6, G4, T0, E15, AM5.
Model spec: `assets/specs/characters/tanuki.md` (asset-smith). Its true-form 3D model is the optional line T4. Concept: `docs/design/characters/tanuki/tanuki-true-form-astra-*.png`. Shared skeleton: `assets/specs/rigs/skeleton.md`. Template: the Kappa v2 (`kappa.md`).

In the fight the Tanuki uses Ryo's body (T0: `FighterAnimSet.Ryo`; `HitSparks` "ryo" or "tanuki"). This rig is the true form for the reveal, and nothing in the game uses it yet.

## v2b (Tripo retake: longer legs), 2026-11-10

The designer approved the retakes ("approve retakes") and ran the jobs:
- **Mesh:** H3.1 task `1c72d411-4b2e-4e87-a85c-cad592c78b1e` (30 credits), with longer legs.
- **Rig:** v1.0 task `6931c5be-1da2-4a99-b153-efde6a72158d` (25 credits). No paid call was made by the agent.

Raw: `tripo-out/tanuki-v2b/rig/tripo-out/rig-3-6931c5be/model.glb`. It has the take-1 layout: 23 joints plus `Root`, faces +X, limbs on their own sides, source 0.80 m, 19,979 tris.

**Verdict: rig RETAKE-class still, but much closer.**
- **Rest pose now PASS** (arms 14.0 / 14.4°).
- **Weights still FAIL:** 240 arm-and-leg bleed verts (774 before) and 11 cross-side.
- **Kick and crouch FAIL** (70 / 206).
- **G4: GO on the walk; NO-GO on the heavy, the throw and the high hit**, on tears just over the limit.
- **The feet now track.** The worst sink is −7.6 cm in the walk, against −14.2.

**Game file:** `game/assets/generated/characters/tanuki/tanuki-v2-rigged.glb`, sha256 `2406a7f6d9c4e4de…`. Take 1 is kept as `tanuki-v2-rigged-take1.glb` (`753c2f4512b2d734…`). Neither is wired in.

**Takes**
| # | Job | Task id | Credits |
|---|---|---|---|
| v2b-1 | mesh, H3.1 multiview (designer-run) | `1c72d411-4b2e-4e87-a85c-cad592c78b1e` | 30 |
| v2b-2 | `tripo anim rig 1c72d411… --model v1.0 --rig-type biped --spec mixamo` (designer-run) | `6931c5be-1da2-4a99-b153-efde6a72158d` | 25 |

**Build chain** (free; logs `logs/tanuki-v2b-*.json`). Canon with the take-1 flags at 1.75 m (scale 2.19), then `repair_weights.py --fighter tanuki-tripo`, unchanged (only the rigid tail, X4: 351 rigid verts and 830 in the blend).

Variants that were tried and dropped, as a temporary profile (G4 tears, walk / heavy / throw / high hit):

| Variant | Weights | G4 tears | Kick |
|---|---|---|---|
| T4 bleed + X3 cross-side clean | **PASS** | 31 / 133 / 89 / 97 | 144 |
| T4 alone | 11 cross-side | 29 / 114 / 90 / 93 | 79 |
| X3 alone | 240 bleed | 26 / 125 / 68 / 86 | 135 |

The kept profile gives **21 / 104 / 68 / 82**. A weights-PASS rig costs 10-50% more clip tears, so the profile stays as it is (the designer may prefer PASS weights).

**Take 1 → v2b**

| | Take 1 | v2b |
|---|---|---|
| Legs: `Hips` height / height | 0.552 m / 0.315 | **0.637 m / 0.364** |
| Knees / ankles | 0.21 / 0.05 m | 0.30 / 0.04 m |
| Hip joints apart | 0.57 m, 9.5 cm apart in height | **0.46 m**, level |
| Hip scale on the clips | 0.553 | 0.638 |
| Rest pose | FAIL (arms 20.1°) | **PASS** (14.0 / 14.4°) |
| Weights | FAIL, 774 bleed, 3 cross-side | FAIL, **240** bleed, 11 cross-side |
| Kick / crouch | FAIL 265 / 504 | FAIL **70 / 206** |
| Arm raise / twist / grab | WARN 37 / 36 / 15 | WARN 17 / 16 / 7 |

Rig check: `logs/tanuki-v2b-rig-check.json`, stills `stills/tanuki-v2b/*.png`; tear limit 64 (32,093 edges).

Godot 4.7.2: 25 bones in shared order, `Hips` rest (−0.023, 0.637, 0.038), one skinned mesh `tanuki`, 1.75 m.

**G4** (`godot_play.py`, hip scale 0.638; logs `logs/tanuki-v2b-g4/`, stills `stills/tanuki-v2b/g4/`; Ryo's tears 8 / 98 / 20 / 35)

| Clip | Tears, take 1 → v2b (limit 64) | Contact cm, take 1 → v2b | Verdict |
|---|---|---|---|
| Walk | 56 → **21** | −14.2..+2.1 → **−7.6..+3.6** | **GO** (feet WARN: the right sole sinks on one frame) |
| Heavy | 172 → **104** | −8.7..+1.5 → −5.7..+1.0 | **NO-GO** (`Spine1` and arm roots at f24) |
| Throw | 70 → **68** | −5.4..−1.1 → **−1.4..0.0** | **NO-GO by 4 edges** (feet GO) |
| High hit | 141 → **82** | −7.8..−0.4 → **−1.1..+1.1** | **NO-GO** (feet GO) |

Remaining options (the designer chooses):
- **Planted feet** (free, animator): for the walk and the heavy.
- **Upper-back and arm-root smoothing** for the heavy (free, scripted). Untested.
- **The reveal as art or a posed still**, if the fight keeps Ryo's body (T0): 0 credits.
- **A Tanuki clip set** from Tripo presets: 10 credits each.

## v2 (Tripo), 2026-11-10: take 1, superseded by v2b above (kept as `tanuki-v2-rigged-take1.glb`)

Mesh: Tripo H3.1 task `c586cb59-4f5f-406b-83a3-f8439b60f2a2`, run by the main session; it passed the free `anim check` as riggable and biped. The striped tail is baked into the mesh.

**Height: 1.75 m.** The specs give the true form no height; the asset spec's optional mesh line says "~1.4 m". The game today gives the Tanuki Ryo's 1.75 m body, so the true form takes the same height and stands as tall as the merchant's opponent. A uniform rescale later is free.

**Verdict: RETAKE (not accepted). Stopped at the checks, as approved; no retake run.**
- Skeleton, scale, axes and origin PASS.
- **Rest pose FAIL** by 0.1°: the arms sit 20.1° below horizontal against a limit of 20.
- **Weights FAIL**: 774 arm-and-leg bleed verts. No scripted repair cleared them without tearing the gate clips more (below).
- The kick and the crouch FAIL. The tail tears where it meets the seat.
- **G4: NO-GO** on the heavy and the high hit, and on the feet in every clip.
- Game file (take 1, for review only, not wired in): now `game/assets/generated/characters/tanuki/tanuki-v2-rigged-take1.glb`, sha256 `753c2f4512b2d734…`.

### Takes
| # | Date | Job | Task id | Credits |
|---|---|---|---|---|
| v2-1 | 2026-11-10 | `tripo anim rig c586cb59… --model v1.0 --rig-type biped --spec mixamo --out-format glb` (designer approval 2026-11-10, 225 for the three redos) | `21ae46c0-5569-48b8-b2d7-d4dd879deee1` | **25** (`credits_consumed` 25; balance 350 → 325) |

Raw output: `tripo-out/tanuki-v2/rig/tripo-out/tanuki-v2-rig-21ae46c0/model.glb` (not committed). It is the same layout as the Kappa's:
- 23 joints plus `Root`, no fingers and no leaves;
- the joint nodes carry TRS, and the IBMs equal them to 2.6e-6;
- faces +X; source 0.60 m tall, 11,849 verts, 19,828 tris.

### Build chain (scripted, free)
1. **Canon:** `canon_tripo_rig.py --height 1.75 --mesh-name tanuki --drop-root --add-leaves --face-z --trust-bind --frames-from game/assets/generated/characters/ryo/ryo-tripo-rigged.glb --frames-swing LeftUpLeg,LeftLeg,RightUpLeg,RightLeg`. These are the Kappa's flags, scale 2.917. Log: `logs/tanuki-v2-canon.json`.
2. **Weights:** `repair_weights.py --fighter tanuki-tripo` (new profile). Log: `logs/tanuki-v2-repair.json`.
   - Only the new step **X4 (rigid tail)** runs. It is off by default; Ryo's and the Kappa's outputs are byte-identical.
   - X4 makes every vert more than 0.40 m behind the `Hips` head (fully from 0.60 m, smoothstep) `Hips` 1.0: 756 rigid verts and 773 in the blend.
   - Every belly rule tore the gate clips more. G4 tears, walk / heavy / throw / high hit:

| Variant | G4 tears | Stress poses |
|---|---|---|
| Raw (no repair) | 65 / 203 / 103 / 152 | kick 194, crouch 364 |
| **Chosen: tail rigid on `Hips` (y 0.40-0.60 m)** | **56 / 172 / 70 / 141** | kick 265, crouch 504 |
| Tail rigid, narrower blend (0.40-0.50, 0.8 `Hips`) | 55 / 280 / 173 / 254 | |
| Tail 0.45-0.65, 0.6 `Hips` | 56 / 172 / 78 / 136 | |
| Kappa steps (T2 / T6 / C / K / T4) | 65 / 273 / 167 / 199 | |
| T4 bleed only, with the tail | 97 / 236 / 167 / 202 | arm raise 110 |
| Arm strip on the belly (T1, three bands) | 87-97 / 232-253 / 152-164 / 192-198 | |
| Band smoothing 8-40 iterations | 47-66 / 168-357 / 101-219 | |
| X3 cross-side clean | 154 / 265 / 135 / 155 | |

### Rig check (`rig_check.py`, Blender 4.1.1)
Log: `logs/tanuki-v2-rig-check.json`. Stills: `stills/tanuki-v2/{rest,high_kick,deep_crouch,arm_raise,torso_twist,grab}_{front,side}.png`. Tear limit: 63 (0.2% of 31,583 edges).

| Check | Raw canon | Final | Notes |
|---|---|---|---|
| Skeleton | PASS | **PASS** | 25 bones in shared order |
| Rest pose | FAIL | **FAIL** | Arms 20.1 / 19.1° below horizontal (limit 20): the mesh's own droop. Legs 6.4 / 8.2°, rest == bind |
| Scale / up / origin | PASS | **PASS** | 1.750 m; hips to head 4.0°; faces +Z; feet on 0 |
| Weights | FAIL | **FAIL** | 774 arm-and-leg bleed verts on the belly top (z 0.82-0.99 m, under the short arms), 3 cross-side. Load-bearing: stripping either side made every clip tear more |
| High kick | FAIL 194 | FAIL 265 | Seat and tail base (`Hips` 228) |
| Deep crouch | FAIL 364 | FAIL 504 | Volume collapse in `Spine` 0.64, `LeftUpLeg` 0.74, `RightLeg` 0.75. The thighs fold into the belly |
| Arm raise / twist / grab | WARN 37 / 44 / 16 | WARN 37 / 36 / 15 | |

**Tail flag (designer's request).** The rigid tail tears at its seam with the seat whenever a thigh lifts: the kick goes 194 → 265 and the crouch 364 → 504. In the gate clips it tears less than the raw weights did, which left the tail on both thighs.

### Measured proportions
| | Measured |
|---|---|
| `Hips` joint | 0.552 m. **Hip scale 0.553** on the shared clips |
| Hip joints | `LeftUpLeg` (0.31, 0.60, 0.01), `RightUpLeg` (−0.26, 0.50, 0.08). **Tripo placed them 9.5 cm apart in height**; both sit above `Hips` at the belly's sides |
| Knees / ankles | 0.21 / 0.05 m: the legs are 0.55 m on a 1.75 m body |
| Arms | `LeftArm` (0.35, 1.17, 0.00), hand at 0.67 m out |
| Head | joint at 1.22 m; head and ears 0.53 m |
| Tail | from the seat to 0.70 m behind the back, z 0.19 to 0.95 m |

Godot 4.7.2 import (throwaway project, headless, `--audio-driver Dummy`):
- `Armature/Skeleton3D`, 25 bones in shared order;
- `Hips` rest (0.027, 0.552, 0.045), `RightHand` global rest (−0.648, 1.095, 0.098);
- one skinned mesh `tanuki`, 1.75 m, no animation.

### G4 (retarget gate), as the game plays it
Tool: `godot_play.py`, with hip scale 0.553. Logs: `logs/tanuki-v2-g4/`. Stills: `stills/tanuki-v2/g4/tanuki-v2-<clip>_f<frame>_{front,side,back}.png`. Ryo's figures are in brackets (`logs/kappa-v2-g4/ryo-*.json`, re-run and identical).

| Clip (trim) | Skin tears, worst frame (limit 63) | Contact cm | Verdict |
|---|---|---|---|
| Walk `ryo-walk` (0-64) | 56 at f36 (8) | **−14.2 to +2.1** (0.0 to +3.9) | **NO-GO: the feet sink 12-14 cm** |
| Heavy `ryo-heavy` (28-61) | **172** at f25 (98) | **−8.7 to +1.5** (−0.2 to 0.0) | **NO-GO** |
| Throw `ryo-grab-2` (30-59) | 70 at f20 (20) | −5.4 to −1.1 (−0.8 to +1.1) | **NO-GO** (tears just over, feet sink) |
| High hit `ryo-hit-high` (28-70 at 2.5x) | **141** at f13 (35) | −7.8 to −0.4 (0.0 to +2.2) | **NO-GO** |

Why:
- The shared clips are human motion. On legs 0.31 of his height, with the hips at 0.55 m and the hip joints 0.57 m apart, Ryo's leg swings and pelvis roll push the feet through the floor. Hip scale only fixes the pelvis height.
- The thighs pass through the belly.
- No bone-frame or weight change fixes the proportions.

### Options (the designer chooses)
1. **Keep the true form for the reveal only** (V7: card art or a camera push-in on a static or posed model). The fight uses Ryo's body (T0), so the fight needs nothing from this rig. A posed still needs no gate clips: 0 credits.
2. **A Tanuki clip set for the reveal:** Tripo presets retargeted to this rig (for example idle, taunt, `hit_to_*`), 10 credits each, animator's spec, plus `plant_feet.py` (free).
3. **Model retake** with longer legs and a smaller belly so the shared clips fit. Asset-smith's spec: Tripo mesh 30 plus rig 25 credits.
4. Fixable by script whichever option is chosen: the arm droop, by re-posing the arms by script like the Kappa's leg re-pose. It needs a go-ahead, because it moves joints.
