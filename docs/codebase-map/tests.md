# Tests: `game/tests/`

Index: [README.md](README.md). Each area file names the test classes for its files; this page is the runner, the entry scenes, the fixtures and a finder by area.

## Running

```
cd game
dotnet build
<godot-console-exe> --headless --path . --import          # first run only
<godot-console-exe> --headless --path . res://tests/test_runner.tscn [-- only=<text>]
```

- `tests/test_runner.tscn` (`TestRunner.cs`) runs every public static `[Test]` method (optionally taking the runner `Node`), prints `PASS/FAIL`, exits 0/1. It awaits a frame between classes and drains finalizers before quitting (`DrainFinalizers`: a pending RefCounted wrapper at shutdown crashes Godot). Tests that build `InputEvent`s dispose them after `PushInput`. Add tests as new static classes; no NuGet.
- Build and test with the Godot 4.7.2 mono build; CI runs the same (`.github/workflows/`).
- `tests/probes/` = test-breaker's probe tests (`*Probes.cs`); they run with the suite. Only test-breaker writes there.
- Isolation rules enforced by tests: `Sim/`, `Ai/`, `Replay/` never reference Godot, settings, saves or dev mode; `Tutorial/` and `SaveGame/` have no clock, floats or randomness; naming the save store is an explicit list in `SaveProbes.Isolation_*`, not a name suffix (see [savegame.md](savegame.md)).

## Entry scenes (not tests)

| Scene | Script | What it does |
|---|---|---|
| `tests/run_harness.tscn` | `RunHarnessMain.cs` | Whole-run bot batch, see [harness.md](harness.md). |
| `tests/ai_harness.tscn` | `AiHarnessMain.cs` | `-- [--profile id] [--p1 thrower\|jumper\|random\|idle] [--reaction N] [--seed N] [--ticks N] [--sweep] [--trace]` → `harness/results/ai/` (git-ignored). |
| `tests/replay.tscn` | `ReplayMain.cs` | `-- --replay <file.yfr\|folder\|user://replays>`: OK/FAIL per file, exit 0/1. |
| `tests/captures/*_capture.tscn` | `*Capture.cs` | Windowed screenshot tools (hud, overlay, model, stage, oni_kappa, copy_hud, twist, flow, map, menu, kit, start, reward, elder_reward (YOK-186: rare cancel-rule and rare-special cards), rest_stop, shop, practice, dev, title_continue, hitbox) → `docs/screenshots/current/`. |
| `tests/bone_probe.tscn` | `BoneProbe.cs` | Prints a rig's bones (`-- out=<f> meshy`). |

## Fixtures (`tests/fixtures/`)

Everything here is **TEST FIXTURE** data: made-up numbers for tests, never copied to `data/`. `moves/` (`test-jab`, `test-sweep`, `test-dodge`), `ryo-normals/` (C8 timings), `ryo-air-normals/` (E19 jump-ins), `throws/` (C4/E4 throw), `specials/` (Spirit Wave, Rising Talisman), `rewards/` (Foxfire, Will-o'-wisp, Fox's Patience), `profiles/` (Kitsune aggressive/patient on the fixture kit), `trials/` (Spirit Wave trial), `presets/`, `old_fighters/`. Shared accept/reject lists run by both C# and `tools/tests/test_validate_data.py`: `modifier-gate/cases.json`, `clip-retime-gate/cases.json`. Kit fallbacks load fixtures only while `data/moves/` has none of that kind; tests that touch the scene or pool load fixtures explicitly (`FightScene.FixtureSources`, `AbilityPool.LoadFixtures`).

## Test classes by folder

Each test class sits in `tests/<Area>/`, mirroring `scripts/`; the capture tools are in `tests/captures/`, and the entry scenes above stay at `tests/`. The runner finds classes by reflection, so a folder is only for people.

| Folder | Classes |
|---|---|
| `tests/Sim/` | `FightLoopTests` (clock, facing, walls, KO, determinism), `MoveSystemTests`, `KihonTests` (K3 hash-identical Kata/Kihon), `RyoKitTests`, `ThrowTests`, `SpecialTests`, `AirNormalTests`, `MeterBurstHitstopTests`, `WakeUpTests`, `PushBoxAftershockTests`, `ModifierEffectTests`, `ElderTwistTests`, `KitTests`, `TanukiKitTests`, `ContentPathsTests` |
| `tests/Sim/Input/` | `InputParserTests` |
| `tests/Sim/Run/` | `RewardDraftTests`, `ElderCancelRuleTests` (YOK-186: elder cancel-rule offer, hold, continue, R8 provisional), `DraftFromBeatenYokaiTests`, `RunMapTests`, `RunFlowTests`, `CoinTests`, `MerchantTests`, `MerchantShopTests` + `MerchantShopSaveTests`, `ShopShelfTests`, `DojoTests`, `DojoTrialTests`, `TrialTests`, `CarryOverTests`, `UnlockTests`, `UnlockEffectTests`, `CopyRuleTests`, `CopyLockTests`, `RunSavePaidNodeTests`, `PracticeTests` |
| `tests/Ai/` | `AiTests` (P6: identical P1 streams → identical AI output until t + reaction), `AiMeterTests`, `AiGuardTests`, `AiGuardHitstopTests`, `StandingHabitTests`, `PerYokaiProfileTests`, `TanukiPressureTests`, `ApprenticeDamageTests` |
| `tests/Harness/` | `RunHarnessTests`, `BotLevelsTests`, `BotIdleGuardTests`, `MerchantShopBotTests`, `HitLogTests`, `PunishWindowTests`, `HarnessTwistTests` |
| `tests/Tutorial/` | `TutorialTests` |
| `tests/Replay/` | `ReplayTests` |
| `tests/SaveGame/`, `tests/Settings/` | `SaveTests`; `SettingsTests` |
| `tests/Flow/` | `BootFlowTests`, `DemoFlowTests`, `EndScreenTitleTests`, `PauseFlowTests` |
| `tests/Fight/` | `ModelPresentationTests`, `ClipRetimeTests`, `HipScaleTests`, `OniKappaBodyTests`, `TripoRyoTests`, `ModelExtentTests`, `CameraTests`, `DynamicCameraTests`, `TallestFighterCameraTests`, `StageTests`, `StageContainerTests`, `EditableSceneTests`, `EditorPreviewTests`, `DebugOverlayTests`, `HitSparkTests`, `FightHudTests`, `TextCutTests`, `YokaiNameTests` |
| `tests/Ui/` | `RewardScreenTests`, `MapScreenTests`, `MapCopyChipTests`, `RestStopTests`, `RestStopDojoPickerTests`, `ForecastCostTests`, `ShopScreenTests` (+ `ShopScreenFixtures`), `StoryScreenTests`, `TwistUiTests`, `AbilityArtTests`, `AbilityIconScreensTests`, `MenuMockupTests`, `MenuInputPathTests`, `PauseLayoutTests`, `SettingsLayoutTests`, `StartScreenTests`, `BootScreenLayoutTests`, `KitScreenTests` |
| `tests/Story/` | `StoryTests` |
| `tests/Dev/` | `DevModeTests`, `DevMapTests`, `DevJumpTests`, `DevRunStateTests` |
