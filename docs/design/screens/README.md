# Screen designs

The target look for every screen: one 16:9 concept per screen (1672 x 941). The in-game screens are rebuilt in Godot to match these. They are layout and style targets, not textures: text, bars and buttons stay live controls.

| Concept | Screen | In the game today |
|---|---|---|
| `title.png` | Title menu | Mockup only (`title_menu.tscn`), not in the boot |
| `control-select.png` | Kata / Kihon choice before the first fight | `start_screen.tscn`, different layout |
| `hud.png` | Fight HUD | `hud.tscn` |
| `pause.png` | Pause menu | Mockup only (`pause_menu.tscn`) |
| `pause-no-characters.png` | Earlier pause concept without the fight behind it or the move icons; superseded by `pause.png` | n/a |
| `pause-confirm.png` | Restart confirmation over the pause menu | Mockup only |
| `settings.png` | Settings, sound tab | Mockup only (`settings_panel.tscn`) |
| `reward.png` | Three reward cards | `reward_screen.tscn`, `reward_card.tscn` |
| `modifier-target.png` | Pick the special a modifier attaches to | `target_card.tscn` |
| `story-card.png` | Story card (binding line) | `story_card.tscn` |
| `defeat.png` | Lose screen | `lose_screen.tscn` |
| `demo-complete.png` | Demo complete | `demo_complete.tscn` |
| `map.png` | Run map (YOK-109; brief `astra-brief-map-hud-twist.md`) | `map_screen.tscn`, built to the rough |
| `hud-copy-target.png` | Fight HUD in the Tanuki fight, locked copy talisman (AM6) | `hud.tscn` |
| `hud-copy-target-states.png` | Copy-target talisman, states A–F | `CopyTalisman` |
| `elder-twist-card.png` | Elder-twist intro card and first-meeting callout | `story_card.tscn` via `FightScene.Flow.cs` |

No concept image exists for the move list or the settings controls tab; their prompts are in `prompts/prompts-movelist-controls.json`.

`prompts/` holds the prompts each image was generated from. Screenshots of what the game looks like now are in `docs/screenshots/current/`.
