# Kitsune rig

Ticket: YOK-111 (carries over YOK-30 rig and YOK-31 retarget gate). Owner: rig-specialist. Rules: F6, G4, T0, E15, AM5.
Model spec and acceptance: `assets/specs/kitsune.md` (asset-smith). Shared skeleton: `assets/specs/rigs/skeleton.md`.

**Verdict: ACCEPT** (carried over from the closed G4 gate; designer decision 2026-10-06, Kitsune GO with the sleeve flare accepted). Scripted rig check pending (YOK-110).

## Rig
| Field | Value |
|---|---|
| Rigger | Meshy auto-rig (`meshy_rig`, `height_meters: 1.65`), YOK-30 take 2 |
| Rig task id | `01a10e47-91f6-709b-bd34-8884f8cbabbb` |
| File | `game/assets/generated/characters/kitsune/kitsune-rigged.glb` (free walk/run: `kitsune-walk.glb`, `kitsune-run.glb`) |
| Skeleton | The shared skeleton (one of its two source rigs): 24 joints, names and skin order identical to Ryo, no bone map |
| Height, frame | 1.65 m, Y-up, +Z facing, origin at the feet (bbox min Y 0.000), T-pose rest, bones in cm under a 0.01 `Armature` node. The whole column sits about 5 cm forward of the origin (`Hips` z 0.048) |
| Mesh | `char1`, 17,198 verts / 15,153 tris, 1 material, 2K base colour |
| Weights | Max 4 influences, all normalised; all 24 bones weighted (`Spine` only 50 verts). Over half the verts use 4 influences (9,042), against about a quarter on Ryo, because the sleeves and hakama span several bones |
| Fingers | None: open-hand strikes (E15) |
| Cost | 5 credits (YOK-30 take 2, already spent). This ticket spent 0 |

## Extra parts
| Part | Treatment | State |
|---|---|---|
| Nine tails | **Separate mesh** (`characters/kitsune/tail.glb`, one mesh instanced 9x), rigid, **not rigged** (designer, 2026-10-05). Mounted in `game/scenes/fighters/kitsune.tscn` on a `BoneAttachment3D` "Tails" at `Hips`; each `TailN` pivot sits at (0, -4, -16) cm in hip space with its fan pose on a child "Fan" node | In game |
| Tail sway | **Tick-driven, no physics**: `FighterModel.SwayTails(worldFrame)` rotates each pivot about its local X axis by `TailSwayDeg * sin(2π * ((worldFrame + i*11) % TailSwayPeriod) / TailSwayPeriod)`, defaults 5 deg and 96 frames, each tail 11 frames out of phase. Deterministic, re-derived from the frame on every seek | Matches the rule; not yet rendered in stress poses |
| Haori sleeves (wide) | Baked into the body skin, no extra bones | **Known cost: sleeve flare** (below) |
| Hakama (full-length, wide) | Baked into the body skin | Holds: no leg poke-through or tear at the gate |
| Hair (long, down the back to hip height) | Baked, weighted to `Head`/`neck`/spine | Clears the hakama except 3 mm in the deepest lean (389 grab, a rejected clip) |
| Mask, ears, bell, obi cord | Baked | OK |

### Sleeve flare (accepted, 2026-10-06)
On big arm swings the right haori sleeve flares open into an inside-out red cuff across the chest and under the mask (heavy, ticks 34-42), and the left folds into the haori. Gate figures for the heavy: 843 edges over 2x stretch (max 11.7x over the clip, 9.6x on its hit frames) against Ryo's 137; sleeve verts inside the torso core +194 over bind. The designer accepted it for the demo. The clip-acceptance bar animator uses since YOK-42: hit-frame sleeve stretch above 14x, or a smeared or inside-out sleeve on the hit frame, is a reject; prefer clips with low or close arms. The cheaper cure if it ever stops being acceptable is a rig fix, not a clip fix: tied-back (tasuki) or shorter sleeves in a new Kitsune take (about 44 credits for turnaround, mesh and rig, plus re-applying the clips), or two `X_Sleeve` bones per arm with tick-driven lag. Neither is proposed now.

### Tails at the gate
With a 9-tail proxy on `Hips` (±55 deg fan, 0.5 m): seated through every clip with no gap or flip; clear of legs and hakama in walk, the heavy's hip twist (yaw -43, pitch +29, roll -21 deg) and the grab lean; overlapped the hair at the mount in every pose (a layout issue for data, not a clip fault; the game mount now sits at z -16 cm, y -4 cm in hip space, still to be confirmed clear of the hair in the stress stills); dipped a few cm into the back of the haori in the deepest leans. Sway was not simulated at the gate (no code then); it is in now and still needs the stress-pose stills.

## Attachment bones
Foxfire / hand VFX on `LeftHand`/`RightHand`; chest sparks on `Spine01`; head VFX on `Head`/`headfront` (the face point, 16.5 cm in front of `Head`, out at the mask); tails on `Hips`; foot contact on `LeftToeBase`/`RightToeBase`. No new bones.

## Retarget gate carry-over (YOK-31, G4)
Full report: `assets/specs/clips/retarget_gate.md`. Stills: `assets/specs/clips/stills/retarget-gate/kitsune-*.png`.

| Gate clip | Result | Notes |
|---|---|---|
| Walk (free `walking_man`) | WARN | Hakama holds; pleats pinch a little on the leg lift (ticks 44-48); 563 edges over 2x (max 7.3x) |
| Heavy (preset 194) | WARN, accepted | Sleeve flare on contact frames (above); hakama coherent in the wide stance |
| Grab / throw | Source FAIL (389), retarget OK | T01 is the 259 shove, head-trimmed from tick 30 (E15); Kitsune's take is `clips/kitsune/kitsune-throw.glb` (YOK-42) |

Decision 2026-10-06: **Kitsune GO.** No folklore human-guise fallback needed. The fallback stays on file in `assets/specs/kitsune.md` (tail-less, plain kimono, mask kept on).

## Stress poses (pending)
High kick, deep crouch, full arm raise, torso twist and the grab have not been rendered as rig stills yet, with the real tail fan and sway. The arm raise (sleeves) and high kick (hakama, tails) are the poses to watch. Stills go to `assets/specs/rigs/stills/kitsune-*.png` when `rig_check.py` lands.

## Check run
- **Pending:** `tools/blender/rig_check.py` (YOK-110) does not exist yet. Expected values are in `skeleton.md`.

## Takes
| # | Date | Job | Task id | Credits |
|---|---|---|---|---|
| 1 | 2026-10-05 | `meshy_rig` (take 1, superseded) | `01a10e1a-063d-7300-8ecd-5b42ad89b086` | 5 |
| 2 | 2026-10-05 | `meshy_rig` (YOK-30 rebuild, current) | `01a10e47-91f6-709b-bd34-8884f8cbabbb` | 5 |
