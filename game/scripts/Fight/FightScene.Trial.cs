using System;
using Godot;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-98, R7: the dojo's training trial on this same fight scene and sim loop. Set <see cref="TrialStop"/> (the
/// session's dojo, its trial already chosen with <see cref="Dojo.ChooseTrial"/>) before the scene enters the tree:
/// the match is then built from the trial's setup (<see cref="TrialRun.Config"/>: start distance) with Ryo's run
/// kit as P1 and the dojo node's opponent from data/map/node-scenes.json as the dummy (P2, scripted by the trial,
/// no AI). Each ended attempt is reported to <see cref="Dojo.RecordAttempt"/>; when the dojo says the trial is over
/// (landed, or the third miss) <see cref="TrialFinished"/> is raised and the flow returns to the rest stop.
/// No replay is recorded for a trial (its attempts reset the match). The trial opens on the twist card's <see cref="TwistScroll"/>
/// drawn with the dojo's handscroll plate (the dojo master, the special and its icon, the quest) and the fight holds until it
/// is closed; during the attempts the quest stays on a small handscroll under the HUD (<see cref="TrialScrollBanner"/>).
/// </summary>
public partial class FightScene
{
	/// <summary>The dojo whose chosen trial this scene plays; null = an ordinary fight.</summary>
	public Dojo? TrialStop { get; set; }
	public TrialRun? Trial { get; private set; }
	/// <summary>Raised once, after the dojo recorded the trial's last attempt (its heal and level are applied).</summary>
	public event Action? TrialFinished;
	public bool TrialDone { get; private set; }

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

	/// <summary>The in-fight scroll that carries the quest and the attempt count; null outside a trial.</summary>
	public TrialScrollBanner? TrialBanner => _trialBanner;
	private TrialScrollBanner? _trialBanner;

	private void SetUpTrialLabel()
	{
		if (!InTrial) return;
		Recorder = null; // a trial's attempts reset the match: no replay
		_trialBanner = new TrialScrollBanner { Position = new Vector2((1920 - TrialScrollBanner.BannerWidth) / 2f, 128) };
		var layer = new CanvasLayer { Name = "TrialLayer", Layer = 4 };
		layer.AddChild(_trialBanner);
		AddChild(layer);
		UpdateTrialLabel();
	}

	/// <summary>The trained special's display name (the move data's name, else its id), as the rest stop's card names it.</summary>
	private string TrialSpecialName()
	{
		if (TrialStop is { } stop && Session?.Run[stop.TrialSlot]?.Data is { } data) return data.Name.Length > 0 ? data.Name : data.Id;
		return Trial?.Trial.Special ?? "";
	}

	private void UpdateTrialLabel()
	{
		if (_trialBanner is null || Trial is not { } run || TrialStop is not { } stop) return;
		string name = TrialSpecialName().ToUpperInvariant();
		if (TrialDone)
		{
			bool landed = stop.TrialResult == DojoTrialResult.Landed;
			_trialBanner.Show($"DOJO TRIAL · {name} · {(landed ? "LANDED" : "MISSED")}",
				landed ? $"Trial landed: Lv {stop.LevelBefore} → {stop.LevelAfter}. Healed {stop.Healed}." : $"Trial missed. Healed {stop.Healed}.");
			return;
		}
		_trialBanner.Show($"DOJO TRIAL · {name} · ATTEMPT {run.Attempt} OF {run.Trial.Attempts}", run.Trial.Prompt);
	}

	/// <summary>
	/// The trial opens on the hanging scroll (the twist card): kicker, the special's name, the quest as the quote, the stakes on
	/// the red line, the dojo master's portrait and the special's icon as the stamp. The fight holds while it shows
	/// (<see cref="FlowPaused"/>), so the first attempt's clock starts when it is closed.
	/// </summary>
	private void ShowTrialScroll()
	{
		if (!InTrial || TwistCard == null || TwistCard.Showing || Trial is not { } run || TrialStop is not { } stop) return;
		string name = TrialSpecialName();
		int row = Node?.Row ?? 0;
		string kicker = row > 0 ? $"DOJO TRIAL · ROW {row}" : "DOJO TRIAL";
		string stakes = $"Land it within {run.Trial.Attempts} tries to raise {name} to Lv {stop.LevelBefore + 1}. Heals {Dojo.TrialHealPercent}% either way.";
		string? portrait = UiTextures.Exists(RestStopScreen.DojoPortraitPath) ? RestStopScreen.DojoPortraitPath : null;
		TwistCard.Display(run.Trial.Prompt, kicker, null, "T", name, stakes, portrait);
		TwistCard.Scroll?.SetStampArt(AbilityArt.ForId(run.Trial.Special));
		TwistCard.Scroll?.SetPlate(TrialScrollBanner.LoadPlate()); // the dojo's handscroll, not the elder's hanging cloth
	}
}
