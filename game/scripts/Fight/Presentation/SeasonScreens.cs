using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-152: the tea house's season screens. On the backdrop quad's ShaderMaterial (<c>shaders/season_backdrop.gdshader</c>)
/// it sets two season paintings and a blend: the shader shows <c>mix(backdrop, season, panel mask)</c> with the painting's
/// UV remapped onto <see cref="TeaHouseStage.PanelRectUv"/>, so the screen frames stay painted. Round n shows
/// <see cref="TeaHouseStage.SeasonForRound"/>(n); from round 2 on it cross-fades from the last round's season over the
/// first <see cref="TeaHouseStage.SeasonFadeTicks"/> ticks of the round (tick-driven, at round start only, never
/// mid-round). A missing season painting shows the plain backdrop in its place (one warning, no crash).
/// </summary>
[Tool]
[GlobalClass]
public partial class SeasonScreens : Node, IStageScenery
{
	/// <summary>The backdrop quad whose material shows the screens.</summary>
	[Export] public NodePath Backdrop { get; set; } = "../Backdrop";
	/// <summary>Folder of the season paintings (<c>spring.png</c>, ...).</summary>
	[Export] public string SeasonDir { get; set; } = TeaHouseStage.DefaultDir + "seasons/";

	/// <summary>Tests: where the season paintings are looked for when a SeasonScreens loads (null = its own <see cref="SeasonDir"/>).</summary>
	public static string? SeasonDirOverride { get; set; }

	private Texture2D?[]? _textures;

	/// <summary>The season paintings by <see cref="TeaHouseStage.Seasons"/> index; null where the file is missing.</summary>
	public Texture2D?[] Textures => _textures ??= Load(SeasonDirOverride ?? SeasonDir);

	/// <summary>The seasons shown now: from, to and the blend (1 = all <c>To</c>).</summary>
	public (int From, int To, float Blend) Shown { get; private set; }

	/// <summary>The paintings under <paramref name="dir"/>; a missing one is null, with one warning naming them all.</summary>
	public static Texture2D?[] Load(string dir)
	{
		var list = new Texture2D?[TeaHouseStage.Seasons.Length];
		var missing = new List<string>();
		for (int i = 0; i < list.Length; i++)
		{
			string path = dir + TeaHouseStage.Seasons[i] + ".png";
			list[i] = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
			if (list[i] is null) missing.Add(TeaHouseStage.Seasons[i]);
		}
		if (missing.Count > 0) GD.PushWarning($"YOK-152: season screens missing under {dir}: {string.Join(", ", missing)}; those rounds show the plain backdrop");
		return list;
	}

	/// <summary>Pure: what round <paramref name="round"/> shows at <paramref name="tick"/> of it (from, to, blend).</summary>
	public static (int From, int To, float Blend) At(int round, int tick, int fadeTicks = TeaHouseStage.SeasonFadeTicks)
	{
		int to = TeaHouseStage.SeasonForRound(round);
		if (round <= 1 || fadeTicks <= 0) return (to, to, 1f);
		int from = TeaHouseStage.SeasonForRound(round - 1);
		return (from, to, Mathf.Clamp(tick / (float)fadeTicks, 0f, 1f));
	}

	public override void _Ready() => Present(new StageClock(1, 0));

	public void Present(StageClock clock)
	{
		var (from, to, blend) = At(clock.Round, clock.Tick);
		Shown = (from, to, blend);
		if (GetNodeOrNull<MeshInstance3D>(Backdrop)?.MaterialOverride is not ShaderMaterial mat) return;
		Texture2D? a = Textures[from], b = Textures[to];
		mat.SetShaderParameter("season_from", a);
		mat.SetShaderParameter("season_to", b);
		mat.SetShaderParameter("use_from", a is null ? 0f : 1f);
		mat.SetShaderParameter("use_to", b is null ? 0f : 1f);
		mat.SetShaderParameter("blend", blend);
	}
}
