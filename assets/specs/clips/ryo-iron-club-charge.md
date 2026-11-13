# Clip spec: ryo-iron-club-charge (Ryo plays the Oni's Iron-Club Charge, and the Tanuki copy of it)

Status: SPEC, waiting for designer approval. Nothing applied: manifest, `data/clips/ryo-iron-club-charge.json`, the clip GLB and FighterAnimSet are unchanged. Cost: 0 credits (a cached take). Rules: F2, F3, F5, F6, T0, E15, AM24, AM26. Author: animator, 2026-11-12.

## Problem and diagnosis (measured)
Move data (shared with the Oni, AM24, not changed): `data/moves/iron-club-charge.json`, 12 / 6 / 14 = 32 frames, hit frames 13-18, 240 units of forward travel, armour 5-18. Ryo has no club and strikes with closed fists (AM26, E15).

The shipped clip is Tripo `preset:biped:slash` (raw take `raw-tripo/slash.glb`, 396 ticks = 6.6 s), played from raw ticks 168-230 at 2.0x in all three segments.
- The raw take is a two-handed overhead weapon chop. Hands join on a handle at ticks 90-120 (hand separation falls to 14 cm in take units, hands up to 48 cm above the hips in Ryo's units), then the chop drops the head 20 cm by tick 135.
- The shipped window is the tail of that chop. In the game's own render (`stills/charge-fix/current-slash-charge_ingame_strip.png`, frames 1, 8, 13, 25): the torso starts bowed (head 15 cm in front of the hips) and rises to upright (38 cm) while both arms hang at thigh height (hand height relative to the hips -39 to +5 cm over all 32 frames). No arm path to a strike exists: left hand horizontal reach from the hips 45 cm at frame 1, 49 cm at frame 13, 45 cm at frame 18 (peak 50 at frame 12); hand speed in the window peaks at 4.5 cm per game frame against 24 cm per tick at the chop (tick 123). `measure_clip.py` on the window suggests a "hit" window of frames 26-35 at 13.9 cm of reach: nothing happens on the declared hit frames 13-18.
- So it reads as someone who has just swung a heavy weapon and is straightening up from the follow-through. Diagnosis confirmed.
- Side finding for the Producer: in the Tripo batch b, `slash` and `chop` are the same animation (identical key data, both 6.6 s, hash 912d6e05). `ryo-oni-quake` (chop, ticks 140-250) and `ryo-iron-club-charge` (slash, ticks 168-230) are therefore two windows of one take.
- In-game QA of the shipped clip, Ryo: PASS (sink 1.1, float 2.6, drift 4.8 / 0.0 cm). Tanuki (copies it, T0): FIX (sinks 16.3 cm at f1, hand and forearm into spine 14.6 cm over 18 frames).

## Source
Cheapest that can work, and it works: a free, already paid take in `C:/Users/Andreas/projects/wf-clips/assets/specs/clips/raw-tripo/` (rebuild source: batch a, task 2d7e7550-47ce-41f6-8063-c4fcde97c744, `ryo-batch-a`).
- **Recommended: Tripo `preset:biped:box_01`** (133 ticks, 2.2 s), a left-lead lunging straight punch: left-hand extension 18 to 42 cm over ticks 23-38 (arm length 42), hand 64 cm out from the hips at the peak (31 cm at the start), torso lean 4 to 20 cm into the punch, hips down 7 cm, wide forward step, then the arm eases back over ticks 44-62. A fist driven forward behind a lunge reads as a charge with the shoulder and fist leading.
- Take count for this move (F5, cap 3 across all sources): the slash take is take 1 (rejected by this spec), box_01 is take 2. One take remains. No credits are spent either way.

## What it must show (clip_needs)
- Anticipation (frames 1-12): the step and weight shift into the lunge, fist drawn back. Box_01 ticks 17-34: the step lands, the hand dips from 23 to 18 cm extension, then drives.
- Strike (frames 13-18): the fist at full reach. Box_01 ticks 34-42: left-hand extension 40, 41, 41, 42, 42, 41 cm on frames 13-18 (all at least 95% of the window peak of 42), fist at shoulder height, 61-64 cm from the hips.
- Follow-through (frames 19-32): the arm holds out through frame 25 and eases back, the torso rises 7 cm. Ticks 42-62.

## Constraints
- In place (root horizontal travel stripped, `--strip-root horizontal`); the 240 units of travel stay move data.
- Shared Tripo skeleton, Ryo's rig (`ryo-tripo-rigged.glb`); plays on the Tanuki (T0) through the same file, so it needs a Tanuki hips offset too (see "Engine follow-ups").
- Facing: the Tripo boxing presets stand about 90 degrees off the fight camera's axis (in the game render the unrotated take punches toward the camera). The yaw is baked into the clip by `clean_clip.py --yaw -90` (a constant turn of the root rotation channel, scripted, no hand-placed key). `yaw_offset` in the clip data stays 0 because the engine does not read it yet.
- Fists: closed fists in the render (the fist-morph hands on the Tripo Ryo). No prop.

## Fighters
Ryo (Tripo rig) and the Tanuki through Ryo's clip root. The Meshy Ryo twin file in `clips/ryo/` is rebuilt by the same script from the same take.

## Candidates measured (all free, cached)
All run through `clean_clip.py --strip-root horizontal`, then `retarget_bvh.py --src-skeleton tripo` onto Ryo's rig; the strike windows retimed to 12 / 6 / 14 with each segment inside 0.5x-2x; in-game numbers from `tools/rig_qa` (FinishQaCapture, the game's own skinned body, in a scratch copy of the project so nothing here was touched). QA limits: sink and float at most 4 cm, planted drift at most 10 cm, a limb into the body at most 8 cm deep on 4 frames or more.

| Take | Strike on the declared frames 13-18? | Foot slide | Floor (after a per-clip hips offset) | Limbs through body (game body) | Jitter | Verdict |
|---|---|---|---|---|---|---|
| slash (shipped) | No: hands hang at thigh height, reach 45 to 49 cm from the hips and flat | 0.25 cm per frame, drift 4.8 / 0 cm | sink 1.1, float 2.6 | max 8.6 cm on 13 frames (thigh on thigh); no arm into torso beyond 7.4 | 3.0 deg/frame^2 | Fails the brief (weapon follow-through, no strike); numbers are clean |
| **box_01 (yaw -90)** | **Yes: extension 40-42 cm on all six frames, fist at shoulder height** | window at 1x 1.38 cm per frame on the first plant (movement clip, not judged), retimed drift **9.7 / 9.5 cm**, worst frame 2.05 cm | offset -0.21 m: sink 2.9, float 3.4 (2 frames) | max **6.8 cm** (forearm into spine1, 29 frames); all others 4.9 or less | 5.9 deg/frame^2 at 1x, about 14 at the 1.54x peak segment (warn 20) | **PASS** on Ryo; drift is 0.3 cm under the limit |
| box_03 (yaw -90) | Partly: peak 39 cm at frame 14, then the arm folds (34 at frame 17, 31 at frame 18); only 12 ticks at 85% of peak | drift 0 / **10.1 cm** | sink 1.9, float 3.3 | max 6.2 cm | 10.2 deg/frame^2 at 1x (about 25 at 1.6x, over the warn line) | FIX (drift), lean only 8 cm, hips rise 12 cm, reads as a jab not a drive |
| box_02 | No: left hand held at 26-27 cm extension (of 39) through all three punches; it is footwork with a guard | 0.42 cm per frame | n/a | n/a | 17.8 deg/frame^2, 22 reversals | Rejected (no discrete strike, jittery) |
| front_kick_02 | A kick, not a fist or shoulder | worst slide 1.94 cm per frame (WARN) | n/a | n/a | 10.2 | Rejected (not a fist, foot slide) |
| hug | Arms forward to grab, no strike; two-handed | 0.86 | n/a | n/a | 7.4 | Rejected (a grab reads as the generic throw, C4) |
| cast_a_spell, flip, turn, jump, walk, lift_heavy, idle, defeat_02, hit_to_head/stomach | Not measured further: hand reach from the shoulder at most 32% of the arm (turn, jump, walk, lift_heavy, idle), or a cast, a flip or a reaction | | | | | Rejected by role; cast_a_spell is Foxfire's |
| chop | Identical data to slash (same take) | | | | | Rejected |

Stills: `assets/specs/clips/stills/charge-fix/box01-yaw-90_ingame_strip.png` (the game's Ryo, fight camera, frames 1, 13, 20, 25) against `current-slash-charge_ingame_strip.png`. Skeleton stills of the shipped clip: `cur-charge_f01/f13/f15/f18.png`.

Not run in a live fight (the capture rig plays the clip by frame, not the sim's 240-unit travel): the designer should look at the strip first, then at the move on screen after it lands.

## Retiming (F2, AM5): the curve, segments inside 0.5x to 2x, no hold
Ticks are the cleaned take's ticks (tick = raw tick, root-stripped, no trim; frame n of the file = tick n-1), 60 per second.
| Game frames | Ticks | Speed | Shows |
|---|---|---|---|
| 1-13 (startup, 12 frames) | 17 to 34 | 1.42x | the step lands, the fist chambers, then drives |
| 13-19 (active, hit 13-18) | 34 to 42 | 1.33x | the fist at full reach (40-42 cm) through all six active frames |
| 19-32 (recovery, 14 frames) | 42 to 62 | 1.54x | the arm holds out then eases back, the torso rises |
Frames as retimed: total 32, startup 12, active 6, recovery 14, `hit_start` 13, `hit_end` 18. This matches the Oni's frame data exactly; Movesmith has nothing to change. `ClipCatalog` already reads `retime` curves, so no engine flag is needed for this one.

## Proposed manifest entry (NOT applied)
Replace the entry `ryo-iron-club-charge` in `tools/finish_clips_manifest.json` with:
```json
{
 "id": "ryo-iron-club-charge",
 "body": "ryo",
 "src": "../assets/specs/clips/raw-tripo/box_01.glb",
 "preset": "box_01",
 "clean": true,
 "yaw": -90,
 "source": "tripo-library",
 "clip": "preset:biped:box_01",
 "target": { "startup": 12, "active": 6, "total": 32 },
 "retime_override": [
  { "frames": [1, 13],  "ticks": [17, 34], "note": "step lands, fist drives out" },
  { "frames": [13, 19], "ticks": [34, 42], "note": "fist at full reach through the active frames" },
  { "frames": [19, 32], "ticks": [42, 62], "note": "arm holds then eases back" }
 ],
 "movement": true
}
```
- `src` is under `assets/specs/clips/raw-tripo/` in this worktree, which does not exist here (91 MB, untracked); it lives in `wf-clips`. Copy `box_01.glb` over (or `split_tripo_batch.py` from the batch-a model) before the rebuild.
- `yaw` is new: I added the optional `--yaw DEG` flag to `tools/blender/clean_clip.py` and the matching manifest key in `tools/finish_clips.py` (both additive, default off, uncommitted in this worktree: `git diff tools/`). Verified: yaw -60 and -90 both turn the body rigidly, with the identical in-game metrics as the unrotated take.
- Rebuild: `python tools/finish_clips.py --only ryo-iron-club-charge` (also rewrites the Meshy-rig twin `clips/ryo/ryo-iron-club-charge.glb` and `data/clips/ryo-iron-club-charge.json`, `source` stays `tripo-library`, `clip` becomes `preset:biped:box_01`).

## Cleanup (scripted, nothing hand-keyed; F5)
1. `clean_clip.py --clip box_01.glb --strip-root horizontal --yaw -90`: resample 60 fps, root horizontal travel stripped (the start position kept), constant -90 degree turn of the root rotation channel about the vertical axis, no smoothing (jitter 5.9 is far under 40), no trim (the window is the retime curve).
2. `retarget_bvh.py --src-skeleton tripo --rig ryo-tripo-rigged.glb` (default flags, same as every finish clip).
3. Measure: `measure_clip.py` on the whole take (PASS, slide 0.93, accel 5.9) and on the window (WARN only on foot slide of the first plant, a movement clip), then the in-game QA above.

## Engine follow-ups (not mine, flag for the Producer)
- **Hips offset (C# data, gameplay-programmer):** the retargeted box_01 floats 18.1-24.4 cm (the same effect as the accepted kitsune-light-punch at -0.147 and kappa punches at -0.13 to -0.16). Add `["ryo-iron-club-charge"] = -0.21f` to `FighterAnimSet.RyoTripo` (it passes `clipHipOffsetY` to `RyoBody`, currently none). Without it the QA verdict is FIX (floats 24.4 cm).
- **Tanuki (T0):** he plays the same file on his own body. Measured with box_01 and no offset: float 1.5 to 10.9 cm (median 6.4), forearm into spine1 11.1 cm. The Ryo offset of -0.21 sinks him 18.9 cm, so he needs his own entry in `TanukiTripo` (about -0.064, the median float) and the 11.1 cm belly clip stays a FIX (over the 8 cm line). That is better than the shipped clip (sinks 16.3 cm, hand and forearm into the spine 14.6 cm over 18 frames, QA score 138 against 47), but it is not a pass. If the designer wants the Tanuki to pass, the options are a Tanuki-only retake or accepting it as a G4 note; I recommend accepting it for now since the shipped clip is worse.
- **Check on the kept accepted clips:** `kitsune-light-punch`, `kappa-light-punch` and `kappa-heavy-punch` are box_01 / box_03 takes accepted by QA without a facing check. The same take punches about 90 degrees off the camera axis on Ryo; check how they face in a live fight (a one-line `--yaw` rebuild fixes each).

## Takes
| # | Source | Task / file | Cost |
|---|---|---|---|
| 1 | Tripo `preset:biped:slash` | `raw-tripo/slash.glb`, batch b `c13aaff5` | 10 credits (already spent) |
| 2 | Tripo `preset:biped:box_01` | `raw-tripo/box_01.glb`, batch a `2d7e7550-47ce-41f6-8063-c4fcde97c744` | 10 credits (already spent) |
New spend for this fix: **0 credits**. Balance unchanged (160, not rechecked, no paid command run).

## If the designer wants more "charge" than a lunge punch
No shoulder-charge preset exists in Tripo rig v1.0. Paid presets not yet taken (`run`, `flee_01`, `flee_02`, `hit_to_body_01/02`, `hurt`) are 10 credits each, but none is a forward strike: a `run` is a sprint with no hit frame. I do not recommend a paid take. The better upgrade is a short video-capture of the designer driving a shoulder-first lunge (camera side-on, full body, 3 repetitions, 4 m of room), cleaned the same way; that is a designer-run session, not requested here.
