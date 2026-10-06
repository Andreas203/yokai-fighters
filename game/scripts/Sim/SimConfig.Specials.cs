namespace YokaiFighters.Sim;

/// <summary>YOK-21: special moves, EX and projectiles.</summary>
public sealed partial record SimConfig
{
	/// <summary>K1: Kata motion-input specials deal +10% damage (Kihon and direct requests 100%).</summary>
	public int PrecisionBonusPct { get; init; } = 10;
	/// <summary>
	/// EX input, **designer proposal**: the special's motion with two punches or two kicks (never LP+LK,
	/// the E12 throw; LP+MP+HP only bursts from hitstun, E13, where specials can't start). The second
	/// button may come up to ExPressWindow - 1 ticks after the first (same 3-tick leniency as E12):
	/// the special upgrades in place while its frame is still within the window.
	/// </summary>
	public int ExPressWindow { get; init; } = 3;
}
