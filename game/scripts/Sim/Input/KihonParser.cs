namespace YokaiFighters.Sim;

/// <summary>
/// Kihon (K2, E18): six buttons + the Special button. On a tick where Special goes down, the direction held
/// (relative to facing, so it flips with sides) picks the slot: neutral = A, forward = B, back = C,
/// down = D; down-forward and down-back = D; up = A, up-forward = B, up-back = C (Special beats the jump).
/// No precision bonus (100% damage).
/// <para>K1 motions also work in Kihon (E18): a completed motion + attack fires that slot's special exactly as
/// in Kata (same motion rules, same two-punch/two-kick EX) but with Precision false, so 100% damage.
/// Priority: if Special goes down on the same tick, Special + direction wins and the motion is ignored.</para>
/// <para>Kihon EX (E18): Special + direction + one extra punch or kick, on the same tick or up to
/// ExPressWindow - 1 (2) ticks after Special; the special turns EX in place. The Special bit stays in Pressed,
/// and <see cref="Match.ExPair"/> reads Special + any attack as EX. An attack pressed before Special gives
/// its normal (the normal is already out, or the special replaces it in the command buffer).</para>
/// Every other rule (throws, burst, cancels, slot resolution) lives in Match and reads the same buffer
/// for both schemes (K3).
/// </summary>
public sealed class KihonParser : ICommandParser
{
	readonly InputConfig _cfg;
	public KihonParser(InputConfig cfg) { _cfg = cfg; }

	/// <summary>E18 slot for a numpad direction relative to facing.</summary>
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
			// Special + direction beats a motion completed on the same tick (E18).
			// Button is the extra attack, if any: it gives that normal when the slot is empty.
			return new InputCommand(CommandKind.Special, SlotOf(dir), Motion.None,
				KataParser.PickButton(attacks, _cfg), attacks | InputBits.Special, dir, false);
		}
		if (attacks == InputBits.None) return InputCommand.None;
		InputBits button = KataParser.PickButton(attacks, _cfg);
		Motion motion = KataParser.BestMotion(buffer, facing, _cfg);
		if (motion != Motion.None) // K1 motion in Kihon: same special as Kata, no precision bonus (E18)
			return new InputCommand(CommandKind.Special, KataParser.SlotOf(motion), motion, button, attacks, dir, false);
		return new InputCommand(CommandKind.Normal, SpecialSlot.None, Motion.None, button, attacks, dir, false);
	}
}
