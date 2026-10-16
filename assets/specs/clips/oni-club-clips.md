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
