namespace YokaiFighters.Fight;

/// <summary>
/// YOK-129 / AM9 (proposed): every tunable of the dynamic fight camera (<see cref="DynamicCamera"/>) in one place.
/// Presentation only: floats, never read by Sim/, Ai/ or the replay. The widest view is the sim's
/// <see cref="Sim.SimConfig.ViewWidth"/> (an integer, so the screen walls and projectile despawn stay in the sim);
/// the closest is <see cref="CloseViewWidth"/>. Widths are gameplay units at the Z = 0 plane (200 per metre, E1).
/// </summary>
public sealed record CameraConfig
{
	public static readonly CameraConfig Default = new();

	/// <summary>F4: vertical FOV, unchanged at ~25 deg (the dolly does the zooming, not the lens).</summary>
	public float FovDegrees { get; init; } = 25f;

	/// <summary>Screen aspect the widths are measured on (1920x1080).</summary>
	public float Aspect { get; init; } = 16f / 9f;

	/// <summary>Visible width when the fighters are close (at or under <see cref="NearSeparation"/>): Ryo's fighting stance fills ~52% of the screen height (~36% at the widest).</summary>
	public float CloseViewWidth { get; init; } = 1060f;

	/// <summary>Separation (units) up to which the camera holds its closest zoom.</summary>
	public float NearSeparation { get; init; } = 200f;

	/// <summary>Camera height (m) at the closest zoom when nobody is airborne: chest height.</summary>
	public float BaseHeightMeters { get; init; } = 1.2f;

	/// <summary>Camera height (m) at the widest zoom (designer review 2026-10-16: stays at chest height, no rise).</summary>
	public float WideHeightMeters { get; init; } = 1.2f;

	/// <summary>
	/// Pitch at the closest zoom, degrees down (negative = up), at most 5 either way. Lerped with the zoom to
	/// <see cref="WidePitchDegrees"/>, so the feet sit ~88-90% of the way down the frame at every zoom (a camera
	/// that dollies back at a fixed height sees the floor line rise unless it tilts up a little).
	/// </summary>
	public float PitchDegrees { get; init; } = 0.5f;

	/// <summary>Pitch at the widest zoom: 3 deg up, a low heroic angle that keeps the feet ~90% down at the screen walls.</summary>
	public float WidePitchDegrees { get; init; } = -3f;

	/// <summary>Share of the higher fighter's jump height the camera rises by ("rises partway").</summary>
	public float JumpFollow { get; init; } = 0.45f;

	/// <summary>Extra view width per unit of the higher fighter's jump height (a jump eases the camera out a little).</summary>
	public float JumpZoomPerUnit { get; init; } = 1f;

	/// <summary>Per-tick easing toward the target width and height (fixed lerp per sim tick, never delta time).</summary>
	public float ZoomEase { get; init; } = 0.08f;
	public float HeightEase { get; init; } = 0.1f;

	/// <summary>
	/// Highest point of a model above its feet, in metres: the top bone over every clip (ModelExtentTests checks it;
	/// 1.732 m, Ryo) plus the 15 cm hair/ear allowance the HUD test uses. Kept below <see cref="HudClearPx"/>.
	/// </summary>
	public float ModelTopMeters { get; init; } = 1.9f;

	/// <summary>
	/// What "a fighter fills x% of the screen height" measures: Ryo's idle fighting stance, feet to hair top, 1.55 m
	/// (measured on the YOK-129 captures; standing straight he is ~1.75 m). Framing tests only; the camera never reads it.
	/// </summary>
	public float StanceHeightMeters { get; init; } = 1.55f;

	/// <summary>The top HUD strip (138 px) plus a margin, on the 1080-px canvas: no fighter's head goes above this line.</summary>
	public float HudClearPx { get; init; } = 146f;

	/// <summary>
	/// How far past <see cref="Sim.FightCamera.VisibleHalfWidth"/> the view may show at the stage edge: the pitched
	/// camera sees a jumping head a little nearer than the plane, so a cornered model is framed by projection, not by the
	/// plane width. The stage art covers this (StageTests).
	/// </summary>
	public float StageEdgePadUnits { get; init; } = 20f;

	/// <summary>V7 round-ending blow: the slow push-in over the KO slow-down, as a share of the view width.</summary>
	public float KoPushIn { get; init; } = 0.12f;
}
