namespace YokaiFighters.Sim;

/// <summary>
/// Kihon (K2): six buttons + the Special button. On a tick where Special goes down, the direction held
/// (relative to facing, so it flips with sides) picks the slot: neutral = A, forward = B, back = C,
/// down = D. No motions, no precision bonus (100% damage). Attack presses without Special are normals,
/// exactly as in Kata, and every other rule (throws, burst, EX upgrade, cancels, slot resolution) lives
/// in Match and reads the same buffer for both schemes (K3).
/// <para><b>Designer proposal</b> (diagonals, not in K2): down-forward and down-back count as down (D);
/// up, up-forward and up-back count as neutral, forward and back (the vertical part is ignored, and the
/// special wins over the jump).</para>
/// <para><b>Designer proposal</b> (EX, E16 for Kihon): Special + direction + one extra punch or kick, on the
/// same tick or up to ExPressWindow - 1 (2) ticks after Special; the special turns EX in place. The
/// Special bit stays in Pressed, and <see cref="Match.ExPair"/> reads Special + any attack as EX.</para>
/// </summary>
public sealed class KihonParser : ICommandParser
{
	readonly InputConfig _cfg;
	public KihonParser(InputConfig cfg) { _cfg = cfg; }

	/// <summary>K2 slot for a numpad direction relative to facing (diagonals: designer proposal above).</summary>
	public static SpecialSlot SlotOf(int numpad) => numpad switch
	{
		1 or 2 or 3 => SpecialSlot.D,
		6 or 9 => SpecialSlot.B,
		4 or 7 => SpecialSlot.C,
		_ => SpecialSlot.A, // 5 and 8
	};

	public InputCommand Parse(InputBuffer buffer, int facing)
	{
		InputBits down = buffer.PressedAt(0);
		InputBits attacks = down & InputBits.Attacks;
		int dir = buffer.Dir(0, facing);
		if ((down & InputBits.Special) != 0)
		{
			// Button is the extra attack, if any: it gives that normal when the slot is empty.
			return new InputCommand(CommandKind.Special, SlotOf(dir), Motion.None,
				KataParser.PickButton(attacks, _cfg), attacks | InputBits.Special, dir, false);
		}
		if (attacks == InputBits.None) return InputCommand.None;
		return new InputCommand(CommandKind.Normal, SpecialSlot.None, Motion.None,
			KataParser.PickButton(attacks, _cfg), attacks, dir, false);
	}
}
