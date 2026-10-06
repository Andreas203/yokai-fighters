using System;
using Godot;
using YokaiFighters.Replay;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-49 hook: every fight is recorded from tick 0 (setup + per-tick inputs + StateHash). Debug builds:
/// <b>F8</b> saves the current fight to user://replays/ (replay it with tests/replay.tscn).
/// </summary>
public partial class FightScene
{
	public const Key SaveReplayKey = Key.F8;
	public const string ReplayDir = "user://replays";

	public ReplayRecorder? Recorder { get; private set; }
	private bool _saveReplayHeld;

	/// <summary>Starts a fresh recording of the current match (fight start and every reset).</summary>
	public void StartRecording() => Recorder = ReplayRecorder.Begin(Match, P2Ai, AiSeed, DemoRun ?? Run); // YOK-48: the demo's run

	/// <summary>The one place the scene advances the sim: step, then record what the sim saw.</summary>
	private void StepSim(FighterInput p1, FighterInput p2)
	{
		Match.Step(p1, p2);
		Recorder?.Record(p1, p2, Match.StateHash());
	}

	/// <summary>Writes the fight so far (result stamped as it stands) and returns the file's absolute path.</summary>
	public string SaveReplay(string? path = null)
	{
		var rec = (Recorder ?? throw new InvalidOperationException("no recording")).Finish(Match);
		path ??= ProjectSettings.GlobalizePath($"{ReplayDir}/fight-{DateTime.Now:yyyyMMdd-HHmmss}-t{Match.Tick}.yfr");
		rec.Save(path);
		GD.Print($"FightScene: replay saved to {path} ({rec.Steps} steps)");
		return path;
	}

	private void PollReplayKey()
	{
		if (!OS.IsDebugBuild()) return;
		bool down = Input.IsPhysicalKeyPressed(SaveReplayKey);
		if (down && !_saveReplayHeld && Recorder is not null) SaveReplay();
		_saveReplayHeld = down;
	}
}
