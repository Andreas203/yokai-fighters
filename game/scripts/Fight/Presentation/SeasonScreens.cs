using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-152: the tea house's season screens. On the backdrop quad's ShaderMaterial (<c>shaders/season_backdrop.gdshader</c>)
/// it sets two season paintings and a blend: the shader shows <c>mix(backdrop, season, panel mask)</c> with the painting's
/// UV remapped onto <see cref="TeaHouseStage.PanelRectUv"/>, so the screen frames stay painted. The seasons follow the
/// Tanuki's health beats (<see cref="SeasonBeats"/>, designer 2026-11-09): spring at the fight's start, the next season
/// once its beat is first crossed, cross-faded over <see cref="TeaHouseStage.SeasonFadeTicks"/> ticks from that tick,
/// never back within the fight; a new round (fight, Restart, reset) starts at spring. A missing season painting shows
/// the plain backdrop in its place (one warning, no crash).
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

	/// <summary>The health beats seen this round.</summary>
	public SeasonBeats Beats { get; } = new();

	/// <summary>The seasons shown now: from, to and the blend (1 = all <c>To</c>).</summary>
	public (int From, int To, float Blend) Shown { get; private set; } = (0, 0, 1f);

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
		if (missing.Count > 0) GD.PushWarning($"YOK-152: season screens missing under {dir}: {string.Join(", ", missing)}; those seasons show the plain backdrop");
		return list;
	}

	public override void _Ready() => Apply();

	public void Present(StageClock clock)
	{
		Beats.Observe(clock);
		Shown = Beats.Shown(clock.Tick);
		Apply();
	}

	private void Apply()
	{
		if (GetNodeOrNull<MeshInstance3D>(Backdrop)?.MaterialOverride is not ShaderMaterial mat) return;
		var (from, to, blend) = Shown;
		Texture2D? a = Textures[from], b = Textures[to];
		mat.SetShaderParameter("season_from", a);
		mat.SetShaderParameter("season_to", b);
		mat.SetShaderParameter("use_from", a is null ? 0f : 1f);
		mat.SetShaderParameter("use_to", b is null ? 0f : 1f);
		mat.SetShaderParameter("blend", blend);
	}
}

/// <summary>
/// YOK-152: the tea house's season state within a round, from the <see cref="StageClock"/> seen every sim tick. The season
/// is the furthest health beat reached (<see cref="TeaHouseStage.SeasonForHealth"/> of the health that tick, kept if
/// higher): monotonic, so health rising never brings a season back. A new <see cref="StageClock.Round"/> starts over at
/// spring. Crossing a beat starts a fade from the season reached before (a jump of two beats in one hit fades straight
/// across) at that tick. Observing the same tick twice changes nothing, so the picture is a function of the ticks seen.
/// </summary>
public sealed class SeasonBeats
{
	private int _round = int.MinValue;

	/// <summary>The season reached (index into <see cref="TeaHouseStage.Seasons"/>).</summary>
	public int Season { get; private set; }
	/// <summary>The season it fades from (the one reached before).</summary>
	public int From { get; private set; }
	/// <summary>The tick the latest beat was crossed (null: none yet this round).</summary>
	public int? ChangedTick { get; private set; }

	public void Observe(StageClock clock)
	{
		if (clock.Round != _round) { _round = clock.Round; Season = From = 0; ChangedTick = null; }
		int s = TeaHouseStage.SeasonForHealth(clock.OpponentHealth, clock.OpponentMaxHealth);
		if (s <= Season) return;
		From = Season;
		Season = s;
		ChangedTick = clock.Tick;
	}

	/// <summary>Pure: what shows at <paramref name="tick"/>: (from, to, blend), the blend rising over <paramref name="fadeTicks"/> from the crossing tick.</summary>
	public (int From, int To, float Blend) Shown(int tick, int fadeTicks = TeaHouseStage.SeasonFadeTicks)
	{
		if (ChangedTick is not { } at || fadeTicks <= 0) return (Season, Season, 1f);
		float t = Mathf.Clamp((tick - at) / (float)fadeTicks, 0f, 1f);
		return t >= 1f ? (Season, Season, 1f) : (From, Season, t);
	}
}
