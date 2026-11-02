using System;
using Godot;
using YokaiFighters.Audio;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-198 partial: hit sparks (V5, V8) and the frame-timed hit-sound hook (V6). Presentation only: the scene
/// listens to <see cref="Match.Impact"/>, spawns a spark and raises one <see cref="HitCue"/> per impact; nothing here
/// is read by the sim, the AI, replays or the harness. Hooks elsewhere: <c>SetUpHitSparks()</c> in <c>_Ready</c>,
/// <c>RenderHitSparks()</c> in <c>Render</c> and <c>DetachHitSparks()</c> in <c>_ExitTree</c>. A replaced match (rematch, next node) is picked up on its first render
/// (<c>ReplaceMatch</c> and <c>_Ready</c> both render before any tick), and <see cref="HitSparks"/> drops its sparks
/// when the tick goes back (a reset).
/// </summary>
public partial class FightScene
{
	/// <summary>The fight's sparks (the layer draws them every render).</summary>
	public HitSparks Sparks { get; } = new();

	/// <summary>
	/// Where each impact's sound cue goes. YOK-93: a <see cref="DataHitCueSink"/> when the cue tables (data/sound/, or
	/// <see cref="SoundCueTables"/>) have a rule, set in <c>_Ready</c>; silent otherwise, and the placeholder hit pack plays.
	/// </summary>
	public IHitCueSink HitCues { get; set; } = SilentHitCues.Instance;

	/// <summary>YOK-93: cue tables for this scene, set before it enters the tree (tests); null = <see cref="SoundCues"/>.</summary>
	public HitCueLibrary? SoundCueTables { get; set; }

	/// <summary>YOK-93: the data sink when the cue tables have a rule (it replaces the placeholder hit pack), else null.</summary>
	public DataHitCueSink? DataCues { get; private set; }

	/// <summary>YOK-93: the players the data sink plays through, or null.</summary>
	public HitSoundPool? HitSoundPool { get; private set; }

	/// <summary>The cue of the last impact (tests, debugging).</summary>
	public HitCue? LastHitCue { get; private set; }

	public HitSparkLayer? SparkLayer { get; private set; }

	private Match? _sparkMatch;
	private static LevelPresetLibrary? _presets;
	private static HitCueLibrary? _soundCues;

	/// <summary>YOK-93 (V6): data/sound/, read once. A file the loader refuses is reported and the placeholder hit pack plays.</summary>
	public static HitCueLibrary SoundCues => _soundCues ??= LoadSoundCues(ContentPaths.Data("sound"));

	public static HitCueLibrary LoadSoundCues(string dir)
	{
		try { return HitCueLibrary.LoadDirectory(dir); }
		catch (Exception e) when (e is FormatException or System.IO.IOException or UnauthorizedAccessException)
		{
			GD.PushError($"FightScene: hit sound cues not loaded, placeholder hit pack only: {e.Message}");
			return HitCueLibrary.Empty;
		}
	}

	/// <summary>
	/// YOK-93: when the cue tables have a rule, every impact plays its cue through a <see cref="HitSoundPool"/> and the
	/// placeholder round-robin is not set up (true). With none, nothing changes (false).
	/// </summary>
	private bool SetUpDataHitCues()
	{
		var tables = SoundCueTables ?? SoundCues;
		if (!tables.HasCues) return false;
		HitSoundPool = new HitSoundPool();
		AddChild(HitSoundPool);
		DataCues = new DataHitCueSink(tables, HitSoundPool);
		HitCues = DataCues;
		return true;
	}

	/// <summary>data/presets/ (V8), read once. A file the loader refuses is reported and every hit falls back to the default spark.</summary>
	public static LevelPresetLibrary Presets => _presets ??= LoadPresets(ContentPaths.Data("presets"));

	public static LevelPresetLibrary LoadPresets(string dir)
	{
		try { return LevelPresetLibrary.LoadDirectory(dir); }
		catch (Exception e) when (e is FormatException or System.IO.IOException or UnauthorizedAccessException)
		{
			GD.PushError($"FightScene: level presets not loaded, default sparks only: {e.Message}");
			return LevelPresetLibrary.Empty;
		}
	}

	private void SetUpHitSparks()
	{
		Sparks.Presets = Presets;
		SparkLayer = new HitSparkLayer { Name = HitSparkLayer.NodeName };
		AddChild(SparkLayer);
	}

	private void OnImpactSpark(Match m, ImpactEvent e)
	{
		var cue = Sparks.OnImpact(m, e);
		LastHitCue = cue;
		HitCues.Play(cue);
	}

	/// <summary>Stops listening to the match (from <c>_ExitTree</c>): a match that outlives the scene never calls back into it.</summary>
	private void DetachHitSparks()
	{
		if (_sparkMatch != null) _sparkMatch.Impact -= OnImpactSpark;
		_sparkMatch = null;
	}

	private void RenderHitSparks()
	{
		if (!ReferenceEquals(_sparkMatch, Match))
		{
			if (_sparkMatch != null) _sparkMatch.Impact -= OnImpactSpark;
			_sparkMatch = Match;
			Match.Impact += OnImpactSpark;
			Sparks.Clear();
			DataCues?.Clear(); // YOK-93: a delayed hit sound of the old match never plays in the new one
		}
		SparkLayer?.Draw(Sparks.Visible(Match));
	}
}
