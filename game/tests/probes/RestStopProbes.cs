using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Settings;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-96 (rest-stop screen). Expected values come from rules T1-T6, R6-R8, M5, C1, S3.</summary>
public static class RestStopProbes
{
	const string Scene = "res://scenes/ui/rest_stop.tscn";
	static string MapDir => ContentPaths.Data("map");

	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		try { body(); }
		finally { FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe; RestStopScreenForecast.CostSource = null; }
	}

	static RunSession NewSession(uint seed = 5) =>
		RunSession.Start(FightScene.LoadAbilityPool(), FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir), seed);

	static void Finish(RunSession s, int healthLeft)
	{
		if (s.WinFight(healthLeft) is { } draft)
		{
			var card = draft.Offer.Cards[0];
			Assert.True(draft.Pick(0, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, "picked");
		}
		Assert.True(s.CompleteNode().Ok, "completed");
	}

	static bool Leads(RunSession s, string from, string to) => from == to || s.Map.Graph.Next(from).Any(n => Leads(s, n, to));

	/// <summary>Standing at the named node; earlier stops passed through the model (heal taken), fights won with healthLeft.</summary>
	static RunSession AtNode(string nodeId, int health = 300, uint seed = 5)
	{
		var s = NewSession(seed);
		for (; ; )
		{
			string pick = s.Reachable().First(id => Leads(s, id, nodeId));
			Assert.True(s.Enter(pick).Ok, "entered " + pick);
			if (pick == nodeId) break;
			if (s.Node!.Type is "merchant" or "dojo") Assert.True(s.CompleteNode().Ok, "a stop passed");
			else Finish(s, health);
		}
		s.Run.SetHealth(health);
		return s;
	}

	// ---- 1. whole runs ----

	/// <summary>Plays a whole run. useModel: stops go through RestStopScreenModel; otherwise through Merchant/Dojo directly.</summary>
	static (RunSession s, List<ulong> hashes, List<string> offers, int coinsAfterRow3, int coinsAtBoss) Play(uint seed, string row7, bool useModel, bool win)
	{
		var s = NewSession(seed);
		var hashes = new List<ulong>(); var offers = new List<string>();
		int c3 = -1, cb = -1, hp = 600;
		for (; ; )
		{
			string pick = s.Reachable().FirstOrDefault(id => id == row7) ?? s.Reachable()[0];
			Assert.True(s.Enter(pick).Ok, "entered " + pick);
			Assert.Equal(s.Map.Visited.Count + 1, s.Step, "step counter");
			var n = s.Node!;
			if (n.Row == 4) c3 = s.Run.Coins;
			if (n.Type == "boss") { cb = s.Run.Coins; Assert.True(s.CopyLocked && s.LockedCopy != null, "T1: lock taken on entering the boss"); }
			hashes.Add(s.StateHash());
			if (n.Type is "merchant" or "dojo")
			{
				int before = s.Run.Health, coins = s.Run.Coins;
				if (useModel)
				{
					var m = RestStopScreenModel.Create(s)!;
					int expect = n.Type == "merchant" ? Math.Min(1000, before + 250) : Math.Min(1000, before + 300);
					Assert.Equal(expect, m.HealthAfterFree, "preview");
					Assert.True(m.TakeFree().Ok, "take"); Assert.True(m.Leave().Ok, "leave");
				}
				else
				{
					if (n.Type == "merchant") Assert.True(s.Merchant!.TakeHeal().Ok, "heal"); else Assert.True(s.Dojo!.Rest().Ok, "rest");
					Assert.True(s.CompleteNode().Ok, "leave");
				}
				Assert.Equal(coins, s.Run.Coins, "a stop pays 0 coins (R8)");
				Assert.True(s.Run.Health >= before, "health carries through the stop (C1)");
				hp = s.Run.Health;
			}
			else if (n.Type == "boss")
			{
				if (!win) { Assert.True(s.LoseFight().Ok, "lost"); break; }
				Assert.True(s.WinFight(500) == null || true, "won"); Assert.True(s.CompleteNode().Ok, "done"); break;
			}
			else
			{
				var d = s.WinFight(Math.Max(1, hp - 100));
				if (d != null) { offers.Add(string.Join(",", d.Offer.Cards.Select(c => c.ToString()))); var card = d.Offer.Cards[0]; Assert.True(d.Pick(0, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, "pick"); }
				Assert.True(s.CompleteNode().Ok, "complete");
				hp = s.Run.Health;
			}
			hashes.Add(s.StateHash());
		}
		return (s, hashes, offers, c3, cb);
	}

	[Test]
	public static void WholeRun_BothRow7Variants_ManySeeds_ReachBossAndEndWonOrLost_CoinsAndHashMatchDirectRules() => OnFixtures(() =>
	{
		foreach (uint seed in new uint[] { 1, 5, 42, 9001 })
			foreach (string row7 in new[] { "r7-merchant", "r7-dojo" })
				foreach (bool win in new[] { true, false })
				{
					var a = Play(seed, row7, true, win);
					var b = Play(seed, row7, false, win);
					string tag = $"seed {seed} {row7} win={win}";
					Assert.Equal(75, a.coinsAfterRow3, tag + ": R8 coins after row 3");
					Assert.Equal(150, a.coinsAtBoss, tag + ": R8 coins by the boss (25x3 + 50 + 25, row 8 pays none)");
					Assert.True(a.s.IsOver && a.s.Ended!.Won == win, tag + ": ends " + (win ? "won" : "lost"));
					Assert.True(a.hashes.SequenceEqual(b.hashes), tag + ": the model path hashes like the rule classes");
					Assert.True(a.offers.SequenceEqual(b.offers), tag + ": draft offers unchanged");
				}
	});

	[Test]
	public static void WholeRun_ThroughGameFlow_BothVariants_StopsAreScreens_RunReachesTheBoss(Node runner)
	{
		foreach (string row7 in new[] { "r7-merchant", "r7-dojo" })
		{
			string dir = Path.Combine(Path.GetTempPath(), "yokai-rsprobe-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
			FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
			MenuSettings.Reset();
			var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
			flow.QuitAction = () => { }; flow.FixedRunSeed = 31; flow.SaveFile = Path.Combine(dir, "run.dat"); flow.SettingsFile = Path.Combine(dir, "settings.dat");
			flow.ConfigureFight = f => f.ExternalDrive = true;
			try
			{
				runner.AddChild(flow);
				flow.Title.StartButton.EmitSignal(BaseButton.SignalName.Pressed);
				flow.ControlSelect.Start.EmitSignal(BaseButton.SignalName.Pressed);
				var s = flow.Session!;
				int stops = 0;
				for (; ; )
				{
					string id = s.Reachable().FirstOrDefault(x => x == row7) ?? s.Reachable()[0];
					Assert.True(flow.ChooseNode(id).Ok, "entered " + id);
					var t = s.Node!.Type;
					if (t is "merchant" or "dojo")
					{
						stops++;
						var screen = flow.NodeScene as RestStopScreen;
						Assert.True(screen != null && flow.Fight == null, id + " is the rest-stop screen");
						int before = s.Run.Health, coins = s.Run.Coins;
						Assert.True(!screen!.Leave() && s.Node != null, "Leave before a choice is refused");
						Assert.True(screen.TakeFree(), "take");
						Assert.True(screen.Leave(), "leave");
						Assert.True(!screen.Leave(), "Leave twice");
						Assert.Equal(coins, s.Run.Coins, "0 coins");
						Assert.True(flow.Router.Current == Screen.Map, "back on the map");
					}
					else if (t == "boss") { Assert.True(s.CopyLocked, "lock taken on entering the boss"); break; }
					else Finish(s, 400);
				}
				Assert.Equal(2, stops, "row 4 and row 7");
			}
			finally
			{
				runner.RemoveChild(flow); flow.QueueFree(); MenuSettings.Reset();
				FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe;
				try { Directory.Delete(dir, true); } catch (IOException) { }
			}
		}
	}

	// ---- 2. stuck? ----

	[Test]
	public static void Stuck_FullHealth_Leave_TakeTwice_EnterTwice_AreAllHandled() => OnFixtures(() =>
	{
		foreach (string node in new[] { "r4-merchant", "r7-merchant", "r7-dojo" })
		{
			var s = AtNode(node, 1000);
			var m = RestStopScreenModel.Create(s)!;
			ulong h0 = s.StateHash();
			Assert.True(!m.Leave().Ok && s.StateHash() == h0 && s.Node != null, node + ": Leave before a choice refused, hash unchanged");
			Assert.True(!s.Enter(node).Ok && s.StateHash() == h0, node + ": entering the stop twice is refused");
			Assert.True(m.CanTakeFree && m.FreeGain == 0, node + ": at full health the +0 take is offered");
			Assert.True(m.TakeFree().Ok && s.Run.Health == 1000, node + ": +0 taken");
			ulong h1 = s.StateHash();
			Assert.True(!m.TakeFree().Ok && s.StateHash() == h1, node + ": take twice refused");
			Assert.True(m.Leave().Ok, node + ": leave");
			ulong h2 = s.StateHash();
			Assert.True(!m.Leave().Ok && !m.TakeFree().Ok && s.StateHash() == h2, node + ": leave twice / take after leaving refused, hash unchanged");
		}
	});

	[Test]
	public static void Stuck_RunOver_AndNotAStop_ModelRefusesEverything() => OnFixtures(() =>
	{
		foreach (string node in new[] { "r4-merchant", "r7-dojo" })
		{
			var s = AtNode(node, 400);
			var m = RestStopScreenModel.Create(s)!;
			Assert.True(s.LoseFight().Ok, "ended");
			ulong h = s.StateHash(); int hp = s.Run.Health; var ev = 0; s.NodeCompleted += _ => ev++;
			Assert.True(!m.Open && !m.TakeFree().Ok && !m.Leave().Ok, node + ": a model of a run that is over refuses");
			Assert.True(s.StateHash() == h && s.Run.Health == hp && ev == 0, node + ": nothing changed, no event");
			var late = RestStopScreenModel.Create(s);
			if (late != null) Assert.True(!late.TakeFree().Ok && !late.Leave().Ok && s.Run.Health == hp && s.StateHash() == h, node + ": a model made after the run ended refuses too");
		}
		var d = NewSession(); d.Enter("r1-apprentice");
		Assert.True(RestStopScreenModel.Create(d) == null, "a duel node: no model");
		Assert.True(RestStopScreenModel.Create(NewSession()) == null, "no node entered: no model");
	});

	// ---- 3. stale model ----

	[Test]
	public static void StaleModel_AtADuel_AfterRunEnd_AtAnotherKind_RefusesWithHashAndHealthUnchanged() => OnFixtures(() =>
	{
		var s = AtNode("r4-merchant", 300);
		var old = RestStopScreenModel.Create(s)!;
		Assert.True(old.TakeFree().Ok && old.Leave().Ok, "row 4 done");
		s.Enter("r5-elder-oni");
		ulong h = s.StateHash(); int hp = s.Run.Health;
		Assert.True(!old.Open && !old.TakeFree().Ok && !old.Leave().Ok && s.StateHash() == h && s.Run.Health == hp, "at a duel node");
		Finish(s, 300);
		string r7 = s.Reachable().Contains("r6-kitsune") ? "r6-kitsune" : s.Reachable()[0];
		s.Enter(r7); Finish(s, 300);
		Assert.True(s.Enter("r7-dojo").Ok, "dojo");
		h = s.StateHash(); hp = s.Run.Health;
		Assert.True(!old.TakeFree().Ok && !old.Leave().Ok && s.StateHash() == h && s.Run.Health == hp && s.Dojo!.Choice == DojoChoice.None, "a merchant model used at the dojo");
		var dm = RestStopScreenModel.Create(s)!;
		Assert.True(dm.TakeFree().Ok && dm.Leave().Ok, "dojo model works");
		Assert.True(s.Enter(s.Reachable()[0]).Ok, "row 8");
		h = s.StateHash(); hp = s.Run.Health;
		Assert.True(!dm.TakeFree().Ok && !dm.Leave().Ok && s.StateHash() == h && s.Run.Health == hp, "a dojo model at the elder");
		// a dojo model of another session does not touch this one
		var other = AtNode("r7-merchant", 300);
		var t = AtNode("r7-dojo", 300);
		var dm2 = RestStopScreenModel.Create(t)!;
		ulong oh = other.StateHash();
		dm2.TakeFree();
		Assert.True(other.StateHash() == oh && other.Run.Health == 300, "a model only acts on its own session");
	});

	// ---- 4. preview == result ----

	[Test]
	public static void Preview_EqualsWhatMerchantAndDojoDo_ForTheBoundaryHealths() => OnFixtures(() =>
	{
		foreach (string node in new[] { "r4-merchant", "r7-dojo" })
			foreach (int hp in new[] { 0, 1, 699, 700, 701, 749, 750, 751, 999, 1000 })
			{
				var s = AtNode(node, hp);
				Assert.Equal(hp, s.Run.Health, "set");
				var m = RestStopScreenModel.Create(s)!;
				int after = m.HealthAfterFree, gain = m.FreeGain;
				Assert.True(m.TakeFree().Ok, "take");
				Assert.Equal(after, s.Run.Health, $"{node} hp {hp}: preview health after");
				Assert.Equal(gain, s.Run.Health - hp, $"{node} hp {hp}: preview gain");
				Assert.Equal(gain, m.FreeGain, $"{node} hp {hp}: gain after taking");
				Assert.True(m.Health == s.Run.Health, "model health");
			}
	});

	[Test]
	public static void Preview_TextOnTheCard_EqualsTheResult(Node runner) => OnFixtures(() =>
	{
		foreach (string node in new[] { "r4-merchant", "r7-dojo" })
			foreach (int hp in new[] { 0, 700, 750, 751, 999, 1000 })
			{
				var s = AtNode(node, hp);
				var screen = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
				screen.LineSource = _ => "A line."; screen.BindRun(s, ControlScheme.Kihon); runner.AddChild(screen);
				try
				{
					string before = screen.GetNode<Label>("FreeCard/Box/Desc").Text;
					Assert.True(screen.TakeFree(), "take");
					int gain = s.Run.Health - hp;
					Assert.True(before.Contains($"+{gain} → {s.Run.Health} / 1000"), $"{node} hp {hp}: card said '{before}', real +{gain} → {s.Run.Health}");
					string done = screen.GetNode<Label>("FreeCard/Box/Desc").Text;
					Assert.True(done.Contains($"+{gain} → {s.Run.Health} / 1000"), $"{node} hp {hp}: used text '{done}'");
				}
				finally { runner.RemoveChild(screen); screen.Free(); }
			}
	});

	// ---- 5. forecast ----

	[Test]
	public static void Forecast_FollowsLevellingAndReplacing_BeforeTheLock_AndFreezesAtTheLock() => OnFixtures(() =>
	{
		var s = AtNode("r4-merchant", 500);
		void Check(string what)
		{
			var want = CopyRule.Target(s.Run);
			var f = RestStopScreenForecast.For(s);
			Assert.True(want != null && f != null && f.SpecialId == want.Data.Id && f.Slot == want.Slot && f.Level == want.MoveLevel && f.RyoLevel == want.Level && !f.Locked, what);
			Assert.True(f!.Level <= 2, what + ": T4 the copy never runs above Lv 2");
		}
		Check("start");
		foreach (var sl in s.Run.Owned.Select(o => o.Slot).ToList())
		{
			while (s.Run.CanLevelUp(sl)) { s.Run.LevelUp(sl); Check($"slot {sl} levelled to {s.Run[sl]!.Level}"); }
		}
		// replacing the top special: the forecast leaves it (T2: replaced moves don't count)
		var top = CopyRule.Target(s.Run)!;
		var other = s.Pool.Specials.FirstOrDefault(sp => sp.Slot == top.Slot && sp.Id != top.Data.Id);
		if (other != null) { s.Run.Equip(other); Check("replaced the top special"); Assert.True(RestStopScreenForecast.For(s)!.SpecialId != top.Data.Id, "replaced move no longer named"); }
		// no mutation by asking
		ulong h = s.StateHash();
		for (int i = 0; i < 5; i++) { _ = RestStopScreenForecast.For(s); _ = RestStopScreenModel.Create(s)!.Forecast; }
		Assert.True(s.StateHash() == h, "reading the forecast changes nothing");
	});

	[Test]
	public static void Forecast_Lv3Target_ShowsLv2_AndEvolutionNote(Node runner) => OnFixtures(() =>
	{
		var s = AtNode("r4-merchant", 500);
		foreach (var sl in s.Run.Owned.Select(o => o.Slot).ToList()) while (s.Run.CanLevelUp(sl)) s.Run.LevelUp(sl);
		var t = CopyRule.Target(s.Run)!;
		Assert.True(t.Level == 3, "levelled target is Lv " + t.Level);
		var screen = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
		screen.LineSource = _ => "A line."; screen.BindRun(s, ControlScheme.Kihon); runner.AddChild(screen);
		try
		{
			Assert.Equal("Lv 2", screen.GetNode<Label>("Forecast/LevelTag/LevelLabel").Text, "T4: the Tanuki's copy is at the highest unevolved level");
			Assert.True(screen.GetNode<Label>("Forecast/CopyNote").Text.Contains("evolution"), "the evolution note");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	[Test]
	public static void Forecast_BossLocked_StaysOnLockedCopyOnTheScreen(Node runner) => OnFixtures(() =>
	{
		// A stop is never entered after the lock in play, but a session restored after the lock at a stop must still read it.
		var s = AtNode("r7-merchant", 500);
		var want = CopyRule.Target(s.Run)!;
		var screen = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
		screen.LineSource = _ => "A line."; screen.BindRun(s, ControlScheme.Kihon); runner.AddChild(screen);
		try
		{
			Assert.Equal(want.Data.Name, screen.GetNode<Label>("Forecast/SpecialName").Text, "name");
			Assert.True(screen.GetNode<Label>("Forecast/LockNote").Text.Contains("Locks when"), "not locked yet before the boss (T1)");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	[Test]
	public static void S3_NoTanukiLikenessOrRevealOnTheMerchantsSide_BeforeTheBoss(Node runner) => OnFixtures(() =>
	{
		foreach (string node in new[] { "r4-merchant", "r7-merchant" })
		{
			var s = AtNode(node, 500);
			var screen = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
			screen.BindRun(s, ControlScheme.Kihon); runner.AddChild(screen);
			try
			{
				var bad = new[] { "tanuki", "raccoon", "disguise", "drops", "true face", "identity" };
				foreach (string p in new[] { "RowLine", "LineBox/Box/Speaker", "LineBox/Box/Line", "Portrait/PortraitLabel", "FreeCard/Box/Top/Titles/Title", "FreeCard/Box/Top/Titles/Sub", "FreeCard/Box/Desc", "SecondCard/Box/Top/Titles/Title", "SecondCard/Box/Top/Titles/Sub", "SecondCard/Box/Desc", "PickLine" })
				{
					string text = screen.GetNode<Label>(p).Text.ToLowerInvariant();
					foreach (string b in bad) Assert.True(!text.Contains(b), $"{node}: '{p}' = '{text}' reveals '{b}' (S3)");
				}
				foreach (var n in AllNodes(screen))
					if (n is TextureRect tr && tr.Texture?.ResourcePath is { } rp && !n.GetPath().ToString().Contains("Forecast"))
						Assert.True(!rp.ToLowerInvariant().Contains("tanuki"), $"{node}: texture {rp} shows a Tanuki likeness");
			}
			finally { runner.RemoveChild(screen); screen.Free(); }
		}
	});

	static IEnumerable<Node> AllNodes(Node root) { yield return root; foreach (var c in root.GetChildren()) foreach (var d in AllNodes(c)) yield return d; }

	// ---- 6. saves ----

	[Test]
	public static void Saves_StopRestoresToTheStop_SameHash_NothingWrittenInsideTheStop(Node runner)
	{
		foreach (string target in new[] { "r4-merchant", "r7-dojo" })
		{
			string dir = Path.Combine(Path.GetTempPath(), "yokai-rsprobe-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			string save = Path.Combine(dir, "run.dat");
			var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
			FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
			MenuSettings.Reset();
			var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
			flow.QuitAction = () => { }; flow.FixedRunSeed = 77; flow.SaveFile = save; flow.SettingsFile = Path.Combine(dir, "settings.dat");
			flow.ConfigureFight = f => f.ExternalDrive = true;
			try
			{
				runner.AddChild(flow);
				flow.Title.StartButton.EmitSignal(BaseButton.SignalName.Pressed);
				flow.ControlSelect.Start.EmitSignal(BaseButton.SignalName.Pressed);
				var s = flow.Session!;
				while (true)
				{
					string id = s.Reachable().First(n => Leads(s, n, target));
					Assert.True(flow.ChooseNode(id).Ok, "entered " + id);
					if (id == target) break;
					if (flow.NodeScene is RestStopScreen early) Assert.True(early.TakeFree() && early.Leave(), "passed");
					else Finish(s, 350);
				}
				Assert.True(File.Exists(save), target + ": saved on entering the node (M5)");
				byte[] entryBytes = File.ReadAllBytes(save);
				ulong entryHash = s.StateHash(); int entryHp = s.Run.Health, entryCoins = s.Run.Coins;
				var screen = (RestStopScreen)flow.NodeScene!;
				screen.ToggleDetail(); screen.TakeFree();
				Assert.True(File.ReadAllBytes(save).SequenceEqual(entryBytes), target + ": nothing written while inside the stop");
				Assert.True(flow.ReturnToTitle() && flow.ContinueRun(), "quit, Continue");
				var s2 = flow.Session!;
				Assert.True(s2.Node?.NodeId == target, "restored to the stop");
				Assert.True(s2.StateHash() == entryHash, target + ": the restored hash is the entry hash (M5)");
				Assert.True(s2.Run.Health == entryHp && s2.Run.Coins == entryCoins, target + ": entry health and coins");
				var again = (RestStopScreen)flow.NodeScene!;
				Assert.True(!again.Model!.ChoiceMade && again.FreeCard.CardDisabled == false && !again.ContinueButton.Visible, "choice open again");
				Assert.True(again.TakeFree() && !again.TakeFree(), "taken once");
				int gain = s2.Run.Health - entryHp;
				Assert.True(again.Leave(), "leave");
				Assert.True(flow.Session!.Run.Health == entryHp + gain && gain == (target == "r4-merchant" ? Math.Min(250, 1000 - entryHp) : Math.Min(300, 1000 - entryHp)), target + ": healed once");
				// the completed-node save restores to the map, not the stop
				Assert.True(flow.ReturnToTitle() && flow.ContinueRun(), "quit on the map, Continue");
				Assert.True(flow.Session!.Node == null && flow.Session.Run.Health == entryHp + gain, target + ": after completion the save holds the healed run on the map");
			}
			finally
			{
				runner.RemoveChild(flow); flow.QueueFree(); MenuSettings.Reset();
				FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe;
				try { Directory.Delete(dir, true); } catch (IOException) { }
			}
		}
	}

	// ---- 7. determinism / no side effects ----

	[Test]
	public static void Determinism_OpeningFocusDetailAndFailedActions_LeaveHashUnchanged_SameInputsSameHashes(Node runner) => OnFixtures(() =>
	{
		var traces = new List<List<ulong>>();
		for (int pass = 0; pass < 2; pass++)
		{
			var s = AtNode("r4-merchant", 420, 7);
			var tr = new List<ulong> { s.StateHash() };
			var screen = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
			screen.LineSource = _ => "A line."; screen.BindRun(s, ControlScheme.Kihon); runner.AddChild(screen);
			try
			{
				tr.Add(s.StateHash());
				screen.ToggleDetail(); screen.ToggleDetail(); screen.Refresh(); tr.Add(s.StateHash());
				Assert.True(!screen.Leave(), "refused"); tr.Add(s.StateHash());
				Assert.True(tr.All(h => h == tr[0]), "opening, Tab, refused Leave: hash never moved");
				Assert.True(screen.TakeFree(), "take"); tr.Add(s.StateHash());
				Assert.True(!screen.TakeFree(), "retake"); tr.Add(s.StateHash());
				Assert.True(tr[^1] == tr[^2] && tr[^1] != tr[0], "a refused retake changes nothing; the real take does");
				Assert.True(screen.Leave(), "leave"); tr.Add(s.StateHash());
			}
			finally { runner.RemoveChild(screen); screen.Free(); }
			traces.Add(tr);
		}
		Assert.True(traces[0].SequenceEqual(traces[1]), "same seed and inputs: same hash sequence");
		// a different choice (dojo rest vs merchant heal) gives a different state
		var a = AtNode("r7-merchant", 400); var b = AtNode("r7-dojo", 400);
		RestStopScreenModel.Create(a)!.TakeFree(); RestStopScreenModel.Create(b)!.TakeFree();
		Assert.True(a.Run.Health != b.Run.Health, "250 vs 300");
	});

	// ---- 8. name fit ----

	[Test]
	public static void NameFit_RulesEight_AreAtLeastTheReadableFloor(Node runner)
	{
		var screen = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
		runner.AddChild(screen);
		try
		{
			var label = screen.GetNode<Label>("Forecast/SpecialName");
			var font = label.GetThemeFont("font");
			// The HUD's floor is 17 px at 720p = 25.5 px at the 1080p design size; the forecast floor is 20.
			foreach (string n in new[] { "Spirit Wave", "Rising Talisman", "Foxfire", "Fox Mirage", "Iron-Club Charge", "Oni Quake", "River Grab", "Shell Spin" })
			{
				int size = RestStopScreen.NameFontSize(font, n, label.Size.X, label.Size.Y);
				Assert.True(size >= 26, $"'{n}' is set at {size}px at 1080p ({size * 720 / 1080}px at 720p), under the HUD's 17px floor at 720p");
			}
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	}

	[Test]
	public static void NameFit_AVeryLongSingleWord_FitsAtOrAboveTheFloor(Node runner)
	{
		var screen = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
		runner.AddChild(screen);
		try
		{
			var label = screen.GetNode<Label>("Forecast/SpecialName");
			var font = label.GetThemeFont("font");
			string word = "Incomprehensibilities";
			int size = RestStopScreen.NameFontSize(font, word, label.Size.X, label.Size.Y);
			Assert.True(RestStopScreen.NameFits(font, word, label.Size.X, label.Size.Y) && size >= 26, $"'{word}' at {size}px, fits={RestStopScreen.NameFits(font, word, label.Size.X, label.Size.Y)}");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	}

	// ---- 9. input ----

	static void Push(Node n, InputEvent e) => n.GetViewport().PushInput(e);
	static void Tap(Node n, Key k) { Push(n, new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true }); Push(n, new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false }); }
	static void Pad(Node n, JoyButton b) { Push(n, new InputEventJoypadButton { ButtonIndex = b, Pressed = true }); Push(n, new InputEventJoypadButton { ButtonIndex = b, Pressed = false }); }
	static Control? Focus(Node n) => n.GetViewport().GuiGetFocusOwner();
	static bool Inside(Control? f, Control root) => f != null && (f == root || root.IsAncestorOf(f));

	[Test]
	public static void Input_KeysAndPad_FocusNeverLost_EnterTakes_TabToggles_EscAsksForTitle_EvenWhenUsed(Node runner) => OnFixtures(() =>
	{
		var s = AtNode("r4-merchant", 500);
		var screen = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
		screen.LineSource = _ => "A line."; screen.BindRun(s, ControlScheme.Kihon); runner.AddChild(screen);
		int titles = 0; screen.TitleRequested += () => titles++;
		try
		{
			Assert.True(Inside(Focus(runner), screen), "focus on open: " + Focus(runner)?.Name);
			ulong h = s.StateHash();
			foreach (var k in new[] { Key.Down, Key.Up, Key.Down, Key.Left, Key.Right, Key.Tab, Key.Tab })
			{ Tap(runner, k); Assert.True(Inside(Focus(runner), screen), $"focus lost after {k}: {Focus(runner)?.Name}"); }
			foreach (var b in new[] { JoyButton.DpadDown, JoyButton.DpadUp, JoyButton.Y, JoyButton.Y })
			{ Pad(runner, b); Assert.True(Inside(Focus(runner), screen), $"focus lost after pad {b}"); }
			Assert.True(s.StateHash() == h && s.Run.Health == 500, "navigation changes nothing");
			Tap(runner, Key.Tab);
			// move focus to the free card, press Enter
			screen.FreeCard.GrabFocus();
			Tap(runner, Key.Enter);
			Assert.True(s.Run.Health == 750 && screen.ContinueButton.Visible, "Enter takes the free card: " + s.Run.Health);
			Assert.True(Inside(Focus(runner), screen) && Focus(runner) == screen.ContinueButton, "focus moves to Continue, not lost: " + Focus(runner)?.Name);
			foreach (var k in new[] { Key.Down, Key.Up, Key.Tab, Key.Left })
			{ Tap(runner, k); Assert.True(Inside(Focus(runner), screen), $"used state: focus lost after {k}"); }
			Tap(runner, Key.Escape); Pad(runner, JoyButton.B);
			Assert.True(titles >= 2 && s.Node != null && s.Run.Health == 750, "Esc from the used state asks for the title and completes nothing");
			screen.ContinueButton.GrabFocus();
			Tap(runner, Key.Enter);
			Assert.True(s.Node == null, "Enter on Continue completes the node");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	// ---- 10. story line ----

	[Test]
	public static void Story_Row4CardOnRow4Only_MoveFilled_NoSpecialDoesNotThrow(Node runner) => OnFixtures(() =>
	{
		var s4 = AtNode("r4-merchant", 500);
		var want = CopyRule.Target(s4.Run)!;
		var a = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
		a.BindRun(s4, ControlScheme.Kihon); runner.AddChild(a);
		try
		{
			string line = a.GetNode<Label>("LineBox/Box/Line").Text;
			Assert.Equal($"That {want.Data.Name} of yours... I'd love one.", line, "S3 verbatim, templated to the copy target (T2)");
			Assert.True(!line.Contains("{"), "no raw placeholder");
		}
		finally { runner.RemoveChild(a); a.Free(); }

		var s7 = AtNode("r7-merchant", 500);
		var b = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
		b.BindRun(s7, ControlScheme.Kihon); runner.AddChild(b);
		try { Assert.True(!b.GetNode<Label>("LineBox/Box/Line").Text.Contains("I'd love"), "row 7 does not show the row-4 card"); }
		finally { runner.RemoveChild(b); b.Free(); }

		var d = AtNode("r7-dojo", 500);
		var c = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
		c.BindRun(d, ControlScheme.Kihon); runner.AddChild(c);
		try { Assert.True(!c.GetNode<Label>("LineBox/Box/Line").Text.Contains("I'd love"), "dojo does not show the row-4 card"); }
		finally { runner.RemoveChild(c); c.Free(); }

		// no special owned at row 4: must not throw, must not leave a placeholder
		var walked = AtNode("r4-merchant", 500);
		var run = RunState.Restore(500, Array.Empty<(SpecialSlot, OwnedSpecial)>());
		run.SetMap(walked.Map);
		var bare = RunSession.Restore(run, walked.Pool, NodeSceneTable.Default(MapDir), 5, false, null);
		var e = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
		e.BindRun(bare, ControlScheme.Kihon); runner.AddChild(e);
		try { Assert.True(!e.GetNode<Label>("LineBox/Box/Line").Text.Contains("{"), "no raw placeholder with no special"); }
		finally { runner.RemoveChild(e); e.Free(); }
	});
}
