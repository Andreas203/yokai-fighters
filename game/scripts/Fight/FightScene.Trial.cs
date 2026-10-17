using System;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-98, R7: the dojo's training trial on this same fight scene and sim loop. Set <see cref="TrialStop"/> (the
/// session's dojo, its trial already chosen with <see cref="Dojo.ChooseTrial"/>) before the scene enters the tree:
/// the match is then built from the trial's setup (<see cref="TrialRun.Config"/>: start distance) with Ryo's run
/// kit as P1 and the dojo node's opponent from data/map/node-scenes.json as the dummy (P2, scripted by the trial,
/// no AI). Each ended attempt is reported to <see cref="Dojo.RecordAttempt"/>; when the dojo says the trial is over
/// (landed, or the third miss) <see cref="TrialFinished"/> is raised and the flow returns to the rest stop.
/// No replay is recorded for a trial (its attempts reset the match). The prompt line is a greybox label (ui-designer).
/// </summary>
public partial class FightScene
{
	/// <summary>The dojo whose chosen trial this scene plays; null = an ordinary fight.</summary>
	public Dojo? TrialStop { get; set; }
	public TrialRun? Trial { get; private set; }
	/// <summary>Raised once, after the dojo recorded the trial's last attempt (its heal and level are applied).</summary>
	public event Action? TrialFinished;
	public bool TrialDone { get; private set; }
	private Label? _trialLabel;

	private bool InTrial => TrialStop is not null;

	/// <summary>The trial's match: the run's Ryo against the dojo's dummy, the trial's start distance apart.</summary>
	private Match NewTrialMatch()
	{
		var stop = TrialStop!;
		var data = stop.Trial(stop.TrialId) ?? throw new InvalidOperationException($"FightScene: dojo '{stop.NodeId}' has no chosen trial (R7)");
		if (!stop.TrialRunning) throw new InvalidOperationException($"FightScene: the trial '{data.Id}' is not running (R7)");
		Match m = NewMatch(DemoRun, Session, cfg => TrialRun.Config(cfg, data));
		if (PlayerScheme is { } scheme) m.P1.Input.Scheme = scheme;
		Trial = new TrialRun(data, m);
		P2AiEnabled = false; // the dummy follows the trial's script (R7), never an AI
		GD.Print($"FightScene: trial '{data.Id}' for {data.Special} (slot {stop.TrialSlot}), {data.Distance} units, {data.AttemptFrames} ticks per attempt");
		return m;
	}

	/// <summary>After each sim step in trial mode: report an ended attempt, start the next or finish.</summary>
	private void CheckTrial()
	{
		if (Trial is not { AttemptOver: true } run || TrialDone) return;
		var stop = TrialStop!;
		var r = stop.RecordAttempt(run.Landed);
		if (!r.Ok) GD.PushWarning($"FightScene: {r.Error}");
		if (r.Ok && stop.TrialRunning)
		{
			run.NextAttempt();
			_camMatch = null; // the camera snaps to the reset positions
			Shake.Stop();
			UpdateTrialLabel();
			return;
		}
		TrialDone = true;
		UpdateTrialLabel();
		TrialFinished?.Invoke();
	}

	private void SetUpTrialLabel()
	{
		if (!InTrial) return;
		Recorder = null; // a trial's attempts reset the match: no replay
		_trialLabel = new Label { Name = "TrialPrompt", HorizontalAlignment = HorizontalAlignment.Center, Position = new Vector2(0, 140), Size = new Vector2(1920, 80) };
		_trialLabel.AddThemeFontSizeOverride("font_size", 28);
		var layer = new CanvasLayer { Name = "TrialLayer", Layer = 4 };
		layer.AddChild(_trialLabel);
		AddChild(layer);
		UpdateTrialLabel();
	}

	private void UpdateTrialLabel()
	{
		if (_trialLabel is null || Trial is not { } run || TrialStop is not { } stop) return;
		_trialLabel.Text = TrialDone
			? (stop.TrialResult == DojoTrialResult.Landed ? "Trial landed." : "Trial missed.") + $" Healed {stop.Healed}."
			: $"{run.Trial.Prompt}\nAttempt {run.Attempt} of {run.Trial.Attempts}";
	}
}
