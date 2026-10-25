# Audio: music

`game/scripts/Audio/`. Sound is sourced, never generated (AM1, AM5). Buses `Master`, `Music`, `Effects` are in `game/default_bus_layout.tres` (names in `Ui/Menus/MenuSettings.cs`). Frame-timed hit sound hooks are `HitCue` / `IHitCueSink` in [fight.md](fight.md). Index: [README.md](README.md).

| File | What it is and its public surface |
|---|---|
| `Audio/MusicPlayer.cs` | Autoload `MusicPlayer` (`project.godot`), so scene changes never restart it: one pausable `AudioStreamPlayer` on the `Music` bus. `Instance`, `PlayMenuMusic()`, `PlayBattleMusic()` (no restart when the track is already playing; loops the whole 60 s track). Tracks `MenuTrackPath`, `BattleTrackPath` under `res://sound/`. Called by `GameFlow`, `GameFlow.Practice` and `FightScene`. |
