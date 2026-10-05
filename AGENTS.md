# Yokai Fighters — agent roster

The crew that builds Yokai Fighters. Development tools only — none of them ship in the game.

**`CLAUDE.md` is the project's source of truth** for the design, the pipeline and the non-negotiables. This file is only the roster: who exists, where their instructions live, and which model they run on.

The crew exists in two places, from the same role definitions in `.claude/agents/<slug>.md`:

- **Claude Code subagents** — read straight from `.claude/agents/<slug>.md` inside a session; `/produce` dispatches them.
- **Paperclip agents** — hired into the Paperclip company "yokai-fighters", each carrying a managed `AGENTS.md` bundle derived from the same file plus the shared operating contract below.

`.claude/agents/<slug>.md` is the single source for every role. Nothing here duplicates it.

## Roster

| Team | Agent | Role definition | Paperclip role | Paperclip model |
|---|---|---|---|---|
| Lead | Producer | [`producer`](.claude/agents/producer.md) | `ceo` | `claude-sonnet-5-5` |
| Assets | Asset Smith | [`asset-smith`](.claude/agents/asset-smith.md) | `designer` | not hired yet |
| Assets | Sound Scout | [`sound-scout`](.claude/agents/sound-scout.md) | `researcher` | unset → Opus 5 (hired as Asset Scout; instructions need re-applying) |
| Assets | Clip Matcher | [`clip-matcher`](.claude/agents/clip-matcher.md) | `designer` | unset → Opus 5 |
| Content | Movesmith | [`movesmith`](.claude/agents/movesmith.md) | `designer` | unset → Opus 5 |
| Content | Habit Writer | [`habit-writer`](.claude/agents/habit-writer.md) | `designer` | unset → Opus 5 |
| Content | Rules Lawyer | [`rules-lawyer`](.claude/agents/rules-lawyer.md) | `qa` | unset → Opus 5 |
| Content | Sparring Partner | [`sparring-partner`](.claude/agents/sparring-partner.md) | `researcher` | unset → Opus 5 |
| Engineering | Gameplay Programmer | [`gameplay-programmer`](.claude/agents/gameplay-programmer.md) | `engineer` | unset → Opus 5 |
| Engineering | UI Designer | [`ui-designer`](.claude/agents/ui-designer.md) | `designer` | unset → Opus 5 |

The **Paperclip model** column is the hired agent's `adapterConfig.model`. Where it is unset the `claude_local` adapter falls back to `claude-opus-5`, so those seven all run on Opus 5 today regardless of what their role file asks for. Setting one needs the `agents:configure` permission, which only the board holds.

This is separate from the `model:` frontmatter in `.claude/agents/<slug>.md` (`sonnet` for everyone except `gameplay-programmer` and `sparring-partner`, which ask for `opus`). That frontmatter governs the **Claude Code subagent** only — it does not reach the hired Paperclip agent.

Everyone reports to the Producer. Paperclip's `role` field is a fixed enum (`ceo`, `cto`, `cmo`, `cfo`, `security`, `engineer`, `designer`, `pm`, `qa`, `devops`, `researcher`, `general`), so each crew role maps to its nearest option; the real role is the job title and the instruction bundle.

## What the Paperclip bundle adds

Each hired agent's `AGENTS.md` is its role definition plus a fixed operating contract:

- Comment on every issue you touch, even when the answer is "no change needed", and always leave a clear next action.
- Mark an issue `blocked` only with a named unblock owner and the exact action that unblocks it.
- Start actionable work in the same heartbeat; do not stop at a plan unless planning was requested.
- Use child issues for long or parallel delegated work instead of polling.
- Respect budget, pause/cancel, approval gates and company boundaries.

## Standing rules for every agent

- The **designer** (the human) owns open questions, purchases, generation jobs, cut gates and every code merge. No agent decides those.
- Agents never push to `main` and never merge. Branches and PR titles are `YOK-<number>-<brief-name>`.
- Content merges only after schema, the Rules Lawyer gate and harness checks. Code merges only after designer approval.
- Timer heartbeats are off for the whole crew; agents wake on demand when the Producer assigns them an issue.
- Each agent holds only the `paperclip` skill, for reading and updating its own issues.

## Changing a role

Edit `.claude/agents/<slug>.md`. Claude Code picks it up immediately; the hired Paperclip agent does not. Re-applying a Paperclip bundle needs the `agents:configure` permission, which only the board holds — ask the designer to update that agent's instructions from the edited file.
