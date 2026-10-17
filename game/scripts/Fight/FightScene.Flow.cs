using System;
using System.Collections.Generic;
using Godot;
using YokaiFighters.Replay;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Fight;

/// <summary>Where the YOK-39 demo is: in a duel, or on one of the screens between and after duels.</summary>
public enum DemoStage
{
	Fighting, Binding, Reward, Complete, Lost,
	/// <summary>YOK-77: the node is completed and the run goes on; the boot flow is taking this scene away for the map.</summary>
	Left,
}

/// <summary>
/// YOK-48 demo flow (YOK-39 vertical slice): the scene starts in Ryo vs Kitsune on a fresh run (C1, 1,000 health;
/// the title and control select come before it, see <see cref="Flow.GameFlow"/>),
/// then route on the result. Loss: the HUD lose screen (data/story/lose-screen.json), Restart = a fresh run.
/// Win: KO slow-down (V4) → binding line card → reward screen (three cards from the beaten yokai, A7) → the pick
/// applied to the run → rematch with health left + 50, capped at 1,000 (R3). After the rematch: win = "demo
/// complete" card, loss = lose screen; every Restart starts the first fight again. Each duel gets its own recording
/// (YOK-49), stamped with the run. Debug builds: <b>F9</b> opens the debug menu (Kitsune temperament, restart run),
/// pausing the fight; <c>--temperament=patient</c> on the command line picks one before the first fight.
/// <para>
/// YOK-77: the scene plays one node of a <see cref="RunSession"/> and only calls it (<c>WinFight</c>,
/// <c>CompleteNode</c>, <c>LoseFight</c>). The opponent, its Y4 reaction row and whether a win binds come from the
/// node's <see cref="NodeContext"/>. Hosted (the boot flow set <see cref="Session"/>): after the node the flow
/// returns to the map, and Restart asks the host for a new run. On its own (tests, captures, the harness, the
/// editor, <c>--fight</c>): it starts a session on <c>data/map/demo-map.json</c>, the two-duel demo above, and
/// walks it by itself, so the demo is one map of the same flow.
/// </para>
/// </summary>
public partial class FightScene
{
	public const Key DebugMenuKey = Key.F9;
	/// <summary>The map the scene walks by itself when no session was handed to it.</summary>
	public const string DemoMapFile = "demo-map.json";
	/// <summary>Placeholder end card for a won run (the Tanuki fight and its win card are not built).</summary>
	public const string RunWonTitle = "Run complete", RunWonText = "Placeholder end card: the run's last node is cleared.";

	/// <summary>
	/// YOK-77: the run this scene plays a node of. Set it before the scene enters the tree with a node already
	/// entered (the boot flow); left null, the scene starts its own on <see cref="DemoMapFile"/>.
	/// </summary>
	public RunSession? Session { get; set; }
	/// <summary>True when the scene made its own session: it then picks the next node and restarts by itself.</summary>
	public bool OwnsSession { get; private set; }
	/// <summary>The node being fought (opponent, Y4 row, binds).</summary>
	public NodeContext Node { get; private set; } = null!;
	/// <summary>YOK-90 (Y3): the twist of the fight on screen (the HUD reminder), or null off an elder node.</summary>
	public ElderTwist? Twist => Match?.P2.Twist;
	/// <summary>Hosted only: Restart was pressed (lose screen, end card, pause, debug menu); the host starts a new run.</summary>
	public event Action? RunRestartRequested;
	/// <summary>Fights in a whole walk of the run's map (2 for the demo).</summary>
	public int Duels => Session!.Steps;

	/// <summary>A session on a map file in data/map/ with the node-scene table next to it. <paramref name="run"/>: an existing run to walk it with (given the map if it has none).</summary>
	public static RunSession NewSession(string mapFile, uint seed, AbilityPool? pool = null, RunState? run = null)
	{
		pool ??= LoadAbilityPool();
		string dir = ContentPaths.Data("map");
		var graph = new FileMapSource(System.IO.Path.Combine(dir, mapFile)).Load();
		run ??= RunState.NewRun(pool);
		if (run.Map is null || run.Map.IsFinished) run.StartMap(graph);
		return new RunSession(run, pool, NodeSceneTable.Default(dir), seed) { Trials = LoadTrials() };
	}

	/// <summary>YOK-98, R7: the dojo's trials from data/trials/ (none when the folder is absent; a broken file is logged and offers none).</summary>
	public static TrialData[] LoadTrials()
	{
		try { return TrialLoader.LoadDirectory(ContentPaths.Data("trials")); }
		catch (Exception e) when (e is FormatException or System.IO.IOException or System.Text.Json.JsonException)
		{
			GD.PushWarning($"FightScene: no dojo trials: {e.Message}");
			return Array.Empty<TrialData>();
		}
	}

	/// <summary>Seed for the reward draft (duel n uses RunSeed + n), so one run offers the same cards on replay.
	/// A fresh run picks a new seed unless <see cref="FixedRunSeed"/> is set (tests, harness).</summary>
	public uint RunSeed { get; set; } = 48;
	/// <summary>Set to pin every run to one seed; null = a new random seed per run (designer decision, YOK-48).</summary>
	public uint? FixedRunSeed { get; set; }

	private void PickRunSeed() => RunSeed = FixedRunSeed ?? (uint)System.Random.Shared.Next();
	public DemoStage Stage { get; private set; } = DemoStage.Fighting;
	/// <summary>
	/// The scheme P1 plays this run with, set before the scene enters the tree (the boot flow's control select,
	/// <see cref="Flow.GameFlow"/>); null = <see cref="DefaultScheme"/>. Applied to every duel of the run, restarts included.
	/// </summary>
	public ControlScheme? PlayerScheme { get; set; }
	/// <summary>Raised by <see cref="RequestTitle"/>; the boot flow frees this scene and shows the title.</summary>
	public event Action? TitleRequested;
	/// <summary>Leave the fight for the title menu (the pause menu's "Quit to title" calls this). False when nothing is
	/// listening, i.e. the scene was opened on its own (tests, captures, the editor).</summary>
	public bool RequestTitle()
	{
		if (TitleRequested == null) return false;
		TitleRequested.Invoke();
		return true;
	}
	/// <summary>Buttons held when the fight was entered (Start Game) or a Restart was pressed: ignored as P1 input until released, so Space does not fire a Special.</summary>
	private InputBits _suppress;
	/// <summary>Which fight of the walk this is: 1 = the first node (the demo: 1 = the first fight, 2 = the rematch).</summary>
	public int Duel { get; private set; } = 1;
	/// <summary>The run this scene plays: the session's (on its own: fresh at boot and on every Restart; <see cref="Run"/> if a caller set one).</summary>
	public RunState DemoRun => Session!.Run;
	public AbilityPool Pool { get; private set; } = null!;
	/// <summary>The current reward offer (from the beaten yokai), while the binding and reward screens show.</summary>
	public DraftRewardSource? Draft { get; private set; }
	/// <summary>Finished recordings of this run's duels, in order (cleared on Restart).</summary>
	public List<ReplayRecording> DuelRecordings { get; } = new();
	public bool FlowPaused => DebugMenu?.Visible == true || Paused || TwistCard?.Showing == true;

	public StoryCard BindingCard { get; private set; } = null!;
	/// <summary>YOK-139 (Y3): the elder-twist intro card shown on entering an elder node; the fight waits behind it.</summary>
	public StoryCard? TwistCard { get; private set; }
	/// <summary>YOK-139 (Y3, YOK-106): this node is Ryo's first meeting with its elder (<c>GameFlow.FirstMeeting</c>); set by the host before the scene enters the tree. The callout shows only then.</summary>
	public bool FirstMeeting { get; set; }
	public RewardScreen Rewards { get; private set; } = null!;
	public DemoCompleteScreen CompleteScreen { get; private set; } = null!;
	public DemoDebugMenu? DebugMenu { get; private set; }
	private bool _menuHeld;

	/// <summary>Called first in _Ready: the run, and the first fight built from it.</summary>
	private void SetUpRun()
	{
		if (Session is { } hosted) { Pool = hosted.Pool; RunSeed = hosted.Seed; }
		else
		{
			Pool = LoadAbilityPool();
			PickRunSeed();
			OwnsSession = true;
			Session = NewSession(DemoMapFile, RunSeed, Pool, Run);
		}
		BeginNode();
		Match = InTrial ? NewTrialMatch() : NewDuel(); // YOK-98: a dojo trial plays on this same loop
	}

	/// <summary>Takes the session's entered node (on its own: enters the next one first): opponent, fight number, the log of stand-ins.</summary>
	private void BeginNode()
	{
		if (Session!.Node is null)
		{
			if (!OwnsSession) throw new InvalidOperationException("FightScene: the run session has no entered node (RunSession.Enter before adding the scene)");
			var step = Session.Enter(Session.Reachable()[0]);
			if (!step.Ok) throw new InvalidOperationException($"FightScene: {step.Error}");
		}
		Node = Session.Node!;
		Opponent = Node.Fighter;
		Duel = Session.Step;
		GD.Print($"FightScene: node '{Node.NodeId}' ({Node.Type}, row {Node.Row}): vs {Node.Fighter}, Y4 {Node.Tier} {Node.ReactionFrames}f{(Node.StandIn ? " [stand-in]" : "")}");
		foreach (string note in Node.Notes) GD.Print("FightScene: " + note);
		if (Profiles.Length > 0) LoadNodeProfiles(); // a later node of a self-walked run (the first one loads in SetUpAi)
	}

	/// <summary>The next duel of <see cref="DemoRun"/>, with P1 on the scheme chosen for this run.</summary>
	private Match NewDuel()
	{
		Match m = NewMatch(DemoRun, Session); // P2 = Opponent, set from the node in BeginNode
		if (PlayerScheme is { } scheme) m.P1.Input.Scheme = scheme;
		if (Twists.Apply(m, Node) is { } twist) // YOK-90: from the node's elder, whoever stands in
			GD.Print($"FightScene: elder twist '{twist.Id}' ({twist.Effect}) on {Node.Fighter}{(Node.StandIn ? " [stand-in]" : "")}");
		return m;
	}

	/// <summary>Fills the demo-complete screen from data/story/demo-complete.json; keeps the placeholder (and warns) if missing or bad.</summary>
	private void ApplyDemoCompleteCard()
	{
		if (!OwnsSession) { CompleteScreen.SetText(RunWonTitle, RunWonText); return; } // a real run: placeholder until the win card exists
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
		Rewards.Picked += (_, _) => FinishNode();
		CompleteScreen = GetNode<DemoCompleteScreen>("Flow/Complete");
		CompleteScreen.RestartRequested += RestartRun;
		ApplyDemoCompleteCard();
		TwistCard = GetNodeOrNull<StoryCard>("Flow/TwistCard");
		if (TwistCard != null) TwistCard.Finished += OnTwistCardDone;
		ShowTwistIntro();
		if (!ExternalDrive) _suppress = HeldBits(); // entered from a menu: the Space/A that pressed Start Game must not fire a Special
		if (!OS.IsDebugBuild()) { GetNode("DebugMenuLayer").QueueFree(); return; } // the debug menu is for debug builds only
		DebugMenu = GetNode<DemoDebugMenu>("DebugMenuLayer/DebugMenu");
		DebugMenu.Bind(this);
		foreach (string arg in OS.GetCmdlineUserArgs())
			if (arg.StartsWith("--temperament=", StringComparison.Ordinal)) SetTemperament(arg["--temperament=".Length..]);
	}

	/// <summary>
	/// Y3: entering an elder node (hosted by a run) shows the twist's intro card first, and the fight starts when it is
	/// confirmed. The words come from story data through <see cref="TwistCopy"/>; the elder, its twist and whether this
	/// is a first meeting are all read, never decided here.
	/// </summary>
	private void ShowTwistIntro()
	{
		if (TwistCard == null || OwnsSession || !Node.Elder || Twist is not { } twist) return;
		string name = Story.StoryLibrary.DisplayName(Opponent);
		var line = TwistCopy.Intro(twist, TwistCopy.Story, name);
		TwistCard.Display(line.Text, $"ELDER TWIST · ROW {Node.Row}", twist.Icon, "E");
	}

	private void OnTwistCardDone()
	{
		_suppress = HeldBits(); // the Enter/Space/A that closed the card must not fire a Special
		if (!FirstMeeting || Twist is not { } twist) return;
		_hud.ShowTwistCallout(TwistCopy.Callout(twist, TwistCopy.Story, Story.StoryLibrary.DisplayName(Opponent)).Text, twist.Icon);
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

	private InputBits HeldBits() => HeldBitsSource?.Invoke() ?? InputDevices.Read(0).Bits;

	/// <summary>P1's live input minus anything still held from a screen button (Start, Restart).</summary>
	private FighterInput P1Live()
	{
		InputBits raw = HeldBits();
		_suppress &= raw;
		return new FighterInput(raw & ~_suppress);
	}

	/// <summary>Test seam: the mask applied to a raw P1 read of <paramref name="raw"/>, advancing the release tracking.</summary>
	public InputBits SuppressedInputForTest(InputBits raw) { _suppress &= raw; return raw & ~_suppress; }
	/// <summary>Test seam: pretend these were held when the fight was entered.</summary>
	public void ArmSuppressForTest(InputBits held) => _suppress = held;

	private void PollFlowKeys()
	{
		if (DebugMenu == null) return;
		bool down = Input.IsPhysicalKeyPressed(DebugMenuKey);
		if (down && !_menuHeld && !Paused) ToggleDebugMenu();
		_menuHeld = down;
	}

	/// <summary>After each sim step: route once when a duel ends.</summary>
	private void CheckDuelOver()
	{
		if (InTrial) { CheckTrial(); return; } // YOK-98: attempts, not a duel result
		if (Stage != DemoStage.Fighting || Match.Phase != MatchPhase.Over) return;
		if (Recorder is { } rec) { DuelRecordings.Add(rec.Finish(Match)); Recorder = null; }
		var session = Session!;
		if (session.IsOver) // debug R replayed a duel of a run that already ended: show the result, the run stays ended
		{
			Stage = Match.Winner != 0 ? DemoStage.Lost : DemoStage.Complete;
			if (Stage == DemoStage.Complete) CompleteScreen.Show();
			return;
		}
		if (Match.Winner != 0) { Stage = DemoStage.Lost; session.LoseFight(); return; } // the run ends; the HUD shows the lose screen (double KO counts as a loss)
		// R3 (health left + 50, capped at 1,000) and the A7 offer when the node binds; null = nothing to pick (last node, stand-in stop).
		var offer = session.Draft ?? session.WinFight(Match.P1.Health);
		if (offer is null) { FinishNode(); return; }
		Draft = new DraftRewardSource(offer, DemoRun);
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

	/// <summary>
	/// The node is done (any pick is already on the run, DraftRewardSource.Pick). Last node: the end card. Hosted:
	/// the boot flow hears <see cref="RunSession.NodeCompleted"/> and shows the map. On its own: straight into the
	/// next node with the pick and the carried health.
	/// </summary>
	private void FinishNode()
	{
		Draft = null;
		var done = Session!.CompleteNode();
		if (!done.Ok) { GD.PushError($"FightScene: node '{Node.NodeId}' not completed: {done.Error}"); return; }
		if (Session.Ended is { Won: true }) { Stage = DemoStage.Complete; CompleteScreen.Show(); return; }
		if (!OwnsSession) { Stage = DemoStage.Left; return; }
		BeginNode();
		Stage = DemoStage.Fighting;
		ReplaceMatch(NewDuel());
	}

	/// <summary>Restart from any end screen (and the debug menu): a fresh run. Hosted: the host starts it (back to the map). On its own: back to the first fight.</summary>
	public void RestartRun()
	{
		if (Paused) PauseMenu!.Close(); // a restart from anywhere leaves no menu up
		HideFlowScreens();
		if (!OwnsSession && RunRestartRequested is { } host) { host(); return; }
		PickRunSeed();
		OwnsSession = true;
		Session = NewSession(DemoMapFile, RunSeed, Pool);
		BeginNode();
		_suppress = HeldBits(); // the Space/A that pressed Restart must not fire a Special
		Draft = null;
		Stage = DemoStage.Fighting;
		DuelRecordings.Clear();
		ReplaceMatch(NewDuel());
	}

	private void HideFlowScreens()
	{
		BindingCard.Dismiss();
		TwistCard?.Dismiss();
		_hud.ClearTwistCallout();
		Rewards.Dismiss();
		CompleteScreen.Hide();
		DebugMenu?.Close();
	}

	/// <summary>A new Match (rematch, restart): rewire the HUD and shake, rebuild the AI, start a new recording.</summary>
	private void ReplaceMatch(Match m)
	{
		_hud.ClearTwistCallout(); // a new match's ticks restart at 0: the old callout must not come back
		Match = m;
		_lastP1 = InputBits.None;
		_hud.View = new MatchHudView(m, 0);
		m.Impact += (_, e) => Shake.OnImpact(e);
		SetUpTutorial(); // YOK-92: the new node may or may not be guided
		RebuildAi();
		StartRecording();
		Shake.Stop();
		_clock.Restart();
		Render();
	}
}
