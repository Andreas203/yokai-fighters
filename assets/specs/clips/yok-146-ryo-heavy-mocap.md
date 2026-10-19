# YOK-146 Ryo heavy punch and heavy kick: video mocap capture spec (spec step, nothing run)

Ticket YOK-146 (Tripo redo: Ryo, pilot). Rules: F2, F3, F5, F6, C8, E6, E15, T0, AM5. **Status: WAITING FOR DESIGNER APPROVAL. Nothing has been bought, captured or run.**
Author: animator. The Linear ticket text was not readable from this session; the brief came from the Producer's message. Check the ticket for anything it adds.

Context (designer decisions, 2026-10-19):
- Ryo is re-rigged in Tripo. **Tripo's skeleton becomes the shared skeleton** (Option A): Mixamo-preset Tripo rig, `mixamorig:` bones, 3 spine bones, fingers, no twist bones. rig-specialist is rewriting `assets/specs/rigs/skeleton.md` on `YOK-146-tripo-ryo-rig`. This spec does not edit it; bone names below are the Mixamo names and must be checked against the rewritten file.
- The designer wants new, good heavy punch and heavy kick clips from **video mocap**. These are the first new clips on the Tripo rig. Each move's take cap is 3 across all sources (F5).
- Tanuki copies specials only (T0), not normals, so these two clips only need to work on Ryo's rig. Kitsune and the other yokai have their own heavies (`data/clips/*-heavy-*.json`); they are out of scope.

## Current state (what we are replacing)
| | Heavy punch (HP) | Heavy kick (HK) |
|---|---|---|
| Clip now | Meshy 194 `Right_Uppercut_from_Guard`, 34 frames, hit 11-14 | Meshy 215 `High_Kick`, 38 frames at 1.25x, hit 12-15 |
| Frame data now | startup 10, active 4, recovery 20 (C8 10/4/20), damage 80, hitstun 24, blockstun 21 | startup 11, active 4, recovery 23 (C8 target 12/4/22, the clip won, F2), damage 90, hitstun 27, blockstun 24, knockdown |
| Move-list text (`MoveListSource.cs`) | "Slow, heavy punch. Big damage if it lands." | "Wide sweeping kick. Slow, but it hits hard." |
| Known faults | A tight uppercut, not a "heavy punch" read. Arm at 74% reach at the cut. | Head-high (foot at 129-144 cm), but the designer asked for a mid kick and the library has none (E6: crouch-blocking also blocks mids, so a mid cannot be ducked; a high kick can be). 50 cm of hip travel, 21 cm slide of the planted foot if stripped. Foot at 66% reach at the cut. Not "wide sweeping". |
| Boxes | `data/moves/ryo-heavy-punch.json`, placed on the old poses (YOK-33) | `data/moves/ryo-heavy-kick.json`, same |

Both moves' hitboxes and hurtboxes were read off the old poses. After the new clips are approved, movesmith redoes frame data AND boxes from the new stills (F2). Nothing in `data/moves/` changes before that.

## 1. The two moves

### Shared: the stance (Ryo's guard idle)
Both clips start and end in the same guard so they blend into the idle with no pop. The Meshy idle (250 `Idle_10`) sits 75 cm hips-high against 85-87 cm for the strikes, which is why every blend there needed 8-10 ticks; this time the performer holds one fixed stance so that mismatch cannot appear.

Guard, facing screen-right (Ryo stands left of his opponent by default; the clip is mirrored by the game for P2 if needed, not re-captured):
- Feet a little wider than the shoulders, **left foot forward, right foot back**, rear heel up a finger's width, both feet about 15 degrees open. Knees bent, hips low (about 90 percent of standing height), weight 55 percent on the back foot, torso upright to a slight lean.
- **Open hands**, not fists: left (lead) palm at chest height facing forward at arm's length about 30 cm in front of the chest; right (rear) palm beside the jaw, elbow tucked. Fingers relaxed and together. This is an exorcist's guard with open palms.
- Chin down, eyes forward. No bounce while holding.
- The performer keeps one strip of floor tape for the front foot and one for the rear foot (see the shoot plan) so every rep starts from the same stance.

Open question for the designer (Q3): the old guard idle is a two-fist boxing stance (250). Do you want the new guard open-handed as above? If an idle clip is also wanted on the Tripo rig, shoot it in the same session as an optional third clip (about 20 s of guard with a gentle breath, one more upload, costs one more take of its own move).

### E15 and the heavy punch
E15 says strikes are open-handed because the Meshy rig had no finger bones and the designer accepted no code-posed fists. The Tripo rig **does have finger bones**, so the technical limit is gone, but the rule text has not changed, and video mocap does not give reliable finger capture anyway (the tools track wrists and sometimes a flat hand; fist-clench is rarely captured and is noisy). So:
- The punch is a **palm-heel strike (shotei)**: the rear hand drives forward with an open hand, wrist cocked back, heel of the palm as the contact point. Fingers stay at the rig's rest pose (flat, relaxed); nothing is code-posed. The take satisfies E15 as written, and an open palm on a "binding exorcist" reads as thematic, not as a missing fist.
- It also avoids the worst video-mocap failure on punches: the tool guessing a fist from a blurred hand.
- Q2 for the designer: keep E15 as is (palm strikes), or reopen it now that the rig has fingers (a data-driven fist pose is possible but is not covered by F5's "no hand posing" cleanly and needs your explicit decision and a rules.md edit). This spec assumes **palm strikes, E15 unchanged**.

### Heavy punch: rear palm-heel drive (HP)
Role: Ryo's slowest, biggest punch. Highest damage of the punches (80), so it must look committed and telegraphed. Frame data now 10/4/20 (34 frames). The strike travels along the fighter's facing axis, so it is read side-on at normal camera distance (about 25 degrees FOV, F4: the fighter fills about a third of the frame height).

Beat plan (performer, at full speed; game frames in brackets are the retimed targets, raw seconds are the performance):

| Beat | What the body does | Raw time | Game frames |
|---|---|---|---|
| Guard | Hold the stance, still. | hold 1 s before | frame 1 |
| Anticipation | Weight sinks onto the back leg, hips drop 3-5 cm, rear hand pulls back to the hip (palm up or in), lead hand stays up. Shoulders turn slightly away from the opponent (about 20 degrees). One clear beat so the audience sees it coming. | 0.12-0.15 s | 1-4 |
| Wind-up / load | Back foot drives into the floor, hips begin to turn forward, front knee absorbs. Elbow behind the ribs. This is the "heavy" pose and the pose a player reads for a block. | 0.08-0.10 s | 5-7 |
| Strike | Hips snap square, shoulders follow, rear arm shoots forward, hand rotating palm-forward on the way. **Contact pose at full extension at about shoulder height**, rear shoulder past the lead shoulder, rear heel off the floor, front knee bent, body weight forward. A small step with the front foot (about 10 cm) is allowed, but **no travel more than one foot length**; the game moves Ryo by data. | 0.10-0.12 s | 8-10 |
| Active | **Hold the extended pose** (arm straight, palm out, head forward and eyes on the target, the lead hand guarding the chin). The body is dead still for a beat: this is the frame the player must see land. | 0.08-0.10 s (2-3 raw frames at 60 fps is natural; the retime holds up to 4) | 11-14 |
| Follow-through | The hand pulls back a little and the weight rolls forward over the front leg; torso settles. | 0.15 s | 15-20 |
| Recovery | Rear hand returns to the jaw, rear foot re-plants, hips rise back to the guard height, feet re-settle on the tape. **Ends exactly in the starting guard**, still, for a one-second hold. | 0.40-0.50 s | 21-34 |

Timing check against the 2x limit (F2): game startup is 10 frames (0.167 s) from guard to the first active frame. The beats above sum to 0.30-0.37 s raw, i.e. 1.8-2.2x. That is at the edge. Direct the performer to **keep guard to contact at or under 0.33 s** (about 20 raw frames at 60 fps): say "quick load, hard snap". If the measured take is slower, either (a) the wind-up segment plays at the 2x ceiling and the strike at 1x, or (b) movesmith takes startup **12** (see below). Never go past 2x or relax the 4-frame hold cap.

Proposed frame data change (for movesmith, after measurement; clip first): **keep 10/4/20 as the target the take is shot for.** If the measured raw wind-up cannot fit in 10 frames at 2x, propose **12/4/22** (36 frames) because "slow, heavy punch" in the move-list text fits a longer telegraph, and the three punches then read 4 / 6 / 12 (light, medium, heavy) with a clearer step up than 10 does. That would touch the balance work due at the gate on 2026-10-21 (damage 80 and the frame advantage), so I do not propose it unless the take forces it. Recovery 20 at the end is the real limit: raw recovery of 0.40-0.50 s (24-30 frames) retimes to 20 at 1.2-1.5x, comfortably inside the band.

Reading it:
- **Side-on, fighting-game distance:** the silhouette has three clear shapes: compact guard, coiled back-lean (rear hand at hip, chest turned slightly away), and a long horizontal line from rear foot to palm at contact. The contact line must be the longest, flattest shape in the clip; it carries the hit. No wide arm swing in the plane facing the camera.
- **Three-quarter (the capture angle):** the hip rotation shows. The torso turns about 40 degrees from the starting angle to the contact pose.
- Avoid: a hooking or looping arm (reads as a swat and moves the hitbox's origin), a hop, a lean past about 25 degrees (the hurtbox grows), the lead hand dropping (hurtbox at the chin).

### Heavy kick: rear roundhouse to the body (HK)
Role: "Wide sweeping kick. Slow, but it hits hard." Highest damage normal (90), knocks down (C8). It is the move that punishes a standing opponent at mid range. Frame data now 11/4/23 (38 frames), C8 target 12/4/22.

**Height: a mid kick (ribs to chest), foot contact at about 110-125 cm for a 1.75 m performer (about 65-70 percent of height).** This is what the designer asked for in the first place and the old clip (head-high) could not give. A mid is blocked standing or crouching (E6) and cannot be ducked; knockdown stays in the data. If the designer would rather keep the head-high kick of the old clip (it can be ducked, E6), say so (Q4).

Beat plan:

| Beat | What the body does | Raw time | Game frames |
|---|---|---|---|
| Guard | Same stance. | hold 1 s before | frame 1 |
| Anticipation | Weight shifts to the front foot, hips drop slightly, torso leans away from the kick (about 10 degrees), rear heel lifts. The hands stay open and up. | 0.12 s | 1-3 |
| Chamber | Rear knee drives up and across the body to the hip line, shin folded, **front (planted) foot pivots on the ball through about 90-120 degrees**, hips opening as the knee rises. This is the "wind-up" read: the knee is high and pointing across. | 0.12-0.15 s | 4-8 |
| Strike | Hips whip around, the shin extends along the arc, the foot sweeps through at mid height. Rear arm swings back for balance, lead hand stays high. | 0.08-0.10 s | 9-11 |
| Active | **Hold at the full extension** (leg straight, toes pointed, body leaned back about 20-25 degrees, planted foot fully pivoted, arms spread). Perform a hold of 0.07-0.10 s. | 0.07-0.10 s | 12-15 |
| Follow-through | The leg stays up and slowly pulls into the chamber; the hips turn back. | 0.20 s | 16-22 |
| Recovery | The rear foot lands **in the guard position** (not past it), the front foot pivots back to the tape, hips come back to guard height. **Ends in the starting guard**, still for a one-second hold. | 0.45-0.55 s | 23-38 |

Timing check: game startup is 11 frames (0.18 s); the performance takes 0.32-0.37 s raw to reach contact, 1.75-2.0x. Same warning as the punch. Tell the performer to snap the chamber ("kick fast, land slow"). If the raw wind-up is over 0.37 s, take startup **13** as the proposal for movesmith (C8 target is 12; 13 stays within the "slow, but hits hard" read) rather than breaking 2x. Recovery: 0.45-0.55 s raw (27-33 frames) to 23 frames is 1.2-1.45x.

Reading it:
- **Side-on:** a roundhouse is a hip-turn kick, so seen from the side the leg rises across the body and the foot ends in front at mid height. The strongest read is the chambered knee (a triangle) followed by the long straight line at contact. The "wide sweeping" quality comes from the torso lean and the spread arms in the active pose, since the arc itself is partly in depth.
- **Three-quarter (capture):** the arc is visible and the hip spin is clear.
- Avoid: a hop or step (the game moves Ryo, not the clip), a back-leg spin that carries the whole body round (turning more than 120 degrees makes the planted foot slide and the tool lose the feet), a high kick, a flat-footed pivot (foot slide).

### Both: the planted foot (foot slide)
The most likely reject is foot slide (measured against slide 2.0 cm/frame reject, 1.0 warn, `measure_clip.py`). Pivots must happen on the ball of the planted foot with no walking; the performer marks the planted foot's position and stays within about 5 cm of it. Hips drift with the strike lean; that is fine and is move data (strip root).

## 2. Mocap tool options

**Caveat on every number below:** I cannot browse from this environment. Prices, limits and file formats are from my training knowledge (to mid 2026) and tool pages change often. The designer must check each current pricing and licence page before buying; items I am unsure about are marked "check". Every candidate's output must be cleared for commercial use on the plan bought (AM5); a capstone that is released (even free, even on a portfolio site or itch.io) counts as commercial in most licences, so "personal / non-commercial" free tiers do not cover it.

| Tool | Cameras | Output | Frame rate | Cost / licence (check) | Notes for fast strikes |
|---|---|---|---|---|---|
| **DeepMotion Animate 3D** (web) | single video | FBX, BVH, GLB (check); can output onto its own skeleton or onto an uploaded custom character | up to 60 fps output on paid plans, fps input accepted from 30-60 (check) | Free tier of monthly credits (about 30 seconds a month), watermark/limits and, as I recall, no commercial use on free; paid Innovator about USD 9-15 a month (billed yearly vs monthly, check), Studio higher. Credits are per second of video | Has an explicit foot-locking option and "physics filter" / smoothing sliders, hand tracking option, multiple-person off. Quality on fast strikes is middling: it smooths aggressively, which can round off the contact pose. |
| **Rokoko Video** (formerly Vision) | single video | FBX, BVH | 30 or 60 fps (check) | Has a free tier with caps (check commercial); paid Plus/Pro by month; Rokoko plan terms cover commercial use on paid (check) | Output is on Rokoko's own skeleton, needs a Blender retarget (Rokoko's free Blender add-on does name mapping). Foot lock and cleanup sliders exist. Roughly similar to DeepMotion in the strike pose. |
| **Move.ai Move One** | single iPhone video (iOS app) | FBX | 30 fps in my last knowledge, and 60 possible (check) | Per-credit pricing; roughly USD 15 a month for a starter pack, credits per clip (check); commercial on paid | Best known for single-camera quality and foot handling. Needs an iPhone (the designer's phone?). Moves are returned on a standard rig; retarget via names. Multi-camera "Move Pro" is studio-priced and out of scope. |
| **Plask** | single video | FBX, BVH, GLB | 30 fps input (check) | Free tier with a monthly cap and watermark, Pro about USD 25-30 a month (check) | Good for quick tests, jitter is typically higher, fewer cleanup options. |
| **FreeMoCap** (open source) | 1-N cameras (best with 2-4) | CSV, Blender file via the FreeMoCap Blender add-on, BVH via script | camera fps, 60 fps possible with webcams | Free, AGPL code; captured output is yours | Needs calibrated multi-camera set-up, effort and time. Fast strikes at 60 fps are acceptable with good lighting. Honest about quality: raw skeleton, the feet float without extra cleanup. Setup time risk is large for a one-day shoot. |
| **Blender MediaPipe add-ons** (e.g. BlendArMocap) | single webcam | Blender keys | camera fps | Free | Rough; very jittery on fast moves; not suitable for hit poses. Free fallback only. |
| Research tools (WHAM, 4DHumans, etc.) | single video | SMPL body params | video fps | **SMPL licence is non-commercial**; do not use | Excluded on licence grounds. |

None of the tools gives clean hit poses without scripted cleanup and review; the plan expects to reject some takes (F5 cap 3 per move).

### Recommendation
**DeepMotion Animate 3D on a one-month paid plan (about USD 15 if billed monthly; verify)**, with Rokoko Video as the runner-up if the free-tier test shows DeepMotion over-smooths the contact pose.
- Why: a monthly cancel-anytime price; takes 60 fps video and exports 60 fps (check); an explicit foot-lock switch; BVH/FBX; and it can bake the capture onto an uploaded Mixamo-rigged character, which matches the Tripo rig's `mixamorig:` bones and may remove the retarget step. A subscription covers the credits for several takes (video seconds): two moves of about 25 seconds and the cap of 3 takes each is 150 s total, a few days of usage within one month.
- Why not Move One first: single iPhone, 30 fps listed, per-credit; better foot handling reported, but we would pay before knowing if it works with the rig. It is the retake option if DeepMotion fails. Mention it as the take-3 alternative so a switch does not need a new cost approval beyond that tool's own price.
- **Before paying, run a zero-cost trial** (Q1): record the 20-second test clip described below, upload it to the free tiers of DeepMotion and Rokoko Video (no commercial use needed to compare quality; nothing from the free trial ships), and compare the contact pose. This does not use a take on the moves: it is a test of the tool, with a throw-away clip. If you prefer not to do a trial, approve the recommended plan directly.

Trial clip: T-pose 2 s, guard 2 s, 3 palm-heel strikes at full speed with a 2 s guard between them, 1 roundhouse. No slate needed.

## 3. Shoot plan (the designer can follow it alone)
A phone, a tripod, tape, one afternoon (30-45 minutes of recording).

**Space.** A clear floor of about 3 m x 3 m for the performer and a clear line of 4.5 m from the performer to the camera. Ceiling at least 2.5 m (so the kick does not clip the light). A non-slip floor (wood or rubber; no rug). Warm up for 5 minutes. Do not lock the knee or elbow at full extension; extend with control.

**Camera (primary: A) at three-quarter.**
- Phone in **landscape**, on a tripod, **1.0-1.1 m high** (about hip/belt height) and **level** (no tilt).
- **45 degrees off the facing axis**, on the performer's **front-right** (the performer faces screen-right in the game; the camera sees the striking right arm and leg without the torso blocking them). Distance **4-4.5 m** from the performer's centre.
- Lens: the **main 1x** lens (not ultrawide: it distorts; not zoomed: digital zoom adds blur). Frame so the performer is about **60-65 percent of the frame height** with the whole body and the full extension of the arm and leg inside the frame at all times, with feet in shot. Check by doing one test kick.
- Settings: 1080p at **60 fps** (4K at 60 only if the phone does it without a "lite" mode; the tools resize anyway), **locked exposure and focus** (tap and hold), **video stabilisation off**, HDR video off, slow-motion mode off (240 fps slow-mo is too low in quality for the tools), flash off. Shutter should be fast: use bright, even light and, if the camera app has a manual mode, set about **1/500 s** (or as fast as the light allows) to avoid a smeared limb.
- 60 fps is the minimum asked. If the phone can do 120 fps at 1080p, ask the tool whether it accepts 120 and downsamples cleanly (check; most do not and cap at 60).

**Camera (backup: B) side-on.** If a second phone is available, put it **90 degrees to the facing axis on the performer's right**, same height, same distance, same settings, recorded at the same time. B is a spare for a retake (it counts as a take if used) and a cross-check of the forward extension. If only one phone exists, record all of A first, then decide with the designer whether to shoot B in a second pass.

**Clothing.** Tight-fitting top and trousers (a long-sleeve sports shirt and leggings or slim joggers), **one solid colour that contrasts with the background and is not skin tone** (dark on a light wall, or the reverse). **No hoodie, coat, robe, skirt, scarf, hair that covers the face, or loose sleeves**: the tool tracks the body, and Ryo's own coat and sleeves are rig concerns for rig-specialist. Bare feet or thin plain socks, or flat dark shoes; the feet and toes must be visible against the floor (a floor that contrasts with the feet). No jewellery on the wrists. Hair tied back.

**Lighting and background.** Bright, even, soft light (daylight room with a lamp opposite the window; no strong direct sun, no spotlights); avoid backlight (no window behind the performer). **Plain matte wall or sheet** behind, a different colour from the clothing, **no mirror, no other people, no posters of people, no clutter**. Nothing in the room that moves.

**Marks.** Two strips of tape on the floor for the feet in guard, the same marks for every rep, and one X for the centre. Shoot a still photo of the guard stance from camera A at the start and keep it with the files.

**Slate and naming.** At the start of each video, hold a sheet of paper to the camera for two seconds showing `YOK146 HP T1` (move, take number), then clap once (both phones hear it, so A and B can be synced). File names:
```
yok146_ryo-heavy-punch_t1_A-3q_60fps.mp4
yok146_ryo-heavy-punch_t1_B-side_60fps.mp4
yok146_ryo-heavy-kick_t1_A-3q_60fps.mp4
yok146_ryo-heavy-kick_t1_B-side_60fps.mp4
```
Keep the originals on the designer's disk (videos are large and not committed); note each file's name, fps and size in the spec's `## Takes`.

**Take structure (one video per move per take).** A "take" in this spec is **one processed upload of one performance video**; the video holds several reps and the best rep is chosen by `measure_clip.py` and the stills (trimming a rep is scripted cleanup, not hand keying). Q5 for the designer: confirm this reading of the F5 cap, otherwise a take is one rep and the number of reps drops to 1-2 per video.

Per video:
1. Slate, clap.
2. **T-pose for 2 seconds** (arms straight out, palms down, feet together, facing the tape X on the 3/4 camera axis, as the tool prefers; DeepMotion and Rokoko do not require it but the T-pose makes calibration reliable).
3. Step to the guard on the tape. Hold still for 2 s.
4. **Rep 1**: strike at full speed, hold the contact pose for a beat (the active pose), recover to the guard, **hold still for 2 s** (this gives a clean start and end, and the cleanup trims here).
5. Repeat for **6 reps**, with the same 2 s guard hold between reps. Rest 5 s every 3rd rep. Keep the same intensity: do not slow down, do not stop in the middle.
6. End in guard, then T-pose for 2 s.
Length about 35-40 seconds per video.

**Speed.** Full speed, committed power, every rep, because the retiming curve (F2) handles the game timing and a slowed-down performance cannot be speeded up without losing weight. Do not "act" slowly to be safe. A strike aimed at a spot in the air at the target height (the punch at shoulder height, the kick at rib/chest height), stop before the joint locks. Do **not** hit or kick a bag: contact changes the pose.

**Count and cost of shoots.** 2 moves x up to 3 takes. First session: 1 take per move (about 40 s each, 80 s of upload). Retakes (new approval each, F5) re-shoot only the failed move after the verdict says what to change.

## 4. Pipeline after capture

Naming below uses the Mixamo bone names from the Tripo rig (`mixamorig:Hips`, `mixamorig:Spine`/`Spine1`/`Spine2`, `Neck`, `Head`, `LeftShoulder`, `LeftArm`, `LeftForeArm`, `LeftHand`, `LeftUpLeg`, `LeftLeg`, `LeftFoot`, `LeftToeBase`, and the right side). **Confirm each against the rewritten `assets/specs/rigs/skeleton.md` before the first run.**

1. **Download and file.** Tool exports FBX (preferred) or BVH, with 60 fps if offered. The designer drops the raw export in `game/assets/generated/clips/ryo/raw/yok146-ryo-heavy-punch-t1.fbx` (and `-kick-t1`). Log the file, tool, plan, settings (foot lock on/off, smoothing level) and cost in `## Takes` below.
2. **Import and check names.** Open in Blender 4.1 headless, list the armature's bones and compare with the Tripo rig. If the tool built the animation on an uploaded Tripo/Mixamo character, the names match and no retarget is needed (check: bone names identical, rest pose identical, scale 1.0). If the tool's own skeleton came back, run a name-based retarget (script gap 1) from the tool skeleton to the Tripo rig. Retarget **by name** because both are Mixamo-compatible; for each mapped bone apply the source's rest-relative world-rotation change to the target rest pose (the method `retarget_bvh.py` already uses), not a copy of local values: the rigs may differ between T- and A-pose.
3. **Things to check on the retarget** (rig-specialist is the owner of the G4 retarget gate; this is the animator's own pre-check):
   - the clip faces the rig's +Z (or the shared skeleton's facing) and plays "toward screen-right" when the game faces right; the hips height scaled by the rigs' height ratio, not the performer's absolute;
   - no unkeyed twist bones needed (none in this rig);
   - the three spine bones all receive rotations (Mixamo-preset rigs have `Spine`, `Spine1`, `Spine2` counted upwards; the Meshy order was downwards: do not reuse the old map);
   - fingers: leave them at rest (palm strike); check the hand pose is an open flat hand, not whatever the tool wrote (DeepMotion may key finger curls; if it does, null those channels by script and note it);
   - shoulder or elbow candy-wrapping and arm-through-torso on the contact pose;
   - the hand reaches the same side as the performance (not mirrored).
4. **Clean** with `tools/blender/clean_clip.py` (settings recorded in the clip spec `## Cleanup`):
   ```
   Blender -b --python tools/blender/clean_clip.py -- --clip <retargeted.glb> --out game/assets/generated/clips/ryo/ryo-heavy-punch.glb \
       --trim A B --strip-root horizontal --root-bone mixamorig:Hips \
       --smooth-bones "*Hand" "*ForeArm" "*Foot" "*Spine*" --cutoff 10 --order 4 --hit H0 H1 --hit-margin 1 --taper 2
   ```
   - Trim one clean rep from its guard-hold start (the last frame of the stillness) to the frame it is back in guard and still.
   - Strip root motion in the ground plane (the move is in place).
   - Resample to 60 fps (the script does this, whatever the tool gave).
   - Butterworth smoothing only on jittery channels (hands, forearms, feet, spine), **never** on the hit frames (`--hit` with margin and taper protect the strike pose; 10 Hz cutoff first, raise if the strike pose rounds off, lower if the jitter stays).
   - Cascadeur's physics pass only if the weight reads wrong, and only with a new designer approval.
5. **Measure** with `tools/blender/measure_clip.py --clip <clean.glb> --id ryo-heavy-punch [--hit A B]` (frame count; foot slide per planted foot; joint jitter per frame; the suggested hit window from the reach of the striking end effector; stills in `assets/specs/clips/stills/`). Use `--as-stripped` on the raw take if root motion remains. Thresholds: slide reject 2.0 and warn 1.0 cm/frame, jitter reject 40 and warn 20 deg/frame^2.
6. **Retiming curve.** From the measured raw timeline, write segments mapping game frames onto clip time:
   - punch: guard/anticipation 1-4 and wind-up 5-7 play at the fastest they need (up to 2x), strike 8-10 plays at 1-1.3x, active 11-14 holds the extended pose (a hold of 4 frames is the cap, equal to the active length), follow-through 15-20 at 1x, recovery 21-34 eases out at 1.2-1.5x;
   - kick: anticipation + chamber 1-8 at up to 2x, strike 9-11 at 1-1.3x, active 12-15 holds the extension (4 frames), follow-through 16-22 at 1x, recovery 23-38 at 1.2-1.5x;
   - Each segment between 0.5x and 2x, no held pose over 4 frames. Report frames **as retimed**. Until `ClipCatalog` reads curves (YOK-113), only `trim_start`, `trim_end` and `speed_scale` reach the game: write the curve in this file and flag it to the Producer; if one speed has to stand in, use the active-segment-weighted speed and say which frames drift.
7. **Take verdict checklist** (ACCEPT / WARN / REJECT; the stills decide the smear, F5):
   - Hit frames: strike pose clear and unsmeared in the stills; contact at the expected height (punch: shoulder height; kick: 110-125 cm foot).
   - Startup within 2x: guard to contact fits the target at 2x or less.
   - Foot slide per planted foot under 1.0 cm/frame (reject over 2.0); the planted foot of the kick pivots without a glide.
   - Joint jitter under 20 deg/frame^2 (reject over 40), no zig-zag at the contact pose.
   - Root motion strips cleanly; the net hip travel stays under about 25 cm.
   - Start and end pose match the guard idle (hips height, foot positions, hand heights within a few cm) so the blend is under 4 ticks. Compare against the performer's guard still.
   - Arms and leg do not intersect the torso, head or the opposing leg at any frame; no hand clipping the coat or sleeves (rig-specialist is told about the Tripo cloth and coat).
   - Open hand preserved (E15).
   - Reject reasons say exactly what to change in the performance or the tool setting for the next take (for example "wind-up too slow: perform it faster", "foot slides: tape the pivot and re-pivot on the ball", "contact smeared: shutter faster or lighter smoothing in the tool").
8. **Handoff.** Write `data/clips/ryo-heavy-punch.json` and `data/clips/ryo-heavy-kick.json` to `data/schema/clip.schema.json`: `source` (`mocap`), `clip` (the file and the rep), `frames_total`, `hit_start`, `hit_end` (as retimed), `retarget_notes`, trim, speed and file. Movesmith then re-derives startup/active/recovery, damage-neutral hit boxes and hurtboxes from the stills, and the notes in `data/moves/ryo-heavy-punch.json` and `ryo-heavy-kick.json` (clip first, then frame data). `rules-lawyer` checks the content (F2: frame data matches the clip as retimed; C8 targets; E6 hit height; E15 open hand; T0 unaffected), then harness checks. The rig-specialist's G4 gate is still run on the Tripo rig by rig-specialist.
9. **Sound.** After the clip is retimed, I cue the sounds (`data/sound/<move-id>.json`) from the bought packs: a whoosh on startup, an impact thud with a short yokai sting on the hit frame. This waits on the sound inventory.

### Script gaps (all need a ticket; the first two block this work)
1. **No retarget script for Mixamo names.** `tools/blender/retarget_bvh.py` is written for Meshy's BVH and the old shared-skeleton names: its `MAP` has `Spine02/Spine01/Spine`, `neck`, `LeftUpLeg`... as targets and Meshy's `Spine1/Spine2/Chest/Neck1/LeftLeg=thigh` as sources. It does not know `mixamorig:` names, the upward spine order, or fingers. Needed: a `retarget_by_name.py` (name map as a table file; source and target names given as arguments, `mixamorig:` prefix handled), keeping the rest-relative rotation method. If DeepMotion bakes onto the uploaded Tripo rig directly, this is only a name check.
2. **Bone names are hard-coded.** `tools/blender/measure_clip.py` stops with "not on the shared skeleton" unless `Hips`, `LeftFoot` and `RightFoot` exist, and uses `LeftHand`/`RightHand`/`LeftFoot`/`RightFoot` for reach; `clip_common.py` and the stills figure code use side names likewise. With `mixamorig:` names none of it runs. Needed: a `--bone-prefix` argument or a name normaliser in `clip_common.py` (strip `mixamorig:`, `mixamorig_`, `mixamorig1:`), used by `measure_clip.py`, `clean_clip.py` (`--root-bone` already accepts any name; default `Hips`) and the rig checks. This also depends on what rig-specialist names the shared skeleton in the rewritten `skeleton.md`; align with it.
3. **Godot may rename the bones.** Godot's glTF import turns ":" into "_" in names (to check by the gameplay programmer / rig-specialist when the first Tripo GLB loads), which would affect `ClipCatalog` track paths.
4. **Old clips are on the Meshy skeleton.** Every `data/clips/ryo-*.json` (and Kitsune's, Oni's) currently points at a Meshy-skeleton GLB. Under Option A they do not play on the Tripo rig until retargeted; skeleton.md's rule that a new rig "must play the existing walk, heavy and throw clips" is superseded for heavy because this spec replaces both heavies, but the walk, the throw and the other 20+ clips remain a rig-specialist/Producer decision (retarget by script, or re-capture). Flag for the Producer.
5. No script exists for an automatic finger-channel null, if the tool curls the fingers. Small; do it inside `clean_clip.py` as a `--zero-bones` option only if needed.
6. No script cuts a single rep out of a multi-rep video export other than `--trim`; the rep boundaries are found by hand from the stillness in the data (read from frame values, not keyed). Optional: a small `find_reps.py` that lists the still-guard frames.

## 5. Cost and approval block

| Item | Cost | Needs the designer |
|---|---|---|
| Zero-cost tool trial (free tiers of DeepMotion and Rokoko Video, one 20 s test clip) | 0 | yes: confirm (Q1) |
| Recommended: **DeepMotion Animate 3D, 1 month, paid plan** | about USD 15 if monthly (verify at purchase; cheaper when billed yearly, do not take a year) | yes: the purchase, YOK-115 |
| Alternative / take-3 option: Rokoko Video or Move.ai Move One monthly | about USD 15-30 (verify) | only if the first tool fails; its own approval |
| Phone, tripod, tape, a plain wall | owned | none |
| Cleanup, measure, retiming | 0 (scripts) | none |
| Takes | 2 moves x 3-take cap (F5); first session 1 take each | each retake needs a fresh approval |

**Approve (no action runs before this):**
1. The tool and its plan (default: DeepMotion, 1 month, up to the price shown at checkout), plus the free-tier trial first.
2. The two beat plans (palm-heel punch; mid roundhouse).
3. The shoot plan and that **you** (or a performer you pick) are recorded; if someone else, they need the same guard stance. Say the person is happy for the capture to be used.
4. The target frame data (10/4/20 and 12/4/22, or the fallbacks 12/4/22 and 13/4/22 if the take forces them) as a proposal only; movesmith finalises after the take.

**Questions for the designer**
- Q1: Free-tier trial first, or buy the recommended plan directly? Which phone is the primary camera (iPhone makes Move One available)?
- Q2: E15: keep palm strikes (this spec) or reopen it now that the rig has fingers?
- Q3: Open-handed guard stance as described? Do you want the guard idle shot as a third clip in the same session?
- Q4: Heavy kick height: mid (this spec) or keep the head-high kick?
- Q5: Is a "take" one processed upload (several reps inside) for the F5 cap of 3?
- Q6: Heavy punch is shot for 10/4/20; may movesmith move it to 12/4/22 if the take needs it, even close to the 2026-10-21 balance gate?
- Q7 (for the Producer): who retargets the other ~20 Meshy-skeleton clips onto the Tripo rig, and when?

## Takes
(none; nothing has run)

## Cleanup
(none; recorded after the first approved take, with every script setting)
