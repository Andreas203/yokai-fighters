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
/// <see cref="RunSession.LockedCopy"/>, the expected cost = the copy's damage per hit from data, YOK-173), the merchant's heal (R6) and the
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
		finally { FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe; }
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

	static bool Leads(RunSession s, string from, string to) =>
		from == to || s.Map.Graph.Next(from).Any(n => Leads(s, n, to));

	/// <summary>A run standing at the named node (a stop), every earlier stop passed and every fight won.</summary>
	static RunSession AtNode(string nodeId, int health = 300)
	{
		var s = NewSession();
		for (; ; )
		{
			var next = s.Reachable();
			string pick = next.First(id => Leads(s, id, nodeId));
			Assert.True(s.Enter(pick).Ok, "entered " + pick);
			if (pick == nodeId) break;
			if (s.Node!.Type is "merchant" or "dojo") Assert.True(s.CompleteNode().Ok, "a stop passed");
			else Finish(s, health);
		}
		s.Run.SetHealth(health);
		return s;
	}

	// ---- the Tanuki forecast (T6) ----

	[Test]
	public static void Forecast_BeforeTheLock_NamesTheCurrentBestSpecial_ItsLevelAndItsDamagePerHit() => OnFixtures(() =>
	{
		var s = AtStop("merchant");
		Assert.True(!s.CopyLocked, "the lock is the Tanuki's node (T1): not yet");
		var want = CopyRule.Target(s.Run)!;
		var f = RestStopScreenForecast.For(s)!;
		Assert.True(f.SpecialId == want.Data.Id && f.Slot == want.Slot && f.Level == want.MoveLevel && f.RyoLevel == want.Level && !f.Locked, "the forecast is CopyRule.Target (T2)");
		Assert.True(f.Name.Length > 0, "it names the move");
		Assert.True(f.Cost == want.Build().Damage && f.Cost > 0, "the cost is the copy's damage per hit, from data (T6)");
	});

	[Test]
	public static void Forecast_AfterTheLock_ReadsTheLockedCopy_NotTheCurrentBest() => OnFixtures(() =>
	{
		var s = AtStop("boss");
		Assert.True(s.CopyLocked && s.LockedCopy != null, "entering the Tanuki's node took the T1 lock");
		var l = s.LockedCopy!;
		// Ryo moves on after the lock: level the other specials until one overtakes it (T2/T3) ...
		foreach (var sl in s.Run.Owned.Select(o => o.Slot).Where(x => x != l.Slot).ToList())
			while (s.Run.CanLevelUp(sl) && CopyRule.Target(s.Run)!.Slot == l.Slot) s.Run.LevelUp(sl);
		// ... or, if the locked one is out of reach, it is replaced by another special of its slot (A6).
		if (CopyRule.Target(s.Run)!.Slot == l.Slot)
			s.Run.Equip(s.Pool.Specials.First(sp => sp.Slot == l.Slot && sp.Id != l.Data.Id));
		Assert.True(CopyRule.Target(s.Run)!.Data.Id != l.Data.Id,"the current best is now a different special than the lock");
		var f = RestStopScreenForecast.For(s)!;
		Assert.True(f.Locked && f.SpecialId == l.Data.Id && f.Slot == l.Slot && f.RyoLevel == l.Level && f.Level == l.MoveLevel, "after the lock: the forecast is still LockedCopy, not the new best");
	});

	// ---- the model: one choice per stop ----

	[Test]
	public static void Merchant_FreeHeal_IsTakenOnce_ARefusedSecondTakeChangesNothing() => OnFixtures(() =>
	{
		var s = AtStop("merchant");
		var m = RestStopScreenModel.Create(s)!;
		Assert.True(m.Kind == RestStopKind.Merchant && m.Row == 4 && m.Health == 300 && m.FreePercent == 25, "R6: merchant, row 4, 25%");
		Assert.True(m.Free == RestStopOption.Ready && m.Second == RestStopOption.Ready && m.FreeGain == 250 && m.HealthAfterFree == 550, "preview +250 -> 550; the shop (YOK-104) is open too");
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

	[Test]
	public static void Model_BoundToItsOwnStop_ALaterStopOfTheSessionIsNotIt() => OnFixtures(() =>
	{
		var t = AtNode("r4-merchant");
		var old = RestStopScreenModel.Create(t)!;
		Assert.True(old.TakeFree().Ok && old.Leave().Ok, "row 4 taken and left");
		foreach (string id in new[] { "r5-elder-oni", "r6-kitsune", "r7-merchant" })
		{
			Assert.True(t.Enter(id).Ok, "entered " + id);
			if (id != "r7-merchant") Finish(t, 300);
		}
		ulong before = t.StateHash();
		int hp = t.Run.Health;
		Assert.True(!old.Open && !old.TakeFree().Ok && !old.Leave().Ok, "the row-4 model does nothing at row 7");
		Assert.True(t.StateHash() == before && t.Run.Health == hp && t.Node!.NodeId == "r7-merchant", "and row 7's choice stays unspent, the node stays entered");
		var m7 = RestStopScreenModel.Create(t)!;
		Assert.True(m7.Row == 7 && m7.Kind == RestStopKind.Merchant && m7.Free == RestStopOption.Ready && m7.FreePercent == 25, "a model made for row 7 can take it");
	});

	[Test]
	public static void Merchant_Row7_FullHealthHealIsPlusZero_AndTheStopIsStillSpent() => OnFixtures(() =>
	{
		var s = AtNode("r7-merchant", 1000);
		var m = RestStopScreenModel.Create(s)!;
		Assert.True(m.Row == 7 && m.FreeGain == 0 && m.HealthAfterFree == 1000, "at full health the preview reads +0");
		Assert.True(m.TakeFree().Ok && s.Run.Health == 1000 && m.FreeGain == 0 && m.ChoiceMade, "taken: nothing healed, the choice is spent anyway");
		Assert.True(!m.TakeFree().Ok && m.Leave().Ok, "no second take, then leave");
	});

	[Test]
	public static void Dojo_QuitThenContinue_ShowsTheDojoAgainWithNoRestTaken_M5(Node runner)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-reststop-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		var savedFoe = FightScene.Opponent;
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
			var s = flow.Session!;
			while (true)
			{
				string id = s.Reachable().First(n => Leads(s, n, "r7-dojo"));
				Assert.True(flow.ChooseNode(id).Ok, "entered " + id);
				if (id == "r7-dojo") break;
				if (flow.NodeScene is RestStopScreen early) Assert.True(early.TakeFree() && early.Leave(), "a stop passed");
				else Finish(s, 300);
			}
			var screen = (RestStopScreen)flow.NodeScene!;
			int entry = s.Run.Health;
			Assert.True(screen.TakeFree() && s.Run.Health > entry, "rested");
			Assert.True(flow.ReturnToTitle() && flow.ContinueRun(), "quit and Continue");
			var again = (RestStopScreen)flow.NodeScene!;
			Assert.True(again != screen && again.Model!.Kind == RestStopKind.Dojo && !again.Model.ChoiceMade && !again.ContinueButton.Visible, "the dojo again, no choice taken (M5)");
			Assert.True(flow.Session!.Run.Health == entry, "entry-state health");
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
			Assert.Equal($"{RestStopScreenForecast.ExpectedCost(want)} per hit", L(screen, "Forecast/CostValue").Text, "the expected cost: the copy's damage per hit (T6)");
			Assert.True(screen.GetNodeOrNull("Forecast/Placeholder") == null && !L(screen, "Forecast/CostMeaning").Text.Contains("fights so far"), "no placeholder label, no fight-history wording (YOK-173)");
			Assert.True(!screen.FreeCard.CardDisabled && !screen.SecondCard.CardDisabled && L(screen, "SecondCard/Box/Coming").Text == "", "free heal open, the shop card open too (YOK-104)");
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
	public static void Scene_Portrait_MerchantShowsHisPlateBust_DojoKeepsThePlaceholder(Node runner) => OnFixtures(() =>
	{
		foreach (var (kind, merchant) in new[] { ("merchant", true), ("dojo", false) })
		{
			var screen = Open(runner, AtStop(kind));
			try
			{
				var art = screen.GetNode<TextureRect>("Portrait/Art");
				var decor = screen.GetNode<RestStopScreenDecor>("Decor");
				if (merchant)
				{
					var bust = art.Texture as AtlasTexture;
					Assert.True(art.Visible && bust != null && bust.Atlas == ShopArt.Portrait && bust.Region == RestStopScreen.MerchantBust, "the merchant shows the shop's plate, cropped to the bust");
					Assert.True(new Rect2(Vector2.Zero, bust!.Atlas.GetSize()).Encloses(bust.Region), "the bust lies inside the plate");
					Assert.True(!L(screen, "Portrait/PortraitLabel").Visible && decor.PortraitFilled, "no caption and no dashed box behind the art");
				}
				else
				{
					Assert.True(!art.Visible && art.Texture == null, "the dojo master has no art yet");
					Assert.True(L(screen, "Portrait/PortraitLabel").Visible && L(screen, "Portrait/PortraitLabel").Text == "dojo master (art to come)" && !decor.PortraitFilled, "the dojo keeps the labelled placeholder");
				}
			}
			finally { runner.RemoveChild(screen); screen.Free(); }
		}
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

	[Test]
	public static void Scene_Row7MerchantShowsNoRow4StoryLine_AndNoForecastClearsItsLabels(Node runner) => OnFixtures(() =>
	{
		var s = AtNode("r7-merchant");
		var screen = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
		screen.BindRun(s, ControlScheme.Kihon);
		runner.AddChild(screen);
		try
		{
			string line = L(screen, "LineBox/Box/Line").Text;
			Assert.True(!line.Contains("I'd love") && line.Contains("S3"), "row 7 gets a labelled placeholder, not the row-4 card");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }

		// Ryo owns no special (a state T5 makes unreachable in play): the forecast has nothing to say and says nothing stale
		var walked = AtNode("r4-merchant");
		var run = RunState.Restore(500, Array.Empty<(SpecialSlot, OwnedSpecial)>());
		run.SetMap(walked.Map);
		var bare = RunSession.Restore(run, walked.Pool, NodeSceneTable.Default(MapDir), 5, false, null);
		var empty = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
		empty.LineSource = _ => "A line.";
		empty.BindRun(bare, ControlScheme.Kihon);
		runner.AddChild(empty);
		try
		{
			Assert.True(RestStopScreenForecast.For(bare) == null, "no forecast");
			Assert.True(!empty.GetNode<Control>("Forecast/LevelTag").Visible && empty.GetNodeOrNull("Forecast/Placeholder") == null, "no level tag, no placeholder");
			Assert.True(L(empty, "Forecast/CostValue").Text == "" && !L(empty, "Forecast/LockNote").Text.Contains("Locks when you enter") && !L(empty, "Forecast/CopyNote").Text.StartsWith("Copied at your level"), "no stale cost, lock or copy line");
		}
		finally { runner.RemoveChild(empty); empty.Free(); }
	});

	[Test]
	public static void Forecast_EveryMoveNameFitsItsColumn_TheRulesEightAndThePool(Node runner)
	{
		var screen = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
		runner.AddChild(screen);
		try
		{
			var label = screen.GetNode<Label>("Forecast/SpecialName");
			var font = label.GetThemeFont("font");
			var names = new System.Collections.Generic.List<string> { "Spirit Wave", "Rising Talisman", "Foxfire", "Fox Mirage", "Iron-Club Charge", "Oni Quake", "River Grab", "Shell Spin" };
			names.AddRange(FightScene.LoadAbilityPool().Specials.Select(sp => sp.Name.Length > 0 ? sp.Name : sp.Id));
			foreach (string n in names.Distinct())
				Assert.True(RestStopScreen.NameFits(font, n, label.Size.X, label.Size.Y), $"'{n}' fits the forecast column ({label.Size.X}x{label.Size.Y})");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	}
}
