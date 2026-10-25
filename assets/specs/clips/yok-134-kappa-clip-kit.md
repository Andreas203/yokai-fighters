# YOK-134 Kappa clip kit: River Grab, Shell Spin and normals (spec step, nothing run)

Ticket YOK-134, branch `YOK-134-kappa-clip-kit-spec`. Rules: F2, F3, F5, F6, C4, C8, G4, T0, V6; AM1, AM5. Author: animator.
**Status: WAITING FOR DESIGNER APPROVAL. No Meshy, Tripo, mocap or Blender job was run. No credits spent.**
The Linear ticket text was not readable from this session (no Linear tool); the brief came from the Producer's message. Check the ticket for anything it adds.

## Source tool after the YOK-146 Tripo redo (from `assets/specs/rigs/skeleton.md`, merged 2026-10-19)
- The shared skeleton is now Tripo's (Mixamo bones, bare names, metres). **Kappa is still a Meshy rig (`kappa-rigged.glb`, 1.551 m, shell cut, shell mounted on `Spine01`) until YOK-149.** The Tripo Kappa will have different weights and a different spine name for the shell mount.
- Meshy library clips reach the Tripo skeleton through `retarget_bvh.py --clip` (`MESHY_CLIP_MAP`, chain position). A Meshy preset is therefore not wasted by YOK-149; only the shell verdict is rig-bound.
- **Per take:** every take below is a Meshy library preset (3 credits, the known pipeline). No Tripo clip price exists in the repo, so no Tripo take is proposed. Mocap is possible only if the designer buys the YOK-146 DeepMotion plan (`yok-146-ryo-heavy-mocap.md`); a shell-safe grab and a spin are poor mocap subjects (a performer cannot spin 3 times a second in place), so I do not recommend it for the Kappa.
- **Timing of the run:** the shell check is the gate. A take that clears the shell on the Meshy Kappa has to be measured again on the Tripo Kappa (0 credits). Recommendation: run only the two specials now (S07 is the highest retarget-clipping risk in clip_needs, and G4 depends on it), and hold the normals until the Tripo Kappa exists.

## Shell safety, the constraint on every preset
The shell is a rigid prop (0.68 x 0.44 x 0.58 m, fit box z 0.79-1.37 m) riding `Spine01`. Rig stress-pose results (`assets/specs/rigs/kappa.md`): deep crouch 2 verts at 2.5 cm, the arm raise, twist and arms-down overlaps are the underside meeting the shoulder blades and hidden under the dome. The check for a take is `tools/blender/prop_clearance.py --clip` on `kappa-rigged.glb` with `shell.glb` and its prep log, rotations only, **pass = no shell vertex more than 2 cm newly inside the body**, every tick (every 2nd tick for presets over 150 ticks), and stills at the worst tick into `assets/specs/clips/stills/kappa/`.

Preset picks must:
- keep the arms in front of the chest or out to the sides, at or below shoulder height (no overhead reach, no arm swung behind the back, no elbows driven back past the ribs);
- keep the torso within about 45 degrees of a twist, and the lean forward or upright, not a deep lean back (the shell sinks into the back and the hips drop);
- not lie on the back, roll, or cartwheel (the downward knockdown is the shared clip's job, not these);
- keep the hips from dropping more than the shared heavy does (see the heavy-foot item below).

## The open heavy-foot decision (-3.2 cm)
After the leg re-pose, Kappa's toe joints against the rig's own rest contact (`retarget_gate.md`): walk +0.5, idle +0.4, throw +1.9, **heavy -3.2 cm** at its deepest tick, 1.2 cm outside the agreed plus-or-minus 2 cm. Ryo's own heavy stays at +1.3 there. The cause: the shared uppercut drops the hips, and the Kappa's hip scale (0.828) puts his feet into the floor. Skin worst-edge counts are 20 / 53 / 35 (walk / heavy / throw) with no visible tearing.

Options (designer decides):
| Option | What | Cost |
|---|---|---|
| A | Accept -3.2 cm: 3 cm of foot under the ground at one tick, hidden by the stage floor, the skin and shell still pass the stills | 0 |
| B | **Give the Kappa his own heavy punch take** (take H1 below): a short-stance palm thrust (a sumo `tsuppari`, which also fits the Sumo Stance modifier), the hips staying level so there is no sink | 3 credits, one of 3 takes for HP |
| C | A per-fighter hip height offset in data (a Kappa-only vertical offset on the heavy). Engine work for gameplay-programmer, not an animation edit | 0 credits, code |
Recommendation: **B**, because it removes the cause rather than hiding it, and the Kappa then does not play a boxing uppercut. If B's take also sinks more than 2 cm, fall back to A.

## Takes (all for approval; Meshy library, 3 credits each, on the Meshy Kappa rig, ids from the preview sheet)
Preset ids for new takes are **not known to me** (I cannot open the library from here); the designer picks from the preview sheet by the search words below, or says "pick for me" and the next session lists three candidates for sign-off before any run. The only ids already known: 259 `Step_Forward_and_Push` (the generic throw, plays on the Kappa), 128 `Heavy_Hammer_Swing` (two-handed overhead, not shell-safe by the rules above).

| Take | Move (frames) | Search words | Must show | Constraints and pass test | Fighters | Cost |
|---|---|---|---|---|---|---|
| S1 | **Shell Spin** S07 (10/18/20, 48 frames) | spin, whirl, tornado, 360 | Startup: a short crouch or tuck, 10 frames (at most 20 raw ticks at 2x). Active: a sustained in-place spin, 18 frames, so the loop must close: one or two full turns in 18 frames means a turn of 9 to 36 raw ticks per revolution at 0.5x to 2x. Recovery: the spin slows and the feet settle, 20 frames | Arms tucked or at shoulder height; in place (strip root); shell clearance every tick; loop closure error under the rejected-walk figure of 3.2 on the Oni measure (`oni-club-clips.md`); the planted foot may pivot, it must not slide more than 1.0 cm per frame | Kappa only (Shell Spin is the Kappa's special; the Tanuki copies Ryo's most invested special, not the Kappa's unless drafted: T0 uses Ryo's model and clips, so a copied Shell Spin would play on Ryo's rig and needs the same retarget check) | 3 credits |
| S2 | Shell Spin second preset | tornado kick, whirlwind | as S1, a different preset if S1 fails | as S1 | as S1 | 3 credits, only if S1 fails |
| G1 | **River Grab** T01 (6/2/30, 38 frames) | grab, seize, bear hug, push | Fast reach in startup 6 (at most 12 raw ticks to the grab at 2x), both arms forward below the shoulders, a pull-in or lift on the grab, a settle in recovery (30). One generic grab and one standard knockdown (C4): **never a paired animation**, the defender plays the generic reaction | Torso upright or leaning forward, no torso twist over 45 degrees, shell clearance; the grab lands at belly height (the Kappa's push landed at 0.91 m on the 259 gate) | Kappa. The generic grab 259 stays for Ryo, Kitsune and Oni | 3 credits |
| H1 | **Heavy punch** N03 (10/4/20, 34 frames; the heavy-foot fix, option B) | palm strike, push, shove, sumo | Short stance, a level-hip drive of both palms or one; early frames work as an anti-air per clip_needs | Hips drop at most as the shared heavy minus 3 cm; toe joints within plus-or-minus 2 cm of the 5.5 cm rest contact on every tick; shell clearance | Kappa | 3 credits |
| N1 | Light punch N01 (4/2/7, 13 frames) | jab | Fast straight punch | shell, feet | Kappa | 3 credits |
| N2 | Medium punch N02 (6/3/12, 21) | cross | Cross punch | shell, feet | Kappa | 3 credits |
| N3 | Light kick N04 (5/2/9, 16) | low kick | Quick shin kick | shell, feet | Kappa | 3 credits |
| N4 | Medium kick N05 (7/3/14, 24) | front kick | Long front kick | shell, feet | Kappa | 3 credits |
| N5 | Heavy kick N06 (12/4/22, 38) | roundhouse | Big round kick that knocks down | **Highest risk**: the twist and the swing of the shell; hips and the planted foot | Kappa | 3 credits |

Take cap: 3 across all sources per move (F5). None used for the Kappa yet (he plays the shared Ryo clips through the hip scale). Frame counts above are the targets from `clip_needs.md`; the retimed frame counts and hit frames are reported after measuring, and movesmith writes frame data from those. Segment limits are 0.5x to 2x with a held pose of at most 4 frames; the engine reads only `trim_start`, `trim_end` and `speed_scale` until `ClipCatalog` reads curves (flag for the Producer, as on `yok-137-oni-specials.md`).

### Cleaning, measuring and retiming (after approval, per take)
`clean_clip.py` (trim, strip root horizontal, resample 60 fps, smoothing only off the key poses); `measure_clip.py` (frame count, foot slide per planted foot, jitter, hit-frame stills); `prop_clearance.py --clip` for the shell; then the retime curve as above. Every setting under `## Cleanup`. `data/clips/kappa-<move>.json` per accepted take, to `data/schema/clip.schema.json`, plus sound cues in `data/sound/` once the sound schema exists (River Grab: a short grab thud and a teal-spark sting at the grab frame; Shell Spin: a whoosh loop under the 18 active frames).

If a special fails after 3 takes: the F5 substitute for River Grab is the generic grab 259 (a free, proven clip); for Shell Spin it is the G4 route: the Kappa uses the folklore human guise with a whirlwind preset (rig-specialist's gate), flagged to the Producer. I never hand-key.

## Cost for approval

| Package | Takes | First pass | Worst case (cap 3 each) |
|---|---|---|---|
| **Recommended now** | S1 Shell Spin, G1 River Grab, H1 heavy punch (heavy-foot fix) | **9 credits** | 27 credits |
| Plus normals (after YOK-149) | N1-N5 | +15 credits | +45 credits |
| Everything | 8 clips | 24 credits | 72 credits |
| Free reuse instead | River Grab = generic grab 259, normals and heavy = the shared Ryo clips as today | 0 | 0 |

Credit price is the 3 credits per Meshy take seen on the Oni and Kitsune runs (`oni-club-clips.md`, `yok-42-kitsune-clips.md`); the current balance is 655 after the Oni runs.

## Decisions for the designer
1. **Heavy-foot -3.2 cm:** A accept, B Kappa's own heavy punch (recommended, 3 credits), or C a data offset (code).
2. **Own takes or reuse:** River Grab and normals, own takes (9 to 24 credits) or the shared clips for free? Recommendation: own takes for Shell Spin, River Grab and the heavy only.
3. **Run before or after YOK-149** (the Tripo Kappa): recommendation, specials now, normals after.
4. **Preset ids** for S1, G1 and H1: your picks from the preview sheet, or "pick for me" and I list three candidates each for sign-off.
5. **T0 on Shell Spin:** if the Tanuki can copy Shell Spin it plays on Ryo's rig, so S1 also has to clear Ryo's Tripo rig (no shell there): a free check, confirm.
6. Whether River Grab's own clip is wanted at all: the generic grab already plays on the Kappa (push at 0.91 m) with startup 5 to 6 frames.

## Takes (log)
(none; nothing has run)

## Cleanup
(none; recorded after the first approved take, with every script setting)
