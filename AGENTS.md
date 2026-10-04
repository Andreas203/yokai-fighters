# Yokai Fighters — agent roster

The crew that builds Yokai Fighters. Development tools only — none of them ship in the game.

**`CLAUDE.md` is the project's source of truth** for the design, the pipeline and the non-negotiables. This file is only the roster: who exists, where their instructions live, and which model they run on.

The crew exists in two places, from the same role definitions in `.claude/agents/<slug>.md`:

- **Claude Code subagents** — read straight from `.claude/agents/<slug>.md` inside a session; `/produce` dispatches them.
- **Paperclip agents** — hired into the Paperclip company "yokai-fighters", each carrying a managed `AGENTS.md` bundle derived from the same file plus the shared operating contract below.

`.claude/agents/<slug>.md` is the single source for every role. Nothing here duplicates it.

## Roster

| Team | Agent | Role definition | Paperclip role | Model |
|---|---|---|---|---|
| Lead | Producer | [`producer`](.claude/agents/producer.md) | `ceo` | Opus |
| Assets | Asset Scout | [`asset-scout`](.claude/agents/asset-scout.md) | `researcher` | Sonnet |
| Assets | Clip Matcher | [`clip-matcher`](.claude/agents/clip-matcher.md) | `designer` | Sonnet |
| Content | Movesmith | [`movesmith`](.claude/agents/movesmith.md) | `designer` | Sonnet |
| Content | Habit Writer | [`habit-writer`](.claude/agents/habit-writer.md) | `designer` | Sonnet |
| Content | Rules Lawyer | [`rules-lawyer`](.claude/agents/rules-lawyer.md) | `qa` | Sonnet |
| Content | Sparring Partner | [`sparring-partner`](.claude/agents/sparring-partner.md) | `researcher` | Opus |
| Engineering | Gameplay Programmer | [`gameplay-programmer`](.claude/agents/gameplay-programmer.md) | `engineer` | Opus |
| Engineering | UI Designer | [`ui-designer`](.claude/agents/ui-designer.md) | `designer` | Sonnet |

Everyone reports to the Producer. Paperclip's `role` field is a fixed enum (`ceo`, `cto`, `cmo`, `cfo`, `security`, `engineer`, `designer`, `pm`, `qa`, `devops`, `researcher`, `general`), so each crew role maps to its nearest option; the real role is the job title and the instruction bundle.

## What the Paperclip bundle adds

Each hired agent's `AGENTS.md` is its role definition plus a fixed operating contract:

- Comment on every issue you touch, even when the answer is "no change needed", and always leave a clear next action.
- Mark an issue `blocked` only with a named unblock owner and the exact action that unblocks it.
- Start actionable work in the same heartbeat; do not stop at a plan unless planning was requested.
- Use child issues for long or parallel delegated work instead of polling.
- Respect budget, pause/cancel, approval gates and company boundaries.

## Standing rules for every agent

- The **designer** (the human) owns open questions, purchases, cut gates and every code merge. No agent decides those.
- Agents never push to `main` and never merge. Branches and PR titles are `YOK-<number>-<brief-name>`.
- Content merges only after schema, the Rules Lawyer gate and harness checks. Code merges only after designer approval.
- Timer heartbeats are off for the whole crew; agents wake on demand when the Producer assigns them an issue.
- Each agent holds only the `paperclip` skill, for reading and updating its own issues.

## Changing a role

Edit `.claude/agents/<slug>.md`. Claude Code picks it up immediately; the hired Paperclip agent does not. Re-applying a Paperclip bundle needs the `agents:configure` permission, which only the board holds — ask the designer to update that agent's instructions from the edited file.
