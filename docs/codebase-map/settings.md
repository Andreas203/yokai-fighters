# Settings: sound levels and control scheme

`game/scripts/Settings/`, namespace `YokaiFighters.Settings`. Saved to `user://settings.json`; the run, unlocks and story are in the save file ([savegame.md](savegame.md)). `Sim/`, `Ai/`, `Replay/` never read it (test-scanned); the fight gets its scheme as `FightScene.PlayerScheme`. Only `GameFlow._EnterTree` attaches the file (running game, or `GameFlow.SettingsFile` in tests), so tests, captures and the harness run on defaults. The live values are `Ui/Menus/MenuSettings.cs` ([ui.md](ui.md)); buses `Master`, `Music`, `Effects` are in `game/default_bus_layout.tres`. Index: [README.md](README.md).

| File | What it is and its public surface |
|---|---|
| `Settings/SettingsData.cs` | `SettingsData` record: `Master` 80, `Music` 70, `Effects` 80 (0–100), `Muted`, `Scheme` Kihon (E18); `Defaults`. `AudioLevels.ToDb(level)` = `40·log10(level/100)`, floor −80; `IsMute(level)`. |
| `Settings/SettingsStore.cs` | Path injected (`new SettingsStore(path)`). Flat JSON, `CurrentVersion` 1. `Load()` never throws → `SettingsLoad(Data, Status, Problems)` (Loaded/Missing/Corrupt/Partial/OlderVersion/NewerVersion; each field read on its own). `Save(data)` = temp file + rename, false + `LastSaveError`. Tests `SettingsTests`. |
