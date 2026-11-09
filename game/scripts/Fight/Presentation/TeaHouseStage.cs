using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-152: Tanuki's tea house (V9, the boss stage), <c>res://scenes/stage/tea_house.tscn</c>. The back wall's eight paper
/// screens show the seasons (<see cref="SeasonScreens"/>): which season a round shows is <see cref="SeasonForRound"/>,
/// the one place to change it (designer 2026-10-20: the season changes at round start only, never mid-round).
/// </summary>
public static class TeaHouseStage
{
	public const string Kind = "tea-house";
	public const string DefaultDir = "res://assets/generated/stage/tea-house/";

	/// <summary>The four season paintings, in order, under <c>seasons/</c> of the stage folder (<c>spring.png</c>, ...).</summary>
	public static readonly string[] Seasons = { "spring", "summer", "autumn", "winter" };

	/// <summary>
	/// The season a round shows, an index into <see cref="Seasons"/>: round n shows (n - 1) mod 4, so round 1 is spring and
	/// round 5 spring again. A round below 1 (none seen yet) counts as round 1. Pure; the designer's knob.
	/// </summary>
	public static int SeasonForRound(int round) => (Mathf.Max(1, round) - 1) % Seasons.Length;

	/// <summary>Ticks the screens take to cross-fade from the last round's season into this round's, from the round start (0 = a hard cut).</summary>
	public const int SeasonFadeTicks = 30;

	/// <summary>
	/// The eight screens' bounding box in the backdrop (asset acceptance, 1280x720 px: x 148-1132, y 182-438), as UV
	/// (x, y, width, height; v down). Each season painting (one wide image of the same aspect) covers exactly this box.
	/// </summary>
	public static readonly Vector4 PanelRectUv = new(148f / 1280f, 182f / 720f, 984f / 1280f, 256f / 720f);
}
