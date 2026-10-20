# YOK-137 Oni Quake (on Ryo's rig) and Iron-Club Charge: clip spec (spec step, nothing run)

Ticket YOK-137, branch `YOK-137-oni-special-clip-specs`. Rules: F2, F3, F5, F6, C3, T0, E15; AM1, AM5. Author: animator.
**Status: WAITING FOR DESIGNER APPROVAL. No Meshy, Tripo, mocap or Blender job was run. No credits spent.**
The Linear ticket text was not readable from this session (no Linear tool); the brief came from the Producer's message. Check the ticket for anything it adds.

## Source tool after the YOK-146 Tripo redo (read from `assets/specs/rigs/skeleton.md` and `ryo-tripo.md`, merged 2026-10-19)
- The shared skeleton is now Tripo's (Mixamo bones with bare names `Hips`, `Spine`..`Spine2`, no fingers, metres, Ryo 1.75 m). **Ryo's rig is Tripo; Oni and Kappa stay Meshy rigs until YOK-148 and YOK-149.**
- Meshy library clips still reach the Tripo skeleton: `tools/blender/retarget_bvh.py --clip` maps them by chain position (`MESHY_CLIP_MAP`), and the stopgap Ryo clips in `game/assets/generated/clips/ryo-tripo/` were made that way. A Meshy preset is **not** invalidated by the redo.
- Tripo clip generation: there is no Tripo MCP and no Tripo clip price in the repo. I do not propose a Tripo take; if the designer wants one, the price has to come first.
- **Per take:**
  - Quake: the existing Meshy preset 128 take, retargeted to Ryo's Tripo rig (free). Quake has to play on Ryo's rig (T0).
  - Iron-Club Charge: a Meshy library preset, club-checked on `oni-rigged.glb` today. Re-measure on the Tripo Oni when YOK-148 lands, because club clearance depends on the rig.
  - Mocap is an alternative for either move if the designer buys the YOK-146 DeepMotion plan. That tool can bake onto a Mixamo rig, so it matches the Tripo skeleton. It adds seconds of video, not a new subscription (`yok-146-ryo-heavy-mocap.md`, section 2).

## 1. Oni Quake on Ryo's rig (S06)

### What it must show (clip_needs S06)
Two-hand overhead ground slam with a hits-low read. Anticipation: a rise and a held overhead tell. Key poses: overhead peak, impact pose with the hands low in front of the shins. Follow-through settle. In place. One frame table and one curve for Oni and for Ryo/Tanuki (T0), since the move data is shared.

### Step 0, the free check: does Heavy_Hammer_Swing retarget to Ryo with no club?
Source: Meshy preset 128 `Heavy_Hammer_Swing`, the accepted Oni heavy take, task `01a14515-ba26-7266-bea2-b233e1d2f6e2`, 113 raw ticks, raw GLB in `game/assets/generated/characters/oni/takes/` (uncommitted, main checkout; not in this worktree). Today `data/clips/oni-quake.json` and `oni-heavy-punch.json` reuse it for Oni only.

Procedure (scripts only, F5):
1. `retarget_bvh.py --clip` the raw take onto `ryo-tripo-rigged.glb` through `MESHY_CLIP_MAP`. The raw Oni rig is 2.0 m and Ryo 1.75 m; the hip scale is a data ratio (`FighterModel.HipScale`), not a re-key. Check it on the first run.
2. `clean_clip.py`: strip root horizontal, trim raw 60-112, resample to 60 fps, no smoothing across raw 76 (peak) or raw 92-96 (impact). Record the settings under `## Cleanup`.
3. `measure_clip.py` (with the YOK-146 name normaliser) and `skin_clip_check.py` on Ryo's Tripo rig. Stills at the peak (raw 76), the impact (raw 92 and 96) and the recovery into `assets/specs/clips/stills/quake/`.
4. Gates:

| Check | Pass | Why this clip may fail |
|---|---|---|
| Planted-foot slide | at most 1.0 cm per frame per planted foot | The Oni take already WARNed: the left foot shuffles 1.04 cm per frame, 14 cm net over the slam. A faster slam curve raises the per-frame figure |
| Hands at impact | both hands below 0.45 m and 0.9-1.2 m forward of the hips | With no club the empty hands are the low point (the club tip was the 0.42 m low point). If they stop at belt height the hits-low read is lost (the box is data, C3, but the pose has to agree) |
| Skin on Ryo's rig | no worse than the walk/heavy figures on `ryo-tripo.md`; no tear in the stills | The slam folds the spine forward. The known Tripo FAIL is the deep-crouch volume (charm and pouch), not a forward fold, but it is checked |
| Jitter | at most 20 deg per frame squared (the preset was 13.7 on Oni) | Retiming at 1.6-2x scales the per-frame acceleration up |
| Hands pair | both hands keep a similar height from the peak on | The preset is two-handed; with no club a hand can drift off the shaft line and read as a one-hand swat |

If all pass: write `data/clips/ryo-quake.json` (`status: proposed`, same curve as the Oni file). 0 credits, 0 takes spent (Quake has used none of its own; take 128 is shared with Oni's heavy). If it fails, log it and use the fallbacks below.

### Retiming, the two timings the designer must choose between
Raw landmarks of preset 128: lean-back wind-up raw 1-46, rise to the overhead peak at 76 (club tip 2.36 m), slam 85-97, impact pose 92-96, hunched hold to 112. Segment limits: 0.5x to 2x each, a held pose at most 4 frames.

**Option A: 27/4/25, 56 frames (the file now, `data/clips/oni-quake.json`).**

| Game frames | Raw ticks | Speed | Role |
|---|---|---|---|
| 1-17 | 60-76 | 1x | charged rise |
| 17-20 | 76 | hold 4 | overhead tell |
| 20-28 | 76-92 | 2x | the slam falls |
| 28-32 | 92-96 | 1x | **hit 28-31** |
| 32-40 | 96-104 | 1x | recovery |
| 40-56 | 104-112 | 0.5x | recovery eases out |

**Option B: 22/5/26, 53 frames (rules.md and the GDD value; fits on paper by my arithmetic).**

| Game frames | Raw ticks | Speed | Role |
|---|---|---|---|
| 1-9 | 60-76 | 2x (the ceiling) | rise |
| 9-13 | 76 | hold 4 | overhead tell |
| 13-23 | 76-92 | 1.6x | the slam falls |
| 23-28 | 92-96 | 0.8x | **hit 23-27, 5 frames** |
| 28-53 | 96-112 | 0.62x | recovery, 26 frames |

Check: 8 + 4 + 10 = 22 startup, 5 active, 26 recovery, 53 total. Every segment is inside 0.5-2x and the hold is 4. Cost of B: the rise is at the 2x ceiling and the telegraph is about 12 frames (0.2 s) against 20 (0.33 s) in A. Measured jitter and foot slide decide; the arithmetic is not a measurement.

The engine does not read retiming curves yet: only `trim_start`, `trim_end` and `speed_scale` reach the game until `ClipCatalog` does. **Flag for the Producer**: both options need the curve engine. With `speed_scale` alone the closest fit is raw 60-112 at 1x, a 53-frame clip with the impact near frame 33, which matches neither option.

### If step 0 fails (take cap 3 across all sources; none used)
| Take | Source | Settings | Cost |
|---|---|---|---|
| Q1 | Meshy library, a two-hand overhead smash picked from the preview sheet (id supplied by the designer before approval; I cannot open the library from here), retargeted to Tripo | prefer presets that start with the arms down or out | 3 credits |
| Q2 | Mocap, **only if the YOK-146 DeepMotion plan is bought**: empty hands, overhead two-hand slam onto the floor, 3/4 side camera, 3 reps in one upload, hold the impact 0.1 s | one upload is one take | plan only (about USD 15 a month, to verify) |
| Q3 | F5 substitute: Quake data on an approved clip (Ryo's heavy punch), flagged | none | 0 |

### Quake decisions for the designer
1. **Timing.** 27/4/25 (A, the clip as built) or 22/5/26 (B, the written rule)? Recommendation: B if step 0 passes at that timing, because rules.md, the GDD and the move list all say 22/5/26, and moving it is a rules edit and a balance change at the mid-week-3 gate. Otherwise A, with movesmith flagging the delta (F2: the clip is truth). Damage 90 either way. Movesmith writes the frame data from the as-retimed frames after the approval.
2. Approve the free retarget check (0 credits).
3. If it fails: Q1 (3 credits) first, or Q2 if the mocap plan is bought.

## 2. Iron-Club Charge (S05)

### What it must show
A lunging forward charge with the club or shoulder in the lead. Anticipation: a clear lean or crouch to start. Key pose: body low, club forward at the strike. A strike pose held through the 6 active frames, then a follow-through that slows (recovery 24). The 1-hit armour is data. In place; rush distance and speed are move data. The arms must start low or out in front, not folded: folded forearms push the club through the chest (4 of 5 Oni takes, `oni-club-clips.md`).

### Why the current clip cannot reach 46 frames
`data/clips/oni-iron-club-charge.json`: preset 654 `Rifle_Charge_inplace`, 14 clean frames (raw 20-33, `oni-charge-club.glb`), `hit_start` and `hit_end` null. The raw 33-tick cycle is a stride and the club touches the shoulder at raw 14-19 (up to 32 cm), so 14 ticks are usable. The limits: 14 ticks at 0.5x is 28 frames, plus one 4-frame hold is 32. A 16/6/24 clip is 46 frames from 14 ticks, a mean of 0.30x, below the 0.5x floor. The take also has no strike moment: a run has no pose that reads as a hit.

### Options (designer picks one)
| Option | What | Cost | Reaches 46? | Risks |
|---|---|---|---|---|
| A | **New take** of a preset with its own anticipation, drive and strike, at least 23 clean raw ticks | 3 credits per take; 2 left of the cap (654 was take 1), so 3 to 6 | Yes: 23 ticks at 0.5x plus a hold fits 16/6/24 | The club check decides. A new id is needed (take C2) |
| B | **Scripted splice** of two accepted clips: the 654 clean run (14 ticks) into the 128 overhead fall, impact and recovery (raw 76-96 and on), with a scripted blend | 0 credits; needs a new `splice_clips.py` (script gap, a ticket) | Yes: run 14 + blend + fall + hit 6 + recovery 24 | It reads as run-then-overhead smash, not a shoulder charge. A pop at the join. The club check already passes on each half |
| C | **Fit the data to the clip**: 14 frames at 0.5x plus a hold, about 32 frames, e.g. 10/6/16 | 0 credits | No: the clip is about 32 frames and rules.md and the GDD row 16/6/24 change | A 110-damage punish with 16 frames recovery is safer on block; balance impact. A 0.5x run reads as slow motion |
| D | Mocap (only with the plan): arms-forward lunge with a shoulder lead, no club, 3 reps | plan only | Yes (about 0.7 s of video per rep) | The club is not in the capture, so the club check still runs on the retarget |

Recommendation: **A with one take** (3 credits, table kept). B is the free fallback if both A takes fail the club check.

### Takes for approval (Iron-Club Charge)
| Take | Source and preset | Settings | Pass test | Cost |
|---|---|---|---|---|
| C2 | Meshy library, a sprint-start or running-tackle preset with a low lead lean. **Id to be supplied by the designer from the preview sheet before approval.** Known fallback: **673** `Standard_Forward_Charge_inplace` (the arms pump; the old pumping-arm Oni walk failed the club check) | Oni's Meshy rig `01a144ad-b98d-7520-8376-c776c414a476`; in-place variant; raw GLB to `game/assets/generated/characters/oni/takes/oni-charge-<id>.glb` | `prop_clearance.py --clip` on `oni-rigged.glb` with `club-grip.glb`, `--ignore-radius 0.12`: at most 2 cm newly inside the body on at least 24 consecutive ticks; jitter at most 20 | 3 credits |
| C3 | A second preset, only if C2 fails | as C2 | as C2 | 3 credits |
| C-splice | Option B instead of a take | scripted | clearance on every tick of the joined clip | 0 |

After a pass: `clean_clip.py` (trim to the clean window, strip root horizontal, no smoothing across the strike pose), `measure_clip.py`, then the curve:
- startup 16: lean and drive (anticipation 4-6 frames, the club comes forward);
- active 6: the strike pose, a 4-frame hold plus 2 slow frames;
- recovery 24: ease out at 0.5-0.8x.
Segment edges are fixed after measuring. Write `data/clips/oni-iron-club-charge.json` (replacing the 14-frame record) with `hit_start` and `hit_end` filled. Clearance to note for rig-specialist: club into the shoulder and chin on a pump, club tip on the floor on a deep lean (keep it above 0.4 m).

### Iron-Club Charge decisions for the designer
1. **Length:** A (new take, 3-6 credits, table kept), B (free splice, needs a script ticket), C (data to about 32 frames, changes rules.md and the GDD) or D (mocap).
2. Approve the C2 preset id (your pick from the preview sheet) and the cap use (take 2 of 3).
3. If C: who rewrites the 16/6/24 row (movesmith with the balance gate), since it is in rules.md.

## Cost for approval

| Take | What | Cost | Status |
|---|---|---|---|
| Q0 | Quake: retarget check of Heavy_Hammer_Swing onto Ryo's Tripo rig, no club | 0 credits | needs approval |
| Q1 | Quake fallback: Meshy overhead-smash preset (designer's id) | 3 credits | only if Q0 fails |
| Q2 | Quake fallback: mocap in the YOK-146 session | plan only (about USD 15 a month, to verify) | only if the plan is bought |
| C2 | Iron-Club Charge Meshy take (option A) | 3 credits | recommended |
| C3 | Iron-Club Charge second take | 3 credits | only if C2 fails |
| C-splice | Option B | 0 credits, a script ticket | alternative |

Recommended spend: **3 credits** (C2). Worst case under the F5 cap: Quake 3 + Charge 6 = 9 credits. Credits so far on the Oni: 42 (balance 655).

## Script gaps
1. `retarget_bvh.py --clip` onto the Tripo rig exists (YOK-146) but has only run on stopgap Ryo clips, not on a 2.0 m Meshy take. Check the hip scale on the first run.
2. `splice_clips.py` (option B) does not exist.
3. `ClipCatalog` does not read retiming curves.

## Takes
(none; nothing has run)

## Cleanup
(none; recorded after the first approved take, with every script setting)
