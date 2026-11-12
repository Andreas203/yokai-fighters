# Finish QA: feet and clipping on every fighter, every fight clip

Owner: rig-specialist. Branch `YOK-finish-qa`. Rules: F2, F3, F5, F6, G4, T0, AM1, AM5. Date 2026-11-12.
Bodies (locked by the designer today): Tripo Ryo, Meshy Kitsune, Meshy Oni + club, Meshy Kappa + shell, Tripo Tanuki true form (runs Ryo's Tripo clips).

## Method

Everything is measured in the game's own pose, not in Blender:
- `game/tests/captures/finish_qa_capture.tscn` (`FinishQaCapture.cs`, `.Measure.cs`) builds each body with `FighterModel.Create` (the fight's clip library: root motion stripped, hips Y scaled by `HipScale`, game frames through each clip's trim or retime curve) and plays every clip that body plays in a fight: the `FighterAnimSet` pose table, the move clips from `data/moves` through `ClipOverrides`, and the specials it can draft or copy. Ryo and Tanuki: 30 clips each, Kitsune 20, Oni 20, Kappa 28.
- Each game frame is CPU-skinned from the skeleton (about 4,000 to 6,000 sampled vertices per body, plus the club, shell and tails at their `BoneAttachment3D`). Measured per frame: the lowest foot against the floor (y = 0), the lowest body point, sole contact slip, the planted-foot drift (how far a foot that stays on the floor moves), and self-intersection: a vertex of one part inside the core capsule (0.6 of the median radius) of a non-adjacent part. Depth is the median radius minus the distance to the core axis.
- Thresholds for FIX (`tools/rig_qa/verdicts.py`): sinks more than 4 cm (jump-kick and jump-punch skipped: they play in the air); floats more than 4 cm on a standing clip; a planted foot drifts more than 10 cm (walks, the back dash and lying clips skipped, since the sim moves the body there); an intersection at least 8 cm deep on at least 4 frames and 12 vertices. Shallower hits are sleeves, fur and belly volume pressing, not a limb going through a body.
- Contact sheets (`tools/rig_qa/sheets.py`) are in `stills/finish-qa/<fighter>-p<n>.jpg` (after the fixes) and `stills/finish-qa/before/` (before them). Each sheet has one row per clip, worst first. Columns A-H are start, active (`hit_start`), recovery and the worst numeric frame, each from the fight camera and from the fighter's front. Every cell has an id and a frame number. The red line is the floor, drawn over the body. The `.json` next to each sheet maps cell id to clip, frame and view. `summary.json` holds every number.
- Re-run: `tools/rig_qa/run.sh <out> render=1`, then `python tools/rig_qa/rank.py <out>`, `sheets.py <out> <out>/sheets` and `verdicts.py <out>`.

**Kappa note:** the Kappa's "tail" in the tables is his lower belly and seat (hips-weighted vertices more than 28 cm from the hip joint), not a tail. On the Tanuki, "tail" is his real tail.

## Astra pass

The designer approved Astra for this check. One call ran (`gpt-6-astra` via `codex exec`, sheet `before/oni-p1`). It returned a usable JSON verdict for all 80 cells, saved as `stills/finish-qa/before/astra-oni-p1.json`. It agreed with the numbers: the Oni's feet sink in every row, and the club head goes into the floor on the Quake / heavy punch recovery.

The other 12 calls failed before reaching the model ("usage limit, try again at 3:23 PM"), so they produced no verdict. Every other verdict below comes from the numeric pass plus my own reading of the sheets. The prompt is `tools/rig_qa/astra_prompt.txt`, ready to run on the worst sheets after 15:25 (cap: 14 calls left of 15).

## Counts (PASS / FIX)

| Body | Before | After the safe fixes | After Fix B + D in code |
|---|---|---|---|
| Ryo (Tripo) | 10 / 20 | 15 / 15 | unchanged (no offset) |
| Tanuki (true form) | 2 / 28 | 2 / 28 | 2 / 28 (lying sink gone; tail and head hits remain) |
| Kitsune | 9 / 11 | 11 / 9 | unchanged (no offset) |
| Oni + club | 0 / 20 | 0 / 20 | 8 / 12 |
| Kappa + shell | 1 / 27 | 1 / 27 | 1 / 27 (sink gone; arm and belly hits remain) |

Mean planted-foot drift on standing clips, before and after:

| Body | Before | After |
|---|---|---|
| Ryo | 21.9 cm | 10.8 cm |
| Kitsune | 15.2 cm | 4.5 cm |
| Oni | 26.7 cm | 18.1 cm |
| Kappa | 21.5 cm | 18.1 cm |
| Tanuki | 21.7 cm | 19.5 cm |

The Oni and Kappa stayed FIX almost everywhere for one shared reason: the whole body sank (Oni median 8 cm, Kappa median 3 cm). Fix B and the lying part of Fix D are now in code (below).

## Worst issues and their status

| # | Body | Clip | Problem (after fixes) | Status / fix |
|---|---|---|---|---|
| 1 | Tanuki | ryo-knockdown, ryo-get-up | Lying body and big tail sink 42-56 cm through the floor; thigh into tail 15-20 cm | Sink fixed (lying lift curve, code): 56.4 to 0.4 cm, 42.4 to 0.0 cm. Thigh into tail open (E1) |
| 2 | Kappa | kitsune-get-up (kappa-get-up the same) | Legs sink 25-27 cm below the floor at f13; legs into belly 13-17 cm | Sink fixed (lying lift curve, code): 24.7 to 0.0 cm. Legs into belly open (C/D clip) |
| 3 | Oni | every clip | Feet sink 7-16 cm on every standing frame (idle 9.5) | Fixed (Fix B, code + camera): idle 9.5 to 1.5 cm, median 10.0 to 2.5 cm. Residual on borrowed jump / heavy kick / walk (C) |
| 4 | Oni | ryo-get-up, ryo-knockdown | Thigh into belly 17 cm (the belly is wider than the Ryo clip allows) | Open. Fix D (Oni own lying clips; YOK-246 `oni-get-up` measures the same 17-18 cm, so it is not fixed by it) |
| 5 | Kappa | kitsune-hit-high | Forearm and hand into chest 11.6 cm, sinks 7 cm | Open. Fix C (Kappa-proportion retarget with the arms spread) |
| 6 | Tanuki | ryo-jump, ryo-jump-kick/punch, ryo-rising-talisman | Arm or hand into the big head 14-17 cm | Open. Fix C or Fix E (head size is the model) |
| 7 | Oni | ryo-heavy-kick (Meshy file) | Support foot drifted 70 cm; sinks 16 cm | Foot lock applied: drift 70 to 49 cm. Rest is the Oni's longer legs on Ryo's clip (Fix C) |
| 8 | Ryo / Kitsune | heavy kick, foxfire, throw, hits, mk, lk | Support foot slid 17-49 cm while planted | **Fixed** (foot lock): Ryo heavy-kick 46 to 18, foxfire 37/49 to 1/29, throw 36 to 2, Kitsune foxfire 34 to 1, hit-high 26 to 0.3 cm |
| 9 | Kappa | kitsune-medium-kick / kappa-medium-kick | Forearm through the shell 15.8 cm (f10-f22) | Open. Fix C (arm path), or shell-fit trim (Fix E) |
| 10 | Oni | oni-iron-club-charge | Feet drift 47-52 cm, sinks 11 cm, forearm into chest 13 cm | Drift is mostly the charge's `forward_travel` (move data), so it was left unlocked. Sink: Fix B |

Seen on the sheets, not in the numbers (the floor check measures the body only):
- The Kitsune's tails dip through the floor on `kitsune-knockdown` f21-f36 (sheet `kitsune-p1` row 2).
- The Oni's club head goes into the floor on `oni-heavy-punch` / `oni-quake` recovery. Astra flagged this too. It is by design for the Quake slam, and minor.

## Fixes applied (this branch; no paid job, no model file, no frame data)

**Fix A, foot lock (`tools/blender/foot_lock.py`, batch `tools/rig_qa/foot_lock_batch.sh`).**
- What it does: the game pins the hips X/Z, so a take whose hips travel slides its planted foot. The script reproduces the pin. On every raw tick where a foot is planted in the take (toe within 2.5 cm of its lowest point and slow), it re-solves that thigh and shin with analytic two-bone IK in the knee's own bend plane, so the ankle stays where it landed. The foot keeps its own orientation. Locks fade out over 4 ticks.
- What it does not touch: no key time, frame count, trim, retime or hit frame. F2 frame data and `data/clips` are untouched. Nothing is placed by hand (F5). Logs are in `assets/specs/rigs/logs/foot-lock/`.
- Files (19, in place): Tripo `ryo-tripo/` ryo-hk, ryo-foxfire, ryo-grab-2, ryo-lk, ryo-mk, ryo-heavy, ryo-hit-high, ryo-hit-low. Meshy `ryo/ryo-hk`, `ryo-grab-2`, `kitsune/` kitsune-foxfire, -hk, -throw, -mk, -hit-high, -hit-low, `kitsune-heavy`, `oni/` oni-heavy-club, oni-swing-club.
- Not locked: the walks, the back dash and the hit pushback (the sim moves the body there), and the Oni charge (`forward_travel`).
- Checked: `ModelExtentTests`, `BaseFighterProbes`, `HipScale*`, `Clip*`, `TripoRyo*`, `OniKappa*` and `SpecialClip*` pass on the result.
- Side effect: on the throw the feet now meet at the end (Ryo feet touch 5 cm at f27-f38, under the threshold). On the Kitsune the right shin crosses the left 11 cm at f13-f22. Her throw had a foot-into-shin hit of the same depth before the lock, so it stays FIX.
- Tanuki and Oni gain less: they play the same files with other leg lengths and hip scale.

**Tried and reverted, Fix B as a scene offset.** Raising `Mirror/Yaw/Model` by 8 cm in `oni.tscn` and 3 cm in `kappa.tscn` fixes most of the sinking. It breaks three tests:
- `ModelExtentTests.ModelTop_FitsTheCameraFraming`: club top 3.204 m against a framed top of 3.150.
- `BaseFighterProbes.EveryFighterTreeMatchesOriginMain`.
- `BaseFighterProbes.OniMirroredClubStaysOnRightHandWithBody`.

So it is a code and camera change, not a safe data fix. It is now in code, below.

## Fix B and Fix D (lying sink), applied in code (gameplay-programmer)

Presentation only: the sim never reads it (F3), no scene, model, clip or frame-data file changes, replays unchanged.
- `FighterAnimSet.HipOffsetY` (metres, every clip): Oni 0.08, Kappa 0.03; Ryo, Kitsune and Tanuki 0. `ClipHipOffsetY` overrides it per clip where the body's own take already met the floor: Oni `oni-heavy-punch` and `oni-quake` 0.02, `oni-iron-club-charge` 0.05 (at 0.08 they floated 12 cm).
- `FighterAnimSet.LyingHipLift`: a (game frame, metres) curve per lying pose, linear between points, held past the ends; sized from the per-frame sink of the lowest skinned point plus 0.5 cm, kept within 2 cm above it. Tanuki knockdown and KO (same clip and curve, up to 0.57 m), Tanuki get-up (up to 0.43 m, 0 from f24), Kappa get-up (up to 0.23 m, 0 from f20). The lying body now rests on its lowest point (the tail on the Tanuki), so his feet lift off the floor while he lies.
- `FighterModel.BuildLibrary` adds both to the scaled `Hips` Y keys, converted from metres by the node scales above the skeleton.
- Oni camera top `CameraConfig.TallBodyTopMeters["oni"]` 3.15 to 3.23 m: his club tip now measures 3.204 m (ryo-heavy-kick).

Measured with `finish_qa_capture.tscn` (render=0) and `rank.py`, before and after, on the same branch:

| Body | Clips sinking > 4 cm | Clips floating > 4 cm | Median sink | PASS / FIX |
|---|---|---|---|---|
| Oni | 20 to 6 | 2 to 4 | 10.0 to 2.5 cm | 0 / 20 to 8 / 12 |
| Kappa | 20 to 5 | 0 to 7 (worst 6.6, walk-guard-fwd) | 4.7 to 1.7 cm | 1 / 27 to 1 / 27 |
| Tanuki | 15 to 13 | 1 to 1 | 5.5 to 4.1 cm | 2 / 28 to 2 / 28 |

| Clip | Before | After |
|---|---|---|
| oni-idle | sinks 9.5 cm | sinks 1.5 cm |
| oni-heavy-punch, oni-quake | sink 7.2, float 4.3 cm | sink 5.2, float 6.3 cm |
| oni ryo-heavy-kick, kitsune-jump, oni-walk-fwd | sink 16.4, 18.5, 14.2 cm | sink 8.4, 10.5, 6.2 cm (walk floats 8.5) |
| kappa kitsune-idle | sinks 3.1 cm | sinks 0.1 cm |
| kappa kitsune-get-up | sinks 24.7 cm (f13) | lowest point 0.7 to 4.9 cm above the floor every frame (4.9 on the standing end: the 3 cm body offset) |
| tanuki ryo-get-up | sinks 42.4 cm (f13) | lowest point 0.1 to 4.0 cm above the floor every frame (4.0 on the standing end, as before) |
| tanuki ryo-knockdown | sinks 56.4 cm (f25) | lowest point -0.4 to 2.9 cm every frame |

Left for clips (C, D) or the model (E): the Tanuki's standing sink (heavy kick 11.8, hit-high 9.5 cm: other leg lengths on Ryo's clips, no body offset since his idle already stands at 0), the Kappa floats on kicks and walks (the 3 cm offset trades them for the sink), and every intersection row.

## Fixes proposed (for the designer to route)

Clip and data side (no model file):
- **B. Per-body hip offset**, owner gameplay-programmer, code review. A `FighterAnimSet.HipOffsetY` (Oni +0.08 m, Kappa +0.03 m, Tanuki +0.015 m), applied in `FighterModel.BuildLibrary` to the Hips Y keys. It also needs a 0.06 m higher Oni camera top in `CameraConfig.ForBodies` and the two probes above updated. This removes about 80% of the Oni and Kappa FIX rows. The residual per-clip sink is 2-6 cm (walks, hits).
- **C. Body-proportion retargets**, owner animator, free (`retarget_bvh.py`, no paid job). The Oni, Kappa and Tanuki play clips cut for Ryo or Kitsune proportions: arms cross wide bellies, hands go into big heads, and long legs over-sink. YOK-246 started this for the pose clips. The move clips with the deepest hits are next: Kappa heavy-punch / shell-spin / hit-high / medium-kick, Tanuki jump / jump-kick / rising-talisman, Oni foxfire / heavy-kick. Run `foot_lock.py` on each new file.
- **D. Lying clips per body**, owner animator + gameplay-programmer (a `ClipOverrides` / pose-table entry). Tanuki, Kappa and Oni need their own knockdown and get-up with the hips raised by the body's lying thickness, so the tail and belly stay above the floor. These clips would keep the same frame count, so there is no F2 change.

Need a model or weight file change (designer must approve):
- **E1. Tanuki tail**: a tail bone, or weights off the hips over the tail, so it can bend on the floor. Alternative: Fix D alone.
- **E2. Kappa shell-fit**: trim the shell's front rim where the forearm passes on medium-kick f10-f22 (`cut_shell.py`). Alternative: Fix C (arm path).
- No weight problems were found: no tearing or candy-wrapper twists on the sheets.

## YOK-246 pose clips (animator, branch YOK-finish-clips, judged read-only)

Rendered in the game's pose from `2a6c94d8` + `2fd3ca35`. The files were overlaid into this worktree, measured and removed again. Nothing was committed there. Sheets and numbers are in `stills/finish-qa/yok-246/`.

| Clip | Verdict | Feet | Shell / club / limbs | Against the clip it replaces |
|---|---|---|---|---|
| kappa-idle | FIX | sink 2.6 cm | right forearm into chest 10.5 cm (191 f); belly on thigh 9.3 cm; shell clear | same as kitsune-idle on the Kappa |
| kappa-walk-guard-fwd | FIX | sinks 7.6 cm | forearm into chest 11.7 cm (all frames); shell clear | same |
| kappa-walk-guard-back | FIX | sinks 8.0 cm | forearm into chest 10.4 cm; shell clear | same |
| kappa-crouch | FIX | sink 3.5 cm | belly on thighs 9-10 cm (soft, fat body); shell clear | same |
| kappa-block-high | FIX | sink 2.2 cm | left hand into head 10.4 cm (37 f) | same (10.9 before) |
| kappa-block-low | FIX | sinks 6.1 cm | forearms into chest 11 cm; slide 13.7 cm | same |
| kappa-dash-back | PASS | sink 2.9 cm | shell clear, forearm 9 cm on 4 f only | better (sink 3.3 and arm hits before) |
| kappa-get-up | FIX | **sinks 27 cm** (legs through the floor f8-f20) | legs into belly 13-17 cm | same as kitsune-get-up (24.7) |
| oni-crouch | FIX | sinks 9 cm | **club through right thigh** 8-13 cm (all frames) | same as ryo-crouch (club 6 cm) |
| oni-block-high | FIX | sinks 9.6 cm | **club through head** 13.6 cm (f8-f37) | worse (ryo-block-high had hand into club 8 cm) |
| oni-block-low | FIX | sinks 9.9 cm | forearm into chest 10.7 cm; club clear | sink same |
| oni-dash-back | FIX | sinks 11.3 cm | head into club 8.3 cm | same as ryo-dash-back |
| oni-get-up | FIX | sinks 6.8 cm | thigh into belly 17-18 cm | same as ryo-get-up |
| kitsune-dash-back | PASS | sink 2.9 cm | clean (tails clear) | better (ryo-dash-back: sink 3.9, tail into arm 6 cm) |

- The shell is clear on all 8 Kappa clips: no shell hit at the 8 cm level, and none at 6 cm either.
- On the Oni, the club goes through the head on block-high and through the thigh on crouch. Both are visible on `yok-246/oni-p1` (rows 2 and 5). The Oni's sinking is the body-wide Fix B, not these clips.
- `prop_clearance.py` clip mode: the rest pose already reads 0.6 m, so its fault comes before any frame mapping. It probably compares the prop placed from the Godot mount log (cm bone space) against the Blender-imported rig without the armature's own scale or axis change, and clip mode applies the clip's Blender pose-space rotations (relative to each armature's rest), where the game uses absolute local rotations. I did not prove which. Use this capture for prop clearance until it is fixed.

## Per-clip verdicts (after the fixes)

The "Was" column shows the verdict before the foot lock, where it changed.

### ryo

| Clip | Verdict | Problem (game pose) | Proposed fix | Was (before the foot lock) |
|---|---|---|---|---|
| ryo-block-high | PASS | - | - |  |
| ryo-block-low | FIX | sinks 4.4 cm (f3); planted foot slides 12.9 cm (L/R 12.9/4.9) | C (hips offset per clip), A |  |
| ryo-crouch | PASS | - | - |  |
| ryo-dash-back | PASS | - | - |  |
| ryo-fox-mirage | FIX | planted foot slides 29.1 cm (L/R 0.9/29.1) | A done, rest C | planted foot slides 48.6 cm (L/R 36.9/48.6) |
| ryo-foxfire-cast | FIX | planted foot slides 29.1 cm (L/R 0.9/29.1) | A done, rest C | planted foot slides 49.0 cm (L/R 36.7/49.0) |
| ryo-generic-throw | PASS | - | - | planted foot slides 35.9 cm (L/R 35.9/3.2) |
| ryo-get-up | FIX | sinks 7.8 cm (f2); R.arm into R.upleg 10.5 cm (5 frames, worst f18) | D, C |  |
| ryo-heavy-kick | FIX | sinks 4.9 cm (f9); planted foot slides 18.4 cm (L/R 18.4/0.0) | C (hips offset per clip), A done, rest C | sinks 4.1 cm (f22); planted foot slides 46.3 cm (L/R 46.3/0.0) |
| ryo-heavy-punch | FIX | sinks 4.4 cm (f19) | C (hips offset per clip) | planted foot slides 17.0 cm (L/R 17.0/12.3) |
| ryo-hit-high | FIX | planted foot slides 12.9 cm (L/R 12.9/0.4) | A done, rest C | sinks 4.8 cm (f7); planted foot slides 31.2 cm (L/R 9.9/31.2) |
| ryo-hit-low | FIX | planted foot slides 20.1 cm (L/R 20.1/16.3) | A done, rest C | planted foot slides 25.2 cm (L/R 25.2/22.9) |
| ryo-idle-guard | PASS | - | - |  |
| ryo-iron-club-charge | FIX | sinks 4.3 cm (f22) | C (hips offset per clip) | planted foot slides 17.0 cm (L/R 17.0/12.3) |
| ryo-jump | FIX | sinks 4.8 cm (f6) | C (hips offset per clip) |  |
| ryo-jump-back | PASS | - | - |  |
| ryo-jump-kick | PASS | - | - |  |
| ryo-jump-punch | PASS | - | - |  |
| ryo-knockdown | PASS | - | - |  |
| ryo-light-kick | PASS | - | - | planted foot slides 23.9 cm (L/R 23.9/0.0) |
| ryo-light-punch | PASS | - | - |  |
| ryo-medium-kick | PASS | - | - | planted foot slides 17.4 cm (L/R 17.4/0.0) |
| ryo-medium-punch | PASS | - | - |  |
| ryo-oni-quake | FIX | sinks 4.4 cm (f38) | C (hips offset per clip) | planted foot slides 16.9 cm (L/R 16.9/12.3) |
| ryo-rising-talisman-strike | FIX | planted foot slides 18.3 cm (L/R 8.8/18.3) | A |  |
| ryo-river-grab | PASS | - | - | planted foot slides 35.6 cm (L/R 35.6/2.4) |
| ryo-shell-spin | FIX | sinks 4.4 cm (f27) | C (hips offset per clip) | planted foot slides 17.0 cm (L/R 17.0/12.3) |
| ryo-spirit-wave-cast | FIX | planted foot slides 19.0 cm (L/R 11.8/19.0); L.upleg into R.upleg 10.8 cm (20 frames, worst f33); R.upleg into L.upleg 9.8 cm (16 frames, worst f43) | A, C |  |
| ryo-walk-guard-back | PASS | - | - |  |
| ryo-walk-guard-fwd | FIX | floats 4.1 cm (f96, 94 frames) | C (hips offset per clip) |  |

### tanuki

| Clip | Verdict | Problem (game pose) | Proposed fix | Was (before the foot lock) |
|---|---|---|---|---|
| ryo-block-high | PASS | - | - |  |
| ryo-block-low | FIX | planted foot slides 26.1 cm (L/R 9.7/26.1); L.upleg into tail 15.2 cm (18 frames, worst f34); head into R.arm 9.1 cm (11 frames, worst f35) | A, E1 or C, C |  |
| ryo-crouch | FIX | floats 6.3 cm (f1, 1 frames) | B |  |
| ryo-dash-back | FIX | R.hand into head 12.6 cm (19 frames, worst f16) | C |  |
| ryo-fox-mirage | FIX | planted foot slides 48.1 cm (L/R 29.9/48.1); L.upleg into tail 16.6 cm (18 frames, worst f19); L.hand into spine1 11.7 cm (9 frames, worst f12) | A done, rest C, E1 or C, C | planted foot slides 55.7 cm (L/R 50.3/55.7); L.upleg into tail 16 cm (18 frames, worst f19); L.hand into spine1 11.7 cm (9 frames, worst f12) |
| ryo-foxfire-cast | FIX | planted foot slides 48.1 cm (L/R 29.9/48.1); L.upleg into tail 16.6 cm (31 frames, worst f25); L.hand into spine1 11.6 cm (24 frames, worst f15) | A done, rest C, E1 or C, C | planted foot slides 55.6 cm (L/R 50.1/55.6); L.upleg into tail 16 cm (31 frames, worst f25); L.hand into spine1 11.6 cm (24 frames, worst f15) |
| ryo-generic-throw | FIX | planted foot slides 26.7 cm (L/R 13.3/26.7) | A done, rest C | planted foot slides 32.8 cm (L/R 32.8/23.1) |
| ryo-get-up | FIX | sinks 42.4 cm (f13); R.upleg into tail 20.1 cm (22 frames, worst f14); R.leg into tail 15.8 cm (4 frames, worst f1) | D, E1 or D |  |
| ryo-heavy-kick | FIX | sinks 11.8 cm (f9); planted foot slides 32.8 cm (L/R 32.8/0.0); R.upleg into tail 14.5 cm (16 frames, worst f1) | B, A done, rest C, E1 or C | sinks 13.0 cm (f22); planted foot slides 52.9 cm (L/R 52.9/0.0); R.upleg into tail 14.5 cm (16 frames, worst f1) |
| ryo-heavy-punch | FIX | sinks 6.7 cm (f17); planted foot slides 10.5 cm (L/R 0.0/10.5) | B, A done, rest C | sinks 4.9 cm (f11); R.upleg into tail 11.8 cm (19 frames, worst f10) |
| ryo-hit-high | FIX | sinks 9.5 cm (f7); planted foot slides 29.2 cm (L/R 1.2/29.2); R.upleg into tail 14.9 cm (7 frames, worst f1); R.hand into spine1 12.4 cm (4 frames, worst f6) | B, A done, rest C, E1 or C, C | sinks 11.3 cm (f7); planted foot slides 35.7 cm (L/R 1.4/35.7); R.upleg into tail 14.9 cm (9 frames, worst f1); R.hand into spine1 12.4 cm (4 frames, worst f6) |
| ryo-hit-low | FIX | sinks 6.0 cm (f11); planted foot slides 18.7 cm (L/R 8.2/18.7) | B, A done, rest C | sinks 6.0 cm (f11); planted foot slides 22.1 cm (L/R 10.6/22.1) |
| ryo-idle-guard | PASS | - | - |  |
| ryo-iron-club-charge | FIX | sinks 6.7 cm (f21); planted foot slides 10.4 cm (L/R 0.0/10.4) | B, A done, rest C | sinks 4.9 cm (f13); R.upleg into tail 11.8 cm (19 frames, worst f12) |
| ryo-jump | FIX | sinks 4.6 cm (f6); L.arm into head 17.4 cm (28 frames, worst f1); L.forearm into head 15.4 cm (5 frames, worst f2) | B, C |  |
| ryo-jump-back | FIX | sinks 7.3 cm (f22) | B |  |
| ryo-jump-kick | FIX | R.hand into head 14.4 cm (6 frames, worst f16); head into R.arm 8.1 cm (14 frames, worst f6) | C |  |
| ryo-jump-punch | FIX | R.hand into head 14.4 cm (6 frames, worst f16); head into R.arm 8.1 cm (14 frames, worst f6) | C |  |
| ryo-knockdown | FIX | sinks 56.4 cm (f25); R.upleg into tail 15 cm (15 frames, worst f6) | D, E1 or D |  |
| ryo-light-kick | FIX | sinks 5.0 cm (f16); planted foot slides 22.8 cm (L/R 6.4/22.8) | B, A done, rest C | sinks 5.0 cm (f16); planted foot slides 22.8 cm (L/R 6.2/22.8) |
| ryo-light-punch | FIX | planted foot slides 11.0 cm (L/R 11.0/9.8); head into R.arm 8.9 cm (10 frames, worst f5) | A, C |  |
| ryo-medium-kick | FIX | sinks 7.0 cm (f12); L.upleg into tail 12.9 cm (19 frames, worst f9) | B, E1 or C | sinks 8.2 cm (f12); planted foot slides 16.5 cm (L/R 16.5/0.0); L.upleg into tail 12.8 cm (19 frames, worst f8) |
| ryo-medium-punch | FIX | planted foot slides 20.7 cm (L/R 20.7/14.5); L.upleg into tail 19.2 cm (17 frames, worst f13); R.upleg into tail 16.3 cm (18 frames, worst f9) | A, E1 or C |  |
| ryo-oni-quake | FIX | sinks 6.7 cm (f36); planted foot slides 10.5 cm (L/R 0.0/10.5) | B, A done, rest C | sinks 4.9 cm (f24); R.upleg into tail 11.8 cm (31 frames, worst f22) |
| ryo-rising-talisman-strike | FIX | planted foot slides 11.6 cm (L/R 11.2/11.6); R.hand into head 16 cm (35 frames, worst f8) | A, C |  |
| ryo-river-grab | FIX | planted foot slides 26.9 cm (L/R 13.3/26.9) | A done, rest C | planted foot slides 32.8 cm (L/R 32.8/23.6) |
| ryo-shell-spin | FIX | sinks 6.7 cm (f23); planted foot slides 10.5 cm (L/R 0.0/10.5) | B, A done, rest C | sinks 4.9 cm (f11); R.upleg into tail 11.8 cm (29 frames, worst f10) |
| ryo-spirit-wave-cast | FIX | planted foot slides 28.6 cm (L/R 10.3/28.6); L.upleg into tail 15.2 cm (29 frames, worst f41); head into R.arm 8.8 cm (8 frames, worst f5) | A, E1 or C, C |  |
| ryo-walk-guard-back | FIX | sinks 6.2 cm (f35); L.hand into spine 13.1 cm (104 frames, worst f22) | B, C |  |
| ryo-walk-guard-fwd | FIX | sinks 6.8 cm (f10); L.hand into spine 13.9 cm (104 frames, worst f24) | B, C |  |

### kitsune

| Clip | Verdict | Problem (game pose) | Proposed fix | Was (before the foot lock) |
|---|---|---|---|---|
| kitsune-block-high | PASS | - | - |  |
| kitsune-block-low | FIX | planted foot slides 10.8 cm (L/R 10.8/2.4) | A |  |
| kitsune-crouch | PASS | - | - |  |
| kitsune-fox-mirage | FIX | L.forearm into L.upleg 9.1 cm (16 frames, worst f16) | C | planted foot slides 33.4 cm (L/R 33.4/10.4); L.forearm into L.upleg 8.9 cm (16 frames, worst f16) |
| kitsune-foxfire | FIX | L.forearm into L.upleg 9.1 cm (30 frames, worst f21) | C | planted foot slides 33.6 cm (L/R 33.6/10.6); L.forearm into L.upleg 8.9 cm (30 frames, worst f20) |
| kitsune-get-up | FIX | L.arm into L.leg 11.3 cm (5 frames, worst f13); R.upleg into R.arm 8.2 cm (11 frames, worst f7) | C |  |
| kitsune-heavy-kick | FIX | floats 4.7 cm (f38, 30 frames) | C (hips offset per clip) | floats 4.1 cm (f12, 9 frames); planted foot slides 29.2 cm (L/R 29.2/0.0) |
| kitsune-heavy-punch | FIX | R.forearm into R.upleg 8.3 cm (8 frames, worst f5) | C | planted foot slides 10.4 cm (L/R 10.4/2.2); R.forearm into R.upleg 8.3 cm (8 frames, worst f5) |
| kitsune-hit-high | PASS | - | - | planted foot slides 25.7 cm (L/R 6.6/25.7) |
| kitsune-hit-low | PASS | - | - | planted foot slides 17.5 cm (L/R 17.5/15.9) |
| kitsune-idle | PASS | - | - |  |
| kitsune-jump | PASS | - | - |  |
| kitsune-knockdown | PASS | - | - |  |
| kitsune-light-punch | PASS | - | - |  |
| kitsune-medium-kick | FIX | R.forearm into R.upleg 9.9 cm (9 frames, worst f4); R.hand into tail 8.6 cm (10 frames, worst f15) | C | planted foot slides 16.2 cm (L/R 16.2/0.0); R.forearm into R.upleg 10.1 cm (9 frames, worst f1); R.hand into tail 8.6 cm (10 frames, worst f15) |
| kitsune-medium-punch | PASS | - | - |  |
| kitsune-throw | FIX | R.toebase into L.leg 11.7 cm (18 frames, worst f17); R.foot into L.leg 11.2 cm (11 frames, worst f22) | C | floats 4.3 cm (f27, 13 frames); planted foot slides 12.4 cm (L/R 12.4/0.0); R.foot into L.leg 11.3 cm (11 frames, worst f13); R.toebase into L.leg 10.2 cm (10 frames, worst f7) |
| kitsune-walk-guard-back | PASS | - | - |  |
| kitsune-walk-guard-fwd | FIX | floats 5.7 cm (f95, 104 frames) | C (hips offset per clip) |  |
| ryo-dash-back | PASS | - | - |  |

### oni

| Clip | Verdict | Problem (game pose) | Proposed fix | Was (before the foot lock) |
|---|---|---|---|---|
| kitsune-foxfire | FIX | sinks 8.1 cm (f20); planted foot slides 50.3 cm (L/R 10.8/50.3); L.upleg into R.upleg 12.7 cm (26 frames, worst f26); R.forearm into spine01 10.7 cm (4 frames, worst f7) | B, A done, rest C, C | sinks 8.6 cm (f21); planted foot slides 70.3 cm (L/R 53.5/70.3); R.forearm into spine01 10.7 cm (4 frames, worst f7) |
| kitsune-jump | FIX | sinks 18.5 cm (f7); head into L.arm 9.6 cm (8 frames, worst f3) | B, C |  |
| oni-block-low | FIX | sinks 8.7 cm (f1) | B |  |
| oni-heavy-punch | FIX | sinks 7.2 cm (f1); floats 4.3 cm (f12, 13 frames); planted foot slides 27.1 cm (L/R 27.1/8.0) | B, A done, rest C | sinks 11.3 cm (f8); planted foot slides 34.4 cm (L/R 34.4/8.0) |
| oni-hit-high | FIX | sinks 10.0 cm (f33); planted foot slides 11.1 cm (L/R 8.1/11.1) | B, A |  |
| oni-hit-low | FIX | sinks 10.0 cm (f33); planted foot slides 11.1 cm (L/R 8.1/11.1) | B, A |  |
| oni-idle | FIX | sinks 9.5 cm (f1) | B |  |
| oni-iron-club-charge | FIX | sinks 11.1 cm (f3); planted foot slides 52.3 cm (L/R 47.4/52.3); L.forearm into spine01 13.3 cm (32 frames, worst f28); R.hand into R.upleg 12.5 cm (9 frames, worst f5) | B, move data (forward_travel), C |  |
| oni-medium-kick | FIX | sinks 10.9 cm (f16) | B | sinks 10.1 cm (f15); planted foot slides 25.9 cm (L/R 25.9/3.8) |
| oni-medium-punch | FIX | sinks 10.8 cm (f10) | B | sinks 10.2 cm (f9); planted foot slides 25.9 cm (L/R 25.9/3.8) |
| oni-quake | FIX | sinks 7.2 cm (f1); floats 4.3 cm (f23, 13 frames); planted foot slides 27.1 cm (L/R 27.1/8.0) | B, A done, rest C | sinks 11.3 cm (f16); planted foot slides 34.4 cm (L/R 34.4/8.0) |
| oni-walk-fwd | FIX | sinks 14.2 cm (f56) | B |  |
| ryo-block-high | FIX | sinks 9.1 cm (f6); L.hand into club 8.1 cm (7 frames, worst f8) | B, C |  |
| ryo-crouch | FIX | sinks 8.7 cm (f1) | B |  |
| ryo-dash-back | FIX | sinks 11.4 cm (f17); head into club 8.3 cm (19 frames, worst f5) | B, C |  |
| ryo-generic-throw | FIX | sinks 12.1 cm (f27); planted foot slides 14.2 cm (L/R 8.1/14.2); L.hand into club 8.2 cm (10 frames, worst f23) | B, A done, rest C, C | sinks 7.6 cm (f17); planted foot slides 48.3 cm (L/R 48.3/14.2); L.hand into club 8.2 cm (10 frames, worst f23) |
| ryo-get-up | FIX | sinks 6.8 cm (f20); R.upleg into spine01 17.3 cm (11 frames, worst f17); spine01 into L.upleg 17.2 cm (8 frames, worst f21) | D, C |  |
| ryo-heavy-kick | FIX | sinks 16.4 cm (f7); planted foot slides 49.4 cm (L/R 49.4/0.0) | B, A done, rest C | sinks 14.2 cm (f9); planted foot slides 70.1 cm (L/R 70.1/0.0) |
| ryo-knockdown | FIX | sinks 10.2 cm (f1); L.upleg into spine01 17.6 cm (8 frames, worst f26); R.upleg into spine01 17.1 cm (9 frames, worst f29) | D, C |  |
| ryo-light-punch | FIX | sinks 9.1 cm (f1) | B |  |

### kappa

| Clip | Verdict | Problem (game pose) | Proposed fix | Was (before the foot lock) |
|---|---|---|---|---|
| kappa-heavy-kick | FIX | sinks 4.3 cm (f12); planted foot slides 25.5 cm (L/R 25.5/0.0); L.hand into head 12.8 cm (4 frames, worst f15); tail into L.upleg 8.8 cm (23 frames, worst f22) | B, A done, rest C, C | sinks 4.2 cm (f21); planted foot slides 51.5 cm (L/R 51.5/0.0); L.hand into head 12.8 cm (4 frames, worst f15); tail into L.upleg 9.3 cm (27 frames, worst f24) |
| kappa-heavy-punch | FIX | sinks 7.2 cm (f17); L.hand into head 13.2 cm (10 frames, worst f9); R.hand into spine02 11.7 cm (4 frames, worst f1) | B, C | sinks 5.0 cm (f3); L.hand into head 13.2 cm (10 frames, worst f9); R.hand into spine02 11.7 cm (4 frames, worst f1) |
| kappa-light-kick | FIX | sinks 5.3 cm (f16); planted foot slides 10.1 cm (L/R 1.5/10.1); L.hand into head 9.1 cm (5 frames, worst f9); tail into R.upleg 8.3 cm (4 frames, worst f7) | B, A, C |  |
| kappa-light-punch | FIX | sinks 4.7 cm (f11); planted foot slides 12.4 cm (L/R 12.4/10.3); L.upleg into R.upleg 11.5 cm (11 frames, worst f10) | B, A, C |  |
| kappa-medium-kick | FIX | sinks 4.6 cm (f1); planted foot slides 28.4 cm (L/R 4.5/28.4); R.forearm into shell 15.8 cm (13 frames, worst f15); tail into R.upleg 9.5 cm (20 frames, worst f7) | B, A done, rest C, E2 or C, C | planted foot slides 26.5 cm (L/R 16.3/26.5); R.forearm into shell 15.8 cm (13 frames, worst f15); tail into R.upleg 9.5 cm (20 frames, worst f6) |
| kappa-medium-punch | FIX | sinks 4.7 cm (f13); planted foot slides 14.5 cm (L/R 14.5/10.6); L.upleg into R.upleg 11.6 cm (14 frames, worst f12) | B, A, C |  |
| kappa-river-grab | FIX | planted foot slides 42.3 cm (L/R 6.8/42.3) | A done, rest C | planted foot slides 42.3 cm (L/R 31.1/42.3) |
| kappa-shell-spin | FIX | sinks 7.2 cm (f23); L.hand into head 13.2 cm (13 frames, worst f9); R.hand into spine02 11.7 cm (4 frames, worst f1) | B, C | sinks 5.0 cm (f3); L.hand into head 13.2 cm (13 frames, worst f9); R.hand into spine02 11.7 cm (4 frames, worst f1) |
| kappa-throw | FIX | planted foot slides 42.3 cm (L/R 6.8/42.3) | A done, rest C | planted foot slides 42.3 cm (L/R 31.1/42.3) |
| kitsune-block-high | FIX | L.hand into head 10.9 cm (37 frames, worst f22) | C |  |
| kitsune-block-low | FIX | sinks 6.2 cm (f25); planted foot slides 13.6 cm (L/R 8.6/13.6); L.forearm into spine01 11.4 cm (11 frames, worst f1); tail into L.upleg 10.8 cm (20 frames, worst f32) | B, A, C |  |
| kitsune-crouch | FIX | tail into R.upleg 10.7 cm (30 frames, worst f27); tail into L.upleg 9.6 cm (30 frames, worst f16) | C |  |
| kitsune-foxfire | FIX | sinks 4.7 cm (f6); planted foot slides 37.5 cm (L/R 13.6/37.5); R.forearm into spine01 11.5 cm (10 frames, worst f5); L.forearm into spine01 11.3 cm (35 frames, worst f22) | B, A done, rest C, C | sinks 4.7 cm (f6); planted foot slides 47.8 cm (L/R 36.9/47.8); R.forearm into spine01 11.5 cm (10 frames, worst f5); L.forearm into spine01 11.3 cm (35 frames, worst f22) |
| kitsune-get-up | FIX | sinks 24.7 cm (f13); R.leg into tail 16.5 cm (5 frames, worst f2); L.leg into spine02 13 cm (6 frames, worst f13) | D, C |  |
| kitsune-heavy-kick | FIX | sinks 4.2 cm (f12); planted foot slides 25.5 cm (L/R 25.5/0.0); L.hand into head 13.3 cm (5 frames, worst f16); tail into L.upleg 8.8 cm (23 frames, worst f22) | B, A done, rest C, C | sinks 4.3 cm (f22); planted foot slides 50.7 cm (L/R 50.7/0.0); L.hand into head 13.3 cm (5 frames, worst f16); tail into L.upleg 9.3 cm (27 frames, worst f25) |
| kitsune-heavy-punch | FIX | sinks 7.2 cm (f17); L.hand into head 13.2 cm (10 frames, worst f9); R.hand into spine02 11.7 cm (4 frames, worst f1) | B, C | sinks 5.0 cm (f3); L.hand into head 13.2 cm (10 frames, worst f9); R.hand into spine02 11.7 cm (4 frames, worst f1) |
| kitsune-hit-high | FIX | sinks 7.0 cm (f14); planted foot slides 15.1 cm (L/R 1.5/15.1); L.forearm into spine01 11.6 cm (10 frames, worst f7); L.hand into spine01 11.5 cm (14 frames, worst f11) | B, A done, rest C, C | sinks 8.2 cm (f14); planted foot slides 24.1 cm (L/R 2.1/24.1); L.forearm into spine01 11.6 cm (10 frames, worst f7); L.hand into spine01 11.5 cm (14 frames, worst f11) |
| kitsune-hit-low | FIX | sinks 5.3 cm (f3); R.hand into head 12.6 cm (15 frames, worst f13); tail into L.upleg 8.2 cm (10 frames, worst f37) | B, C | sinks 5.2 cm (f1); planted foot slides 15.8 cm (L/R 11.3/15.8); R.hand into head 12.6 cm (15 frames, worst f13); tail into L.upleg 8.9 cm (10 frames, worst f37) |
| kitsune-idle | FIX | tail into L.upleg 10.9 cm (222 frames, worst f159); R.forearm into spine01 10.5 cm (197 frames, worst f57) | C |  |
| kitsune-jump | FIX | sinks 6.3 cm (f4); tail into R.upleg 10.7 cm (16 frames, worst f21); tail into L.upleg 10.7 cm (20 frames, worst f49) | B, C |  |
| kitsune-knockdown | FIX | tail into R.upleg 11.8 cm (49 frames, worst f37); tail into L.upleg 11.6 cm (22 frames, worst f41) | C |  |
| kitsune-light-punch | FIX | sinks 4.7 cm (f11); planted foot slides 12.4 cm (L/R 12.4/10.3); L.upleg into R.upleg 11.5 cm (11 frames, worst f10) | B, A, C |  |
| kitsune-medium-kick | FIX | sinks 4.6 cm (f1); planted foot slides 27.9 cm (L/R 4.6/27.9); R.forearm into shell 15.8 cm (13 frames, worst f15); tail into R.upleg 9.6 cm (20 frames, worst f7) | B, A done, rest C, E2 or C, C | planted foot slides 25.9 cm (L/R 16.2/25.9); R.forearm into shell 15.8 cm (13 frames, worst f15); tail into R.upleg 9.5 cm (20 frames, worst f6) |
| kitsune-medium-punch | FIX | sinks 4.7 cm (f13); planted foot slides 14.5 cm (L/R 14.5/10.6); L.upleg into R.upleg 11.6 cm (14 frames, worst f12) | B, A, C |  |
| kitsune-throw | FIX | planted foot slides 42.3 cm (L/R 6.8/42.3) | A done, rest C | planted foot slides 42.3 cm (L/R 31.1/42.3) |
| kitsune-walk-guard-back | FIX | sinks 7.5 cm (f35); R.forearm into spine01 10.2 cm (104 frames, worst f35) | B, C |  |
| kitsune-walk-guard-fwd | FIX | sinks 7.4 cm (f9); R.forearm into spine01 11.6 cm (104 frames, worst f18) | B, C |  |
| ryo-dash-back | PASS | - | - |  |
