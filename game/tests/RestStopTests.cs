using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Settings;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-96: the rest-stop screen. The Tanuki forecast (T6: before the lock from the current best special, after it from
/// <see cref="RunSession.LockedCopy"/>, the expected cost from one swappable method), the merchant's heal (R6) and the
/// dojo's rest (R7) taken once, refused choices changing nothing, the scene's controls, and the boot flow: a
/// merchant node runs this screen, and quitting mid-node then Continue shows it again with no choice taken (M5).
/// </summary>
public static class RestStopTests
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
		finally { FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe; RestStopScreenForecast.CostSource = null; }
	}

	static RunSession NewSession() =>
		RunSession.Start(FightScene.LoadAbilityPool(), FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir), 5);

	static void Finish(RunSession s, int healthLeft)
	{
		if (s.WinFight(healthLeft) is { } draft)
		{
			var card = draft.Offer.Cards[0];
			Assert.True(draft.Pick(0, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, "picked");
		}
		Assert.True(s.CompleteNode().Ok, "completed");
	}

	/// <summary>A run standing at its row-4 merchant with 300 health, or at its row-7 dojo.</summary>
	static RunSession AtStop(string nodeType, int health = 300)
	{
		var s = NewSession();
		for (; ; )
		{
			var next = s.Reachable();
			Assert.True(next.Count > 0, $"the walk reaches a {nodeType}");
			string pick = next.FirstOrDefault(id => s.Map.Graph.Node(id).Type == nodeType) ?? next[0];
			Assert.True(s.Enter(pick).Ok, "entered " + pick);
			if (s.Node!.Type == nodeType) break;
			if (s.Node.Type is "merchant" or "dojo") Assert.True(s.CompleteNode().Ok, "a stop passed");
			else Finish(s, health);
		}
		s.Run.SetHealth(health);
		return s;
	}

	// ---- the Tanuki forecast (T6) ----

	[Test]
	public static void Forecast_BeforeTheLock_NamesTheCurrentBestSpecial_ItsLevelAndTheCostFromOneMethod() => OnFixtures(() =>
	{
		var s = AtStop("merchant");
		Assert.True(!s.CopyLocked, "the lock is the Tanuki's node (T1): not yet");
		var want = CopyRule.Target(s.Run)!;
		var f = RestStopScreenForecast.For(s)!;
		Assert.True(f.SpecialId == want.Data.Id && f.Slot == want.Slot && f.Level == want.MoveLevel && f.RyoLevel == want.Level && !f.Locked, "the forecast is CopyRule.Target (T2)");
		Assert.True(f.Name.Length > 0, "it names the move");
		Assert.True(f.Cost == RestStopScreenForecast.PlaceholderCost && f.CostIsPlaceholder, "the stand-in cost is flagged as a placeholder");

		RestStopScreenForecast.CostSource = t => t.Data.Id == want.Data.Id ? 321 : null;
		var real = RestStopScreenForecast.For(s)!;
		Assert.True(real.Cost == 321 && !real.CostIsPlaceholder, "the cost comes from the one method, and is no longer called a placeholder");
		RestStopScreenForecast.CostSource = _ => null;
		Assert.True(RestStopScreenForecast.For(s)!.Cost == null, "no data yet reads as no figure");
	});

	[Test]
	public static void Forecast_AfterTheLock_ReadsTheLockedCopy_NotTheCurrentBest() => OnFixtures(() =>
	{
		var s = AtStop("boss");
		Assert.True(s.CopyLocked && s.LockedCopy != null, "entering the Tanuki's node took the T1 lock");
		var f = RestStopScreenForecast.For(s)!;
		var l = s.LockedCopy!;
		Assert.True(f.Locked && f.SpecialId == l.Data.Id && f.Slot == l.Slot && f.RyoLevel == l.Level && f.Level == l.MoveLevel, "after the lock: the forecast is LockedCopy");
	});

	// ---- the model: one choice per stop ----

	[Test]
	public static void Merchant_FreeHeal_IsTakenOnce_ARefusedSecondTakeChangesNothing() => OnFixtures(() =>
	{
		var s = AtStop("merchant");
		var m = RestStopScreenModel.Create(s)!;
		Assert.True(m.Kind == RestStopKind.Merchant && m.Row == 4 && m.Health == 300 && m.FreePercent == 25, "R6: merchant, row 4, 25%");
		Assert.True(m.Free == RestStopOption.Ready && m.Second == RestStopOption.ComingSoon && m.FreeGain == 250 && m.HealthAfterFree == 550, "preview +250 -> 550; the shop is not built yet");
		Assert.True(!m.Leave().Ok && s.Node != null, "no leaving without a choice (open question: skipping)");
		Assert.True(m.TakeFree().Ok && s.Run.Health == 550, "the heal");
		Assert.True(m.ChoiceMade && m.Free == RestStopOption.Taken && m.Second == RestStopOption.Closed && m.FreeGain == 250, "spent: the other card is closed");
		ulong before = s.StateHash();
		Assert.True(!m.TakeFree().Ok && s.StateHash() == before && s.Run.Health == 550, "a second take is refused and changes nothing");
		Assert.True(m.Leave().Ok && s.Node == null, "Continue completes the node");
		Assert.True(!m.Open && !m.TakeFree().Ok && s.Run.Health == 550, "a stop already left does nothing");
	});

	[Test]
	public static void Dojo_Rest_HealsThirtyPercent_OnceAndClampsAtFull() => OnFixtures(() =>
	{
		var s = AtStop("dojo", 900);
		var m = RestStopScreenModel.Create(s)!;
		Assert.True(m.Kind == RestStopKind.Dojo && m.Row == 7 && m.FreePercent == 30, "R7: dojo, row 7, 30%");
		Assert.True(m.HealthAfterFree == 1000 && m.FreeGain == 100, "the preview clamps at full health");
		Assert.True(m.TakeFree().Ok && s.Run.Health == 1000 && m.FreeGain == 100, "rested, +100 only");
		ulong before = s.StateHash();
		Assert.True(!m.TakeFree().Ok && s.StateHash() == before, "a second rest is refused");
		Assert.True(m.Free == RestStopOption.Taken && m.Second == RestStopOption.Closed, "one choice");
	});

	[Test]
	public static void Model_IsNullOffAStop() => OnFixtures(() =>
	{
		var s = NewSession();
		Assert.True(RestStopScreenModel.Create(s) == null, "no node entered");
		s.Enter("r1-apprentice");
		Assert.True(RestStopScreenModel.Create(s) == null, "a duel is not a stop");
	});

	// ---- the scene ----

	static RestStopScreen Open(Node runner, RunSession s)
	{
		var screen = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
		screen.LineSource = _ => "A line.";
		screen.BindRun(s, ControlScheme.Kihon);
		runner.AddChild(screen);
		return screen;
	}

	static Label L(Node n, string path) => n.GetNode<Label>(path);

	[Test]
	public static void Scene_Merchant_ShowsHealthCoinsForecastAndLocksTheShop_ThenSpendsTheChoice(Node runner) => OnFixtures(() =>
	{
		var s = AtStop("merchant");
		int coins = s.Run.Coins;
		var want = CopyRule.Target(s.Run)!;
		var screen = Open(runner, s);
		try
		{
			Assert.Equal("RYO · 300 / 1000", L(screen, "HealthLabel").Text, "health, as the HUD writes it");
			Assert.Equal(coins.ToString(), L(screen, "CoinChip/CoinValue").Text, "coins");
			Assert.Equal("Row 4 · the merchant's lantern", L(screen, "RowLine").Text, "row line");
			Assert.Equal(want.Data.Name, L(screen, "Forecast/SpecialName").Text, "the forecast names the copy target");
			Assert.Equal($"Lv {want.MoveLevel}", L(screen, "Forecast/LevelTag/LevelLabel").Text, "and its level");
			Assert.True(L(screen, "Forecast/CostValue").Text.StartsWith("~") && screen.GetNode<Control>("Forecast/Placeholder").Visible, "the expected cost, labelled a placeholder");
			Assert.True(!screen.FreeCard.CardDisabled && screen.SecondCard.CardDisabled && L(screen, "SecondCard/Box/Coming").Text == "Coming soon", "free heal open, shop disabled with its coming state");
			Assert.True(L(screen, "FreeCard/Box/Desc").Text.Contains("+250") && !screen.ContinueButton.Visible, "heal preview, no Continue yet");

			Assert.True(screen.TakeFree(), "take the heal");
			Assert.Equal("RYO · 550 / 1000", L(screen, "HealthLabel").Text, "health follows");
			Assert.True(screen.FreeCard.CardDisabled && screen.ContinueButton.Visible, "the choice is spent, Continue is shown");
			Assert.True(!screen.TakeFree() && s.Run.Health == 550, "a second take does nothing");

			bool left = false;
			screen.Left += () => left = true;
			Assert.True(screen.Leave() && left && s.Node == null, "Continue completes the node");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	[Test]
	public static void Scene_Dojo_RestIsLive_TrialIsPresentButDisabled(Node runner) => OnFixtures(() =>
	{
		var s = AtStop("dojo", 700);
		var screen = Open(runner, s);
		try
		{
			Assert.Equal("Row 7 · the dojo at the bend", L(screen, "RowLine").Text, "row line");
			Assert.Equal("Rest", L(screen, "FreeCard/Box/Top/Titles/Title").Text, "rest card");
			Assert.Equal("Training trial", L(screen, "SecondCard/Box/Top/Titles/Title").Text, "trial card");
			Assert.True(screen.SecondCard.CardDisabled, "the trial waits for YOK-98");
			Assert.True(screen.TakeFree() && s.Run.Health == 1000, "rest +300 clamps at 1000");
			Assert.True(L(screen, "Forecast/SpecialName").Text.Length > 0, "the forecast is on the dojo too");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	// ---- the boot flow ----

	[Test]
	public static void Boot_MerchantNodeRunsTheRestStop_QuitThenContinueShowsItAgainWithNoChoiceTaken(Node runner)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-reststop-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		string save = Path.Combine(dir, "run.dat");
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = 77;
		flow.SaveFile = save;
		flow.ConfigureFight = f => f.ExternalDrive = true;
		try
		{
			runner.AddChild(flow);
			flow.Title.StartButton.EmitSignal(BaseButton.SignalName.Pressed);
			flow.ControlSelect.Start.EmitSignal(BaseButton.SignalName.Pressed);
			var s = flow.Session!;
			while (true)
			{
				string id = s.Reachable()[0];
				Assert.True(flow.ChooseNode(id).Ok, "entered " + id);
				if (s.Node!.Type == "merchant") break;
				Assert.True(flow.NodeScene is FightScene, id + " is a fight");
				Finish(s, 300);
			}
			Assert.True(flow.NodeScene is RestStopScreen && flow.Fight == null && flow.Router.Current == Screen.Fight, "the merchant node runs the rest-stop screen, no fight");
			var screen = (RestStopScreen)flow.NodeScene!;
			Assert.True(screen.Model != null && screen.Model.Health == s.Run.Health, "bound to the run");
			int entry = s.Run.Health;
			Assert.True(screen.TakeFree() && s.Run.Health == entry + 250, "healed");

			Assert.True(flow.ReturnToTitle() && flow.Router.Current == Screen.Title && flow.NodeScene == null, "quit to the title");
			Assert.True(flow.ContinueRun(), "Continue");
			var again = flow.NodeScene as RestStopScreen;
			Assert.True(again != null && again != screen, "the stop is shown again");
			Assert.True(again!.Model!.Free == RestStopOption.Ready && !again.Model.ChoiceMade && !again.ContinueButton.Visible, "no choice taken (M5)");
			Assert.True(flow.Session!.Run.Health == entry, "the entry-state health: the heal was not saved");

			Assert.True(again.TakeFree() && again.Leave(), "take it and leave");
			Assert.True(flow.Router.Current == Screen.Map && flow.NodeScene == null, "leaving advances to the map");
			Assert.True(flow.Session!.Run.Health == entry + 250 && flow.Session.Map.Visited.Last() == "r4-merchant", "healed once, the stop is done");
		}
		finally
		{
			runner.RemoveChild(flow);
			flow.QueueFree();
			MenuSettings.Reset();
			FightScene.Sources = saved;
			FightScene.Run = savedRun;
			FightScene.Opponent = savedFoe;
			try { Directory.Delete(dir, true); } catch (IOException) { }
		}
	}
}
