using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-152: the tea house's season screens. On the backdrop quad's ShaderMaterial (<c>shaders/season_backdrop.gdshader</c>)
/// it sets the four season paintings and each one's share (<see cref="Weights"/>): the shader shows
/// <c>mix(backdrop, seasons, panel mask)</c> with each painting's UV remapped onto <see cref="TeaHouseStage.PanelRectUv"/>,
/// so the screen frames stay painted. The seasons follow the Tanuki's health beats (<see cref="SeasonBeats"/>, designer
/// 2026-11-09): spring at the fight's start, the next season once its beat is first crossed, cross-faded over
/// <see cref="TeaHouseStage.SeasonFadeTicks"/> ticks from that tick and from whatever the screens show then, never back
/// within the fight; a new round (fight, Restart, reset) starts at spring. A missing season painting shows the plain
/// backdrop in its share (one warning, no crash).
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

	/// <summary>Each season's share of the screens now (spring, summer, autumn, winter; they sum to 1).</summary>
	public Vector4 Weights { get; private set; } = SeasonBeats.Only(0);

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
		Weights = Beats.Weights(clock.Tick);
		Apply();
	}

	private void Apply()
	{
		if (GetNodeOrNull<MeshInstance3D>(Backdrop)?.MaterialOverride is not ShaderMaterial mat) return;
		var present = new float[4];
		for (int i = 0; i < 4; i++)
		{
			mat.SetShaderParameter($"season_{i}", Textures[i]);
			present[i] = Textures[i] is null ? 0f : 1f;
		}
		mat.SetShaderParameter("present", new Vector4(present[0], present[1], present[2], present[3]));
		mat.SetShaderParameter("weights", Weights);
	}
}

/// <summary>
/// YOK-152: the tea house's season state within a round, from the <see cref="StageClock"/> seen every sim tick. The season
/// is the furthest health beat reached (<see cref="TeaHouseStage.SeasonForHealth"/> of the health that tick, kept if
/// higher): monotonic, so health rising never brings a season back. A new <see cref="StageClock.Round"/> starts over at
/// spring. Crossing a beat starts a fade at that tick from whatever the screens show then (the mix of a fade still
/// running, or a whole season) toward the new season: the picture never jumps, each share moving at most
/// 1 / <see cref="TeaHouseStage.SeasonFadeTicks"/> a tick, and two beats in one hit fade straight across. Observing the
/// same tick twice changes nothing, so the picture is a function of the ticks seen.
/// </summary>
public sealed class SeasonBeats
{
	private int _round = int.MinValue;
	private Vector4 _start = Only(0);

	/// <summary>The season reached (index into <see cref="TeaHouseStage.Seasons"/>).</summary>
	public int Season { get; private set; }
	/// <summary>The tick the latest beat was crossed (null: none yet this round).</summary>
	public int? ChangedTick { get; private set; }

	/// <summary>All of one season, none of the others.</summary>
	public static Vector4 Only(int season) => new(season == 0 ? 1 : 0, season == 1 ? 1 : 0, season == 2 ? 1 : 0, season == 3 ? 1 : 0);

	public void Observe(StageClock clock)
	{
		if (clock.Round != _round) { _round = clock.Round; Season = 0; _start = Only(0); ChangedTick = null; }
		int s = TeaHouseStage.SeasonForHealth(clock.OpponentHealth, clock.OpponentMaxHealth);
		if (s <= Season) return;
		_start = Weights(clock.Tick); // what shows on this tick, before the new beat
		Season = s;
		ChangedTick = clock.Tick;
	}

	/// <summary>Pure: each season's share at <paramref name="tick"/>: the mix at the crossing, eased linearly to the season reached over <paramref name="fadeTicks"/>.</summary>
	public Vector4 Weights(int tick, int fadeTicks = TeaHouseStage.SeasonFadeTicks)
	{
		Vector4 target = Only(Season);
		if (ChangedTick is not { } at || fadeTicks <= 0) return target;
		float k = Mathf.Clamp((tick - at) / (float)fadeTicks, 0f, 1f);
		return k >= 1f ? target : _start.Lerp(target, k);
	}
}
