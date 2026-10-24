using System;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-198 partial: hit sparks (V5, V8) and the frame-timed hit-sound hook (V6). Presentation only: the scene
/// listens to <see cref="Match.Impact"/>, spawns a spark and raises one <see cref="HitCue"/> per impact; nothing here
/// is read by the sim, the AI, replays or the harness. Hooks elsewhere: <c>SetUpHitSparks()</c> in <c>_Ready</c> and
/// <c>RenderHitSparks()</c> in <c>Render</c>. A replaced match (rematch, next node) is picked up on its first render
/// (<c>ReplaceMatch</c> and <c>_Ready</c> both render before any tick), and <see cref="HitSparks"/> drops its sparks
/// when the tick goes back (a reset).
/// </summary>
public partial class FightScene
{
	/// <summary>The fight's sparks (the layer draws them every render).</summary>
	public HitSparks Sparks { get; } = new();

	/// <summary>Where each impact's sound cue goes. Silent until the animator's cues (data/sound/) attach.</summary>
	public IHitCueSink HitCues { get; set; } = SilentHitCues.Instance;

	/// <summary>The cue of the last impact (tests, debugging).</summary>
	public HitCue? LastHitCue { get; private set; }

	public HitSparkLayer? SparkLayer { get; private set; }

	private Match? _sparkMatch;
	private static LevelPresetLibrary? _presets;

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

	private void RenderHitSparks()
	{
		if (!ReferenceEquals(_sparkMatch, Match))
		{
			if (_sparkMatch != null) _sparkMatch.Impact -= OnImpactSpark;
			_sparkMatch = Match;
			Match.Impact += OnImpactSpark;
			Sparks.Clear();
		}
		SparkLayer?.Draw(Sparks.Visible(Match.Tick));
	}
}
