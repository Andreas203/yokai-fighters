# Crew diagram: Yokai Fighters asset-to-content crew

Solid arrows are data flow (files the next agent reads). Dotted arrows are tool calls. Hexagons are designer approval gates: no Meshy credit is spent and no content merges without one.

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
    RL -- "FAIL + rule ID → revise (max 3 rounds)" --> MS
    RL -- PASS --> M
    O -. "status updates" .-> LIN
    D -. "reviews / merges" .-> M
```

The agents never call each other directly (Claude Code subagents can't). The orchestrator passes each agent's file outputs and report to the next one, following `crew/orchestration/produce-SKILL.md`.
