# Ryo rig

Ticket: YOK-111 (carries over YOK-30 rig and YOK-31 retarget gate). Owner: rig-specialist. Rules: F6, G4, T0, E15, AM5.
Model spec and acceptance: `assets/specs/ryo.md` (asset-smith). Shared skeleton: `assets/specs/rigs/skeleton.md`.

**Verdict: ACCEPT** (carried over from the closed G4 gate; designer decision 2026-10-06, Ryo GO). Scripted rig check pending (YOK-110).

## Rig
| Field | Value |
|---|---|
| Rigger | Meshy auto-rig (`meshy_rig`, `height_meters: 1.75`), YOK-30 take 2 |
| Rig task id | `01a10e47-857e-73a0-9f7a-cd74795198fb` |
| File | `game/assets/generated/characters/ryo/ryo-rigged.glb` (free walk/run: `ryo-walk.glb`, `ryo-run.glb`) |
| Skeleton | The shared skeleton itself (Ryo is one of its two source rigs): 24 joints, names and skin order identical, no bone map |
| Height, frame | 1.75 m, Y-up, +Z facing, origin at the feet (bbox min Y 0.000), T-pose rest, bones in cm under a 0.01 `Armature` node |
| Mesh | `char1`, 17,714 verts / 15,467 tris, 1 material, 2K base colour |
| Weights | Max 4 influences, all normalised; all 24 bones weighted (`Spine` only 7 verts) |
| Fingers | None: open-hand strikes (E15) |
| Cost | 5 credits (YOK-30 take 2, already spent). This ticket spent 0 |

T0: the Tanuki fights with this model and these clips, so this rig is also the boss rig. Any change to it reaches the boss.

## Extra parts
| Part | Treatment | State |
|---|---|---|
| Coat | Short, tight to the body in take 2 (no long coat tail) | Baked into the body skin. No extra bones needed |
| Rope sash, pouch, hip paper | Baked rigid to `Hips` | No clipping found at the gate |
| Collar over the mouth | Baked to `neck`/`Head` | OK |
| Sleeves | Tight; not a clipping risk | No sleeve flare (unlike Kitsune) |

The YOK-29 spec's fallback (two coat-tail bones with tick-driven sway if it clipped the legs) is **not needed**: take 2 has no long coat tail.

## Attachment bones
Talisman / hand props on `RightHand` / `LeftHand` (mitt, offset placement); chest sparks on `Spine01`; head VFX on `Head`/`headfront`; foot contact on `LeftToeBase`/`RightToeBase`. No new bones.

## Retarget gate carry-over (YOK-31, G4)
Full report: `assets/specs/clips/retarget_gate.md`. Stills: `assets/specs/clips/stills/retarget-gate/ryo-*.png`.

| Gate clip | Result | Notes |
|---|---|---|
| Walk (free `walking_man`) | PASS | In-place treadmill, no pop, no coat, sleeve or leg clipping; 55 edges over 2x stretch (max 3.7x) |
| Heavy (preset 194 `Right_Uppercut_from_Guard`) | PASS | Clean retarget; 28-tick head-trim by Movesmith puts contact in the window; left planted foot 3.1 cm (1 mm over, minor) |
| Grab / throw | Source FAIL, retarget clean | Presets 389, 259 and 421 were not grabs, but all played without coat or sleeve clipping. T01 is the 259 shove, head-trimmed from tick 30, step in move data (E15) |

Decision 2026-10-06: **Ryo GO.** Open-hand strikes accepted (E15).

## Stress poses (pending)
The skill's Blender stress poses (high kick, deep crouch, full arm raise, torso twist, the grab) have not been rendered on this rig yet; YOK-31 checked the gate clips only, and the YOK-32 clip takes (`assets/specs/clips/yok-32-ryo-clips.md`) cover kicks and crouch as clips. Stills go to `assets/specs/rigs/stills/ryo-*.png` when `rig_check.py` lands.

## Check run
- **Pending:** `tools/blender/rig_check.py` (YOK-110) does not exist yet. Expected values are in `skeleton.md`.

## Takes
| # | Date | Job | Task id | Credits |
|---|---|---|---|---|
| 1 | 2026-10-05 | `meshy_rig` (take 1, superseded) | `01a10e19-f774-707d-9b31-3e94ac0acdcd` | 5 |
| 2 | 2026-10-05 | `meshy_rig` (YOK-30 rebuild, current) | `01a10e47-857e-73a0-9f7a-cd74795198fb` | 5 |
