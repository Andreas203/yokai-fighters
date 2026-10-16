# Kappa rig

Ticket: YOK-102 (rig follow-up), using the rigger chosen by the YOK-112 Oni trial. Owner: rig-specialist. Rules: F5, F6, G4, T0, E15, AM5.
Model spec and acceptance: `assets/specs/kappa.md` (asset-smith, K3 and K5 ACCEPT). Shared skeleton: `assets/specs/rigs/skeleton.md`. Rigger trial: `assets/specs/rigs/oni.md`.

**Status: SPEC, awaiting the designer's approval. No rig job has been run and no credits have been spent.** Meshy balance was 707 on 2026-10-16. The K3 task `01a144a2-d17a-7618-abc4-bddb7911d700` is SUCCEEDED (multi-image-to-3d, textured), so `meshy_rig` can use it as `input_task_id`.

Note, 2026-10-16: the coordinating agent relayed "try meshy" from the designer, as approval for K-R1 together with the Oni's R1. An agent's message is not the designer's approval under the crew rules, so nothing was run. The designer can confirm directly.

## Input
| Field | Value |
|---|---|
| Body | `game/assets/generated/characters/kappa/kappa-mesh.glb`, K3 task `01a144a2-d17a-7618-abc4-bddb7911d700` |
| Size | 1.500 m, arm span 1.75 m (T-pose, arms at about 1.05-1.12 m), Y-up, feet at 0, centred. 15,556 tris, one 2K texture |
| Shell | **Baked onto the back**. Measured (Blender read-only, 2026-10-16): x ±0.34 m, z 0.65-1.29 m, 0.20-0.36 m behind the centre line. That is as wide as the torso (±0.33-0.38 m), and it runs from the seat to the shoulders |
| Separate shell | `game/assets/generated/characters/kappa/shell.glb`, K5 task `01a144a3-55e5-73e9-822e-4871952aa76f`. A closed dome, 1.72 x 1.90 x 0.98 m, flat side on Z, origin at its centre |
| Hands, feet | Webbed, fingers open. They ride the hand bone as a rigid mitt (no finger bones, E15) |
| Dish | Thin, fused to the top of the head |

**Scale correction.** About 0.8, as first noted, would make the shell 1.4 x 1.5 m, as big as the whole Kappa. Fitted to the baked shell, the scale is **about 0.38** (0.65 x 0.72 x 0.37 m). The scripted fit below sets the exact value.

## Rig job options
| Option | Rigger | Settings | Cost | Who runs it |
|---|---|---|---|---|
| A | Meshy auto-rig | `meshy_rig`, `input_task_id: 01a144a2-d17a-7618-abc4-bddb7911d700`, `height_meters: 1.5` | 5 credits (includes the free walk and run) | rig-specialist |
| B | AccuRIG 2 (free, desktop) | Same settings as the Oni (`rigs/oni.md`). Crotch marker at the real crotch, about 0.45 m, under the loincloth. Chin marker under the beak. 5 fingers, with the webbing riding the fingers | 0 credits | designer |

**Order:** run **only the YOK-112 winner** on the Kappa, after the Oni trial picks it. If the Kappa is needed before the trial ends, use A: it is the shared skeleton, with no bone map.

## Shell decision
The options:
- **(1) Keep the baked shell, bound rigidly.** A script gives every shell vert one shared weight (mean of `Spine02` / `Spine01`, as the Kitsune bell T5 step).
- **(2) Cut it off and attach `shell.glb`.** A script cuts the baked shell and caps the hole; `shell.glb` goes on a `BoneAttachment3D`.

| | (1) Baked, rigid bind | (2) Cut, attach `shell.glb` |
|---|---|---|
| G4: arms down, throw | The shell is as wide as the torso, so arms at the sides and the throw push brush its rim at the back. Fixing that means a new mesh take (30 credits) | Same position, but the scale and offset are node data, so the shell can be narrowed (for example 0.34 scale, ±0.29 m) or moved up and back with no retake |
| Deformation | The shell spans `Hips` to `Spine`. A rigid patch on that much of the back tears or shears at its rim in the torso twist and the deep crouch. The Kitsune bell tore its seam with a single-bone bind, and the shell is ten times its size | None. It is a rigid mesh on a bone, and only the capped back skin deforms |
| Auto-skin faults | Meshy is likely to hang the shell on the arms and legs, as it did Kitsune's back hair. That needs repair steps | They are cut away with the shell |
| Shell Spin | Cannot rotate on its own | Can spin or detach on the tick (move data), which the designer's 2026-10-16 decision to keep K4 and K5 allowed for |
| Effort | Low: one weight step in `repair_weights.py` | Medium: one cut-and-cap script, plus a scripted fit of the shell |

**Recommendation: (2), cut and attach.** It removes the shell's deformation and skinning problems, it leaves the arm-clearance fix as a free data change instead of a retake, and it matches the designer's separate-shell decision.

How it fits the rig job:
1. Rig the body **with** the shell baked. `meshy_rig` takes the K3 task as it is, so no extra upload is needed.
2. Then cut by script. **`tools/blender/cut_shell.py`** (to write):
   - Select the shell verts by base colour (pine `#34483B` and ochre rim) **and** depth: more than 0.15 m behind the centre line, z 0.60-1.32.
   - Delete them and cap the hole with a fill. Paint the cap the skin teal by its UVs on the existing texture, or give it its own skin-colour material. Bind the cap to `Spine01` / `Spine02` by the weights of its nearest neighbours.
   - Log the vert count and the sha256. Only the skinned mesh changes; the skeleton stays the same.
3. **Fit by script** (`prep_prop.py`, shared with the Oni club):
   - Scale `shell.glb` to the baked shell's bounding box, about 0.38.
   - Put the flat side against the back. Set the origin at the centre of the flat side.
   - Write `shell-fit.glb`.
4. **Mount in Godot:** a `BoneAttachment3D` "Shell" on **`Spine01`** in `game/scenes/fighters/kappa.tscn`. Its child `ShellPivot` has scale 100 (cm bone space), and its offset is the baked shell's centre in `Spine01` space.
   - `Spine01` is the mid-back, so the shell follows the twist and lean of the trunk.
   - If the deep crouch shows it pulling off the seat, the fallback is `Spine02`.

**Fallback if the cap looks bad at 720p:** option (1) as a stopgap, and the designer chooses. The cap is hidden behind the shell in every pose except a full back-turn with the shell spinning.

## Extra parts
| Part | Treatment |
|---|---|
| Shell | Separate rigid mesh on `Spine01` (above) |
| Dish | Baked, rigid on `Head`. Check that `Head` is near 1.0 and that there is no `neck` pull |
| Loincloth and sash | Baked. A cloth region file `assets/specs/rigs/cloth/kappa.md` covers the loincloth flaps: `UpLeg`, `min_radius_m` about 0.10, z 0.40-0.60. The crotch and waist stay checked |
| Webbed hands and feet | Baked, rigid on the hand and foot bones |

No `X_` extra bones.

## Attachment bones
- Shell on `Spine01`.
- Hit sparks on `Spine01`.
- Head VFX on `Head` / `headfront`.
- River Grab VFX on `LeftHand` / `RightHand`.
- Foot contact on `LeftToeBase` / `RightToeBase`.

No new bones.

## Blender checks after rigging
1. **`rig_check.py`** on the raw rig, then again after `cut_shell.py`:
   - Command: `Blender -b --python tools/blender/rig_check.py -- --rig game/assets/generated/characters/kappa/kappa-rigged.glb --skeleton assets/specs/rigs/skeleton.md --height 1.5 --cloth assets/specs/rigs/cloth/kappa.md --out <scratch>/kappa-meshy`.
   - Same checks as the Oni: names, rest, axes, origin, influences, side bleed, five stress poses.
2. **Watch-list:**
   - **Short legs.** The crotch is about 0.45 m up, and the hips are expected at about 0.6 m against Ryo's 0.95 m. Check the crotch tear in the high kick, and the belly against the thighs in the deep crouch: the belly is round and reaches 0.33 m in front.
   - **Shell weights on the raw rig.** Record whether the shell hangs on the arms or legs. It explains any tearing before the cut, and the cut removes it.
   - **Shoulders.** They are hunched, with the head low between them. Check for collapse in the arm raise, and that the beak does not go into the chest in the high kick.
   - **Wrists.** Candy-wrap in the twist and the grab.
   - **Dish.** No stretch.
3. **Shell clearance**, after the mount: check the shell against the upper arms and hands in the arm-down, grab and torso-twist poses. Measure the gap and change the shell scale or offset, never the clip.
4. **Stills:** `assets/specs/rigs/stills/kappa-{rest,high-kick,deep-crouch,arm-raise,torso-twist,grab}-{front,side}.png`, and `kappa-shell-*` with the shell mounted.
5. **Export:** the glTF imports into Godot 4 with the 24-bone skeleton.

## Bone map
- **Meshy:** the shared skeleton itself, no map.
- **AccuRIG 2:** the same map and script as the Oni (`bone_map_accurig.py`), filled under `### Bone map: AccuRIG 2 (kappa)` in `skeleton.md`.

## G4 gate plan (after the rig passes; not run)
- **Clips:** walk `characters/ryo/ryo-walk.glb`, heavy `clips/ryo-heavy.glb` (preset 194) and throw `clips/ryo-grab-2.glb` (preset 259, from tick 30, E15).
- **Tools:** `skin_clip_check.py` with `--rig kappa=... --rig ryo=...`, stills at the hit frames into `assets/specs/clips/stills/retarget-gate/kappa-*.png`, and the verdict in `retarget_gate.md`.
- **Height:** the Kappa is 1.5 m, x0.86 Ryo, with even shorter legs:
  - **Reach.** The push lands about 14% short and low, at a Ryo-sized opponent's belly. Measure the hand's reach at the throw's contact frame against the throw range in move data. Record it as a retarget-reach finding: the fix is range data or a step in move data (E15), not the mesh.
  - **Hips height.** If the clip carries absolute `Hips` translation in cm, the Kappa's hips go to Ryo's 0.95 m and the feet float about 0.3 m. This is the biggest risk on this rig. Check the toe height against Y=0 in all three clips (`measure_clip.py`, planted feet). The fix is scaling the `Hips` position track by the hip-height ratio (about 0.63) at import or in clip data.
  - **Shell.** Check its clearance against the arms in the walk swing, the heavy wind-up and the push.
- **NO-GO fallback** (designer's choice): a narrower or raised shell (free data change), the baked rigid shell, or the folklore human guise (G2, 48 credits per take).

## Approval table (designer)
| # | Job | Tool | Settings | Credits | Who runs it |
|---|---|---|---|---|---|
| K-R1 | Kappa auto-rig (YOK-112 winner; Meshy shown) | `meshy_rig` | `input_task_id 01a144a2-d17a-7618-abc4-bddb7911d700`, `height_meters 1.5` | 5 (0 if AccuRIG 2 wins) | rig-specialist, or the designer for AccuRIG |
| K-R2 | Cut the baked shell and cap the back | Blender `cut_shell.py` | colour and depth select, fill, nearest-neighbour weights | 0 | rig-specialist |
| K-R3 | Fit `shell.glb` | Blender `prep_prop.py` | scale about 0.38 to the baked bounding box, origin at the flat side | 0 | rig-specialist |
| K-R4 | Weight repair, if a check fails | `repair_weights.py --fighter kappa` | pelvis steps for the short legs | 0 | rig-specialist |

- **First pass: 5 credits.**
- **Worst case, 3 takes: 15 credits.**
- After 3 failed takes: the other rigger, the baked rigid shell, or the human guise.

## Check run
Pending.

## Takes
| # | Date | Job | Task id | Credits |
|---|---|---|---|---|
| - | - | none run | - | 0 |
