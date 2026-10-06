namespace YokaiFighters.Sim;

/// <summary>YOK-20: meter (C5), burst (C6) and hitstop (V2) constants.</summary>
public sealed partial record SimConfig
{
	/// <summary>C5: 3 bars x 100.</summary>
	public int MeterBar { get; init; } = 100;
	public int MeterBars { get; init; } = 3;
	public int MeterMax => MeterBar * MeterBars;
	/// <summary>C5 "+6 per hit landed": the attacker, when its move hits (move data meter_gain.on_hit overrides).</summary>
	public int MeterOnHit { get; init; } = 6;
	/// <summary>C5 "+3 per hit blocked": the attacker, when its move is blocked (move data meter_gain.on_block overrides).</summary>
	public int MeterOnBlock { get; init; } = 3;
	/// <summary>C5 "+3 per hit taken": the defender, when a hit lands on it.</summary>
	public int MeterOnTaken { get; init; } = 3;
	/// <summary>C5: an EX special costs 1 bar.</summary>
	public int ExCost => MeterBar;

	/// <summary>C6: invulnerable frames of a burst (the burster acts again right after them).</summary>
	public int BurstFrames { get; init; } = 20;
	/// <summary>
	/// PROPOSED (YOK-20): units the burst throws the opponent back, so the combo cannot simply resume.
	/// C6 gives no figure.
	/// </summary>
	public int BurstPushback { get; init; } = 200;
	/// <summary>
	/// PROPOSED (YOK-20): the three punch presses of a burst must all land within this many ticks
	/// (pressing three keys on exactly the same tick is unreliable on a keyboard).
	/// </summary>
	public int BurstPressWindow { get; init; } = 3;

	/// <summary>V2 hitstop frames by strength; counterhits add CounterHitstop. 0 disables (frame-count tests).</summary>
	public int HitstopLight { get; init; } = 6;
	public int HitstopMedium { get; init; } = 9;
	public int HitstopHeavy { get; init; } = 12;
	public int CounterHitstop { get; init; } = 4;
	public bool HitstopEnabled { get; init; } = true;

	public int Hitstop(HitStrength s, bool counter) => !HitstopEnabled ? 0
		: (s == HitStrength.Light ? HitstopLight : s == HitStrength.Medium ? HitstopMedium : HitstopHeavy)
		  + (counter ? CounterHitstop : 0);

	/// <summary>V3: screen shake length in ticks and amplitude range in px (presentation reads these).</summary>
	public int ShakeFrames { get; init; } = 6;
}
