# Codebase map

Maintained by `gameplay-programmer` (and `ui-designer` for UI). Read this index, then only the area file for the code you touch, then only the files your ticket names. When you add, move or delete a `.cs` file under `game/scripts/`, give it a row in its area file in the same PR: `python -I tools/check_docs.py` fails when a script is missing, listed twice, or listed but gone.

Rows say what a file is and its public surface, plus the invariants, rule IDs and test classes an agent needs. Ticket history and superseded values belong in git and `docs/decision-log.md`, not here.

| Area | Folder | What is there |
|---|---|---|
| [Sim](sim.md) | `game/scripts/Sim/` | The deterministic round: `Match` and its partials, `Fighter`, move data and loaders, kits, Tanuki kit, elder twists, `SimConfig`, fixed-step clock |
| [Sim/Run](sim-run.md) | `game/scripts/Sim/Run/` | Run state, reward draft, ability pool, copy rule, map graph, `RunSession`, node scenes and fights, merchant, shop, dojo, trials, carry-over, unlocks, practice dummy |
| [Sim/Input](sim-input.md) | `game/scripts/Sim/Input/` | Input buffer and the Kata / Kihon parsers (K3) |
| [Ai](ai.md) | `game/scripts/Ai/` | `AiView` (P6), behaviour profiles, `ProfileAi`, temperament, Tanuki pressure, AI harness |
| [Fight](fight.md) | `game/scripts/Fight/` | `FightScene` partials, HUD and fight screens, camera, shake, hit sparks, debug overlay, models and clips |
| [Flow](flow.md) | `game/scripts/Flow/` | `ScreenRouter`, `GameFlow` (boot, save points, practice, dev runs), session map view |
| [Ui](ui.md) | `game/scripts/Ui/` | Reward, story, map, rest stop and shop screens, title and menus, the screen kit |
| [Harness](harness.md) | `game/scripts/Harness/` | Newcomer Kihon bot, bot levels, whole-run harness, fight log, results report |
| [SaveGame](savegame.md) | `game/scripts/SaveGame/` | Save data, save store and format versions, elder meetings |
| [Replay](replay.md) | `game/scripts/Replay/` | Replay file, recorder, replayer and catalog |
| [Tutorial](tutorial.md) | `game/scripts/Tutorial/` | Guided apprentice fight (Y6) |
| [Story](story.md) | `game/scripts/Story/` | Story-card and merchant-line loaders |
| [Dev](dev.md) | `game/scripts/Dev/` | Dev mode, dev menu, free travel, jumps, run-state editor |
| [Audio](audio.md) | `game/scripts/Audio/` | Music player |
| [Settings](settings.md) | `game/scripts/Settings/` | Settings data and store |
| [Project](project.md) | `game/` | `ContentPaths.cs`, export, the scenes the code relies on, editing in Godot |
| [Data](data.md) | `data/`, `data/schema/` | Content kinds, schemas, the loader for each, data conventions |
| [Tests](tests.md) | `game/tests/` | Runner, entry scenes, fixtures, test classes by area |
| [Tools](tools.md) | `tools/`, `.github/workflows/` | Validators, rules check, merge check, docs check, changelog, asset tools, goal agent, CI |
| [Goal agent notes](goal-agent.md) | `tools/goal_agent/` output | Entries the goal agent appends for designer review, to be moved into their area |
