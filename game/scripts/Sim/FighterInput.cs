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
	// Debug-only placeholders until real moves exist (YOK-16/18).
	DebugStrike = 1 << 8,
	DebugCrossUp = 1 << 9,
}

public readonly record struct FighterInput(InputBits Bits)
{
	public static readonly FighterInput None = new(InputBits.None);
	public bool Has(InputBits b) => (Bits & b) == b;
}
