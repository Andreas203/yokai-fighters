# Shared skeleton (AM5, F6)

Ticket: YOK-111. Owner: rig-specialist. Rules: F6, G4, T0, E15, AM5.
Status: **SPEC.** Read from the accepted Meshy rigs of Ryo and Kitsune (YOK-30 rebuild, YOK-31 gate). No generation job was run and no credits were spent. The scripted rig check (`tools/blender/rig_check.py`, YOK-110) does not exist yet, so its run against this spec is **pending** (see the end).

Every clip in the game plays on this one skeleton (F6). T0 depends on it: the Tanuki fights with Ryo's model and clips, and every yokai's clips are shared presets. A rig from another tool joins only through a bone map below. Changing this skeleton is the designer's call.

## Source files
| Fighter | File | Rig task id | Height |
|---|---|---|---|
| Ryo | `game/assets/generated/characters/ryo/ryo-rigged.glb` | `01a10e47-857e-73a0-9f7a-cd74795198fb` | 1.75 m |
| Kitsune | `game/assets/generated/characters/kitsune/kitsune-rigged.glb` | `01a10e47-91f6-709b-bd34-8884f8cbabbb` | 1.65 m |

How it was read (2026-10-15): the glTF JSON chunk parsed directly, and Blender 4.1.1 headless (`-b`, glTF import, read only) for world-space bone heads, vertex groups and weights. The joint list was then compared across every skinned GLB under `game/assets/generated/` (both rigs, their walk/run GLBs, all clip GLBs in `clips/`, `clips/ryo/`, `clips/kitsune/`): **all of them have the same 24 joints in the same skin order.**

## Frame, scale, origin
| Property | Value |
|---|---|
| Up axis | **+Y** (glTF native). Blender shows it as +Z after import |
| Facing | **+Z** in glTF (the `headfront` joint sits 12.7 cm (Ryo) / 16.5 cm (Kitsune) in front of `Head` on +Z). Character's left = +X. The Godot fighter scenes turn the model with the `Mirror/Yaw` node of `game/scenes/fighters/BaseFighter.tscn` (+90° about Y: rig +Z → world +X = sim facing right; YOK-145), shared by every fighter, not by changing the rig or a per-fighter rotation |
| Units | Bones in **centimetres**: the `Armature` node carries scale 0.01, so world units are metres. Bone translations in the files are in cm (Hips at Y about 95) |
| Origin | At the feet: mesh bbox min Y = 0.000 on both rigs; root at world (0, 0, 0) |
| Rest pose | **T-pose**: arms horizontal (hand head 3.8 cm (Ryo) / 6.0 cm (Kitsune) below the shoulder head), legs straight, feet slightly apart (ankles ±0.13 m) |
| Root bone | `Hips` (no separate root bone above it; root motion, if any, lives on `Hips` translation and is stripped to in-place clips per F6) |
| Scene tree | `Armature` (scale 0.01) → `Hips` … and the skinned mesh `char1` as a child of `Armature`. In Godot: `Model/Armature/Skeleton3D` |
| Skin | One skin named `Armature`, 24 joints. Max 4 influences per vertex, all weights normalised (both rigs) |
| Fingers | **None.** Hands are single mitts (E15: open-hand strikes, no code-posed fists) |
| Twist bones | None |
| Embedded clip | Each rig GLB carries one single-frame clip `Armature|clip0|baselayer` (the bind pose); ignore it on import |

## Bones and hierarchy
Skin order is the joint index order used by every clip GLB. All 24 joints carry weights on both rigs, including the two head leaves.

```
Hips
├── Spine02                      (lowest spine bone; Meshy counts DOWN: Spine02 -> Spine01 -> Spine)
│   └── Spine01
│       └── Spine                (chest; parent of neck and both shoulders)
│           ├── neck
│           │   └── Head
│           │       ├── head_end   (top of skull, leaf)
│           │       └── headfront  (face, leaf)
│           ├── LeftShoulder -> LeftArm -> LeftForeArm -> LeftHand
│           └── RightShoulder -> RightArm -> RightForeArm -> RightHand
├── LeftUpLeg -> LeftLeg -> LeftFoot -> LeftToeBase
└── RightUpLeg -> RightLeg -> RightFoot -> RightToeBase
```

Rest-pose joint heads, world metres, glTF axes (X left+, Y up, Z forward):

| # | Bone | Parent | Ryo (x, y, z) | Kitsune (x, y, z) |
|---|---|---|---|---|
| 0 | `Hips` | (root) | 0.008, 0.949, -0.004 | 0.000, 0.959, 0.048 |
| 1 | `Spine02` | `Hips` | 0.006, 1.066, -0.008 | 0.001, 1.058, 0.046 |
| 2 | `Spine01` | `Spine02` | 0.005, 1.183, -0.011 | 0.001, 1.156, 0.043 |
| 3 | `Spine` | `Spine01` | 0.004, 1.303, -0.014 | 0.002, 1.258, 0.040 |
| 4 | `neck` | `Spine` | 0.002, 1.385, -0.017 | 0.003, 1.320, 0.038 |
| 5 | `Head` | `neck` | 0.000, 1.455, -0.018 | 0.004, 1.386, 0.037 |
| 6 | `head_end` | `Head` | 0.008, 1.702, -0.003 | 0.020, 1.580, 0.068 |
| 7 | `headfront` | `Head` | 0.000, 1.455, 0.127 | 0.004, 1.386, 0.202 |
| 8 | `LeftShoulder` | `Spine` | 0.034, 1.347, -0.015 | 0.035, 1.309, 0.039 |
| 9 | `LeftArm` | `LeftShoulder` | 0.162, 1.347, -0.026 | 0.164, 1.309, 0.022 |
| 10 | `LeftForeArm` | `LeftArm` | 0.366, 1.313, -0.021 | 0.331, 1.252, 0.030 |
| 11 | `LeftHand` | `LeftForeArm` | 0.578, 1.309, -0.013 | 0.545, 1.249, 0.039 |
| 12 | `RightShoulder` | `Spine` | -0.033, 1.347, -0.016 | -0.029, 1.306, 0.039 |
| 13 | `RightArm` | `RightShoulder` | -0.170, 1.347, -0.027 | -0.153, 1.306, 0.021 |
| 14 | `RightForeArm` | `RightArm` | -0.355, 1.313, -0.022 | -0.306, 1.252, 0.029 |
| 15 | `RightHand` | `RightForeArm` | -0.570, 1.313, -0.017 | -0.534, 1.256, 0.037 |
| 16 | `LeftUpLeg` | `Hips` | 0.084, 0.863, -0.004 | 0.084, 0.868, 0.048 |
| 17 | `LeftLeg` | `LeftUpLeg` | 0.117, 0.516, 0.031 | 0.121, 0.505, 0.099 |
| 18 | `LeftFoot` | `LeftLeg` | 0.137, 0.121, -0.003 | 0.114, 0.141, 0.058 |
| 19 | `LeftToeBase` | `LeftFoot` | 0.173, 0.031, 0.075 | 0.139, 0.049, 0.118 |
| 20 | `RightUpLeg` | `Hips` | -0.076, 0.863, -0.004 | -0.079, 0.873, 0.048 |
| 21 | `RightLeg` | `RightUpLeg` | -0.113, 0.520, 0.031 | -0.114, 0.529, 0.099 |
| 22 | `RightFoot` | `RightLeg` | -0.132, 0.123, -0.004 | -0.110, 0.141, 0.047 |
| 23 | `RightToeBase` | `RightFoot` | -0.169, 0.032, 0.073 | -0.138, 0.050, 0.114 |

What the shared skeleton fixes and what it does not:
- **Fixed:** the 24 names (case-sensitive, note lower-case `neck`, `head_end`, `headfront`), the parent of each, the skin order, T-pose rest, Y-up, +Z facing, cm bones under a 0.01 node, origin at the feet.
- **Per rig:** bone positions (proportions). The two rigs differ by height (1.75 / 1.65 m) and Kitsune's whole column sits about 5 cm forward of the origin. Clips are rotations per bone (F6, in place), so they carry across proportions; `Hips` translation is the only positional channel and is stripped or kept small in place.

Weight notes (both rigs, for the stress-pose checks):
- `Spine` (the chest bone) carries almost no weight (Ryo 7 verts, Kitsune 50); chest skin is on `Spine01`, `neck` and the shoulders. Torso twist reads mostly through `Spine02`/`Spine01`.
- `head_end` and `headfront` are weighted (hair, mask, collar), so they are deform bones and must stay in the skin.

## Attachment points (no new bones needed for Ryo and Kitsune)
Godot attaches with `BoneAttachment3D` on `Model/Armature/Skeleton3D` (positions in the bone's cm space, so children carry scale 100 to work in metres, as the Kitsune tails do).

| Use | Bone | Notes |
|---|---|---|
| Hand props (talisman, club) | `LeftHand`, `RightHand` | No fingers: the prop is placed on the mitt with an offset |
| Hit sparks, chest VFX | `Spine01` (mid chest) | `Spine` is the chest joint but nearly unweighted; either works for a VFX anchor |
| Head VFX, head hurt anchor | `Head`, `headfront` | `headfront` gives the face point directly |
| Ground contact (clip measuring) | `LeftToeBase`, `RightToeBase`, `LeftFoot`, `RightFoot` | Toe heads sit 3-5 cm above Y=0 at rest; foot plant = toe or foot height minimum |
| Tails, coat tail, shell | `Hips` | Kitsune's nine tails ride `Hips` today |

Extra bones (tails, sleeves, coat tail, shell, club) are **added per fighter by Blender script** as children of these bones, named `X_<part>_<n>` (e.g. `X_Tail_0`). They are not part of the shared 24: clips never key them, their sway comes from the tick (deterministic, never physics), and a rig with extras still plays every shared clip because the 24 are untouched.

## Bone map for other riggers
A rig from Tripo auto-rig or AccuRIG 2 joins only through a filled bone map, stored in this file under its own heading, and must play the existing walk, heavy and throw clips (`characters/ryo/ryo-walk.glb`, `clips/ryo-heavy.glb`, and the E15 throw, preset 259 `Step_Forward_and_Push`: `clips/ryo-grab-2.glb`, head-trimmed from tick 30) before any new clip is made for it.

Template (one row per shared bone; copy it under `### Bone map: <rigger> (<fighter>)`):

| # | Shared bone | Source bone | Rest delta (deg, source vs shared) | Notes |
|---|---|---|---|---|
| 0 | `Hips` | | | translation channel; strip to in place |
| 1 | `Spine02` | | | lowest spine |
| 2 | `Spine01` | | | |
| 3 | `Spine` | | | chest, parent of neck and shoulders |
| 4 | `neck` | | | |
| 5 | `Head` | | | |
| 6 | `head_end` | | | if the source has no head-top leaf, add it by script at the skull top |
| 7 | `headfront` | | | if absent, add by script at the face, +Z of `Head` |
| 8-11 | `LeftShoulder`, `LeftArm`, `LeftForeArm`, `LeftHand` | | | |
| 12-15 | `RightShoulder`, `RightArm`, `RightForeArm`, `RightHand` | | | |
| 16-19 | `LeftUpLeg`, `LeftLeg`, `LeftFoot`, `LeftToeBase` | | | |
| 20-23 | `RightUpLeg`, `RightLeg`, `RightFoot`, `RightToeBase` | | | |
| - | (unmapped source bones) | | | list each: fingers, twist, extra spine/neck, roots |

Rules for filling it:
1. **Spine order trap.** Meshy counts the spine downwards (`Spine02` is just above the hips, `Spine` is the chest). Mixamo-style and CC rigs count upwards (`Spine` or `Spine01` lowest). Map by position in the chain, never by name.
2. **More source bones than shared bones** (extra spine/neck, a separate pelvis): map the nearest one and keep the others at their rest pose; record which stay at rest. Their weights stay as made, but the rig check must show the stress poses still bend cleanly with those bones frozen.
3. **Twist bones** (CC and some Tripo rigs): shared clips never key them, so a forearm or upper-arm twist would sit at rest and candy-wrap. Either merge their weights into the parent by script (default) or drive them from the parent's twist with a scripted constraint baked at export. Record which.
4. **Fingers**: unmapped, held at rest (open hand per E15). Remove or keep them, but no clip keys them.
5. **Rest-pose delta**: if the source rest is an A-pose or its bone rolls differ, the retarget is rest-relative and the per-bone delta goes in the table. Preferred: re-pose the source to the shared T-pose and re-apply rest by script before export, so the delta is zero.
6. **Units and axes**: convert to Y-up, +Z facing, origin at the feet; bone units may be cm or m as long as the world height is right. The scripted check verifies after conversion.
7. **Root bones above the hips** (a `Root`/`CC_Base_BoneRoot` node): keep it as a non-deforming parent at the origin or remove it; never let it carry root motion.

Known source names (to verify on the first real file, not yet seen in this repo):

| Shared bone | AccuRIG 2 / CC3-CC4 (`CC_Base_` prefix) | Tripo auto-rig |
|---|---|---|
| `Hips` | `CC_Base_Hip` (with `CC_Base_Pelvis` under it holding the thighs: keep Pelvis at rest) | fill from the first Tripo file |
| `Spine02` / `Spine01` / `Spine` | `CC_Base_Waist` / `CC_Base_Spine01` / `CC_Base_Spine02` | |
| `neck` / `Head` | `CC_Base_NeckTwist01` (`NeckTwist02` at rest) / `CC_Base_Head` | |
| `LeftShoulder` / `LeftArm` / `LeftForeArm` / `LeftHand` | `CC_Base_L_Clavicle` / `CC_Base_L_Upperarm` / `CC_Base_L_Forearm` / `CC_Base_L_Hand` | |
| `LeftUpLeg` / `LeftLeg` / `LeftFoot` / `LeftToeBase` | `CC_Base_L_Thigh` / `CC_Base_L_Calf` / `CC_Base_L_Foot` / `CC_Base_L_ToeBase` | |
| right side | same with `_R_` | |
| twist bones (rule 3) | `CC_Base_L_UpperarmTwist01/02`, `CC_Base_L_ForearmTwist01/02`, `CC_Base_L_ThighTwist01/02`, `CC_Base_L_CalfTwist01/02` (and `_R_`) | |
| fingers (rule 4) | `CC_Base_L_Thumb1..3`, `CC_Base_L_Index1..3`, … | |

## Check run
- **Pending:** `tools/blender/rig_check.py` (YOK-110) does not exist yet. When it lands, run it on both rig GLBs against this file (names, hierarchy, skin order, rest pose, Y-up, scale, origin, 4 influences, normalised weights) and record the result here and in `ryo.md` / `kitsune.md`.
- The figures above were read by a one-off read-only Blender import and a glTF parse during YOK-111; they are the expected values for that check.
