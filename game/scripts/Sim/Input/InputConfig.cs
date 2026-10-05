namespace YokaiFighters.Sim;

/// <summary>
/// Input timing constants, in 60 Hz ticks. The GDD fixes the motions (K1) but no frame windows,
/// so every value here is a proposal for the designer; a data loader fills it later (YOK-40).
/// </summary>
public sealed record InputConfig
{
	/// <summary>Ticks of raw input kept. Must cover ChargeTicks + ChargeGrace + MotionWindow.</summary>
	public int HistoryTicks { get; init; } = 128;

	/// <summary>Max span from a motion's first direction to the button press, inclusive (lenient timing).</summary>
	public int MotionWindow { get; init; } = 16;

	/// <summary>Ticks a parsed command waits to be consumed (pressed during recovery/hitstop).</summary>
	public int CommandBuffer { get; init; } = 5;

	/// <summary>Ticks a direction must be held to store a charge (charge motions).</summary>
	public int ChargeTicks { get; init; } = 40;

	/// <summary>Ticks allowed between releasing the charge and pressing the release direction.</summary>
	public int ChargeGrace { get; init; } = 8;
	/// <summary>
	/// Dash (C2) is a double tap: forward (or back), release, forward again, with the first tap at most
	/// this many ticks before the second. Proposed (YOK-18); the GDD gives no window.
	/// </summary>
	public int DashWindow { get; init; } = 12;

	/// <summary>
	/// Tie-break when several motions in the window complete on the same tick (otherwise the most
	/// recently completed motion wins). Earlier wins. Default: dragon punch over quarter-circles
	/// over down-down (standard convention).
	/// </summary>
	public Motion[] MotionPriority { get; init; } =
		{ Motion.Dp623, Motion.Qcf236, Motion.Qcb214, Motion.DownDown22 };

	/// <summary>
	/// Which attack wins when several are pressed on the same tick: heavy over medium over light,
	/// punch over kick at equal strength. The full pressed mask is still on the command (throws, C4).
	/// </summary>
	public InputBits[] ButtonPriority { get; init; } =
	{
		InputBits.HeavyPunch, InputBits.HeavyKick, InputBits.MediumPunch,
		InputBits.MediumKick, InputBits.LightPunch, InputBits.LightKick,
	};

	public static readonly InputConfig Default = new();
}
