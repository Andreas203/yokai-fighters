using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-152: Tanuki's tea house (V9, the boss stage), <c>res://scenes/stage/tea_house.tscn</c>. The back wall's eight paper
/// screens show the seasons (<see cref="SeasonScreens"/>), on the Tanuki's health beats (designer 2026-11-09, replacing
/// round start): <see cref="SeasonForHealth"/> and <see cref="SeasonBelowPct"/> are the one place to change them.
/// </summary>
public static class TeaHouseStage
{
	public const string Kind = "tea-house";
	public const string DefaultDir = "res://assets/generated/stage/tea-house/";

	/// <summary>The four season paintings, in order, under <c>seasons/</c> of the stage folder (<c>spring.png</c>, ...).</summary>
	public static readonly string[] Seasons = { "spring", "summer", "autumn", "winter" };

	/// <summary>
	/// The health beats: the opponent's health below each percent (of its maximum) brings the next season in. Spring at
	/// the fight's start; summer below 75%, autumn below 50%, winter below 25% (designer 2026-11-09).
	/// </summary>
	public static readonly int[] SeasonBelowPct = { 75, 50, 25 };

	/// <summary>
	/// Pure: the season (index into <see cref="Seasons"/>) that health <paramref name="health"/> of <paramref name="maxHealth"/>
	/// has reached: the number of beats it is strictly below. Integer maths (health x 100 &lt; pct x max). No maximum (0 or
	/// less) = spring. Which season shows is the lowest health seen in the fight (<see cref="SeasonBeats"/>: never back).
	/// </summary>
	public static int SeasonForHealth(int health, int maxHealth)
	{
		if (maxHealth <= 0) return 0;
		int season = 0;
		foreach (int pct in SeasonBelowPct)
			if ((long)health * 100 < (long)pct * maxHealth) season++;
		return season;
	}

	/// <summary>Ticks the screens take to cross-fade into a new season, from the tick its beat is crossed (0 = a hard cut).</summary>
	public const int SeasonFadeTicks = 30;

	/// <summary>
	/// The eight screens' bounding box in the backdrop (asset acceptance, 1280x720 px: x 148-1132, y 182-438), as UV
	/// (x, y, width, height; v down). Each season painting (one wide image of the same aspect) covers exactly this box.
	/// </summary>
	public static readonly Vector4 PanelRectUv = new(148f / 1280f, 182f / 720f, 984f / 1280f, 256f / 720f);

	/// <summary>
	/// The season paintings' row (v, down) that sits on the screens' bottom edge. Each painting's landscape ends in an ink
	/// ground line from row 456 (winter, autumn) to 463 of 566, with blank paper below it, which read as the view cut off at the bottom
	/// (designer 2026-11-10). So each painting is shifted down, unscaled, until row 454 meets the sill (two rows clear of the ink, for bilinear sampling): the hills run down
	/// to the frame and the paper strip and its ink line are never shown. The top of the screens repeats row 0, which is
	/// the paintings' flat sky.
	/// </summary>
	public const float PaintingBottomV = 454f / 566f;
}
