using System;

namespace YokaiFighters.Sim;

/// <summary>Motions the parser recognises (numpad notation, relative to facing).</summary>
public enum Motion : byte
{
	None,
	Qcf236,      // K1 slot A: down, down-forward, forward
	Dp623,       // K1 slot B: forward, down, down-forward
	Qcb214,      // K1 slot C: down, down-back, back
	DownDown22,  // K1 slot D: down, release, down
	// Not used by any K1 slot; available to moves that need them (not in the default priority).
	ChargeBackForward, // [4]6: hold back ChargeTicks, then forward
	ChargeDownUp,      // [2]8: hold down ChargeTicks, then up
}

/// <summary>
/// Pure motion matcher over an <see cref="InputBuffer"/>. A motion matches at <c>endAge</c>
/// (the button-press tick) when its steps appear in order within the MotionWindow ending there.
/// Directions are read relative to the facing passed in, so the same stick motion flips when
/// sides switch. Matching walks backwards greedily (latest occurrence of each step), which finds
/// the subsequence whenever one exists and gives the tightest span.
/// </summary>
public static class MotionParser
{
	const int D1 = 1 << 1, D2 = 1 << 2, D3 = 1 << 3, D4 = 1 << 4, D6 = 1 << 6;
	const int AnyDown = D1 | D2 | D3;
	const int NotDown = (1 << 4) | (1 << 5) | (1 << 6) | (1 << 7) | (1 << 8) | (1 << 9);

	// Step sets as numpad bitmasks. Lenience: 623 accepts 1 for its down step (6-1-3 rolls);
	// 22 accepts any down (1/2/3) for both downs and any non-down for the release.
	static readonly int[] Qcf = { D2, D3, D6 };
	static readonly int[] Dp = { D6, D2 | D1, D3 };
	static readonly int[] Qcb = { D2, D1, D4 };
	static readonly int[] DD = { AnyDown, NotDown, AnyDown };

	public static int[]? Steps(Motion m) => m switch
	{
		Motion.Qcf236 => Qcf,
		Motion.Dp623 => Dp,
		Motion.Qcb214 => Qcb,
		Motion.DownDown22 => DD,
		_ => null,
	};

	public static bool Matches(InputBuffer buf, Motion m, int facing, InputConfig cfg, int endAge = 0)
		=> FinalStepAge(buf, m, facing, cfg, endAge) >= 0;

	/// <summary>
	/// Age of the tick the motion's final step was input on (latest completion), or -1 if the
	/// motion is not in the window ending at endAge. Lower = more recent.
	/// </summary>
	public static int FinalStepAge(InputBuffer buf, Motion m, int facing, InputConfig cfg, int endAge = 0)
	{
		switch (m)
		{
			case Motion.ChargeBackForward:
				return MatchCharge(buf, facing, cfg, endAge, Numpad.IsBack, Numpad.IsForward);
			case Motion.ChargeDownUp:
				return MatchCharge(buf, facing, cfg, endAge, Numpad.IsDown, Numpad.IsUp);
		}
		var steps = Steps(m);
		if (steps == null) return -1;
		int k = steps.Length - 1, final = -1;
		int oldest = endAge + cfg.MotionWindow - 1; // oldest tick the first step may sit on
		for (int age = endAge; age <= oldest && age < buf.Count; age++)
		{
			if ((steps[k] & (1 << buf.Dir(age, facing))) == 0) continue;
			if (final < 0) final = age;
			if (--k < 0) return final;
		}
		return -1;
	}

	static int MatchCharge(InputBuffer buf, int facing, InputConfig cfg, int endAge,
		Func<int, bool> charge, Func<int, bool> release)
	{
		int oldest = endAge + cfg.MotionWindow - 1;
		for (int a = endAge; a <= oldest && a < buf.Count; a++)
		{
			if (!release(buf.Dir(a, facing))) continue;
			// The charge must have ended at most ChargeGrace ticks before the release direction.
			for (int b = a + 1; b <= a + 1 + cfg.ChargeGrace && b < buf.Count; b++)
			{
				if (!charge(buf.Dir(b, facing))) continue;
				int held = 0;
				while (b + held < buf.Count && charge(buf.Dir(b + held, facing))) held++;
				return held >= cfg.ChargeTicks ? a : -1; // only the latest charge run counts
			}
			// Release direction held: keep looking further back for where it began.
		}
		return -1;
	}
}
