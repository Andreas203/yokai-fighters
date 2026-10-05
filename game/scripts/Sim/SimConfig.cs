namespace YokaiFighters.Sim;

/// <summary>
/// Fight-wide constants for the simulation. Pure C#, no Godot types, integers only (F3).
/// Positions are centi-units: 1 unit = 1 px at 1920x1080 (data/schema convention), so 100 = one unit.
/// Defaults are placeholders until a data loader (YOK-40) feeds stage and fighter data.
/// </summary>
public sealed record SimConfig
{
	public const int TicksPerSecond = 60; // C1
	public const int Scale = 100;          // centi-units per gameplay unit

	/// <summary>Half the stage width; the corners sit at +/- this (2 screens wide).</summary>
	public int StageHalfWidth { get; init; } = 1920 * Scale;

	/// <summary>Visible width of the side-on camera, in centi-units (one 1920 px screen).</summary>
	public int ViewWidth { get; init; } = 1920 * Scale;

	/// <summary>Push-box width: two fighters' centres never get closer than this on the ground.</summary>
	public int BodyWidth { get; init; } = 120 * Scale;

	/// <summary>Screen walls: the farthest two fighters may be apart so both stay in view.</summary>
	public int MaxSeparation { get; init; } = 1600 * Scale;

	/// <summary>C2: walk crosses the 1920-unit screen in ~2.5 s (150 ticks) = 12.8 units/tick.</summary>
	public int WalkSpeed { get; init; } = 1280;

	public int StartOffset { get; init; } = 300 * Scale;

	public int P1MaxHealth { get; init; } = 1000; // C1: Ryo
	public int P2MaxHealth { get; init; } = 1000; // placeholder until yokai data lands

	/// <summary>V4: the round-ending blow plays this many world frames at half speed.</summary>
	public int KoSlowFrames { get; init; } = 30;
	public int KoSlowDivisor { get; init; } = 2;

	/// <summary>Placeholder strike used only until frame-data moves land (YOK-16).</summary>
	public int DebugStrikeDamage { get; init; } = 100;

	// --- Hits (YOK-16). Fight-wide fallbacks; per-move numbers live in move data. ---------

	/// <summary>Hurtbox of a fighter not in a move (units, feet-relative). Placeholder until fighter data lands.</summary>
	public Box IdleHurtbox { get; init; } = new(-45, 0, 90, 180);

	/// <summary>Defender slide in units when a move's data gives no pushback.</summary>
	public int HitPushback { get; init; } = 40;
	public int BlockPushback { get; init; } = 50;

	/// <summary>C4 "standard knockdown": frames on the floor, unhittable. Placeholder pending a designer number.</summary>
	public int KnockdownFrames { get; init; } = 40;

	/// <summary>C7 counterhit: +20% damage, +6 frames hitstun.</summary>
	public int CounterHitDamagePct { get; init; } = 20;
	public int CounterHitHitstun { get; init; } = 6;

	public static readonly SimConfig Default = new();
}
