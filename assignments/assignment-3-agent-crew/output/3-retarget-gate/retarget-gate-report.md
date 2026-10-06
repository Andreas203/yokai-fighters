# YOK-31 Retarget gate (G4): walk, heavy punch, generic grab on Ryo and Kitsune

Status: take 1 and grab take 2 run and measured, 2026-10-06 (see "Grab take 2" below; it supersedes the line "Both characters still owe a passing grab"). Designer approved 12 credits (4 x `meshy_animate`); spent 12; balance 959 -> 947. Walks reused free. No retakes, no `data/clips/` entries (those follow the gate decision). Spec: `assets/specs/clips/yok-31-retarget-gate.md`.

The GO / NO-GO lines below are **recommendations**; the designer makes the call.

## Designer decision (2026-10-06)
Gate **CLOSED**.
- **Ryo: GO.**
- **Kitsune: GO.** The sleeve flare on the heavy is accepted.
- **T01 (generic throw):** both rigs use the Meshy "Step_Forward_and_Push" (preset 259) shove, head-trimmed from tick 30, with its forward step carried in move data (rules.md E15). Kitsune's take is still to be generated when clips are picked in YOK-42.
- **Open-hand strikes accepted:** the rig has no finger bones; no code-posed fists (E15).

## Rigs and presets
| Fighter | Rig task id | Height |
|---|---|---|
| Ryo (rebuilt) | `01a10e47-857e-73a0-9f7a-cd74795198fb` | 1.75 m |
| Kitsune (rebuilt) | `01a10e47-91f6-709b-bd34-8884f8cbabbb` | 1.65 m |

The MCP cannot list the animation library, so the ids come from the public Meshy animation-library table (docs.meshy.ai/en/api/animation-library).

| Move | Action id | Name | Why chosen |
|---|---|---|---|
| Heavy (N03) | **194** | `Right_Uppercut_from_Guard` (Fighting/Punching) | Big wind-up hook/uppercut from a guard stance, which is what N03 asks for (clip_needs: early frames work as anti-air). 195 `Right_Upper_Hook_from_Guard` was the alternative |
| Grab (T01) | **389** | `Grip_and_Throw_Down` (BodyMovements/Acting) | Closest "grip then throw" name in the library. Other candidates: 421 `Over_Shoulder_Throw`, 239 `Crouch_Pull_and_Throw`, 259 `Step_Forward_and_Push`. There is no preview, so this was a judgement from the name; it turned out wrong (see T1/T2) |

## Per-take results
Ticks are 60 per second. Clips are exported at 30 fps keys and resampled by linear/slerp interpolation. Measured from the GLBs with `numpy` (FK + linear-blend skinning of the 15k-tri mesh, every tick, mesh checks every 2nd tick) and rendered stills in Godot 4.7.2 (see the end).

| Take | Fighter | Task id | Action | Credits | Frames @60 | Verdict |
|---|---|---|---|---|---|---|
| W1 walk | Ryo | free rig walk (`ryo-walk.glb`, `walking_man`) | n/a | 0 | 64 + loop key (32 keys, 1.067 s) | **PASS** |
| W2 walk | Kitsune | free rig walk (`kitsune-walk.glb`) | n/a | 0 | 64 + loop key | **WARN** (hakama/sleeve stretch) |
| H1 heavy | Ryo | `01a10e5b-45a5-7037-98b1-83de6aa9f9e9` | 194 | 3 | 63 (31 keys, 1.033 s) | **PASS** with a head-trim and a 3 mm-over foot figure (see notes) |
| H2 heavy | Kitsune | `01a10e5b-4763-7086-95f6-9b05c342b132` | 194 | 3 | 63 | **WARN** (sleeve stretch on contact frames) |
| T1 grab | Ryo | `01a10e5b-490c-719f-885b-079e3172a818` | 389 | 3 | 283 (141 keys, 4.7 s) | **FAIL** (wrong motion, 9x too long) |
| T2 grab | Kitsune | `01a10e5b-4a7d-77c0-902a-36f5aa60a3b2` | 389 | 3 | 283 | **FAIL** (same, plus hair touches hakama) |

GLBs: `game/assets/generated/clips/{ryo,kitsune}-{heavy,grab}.glb`. Walks stay in `game/assets/generated/characters/{ryo,kitsune}/`.

### Measurements
| Measure (criterion) | W1 Ryo walk | W2 Kit walk | H1 Ryo heavy | H2 Kit heavy | T1 Ryo grab | T2 Kit grab |
|---|---|---|---|---|---|---|
| Root drift, end - start (xz) | 0.0 cm | 0.0 cm | 0.5 cm | 0.5 cm | 11 cm | 10 cm |
| Root sway range xz / y | 5.6 x 4.6 / 6.8 cm | 5.1 x 4.2 / 6.2 cm | 5.5 x 11.8 / 4.1 cm | 5.0 x 10.8 / 3.8 cm | 17 x 11 / 5.4 cm | 16 x 10 / 4.9 cm |
| Loop pop (y, first vs last) | 0 | 0 | 0 | 0 | 0 | 0 |
| Planted-foot shift, raw clip (L / R, limit 3 cm) | in-place treadmill, see below | same | 3.1 / 2.4 cm | 2.4 / 2.4 cm | 10.6 / 10.8 cm | 10.1 / 9.8 cm |
| Planted-foot shift if root sway is stripped (L / R) | n/a | n/a | 10.1 / 6.4 cm | 8.3 / 6.6 cm | 7.9 / 16.9 cm | 6.0 / 17.2 cm |
| Max joint rotation per tick (limit 30 deg) | 10.9 (LeftLeg) | 10.9 | 12.4 (LeftArm), striking arm 12.7 | 12.4 | 12.7 (RightForeArm) | 12.7 |
| Contact (target heavy 8-14, grab by 6) | n/a | n/a | peak hand speed 3.7 m/s at tick 33; full reach tick 42; reach >= 90% ticks 40-52 | same (3.55 m/s, tick 33; reach tick 42) | no reach; see notes | no reach |
| Edges stretched > 2x bind, ever (max ratio) | 55 (3.7x) | **563 (7.3x)** | 137 (3.8x) | **843 (11.7x)** | 89 (3.4x) | **603 (11.4x)** |
| ...of which sleeve / hakama+hips | 8 / 40 | 146 / 400 | 50 / 55 | 434 / 372 | 54 / 5 | 341 / 232 |
| Arm verts inside torso core (bind baseline -> clip max, of arm total) | 0 -> 0 of 5,366 | 174 -> 181 of 7,213 | 0 -> 10 (0.2%) | 174 -> 368 (+2.7%, tick 28) | 0 -> 17 | 174 -> 431 (+3.6%, tick 20) |
| Leg vs other-leg axis, min (bind) | 6.1 cm (9.4) | 5.7 cm (9.9) | 9.3 (9.4) | 9.2 (9.9) | 9.3 | 10.3 |
| Hair to hakama, min (bind 4.4 cm) | n/a | 4.1 cm | n/a | 3.4 cm | n/a | **0.3 cm at tick 132** |
| Hips rotation swing (yaw / pitch / roll, deg) | n/a | -4..7 / -1..4 / -4..4 | n/a | **-43..14 / -7..29 / -21..8** | n/a | -20..11 / -7..13 / -12..0 |

### Notes per take
**W1 Ryo walk: PASS.** The free walk is an in-place treadmill: no root motion, loop closes with no pop. Stance-foot ground speed is constant at 1.39 m/s (left, std 0.04; right 1.35 with a brief dip at foot strike), so if the walk speed in move data is 1.35-1.4 m/s (about 0.023 m per tick) feet will not slide. The "planted-foot shift" in the table is by design for a treadmill and is only a slide if game speed differs. No sleeve, coat or leg clipping in stills. Neutral walk, not a guarded walk (that needs a separate 3-credit take per rig if wanted).

**W2 Kitsune walk: WARN.** Same clip, same timing and stance speed (1.39 m/s). The hakama holds together in stills: no leg poke-through, no tear, pleats pinch a little on the leg lift (ticks 44-48). But stretched edges are 10x Ryo's (563 vs 55; 400 of them hakama/hips, 146 sleeve; worst 7.3x). Hair stays 4.1 cm from the skirt. Tail proxy (below) clear of legs and hakama.

**H1 Ryo heavy: PASS (needs a head-trim).** A real wind-up: the strike hand drops to its lowest point at about tick 26 (hips drop 3 cm and rotate), whips up and forward (peak 3.7 m/s at tick 33) and reaches full extension at tick 42, then returns to guard by tick 62. Raw, the contact is at tick 38-42, outside the 8-14 window. Trimming the first 28 ticks (Movesmith's decision, not a hand-keyed edit) gives 35 ticks with contact visible at ticks 38-42 (startup 10, active 4, recovery 21), which matches the design 10/4/20 almost exactly. Cost of the trim: the clip starts mid-wind-up, so the visible anticipation is about 10 ticks of the hand dropping/accelerating, which is short. Planted-foot shift is 3.1 cm left (limit 3 cm, 1 mm over, WARN-minor). Do not strip the pelvis sway (stripping would make the feet shift 6-10 cm): net travel is only 0.5 cm, so keep the clip as is. Fingers: the Meshy rig has no finger bones, so the "fist" is an open mitt; the strike reads as a palm/hand swing, not a punch. This is true for every clip on this rig.

**H2 Kitsune heavy: WARN.** Same timing as H1. Stills (ticks 24-42) show the right haori sleeve flaring open into a large dark-red inside-out cuff across the chest and under the mask (visible interior at ticks 34-42 front view), and the left sleeve folding into the haori. Sleeve edges stretched over 2x: 434 (Ryo's whole sleeve+cuff: 50), max 11.7x, and 368 sleeve vertices inside the torso core (+194 over bind). The hakama stays coherent in the wide stance (no tear or pinch at the front pleats). Hair clears the hakama (3.4 cm min vs 4.4 bind). Hip twist is large (yaw to -43 deg, pitch +29, roll -21), which is the stress case for the tail mounts. Reads as sleeve clipping at the contact frames, so not a clean pass; needs a designer look at `docs/assets/retarget-gate/kitsune-heavy.png`.

**T1/T2 grab: FAIL, both rigs.** `Grip_and_Throw_Down` is not a forward grab. It is a one-armed gesture: the right arm goes out to the side then overhead (hand up at ticks 54-190), holds for about 2.3 s, then swings down hard (peak hand speed 5.8 m/s at tick 209) and rests. Total 4.7 s (283 ticks) against a 30-tick target. There is no forward reach and no hands-closed moment by tick 6. Not usable even with a trim: the only fast part (ticks 198-222) is a downward swing, not a grab. Root drift 11 cm and 17 cm planted-foot shift on the right foot if the sway is stripped. The retarget itself behaves: Ryo has no meaningful clipping (17 arm verts, 3.4x max stretch); Kitsune's hair touches her hakama (3 mm at tick 132 while she leans) and sleeve stretch is again 6x Ryo's. Wrong preset, not a retarget failure.

### Kitsune tail attachments
Method: no tail mount exists in the game yet (no scene), so a proxy was rendered: `tail.glb` x9 on a `BoneAttachment3D` at `Hips`, scaled to 0.5 m, fanned +-55 degrees behind and up. The proxy is the stills' tan paddles (texture not applied), not the final look. Findings: (1) the attachment stays seated on the hip through every clip (rigid on the bone, no gap or flip); (2) tails stay clear of the legs and the hakama in the walk, heavy twist (-43 deg yaw) and grab lean; (3) tails overlap the hanging hair at the mount in every pose, including the neutral one at tick 0, because the rig's hair reaches hip height behind the back. This is a static layout issue (mount offset, tail root position), not caused by a clip, and it is fixable in Godot data (move the root back about 5-10 cm or raise it) - WARN, not a failure. (4) In the heavy and grab leans the tails also dip into the back of the haori by a few centimetres; a smaller fan or higher root cures it. Tail sway was not simulated (no sway code exists yet).

## Recommendation (designer decides)

| Character | Recommendation | Reasons |
|---|---|---|
| Ryo | **GO** (provisional until a grab take passes) | Walk and heavy retarget cleanly: no clipping of coat, sleeves or legs in any still, no jitter (max 12.7 deg per tick), stable timing. The heavy needs a 28-tick head-trim to land in the window. The only fail is the grab, and that is a wrong preset, not a retargeting problem |
| Kitsune | **CONDITIONAL GO** | The two risks asset-smith flagged did not materialise at the level of a fail: the wide hakama holds (no leg poke-through or tear in walk, heavy or grab) and the hair clears it except at the deepest lean (3 mm in the grab). The new problem is the wide haori sleeves, which stretch 6-10x more than Ryo's and flare inside out on the heavy's contact frames. If the designer judges the stills at `kitsune-heavy.png` acceptable (the play camera is distant and the pose is brief), GO; otherwise use the cheapest fix below. Tail mount overlaps the hair (data fix) |

Both characters still owe a passing grab (T01) before the gate can fully close.

### Cheapest fixes
| Problem | Fix | Cost |
|---|---|---|
| Grab fails (both) | Retake with another preset, same rigs. Try one rig first: Ryo with 259 `Step_Forward_and_Push` (fast forward reach; a shove-grab, the F5 "mechanically equivalent" route) or 421 `Over_Shoulder_Throw`; if it fits, run Kitsune. Take 2 of 3 for T01; needs new approval | 3 per probe, 6 for both rigs. Worst case two presets on both rigs = 12 |
| Kitsune sleeves look bad | Tie the sleeves (tied-back tasuki-style) or shorten them: new Kitsune rig spec ("Take 3") | about 44 per take (turnaround 9, mesh 30, rig 5); the heavy and grab clips must then be re-applied (3 each, 6 credits more) |
| Kitsune sleeves still bad / hakama fails later | Folklore human guise fallback (tail-less, plain kimono, mask kept) | about 44 per take for K1-K3, not in any total |
| Tails overlap the hair | Godot-side: move the tail root back / raise it, narrow the fan; or tie the hair up in the same Kitsune "Take 3" | 0 (data) |
| Heavy contact late (tick 38-42 raw) | Movesmith head-trims 28 ticks (data, no credits) or retake with 195 `Right_Upper_Hook_from_Guard`, which has not been previewed | 0 / 6 |

## Grab take 2 (T01, take 2 of 3), 2026-10-06
Designer approved up to 12 credits; **spent 6; balance 947 -> 941**. Ryo only: both candidate presets failed on Ryo, so by the agreed order the Kitsune was not run (6 credits saved). GLBs: `game/assets/generated/clips/ryo-grab-2.glb`, `ryo-grab-3.glb` (downloaded from the returned URLs).

Designer decisions recorded: Ryo GO for walk and heavy. Kitsune's heavy sleeve flare accepted for the demo. The heavy head-trim is Movesmith's, in frame data. Tail root and fan will be moved in Godot. Open-hand strikes (no finger bones, mitt hands) are still undecided; both grab takes also show open palms.

| Take | Fighter | Task id | Action | Credits | Ticks @60 | Verdict |
|---|---|---|---|---|---|---|
| T3 grab | Ryo | `01a10e69-98a9-7088-a761-e49d556113e6` | 259 `Step_Forward_and_Push` | 3 | 285 (142 keys, 4.73 s) | **FAIL** |
| T4 grab | Ryo | `01a10e6c-8bc6-7482-a91a-2fcc991b9195` | 421 `Over_Shoulder_Throw` | 3 | 261 (130 keys, 4.33 s) | **FAIL** |
| T5 grab | Kitsune | not run | n/a | 0 | n/a | n/a |

| Measure | T3 Ryo 259 | T4 Ryo 421 |
|---|---|---|
| Root drift end - start (xz) | 1.07 m (slow creeping advance, hips z 0 -> 1.05 m) | 0.13 m |
| Root y range / loop pop | 0.24 m / 0.17 m (ends crouched) | 0.34 m / 0.29 m (ends crouched, hips y 0.6) |
| Planted-foot shift raw (L / R) | 5.5 / 2.6 cm | 3.0 / 3.2 cm |
| Planted-foot shift if root stripped (L / R) | 32.7 / 13.2 cm | 11.4 / 10.4 cm |
| Max joint rotation per tick (limit 30) | 6.3 deg, no jitter | 22.5 deg (RightForeArm) |
| Reach / contact | Two-hand forward palm push. Hands idle until tick ~14, reach 0.16 -> 0.41 m forward of the hips by tick 30 and 0.5 m at tick 42; peak 3.1 m/s at tick 25. Contact is tick 30-36, 16-22 ticks after the reach starts (target 6) | No forward grab. Guard, hand to face (tick 30), arm sweep (54), both hands forward at head height (90-102), second reach (150), lunge with hands overhead (204), ends crouched. No hands-closed moment, no throw |
| Trimmable ~30-tick window | Ticks 14-44 holds the reach, but the right foot steps 0.9 m and hips travel 0.33 m in it, so stripping root motion slides the planted left foot ~33 cm. A head-trim starting at tick 30 gets contact in ~6 ticks but has no wind-up and still carries the step | None |
| Edges stretched > 2x (max) | 150 (5.7x, toe and upper leg) vs Ryo walk 55 | 164 (5.4x, upper legs) |
| Arm verts inside torso core (bind 0) | 0 | 47 (tick 106) |
| Leg vs other-leg axis min (bind 9.4 cm) | 2.7 cm (stride) | 8.4 cm |
| Stills | `docs/assets/retarget-gate/ryo-grab-2.png` ticks 0, 14, 24, 30, 36, 42, 60 | `docs/assets/retarget-gate/ryo-grab-3.png` ticks 0, 30, 54, 90, 114, 204 |

**T3 FAIL.** The closer one: the first ~45 ticks are a quick two-handed reach and read in the stills as a palm push at ticks 36-42, with no coat clipping and arms clear of the torso. But it is a 4.7 s slow-walking push, contact is 16-22 ticks into the reach, and the useful part includes a lunge step whose root travel cannot be stripped without a 33 cm foot slide. Marginal even as an F5 "shove" substitute.

**T4 FAIL.** `Over_Shoulder_Throw` is a martial-arts form (blocks and sweeps), not a throw; 261 ticks; ends crouched; nothing resembles a fast grab. Retarget quality is clean (no jitter, no leg crossing).

The retarget on Ryo is not in question: all four non-walk presets run on his rig without coat or sleeve clipping. The grab fails because the library presets tried are not grabs.

### Gate recommendation after take 2 (designer decides)
| Character | Walk | Heavy | Grab (T01) | Recommendation |
|---|---|---|---|---|
| Ryo | PASS | PASS (28-tick head-trim by Movesmith) | FAIL on 3 presets, a source problem and not a retarget problem | **GO** for the retarget gate. The grab needs another source |
| Kitsune | WARN | WARN (sleeve flare accepted for the demo) | not run | **CONDITIONAL GO**, unchanged. Hakama, hair and tails behaved on every clip; sleeve stretch is the known cost. Her grab is untested, but a preset that fails on Ryo would fail on her |

Take count for T01: take 1 (389) and take 2 (259 and 421 under one approval) are used; one take is left. The tooling cannot list or preview the library, so every further preset is a guess by name. Options:
1. **Substitute (F5)**: use 259 head-trimmed from tick 30 as a "shove" with the step in move data. Free, but flags a change to T01 and needs the foot-slide check on the trimmed window.
2. **Take 3**: one more name-based guess, 239 `Crouch_Pull_and_Throw` (unseen, likely long as well), 3 credits per rig. Low odds.
3. **Reuse the heavy-derived clip** (194, trimmed, already on both rigs) for T01. Free.
Recommend option 1, after a designer look at `ryo-grab-2.png`.

## Stills
Rendered headless-free in Godot 4.7.2 (Forward+, AMD 7900 XT): runtime `GLTFDocument` load of each GLB, `AnimationPlayer.seek` per tick, front and side camera, flat light, grey background. Top row front view, bottom row side view; the tick is in each label. Kitsune stills include the proxy tail fan.

| File | Ticks |
|---|---|
| `docs/assets/retarget-gate/ryo-walk.png` | 0, 12, 28, 44 |
| `docs/assets/retarget-gate/kitsune-walk.png` | 0, 12, 28, 44 |
| `docs/assets/retarget-gate/ryo-heavy.png` | 0, 24, 34, 42, 56 |
| `docs/assets/retarget-gate/kitsune-heavy.png` | 0, 24, 34, 42, 56 |
| `docs/assets/retarget-gate/ryo-grab.png` | 0, 54, 120, 204, 216, 250 |
| `docs/assets/retarget-gate/kitsune-grab.png` | 0, 54, 120, 204, 216, 250 |
| `docs/assets/retarget-gate/ryo-grab-2.png` (take 2, 259) | 0, 14, 24, 30, 36, 42, 60 |
| `docs/assets/retarget-gate/ryo-grab-3.png` (take 2, 421) | 0, 30, 54, 90, 114, 204 |

## Method limits
- Clipping figures are proxies on a single merged mesh (no per-part segmentation): "arm verts inside torso core" uses a capsule from hips to neck at 60% of the 30th-percentile bind radius; stretched edges compare skinned to bind edge length. The stills are the real verdict.
- Contact frames are read from hand speed and reach; the strike is a swing, not a fist, because the rig has no fingers.
- The tail proxy is an assumption (no mount scene exists); sway and the real tail texture were not tested.
- Preset names/ids come from Meshy's public library table; 194 and 389 returned the clips named in the GLBs (`Right_Uppercut_from_Guard`, `Grip_and_Throw_Down`).
