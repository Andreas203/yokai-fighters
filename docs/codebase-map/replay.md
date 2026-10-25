# Replay: record and re-run fights

`game/scripts/Replay/`, namespace `YokaiFighters.Replay`. Pure C#. Record a fight from tick 0 and re-run it headless to an identical result (F3). Content is stored as **ids + fingerprints**, never the data, so a replay made before a data change fails at setup naming what changed. New header fields are left out when default, so older files keep loading. Index: [README.md](README.md).

| File | What it is and its public surface |
|---|---|
| `Replay/ReplayRecording.cs` | `.yfr` = `"YFR1"` + gzip(header JSON, per step P1/P2 bits + move, `StateHash` after the step). `ReplayHeader`: `ConfigFp`, P1/P2 max/start health, `FighterSetup` per side (scheme, `Moves` as `KitEntry(Id, Fp)`, `Specials` as `SpecialEntry(Slot, Id, Level, Modifier, MoveFp, ExFp)`, `CancelRules`, `PushWidth`, `CopiedSlot`, `Twist`/`TwistFp`), `Ai` (`AiSetup`: profile, reaction frames, seed, `FightTier`, `StandingHabits`, `AggressionBonus`, `GuardPct`), `Tutorial`, jump-punish pcts, `Events`, `RunHash`, `Result`. |
| (cont.) | `Fingerprint.Of(obj)` = FNV-1a 64 over sorted public properties (`[FingerprintOmitWhenNull]`). |
| `Replay/ReplayRecorder.cs` | `Begin(match, ai, aiSeed, run?)` at tick 0 (snapshots fighters, not loaders), `Capture`, `Record(p1, p2, hashAfter)`, `OnScheme`/`OnAi`/`OnTutorial`/`OnTutorialSkip`, `Finish(match)`. Caps at one hour of steps. |
| `Replay/Replayer.cs` | `ReplayCatalog.FromRepo(root)` = every `*.json` under `data/` and `game/tests/fixtures/` by kind (moves, specials, modifiers, profiles, twists). `Replayer.Run(rec, catalog)` → `ReplayReport` (`Build`, P2 = the AI re-run from its seed or recorded input, `FirstAiMismatchStep`, `FirstDivergentStep`, result compare). `ReplaySetupException` = what changed. Tests `ReplayTests`. |
