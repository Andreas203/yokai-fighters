# Run log: what the crew produced (5–6 October 2026)

This is the crew's real run on the capstone, building the Ryo vs Kitsune vertical slice of **Yokai Fighters** (Linear ticket YOK-39). Every row links to a pull request in `Andreas203/yokai-fighters` and, where Meshy was called, to the task ids logged in the spec files. Nothing below was hand-made: each artifact is an agent's output, carried to the next agent by the orchestrator.

**Meshy credits:** started at 1,300 credits. 500 spent, all within designer-approved caps, leaving 800 at the time of writing.

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
| Spec | #10 | `assets/specs/{style,ryo,kitsune,bamboo-grove-dusk,ui-paper-demo}.md`, job list `yok-29-job-list.md` (see `output/2-models/`) | 0 |
| First pass (approved, 163 cap) | #16 | Ryo and Kitsune turnaround → image-to-3D (T-pose) → auto-rig; tail mesh ×9; bamboo, lantern, backdrop, ground strip; three UI textures. Acceptance verdicts per job; flagged a texture defect on the Kitsune | 163 |
| Restyled references (approved) | #19 | Ten stylised reference pictures from the designer's sketches; designer picked Ryo E and Kitsune D (`output/1-references/`) | 90 |
| Rebuild from new refs (approved, 88 cap) | #16 | New rigged Ryo and Kitsune (`output/2-models/*-turnaround-front.png`) | 88 |

- **Consumed by:** clip-matcher, which needs the rig task ids and the risk notes on sleeves, hakama, hair and tails.

## 3 · clip-matcher: retarget gate, then every move's clip
| Step | PR | Output | Credits |
|---|---|---|---|
| Gate spec | #17 | `assets/specs/clips/yok-31-retarget-gate.md`: takes, what gets measured, GO/NO-GO criteria | 0 |
| Gate takes | #22, #24 | Heavy and grab on both rigs, measured from the GLBs with numpy FK: frames at 60 ticks, root motion, foot slide, sleeve stretch, tail clearance, plus stills. Three library grabs failed; the shove was substituted under rule F5. Designer call: GO for both (`output/3-retarget-gate/`) | 18 |
| Clip specs | #27, #28 | 34 takes picked from Meshy's library previews, shared across rigs (F6) | 0 |
| Clip takes (approved, 123 cap) | #33, #34 | 41 takes run and measured. 35 usable, written as `data/clips/*.json` (frames_total, hit_start, hit_end, trim, speed). 6 proposed for retake. GLBs stripped to animation-only, 2 MB in git instead of 175 MB (`output/4-clips/`) | 123 |
| Retakes + re-timing (approved, 18 cap) | #41 | 6 retakes: heavy kick and get-up usable, light kick failed again so the F5 substitute was proposed. Free re-timing pass using only trim and speed moved 15 clips onto the GDD timings, e.g. Ryo light punch recovery 19 → 7 and Foxfire 92 → 49 frames shared by both rigs | 18 |

- **Consumed by:** movesmith, which reads `data/clips/*.json` and derives frame data from them (rule F2: clip first).

## 4 · movesmith: frame data and reward content from the clips
| Step | PR | Output |
|---|---|---|
| Reward content | #37 | `data/modifiers/will-o-wisp.json`, `fox-patience.json` (`output/5-content/`); Lv 2 proposals for Spirit Wave and Rising Talisman; card text |
| Ryo frame data | #39 | Ryo's normals, throw, air normals, Spirit Wave and Rising Talisman in `data/moves/`. Every value traces to a clip file; 6 recovery deviations from the GDD flagged for the designer |
| Kitsune frame data | #38 | Her normals, throw and a shared Foxfire file. Flagged that her clips run slower than the GDD targets, and that the engine loaded every fighter's moves into both kits. That bug became a new ticket, YOK-56 |

- **Consumed by:** rules-lawyer.

## 5 · rules-lawyer: the gate
Verdict on PR #37, as written:
```
VERDICT: PASS
data/modifiers/will-o-wisp.json: PASS  (A8/A9 source+rarity, x130% = "30% faster", A10, A2, A12, S2)
data/modifiers/fox-patience.json: PASS (C5/E14/E20: 3 x 1.25 = 3.75 → 4, P1, A10, A12, S2)
docs/design/lv2-proposals.md: PASS     (A4 one step, P1 changes play, E17; Rising Talisman +10% damage alternative flagged vs P1)
```
- **Consumed by:** the designer, who decided the Lv 2 steps (YOK-41, recorded as rule E21). PR #37 was merged into main.

## Designer gates hit during the run
- Approved the job list.
- Picked the tail count, the pose and whether to include UI textures.
- Approved the restyle and chose the references.
- Approved the gate and grab retakes.
- Made the GO calls.
- Approved the clip list, its upgrades and the retakes.
- Decided the Lv 2 steps.
- Approved every merge.

No Meshy job ran without an approval, and every spend stayed at or under its cap.
