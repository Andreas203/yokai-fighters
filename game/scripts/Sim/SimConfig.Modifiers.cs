namespace YokaiFighters.Sim;

/// <summary>YOK-103: fight-wide numbers the modifier effects need that the GDD leaves open.</summary>
public sealed partial record SimConfig
{
	/// <summary>
	/// River Pull: units in "one step" of <c>properties.pull_on_hit</c>. **Proposed** (open designer question:
	/// the GDD gives no distance); 40 = the E5 default pushback on hit, so one step mirrors a default push.
	/// </summary>
	public int PullStepUnits { get; init; } = 40;
	/// <summary>
	/// Trickster's Angle: a projectile with <c>projectile.angle_up</c> climbs this % of its speed each tick.
	/// **Proposed** (open designer question: the GDD gives no angle); 50 = about 27 degrees.
	/// </summary>
	public int ProjectileAngleUpRisePct { get; init; } = 50;

	/// <summary>
	/// The <c>ryo_health_below_30pct</c> condition: health strictly below this % of max (rules.md Modifiers table,
	/// Rage Ember: "+15% damage while Ryo &lt; 30% health"). A constant, not tuning: the data's condition names the number.
	/// </summary>
	public const int LowHealthPct = 30;
}
