# Screen designs

The target look for every screen: one 16:9 concept per screen (1672 x 941). The in-game screens are rebuilt in Godot to match these. They are layout and style targets, not textures: text, bars and buttons stay live controls.

| Concept | Screen | In the game today |
|---|---|---|
| `title.png` | Title menu | Mockup only (`title_menu.tscn`), not in the boot |
| `control-select.png` | Kata / Kihon choice before the first fight | `start_screen.tscn`, different layout |
| `hud.png` | Fight HUD | `hud.tscn` |
| `pause.png` | Pause menu | Mockup only (`pause_menu.tscn`) |
| `pause-confirm.png` | Restart confirmation over the pause menu | Mockup only |
| `settings.png` | Settings, sound tab | Mockup only (`settings_panel.tscn`) |
| `reward.png` | Three reward cards | `reward_screen.tscn`, `reward_card.tscn` |
| `modifier-target.png` | Pick the special a modifier attaches to | `target_card.tscn` |
| `story-card.png` | Story card (binding line) | `story_card.tscn` |
| `defeat.png` | Lose screen | `lose_screen.tscn` |
| `demo-complete.png` | Demo complete | `demo_complete.tscn` |

No concept image exists for the move list or the settings controls tab; their prompts are in `prompts/prompts-movelist-controls.json`.

`prompts/` holds the prompts each image was generated from. Screenshots of what the game looks like now are in `docs/screenshots/current/`.
