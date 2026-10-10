# Yokai Fighters

A single-player **2.5D roguelite fighting game** for PC, built in **Godot 4 (.NET) with C#** as a 5-week capstone.

You play Ryo, an apprentice exorcist who binds yokai instead of destroying them, apologising to each one he seals into a talisman. Every yokai he beats lends him one of three abilities, so the yokai you choose to fight decide the fighter you become. At the end of every run waits the Tanuki, a shapeshifter that copies the move you have invested in most.

## The game at a glance

| | |
|---|---|
| **Run** | 8-row map, one node per row: 6 one-round duels (two against elders) and 2 rest stops, then the Tanuki. 25–40 minutes. |
| **Duel** | 60 fps, deterministic, one round. Ryo's 1,000 health carries across the run. |
| **Reward** | After each win, draft 1 of 3 abilities from the yokai you beat. Specials level up and evolve at Lv 3. |
| **Yokai** | Kitsune (zoner), Oni (rushdown), Kappa (grappler), each with two temperaments, one punishable habit and an elder twist. |
| **Boss** | The Tanuki copies your highest-level special at your level, but never your modifiers or evolution. |
| **Controls** | Kata (motion inputs, +10% precision bonus) or Kihon (one Special button). |
| **Between runs** | Unlocks by depth reached; your best special carries over at Lv 2. |

### Design pillars
1. Every upgrade changes how you fight.
2. Your fighter is yours.
3. Every choice costs something.
4. Easy to start, deep to master.
5. The game answers your habits.
6. Readable, fair fights.

## Documents

| File | What it is |
|---|---|
| [`Yokai_Fighters_GDD_Extended.pdf`](Yokai_Fighters_GDD_Extended.pdf) | Full game design document, the source of truth |
| [`Yokai_Fighters_GDD_Short.pdf`](Yokai_Fighters_GDD_Short.pdf) | 5-page readable summary |
| [`docs/design/rules.md`](docs/design/rules.md) | The GDD's rules and numbers with citable IDs (`C4`, `T2`, `A8`…) |
| [`vault/`](vault/Home.md) | The design as linked notes, one topic per note, for people and for the agents |
| [`tools/content_pipeline/`](tools/content_pipeline/) | Drafts card text from the vault: retrieval, a generator, and a critic that checks each draft against the vault |
| [`docs/crew/vertical-slice-run-log.md`](docs/crew/vertical-slice-run-log.md) | The crew's run that built the Ryo vs Kitsune vertical slice, step by step |
| [`docs/agent-crew.html`](docs/agent-crew.html) | Interactive map of the agent crew (open in a browser) |
| [`CLAUDE.md`](CLAUDE.md) | Working instructions for Claude Code and the crew |

## The agent crew

The game is built with a crew of AI agents running in Claude Code. They are **development tools only**; none of them ship in the game. A human designer reviews their work, runs the asset generators, buys sound, makes cut decisions and approves every code merge.

| Team | Agent | Responsibility |
|---|---|---|
| Lead | `producer` | Turns outstanding work into Linear tickets and a dispatch plan |
| Assets | `asset-smith` | Writes generation specs for models, stages and textures, runs approved jobs through the Meshy MCP and checks the results |
| Assets | `clip-matcher` | Specs each move's clip for Meshy and measures the generated take, then level presets and frame-timed sound |
| Assets | `sound-scout` | Shortlists sound-effect and music packs for the designer to buy |
| Content | `movesmith` | Writes frame data from the matched clip, hitboxes, card text and dojo trials |
| Content | `habit-writer` | Writes yokai behaviour profiles and all story-card text |
| Content | `rules-lawyer` | Gate: rejects content that breaks the written rules, citing the rule |
| Content | `sparring-partner` | Reads whole-run bot results and recommends minimal tuning |
| Engineering | `gameplay-programmer` | C# systems, fight AI, test bot and harness via the Godot MCP |
| Engineering | `ui-designer` | Every screen and the HUD via the Godot MCP |

Agent definitions live in [`.claude/agents/`](.claude/agents/). Run a production pass with **`/produce`** ([`.claude/skills/produce/SKILL.md`](.claude/skills/produce/SKILL.md)).

### How the agents work together

Subagents in Claude Code can't call each other directly. The main Claude Code session is the orchestrator: it runs the Producer, then carries each ticket from agent to agent along one of its workflows. Agents communicate through files in the repo (clip matches, content data, harness results), Linear tickets and pull requests.

```mermaid
flowchart TD
    D(["Designer (human)"])
    O{{"Orchestrator<br/>main session · /produce"}}
    P["Producer<br/>Linear tickets + dispatch plan"]
    L[("Linear · YOK")]

    D -- "requests, playtest notes" --> O
    O -- "plan" --> P
    P -- "tickets" --> L
    P -- "dispatch plan" --> O

    subgraph ASSETS["Assets team"]
        AS["Asset Smith<br/>generation specs + acceptance"]
        CM["Clip Matcher<br/>clip specs, measured takes,<br/>presets, sound cues"]
        SS["Sound Scout<br/>sound + music shortlists"]
    end

    subgraph ENG["Engineering team"]
        GP["Gameplay Programmer<br/>C# via Godot MCP"]
        UI["UI Designer<br/>screens + HUD via Godot MCP"]
    end

    subgraph CONTENT["Content team"]
        MS["Movesmith<br/>frame data, hitboxes, cards, trials"]
        HW["Habit Writer<br/>behaviour profiles, story cards"]
        RL{"Rules Lawyer<br/>gate"}
        SP["Sparring Partner<br/>balance reports"]
    end

    H[["Harness<br/>headless bot runs"]]
    R(["Designer review"])
    M[("main branch")]

    O -- "generate" --> AS
    AS -- "spec → approved → Meshy MCP → accepted rigs" --> CM
    O -- "sound" --> SS
    SS -- "shortlist → designer buys" --> CM

    O -- "clip" --> CM
    CM -- "clip timing" --> MS
    O -- "content" --> HW
    MS -- "content files" --> RL
    HW -- "profiles, story" --> RL
    RL -. "FAIL + rule ID" .-> MS
    RL -. "FAIL + rule ID" .-> HW
    RL -- "PASS" --> H
    H -- "checks pass: data merges" --> M

    O -- "code" --> GP
    O -- "code" --> UI
    GP -- "builds bot + harness" --> H
    GP -- "PR on YOK-n branch" --> R
    UI -- "PR on YOK-n branch" --> R
    R -- "approve + merge" --> M

    H -- "bot results" --> SP
    SP -- "tuning recommendations" --> P
```

**The workflows**

| Workflow | Path | Ends when |
|---|---|---|
| **content** | Clip Matcher (if a clip is needed: see **clip**) → Movesmith or Habit Writer → Rules Lawyer → harness checks | Schema, Rules Lawyer and harness pass, then the data merges. Three failed gate rounds block the ticket for the designer. |
| **generate** | Asset Smith spec → designer approves job and credits → Asset Smith runs it via the Meshy MCP → acceptance check | Accepted, or 3 takes then a fallback |
| **clip** | Clip Matcher spec → designer approves the take → Clip Matcher runs it via the Meshy MCP and measures it | A measured clip in `data/clips/`, or an F5 substitute after 3 takes |
| **sound** | Sound Scout → designer buys → Clip Matcher sound cues | The designer has made the purchase decision |
| **code** | Gameplay Programmer or UI Designer on its own branch, build-and-test loop via the Godot MCP | The designer approves the PR. Agents never merge. |
| **balance** | Harness results → Sparring Partner → Producer tickets the tuning | Tuning tickets re-enter the content workflow |

## Tickets, branches and PRs

- Tickets live in **Linear**, team `Yokai-fighters` (key `YOK`), grouped under Epics A–F.
- Branch names and PR titles are both `YOK-<number>-<brief-name>`, e.g. `YOK-19-generic-throws`.
- Work without a ticket uses the project code alone: `YOK-<brief-name>`, e.g. `YOK-agent-crew`.

## Technical constraints

- Deterministic 60-tick loop. Animations are stepped by exact frames with `AnimationPlayer.Seek()`, and hitboxes are 2D rectangles in move data, so frame data is exactly true and whole-run bot tests are repeatable.
- Visual assets are generated through the Meshy MCP (Image-to-3D, auto-rig and animation, plus its 2D image models for turnarounds, backdrops and textures; agents run only designer-approved jobs) and unified by a toon shader; sound and music are bought. The clip is generated and measured first, then frame data is written from it. No hand-keyed animation, no paired throws. See [`docs/design/gdd-amendments.md`](docs/design/gdd-amendments.md) (AM1).
- **Never cut:** the copy rule, the merchant, Kihon, the harness, the story cards.
- **Cut order if behind:** scope gate (end of week 1): Kata, then meta-unlocks. Balance gate (mid week 3): modifiers 14 → 8, then dojo trials, then colour-only presets.

## Schedule

| Week | Milestone |
|---|---|
| 1 | Move list, fighters generated and auto-rigged, retargeting gate, sound packs bought. Combat core, both control schemes, AI framework, test bot. Scope gate. |
| 2 | All 33 abilities, 7 behaviour profiles, Tanuki and copy rule. Map, merchant, dojo, unlocks with placeholder screens. Whole-run harness. First outside playtest. |
| 3 | Final screens and HUD, story cards, integration. Balance gate. Feature freeze. |
| 4–5 | Polish only: second playtest, balance, audio, effects, bug fixing. |
