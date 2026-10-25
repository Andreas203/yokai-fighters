# Project: Godot project, scenes, content paths, export

`game/`: Godot 4.7 .NET, C# (`YokaiFighters.csproj`, net10.0). Build and test with the 4.7.2 mono build. Main scene `scenes/boot.tscn` (`Flow/GameFlow.cs`); `scenes/fight.tscn` is the fight on its own. Index: [README.md](README.md).

## Content paths and export

| File | What it is and its public surface |
|---|---|
| `ContentPaths.cs` | Where System.IO-read JSON lives. Pure `Resolve(exported, exePath, resDir)`: editor/tests = repo root (`data/`, `game/tests/fixtures/`); exported build (feature `template`) = the exe's folder (`data/`, `fixtures/`). `ContentPaths.Data(sub)`, `FixturesDir`. Clip GLBs are `ResourceLoader.Exists` (in the PCK). Tests `ContentPathsTests`. |

Non-code: `export_presets.cfg` ("Windows Desktop", x86_64, embedded PCK, `tests/*` excluded, output `../build/windows/YokaiFighters.exe`); `tools/export-windows.sh` and `.github/workflows/release.yml` ([tools.md](tools.md)).

## Scenes the code relies on

| Scene | Holds | Script |
|---|---|---|
| `fight.tscn` | `StageContainer` (runtime `Stage`), `CameraRig`, `Fighters` (runtime `P1`, `P2`) + `P1Spawn`/`P2Spawn` markers, `EditorPreview`, UI children (HUD, flow screens, `PauseLayer/Pause`) | `Fight/FightScene.cs` |
| `stage/bamboo_grove.tscn` (default) | `Environment`, `Sun`/`Fill`, `Backdrop` (z −16), `Floor` (`shaders/toon_floor.gdshader`), `Props` (`Bamboo0-9`: holder → `Pivot` → `Model`; `Lantern0-1`: `stage_props/lit_lantern.tscn` instances; `PathLanterns`: `LanternLine.cs`), `PropToon` | `Fight/Presentation/FightStage.cs` |
| `stage_props/lit_lantern.tscn` | A lit stone lantern: `Pivot` → `Model` (lantern.glb), `GlowX`/`GlowZ` (flat unshaded #D8632C window insert, group `flat_fill`, fog off), `Light` (warm OmniLight3D, range 3 m, no shadows). Outside `stage/` so no table can name it as a stage | none (YOK-208) |
| `stage/placeholder.tscn` | Flat floor fallback (missing grove art or `--stage=placeholder`) | `FightStage.cs` |
| `camera_rig.tscn` | `Camera` (Camera3D, FOV 25); the rig is placed every frame from `DynamicCamera.Transform` | none |
| `fighters/BaseFighter.tscn` | Root `FighterModel`, `Mirror` (Scale.X = facing) → `Yaw` (+90°: rig +Z → world +X) → `Model` in variants; `Animator` (AnimationPlayer, **Manual**); `Toon`. Variants must not override these (`EditableSceneTests`) | `Fight/Presentation/FighterModel.cs` |
| `fighters/ryo.tscn`, `ryo-tripo.tscn`, `kitsune.tscn`, `oni.tscn`, `kappa.tscn` | Inherited scenes adding only `Mirror/Yaw/Model` and extras (Kitsune `Tails` → `Tail0-8` → `Fan`; Oni `Club`; Kappa `Shell`) | `FighterModel.cs` |
| `boot.tscn` | `GameFlow`, `Menus` CanvasLayer 30 (`Title`, `ControlSelect`) | `Flow/GameFlow.cs` |
| `ui/*.tscn` | One scene per screen (hud, lose_screen, start_screen, story_card, reward_screen, reward_card, target_card, demo_complete, map_screen, map_node, copy_chip, rest_stop, shop, title_menu, pause_menu, move_list_panel, move_slot_card, settings_panel, menu_mockups, debug_menu, debug_overlay, dev_menu) and `ui/kit/` pieces | see [ui.md](ui.md), [fight.md](fight.md) |
| `dev/dev_shell.tscn` | Autoload `DevShell` | `Dev/DevShell.cs` |

## Editing notes

- Run `dotnet build` in `game/` before opening the editor so the `[Tool]` scripts (`TalismanPanel`, `BrushStroke`, `SealStamp`, `ToonLook`, `FightEditorPreview`) work.
- UI scenes are `Control`s on a 1920×1080 canvas (`canvas_items` stretch to 1280×720). Styles live in one theme, `game/ui/talisman_theme.tres` (palette, button and bar styles, label type variations); minimum kit text 26 px on the design canvas.
- Code moves only what the sim drives (camera, fighter position/facing, animation frames, tail sway); everything placed or styled is in the scenes. Keep the node names listed above and the `UiFind` names each screen script looks up.
- Stage props: the clear band is a test (`StageTests`), not code: nothing nearer than z −8 within |x| < 10.5 m, lanterns at z ≤ −10, nothing in front of z −2.
- Every rig imports Y-up facing +Z; a rig that faces elsewhere is fixed in the rig pipeline, never by a per-variant `Yaw` or rotated `Model`. New body: New Inherited Scene from `BaseFighter.tscn`, set `Fighter`, add the GLB as `Model`, point `FighterAnimSet.ScenePath` at it.
- Keep `Animator` callback mode Manual (enforced in code): frames are stepped with `Seek` from the sim tick (F3).
- Screenshots: the `tests/captures/*_capture.tscn` tools write to `docs/screenshots/current/`.
