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
