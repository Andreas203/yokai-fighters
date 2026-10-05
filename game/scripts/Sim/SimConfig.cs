namespace YokaiFighters.Sim;

/// <summary>
/// Fight-wide constants for the simulation. Pure C#, no Godot types, integers only (F3).
/// Positions are centi-units: 1 unit = 1 px at 1920x1080 (data/schema convention), so 100 = one unit.
/// Defaults are placeholders until a data loader (YOK-40) feeds stage and fighter data.
/// </summary>
public sealed partial record SimConfig
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

	// --- Movement (YOK-18). Frame counts are C2; distances and heights are proposed placeholders. ---

	/// <summary>C2: a dash lasts 18 frames, forward or back; not actionable until it ends.</summary>
	public int DashFrames { get; init; } = 18;
	/// <summary>Proposed: forward dash covers 300 units, back dash 240 (no GDD figure).</summary>
	public int DashDistance { get; init; } = 300 * Scale;
	public int BackDashDistance { get; init; } = 240 * Scale;

	/// <summary>C2: 40 frames of airtime; the fighter lands and acts again on the 41st.</summary>
	public int JumpFrames { get; init; } = 40;
	/// <summary>Proposed: apex 250 units (~1.25 m at E1's 200 units per metre).</summary>
	public int JumpHeight { get; init; } = 250 * Scale;
	/// <summary>Proposed: forward/back jumps drift 8 units per frame, 320 units over the jump.</summary>
	public int JumpSpeedX { get; init; } = 8 * Scale;

	/// <summary>Integer jump arc: height on air frame n (0..JumpFrames), a parabola from 0 to 0.</summary>
	public int JumpY(int n) => (int)(4L * JumpHeight * n * (JumpFrames - n) / ((long)JumpFrames * JumpFrames));

	/// <summary>Dash displacement on dash frame n (1..DashFrames), so the frames sum exactly to the distance.</summary>
	public int DashStep(int n, int distance) => distance * n / DashFrames - distance * (n - 1) / DashFrames;

	public int P1MaxHealth { get; init; } = 1000; // C1: Ryo
	public int P2MaxHealth { get; init; } = 1000; // placeholder until yokai data lands

	/// <summary>V4: the round-ending blow plays this many world frames at half speed.</summary>
	public int KoSlowFrames { get; init; } = 30;
	public int KoSlowDivisor { get; init; } = 2;

	/// <summary>Placeholder strike used only until frame-data moves land (YOK-16).</summary>
	public int DebugStrikeDamage { get; init; } = 100;

	// --- Hits (YOK-16). Fight-wide fallbacks; per-move numbers live in move data. ---------

	/// <summary>
	/// Hurtbox of a fighter not in a move (units, feet-relative), per stance. PROPOSED (YOK-18), for
	/// the designer: E5's single 90x180 box covers only the lower half of a 1.8 m fighter (360 units at
	/// E1), so standing is 90x360, crouching about half plus the head (90x200), airborne tucked (90x280).
	/// Placeholders until fighter data lands; E5 in rules.md is unchanged.
	/// </summary>
	public Box IdleHurtbox { get; init; } = new(-45, 0, 90, 360);
	public Box CrouchHurtbox { get; init; } = new(-45, 0, 90, 200);
	public Box AirHurtbox { get; init; } = new(-45, 0, 90, 280);

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
