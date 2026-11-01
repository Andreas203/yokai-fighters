using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Settings;
using YokaiFighters.Sim;
using YokaiFighters.Ui;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-175: on a rest stop (merchant and dojo) Esc / pad B before a choice goes back to the map with the stop spent
/// (<see cref="RunSession.LeaveNode"/>; provisional defaults the designer confirms at review); after a choice it does
/// nothing. The footer's Back chip reads "Map" (and goes once a choice is made). The title stays reachable through
/// the stop's pause menu (P / pad Start): Quit to title leaves the stop unspent for Continue (M5).
/// </summary>
public static class RestStopLeaveTests
{
	const string Scene = "res://scenes/ui/rest_stop.tscn";
	static string MapDir => ContentPaths.Data("map");

	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		try { body(); }
		finally { FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe; }
	}

	static bool Leads(RunSession s, string from, string to) => from == to || s.Map.Graph.Next(from).Any(n => Leads(s, n, to));

	/// <summary>Wins the entered duel with 300 left (first card picked) and completes it.</summary>
	static void Finish(RunSession s)
	{
		if (s.WinFight(300) is { } d) { var c = d.Offer.Cards[0]; Assert.True(d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null).Ok, "picked"); }
		Assert.True(s.CompleteNode().Ok, "completed");
	}

	/// <summary>A run standing at <paramref name="nodeId"/> (a stop) with 300 health; earlier stops passed, fights won.</summary>
	static RunSession AtNode(string nodeId)
	{
		var s = RunSession.Start(FightScene.LoadAbilityPool(), FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir), 5);
		for (; ; )
		{
			string pick = s.Reachable().First(id => Leads(s, id, nodeId));
			Assert.True(s.Enter(pick).Ok, "entered " + pick);
			if (pick == nodeId) break;
			if (s.Node!.Type is "merchant" or "dojo") Assert.True(s.CompleteNode().Ok, "a stop passed");
			else Finish(s);
		}
		s.Run.SetHealth(300);
		return s;
	}

	static RestStopScreen Open(Node runner, RunSession s)
	{
		var screen = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
		screen.LineSource = _ => "A line.";
		screen.BindRun(s, ControlScheme.Kihon);
		runner.AddChild(screen);
		return screen;
	}

	static void Close(Node runner, Node screen) { runner.RemoveChild(screen); screen.Free(); }

	static void Tap(Node n, Key k)
	{
		n.GetViewport().PushInput(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
		n.GetViewport().PushInput(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
	}

	static void Pad(Node n, JoyButton b)
	{
		n.GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = b, Pressed = true });
		n.GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = b, Pressed = false });
	}

	static void Back(Node n, bool pad) { if (pad) Pad(n, JoyButton.B); else Tap(n, Key.Escape); }

	/// <summary>The footer's action labels, in order.</summary>
	static string Labels(RestStopScreen screen)
	{
		var footer = screen.GetNode<KeyChipFooter>("Footer");
		return string.Join("|", footer.GetChildren().OfType<Label>().Where(l => l.Name.ToString().StartsWith("Action")).Select(l => l.Text));
	}

	static void Press(Button b) => b.EmitSignal(BaseButton.SignalName.Pressed);
	static Button Yes(PauseMenu m) => ((PaperDialog)m.ConfirmBox).ConfirmButton!;

	[Test]
	public static void Scene_EscOrPadB_BeforeAChoice_LeavesForTheMap_NoHealNoCoins_BothVariants(Node runner) => OnFixtures(() =>
	{
		foreach (var (stop, pad) in new[] { ("r4-merchant", false), ("r4-merchant", true), ("r7-dojo", false), ("r7-dojo", true) })
		{
			var s = AtNode(stop);
			int health = s.Run.Health, coins = s.Run.Coins;
			var screen = Open(runner, s);
			try
			{
				int left = 0, titles = 0; screen.Left += () => left++; screen.TitleRequested += () => titles++;
				Assert.True(screen.Model!.CanLeaveToMap, $"{stop}: no choice yet, the map is open");
				Back(runner, pad);
				Assert.True(left == 1 && titles == 0, $"{stop} ({(pad ? "pad B" : "Esc")}): left for the map, not the title");
				Assert.True(s.Node == null && s.Map.Visited.Last() == stop, $"{stop}: the stop is spent, the next row is picked");
				Assert.True(s.Run.Health == health && s.Run.Coins == coins, $"{stop}: no heal, no coins");
			}
			finally { Close(runner, screen); }
		}
	});

	[Test]
	public static void Scene_EscOrPadB_AfterAChoice_DoesNothing(Node runner) => OnFixtures(() =>
	{
		foreach (string what in new[] { "heal", "shop", "rest" })
		{
			var s = AtNode(what == "rest" ? "r7-dojo" : "r4-merchant");
			var screen = Open(runner, s);
			try
			{
				if (what == "shop") { Assert.True(screen.OpenSecond(), "shop chosen"); screen.ShopClosed(); }
				else Assert.True(screen.TakeFree(), what + " taken");
				ulong h = s.StateHash(); int health = s.Run.Health, coins = s.Run.Coins; string node = s.Node!.NodeId;
				int left = 0, titles = 0; screen.Left += () => left++; screen.TitleRequested += () => titles++;
				Assert.True(!screen.Model!.CanLeaveToMap && !screen.LeaveToMap(), $"{what}: leaving for the map is refused");
				Tap(runner, Key.Escape); Pad(runner, JoyButton.B);
				Assert.True(left == 0 && titles == 0, $"{what}: Esc / B ask for nothing");
				Assert.True(s.StateHash() == h && s.Node?.NodeId == node && s.Run.Health == health && s.Run.Coins == coins, $"{what}: nothing changed");
				Assert.True(screen.Leave() && left == 1 && s.Node == null, $"{what}: Continue still moves on");
			}
			finally { Close(runner, screen); }
		}
	});

	[Test]
	public static void Scene_Footer_ReadsMapBeforeAChoice_NoBackChipAfter_BothVariants(Node runner) => OnFixtures(() =>
	{
		InputGlyphs.Set(InputDevice.Keyboard);
		foreach (string stop in new[] { "r4-merchant", "r7-dojo" })
		{
			var s = AtNode(stop);
			var screen = Open(runner, s);
			try
			{
				string before = Labels(screen);
				Assert.True(before.Split('|').Contains("Map") && !before.Contains("Title"), $"{stop}: Back reads Map before a choice: {before}");
				Assert.True(before.EndsWith("Map|Pause"), $"{stop}: Map then Pause: {before}");
				var chips = screen.GetNode<KeyChipFooter>("Footer").ChipTexts();
				Assert.Equal("ESC|P", string.Join("|", chips[^2..]), $"{stop}: keyboard chips");
				Assert.True(screen.TakeFree(), "choice made");
				string after = Labels(screen);
				Assert.Equal("Continue|Forecast detail|Pause", after, $"{stop}: no Back chip once chosen (Esc does nothing)");
				InputGlyphs.Set(InputDevice.Pad);
				Assert.Equal("A|Y|START", string.Join("|", screen.GetNode<KeyChipFooter>("Footer").ChipTexts()), $"{stop}: pad chips");
				InputGlyphs.Set(InputDevice.Keyboard);
			}
			finally { Close(runner, screen); }
		}
	});

	[Test]
	public static void Scene_PauseKeys_OpenThePause_EscResumes_QuitToTitleAsksForTheTitle(Node runner) => OnFixtures(() =>
	{
		foreach (bool chosen in new[] { false, true })
		{
			var s = AtNode("r4-merchant");
			var screen = Open(runner, s);
			try
			{
				if (chosen) Assert.True(screen.TakeFree(), "healed");
				ulong h = s.StateHash();
				int left = 0, titles = 0; screen.Left += () => left++; screen.TitleRequested += () => titles++;
				Tap(runner, Key.P);
				Assert.True(screen.Paused && screen.PauseMenu!.Visible, $"chosen={chosen}: P opens the pause");
				Assert.Equal($"Row 4 of {s.Steps} · Merchant", UiFind.Get<Label>(screen.PauseMenu, "RunSub").Text, $"chosen={chosen}: the run strip names the stop");
				Tap(runner, Key.Escape);
				Assert.True(!screen.Paused && left == 0 && s.Node != null, $"chosen={chosen}: Esc resumes the pause, the stop is not left");
				var focus = runner.GetViewport().GuiGetFocusOwner();
				Assert.True(focus != null && screen.IsAncestorOf(focus) && !screen.PauseMenu.IsAncestorOf(focus), $"chosen={chosen}: focus back on the stop: {focus?.Name}");
				Pad(runner, JoyButton.Start);
				Assert.True(screen.Paused, $"chosen={chosen}: pad Start opens it");
				Tap(runner, Key.Down); Pad(runner, JoyButton.B); Tap(runner, Key.Tab);
				Assert.True(screen.Paused && left == 0 && s.Node != null && !screen.DetailVisible, $"chosen={chosen}: keys under the pause never reach the stop");
				Press(screen.PauseMenu.QuitButton);
				Assert.True(screen.PauseMenu.ConfirmBox.Visible && titles == 0, $"chosen={chosen}: Quit asks first");
				Press(Yes(screen.PauseMenu));
				Assert.True(titles == 1 && !screen.Paused && left == 0, $"chosen={chosen}: confirmed: the title is asked for once");
				Assert.True(s.StateHash() == h && s.Node != null, $"chosen={chosen}: the pause changes no run state");
			}
			finally { Close(runner, screen); }
		}
	});

	// ---- the boot flow ----

	static void WithFlow(Node runner, Action<GameFlow> body)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-leave-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = 77;
		flow.SaveFile = Path.Combine(dir, "run.dat");
		flow.ConfigureFight = f => f.ExternalDrive = true;
		try
		{
			runner.AddChild(flow);
			flow.Title.StartButton.EmitSignal(BaseButton.SignalName.Pressed);
			flow.ControlSelect.Start.EmitSignal(BaseButton.SignalName.Pressed);
			body(flow);
		}
		finally
		{
			runner.RemoveChild(flow); flow.QueueFree(); MenuSettings.Reset();
			FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe;
			try { Directory.Delete(dir, true); } catch (IOException) { }
		}
	}

	/// <summary>Walks the flow's run to <paramref name="nodeId"/> (fights won, earlier stops healed and left) and returns its screen.</summary>
	static RestStopScreen WalkTo(GameFlow flow, string nodeId)
	{
		var s = flow.Session!;
		for (; ; )
		{
			string id = s.Reachable().First(n => Leads(s, n, nodeId));
			Assert.True(flow.ChooseNode(id).Ok, "entered " + id);
			if (id == nodeId) return (RestStopScreen)flow.NodeScene!;
			if (flow.NodeScene is RestStopScreen early) Assert.True(early.TakeFree() && early.Leave(), "a stop passed");
			else Finish(s);
		}
	}

	[Test]
	public static void Boot_EscBeforeAChoice_LandsOnTheMap_SavedAsDone_ContinueResumesAtTheNextRow(Node runner) => WithFlow(runner, flow =>
	{
		foreach (var (stop, pad) in new[] { ("r4-merchant", false), ("r7-dojo", true) })
		{
			var screen = WalkTo(flow, stop);
			var s = flow.Session!;
			int health = s.Run.Health, coins = s.Run.Coins;
			string slots = string.Join("|", s.Run.Owned.Select(o => $"{o.Slot}:{o.Special.Data.Id}:{o.Special.Level}"));
			Back(flow, pad);
			Assert.True(flow.Router.Current == Screen.Map && flow.NodeScene == null && flow.Map.Visible, $"{stop}: on the map");
			Assert.True(s.Node == null && s.Map.Visited.Last() == stop && s.Run.Health == health && s.Run.Coins == coins, $"{stop}: spent, nothing gained");
			Assert.True(flow.ReturnToTitle() && flow.ContinueRun(), $"{stop}: quit on the map, Continue");
			var back = flow.Session!;
			Assert.True(flow.Router.Current == Screen.Map && flow.NodeScene == null && !back.Map.InNode, $"{stop}: Continue lands on the map (the leave was saved, M5)");
			Assert.Equal(string.Join(",", back.Map.Graph.Next(stop)), string.Join(",", back.Reachable()), $"{stop}: at the next-row choice");
			Assert.True(back.Run.Health == health && back.Run.Coins == coins
				&& string.Join("|", back.Run.Owned.Select(o => $"{o.Slot}:{o.Special.Data.Id}:{o.Special.Level}")) == slots, $"{stop}: health, coins and slots unchanged");
		}
	});

	[Test]
	public static void Boot_ShopEsc_BackToTheStop_ThenEscDoesNothing(Node runner) => WithFlow(runner, flow =>
	{
		var screen = WalkTo(flow, "r4-merchant");
		Assert.True(screen.OpenSecond() && flow.Shop != null, "the shop is open");
		Tap(flow, Key.Escape);
		Assert.True(flow.Shop == null && screen.Visible && flow.NodeScene == screen && flow.Router.Current == Screen.Fight, "Esc in the shop: back on the stop (YOK-104)");
		Tap(flow, Key.Escape);
		Assert.True(flow.NodeScene == screen && flow.Router.Current == Screen.Fight && flow.Session!.Node?.NodeId == "r4-merchant", "the shop was chosen: Esc on the stop does nothing");
	});

	[Test]
	public static void Boot_PauseQuitToTitle_ContinueBringsTheStopBackUnspent_M5(Node runner) => WithFlow(runner, flow =>
	{
		var screen = WalkTo(flow, "r4-merchant");
		int entry = flow.Session!.Run.Health;
		Assert.True(screen.TakeFree() && flow.Session.Run.Health > entry, "healed");
		Tap(flow, Key.P);
		Assert.True(screen.Paused, "the stop's pause is open");
		Press(screen.PauseMenu!.QuitButton); Press(Yes(screen.PauseMenu));
		Assert.True(flow.Router.Current == Screen.Title && flow.NodeScene == null, "Quit to title reaches the title from the stop");
		Assert.True(!flow.GetTree().Paused, "the tree is not left paused");
		Assert.True(flow.ContinueRun(), "Continue");
		var again = flow.NodeScene as RestStopScreen;
		Assert.True(again != null && again != screen && !again.Model!.ChoiceMade && flow.Session!.Run.Health == entry, "the stop again, unspent (M5)");
		Tap(flow, Key.P);
		Press(again.PauseMenu!.RestartButton); Press(Yes(again.PauseMenu));
		Assert.True(flow.Router.Current == Screen.Map && flow.NodeScene == null && flow.Session!.Map.Visited.Count == 0, "Restart run from the stop: a fresh run on the map");
	});
}
