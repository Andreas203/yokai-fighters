# YOK-31 Retarget gate: walk, heavy, throw on Ryo and Kitsune

Ticket: YOK-31 (G4, F2, F5, F6, AM1). Status: **TAKE 1 RUN AND MEASURED (12 credits). See `docs/assets/retarget_gate.md`.**
Stacked on YOK-30 (rigs). Demo ships silent: no sound cues. Balance at spec time: 1137 credits.

## Question
Do Meshy clips play on the auto-rigs of Ryo and Kitsune for fighting moves without clipping the sleeves, hakama, coat tail or the rigid tail attachments? One clip per move is shared by both rigs (AM1), so each move is applied to two rigs.

## Rigs (from YOK-30)
| Fighter | Rig task id | Skeleton | Notes |
|---|---|---|---|
| Ryo | `01a10e19-f774-707d-9b31-3e94ac0acdcd` | 24-joint Meshy humanoid, T-pose, feet origin, Y up, 1.75 m | Short asymmetric coat panel, shimenawa sash, forearm wraps |
| Kitsune | `01a10e1a-063d-7300-8ecd-5b42ad89b086` | same skeleton, 1.65 m | Wide haori sleeves, knee-length hakama, hair down the back, nine unrigged tails on hip `BoneAttachment3D` |

## Takes
| # | Move | Clip need | Fighter | Source | Credits |
|---|---|---|---|---|---|
| W1 | Walk (R02/R03) | guarded forward step, loop | Ryo | **Reuse free** `ryo/ryo-walk.glb` (`walking_man`, 32 keys at 30 fps = 1.067 s = ~64 ticks) | 0 |
| W2 | Walk | same | Kitsune | **Reuse free** `kitsune/kitsune-walk.glb` (same clip) | 0 |
| H1 | Heavy punch (N03) | big wind-up hook/uppercut, ~34 ticks, contact near tick 10 | Ryo | `meshy_animate` | 3 |
| H2 | Heavy punch (N03) | same clip | Kitsune | `meshy_animate` | 3 |
| T1 | Generic grab (T01) | fast reach and grab, then toss/pull, ~30 ticks, grab contact by tick 5-6, single fighter, defender plays R11 | Ryo | `meshy_animate` | 3 |
| T2 | Generic grab (T01) | same clip | Kitsune | `meshy_animate` | 3 |

Take 1 total: **12 credits** (4 `meshy_animate` calls at 3). Walk costs 0 because it already exists on both rigs. The free walk is the neutral `walking_man`, not a guarded walk; if the designer wants a guarded walk, that is a separate take (3 per rig).

### Clip source
Library preset first (cheaper, consistent). The MCP has no tool that lists the animation library, so the numeric `action_id` is **not yet known**. After approval I pick a punch/hook/uppercut preset for H and a grab/push/pull/throw-style preset for T, and log the id under `## Takes` before the call. `meshy_animate` takes only a library `action_id` (no free-text prompt). If the library has no usable match, I stop and report to the designer instead of spending. Any price difference from 3 credits per call is reported, not assumed.

Constraints: in place (strip root motion; Movesmith owns movement), humanoid skeleton only, no paired animation (C4), no hand keying (F5).

### Take cap and worst case
Cap 3 takes per move (F5), counted per move. Because a clip is shared, a retake reruns both rigs. Worst case: 2 moves x 3 takes x 2 rigs x 3 = **36 credits**. Each retake of one move costs 6. Every retake needs new designer approval. After 3 failed takes: propose a mechanically equivalent move on an approved clip (F5).

## What I measure per take (from the downloaded GLB; no hand fixing)
1. **Frame count**: keyframe count and duration, resampled to 60 ticks (`frames_total`). Compare to target (heavy ~34, throw ~30, walk loop).
2. **Root motion**: hips/root translation over the clip on all three axes; report max drift in metres and whether it strips cleanly (offset removed, no vertical pop at loop). Fail if stripping breaks the pose.
3. **Foot sliding** (walk, and stance frames of heavy/throw): world-space foot speed while planted; flag any plant moving more than 3 cm over its planted frames.
4. **Jitter / smear**: frame-to-frame bone rotation spikes (over 30 degrees in one tick on a non-striking limb) and pose clarity on the contact frames.
5. **Contact frame**: `hit_start` / `hit_end` for H (fist at full extension, ~tick 10-14 expected) and T (hands closed on the grab point, tick 6 or earlier).
6. **Clipping, per rig, sampled every 2 ticks**: forearm/hand vs torso and head; Kitsune haori sleeves vs torso and arms; hakama vs legs on knee lift and wide stance; Ryo coat panel vs legs; hair vs tail attachments. Method: sampled mesh-vertex penetration against the body's own mesh plus rendered stills (front and side) of wind-up, contact and recovery. Pass/fail is on visible interpenetration in those stills.
7. **Tail attachments (Kitsune)**: the nine tail instances sit on a hip bone attachment with fixed fan angles and tick-driven sway. Per tick, check tails against legs, hakama and hair using the clip's hip rotation: do they stay clear through the heavy twist, the throw lean and the walk hip sway, and does the attachment stay seated on the hip with no gap or flip. Sway amplitude may be tuned within its normal range only; no hand keying.

## GO / NO-GO (G4)
Judged per character. A move passes for a character when all hold: root motion strips cleanly; no foot slide on planted frames; no jitter or smear on contact frames; contact lands in window (heavy tick 8-14, throw tick 6 or earlier); no visible clipping in the sampled stills.

| Character | GO | NO-GO |
|---|---|---|
| Ryo | Walk, heavy and throw all pass | Any of the three fails after 3 takes |
| Kitsune | Walk, heavy and throw all pass **and** the tail attachments hold in all three (no tail-leg or tail-hair penetration, attachment stays seated) | Any fails after 3 takes, or tails cannot be kept clear without hand keying. Fallback: folklore human guise (new spec, new approval, est. 44 credits per take for K1-K3, not in this total) |

Minor clipping fixable Godot-side without touching animation (a few millimetres of sleeve push-in, tail fan angle in data) is a WARN, not a fail. Results are per character: Ryo can be GO while Kitsune is NO-GO.

## Takes (log)
Take 1, 2026-10-06, designer-approved (heavy + grab on both rebuilt rigs, 12 credits, no retakes). Balance 959 -> 947. Rigs: Ryo `01a10e47-857e-73a0-9f7a-cd74795198fb`, Kitsune `01a10e47-91f6-709b-bd34-8884f8cbabbb` (the rig ids in the table above are superseded by the YOK-30 rebuild).

| Take | Fighter | Task id | Action id (preset) | Credits | Verdict |
|---|---|---|---|---|---|
| H1 | Ryo | `01a10e5b-45a5-7037-98b1-83de6aa9f9e9` | 194 `Right_Uppercut_from_Guard` | 3 | PASS (head-trim 28 ticks) |
| H2 | Kitsune | `01a10e5b-4763-7086-95f6-9b05c342b132` | 194 | 3 | WARN (sleeves) |
| T1 | Ryo | `01a10e5b-490c-719f-885b-079e3172a818` | 389 `Grip_and_Throw_Down` | 3 | FAIL (not a forward grab, 283 ticks) |
| T2 | Kitsune | `01a10e5b-4a7d-77c0-902a-36f5aa60a3b2` | 389 | 3 | FAIL |
| W1/W2 | both | free rig walks | `walking_man` | 0 | PASS / WARN |

Full measurements, stills and the GO / NO-GO recommendation: `docs/assets/retarget_gate.md`. Take count for T01 is now 1 of 3.

## Output when run
Per take: task id, credits spent, GLB path, a measurements table for items 1-7, PASS/FAIL per rig, and GO/NO-GO per character. `data/clips/<move-id>.json` is written only for passing takes.
