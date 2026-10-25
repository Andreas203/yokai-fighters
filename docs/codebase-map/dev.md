# Dev: developer mode

`game/scripts/Dev/`, namespace `YokaiFighters.Dev`. A development tool only, never on in a release build without `--dev`. Nothing in `Sim/`, `Ai/` or `Replay/` reads it (test-scanned). Dev mode swaps to its own save slot (`save.dev.json`; the player's file is never opened); a dev-jump run is never written. Run changes only at a node boundary (`DevContext.TryChangeRun`). Opens with F11 on the title, map (dev on) and pause. Index: [README.md](README.md).

| File | What it is and its public surface |
|---|---|
| `Dev/DevMode.cs` | Static flag. `Evaluate(debugBuild, args)`: available in a debug build or with `--dev`; on at launch only with `--dev`. `Available`, `On`, `State`, `Changed`. The only switch: `TrySetOn(place, on, out refusal)` (title only; `SwitchBlocker` while a save write is pending). `MenuKey` F11. `SavePathFor(path)`, `SavePath(playerPath)`. Test hooks `Configure`, `ResetToLaunch`. |
| `Dev/DevShell.cs` | Autoload `DevShell` (`scenes/dev/dev_shell.tscn`, CanvasLayer 120): the "DEV" `Badge` and the `Menu`. `OpenMenu(DevContext, onClosed)`, `CanOpen(place)`. |
| `Dev/DevContext.cs` | What a page gets: `Place` (`DevPlace` Title/Map/Fight), `Flow`, `Fight`. Determinism gate: `AtNodeBoundary`, `RunChangeRefusal`, `TryChangeRun(change, out refusal)` (refused mid-fight, `MidFightReason`). |
| `Dev/DevMenu.cs` | `scenes/ui/dev_menu.tscn`: mode toggle (title only), section buttons, the open page, Close; Esc / pad B / F11 close. `Open(ctx)`, `Close()`, `ShowSection(id)`, `CurrentSection`, `Page`, `Closed`, `AddSection(id, title, build, needsDevMode)` (same id replaces in place), `RemoveSection`, `FindSection`, `Sections`. |
| `Dev/DevMenu.Sections.cs` | Built-in sections: `DebugTools` (every debug key from its owner's constant), `map` = `DevMapPage`, `jump` = `DevJumpPage`, `run` = `DevRunStatePage`. |
| `Dev/DevMap.cs` | Static `FullMapUnlocked`, `RevealAll`, `Active`; `Apply(session)` sets `RunMap.FreeTravel` (sticky `OffRoute`, saved only in a dev session). `SetFullMap`, `SetReveal`, `TravelRefusal`, `Travel(ctx, id)` = `GameFlow.ChooseNode` (T1, drafts and saves run as usual). Tests `DevMapTests`. |
| `Dev/DevMapPage.cs` | The Map page: full-map and reveal toggles, every node with `Travel_<id>` / `Info_<id>`; greyed mid-fight. |
| `Dev/DevJump.cs` | Godot-free catalog: `Targets()` (`DevJumpTarget`), `Opponents()`, `Elders`, `DevFightSetup(...)`, `BuildFight` (one-node `dev-jump` map), `BuildRestStop(type)`, `ShapeCopy(run, pool, special, level)` (T2–T4; throws when unreachable from Ryo's starters), `BuildDraft(pool, yokai, seed, rares)`, `NameOf(setup)`, `PlaysAsOf`. |
| `Dev/DevJumpPage.cs` | The Jump page: fight and draft setup, one button per target. `Jump(id)` → `TryChangeRun` → static `Perform(flow, target, fight, draft, beforeOpen, out error)`. |
| `Dev/DevRunStates.cs` | `IDevRunStateSource { Build(pool); Describe }`, `DefaultDevRunState` (A1 starters Lv 1), settable `DevRunStates.Source`, `Reset()`. Every jump asks it for a fresh `RunState`. |
| `Dev/DevRunEditor.cs` | Pure logic of the Run state page: `DevSlotEdit`, `DevRunBuild(Slots, Coins, Health, Depth, Seed)`, `DevRunEditor(pool, graph, scenes, preferredWalk)` / `For(session)`, `Capture`, `Check(build)` → `DevRunCheck(Session, Copy, Refusal)` via the saved-run path (A2–A4, R6, T3, R8, T1, T5, C1). `TryWalk`, `MaxDepth`, `SpecialsFor`, `ModifiersFor`, `BuiltInPresets(seed)`. Tests `DevRunStateTests`. |
| `Dev/DevPresetStore.cs` | Named builds in `run-presets.dev.json` (`PathFor(playerSavePath)`): `Load` (cap `MaxBytes` 1 MB), `Save`, `Delete`, `Serialize`/`Parse` (v1). Never opens a save file. |
| `Dev/DevRunStatePage.cs` | Thin page over `DevRunEditor`: slots A–D, coins, health, depth, seed, `CopyLabel`, `StatusLabel`, Apply → `GameFlow.AdoptDevSession`, presets. |
| `Dev/DevScreenHost.cs` | Dev viewer (CanvasLayer 110): one screen scene over an opaque backdrop; `Finish()` frees it. |

Tests `DevModeTests`; captures `tests/captures/dev_capture.tscn` → `docs/screenshots/current/dev/`.
