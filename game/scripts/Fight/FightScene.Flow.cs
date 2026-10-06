using System;
using System.Collections.Generic;
using Godot;
using YokaiFighters.Replay;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Fight;

/// <summary>Where the YOK-39 demo is: in a duel, or on one of the screens between and after duels.</summary>
public enum DemoStage { Title, Fighting, Binding, Reward, Complete, Lost }

/// <summary>
/// YOK-48 demo flow (YOK-39 vertical slice): boot straight into Ryo vs Kitsune on a fresh run (C1, 1,000 health),
/// then route on the result. Loss: the HUD lose screen (data/story/lose-screen.json), Restart = a fresh run.
/// Win: KO slow-down (V4) → binding line card → reward screen (three cards from the beaten yokai, A7) → the pick
/// applied to the run → rematch with health left + 50, capped at 1,000 (R3). After the rematch: win = "demo
/// complete" card, loss = lose screen; every Restart starts the first fight again. Each duel gets its own recording
/// (YOK-49), stamped with the run. Debug builds: <b>F9</b> opens the debug menu (Kitsune temperament, restart run),
/// pausing the fight; <c>--temperament=patient</c> on the command line picks one before the first fight.
/// </summary>
public partial class FightScene
{
	public const Key DebugMenuKey = Key.F9;
	/// <summary>Duels in the demo: the first fight and the rematch.</summary>
	public const int DemoDuels = 2;

	/// <summary>Seed for the reward draft (duel n uses RunSeed + n), so one run offers the same cards on replay.
	/// A fresh run picks a new seed unless <see cref="FixedRunSeed"/> is set (tests, harness).</summary>
	public uint RunSeed { get; set; } = 48;
	/// <summary>Set to pin every run to one seed; null = a new random seed per run (designer decision, YOK-48).</summary>
	public uint? FixedRunSeed { get; set; }

	private void PickRunSeed() => RunSeed = FixedRunSeed ?? (uint)System.Random.Shared.Next();
	public DemoStage Stage { get; private set; } = DemoStage.Fighting;
	/// <summary>YOK-39: null = the start screen shows at boot unless the scene is test-driven (<see cref="ExternalDrive"/>); true/false forces it.</summary>
	public bool? StartScreenOverride { get; set; }
	public StartScreen Title { get; private set; } = null!;
	/// <summary>Buttons held when Start (or a Restart) was pressed: ignored as P1 input until released, so Space does not fire a Special.</summary>
	private InputBits _suppress;
	/// <summary>1 = the first fight, 2 = the rematch.</summary>
	public int Duel { get; private set; } = 1;
	/// <summary>The run this scene plays (fresh at boot and on every Restart; <see cref="Run"/> if a caller set one).</summary>
	public RunState DemoRun { get; private set; } = null!;
	public AbilityPool Pool { get; private set; } = null!;
	/// <summary>The current reward offer (from the beaten yokai), while the binding and reward screens show.</summary>
	public DraftRewardSource? Draft { get; private set; }
	/// <summary>Finished recordings of this run's duels, in order (cleared on Restart).</summary>
	public List<ReplayRecording> DuelRecordings { get; } = new();
	public bool FlowPaused => DebugMenu?.Visible == true;

	public StoryCard BindingCard { get; private set; } = null!;
	public RewardScreen Rewards { get; private set; } = null!;
	public DemoCompleteScreen CompleteScreen { get; private set; } = null!;
	public DemoDebugMenu? DebugMenu { get; private set; }
	private bool _menuHeld;

	/// <summary>Called first in _Ready: the run, and the first fight built from it.</summary>
	private void SetUpRun()
	{
		Pool = LoadAbilityPool();
		DemoRun = Run ?? RunState.NewRun(Pool);
		PickRunSeed();
		Match = NewMatch(DemoRun);
	}

	/// <summary>Fills the demo-complete screen from data/story/demo-complete.json; keeps the placeholder (and warns) if missing or bad.</summary>
	private void ApplyDemoCompleteCard()
	{
		try
		{
			var (title, text) = LoadStory().DemoComplete(Story.StoryLibrary.DisplayName(Opponent));
			CompleteScreen.SetText(title, text);
		}
		catch (System.Exception e) when (e is System.IO.IOException or System.FormatException or System.Text.Json.JsonException
			or System.Collections.Generic.KeyNotFoundException or System.InvalidOperationException)
		{
			GD.PushWarning($"demo-complete screen keeps its placeholder: {e.Message}");
		}
	}

	/// <summary>Called at the end of _Ready (after the HUD and AI): the screens and the debug menu.</summary>
	private void SetUpFlow()
	{
		// The screens are instances of scenes/ui/*.tscn in fight.tscn (Flow layer 5, DebugMenuLayer 20); this only wires them.
		BindingCard = GetNode<StoryCard>("Flow/BindingCard");
		BindingCard.Finished += ShowRewards;
		Rewards = GetNode<RewardScreen>("Flow/Rewards");
		Rewards.Picked += (_, _) => StartRematch();
		CompleteScreen = GetNode<DemoCompleteScreen>("Flow/Complete");
		CompleteScreen.RestartRequested += RestartRun;
		ApplyDemoCompleteCard();
		Title = GetNode<StartScreen>("Flow/Title");
		Title.Started += BeginFight;
		if (StartScreenOverride ?? !ExternalDrive) Stage = DemoStage.Title; // shown by default in the scene
		else Title.Visible = false;
		if (!OS.IsDebugBuild()) { GetNode("DebugMenuLayer").QueueFree(); return; } // the debug menu is for debug builds only
		DebugMenu = GetNode<DemoDebugMenu>("DebugMenuLayer/DebugMenu");
		DebugMenu.Bind(this);
		foreach (string arg in OS.GetCmdlineUserArgs())
			if (arg.StartsWith("--temperament=", StringComparison.Ordinal)) SetTemperament(arg["--temperament=".Length..]);
	}

	/// <summary>The temperament P2's AI plays (from its profile), e.g. "aggressive".</summary>
	public string Temperament => Profiles.Length == 0 ? "" : Profiles[_profileIndex % Profiles.Length].Temperament;

	/// <summary>Debug: P2 plays the named temperament (aggressive / patient); false if no profile has it.</summary>
	public bool SetTemperament(string temperament)
	{
		int i = Array.FindIndex(Profiles, p => p.Temperament == temperament);
		if (i < 0) { GD.PushWarning($"FightScene: no {Opponent} profile with temperament '{temperament}'"); return false; }
		_profileIndex = i;
		RebuildAi();
		GD.Print($"FightScene: P2 AI profile {Profiles[i].Id} ({temperament})");
		return true;
	}

	/// <summary>Debug: F9 opens/closes the menu; the fight is paused while it shows.</summary>
	public void ToggleDebugMenu()
	{
		if (DebugMenu == null) return;
		if (DebugMenu.Visible) { DebugMenu.Close(); _clock.Restart(); } // no catch-up burst on resume
		else DebugMenu.Open();
	}

	/// <summary>Start Game: the first fight begins now; whatever was held to press it is ignored until released.</summary>
	private void BeginFight()
	{
		if (Stage != DemoStage.Title) return;
		Stage = DemoStage.Fighting;
		_suppress = HeldBits();
		_clock.Restart();
	}

	private static InputBits HeldBits() => InputDevices.Read(0).Bits;

	/// <summary>P1's live input minus anything still held from a screen button (Start, Restart).</summary>
	private FighterInput P1Live()
	{
		InputBits raw = HeldBits();
		_suppress &= raw;
		return new FighterInput(raw & ~_suppress);
	}

	/// <summary>Test seam: the mask applied to a raw P1 read of <paramref name="raw"/>, advancing the release tracking.</summary>
	public InputBits SuppressedInputForTest(InputBits raw) { _suppress &= raw; return raw & ~_suppress; }
	/// <summary>Test seam: pretend these were held when Start was pressed.</summary>
	public void ArmSuppressForTest(InputBits held) => _suppress = held;

	private void PollFlowKeys()
	{
		if (DebugMenu == null) return;
		bool down = Input.IsPhysicalKeyPressed(DebugMenuKey);
		if (down && !_menuHeld) ToggleDebugMenu();
		_menuHeld = down;
	}

	/// <summary>After each sim step: route once when a duel ends.</summary>
	private void CheckDuelOver()
	{
		if (Stage != DemoStage.Fighting || Match.Phase != MatchPhase.Over) return;
		if (Recorder is { } rec) { DuelRecordings.Add(rec.Finish(Match)); Recorder = null; }
		if (Match.Winner != 0) { Stage = DemoStage.Lost; return; } // the HUD shows the lose screen (double KO counts as a loss)
		DemoRun.AfterDuel(Match.P1.Health); // R3: health left + 50, capped at 1,000
		if (Duel >= DemoDuels) { Stage = DemoStage.Complete; CompleteScreen.Show(); return; }
		Draft = new DraftRewardSource(RewardDraft.Build(DemoRun, Pool, Opponent, RunSeed + (uint)Duel), DemoRun);
		string line;
		try { line = Draft.BindingLine(Story.StoryLibrary.DisplayName(Opponent)); }
		catch (Exception e) when (e is System.IO.IOException or FormatException or System.Text.Json.JsonException
			or KeyNotFoundException or InvalidOperationException)
		{
			GD.PushWarning($"no binding line, straight to the reward: {e.Message}");
			Stage = DemoStage.Binding;
			ShowRewards();
			return;
		}
		Stage = DemoStage.Binding;
		BindingCard.Display(line);
	}

	private void ShowRewards()
	{
		if (Stage != DemoStage.Binding || Draft == null) return;
		Stage = DemoStage.Reward;
		Rewards.Present(Draft);
	}

	/// <summary>The pick is already on the run (DraftRewardSource.Pick); the rematch takes it and the carried health.</summary>
	private void StartRematch()
	{
		Draft = null;
		Duel++;
		Stage = DemoStage.Fighting;
		ReplaceMatch(NewMatch(DemoRun));
	}

	/// <summary>Restart from any end screen (and the debug menu): a fresh run, back to the first fight.</summary>
	public void RestartRun()
	{
		HideFlowScreens();
		DemoRun = RunState.NewRun(Pool);
		PickRunSeed();
		_suppress = HeldBits(); // the Space/A that pressed Restart must not fire a Special
		Draft = null;
		Duel = 1;
		Stage = DemoStage.Fighting;
		DuelRecordings.Clear();
		ReplaceMatch(NewMatch(DemoRun));
	}

	private void HideFlowScreens()
	{
		BindingCard.Dismiss();
		Rewards.Dismiss();
		CompleteScreen.Hide();
		DebugMenu?.Close();
	}

	/// <summary>A new Match (rematch, restart): rewire the HUD and shake, rebuild the AI, start a new recording.</summary>
	private void ReplaceMatch(Match m)
	{
		Match = m;
		_hud.View = new MatchHudView(m, 0);
		m.Impact += (_, e) => Shake.OnImpact(e);
		RebuildAi();
		StartRecording();
		Shake.Stop();
		_clock.Restart();
		Render();
	}
}
