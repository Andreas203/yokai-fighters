namespace YokaiFighters.Sim;

/// <summary>
/// Kata (K1): six buttons + motions. On a tick where an attack button goes down, the motion
/// completed most recently in the MotionWindow (ties: MotionPriority) makes it a special in its slot;
/// otherwise it is a normal with the current direction. The Kihon Special button is ignored.
/// </summary>
public sealed class KataParser : ICommandParser
{
	readonly InputConfig _cfg;
	public KataParser(InputConfig cfg) { _cfg = cfg; }

	public static SpecialSlot SlotOf(Motion m) => m switch
	{
		Motion.Qcf236 => SpecialSlot.A,
		Motion.Dp623 => SpecialSlot.B,
		Motion.Qcb214 => SpecialSlot.C,
		Motion.DownDown22 => SpecialSlot.D,
		_ => SpecialSlot.None,
	};

	public static InputBits PickButton(InputBits pressed, InputConfig cfg)
	{
		foreach (var b in cfg.ButtonPriority)
			if ((pressed & b) != 0) return b;
		return InputBits.None;
	}

	/// <summary>The motion completed most recently in the MotionWindow (MotionPriority breaks same-tick ties), or None.
	/// Shared with <see cref="KihonParser"/>, which also accepts motions (E18).</summary>
	public static Motion BestMotion(InputBuffer buffer, int facing, InputConfig cfg)
	{
		Motion best = Motion.None;
		int bestAge = int.MaxValue;
		foreach (var m in cfg.MotionPriority)
		{
			int age = MotionParser.FinalStepAge(buffer, m, facing, cfg);
			if (age >= 0 && age < bestAge) { best = m; bestAge = age; }
		}
		return best;
	}

	public InputCommand Parse(InputBuffer buffer, int facing)
	{
		InputBits pressed = buffer.PressedAt(0) & InputBits.Attacks;
		if (pressed == InputBits.None) return InputCommand.None;
		InputBits button = PickButton(pressed, _cfg);
		int dir = buffer.Dir(0, facing);
		Motion best = BestMotion(buffer, facing, _cfg);
		if (best != Motion.None)
			return new InputCommand(CommandKind.Special, SlotOf(best), best, button, pressed, dir, true);
		return new InputCommand(CommandKind.Normal, SpecialSlot.None, Motion.None, button, pressed, dir, false);
	}
}
