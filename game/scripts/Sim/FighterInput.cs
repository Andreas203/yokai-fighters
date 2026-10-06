using System;

namespace YokaiFighters.Sim;

/// <summary>
/// One tick of one fighter's input, already resolved to game intents. Directions are absolute
/// screen directions (Left/Right), not forward/back; the sim maps them using facing.
/// The input parser (YOK-17) produces these for Kata and Kihon alike (K3); the AI produces them
/// from on-screen state only (P6). Inputs per tick are the whole replay.
/// </summary>
[Flags]
public enum InputBits : ushort
{
	None = 0,
	Left = 1 << 0,
	Right = 1 << 1,
	Up = 1 << 2,
	Down = 1 << 3,
	// Six attack buttons (K1/K2) and Kihon's Special button (K2). Bit values skip 8/9 (debug).
	LightPunch = 1 << 4,
	MediumPunch = 1 << 5,
	HeavyPunch = 1 << 6,
	LightKick = 1 << 7,
	MediumKick = 1 << 10,
	HeavyKick = 1 << 11,
	Special = 1 << 12,
	Directions = Left | Right | Up | Down,
	Attacks = LightPunch | MediumPunch | HeavyPunch | LightKick | MediumKick | HeavyKick,
	// Debug-only placeholders until real moves exist (YOK-16/18).
	DebugStrike = 1 << 8,
	DebugCrossUp = 1 << 9,
}

/// <param name="Move">Move request this tick: 0 = none, n = the fighter's move slot n-1. The parser
/// (YOK-17) resolves buttons and motions to a slot and sends it on the tick it's recognised; the sim
/// starts it only if the fighter can act.</param>
public readonly record struct FighterInput(InputBits Bits, byte Move = 0)
{
	public static readonly FighterInput None = new(InputBits.None);
	public static FighterInput Attack(int slot, InputBits bits = InputBits.None) => new(bits, (byte)(slot + 1));

	/// <summary>YOK-21: Move values from here up request the special in slot A-D (201-204), EX 205-208.</summary>
	public const int SpecialRequestBase = 200;
	/// <summary>Requests the special loaded in a slot directly (tests, AI): no motion, so no Kata precision bonus.</summary>
	public static FighterInput Special(SpecialSlot slot, bool ex = false, InputBits bits = InputBits.None) =>
		new(bits, (byte)(SpecialRequestBase + (int)slot + (ex ? 4 : 0)));
	public bool IsSpecialRequest(out SpecialSlot slot, out bool ex)
	{
		int n = Move - SpecialRequestBase;
		ex = n > 4;
		slot = n is >= 1 and <= 8 ? (SpecialSlot)(ex ? n - 4 : n) : SpecialSlot.None;
		return slot != SpecialSlot.None;
	}
	public bool Has(InputBits b) => (Bits & b) == b;
	public FighterInput With(InputBits b) => new(Bits | b);
}
