# Yokai Fighters

Single-player 2.5D roguelite fighting game for PC, built in **Godot 4 (.NET) with C#** over a 5-week capstone build. Ryo, an apprentice exorcist, binds yokai and drafts their abilities; the Tanuki boss copies his most-invested special.

## Sources of truth
- `Yokai_Fighters_GDD_Extended.pdf` — the full design (read with `pdftotext -layout`). `Yokai_Fighters_GDD_Short.pdf` is the 5-page summary.
- `docs/design/rules.md` — the written rules distilled from the GDD, with citable IDs (C4, T2, A8…). Every agent works from it; if it disagrees with the PDF, the PDF wins and rules.md gets fixed.
- The **designer** (the human) owns open questions, purchases, cut gates and every code merge. Never decide those on their behalf.

## The agent crew
Development tools only — none of them ship in the game. Defined in `.claude/agents/`.

| Team | Agent | Role |
|---|---|---|
| Lead | `producer` | Tickets outstanding work and plans which workflow runs it |
| Assets | `asset-scout` | Shortlists model, animation, sound and music packs |
| Assets | `clip-matcher` | Picks each move's clip first, then level presets and frame-timed sound |
| Content | `movesmith` | Move list, frame data from matched clips, hitboxes, card text, trials |
| Content | `habit-writer` | Yokai temperament profiles and all story-card text |
| Content | `rules-lawyer` | Gate: rejects content that breaks the written rules, with a reason |
| Content | `sparring-partner` | Reads whole-run bot results; recommends minimal tuning |
| Engineering | `gameplay-programmer` | C# systems, fight AI, test bot and harness via the Godot MCP |
| Engineering | `ui-designer` | Every screen and the HUD via the Godot MCP |

Run a production pass with `/produce` (see `.claude/skills/produce/SKILL.md`): the producer plans, then the main session dispatches tickets through their workflow.

## Pipeline
```
Asset Scout → Clip Matcher → Movesmith / Habit Writer → Rules Lawyer ──fail: back with reason──┐
                                                            │ pass                             │
                                                            ▼                                  │
                                                      Harness checks → data merges        ◄────┘
Gameplay Programmer / UI Designer → own branch, build-and-test loop → designer review → merge
Harness results → Sparring Partner → Producer tickets the tuning
```
- Data merges after schema, Rules Lawyer and harness checks.
- Code merges **only** after designer approval. Agents never push to `main` or merge.

## Repo layout (created as work lands)
| Path | Contents | Owner |
|---|---|---|
| `production/tickets/`, `production/BOARD.md` | Tickets and board | producer |
| `production/playtests/` | Outside playtest notes | designer |
| `assets/shortlists/` | Pack comparisons | asset-scout |
| `data/clips/`, `data/presets/`, `data/sound/` | Clip matches, level presets, sound cues | clip-matcher |
| `data/moves/`, `data/modifiers/`, `data/cancels/`, `data/trials/`, `data/cards/` | Ability content | movesmith |
| `data/profiles/`, `data/story/` | Behaviour profiles, story cards | habit-writer |
| `data/schema/` | Content schemas | gameplay-programmer |
| `game/` | Godot project | gameplay-programmer, ui-designer |
| `harness/results/`, `harness/reports/` | Raw bot runs, balance reports | gameplay-programmer, sparring-partner |
| `docs/codebase-map.md` | Maintained map of the code, to keep agent context small | gameplay-programmer |

## Non-negotiables
- Deterministic 60-tick loop; animation stepped with `AnimationPlayer.Seek()`; hitboxes are 2D rectangles in move data.
- Clip first, then frame data. No bespoke animation, no paired throws.
- Never cut: the copy rule, the merchant, Kihon, the harness, the story cards.
- Cut order if behind: scope gate (end of week 1) → Kata, then meta-unlocks; balance gate (mid week 3) → modifiers 14→8, then dojo trials, then colour-only presets. The designer makes the call.
