# Sim/Input: input parsing

`game/scripts/Sim/Input/`, namespace `YokaiFighters.Sim`. Pure C#, deterministic, shared by Kata and Kihon (K3): only the `ICommandParser` differs, chosen by `InputReader.Scheme`. Throws (E12) and burst (E13) read the raw buffer, not the parser. EX (E16) is `Match.ExPair`: two punches or two kicks for motions; Kihon `Special` + any one attack (E18), same tick or up to 2 ticks after. Per fighter: `InputReader.Update(raw, facing)` once per tick, then the move system calls `TryConsume(out InputCommand)`. Index: [README.md](README.md).

| File | What it is and its public surface |
|---|---|
| `Sim/Input/InputConfig.cs` | Timing in ticks (E7): `MotionWindow` 16, `CommandBuffer` 5, `ChargeTicks` 40, `ChargeGrace` 8, `DashWindow` 12 (proposed), `HistoryTicks` 128; `MotionPriority` (623 > 236 > 214 > 22); `ButtonPriority` (heavy > medium > light, punch > kick). |
| `Sim/Input/Numpad.cs` | Absolute bits ↔ numpad direction relative to facing (6 = toward the opponent). SOCD: left+right and up+down cancel. |
| `Sim/Input/InputBuffer.cs` | Ring buffer of raw `InputBits` (age 0 = now): `Dir(age, facing)`, `PressedAt(age)`, `HeldTicks(bits)`. |
| `Sim/Input/MotionParser.cs` | `Motion` enum (236, 623, 214, 22 = K1 slots A–D; charge `[4]6` / `[2]8` in no slot), `Matches` / `FinalStepAge`: backwards greedy match in the window, facing read at the press. 623 accepts 1 for its down. |
| `Sim/Input/InputCommand.cs` | `InputCommand` (Kind None/Normal/Special, `SpecialSlot`, Motion, Button, `Pressed` mask, Direction, `Precision` = Kata +10%, K1) and `ICommandParser`. |
| `Sim/Input/KataParser.cs` | Kata (K1): on an attack press, the latest completed K1 motion in the window → Special in its slot (ties by `MotionPriority`), else a Normal. Ignores `Special`. `BestMotion` is public static, shared with Kihon. |
| `Sim/Input/KihonParser.cs` | Kihon (K2): on a `Special` press, direction → slot (5 = A, 6 = B, 4 = C, 2 = D; E18 diagonals 1/3 → D, 8 → A, 9 → B, 7 → C), `Precision` false. Attack presses without `Special` parse as Kata but with `Precision` false (E18). Special + direction beats a same-tick motion. |
| `Sim/Input/ControlScheme.cs` | `ControlScheme { Kata, Kihon }`, per player via `InputReader.Scheme`. |
| `Sim/Input/InputReader.cs` | Per-fighter buffer + parser + command buffer. `Scheme` swaps only the parser (history and waiting command stay; not hashed). A normal never replaces a waiting special; a newer special does. `Dash` (+1 = 66, -1 = 44). Owned by `Fighter.Input`. |
