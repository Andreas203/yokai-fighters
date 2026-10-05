using System;
using System.Collections.Generic;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-17: input buffer + Kata motion parser, driven by recorded input sequences.
/// Recording format: space-separated ticks, each a numpad direction relative to the recording
/// facing, optional "+LP" style buttons, optional "*N" repeat. E.g. "5*3 2 3 6+LP".
/// </summary>
public static class InputParserTests
{
	static readonly InputConfig Cfg = InputConfig.Default;

	static readonly Dictionary<string, InputBits> Buttons = new()
	{
		["LP"] = InputBits.LightPunch, ["MP"] = InputBits.MediumPunch, ["HP"] = InputBits.HeavyPunch,
		["LK"] = InputBits.LightKick, ["MK"] = InputBits.MediumKick, ["HK"] = InputBits.HeavyKick,
		["S"] = InputBits.Special,
	};

	/// <summary>Turns a recording into absolute per-tick inputs, as a replay would store them.</summary>
	public static List<FighterInput> Record(string seq, int facing = 1)
	{
		var ticks = new List<FighterInput>();
		foreach (var raw in seq.Split(' ', StringSplitOptions.RemoveEmptyEntries))
		{
			string tok = raw;
			int repeat = 1;
			int star = tok.IndexOf('*');
			if (star >= 0) { repeat = int.Parse(tok[(star + 1)..]); tok = tok[..star]; }
			var parts = tok.Split('+');
			InputBits b = Numpad.ToBits(int.Parse(parts[0]), facing);
			for (int i = 1; i < parts.Length; i++) b |= Buttons[parts[i]];
			for (int i = 0; i < repeat; i++) ticks.Add(new FighterInput(b));
		}
		return ticks;
	}

	/// <summary>Plays a recording through a fresh reader; returns the command parsed on the last tick.</summary>
	static InputCommand Last(string seq, int recFacing = 1, int playFacing = 0, InputConfig? cfg = null)
	{
		var r = new InputReader(cfg ?? Cfg);
		foreach (var t in Record(seq, recFacing)) r.Update(t, playFacing == 0 ? recFacing : playFacing);
		return r.Latest;
	}

	static void IsSpecial(string seq, SpecialSlot slot, string msg, int recFacing = 1, int playFacing = 0)
	{
		var c = Last(seq, recFacing, playFacing);
		Assert.Equal(CommandKind.Special, c.Kind, $"{msg} kind [{seq}]");
		Assert.Equal(slot, c.Slot, $"{msg} slot [{seq}]");
		Assert.True(c.Precision, $"{msg}: Kata motion carries the precision bonus (K1)");
	}

	static void IsNormal(string seq, string msg)
	{
		var c = Last(seq);
		Assert.Equal(CommandKind.Normal, c.Kind, $"{msg} [{seq}]");
	}

	// --- Each K1 motion recognised, both facings ------------------------------------------

	[Test]
	public static void Motions_EachK1MotionRecognised_BothFacings()
	{
		foreach (int f in new[] { 1, -1 })
		{
			IsSpecial("5 2 3 6+LP", SpecialSlot.A, $"236 facing {f}", f);
			IsSpecial("5 6 2 3+HP", SpecialSlot.B, $"623 facing {f}", f);
			IsSpecial("5 2 1 4+MK", SpecialSlot.C, $"214 facing {f}", f);
			IsSpecial("5 2 5 2+LK", SpecialSlot.D, $"22 facing {f}", f);
		}
	}

	[Test]
	public static void Motions_HeldStepsAndRealisticSpeed()
	{
		// Keyboard-speed motions with each direction held a few ticks.
		IsSpecial("5*4 2*3 3*3 6 6+MP", SpecialSlot.A, "slow 236");
		IsSpecial("6*3 5 2*3 3 3+HP", SpecialSlot.B, "623 through neutral");
		IsSpecial("2*4 5*3 2 2+LK", SpecialSlot.D, "slow 22");
	}

	// --- Buffer window ----------------------------------------------------------------------

	[Test]
	public static void Window_MotionAtEdgeAcceptedOneTickLateRejected()
	{
		int w = Cfg.MotionWindow;
		// First step exactly MotionWindow ticks before and including the press tick.
		IsSpecial($"5 2 3*{w - 2} 6+LP", SpecialSlot.A, "236 spanning the full window");
		IsNormal($"5 2 3*{w - 1} 6+LP", "236 one tick too slow is a normal");
		IsSpecial($"5 2 5 2*{w - 3} 2+LK", SpecialSlot.D, "22 spanning the window");
		IsNormal($"5 2 5 2*{w - 2} 2+LK", "22 too slow");
		// Lenient tail: neutral between the motion and the press still counts inside the window.
		IsSpecial("2 3 6 5 5 5+LP", SpecialSlot.A, "late button after returning to neutral");
	}

	[Test]
	public static void Window_IncompleteOrWrongOrderIsNormal()
	{
		IsNormal("5 2 6+LP", "236 without the diagonal");
		IsNormal("6 3 2+LP", "reversed 236");
		IsNormal("2*9 2+LK", "held down is not 22");
		Assert.Equal(CommandKind.None, Last("5 2 3 6 6").Kind, "no button, no command");
	}

	// --- Facing ------------------------------------------------------------------------------

	[Test]
	public static void Facing_MotionsFlipWhenSidesSwitch()
	{
		// Same physical stick motion (recorded facing right) read after sides switch: 236 <-> 214.
		IsSpecial("5 2 3 6+LP", SpecialSlot.C, "right-facing 236 read facing left", 1, -1);
		IsSpecial("5 2 1 4+LP", SpecialSlot.A, "right-facing 214 read facing left", 1, -1);
		// 623 flips to 421, which is no K1 motion.
		Assert.Equal(CommandKind.Normal, Last("5 6 2 3+LP", 1, -1).Kind, "623 mirrored is not a DP");
		IsSpecial("5 2 5 2+LP", SpecialSlot.D, "22 is facing independent", 1, -1);
	}

	[Test]
	public static void Facing_SwitchMidMotionUsesFacingAtPress()
	{
		// Player starts 236 facing right, opponent crosses up, player finishes with
		// absolute down-left, left (forward from the new side): read with the new facing it's 236.
		var r = new InputReader(Cfg);
		foreach (var t in Record("5 2", 1)) r.Update(t, 1);
		foreach (var t in Record("3 6+LP", -1)) r.Update(t, -1);
		Assert.Equal(SpecialSlot.A, r.Latest.Slot, "236 relative to facing at the press");
	}

	// --- Priority ----------------------------------------------------------------------------

	[Test]
	public static void Priority_ButtonsAndMotions()
	{
		var c = Last("5 5+LP+HP+MK");
		Assert.Equal(InputBits.HeavyPunch, c.Button, "heavy beats light/medium");
		Assert.Equal(InputBits.LightPunch | InputBits.HeavyPunch | InputBits.MediumKick, c.Pressed, "full mask kept");
		Assert.Equal(InputBits.HeavyKick, Last("5+MP+HK").Button, "heavy kick beats medium punch");
		Assert.Equal(InputBits.LightPunch, Last("5+LP+LK").Button, "punch beats kick at same strength");
		// Several motions in the window: the one completed most recently wins ...
		IsSpecial("2 3 6 2 3+LP", SpecialSlot.B, "236 then 623: DP is newer");
		IsSpecial("6 2 3 6+LP", SpecialSlot.A, "walk forward then 236 is a QCF, not a DP");
		IsSpecial("2 3 6 5 5 2 1 4+MP", SpecialSlot.C, "fresh 214 beats an older 236 in the window");
		// ... MotionPriority only breaks exact ties.
		IsSpecial("6 2 3 6 3+LP", SpecialSlot.B, "6-2-3 / 2-3-6-3 DP completes on the press tick, beats the 236 a tick earlier");
		// Holding a button does not re-trigger; only the press edge parses.
		var r = new InputReader(Cfg);
		foreach (var t in Record("5+LP 2+LP 3+LP 6+LP")) r.Update(t, 1);
		Assert.Equal(CommandKind.None, r.Latest.Kind, "held button: no new command");
		// Special button alone is not a Kata command.
		Assert.Equal(CommandKind.None, Last("2 3 6+S").Kind, "Kata ignores the Special button");
	}

	[Test]
	public static void Lenience_DiagonalsAndRolls()
	{
		IsSpecial("1 2 3 6+LP", SpecialSlot.A, "236 started from down-back");
		IsSpecial("6 1 3+LP", SpecialSlot.B, "623 rolled through down-back");
		IsSpecial("6 3 2 3+LP", SpecialSlot.B, "623 via 6-3-2-3");
		IsSpecial("1 4 3+LK", SpecialSlot.D, "22 via down-back, release, down-forward");
	}

	// --- Charge motions ---------------------------------------------------------------------

	[Test]
	public static void Charge_BackForwardAndDownUp()
	{
		int n = Cfg.ChargeTicks;
		var full = new InputReader(Cfg);
		foreach (var t in Record($"4*{n} 6+LP")) full.Update(t, 1);
		Assert.True(MotionParser.Matches(full.Buffer, Motion.ChargeBackForward, 1, Cfg), "[4]6 charged");
		var shortC = new InputReader(Cfg);
		foreach (var t in Record($"4*{n - 1} 6+LP")) shortC.Update(t, 1);
		Assert.True(!MotionParser.Matches(shortC.Buffer, Motion.ChargeBackForward, 1, Cfg), "one tick short");
		var grace = new InputReader(Cfg);
		foreach (var t in Record($"1*{n} 5*{Cfg.ChargeGrace} 6+LP")) grace.Update(t, 1);
		Assert.True(MotionParser.Matches(grace.Buffer, Motion.ChargeBackForward, 1, Cfg), "down-back charges, grace held");
		var late = new InputReader(Cfg);
		foreach (var t in Record($"4*{n} 5*{Cfg.ChargeGrace + 1} 6+LP")) late.Update(t, 1);
		Assert.True(!MotionParser.Matches(late.Buffer, Motion.ChargeBackForward, 1, Cfg), "grace exceeded");
		var du = new InputReader(Cfg);
		foreach (var t in Record($"2*{n} 8+HP")) du.Update(t, 1);
		Assert.True(MotionParser.Matches(du.Buffer, Motion.ChargeDownUp, 1, Cfg), "[2]8 charged");
		// Charge flips with facing: back-charge recorded facing right is forward facing left.
		Assert.True(!MotionParser.Matches(full.Buffer, Motion.ChargeBackForward, -1, Cfg), "charge flips with sides");
		// Charge is not in Kata's slot priority, so it parses as a normal.
		Assert.Equal(CommandKind.Normal, full.Latest.Kind, "charge alone is no K1 special");
	}

	[Test]
	public static void Buffer_HeldTicksForHoldButtons()
	{
		var r = new InputReader(Cfg);
		foreach (var t in Record("5 5+HP*30")) r.Update(t, 1);
		Assert.Equal(30, r.Buffer.HeldTicks(InputBits.HeavyPunch), "hold duration (Sumo Stance)");
	}

	// --- Command buffer ---------------------------------------------------------------------

	[Test]
	public static void CommandBuffer_WaitsThenExpiresAndConsumesOnce()
	{
		int n = Cfg.CommandBuffer;
		var r = new InputReader(Cfg);
		foreach (var t in Record($"2 3 6+LP 5*{n}")) r.Update(t, 1);
		Assert.Equal(SpecialSlot.A, r.Pending.Slot, "still waiting after CommandBuffer ticks");
		Assert.True(r.TryConsume(out var c) && c.Slot == SpecialSlot.A, "consumed");
		Assert.True(!r.TryConsume(out _), "consumed only once");
		var e = new InputReader(Cfg);
		foreach (var t in Record($"2 3 6+LP 5*{n + 1}")) e.Update(t, 1);
		Assert.Equal(CommandKind.None, e.Pending.Kind, "expired one tick later");
		// A normal pressed right after a special does not overwrite it; a newer special does.
		var k = new InputReader(Cfg);
		foreach (var t in Record("2 3 6+LP 5 5+LK")) k.Update(t, 1);
		Assert.Equal(SpecialSlot.A, k.Pending.Slot, "normal does not replace waiting special");
		foreach (var t in Record("2 1 4+MP")) k.Update(t, 1);
		Assert.Equal(SpecialSlot.C, k.Pending.Slot, "newer special replaces it");
	}

	// --- Determinism, SOCD, directions --------------------------------------------------------

	[Test]
	public static void Numpad_SocdAndRoundTrip()
	{
		Assert.Equal(5, Numpad.From(InputBits.Left | InputBits.Right, 1), "left+right = neutral");
		Assert.Equal(2, Numpad.From(InputBits.Left | InputBits.Right | InputBits.Down, 1), "SOCD keeps down");
		Assert.Equal(5, Numpad.From(InputBits.Up | InputBits.Down, -1), "up+down = neutral");
		for (int n = 1; n <= 9; n++)
		foreach (int f in new[] { 1, -1 })
			Assert.Equal(n, Numpad.From(Numpad.ToBits(n, f), f), $"round trip {n} facing {f}");
		Assert.Equal(InputBits.Left, Numpad.ToBits(6, -1), "forward facing left is screen-left");
		Assert.Equal(2, Last("5 2+MK").Direction, "normal carries its direction (crouching)");
	}

	[Test]
	public static void Determinism_RandomInputsParseIdentically()
	{
		static ulong Run(uint seed)
		{
			var rng = new SimRng(seed);
			var r = new InputReader(Cfg);
			ulong h = 1469598103934665603UL;
			for (int i = 0; i < 20_000; i++)
			{
				var b = (InputBits)(rng.Next(1 << 13)) & (InputBits.Directions | InputBits.Attacks | InputBits.Special);
				int facing = (i / 300) % 2 == 0 ? 1 : -1;
				r.Update(new FighterInput(b), facing);
				var c = r.Latest;
				h = (h ^ (ulong)(((int)c.Kind << 24) | ((int)c.Slot << 16) | (int)c.Button)) * 1099511628211UL;
			}
			return h;
		}
		Assert.Equal(Run(7), Run(7), "same inputs, same commands");
	}

	// --- Device mapping -----------------------------------------------------------------------

	[Test]
	public static void Devices_KeyboardAndPadMapToFighterInput()
	{
		var held = new HashSet<Key> { Key.S, Key.D, Key.U, Key.Space };
		var b = InputDevices.FromKeys(InputDevices.P1Keys, held.Contains);
		Assert.Equal(InputBits.Down | InputBits.Right | InputBits.LightPunch | InputBits.Special, b, "P1 keyboard");
		var p2 = InputDevices.FromKeys(InputDevices.P2Keys, k => k == Key.Left || k == Key.Kp3);
		Assert.Equal(InputBits.Left | InputBits.HeavyKick, p2, "P2 keyboard");
		var pad = InputDevices.FromPad(j => j == JoyButton.Y || j == JoyButton.LeftShoulder, 0.8f, 0.9f, 0.6f);
		Assert.Equal(InputBits.Right | InputBits.Down | InputBits.MediumPunch | InputBits.HeavyKick | InputBits.Special,
			pad, "pad stick down-right, Y, RT, LB");
		Assert.Equal(InputBits.None, InputDevices.FromPad(_ => false, 0.3f, -0.4f, 0.2f), "inside deadzones");
	}
}
