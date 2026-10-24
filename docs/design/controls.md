# Controls

The default bindings as the game ships them (YOK-25). Source of truth is the code: `game/scripts/Fight/InputDevices.cs` for fights and `game/scripts/Ui/Menus/MenuInput.cs` for menus. If this page and the code disagree, the code wins and this page gets fixed. There is no rebinding yet.

## Schemes (K1, K2)

Both schemes use the same physical buttons. The scheme decides how the sim reads them, not which keys are pressed.

| Scheme | Specials | Damage |
|---|---|---|
| **Kihon** (default, never cut, K4) | Special button + direction: neutral = slot A, forward = B, back = C, down = D | 100% |
| **Kata** | Motions + a button: A ↓↘→, B →↓↘, C ↓↙←, D ↓↓ | +10% on motion-input specials |

The scheme is picked on the control-select screen or switched in a menu with Q / pad X. It is saved in `user://settings.json` (`SettingsData.Scheme`) and applies to every duel of the next run, restarts included.

## Fight

| Input | Keyboard P1 | Keyboard P2 | Pad (Xbox names; pad 0 = P1, pad 1 = P2) |
|---|---|---|---|
| Move | W A S D | Arrow keys | D-pad or left stick (deadzone 0.5) |
| Light punch | U | Numpad 4 | X |
| Medium punch | I | Numpad 5 | Y |
| Heavy punch | O | Numpad 6 | RB |
| Light kick | J | Numpad 1 | A |
| Medium kick | K | Numpad 2 | B |
| Heavy kick | L | Numpad 3 | RT (past 0.5) |
| Special (Kihon) | Space | Numpad 0 | LB |

Keyboard and pad work together: a player's keys and their pad are combined.

## Menus

| Action | Keyboard | Pad |
|---|---|---|
| Move focus | Arrow keys | D-pad or left stick |
| Confirm | Enter, Space | A |
| Back | Esc | B |
| Pause (in a fight) | Esc | Start |
| Frame data toggle | Tab | Y |
| Switch scheme | Q | X |
| Switch tab | Q / E | LB / RB |
