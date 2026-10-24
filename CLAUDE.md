# Yokai Fighters

Single-player 2.5D roguelite fighting game for PC, built in **Godot 4 (.NET) with C#** over a 5-week capstone build. Ryo, an apprentice exorcist, binds yokai and drafts their abilities; the Tanuki boss copies his most-invested special.

## Sources of truth
- `Yokai_Fighters_GDD_Extended.pdf` — the full design. `Yokai_Fighters_GDD_Short.pdf` is the 5-page summary.
- `docs/design/gdd-extended.md`, `docs/design/gdd-short.md` — word-for-word Markdown transcriptions of the two PDFs, with page markers; read these instead of extracting the PDF. Diagrams are approximate there, and if a transcription disagrees with its PDF, the PDF wins and the transcription gets fixed.
- `docs/design/gdd-amendments.md` — approved changes to the PDF (e.g. AM1: visuals are generated, sound is sourced). An amendment wins over the PDF section it names.
- `docs/design/rules.md` — the written rules distilled from the GDD and its amendments, with citable IDs (C4, T2, A8…). Every agent works from it; if it disagrees with the PDF + amendments, they win and rules.md gets fixed.
- `docs/decision-log.md` — append-only history of decisions (Linear comments, amendments, gates, spend). Load it as memory before planning; append new decisions there. What still waits on the designer is in Linear (designer queue YOK-189, label `decision`), not in the repo.
- `docs/process/change-tracking.md` — where each kind of change is recorded (amendment, decision line, Linear ticket, PR and `CHANGELOG.md`). Read it before writing any new `.md` file; `tools/check_docs.py` enforces it in CI. The sources above win if it disagrees.
- The **designer** (the human) owns open questions, purchases, approval of every generation job and its credit spend, cut gates and every code merge. Never decide those on their behalf.

## The agent crew
Development tools only — none of them ship in the game. Defined in `.claude/agents/`.

| Team | Agent | Role |
|---|---|---|
| Lead | `producer` | Measures the gap between the design and the code, tickets it in priority order and plans which workflow runs it |
| Assets | `asset-smith` | Generation specs and acceptance checks for models, stages, UI and VFX textures; level presets |
| Assets | `rig-specialist` | Rigs every fighter (generator auto-rig or AccuRIG 2), checks it in Blender, keeps it on the shared skeleton, runs the G4 gate |
| Assets | `animator` | Specs each move's clip, cleans, measures and retimes the take, then frame-timed sound |
| Assets | `sound-scout` | Shortlists sound-effect and music packs |
| Content | `movesmith` | Move list, frame data from matched clips, hitboxes, card text, trials |
| Content | `habit-writer` | Yokai temperament profiles and all story-card text |
| Content | `rules-lawyer` | Gate: rejects content that breaks the written rules, with a reason |
| Content | `sparring-partner` | Reads whole-run bot results; recommends minimal tuning |
| Engineering | `gameplay-programmer` | C# systems, fight AI, test bot and harness via the Godot MCP |
| Engineering | `ui-designer` | Every screen and the HUD via the Godot MCP |
| Engineering | `code-critic` | Gate: assumes a code branch is wrong and tries to prove it against the written rules; read-only |
| Engineering | `test-breaker` | Gate: writes probe tests that try to break a code branch; writes probes only |

Run a production pass with `/produce` (see `.claude/skills/produce/SKILL.md`): the producer plans, then the main session dispatches tickets through their workflow.

## Pipeline
```
Asset Smith spec → designer approves → Asset Smith runs (Meshy MCP or Tripo) → accepts model
                                                   │
Rig Specialist spec → designer approves → rig job (auto-rig or AccuRIG 2) → Blender checks → G4 gate
                                                   │
Animator spec → designer approves take → take (generator, mocap, pack) → Blender cleanup → measures → retimes
                                                   │
                           Movesmith / Habit Writer → Rules Lawyer ──fail: back with reason──┐
                                                          │ pass                             │
                                                          ▼                                  │
                                                    Harness checks → data merges        ◄────┘
Sound Scout shortlist → designer buys → Animator sound cues
Gameplay Programmer / UI Designer → own branch, build-and-test loop → Code Critic → Test Breaker → designer review → merge
                                             ▲                              │ fail: back with file, line, rule or probe
                                             └──────────────────────────────┘
Goal agent (tools/goal_agent): rules.md + GDD → scan → gaps → priority → writes the top gap → same two gates → designer review
Harness results → Sparring Partner → Producer tickets the tuning
```
- Data merges after schema, Rules Lawyer and harness checks.
- Code merges **only** after designer approval. Agents never push to `main` or merge. The two code gates do not replace that review: they decide what is worth the designer's time.

## Repo layout (created as work lands)
| Path | Contents | Owner |
|---|---|---|
| `production/playtests/` | Outside playtest notes | designer |
| `assets/specs/` | Generation specs and acceptance results for models, stages, textures | asset-smith |
| `assets/specs/rigs/` | Rig specs, the shared skeleton and its bone map (`skeleton.md`), rig verdicts and stress-pose stills | rig-specialist |
| `assets/specs/clips/` | Per-move clip specs (source, cleanup, retiming), take verdicts, measured stills (`stills/`); the retarget gate report is rig-specialist's | animator |
| `assets/shortlists/` | Sound and music pack comparisons | sound-scout |
| `data/clips/`, `data/sound/` | Clip matches, sound cues | animator |
| `data/presets/` | Level presets | asset-smith |
| `tools/blender/` | Headless Blender scripts: rig checks, weight repair, cleanup, retargeting, clip measurement | rig-specialist, animator |
| `data/moves/`, `data/modifiers/`, `data/cancels/`, `data/trials/`, `data/cards/` | Ability content | movesmith |
| `data/profiles/`, `data/story/` | Behaviour profiles, story cards | habit-writer |
| `data/schema/` | Content schemas | gameplay-programmer |
| `game/` | Godot project | gameplay-programmer, ui-designer |
| `harness/results/`, `harness/reports/` | Raw bot runs, balance reports | gameplay-programmer, sparring-partner |
| `docs/README.md`, `docs/process/` | Index of every document; how work moves (review queue, digest, data merge check) and where each change is recorded (`change-tracking.md`) | designer |
| `CHANGELOG.md` | Every merged PR by build week, generated by `tools/changelog.py`; never edited by hand | producer |
| `docs/codebase-map.md` | Maintained map of the code, to keep agent context small | gameplay-programmer |
| `docs/design/screens/` | Target concept for every screen, with the prompts that made them; `rough/` holds ui-designer's greybox mockups | designer, ui-designer |
| `docs/screenshots/current/` | Latest capture of each screen, the stage and the fighters; a new capture overwrites the old | ui-designer, gameplay-programmer |
| `docs/screenshots/review/` | Old ticket screenshots waiting for the designer to keep or delete | designer |
| `vault/` | The design as linked notes, one topic per note; the content agents load the notes listed in their definitions whole | designer |
| `tools/content_pipeline/` | Drafts card text from the vault: runs the writer agents and the Rules Lawyer as critic on the whole notes each lists under "Vault notes"; output is `proposed` and still goes through the Rules Lawyer | designer |
| `tools/goal_agent/` | Goal-oriented coding agent: reads rules.md and the GDD, scans the code, ranks the gaps (`features.json`, `priority.py`) and writes the top one through build, test, Code Critic and Test Breaker gates. `gaps` is the producer's gap report. Works on a branch, never commits | designer |

## Tickets, branches and PRs
- Tickets live in **Linear**: team `Yokai-fighters` (key `YOK`), project "Yokai Fighters: 5-Week Capstone Build", grouped under Epics A–F.
- Branch names **and** PR titles: `YOK-<number>-<brief-name>` for the ticket delivered, e.g. `YOK-19-generic-throws`.
- Work without a ticket uses the project code alone: `YOK-<brief-name>`, e.g. `YOK-agent-crew`.
- Brief name: a few lowercase words joined by hyphens describing the change.

## Non-negotiables
- Deterministic 60-tick loop; animation stepped with `AnimationPlayer.Seek()`; hitboxes are 2D rectangles in move data.
- Visuals are generated (Tripo or Meshy; rigs also AccuRIG 2; clips also motion capture or bought packs, cleaned in Blender by script), sound is sourced (AM1, AM5); agents run only jobs the designer has approved, at the approved cost. Clip first, then frame data. No hand-keyed animation, no paired throws.
- Never cut: the copy rule, the merchant, Kihon, the harness, the story cards.
- Cut order if behind: scope gate (end of week 1) → Kata, then meta-unlocks; balance gate (mid week 3) → modifiers 14→8, then dojo trials, then colour-only presets. The designer makes the call.
