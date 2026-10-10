# Run log: what the crew produced (5–6 October 2026)

This is the crew's real run on the capstone, building the Ryo vs Kitsune vertical slice of **Yokai Fighters** (Linear ticket YOK-39), from the first ticket to a playable build. Every row links to a pull request in `Andreas203/yokai-fighters` and, where Meshy was called, to the task ids logged in the spec files. Nothing below was hand-made: each artifact is an agent's output, carried to the next agent by the orchestrator.

**Meshy credits:** started at 1,300. 500 spent, every job within a designer-approved cap, leaving 800.

## 1 · producer: plan and tickets
- **Input:** GDD, `rules.md`, amendment AM1 (visuals are generated with Meshy, sound is bought), the Linear board, the repo.
- **Output:** a dispatch plan in six waves, plus board fixes in Linear:
  - Rewrote stale tickets YOK-29 and YOK-30 from "buy model packs" to "generate with Meshy".
  - Created YOK-54 (sound shortlist).
  - Broke a circular block between YOK-41 and YOK-43.
  - Added the hidden prerequisites YOK-17, YOK-21 and YOK-33 as blockers.
  - Listed every designer approval needed, with credit estimates.
- **Consumed by:** the orchestrator, which dispatched asset-smith first because the plan put art on the critical path.

## 2 · asset-smith: generation specs → models
| Step | PR | Output | Credits |
|---|---|---|---|
| Spec | #10 | `assets/specs/{style,ryo,kitsune,bamboo-grove-dusk,ui-paper-demo}.md`, job list `assets/specs/yok-29-job-list.md` | 0 |
| First pass (approved, 163 cap) | #16 | Ryo and Kitsune turnaround → image-to-3D (T-pose) → auto-rig; tail mesh ×9; bamboo, lantern, backdrop, ground strip; three UI textures. Acceptance verdicts per job; flagged a texture defect on the Kitsune | 163 |
| Restyled references (approved) | #19 | Ten stylised reference pictures from the designer's sketches; designer picked Ryo E and Kitsune D (`docs/design/characters/*/references/`) | 90 |
| Rebuild from new refs (approved, 88 cap) | #16 | New rigged Ryo and Kitsune (`game/assets/generated/characters/`) | 88 |

- **Consumed by:** clip-matcher, which needs the rig task ids and the risk notes on sleeves, hakama, hair and tails.

## 3 · clip-matcher: retarget gate, then every move's clip
| Step | PR | Output | Credits |
|---|---|---|---|
| Gate spec | #17 | `assets/specs/clips/yok-31-retarget-gate.md`: takes, what gets measured, GO/NO-GO criteria | 0 |
| Gate takes | #22, #24 | Heavy and grab on both rigs, measured from the GLBs with numpy FK: frames at 60 ticks, root motion, foot slide, sleeve stretch, tail clearance, plus stills. Three library grabs failed; the shove was substituted under rule F5. Designer call: GO for both (`docs/assets/retarget_gate.md`) | 18 |
| Clip specs | #27, #28 | 34 takes picked from Meshy's library previews, shared across rigs (F6) | 0 |
| Clip takes (approved, 123 cap) | #33, #34 | 41 takes run and measured. 35 usable, written as `data/clips/*.json` (frames_total, hit_start, hit_end, trim, speed). 6 proposed for retake. GLBs stripped to animation-only, 2 MB in git instead of 175 MB | 123 |
| Retakes (approved, 18 cap) | #41 | 6 retakes: heavy kick and get-up usable; light kick failed again, so the F5 substitute was proposed | 18 |
| Free re-timing (trim + speed only) | #41 | Movesmith's first frame data showed the clips ran long. Using only trim and playback speed, clip-matcher moved 15 clips onto the GDD timings, e.g. Ryo's light punch from 4/2/19 to **4/2/7**, and Foxfire to one shared 15/4/30 cast for both rigs (`data/clips/`, stills in `docs/assets/clips/`) | 0 |

- **Consumed by:** movesmith, which reads `data/clips/*.json` and derives frame data from them (rule F2: clip first).

## 4 · movesmith: frame data and reward content from the clips
| Step | PR | Output |
|---|---|---|
| Reward content | #37 | `data/modifiers/will-o-wisp.json`, `fox-patience.json`; Lv 2 proposals for Spirit Wave and Rising Talisman, which the designer then decided (rule E21); card text |
| First frame data | #38, #39 | Both fighters' moves written from the clips. It **flagged** recoveries 2–4× the GDD values, which led to the re-timing pass above, and found an engine bug that loaded every fighter's moves into both kits. That became its own ticket |
| Re-derived frame data | #38, #39 | Every move re-derived from the re-timed clips. All of Ryo's and the Kitsune's normals now match the GDD's C8 table (`data/moves/`); the only remaining deviation is the clip-derived heavy kick, 11/4/23 against C8's 12/4/22 |
| Hitbox alignment | #48 | Once the rigged models were playing in the game, the hitbox overlay showed boxes floating above limbs. Movesmith probed every bone on every frame and re-placed all 34 moves' hitboxes and hurtboxes on the bodies, without touching timing (`docs/screenshots/hitbox-alignment/`, before and after) |

- **Consumed by:** rules-lawyer.

## 5 · rules-lawyer: the gate
Every content PR above passed through it before merging. Verdicts, as written:
```
#37  VERDICT: PASS  will-o-wisp (x130% = "30% faster"), fox-patience (3 x 1.25 = 3.75 → 4), Lv 2 proposals (A4, P1)
#39  VERDICT: PASS  11 Ryo files: every move matches its clip (F2), normals equal C8, throw C4/E12, Rising Talisman Lv 2 = E21
#38  VERDICT: PASS  7 Kitsune files incl. shared foxfire.json (15/4/30 = A-table; same timing on Ryo's rig, T0)
#48  VERDICT: PASS  18 files: no timing field changed; split hitboxes cover exactly the original windows (F2, F3)
```
Designer-attention notes it raised and the designer ruled on:
- Spirit Wave has two counting conventions for recovery.
- The heavy kick is head-high.
- Rising Talisman's anti-air box is small.

- **Consumed by:** the game. Each passing PR merged into `data/`, and the playable slice loads it.

## 6 · gameplay-programmer: the game that plays the content
- **Input:** the merged `data/` (moves, clips, modifiers, profiles, story), the schemas and `rules.md`.
- **Output:** a Godot 4.7 (.NET/C#) game. The fight logic is pure, integer-only C# with no Godot types, so it is deterministic and runs headless for tests and the harness. Every PR went through its own build-and-test loop and the designer's review.

| PR | Ticket | What it built | Tests after |
|---|---|---|---|
| #12 | YOK-15 | 60-tick fixed-step fight loop, 2.5D stage, camera, KO slow-down | 12 |
| #15 | YOK-16 | Move system from data: startup/active/recovery, 2D hit/hurt boxes, hitstun, blockstun, pushback | 27 |
| #14 | YOK-17 | Input buffer and motion parser (Kata) | 26 |
| #21 | YOK-22 | Hitbox overlay and frame-step (F1–F3), the tool for checking data against clips | 48 |
| #23 | YOK-18 | Ryo's base kit: walk, dash, jump, crouch, blocking, six normals from data | 59 |
| #25 | YOK-19 | Generic throws and throw break | 80 |
| #26 | YOK-20 | Meter, burst, counterhits, hitstop, screen shake | 80 |
| #29 | YOK-21 | Special slots, projectiles, EX specials, Lv 2/3 from data | 122 |
| #30 | YOK-23 | Kihon one-button specials (the never-cut scheme) | 137 |
| #31 | YOK-55 | Jump-in air normals | 150 |
| #36 | YOK-27 | Behaviour-profile AI: reacts only to on-screen state after its reaction frames, with habits as data | 178 |
| #35 | YOK-47 | Reward draft: NEW special, Lv 2 upgrade, modifier attach, health carry-over | 169 |
| #42 | YOK-56 | Per-fighter kit loading (the bug movesmith found) | 174 |
| #44 | YOK-49 | Record and replay: the same seed and inputs give an identical fight every tick | 185 |
| #45 | YOK-48 | Demo flow: fight → binding line → reward → rematch → demo complete | 202 |
| #47 | YOK-53 | Rigged models and clips in the fight, stepped with `AnimationPlayer.Seek` at the exact frame | 214 |
| #51 | YOK-39 | Fix for Ryo "moving on his own" (pushed fighters showed a walk cycle), with a reproducing test | 216 |
| #52 | YOK-53 | Wake-up phase (no get-up pop) and structured clip fields | 223 |
| #50 | YOK-39 | Bamboo grove stage at dusk | 219 |
| #53 | YOK-39 | Camera keeps both models in frame; hurtbox defaults from the models | 241 |
| #56 | YOK-39 | Stage, camera and fighters as editable Godot scenes | 245 |
| #57 | YOK-39 | Windows export: data located next to the exe in release builds, export preset, [release `demo-v0.1`](https://github.com/Andreas203/yokai-fighters/releases/tag/demo-v0.1) | 247 |

**How it closes the loop with the other agents:**
- Its **hitbox overlay** on the real models showed movesmith's boxes floating above the limbs, which led to the hitbox-alignment pass (#48).
- Its **frame-step** and **clip catalog** let clip-matcher's trims be verified frame by frame.
- Its **per-fighter kit bug**, found by movesmith, was fixed with tests that never depend on what's in `data/`.
- It **wired** the rules-lawyer-passed story cards and profiles into the game, and its replay test proves the content plays deterministically.

## Designer gates hit during the run
- Approved the job list.
- Picked the tail count, the pose and whether to include UI textures.
- Approved the restyle and chose the references.
- Approved the gate and grab retakes.
- Made the GO calls.
- Approved the clip list, its upgrades and the retakes.
- Decided the Lv 2 steps.
- Accepted the re-timing and the hurtbox defaults.
- Rewrote the binding line to "Forgive me, {yokai}. I'll set your spirit free." (GDD amendment AM2). The rules-lawyer passed the new story card before it merged (#62).
- Approved every merge.

No Meshy job ran without an approval, and every spend stayed at or under its cap.
