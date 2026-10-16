# Kappa rig

Ticket: YOK-102 (rig follow-up), on the rigger the YOK-112 trial chose (Meshy). Owner: rig-specialist. Rules: F5, F6, G4, T0, E15, AM5.
Model spec and acceptance: `assets/specs/kappa.md` (asset-smith, K3 and K5 ACCEPT). Shared skeleton: `assets/specs/rigs/skeleton.md`. Trial: `assets/specs/rigs/oni.md`.

**Verdict: ACCEPT (rig, weights, shell), 2026-10-16.** The rest-pose check still flags the 18-19 degree leg splay of the generated stance. The feet sink 3-8 cm on the shared clips (G4 WARN).

## Status 2026-10-16 (repair, props, scene, G4)
This section supersedes the take-1 verdict and the work list further down.

**Build chain.** All free and scripted. `kappa-rigged.glb` is now the game file, rebuilt from the raw take-1 rig (at `d7b0999`) by:
1. `cut_shell.py --colour-band 0.0,0.45,0.75,1.36`;
2. `repair_weights.py --fighter kappa`.

Two runs give the same bytes. The old `kappa-rigged.glb (shell cut)` has been removed. Logs: `logs/kappa-shellcut.json`, `logs/kappa-repair.json`, `logs/kappa-rig-check.json`.

**Cut.**
- The depth box behind y 0.17 m, as before.
- A colour band (new): every shell-coloured vert (dark pine or ochre) behind the back in z 0.75-1.36. It removes the ochre rim strip the depth cut left at the flanks.
- 1,421 verts removed, 204 cap triangles in skin teal.
- The rope belt's back middle went with the shell. It was always under the shell.

**Repair (K-R4).** Ryo's pelvis rules are off for the Kappa. Steps:
- T2 leg strip and T6 trunk-by-height over the whole torso, from 0.58 to 0.92 m;
- C: crotch verts within |x| 0.12 m move from `Leg` to `UpLeg`;
- K: within 0.20 m of each knee, `UpLeg`/`Leg` split by height over ±0.05 m;
- shin clean: the calf backs' `UpLeg` weight goes to `Leg` below the knee, over 0.14 m.

The loincloth is cloth (`cloth/kappa.md`).

| Check | Raw (shell baked) | Repaired, shell cut |
|---|---|---|
| skeleton, scale, axes, origin | PASS | PASS |
| rest pose | FAIL (legs splayed 18-19 deg, limit 15) | FAIL (same: the mesh's stance, not a weight fault) |
| weights | WARN, 1,777 arm-and-leg bleed | **PASS**, 0 bleed, 0 cross-side |
| kick / crouch / arm raise / twist / grab | FAIL 134 / FAIL 296 + shin collapse / FAIL 68 / FAIL 71 / WARN 28 | **WARN 8 / WARN 29 / WARN 22 / PASS 0 / WARN 6** (limit 55, no collapse) |

Stills: `stills/kappa-repaired-*-{front,side}.png`, `stills/kappa-cloth-mask-*.png`, `stills/kappa-shellcut-cap-back.png`.

**Shell.**
- `prep_prop.py --mode fit`, target box x ±0.34, depth -0.04 to 0.40 m, z 0.74-1.32 m. The result is 0.68 x 0.44 x 0.58 m, at scale (0.39, 0.45, 0.31).
- The dome is now 0.44 m deep, against 0.27 before. Its underside sinks 4 cm into the back, so no gap shows from the side. Its lower edge sits above the seat, at 0.74 m.
- Mount on **`Spine01`**. `Spine02` was compared: it matches everywhere except the torso twist, where it is worse (586 against 313 shell verts inside, gap up to 14 cm against 9).
- Log: `logs/kappa-shell-fit.json`. Clearance: `logs/kappa-shell-clearance.json`.
- Stills: `stills/kappa-shell-{rest,high-kick,deep-crouch,arm-raise,torso-twist,grab,arms-down}-{front,side,back}.png`.
- The deep crouch no longer flaps: 2 verts, 2.5 cm. The figures left in the kick, twist, arm raise and arms-down are the sunk underside meeting the shoulder blades and back, hidden under the dome in every still.

**Scene.** `game/scenes/fighters/kappa.tscn`:
- `FighterModel` with Fighter `kappa`;
- `Shell`, a BoneAttachment3D on `Spine01` → `ShellPivot` → `shell-fit.glb`.

Godot 4 headless loads it with 24 bones and the shell exactly on the target box. **Not yet wired in `FighterAnimSet`** (gameplay-programmer).

**G4 gate** (full report in `assets/specs/clips/retarget_gate.md`):
- Body: **GO**. Skin worst failing edges per frame are 11 / 42 / 17 (walk / heavy / throw).
- Shell: **GO**, no visible clipping.
- Feet: with the hip fix (0.775) they sink 3.0-7.8 cm below the rest contact (WARN, from the splayed stance). Fixes, open: a scripted leg re-pose to the shared T-pose (free; changes the bind, so it needs the designer's approval), or a per-rig ground offset in code.
- Reach: the throw push lands at 0.56 m forward, 0.91 m high, against Ryo's 0.51 m at 1.06 m.
## Designer decisions (2026-10-16, relayed by the main session)
- **Rigger:** Meshy ("try meshy"). The designer approved K-R1 directly to the main session, which ran it. AccuRIG 2 is the fallback.
- **Shell:** cut the baked shell off by script and attach `shell.glb` on a spine BoneAttachment3D. The designer gave the scale as about 0.38, from the earlier measurement. The final fit is per axis, and its height matches that (below).
- **Human guise (G2): not used.** If G4 fails, the fixes are weight repair, a simpler part, or AccuRIG 2.

## Rig (take 1)
| Field | Value |
|---|---|
| Rigger | Meshy auto-rig (`meshy_rig`, `height_meters: 1.5`), input K3 `01a144a2-d17a-7618-abc4-bddb7911d700` |
| Rig task id | `01a144ad-ca9c-7370-b2e3-c7b2f9e3aa11` (SUCCEEDED, 5 credits, run by the main session) |
| Raw file | `game/assets/generated/characters/kappa/kappa-rigged.glb`, sha256 `43456030534e…`. Free walk and run: `kappa-walk.glb`, `kappa-run.glb` |
| Game file | `game/assets/generated/characters/kappa/kappa-rigged.glb (shell cut)`, the raw rig with the shell cut and the back capped. sha256 `78ed9719285d…`, the same on a rerun |
| Import | `godot --headless --path game --import` runs clean for every Kappa file. Godot sees 24 bones |
| Skeleton | The shared 24 bones: names, parents and skin order identical (PASS), no bone map |
| Height, frame | 1.500 m, Y-up, +Z facing, feet at 0, centred. Bones in cm under the 0.01 `Armature`. `Hips` rest Y 73.5 cm (Ryo 94.9) |
| Rest | **FAIL on leg splay**: the legs rest 18.3 / 19.0 deg apart (limit 15), from the frog-like wide stance the mesh was generated in. Arms 8.8 deg below level. rest == bind |
| Mesh | `char1`, 15,557 verts raw and 14,439 after the cut (glTF counts, split at UV seams) |
| Weights (raw) | Max 4 influences, normalised, 0 unweighted, 0 cross-side. **1,777 verts with arm-and-leg bleed**: the baked shell and the torso hang on the arms and the legs together |
| Fingers | Webbed, open, no finger bones: they ride the hand rigid (E15) |

### The leg splay (rest-pose FAIL)
- A rig retake cannot change the splay, because it is the stance the mesh was generated in.
- The game plays a clip's local rotations onto each rig. The Kappa's thighs will therefore be turned to Ryo's leg angles, about 14 deg narrower than his own rest.
- Watch the crotch and the inner thighs for squeeze on the gate clips.
- If they squeeze, there are two fixes:
  - a scripted re-pose of the legs to the shared T-pose with the rest re-applied (free; changes the bind, so the designer should approve it);
  - a new mesh take in a narrower T-pose (about 35 credits with the rig).
- Not proposed until G4 shows it.

## Check run (2026-10-16)
Command: `Blender -b --python tools/blender/rig_check.py -- --rig <rig> --skeleton assets/specs/rigs/skeleton.md --height 1.5 --out <dir>`. There is no `--cloth` file yet.

A pose FAILs above 0.2% of its edges (59 raw, 55 cut) or on a collapsed bone.

| Pose | Raw (shell baked) | Shell cut off (`kappa-rigged.glb (shell cut)`) |
|---|---|---|
| high kick | **FAIL** 134, max 13.6x (`RightShoulder` 56, `UpLeg` 82) | **FAIL** 71, max 5.1x (`UpLeg` 82, `RightShoulder` 10) |
| deep crouch | **FAIL** 296, max 17.5x; `LeftLeg` (0.80, p5 0.34) and `RightLeg` (0.78, p5 0.19) collapse | **FAIL** 165, max 10.1x; the same shin collapse |
| arm raise | **FAIL** 68, max 7.8x (`UpLeg` 65) | **FAIL** 60, max 7.8x (`UpLeg` 65) |
| torso twist | **FAIL** 71, max 6.8x | WARN 28 |
| grab | WARN 28, max 6.0x | WARN 13 |
| weights | WARN, 1,777 bleed | WARN, 1,243 bleed |

What the stills show:
- **Raw crouch:** the baked shell crumples and bends with the legs.
- **After the cut:** the body still tears at the hip crease in the kick and crouch.
- **Arm raise:** `UpLeg` weight sits high on the torso and drags the belly sides.
- **Shins:** they collapse in the deep crouch.
- **Dish:** rigid. The beak clears the chest.

**K-R4, the proposed repair (free, needs the designer's go-ahead):**
- A `FIGHTERS["kappa"]` entry in `repair_weights.py`, run on `kappa-rigged.glb (shell cut)`.
- Kitsune's T1 arm strip, T2 leg strip and T4 bleed steps over the torso. This also covers the cap and the rim ring left on the back.
- Ryo's pelvis steps 1-3, with the band scaled to the Kappa's hips at 0.735 m.
- A smoothing pass on the `Leg` columns for the shin collapse.
- `cloth/kappa.md` for the loincloth flaps (`UpLeg`, `min_radius_m` about 0.10, z 0.40-0.60).
- Target: WARN on every pose.

Stills:
- `assets/specs/rigs/stills/kappa-raw-{rest,high-kick,deep-crouch,arm-raise,torso-twist,grab}-{front,side}.png`
- `kappa-noshell-est-{high-kick,deep-crouch,arm-raise}-{front,side}.png`: an estimate run before the script, with the same box and the same figures within 2 edges.
- `kappa-shellcut-cap-back.png`

## Shell (done, 2026-10-16)
### Cut
`tools/blender/cut_shell.py`, log `assets/specs/rigs/logs/kappa-shellcut.json`.
- The verts are welded at the UV seams first, so islands and holes are real.
- It removes the 647 welded verts inside the box: behind y 0.17 m, z 0.58-1.29 m, |x| < 0.37 m.
- That leaves one hole of 104 edges, filled with 102 triangles. The fill adds no new verts, so every cap vert keeps its own skin weights.
- The cap's UVs point at the teal skin on the back of the upper arm.
- Cutting deeper (0.13 or 0.11 m) opens a see-through hole: the mesh has no back surface under the shell, because the shell was the back. So 0.17 m stays, and part of the ochre rim ring stays on the body at its edge.

### Fit
`tools/blender/prep_prop.py --mode fit`, log `assets/specs/rigs/logs/kappa-shell-fit.json`, file `game/assets/generated/characters/kappa/shell-fit.glb` (sha256 `be0f4409e149…`).
- The long axis is turned upright, and the dome faces behind the Kappa.
- It is scaled per axis to the outline of the old shell including its rim: x ±0.39, depth 0.10-0.37, z 0.63-1.36 m. The scale is **(0.453 across, 0.275 deep, 0.385 tall)**, giving a 0.78 x 0.27 x 0.73 m shell.
- The height matches the designer's "about 0.38".
- The width needs 0.45 to cover the rim ring left on the body. At a uniform 0.38 the shell would be 0.65 m wide and the ring would show at the sides.
- The pivot is the centre of the flat side.

### Mount (for the fighter scene, owned by gameplay-programmer)
`BoneAttachment3D` on **`Spine01`** → child `Node3D` → `shell-fit.glb`, with this child transform:
```
Transform3D(100.0, 0.0, -7e-06, -0.0, 99.9935, 1.140931, 7e-06, -1.140931, 99.9935, 3e-06, 1.532544, -16.192886)
```
**Checked in Godot 4** (headless): the shell lands exactly on the target box. Its AABB is (-0.39, 0.63, -0.37) m with size (0.78, 0.73, 0.27).

### Clearance (`prop_clearance.py`, on the unrepaired cut rig)
The figures count shell verts that go inside the body in a pose but were not inside at rest. At rest the underside sits against the back by design: 721 verts, up to 6.8 cm.

| Pose | New verts inside | Worst depth |
|---|---|---|
| arm raise | 8 | 3 cm |
| torso twist | 227 | 8 cm |
| grab | 155 | 14 cm |
| arms down | 279 | 16 cm, the rim into the upper arms |
| high kick | 328 | 19 cm |
| deep crouch | 646 | 27 cm, the lower shell into the seat and thighs |

What the stills show:
- **Rest, back view:** the shell sits cleanly and covers the rim ring.
- **Arms down:** the arms pass in front of the rim, and from the front the overlap is hidden behind them.
- **Deep crouch:** the back skin, still weighted to the arms and legs, pulls away from the shell, and the shell's lower edge goes into the seat.

Most of this is the unrepaired weights moving the back. So: re-run after K-R4, then compare a `Spine02` mount for the crouch. If the arms-down rim still shows, narrow the shell (x 0.40) and extend the cut to match.

Stills: `assets/specs/rigs/stills/kappa-shell-{rest-back,arms-down-front,arms-down-side,deep-crouch-side,high-kick-side,grab-front,torso-twist-back}.png`.

## Attachment bones
- Shell on `Spine01`.
- Hit sparks on `Spine01`.
- Head VFX on `Head` / `headfront`.
- River Grab VFX on `LeftHand` / `RightHand`.
- Foot contact on `LeftToeBase` / `RightToeBase`.

No new bones.

## G4 gate plan (not run; the rig is not ready yet)
- **Clips:** the walk `characters/ryo/ryo-walk.glb`, the heavy `clips/ryo-heavy.glb` (preset 194) and the throw `clips/ryo-grab-2.glb` (preset 259, from tick 30, E15).
- **Tools:** `skin_clip_check.py` with `--rig kappa=kappa-rigged.glb (shell cut) --rig ryo=...`, plus `prop_clearance.py` with the shell. Stills go to `assets/specs/clips/stills/retarget-gate/kappa-*.png`, and the verdict to `retarget_gate.md`.
- **Hip height: a code fix, needed before G4.**
  - `FighterModel.BuildLibrary` keeps the clip's `Hips` Y, Ryo's 94.9 cm. The Kappa's rest is 73.5 cm, so it would **float 21 cm**.
  - Fix: scale the `Hips` Y keys by `rest.Y / clipRest.Y` = 73.5 / 94.9 = **0.77**. This is the same code change as the Oni's and belongs to gameplay-programmer.
- **Reach.** The push lands about **14% short and low** (1.5 / 1.75), at a Ryo-sized opponent's belly. Measure the hand at the throw's contact frame against the throw range in move data. Any gap is fixed in move data (range, step, E15), not on the mesh.
- **Legs.** Crotch squeeze from the splay (above).
- **Shell.** Clearance against the arms in the walk swing, the heavy wind-up and the push.
- **NO-GO fallback** (designer's choice): weight repair, a simpler shell (narrower or raised, a free data change), or AccuRIG 2. The human guise is out.

## Work list
| # | Item | Tool | Credits | State |
|---|---|---|---|---|
| K-R1 | Meshy auto-rig, 1.5 m | `meshy_rig` | 5 | **Done** (main session) |
| K-R2 | Cut the shell, cap the back | `cut_shell.py` | 0 | **Done** |
| K-R3 | Fit and mount `shell.glb`, clearance | `prep_prop.py`, `prop_clearance.py` | 0 | **Done**. Re-run the clearance after K-R4 |
| K-R4 | Weight repair to WARN | `repair_weights.py --fighter kappa` (new entry), `cloth/kappa.md` | 0 | **Proposed, needs go-ahead** |
| K-R5 | `Hips` Y scaling in `FighterModel.BuildLibrary` (shared with the Oni's R6) | code (gameplay-programmer) | 0 | Needed before G4 |
| K-R6 | Fighter scene: model, shell mount | `kappa.tscn` (gameplay-programmer) | 0 | After K-R4 |
| K-R7 | G4 gate | `skin_clip_check.py`, `prop_clearance.py` | 0 | After K-R4 and K-R5 |

Spent on the Kappa rig: 5 credits (K-R1). Worst case if K-R4 fails and Meshy is retried: 2 more takes, 10 credits. Then AccuRIG 2 (free).

## Takes
| # | Date | Job | Task id | Credits |
|---|---|---|---|---|
| 1 | 2026-10-16 | `meshy_rig`, 1.5 m (run by the main session on the designer's approval) | `01a144ad-ca9c-7370-b2e3-c7b2f9e3aa11` | 5 |
