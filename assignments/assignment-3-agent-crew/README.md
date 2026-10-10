# Assignment 3: Build an Agent Crew

**Game: Yokai Fighters.** This is my capstone, a single-player 2.5D roguelite fighting game for PC, built in Godot 4 (.NET, C#). Ryo, an apprentice exorcist, beats and binds yokai, then drafts their abilities. The Tanuki boss copies whichever of his specials he has invested in most.

## What the crew produces

The crew turns a character design into **game-ready fighting content**. Its outputs:
- rigged 3D fighters and stage assets generated through the Meshy MCP
- measured animation clips
- frame data and hitboxes derived from those clips
- reward modifiers and card text

Every piece passes a written-rules gate before it reaches the game. In this run it produced the assets and content for the game's first vertical slice, **Ryo vs the Kitsune in the bamboo grove at dusk**. That covers both fighters, nine tails, the stage props and backdrop, 41 measured clips (re-timed onto the GDD's frame targets), frame data and hitboxes for every move of both fighters, and two reward modifiers. All of it now runs in a playable demo (see Demo below).

A fighting game is unforgiving here. A move's frame data has to match what the animation shows, frame by frame, or the game feels wrong. So the crew is built around one rule, **clip first (F2)**: no frame data is written until the clip it describes has been generated *and measured*.

## The crew (6 agents)

All six are Claude Code subagents. Their definitions are the ones the project runs, in [`.claude/agents/`](../../.claude/agents/) ([producer](../../.claude/agents/producer.md), [asset-smith](../../.claude/agents/asset-smith.md), [clip-matcher](../../.claude/agents/clip-matcher.md), [movesmith](../../.claude/agents/movesmith.md), [rules-lawyer](../../.claude/agents/rules-lawyer.md), [gameplay-programmer](../../.claude/agents/gameplay-programmer.md)). The orchestration procedure is [`.claude/skills/produce/SKILL.md`](../../.claude/skills/produce/SKILL.md), and the Meshy MCP config (no key) is [`.mcp.example.json`](../../.mcp.example.json).

| # | Agent | Role | Input | Output | Why it can't be removed |
|---|---|---|---|---|---|
| 1 | **producer** | Lead: turns the goal into tickets and a dispatch order | GDD, `rules.md`, Linear board, repo state | Linear tickets (goal, acceptance criteria, agent, rules) and an ordered dispatch plan with designer approvals listed | Without it nothing is sequenced. Dependencies like "rig before clips, clips before frame data" and the credit approvals are never identified |
| 2 | **asset-smith** | Generates and accepts visual assets | Character sheets, style rules, approved job list | Generation specs with credit estimates, then rigged GLB fighters, stage props, UI textures and acceptance verdicts | Without it there are no rigged models, so clip-matcher has nothing to animate |
| 3 | **clip-matcher** | Picks, generates and *measures* each move's animation | Rig task ids, clip-needs list, approved take list | Clip specs, the retarget go/no-go report, and `data/clips/*.json` with real frame counts and hit windows | Without it movesmith has no timing to derive from, and rule F2 forbids frame data without a measured clip |
| 4 | **movesmith** | Designs moves from the clip timing | `data/clips/*.json`, rules tables | `data/moves/*.json` (startup/active/recovery, 2D hitboxes, damage, EX, levels), `data/modifiers/*.json`, card text | Without it the clips never become playable moves or rewards |
| 5 | **rules-lawyer** | Gate: read-only judge against the written rules | Content files + `docs/design/rules.md` | `PASS`, or `FAIL` with the exact rule ID and fix | Without it content merges unchecked. Wrong sources, broken numbers or paired throws would reach the game |
| 6 | **gameplay-programmer** | Builds the Godot (.NET/C#) game that plays the content, and checks it in the engine | `data/` (moves, clips, modifiers, profiles, story) + schemas + rules | The deterministic fight engine, data loaders and schemas, AI, reward draft, demo flow, the models stepped frame by frame from the clips, the hitbox overlay, and 245 automated tests | Without it the content never becomes a game. Its in-engine checks (overlay, model probes) also found bugs the files alone couldn't show, and each went back to the author agent |

A human designer sits at approval gates between the agents (hexagons in the diagram). No Meshy credit is spent without their approval, and a failed gate goes back to the author agent.

## Architecture

Solid arrows are data flow (files the next agent reads). Dotted arrows are tool calls. Hexagons are designer approval gates: no Meshy credit is spent and no content merges without one. The whole ten-agent crew is drawn in the [project README](../../README.md#how-the-agents-work-together).

```mermaid
flowchart TD
    D([Designer<br/>approves jobs, credits, gates, merges])
    O[["Orchestrator<br/>main Claude Code session<br/>runs /produce, carries outputs between agents"]]

    P["1 · producer<br/>IN: GDD, rules.md, Linear board, repo<br/>OUT: tickets + ordered dispatch plan"]
    AS["2 · asset-smith<br/>IN: character sheets, style rules<br/>OUT: generation specs → rigged models, stage, UI textures"]
    CM["3 · clip-matcher<br/>IN: rigged fighters, clip needs<br/>OUT: measured clips → data/clips/*.json"]
    MS["4 · movesmith<br/>IN: data/clips/*.json, rules tables<br/>OUT: frame data, hitboxes, modifiers, card text"]
    RL{"5 · rules-lawyer<br/>IN: content files + rules.md<br/>OUT: PASS / FAIL + rule ID"}

    G1{{"approve job list<br/>+ credit cap"}}
    G2{{"approve takes<br/>+ credit cap"}}
    G3{{"gate call: GO / fallback"}}
    M[("main branch<br/>game data")]

    LIN[(Linear<br/>YOK tickets)]
    MESHY[(Meshy MCP<br/>image · 3D · rig · animate)]
    REPO[(Repo files<br/>specs · GLBs · JSON)]

    D -- "request: build the vertical slice" --> O
    O --> P
    P -. "create / update tickets" .-> LIN
    P -- "dispatch plan" --> O

    O --> AS
    AS -- "spec + credit estimate" --> G1
    G1 -- approved --> AS
    AS -. "text/image→image, image→3D, rig" .-> MESHY
    AS -- "rigged GLBs, turnarounds, acceptance verdicts" --> REPO

    REPO -- "rigged fighters" --> CM
    O --> CM
    CM -- "clip spec + credit estimate" --> G2
    G2 -- approved --> CM
    CM -. "meshy_animate" .-> MESHY
    CM -- "retarget gate report" --> G3
    G3 -- GO --> CM
    CM -- "data/clips/*.json<br/>(frames, hit window, trim)" --> MS

    O --> MS
    MS -- "data/moves, data/modifiers<br/>(frame data from clip timing, F2)" --> RL
    MS -- "timing deviates from GDD<br/>→ re-time (trim/speed, free)" --> CM
    RL -- "FAIL + rule ID → revise (max 3 rounds)" --> MS
    RL -- PASS --> M
    GP["6 · gameplay-programmer<br/>IN: data/*.json + schemas + rules<br/>OUT: deterministic Godot game, loaders, AI, demo flow, tests"]
    M -- "data/moves, clips, modifiers, profiles, story" --> GP
    GP -- "playable build" --> DEMO([Playable demo])
    GP -- "in-engine check: hitbox overlay<br/>on the models → boxes off → fix" --> MS
    O --> GP
    O -. "status updates" .-> LIN
    D -. "reviews / merges" .-> M
```

**Coordination:** Claude Code subagents can't call each other. The main session is the orchestrator. It runs the `produce` procedure: start the producer, then dispatch each ticket through its workflow (generate → clip → content → gate). It passes each agent's output files and report on to the next agent. Agents run in parallel where tickets don't depend on each other, each in its own git worktree and branch, and every result lands as a pull request.

## Does it run?

Yes, and on real work. [`docs/crew/vertical-slice-run-log.md`](../../docs/crew/vertical-slice-run-log.md) traces one full run on 5–6 October 2026: producer → asset-smith → clip-matcher → movesmith → rules-lawyer. It lists the pull request, the Meshy task ids and the credits for each step. In that run:
- 500 Meshy credits were spent, all inside approved caps.
- 41 clips were measured.
- Movesmith wrote frame data for both fighters. It flagged that the clips ran long, so clip-matcher re-timed them. Movesmith then re-derived the data, and every normal now matches the GDD table.
- Once the models were in the game, movesmith re-placed all 34 moves' hitboxes on the bodies.
- The rules-lawyer passed four content PRs (#37, #38, #39, #48), and each merged into the game's `data/`.
- gameplay-programmer built the game that loads all of it, across 22 PRs from an empty project to a playable demo, with tests growing from 12 to 245.

The outputs are in the project itself, where the game and the next agent read them:

| Stage | Agent | Where it is |
|---|---|---|
| References | asset-smith | The chosen stylised references: [Ryo E](../../docs/design/characters/ryo/references/ryo-ref-e.png), [Kitsune D](../../docs/design/characters/kitsune/references/kitsune-ref-d.png) |
| Models and stage | asset-smith | The approved job list [`assets/specs/yok-29-job-list.md`](../../assets/specs/yok-29-job-list.md); turnarounds and rigged models in [`game/assets/generated/characters/`](../../game/assets/generated/characters/); the [stage backdrop](../../game/assets/generated/stage/bamboo-grove/backdrop.png) |
| Retarget gate | clip-matcher | The gate report [`docs/assets/retarget_gate.md`](../../docs/assets/retarget_gate.md) and its measured stills (the [Kitsune's sleeve flare](../../docs/assets/retarget-gate/kitsune-heavy.png); the [shove substituted for the failed grab](../../docs/assets/retarget-gate/ryo-grab-2.png)) |
| Clips | clip-matcher | [`data/clips/`](../../data/clips/), which movesmith reads, e.g. [`ryo-light-punch.json`](../../data/clips/ryo-light-punch.json) and [`kitsune-foxfire.json`](../../data/clips/kitsune-foxfire.json); stills in [`docs/assets/clips/`](../../docs/assets/clips/) |
| Reward content | movesmith | Modifier files that passed rules-lawyer: [`will-o-wisp.json`](../../data/modifiers/will-o-wisp.json), [`fox-patience.json`](../../data/modifiers/fox-patience.json) |
| Frame data | movesmith | [`data/moves/`](../../data/moves/), derived from the re-timed clips: [Ryo's light punch](../../data/moves/ryo-light-punch.json) (4/2/7 = C8), the [heavy kick](../../data/moves/ryo-heavy-kick.json), the shared [Foxfire](../../data/moves/foxfire.json) |
| Hitbox alignment | movesmith → rules-lawyer | [`docs/screenshots/hitbox-alignment/`](../../docs/screenshots/hitbox-alignment/), before and after: Ryo's heavy-kick and air-kick hitboxes moved from floating above the limb onto the foot |

Failure handling also ran:
- Three library grabs failed measurement, so clip-matcher applied the F5 substitute.
- Six clips were proposed for retake rather than forced.
- Movesmith flagged clip timings that disagreed with the GDD instead of hiding them. That loop (movesmith → clip-matcher re-time → movesmith) brought every normal onto target.
- Movesmith also found an engine bug, which became its own ticket.

## Demo: the vertical slice

The crew's output is playable. The demo is Ryo vs the Kitsune in the bamboo grove at dusk. It uses the rigged models, measured clips, frame data and hitboxes this crew produced, plus the two reward modifiers:

1. **Start screen** with the controls ([screenshot](../../docs/screens/yok-39-editable-ui/start.png)).
2. **The fight** on Kihon controls against the AI Kitsune. She has one readable habit: she jumps right after getting up ([screenshot](../../docs/screenshots/bamboo-grove/fight.png)).
3. **F1 hitbox overlay:** the boxes from movesmith's data drawn on the moving models ([screenshot](../../docs/screenshots/YOK-53/ryo-heavy-kick-active.png)).
4. **Win:** the binding line, *"Forgive me, Kitsune. I'll set your spirit free."*, then three reward cards. Foxfire is NEW, a Lv 2 upgrade is UPGRADE, and Will-o'-wisp or Fox's Patience is MODIFIER ([screenshot](../../docs/screens/yok-39-editable-ui/reward_binding.png), [screenshot](../../docs/screens/yok-39-editable-ui/reward_cards.png)).
5. **Rematch** with the drafted power and the carried health, then the demo-complete card ([screenshot](../../docs/screens/yok-39-editable-ui/demo_complete.png)).

**Play it on Windows, no install needed:**
1. Download [`YokaiFighters-Demo-Windows.zip`](https://github.com/Andreas203/yokai-fighters/releases/download/demo-v0.1/YokaiFighters-Demo-Windows.zip) from the [`demo-v0.1` release](https://github.com/Andreas203/yokai-fighters/releases/tag/demo-v0.1) (145 MB).
2. Right-click the zip and choose **Extract All**. Don't run the game from inside the zip.
3. Open the `YokaiFighters-Demo` folder and double-click **`PLAY.bat`**. It checks that the folder is complete, then starts the game. The start screen lists the keyboard and gamepad controls.

To run it from source instead, install Godot 4.7 (.NET) and the .NET 10 SDK, then:
```
git clone https://github.com/Andreas203/yokai-fighters.git
cd yokai-fighters/game
dotnet build
godot --headless --path . --import   # first run only
godot --path .                        # or open game/project.godot in the editor and press F5
```

Agents 1–5 produced what the game *plays*: the models, clips, frame data, hitboxes and reward content. **gameplay-programmer** (agent 6) built the game that plays it, from the deterministic 60-tick fight engine to the demo flow; its work is listed in the [run log](../../docs/crew/vertical-slice-run-log.md), § 6. The HUD and menu screens came from a separate `ui-designer` agent, which isn't part of this crew.

## Where everything lives

This folder holds only this write-up. The agent definitions, the run log and every output are in the project, linked above, so there is one copy of each and it is the one the game uses.
