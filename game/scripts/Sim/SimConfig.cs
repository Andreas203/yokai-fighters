namespace YokaiFighters.Sim;

/// <summary>
/// Fight-wide constants for the simulation. Pure C#, no Godot types, integers only (F3).
/// Positions are centi-units, so 100 = one gameplay unit; 200 units per metre, and 1,720 units span the 1,920 px
/// screen at the fight camera's widest zoom (E1, AM9 proposed).
/// Defaults are placeholders until a data loader (YOK-40) feeds stage and fighter data.
/// </summary>
public sealed partial record SimConfig
{
	public const int TicksPerSecond = 60; // C1
	public const int Scale = 100;          // centi-units per gameplay unit

	/// <summary>
	/// Half the stage width; the corners sit at +/- this. 3,840 units wide (E1): two of the original 1,920-unit screens,
	/// kept when AM8 brought the camera in, so the stage art and the visible stage (<see cref="FightCamera.VisibleHalfWidth"/>)
	/// do not change; AM9's dynamic camera stops at the same edge (2.2 of its widest 1,720-unit screens).
	/// </summary>
	public int StageHalfWidth { get; init; } = 1920 * Scale;

	/// <summary>
	/// The sim's screen: the camera's widest view at the gameplay plane, in centi-units. AM9 (YOK-129, proposed): the
	/// dynamic camera zooms between <c>CameraConfig.CloseViewWidth</c> (1,200) and this, 1,720 (AM8 had a fixed 1,440,
	/// before it 1,920), at the same ~25 deg FOV (F4). The sim never reads the presentation camera: the screen walls
	/// (<see cref="MaxSeparation"/>) derive from this and projectiles leave the screen at its edge (<see cref="FightCamera"/>).
	/// MaxSeparation + 2 x the larger model reach (front/back) must not exceed it (CameraTests).
	/// </summary>
	public int ViewWidth { get; init; } = 1720 * Scale;

	/// <summary>Push-box width: two fighters' centres never get closer than this on the ground.</summary>
	public int BodyWidth { get; init; } = 120 * Scale;

	/// <summary>
	/// Screen walls: the farthest two fighters may be apart so both models stay in view whichever way each faces.
	/// Derived, so it cannot drift: <see cref="ViewWidth"/> - 2 x the larger model reach (front 240 / back 200), because
	/// a fighter facing away from the other shows its 240 front reach on the outside. E1/AM9 (YOK-129, proposed):
	/// 1,240 + 2 x 240 = 1,720 at the widest zoom (AM8: 960 + 2 x 240 = 1,440; YOK-39: 1,520 for the 1,920 view).
	/// Setting it in an initializer overrides the derivation (tests). Trial start distances are capped at this
	/// (TrialData.MaxStartDistance, trial.schema.json).
	/// </summary>
	public int MaxSeparation
	{
		get => _maxSeparation ?? ViewWidth - 2 * System.Math.Max(System.Math.Max(ModelFrontReach, ModelBackReach), BodyWidth / 2);
		init => _maxSeparation = value;
	}
	private readonly int? _maxSeparation;

	/// <summary>
	/// YOK-39: how far a fighter's rigged model reaches past its X toward its facing (front) and away from it (back),
	/// in centi-units, over every frame of every clip (bones + a flesh pad, the Kitsune's tails; measured by
	/// ModelExtentTests: front 219 in the jump kick, back 187 lying in the knockdown; rounded up). Used only by
	/// <see cref="FightCamera"/>'s clamp (so also where projectiles leave the screen); constants, so deterministic.
	/// </summary>
	public int ModelFrontReach { get; init; } = 240 * Scale;
	public int ModelBackReach { get; init; } = 200 * Scale;

	/// <summary>
	/// C2: walk crosses the original 1,920-unit screen in ~2.5 s (150 ticks) = 12.8 units/tick. AM8 keeps the speed, so
	/// the closer 1,440-unit screen takes ~1.9 s (designer to confirm at review).
	/// </summary>
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
	/// <summary>YOK-47: health at fight start (C1 carry-over, R3); null = max health.</summary>
	public int? P1StartHealth { get; init; }
	public int? P2StartHealth { get; init; }

	/// <summary>V4: the round-ending blow plays this many world frames at half speed.</summary>
	public int KoSlowFrames { get; init; } = 30;
	public int KoSlowDivisor { get; init; } = 2;

	/// <summary>Placeholder strike used only until frame-data moves land (YOK-16).</summary>
	public int DebugStrikeDamage { get; init; } = 100;

	// --- Hits (YOK-16). Fight-wide fallbacks; per-move numbers live in move data. ---------

	/// <summary>
	/// Hurtbox of a fighter not in a move (units, feet-relative), per stance (E5): measured from the rigged
	/// models in YOK-33 hitbox alignment and accepted by the designer (YOK-39). Moves keep their own boxes.
	/// </summary>
	public Box IdleHurtbox { get; init; } = new(-45, 0, 90, 320);
	public Box CrouchHurtbox { get; init; } = new(-45, 0, 90, 240);
	public Box AirHurtbox { get; init; } = new(-45, 40, 90, 270);

	/// <summary>Defender slide in units when a move's data gives no pushback.</summary>
	public int HitPushback { get; init; } = 40;
	public int BlockPushback { get; init; } = 50;

	/// <summary>C4 "standard knockdown": frames on the floor, unhittable. Placeholder pending a designer number.</summary>
	public int KnockdownFrames { get; init; } = 40;
	/// <summary>
	/// YOK-53 follow-up: invulnerable, non-actionable get-up frames after the knockdown (E4 stays 40), so the
	/// presenter can roll the fighter from the knockdown's face-up end onto the get-up clip's face-down start and
	/// play the get-up (347 Stand_Up4, 33 frames) without a pop. 28 is a DESIGNER PROPOSAL (40 + 28 = 68 frames
	/// down, 1.13 s). 0 = no wake-up phase (the old behaviour).
	/// </summary>
	public int WakeUpFrames { get; init; } = 28;
	/// <summary>
	/// E12 (YOK-19): the two throw buttons count as a throw when the second goes down at most this
	/// many ticks after the first (gaps 0..3 throw, 4 does not). Kata and Kihon alike.
	/// </summary>
	public int ThrowPressWindow { get; init; } = 3;

	/// <summary>C7 counterhit: +20% damage, +6 frames hitstun.</summary>
	public int CounterHitDamagePct { get; init; } = 20;
	public int CounterHitHitstun { get; init; } = 6;

	public static readonly SimConfig Default = new();
}
