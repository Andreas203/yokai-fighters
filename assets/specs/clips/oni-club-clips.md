# Oni club clips (YOK-102 follow-up to the G4 gate)

Status: **4 TAKES RUN AND MEASURED (2026-10-16): idle ACCEPT, heavy ACCEPT (WARN), walk forward RETAKE, walk back RETAKE. See `## Takes
Run by the main session on the designer's approval, 2026-10-16: 4 takes x 3 = **12 credits** (balance 697 to 685). One take each, no retakes. Raw GLBs (uncommitted, on the raw 2.0 m Meshy rig, bone names are the shared skeleton so the game plays their rotations on `oni-rigged.glb`) in `game/assets/generated/characters/oni/takes/`.

| Clip | Preset | Task id | Raw ticks |
|---|---|---|---|
| idle | 335 | `01a14515-b3d9-7033-b954-d4eeed6e92bf` | 679 |
| walk forward | 669 | `01a14515-b6a2-7394-bcc1-44f3cf7dba32` | 333 |
| walk back | 687 | `01a14515-b84b-743a-a6bd-f4ee1cb5b9dc` | 83 |
| heavy | 128 | `01a14515-ba26-7266-bea2-b233e1d2f6e2` | 113 |

## Verdicts
Club check = `prop_clearance.py --clip` on `oni-rigged.glb` with `club-grip.glb`, rotations only as the game plays them, `--ignore-radius 0.12`, pass = no club vertex more than 2 cm newly inside the body (`new_worst_depth_m`). Raw takes: whole clip, every tick (walk back), 2nd tick (walk forward), 4th tick (idle), 3rd tick (heavy). Stills of the raw runs are in the scratchpad only.

| Clip | Verdict | Evidence |
|---|---|---|
| Heavy 128 | **ACCEPT (WARN)** | Club clean on all 113 raw ticks and again on the cleaned 53 (0 vertices inside the body or head; club never reaches the floor, lowest 0.42 m). Sequence: lean-back wind-up raw 1-46, rise, overhead peak at 76 (club tip 2.36 m), slam 85-97, hunched hold to 112. Hand peak speed raw 93, peak reach raw 96 (150 cm forward). Jitter 13.7 deg per frame squared (warn 20), 12 reversals. WARN: left planted foot shuffles 1.04 cm per frame (limit 1.0; 14 cm net over the slam). Left hand grips nothing (two-handed preset) |
| Idle 335 | **ACCEPT** | Raw take is 11 s, and most of it fails: the arms cross and the club passes through his hips (up to 35 cm; clean runs only at raw 77-165, 329-365, 509-537). Trimmed raw 77-142, a 66-frame breathing loop that closes within about 1 degree per bone. Club clean on all poses, slide 0.09 cm per frame, jitter 1.8 |
| Walk forward 669 | **RETAKE** | Club through the body at raw 70-80 and 122-128 (up to 17 cm, hips/torso) and at 223-241 and 295-331 (up to 31 cm). The stride repeats every about 160 ticks (2.7 s), and the clean windows (13-69, 81-121, 129-217) contain no pose pair that closes a loop (best closure error 3.2 against 0.77 at the true period). Too slow as well |
| Walk back 687 | **RETAKE** | The torso twists on every step and the right arm goes behind the back: the club goes through the back and head (13-23 cm over ticks 4, 27, 32, 40-56, 69, 72) with 5-6 cm for most of the rest of the loop |

### Retake proposal (not run, needs designer approval)
| Clip | Preset | Why | Credits |
|---|---|---|---|
| Walk forward and back | **611** `Carry_Heavy_Object_Walk_inplace` (WalkAndRun/Walking) | Preview: both forearms held steady out in front while the legs walk, no arm swing and no torso twist, so the club should stay ahead of him. One take serves both: walk back is the same clip played in reverse in move data (as the Kitsune walk back). Unseen with a club, so the check after the take decides | 3 |
| Walk forward fallback | **689** `Walk_Fight_Forward_inplace` (+ **688** `Walk_Fight_Back_inplace`) | Guard walks; but the same guard walks on Ryo's clips fail the club check (up to 22 cm), so a lower chance | 3 + 3 |

This is take 2 of 3 for the walks. After a second rejection propose a mechanically equivalent move on an approved clip (F5), for example the idle clip played as the walk with the movement in data, which is a designer call.

## Walk retake result (take 2 of 3): ACCEPT
Preset 611 `Carry_Heavy_Object_Walk_inplace`, task `01a14545-a7f0-71a5-b698-4fa41ac04df2`, 3 credits (balance 685 to 682). Raw 393 ticks (`takes/oni-walk-611.glb`).
- **Club check:** 0 poses above 2 cm on the raw take (99 poses, every 4th tick of all 393) and on the cleaned loop (62 poses): both forearms stay out in front, the club never touches the body, lowest point 1.59 m.
- **Loop:** pose closure best at raw 74 to 198 (error 0.82, against 3.2 for the rejected 669), so trim raw 74-197, one 124-frame stride cycle (2.07 s). Cleaned: `clean_clip.py --trim 74 197 --strip-root horizontal`, no smoothing; stripped file `game/assets/generated/clips/oni/oni-walk-club.glb`.
- **Measure** (`measure_clip.py --movement-clip`): PASS, root travel 0, jitter 3.1 deg per frame squared, 3 reversals (feet). Planted feet travel 0.9-1.3 cm per frame at 1x (a walk, not judged for slide).
- **Retime:** speed_scale 1.5, 83 frames (1.4 s), which puts the stance foot at about 1.65 cm per tick on screen (about 1 m/s); move data sets the walk speed from that. Walk back = the same clip reversed in data. No curve needed.
- Data: `data/clips/oni-walk-fwd.json` (proposed, validates). Stills: `stills/oni/oni-walk-club_f*.png`, `oni-walk-club-clearance_f{01,63}-side.png`.
- Risk: the Oni walks with both arms forward as if carrying, so the pose is the same as a guard; that is the reason it clears the club.

## Oni-specific picks (designer decisions, 2026-10-16; free calls only, not run)
Decided after the movesmith review: 3 credits each, one take each, rig `01a144ad-b98d-7520-8376-c776c414a476`. Picks are from preview sheets of the public table; the club check after the take decides. A general warning from the Ryo clips: the shared hit high (174), hit low (171) and block low (146) fail the club check, so those presets are not repeated here except as noted.

| # | Clip | Preset | Why | Club-in-right-hand risk | Fallback |
|---|---|---|---|---|---|
| 1 | Hit high | **175** `Face_Punch_Reaction_1` (Fighting/GettingHit, 73 frames) | A head-and-torso twist and recoil, then a recovery to guard; the arms stay close to the chest, so the body moves, not the club. Reads as a big body taking a hit | The recoil turns the torso, which swings the club arm back (the same cause as the old hit high, 25 cm). Check the first 20 ticks | **179** `Hit_Reaction_1` (31 frames, a small flinch), then 176 `Face_Punch_Reaction_2` (unseen) |
| 2 | Hit low | **179** `Hit_Reaction_1` (Fighting/GettingHit, 31 frames) | A short flinch with one arm out, with no deep fold, unlike 171 | 171 `Hit_Reaction_to_Waist`, which the Ryo hit low uses, folds him at the waist and put the club 34 cm into the legs; avoid it. The fold is what makes it read low, so this one reads more as a hit than as a low one; low versus high is also hitstun data | **178** `Hit_Reaction` (41 frames, an arm flail, bigger swing of the club arm) |
| 3 | Block low | **146** `Block10` (Fighting/Blocking, 15 frames) | The one blocking preset that ends in a low crouched guard; the first Ryo take of this id was used for Kitsune/Ryo block low. Short, hold the last pose | The Ryo block low (same id family) put the club 33 cm through the legs at one tick (19) and 8 cm at 4, so check the crouch's final pose; the hands come up to the face, so the club points forward-up | **140** `Block3` (37 frames, a standing high guard; reads less low, so move low to move data, a lowered stance) |
| 4 | Club swing (medium punch and medium kick, retimed in data) | **219** `Right_Hand_Sword_Slash` (Fighting/Punching, 37 frames) | A one-handed right-hand slash across the body with a step: a horizontal swing, the right-hand fit | The arm crosses the torso and the club follows at the hip on the follow-through; check the swing's end near the left leg | **240** `Thrust_Slash` (73 frames, a wide one-arm sweep; longer, head-trim) or **97** `Left_Slash` (97 frames) |
| 5 | Iron-Club Charge | **654** `Rifle_Charge_inplace` (WalkAndRun/Running, in place) | A run with both forearms held in front of the chest, steady, like the 611 walk that cleared the club; the legs drive, the club stays out in front | Same family as the working walk, so the lowest risk of the runs. The pump of the torso may lift the club head into the chin | **673** `Standard_Forward_Charge_inplace` (the arms pump; likely fails like the old walk) |

First pass **15 credits** (5 x 3). Worst case at the F5 cap (3 takes each): 45 credits, so 30 beyond the first pass. The cleanup, 60-tick measure, club check (`prop_clearance.py --clip`, command in the plan above) and retiming follow the same pipeline; the medium punch and the medium kick are two curves over one swing take: punch the first half, kick a slower curve (each segment 0.5-2x, a hold at most 4 frames).

### Oni Quake: reuse of the heavy slam (free)
`data/clips/oni-quake.json` (proposed, validates): the accepted `oni-heavy-club.glb` with its own curve, so the heavy punch and the Quake share one file.

| Frames | File ticks (raw) | Speed | Role |
|---|---|---|---|
| 1-17 | 0-16 (60-76) | 1x | slow charged rise to the overhead peak |
| 17-20 | 16 held (76) | hold, 4 frames | club held overhead, the tell |
| 20-28 | 16-32 (76-92) | 2x | the slam falls |
| 28-32 | 32-36 (92-96) | 1x | **impact, hit 28-31** |
| 32-40 | 36-44 (96-104) | 1x | recovery |
| 40-56 | 44-52 (104-112) | 0.5x | eases out |

56 frames: startup 27, active 4, recovery 25. The earlier heavy punch uses the fast 45-frame curve of the same file. The quake's ground effect and any longer active window belong to movesmith.

### Kit (designer, 2026-10-16)
Oni plays LP, MP, MK, HP, HK and the throw, as the Kitsune does. No light kick, no jumping, no dash back, no air normals, no other fighters' specials, so those clips need no take. Status of each clip for him: idle (accepted take), walk (accepted take), HP (accepted take), MP and MK (swing pick 4), LP (shared light punch, clean), HK (shared heavy kick, clean), throw (shared, clean), crouch (shared, clean), knockdown (shared, clean), get-up (shared, small overlap up to 6.6 cm, a movesmith/designer call), block high (shared, 2.4 cm at one tick), hit high, hit low, block low (picks 1-3), charge (pick 5).

## Oni-specific takes, run 2026-10-16 (take 1 of 3 each, 15 credits, balance 682 to 667)
Raw takes (uncommitted) in `game/assets/generated/characters/oni/takes/`. Club check as before (rotations only, `--ignore-radius 0.12`, fail above 2 cm newly inside the body); every tick or every 2nd or 4th tick as noted. Reject stills (one worst tick each) in `assets/specs/clips/stills/oni/*-reject_f*-side.png`.

| Clip | Preset | Task id | Raw ticks | Poses over 2 cm | Worst | Verdict |
|---|---|---|---|---|---|---|
| Hit high | 175 | `01a14549-133a-745d-b957-afbf6bbcee5e` | 273 | 55 of 69 | 22 cm | **RETAKE** |
| Hit low | 179 | `01a14549-15c8-765a-a6e5-8c0f4beb6294` | 75 | 34 of 38 | 23 cm | **RETAKE** |
| Block low | 146 | `01a14549-17f4-71ff-8857-59a082011581` | 35 | 18 of 35 | 32 cm | **RETAKE** (free alternative below) |
| Club swing | 219 | `01a14549-19b5-717f-adba-e0b7ba686525` | 93 | 37 of 47 | 52 cm | **RETAKE** (no MP/MK data written) |
| Charge | 654 | `01a14549-1b6d-74b5-b5e4-74abde45ab3d` | 33 | 5 of 33 (ticks 14-19) | 32 cm | **WARN, kept as a 14-frame clean window** |

What the club check showed:
- **Hit high 175 and hit low 179 start from a fists-up guard**, and that guard itself puts the club through his chest: tick 1-93 of 175 is the same 16 cm overlap before the hit even starts. Any preset that starts with the forearms folded up fails the same way, which also explains the old shared hit high, hit low and block low. Presets that start with the arms down or out in front clear (the carry walk 611, the idle).
- **Block low 146** fails on its first 8 ticks and its final hold (29-32 cm at ticks 1-8 and 34-35), so the held pose is the part that fails.
- **Swing 219** is a guard-to-cross slash; the arm crosses the body and the club goes through the torso for most of it (11-52 cm).
- **Charge 654**: the raw cycle is 33 ticks, a full stride; the club touches the shoulder at 14-19, nothing else. Cleaned raw 20-33 (`clean_clip.py --trim 20 33 --strip-root horizontal`, 14 frames, stripped file `game/assets/generated/clips/oni/oni-charge-club.glb`): 0 of 14 poses over 2 cm, club lowest 1.19 m, jitter 20.4 deg per frame squared (just over warn), feet travel is move data. Raw measures: hit high PASS (jitter 5.8), hit low PASS (9.1), block low PASS (3.6), swing WARN (jitter 32.8, 8 reversals), charge WARN.
- `data/clips/oni-iron-club-charge.json` is written (proposed, validates). Nothing written for the rejected four.

### Retakes proposed (take 2 of 3, not run, need approval)
Picked from preview sheets for a start with the arms down; the club check after the take decides.

| Clip | Preset | Why | Risk | Fallback | Credits |
|---|---|---|---|---|---|
| Hit high | **173** `Slap_Reaction` (Fighting/GettingHit, 118 frames) | Starts neutral with the arms down; a head snap with the arms staying low | Long (118 frames): trim to the snap; the torso turn may carry the club arm | 178 `Hit_Reaction` (arms hang and flail, 41 frames) | 3 |
| Hit low | **177** `Gunshot_Reaction` (Fighting/GettingHit, 73 frames) | Arms down, the torso hunches and turns to the side with a hand to the hip | The hunch bends the club arm toward the thigh | 178 `Hit_Reaction`, or 172 `Electrocution_Reaction` (arms out, reads big) | 3 |
| Club swing | **97** `Left_Slash` (Fighting/AttackingwithWeapon, 97 frames) | A sword-and-shield slash with the weapon in the right hand held out wide, arm not across the chest | The shield arm is empty-handed on him; long clip, head-trim | 240 `Thrust_Slash` (73 frames, a wide one-arm sweep from a relaxed start) | 3 |
| Block low | **none: reuse the shared crouch** (free) | `ryo-crouch` (the deep squat, held frame) is club-clean (0 poses over 2 cm), so a held crouch with a short blend is the block low | Reads as a crouch, not a guarded block; all guard-hands-up blocks (138-146) would fail like 146 | If the designer wants a real block take: 258-style crouch with the arms low, a retake at 3 credits | 0 |

Total if the three takes are approved: **9 credits** (take 2 of 3; worst case with the 3-take cap another 9 each). After a second failure on a move, F5 applies (a mechanically equivalent move on an approved clip).

MP and MK curves: the swing is not usable yet, so the two retiming curves wait for the retake.

## Retakes, run 2026-10-16 (take 2 of 3, 9 credits, balance 667 to 658) and the crouch block
Raw takes (uncommitted) in `game/assets/generated/characters/oni/takes/`. Same club check (2 cm). Clean runs below are raw ticks (prop_clearance's frame numbering).

| Clip | Preset | Task id | Raw ticks | Result | Verdict |
|---|---|---|---|---|---|
| Hit high | 173 | `01a1454f-9867-7735-9864-464ba218cb23` | 293 | 36 of 100 poses over 2 cm, up to 62 cm; the head-snap itself (raw 58-67) puts the club through the head and body (39-62 cm), and the stagger after it fails too (112-118, 211-274, up to 33 cm). Clean only 1-55 and 70-109, which leave out the snap | **RETAKE** (take 3, text_to_motion, below) |
| Hit low | 177 | `01a1454f-9b2a-7566-8c68-58331f22c1ae` | 183 | 24 of 92 poses over 2 cm (raw 1-21, 57-63, 103-125, 157-183, up to 36 cm), clean 23-55 and 65-101. The reaction is a left-shoulder flinch with the club held up beside the head | **ACCEPT (WARN)**, trimmed raw 23-55, 33 frames: 0 of 33 poses over 2 cm, club lowest 1.61 m, jitter 5.9. Reads as a flinch more than a low hit (height is hitstun data); blend about 6 ticks in and out |
| Swing (MP and MK) | 97 | `01a1454f-9d18-72b6-9771-dd7fb3fddfae` | 193 | 10 of 120 poses over 2 cm: raw 45 (15 cm, in the sweep at peak hand speed), 54 and 56 (2-3 cm), and 67-73 (up to 25 cm, the follow-through around the head). Everything else clean, including the wind-up and the sweep | **ACCEPT (WARN)**, trimmed raw 24-53, 30 frames: 1 of 30 poses over 2 cm (15 cm into the hip, one tick inside the active frames), jitter 33.9 (warn 20, reject 40), 11 reversals |
| Block low | shared `ryo-crouch` | none (0 credits) | 1 hold frame (raw 100) | Club clean (the shared check: 0 poses over 2 cm) | **ACCEPT**, designer-approved |

Cleanup (scripted, `clean_clip.py ... --strip-root horizontal`, no smoothing): hit low `--trim 23 55`; swing `--trim 24 53 --hit 17 27`. Stripped files `game/assets/generated/clips/oni/oni-hit-low-club.glb` and `oni-swing-club.glb`. Measured with `measure_clip.py`: hit low PASS (slide 0.48), swing WARN (jitter only).

### Swing curves (data written, proposed)
Take file ticks 0-29 = raw 24-53; the sweep in front of the body is file ticks 16-26 (raw 40-50), the hand's peak speed 8.8 m/s at raw 46.

| Move | Frames | Curve (frames to file ticks) | Startup / active / recovery |
|---|---|---|---|
| MP `oni-medium-punch` | 20 | 1-8 to 0-14 (2x); 8-14 to 14-26 (2x); 14-20 to 26-29 (0.5x) | 8 / 5 (9-13) / 7 |
| MK `oni-medium-kick` | 28 | 1-14 to 0-14 (about 1x); 14-22 to 14-26 (1.5x); 22-28 to 26-29 (0.5x) | 15 / 6 (16-21) / 7 |

Both play the same file, one swing, so the medium kick is a club strike with a slower, heavier wind-up. No hold; every segment is between 0.5x and 2x.

### Data written
`data/clips/oni-hit-low.json`, `oni-medium-punch.json`, `oni-medium-kick.json`, `oni-block-low.json` (all proposed; `tools/validate_data.py`: 108 files valid, 0 invalid). Nothing for hit high.

### Hit high, take 3 of 3: text_to_motion, not run, needs approval
- **Tool:** `meshy_text_to_motion`, swift, **3 credits** (prime would be 10). Biped, on the same rig.
- **Prompt (exact):** "A huge ogre warrior holds a heavy spiked club in his right hand with the arm hanging low and out to his side, the club pointing forward and down away from his body. He is hit in the face from the front: his head snaps back and to the side, his torso leans back a little and he staggers half a step back, then steadies and returns to the same stance. His right arm and the club stay low and out to the side the whole time and never cross his chest or rise behind his head; his left arm flinches up toward his face. In place, no travel, about 1.5 seconds."
- **Risk:** a text prompt can still swing the right arm; the club check decides. If this also fails, F5 applies: a mechanically equivalent move on an approved clip, for example the accepted hit low (177 trimmed) for both hit heights (a free substitute that the designer may prefer to spending the take).

## Hit high take 3 (text_to_motion), run 2026-10-16: RETAKE, cap reached, F5 substitute
Swift, 3 credits (balance 658 to 655), task `01a14555-2a59-7350-b25e-ffc137dbdc0a`, prompt trimmed to the 400-character limit by the main session. The raw BVH (`takes/oni-hit-high-t2m.bvh`, Meshy's 77-joint text-to-motion skeleton, 240 frames at 30 fps = 8 s, not the 2 s asked) was NOT applied through meshy_animate (it would cost 3 more).
- **Retarget (free, scripted):** new `tools/blender/retarget_bvh.py` maps the 22 body joints of the BVH onto `oni-rigged.glb` through a bone map (Hips, Spine1 to Spine02, Spine2 to Spine01, Chest to Spine, Neck1 to neck, Head, shoulders, arms, forearms, hands; Leg to UpLeg, Shin to Leg, foot, toe; fingers, Root, Neck2, eyes and jaw dropped). For each bone the source's world rotation change from its T-pose rest is applied to the target's rest pose, and Hips takes the translation change scaled by the hip-height ratio. No pose is placed by hand (F5). Both rigs face the same way after import. It clears the rig's bind clip and writes one 479-tick action. Retarget looks right in the stills (a crouched stance, arms and legs in the right places).
- **Club check** (every 6th tick of all 479): 64 of 80 poses over 2 cm, up to 65 cm. Clean only ticks 1-85, before the hit. The hit (91-151) puts the club through the head (65 cm at tick 97), and the take then holds a crouched guard with the right forearm folded up for the other 5 s, so the club stays 5-18 cm into the head and shoulder. The model ignored "right arm low and out to the side".
- **Verdict: RETAKE.** The cap of 3 takes (175, 173, t2m) is spent. **F5 substitute:** the accepted hit low (177, trimmed raw 23-55) serves both hit heights; the height difference is hitstun and hitbox data. `data/clips/oni-hit-high.json` is written (proposed, validates) and points at `oni-hit-low-club.glb`.
- Stills: `stills/oni/oni-hit-high-t2m-reject_f097-side.png` (the hit) and `..._f241-side.png` (the held stance).

## Oni clip kit: final summary
Own = a take made for the Oni. Shared = an existing Ryo clip, club-clean per the check. Data files are all `proposed`, in `data/clips/`; clips in `game/assets/generated/clips/oni/` unless noted. The kit is the Kitsune pattern, LP, MP, MK, HP, HK plus the throw.

| Move | Data file | Clip file | Own or shared | Note |
|---|---|---|---|---|
| Idle | `oni-idle` | `oni-idle-club.glb` | own (335) | 66-frame loop, ACCEPT |
| Walk forward, and back reversed | `oni-walk-fwd` | `oni-walk-club.glb` | own (611) | 83 frames at 1.5x, ACCEPT |
| LP | `ryo-light-punch` | `clips/ryo/ryo-lp.glb` | shared | club-clean |
| MP | `oni-medium-punch` | `oni-swing-club.glb` | own (97) | 20 frames, 8/5/7, WARN: one tick 15 cm |
| MK | `oni-medium-kick` | `oni-swing-club.glb` | own (97) | same take, 28 frames, 15/6/7 |
| HP | `oni-heavy-punch` | `oni-heavy-club.glb` | own (128) | 45 frames, 16/4/25, WARN foot shuffle |
| HK | `ryo-heavy-kick` | `clips/ryo/ryo-hk.glb` | shared | club-clean |
| Throw | `ryo-generic-throw` | `clips/ryo-grab-2.glb` | shared | club-clean (C4) |
| Quake (special) | `oni-quake` | `oni-heavy-club.glb` | own, reuse of HP | 56 frames, 27/4/25 |
| Iron-Club Charge | `oni-iron-club-charge` | `oni-charge-club.glb` | own (654) | 14-frame clean half stride, no loop, WARN |
| Hit high | `oni-hit-high` | `oni-hit-low-club.glb` | own, F5 substitute for hit low | 33 frames, flinch |
| Hit low | `oni-hit-low` | `oni-hit-low-club.glb` | own (177) | 33 frames, WARN |
| Block high | `ryo-block-high` | `clips/ryo/ryo-block-high.glb` | shared | one tick 2.4 cm |
| Block low | `oni-block-low` | `clips/ryo/ryo-crouch.glb` | shared (the crouch hold) | designer-approved, reads as a crouch |
| Crouch | `ryo-crouch` | `clips/ryo/ryo-crouch.glb` | shared | club-clean |
| Knockdown | `ryo-knockdown` | `clips/ryo/ryo-knockdown.glb` | shared | club-clean |
| Get-up | `ryo-get-up` | `clips/ryo/ryo-get-up.glb` | shared | small overlap up to 6.6 cm, a designer call |

Credits spent on the Oni: 12 + 3 + 15 + 9 + 3 = **42** (balance 697 to 655). Not in the kit, so no clip: light kick, jump, dash back, air normals, other fighters' specials. Sound cues for the Oni heavy and Quake still wait for a `data/sound` schema.

## Cleanup
Scripts under `tools/blender/` (Blender 4.1.1 headless), scratch outputs only; the GLBs committed are mesh-stripped with `tools/strip_clip.py`.
- Heavy: `clean_clip.py --clip takes/oni-heavy-128.glb --out oni-heavy-clean.glb --trim 60 112 --strip-root horizontal --hit 33 37` (no smoothing: jitter was under the warn level; file tick 0 = raw tick 60; 53 frames at 60 fps). Result: `game/assets/generated/clips/oni/oni-heavy-club.glb` (stripped, 76 KB).
- Idle: `clean_clip.py --clip takes/oni-idle-335.glb --out oni-idle-clean.glb --trim 77 142 --strip-root horizontal` (no smoothing; 66 frames, file tick 0 = raw 77). Result: `game/assets/generated/clips/oni/oni-idle-club.glb` (stripped, 88 KB).
- Walks: not cleaned (rejected).
- Raw takes were measured with `measure_clip.py --as-stripped --movement-clip`; cleaned ones with `measure_clip.py --id oni-<name>-club [--hit 33 37]`.

## Retiming (heavy)
`data/clips/oni-heavy-punch.json` carries the curve (the schema now has `retime`, YOK-113; ticks are file ticks, raw = file + 60):

| Frames | File ticks (raw) | Speed | Role |
|---|---|---|---|
| 1-9 | 0-16 (60-76) | 2.0x | startup: arms rise to the overhead peak |
| 9-17 | 16-32 (76-92) | 2.0x | the slam falls |
| 17-21 | 32-36 (92-96) | 1.0x | **hit 17-20**, the strike pose with the club low in front |
| 21-29 | 36-44 (96-104) | 1.0x | recovery |
| 29-45 | 44-52 (104-112) | 0.5x | recovery eases out |

45 frames: startup 16, active 4, recovery 25 (movesmith writes frame data from these). No held pose. The wind-up lean (raw 1-59) is cut: at 2x it would add about 30 frames of startup. If the designer wants the lean-back anticipation, a trim from raw 46 costs 7 more frames of startup. Idle: a loop, no curve.

## Data written
`data/clips/oni-heavy-punch.json` and `data/clips/oni-idle.json` (status `proposed`; schema and `tools/validate_data.py` pass, 101 files valid). No `data/sound` file: the repo has no sound schema and no `data/sound` folder yet, so sound cues for the Oni heavy (whoosh at startup, thud and yokai sting at frame 17) wait for that and for the bought packs; flag for the Producer. No walk data until the retake.

## Stills
`assets/specs/clips/stills/oni/`: `oni-heavy-club_f{01,33,35,37}.png` and `oni-idle-club_f{01,36,46,56}.png` (measure_clip skeleton stills), and the club renders `oni-heavy-club-clearance_f{33,35,37}-side.png`, `oni-idle-club-clearance_f01-side.png`, `oni-idle-club-clearance_f33-front.png`.

## Shared clips against the club (free check, 2026-10-16)
`prop_clearance.py --clip` on the game trims of every Ryo clip file, club in the right hand on `oni-rigged.glb`, every 3rd raw tick of `trim_start..trim_end` (speed is ignored; single-tick spikes can be transient). Worst new depth, in cm.

| Result | Clips |
|---|---|
| **Clean** (0 poses above 2 cm) | crouch, generic throw, heavy kick (`ryo-hk`), knockdown, light punch |
| Marginal (one tick, 2.4 cm) | block high (tick 30) |
| Small (up to 7 cm) | get-up (10 of 28 poses, 2-6.6 cm); jump back (3.5-10.9 cm, 6 of 15) |
| **Fail** | block low 32.6 (tick 19); dash back 3-14 (every pose); foxfire cast 39 (tick 35) and 18; hit high 25.5 (tick 61, 11 of 15 poses); hit low 33.9 (14 of 15); jump 23.3 (one tick, 53); jump kick and jump punch 24.4 (all 9; same file); light kick 16.5 (9 of 11); medium kick 29 (ticks 45 and 51); medium punch 35.7 (5 of 10); rising talisman strike 50 (14 of 15); spirit wave cast 19 (4 of 16) |
| Replaced by this spec | idle guard (59 of 75 poses fail, up to 20), walk forward (up to 14), walk guard back (17), walk guard fwd (22), heavy punch (ticks 55-61, up to 11) |

Per-clip numbers are in the scratchpad runs only; re-run with the command in step 4 of the plan to reproduce. Which of these the Oni uses is movesmith's call; a failing clip on a move he keeps needs an Oni take (3 credits) or a trim, a new approval.

## Needed from the designer
1. Approve the walk retake: 611 (3 credits, one take for forward and, reversed, back), take 2 of 3.
2. Decide the failing shared clips: hit reactions (hit high 25 cm, hit low 34 cm) and block low are the ones every fight plays; retakes at 3 credits each, or accept a small visible overlap on a reaction that lasts a few frames.
