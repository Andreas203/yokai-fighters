using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-85 (R8). Tests named *_OPEN pin a finding as it behaves today.</summary>
public static class CoinProbes
{
	static string MapDir => ContentPaths.Data("map");
	static NodeSceneTable Table() => NodeSceneTable.Default(MapDir);
	static MapGraph DefaultMap() => FileMapSource.Default(MapDir).Load();
	static MapGraph DemoMap() => new FileMapSource(System.IO.Path.Combine(MapDir, "demo-map.json")).Load();

	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		try { body(); } finally { FightScene.Sources = saved; }
	}
	static void Throws<T>(Action body, string what) where T : Exception
	{
		try { body(); } catch (T) { return; }
		throw new AssertFailed($"{what}: expected {typeof(T).Name}");
	}
	static RunSession NewSession(MapGraph g, uint seed = 5) => RunSession.Start(FightScene.LoadAbilityPool(), g, Table(), seed);
	static void Take(RunSession s)
	{
		var d = s.Draft;
		if (d is not null) d.Pick(0, d.Offer.Cards[0].Targets.Count > 0 ? d.Offer.Cards[0].Targets[0].Slot : null);
	}

	// ---- 1. _paid is not restorable (R8: a node pays once) ----
	// Reachable only by rebuilding a session mid-node, i.e. save/continue (YOK-73/87). Pinned as open.
	[Test]
	public static void PaidFlag_RebuiltSessionHashesEqualButPaysAgain_OPEN() => OnFixtures(() =>
	{
		string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "coinprobe-single.json");
		System.IO.File.WriteAllText(path, "{\"kind\":\"run_map\",\"id\":\"single\",\"schema_version\":1,\"name\":\"one\",\"status\":\"final\",\"rules\":[],\"nodes\":[{\"id\":\"only\",\"row\":1,\"type\":\"duel\",\"yokai\":\"kitsune\",\"label\":\"K\"}],\"routes\":[]}");
		var graph = new FileMapSource(path).Load();
		var x = NewSession(graph);
		x.Enter("only");
		Assert.True(x.WinFight(500) == null && x.Run.Coins == 25, "setup: draft-less node paid 25");

		var run = RunState.NewRun(FightScene.LoadAbilityPool(), graph);
		run.SetMap(RunMap.Restore(graph, x.Map.Snapshot()));
		run.RestoreCoins(x.Run.Coins);
		run.AfterDuel(500, true);
		var y = new RunSession(run, FightScene.LoadAbilityPool(), Table(), x.Seed);
		Assert.True(y.Node is not null && y.Run.Coins == 25, "Y is in the node with 25");
		bool hashEqual = x.StateHash() == y.StateHash();
		Assert.True(hashEqual, "pinned: the two sessions hash equal while _paid differs");
		x.WinFight(500); y.WinFight(500);
		Assert.True(x.Run.Coins == 25, "X is not paid twice");
		// Observed today: Y pays again (75). Pinned: if this starts failing, _paid became restorable.
		Assert.True(y.Run.Coins == 50, $"OPEN: a rebuilt session pays the node again (hashEqual={hashEqual})");
	});

	// ---- 2. loader looser than the schema, row-8 elder ----
	static string Json(string rows, string extraTypes = "") => $$"""
	{ "kind": "node_scenes", "id": "t", "schema_version": 1,
	  "default_scene": "res://scenes/fight.tscn", "fallback_fighter": "kitsune",
	  "tiers": { "elder": { "reaction_frames": 18 } },
	  "types": { "elder": { "tier": "elder", "binds": true, "coins": 50 }{{extraTypes}} },
	  "rows": {{rows}} }
	""";
	static int Row8Elder(string json) =>
		NodeSceneTable.Parse(json).Resolve(new MapNodeData("e8", 8, "elder", "kitsune", true)).Context.Coins;

	[Test]
	public static void Loader_SchemaGaps_RowEightElder_OPEN()
	{
		// Control: the intended form pays 0.
		Assert.Equal(0, Row8Elder(Json("""{"8":{"types":{"elder":{"coins":0}}}}""")), "control");
		// Gaps the schema rejects but the loader accepts (observed values pinned).
		Assert.Equal(50, Row8Elder(Json("""{"8":{"type":{"elder":{"coins":0}}}}""")), "misspelt 'types' accepted, elder pays 50");
		Assert.Equal(50, Row8Elder(Json("""{"8":{"types":{"elders":{"coins":0}}}}""")), "unregistered type key accepted, elder pays 50");
		Assert.Equal(50, Row8Elder(Json("""{"8":{"types":{"elder":{}}}}""")), "no coins accepted, elder pays 50");
		Assert.Equal(0, Row8Elder(Json("""{"08":{"types":{"elder":{"coins":0}}}}""")), "'08' accepted and read as row 8");
		Assert.Equal(10000, Row8Elder(Json("""{"8":{"types":{"elder":{"coins":10000}}}}""")), "above 9999 accepted");
		Throws<FormatException>(() => Row8Elder(Json("""{"8":{"types":{"elder":{"coins":-1}}}}""")), "negative coins is rejected");
	}

	// ---- 3. CoinsWon at NodeCompleted ----
	[Test]
	public static void NodeCompleted_ListenerSeesCoinsWonZero_OPEN() => OnFixtures(() =>
	{
		var s = NewSession(DefaultMap());
		int seen = -1, coinsThen = -1;
		s.NodeCompleted += _ => { seen = s.CoinsWon; coinsThen = s.Run.Coins; };
		s.Enter("r1-apprentice"); s.WinFight(500); Take(s); s.CompleteNode();
		Assert.Equal(25, coinsThen, "the purse already holds the coins");
		Assert.Equal(0, seen, "OPEN: CoinsWon is already cleared when NodeCompleted fires");
	});

	// ---- 4. refused sequences ----
	[Test]
	public static void Refusals_ChangeNothing() => OnFixtures(() =>
	{
		var s = NewSession(DefaultMap());
		s.Enter("r1-apprentice"); s.WinFight(400);
		ulong h = s.StateHash(); int coins = s.Run.Coins, hp = s.Run.Health;
		int events = 0; s.NodeCompleted += _ => events++; s.RunEnded += _ => events++; s.NodeEntered += _ => events++;
		Throws<InvalidOperationException>(() => s.WinFight(100), "second win on a draft node");
		Assert.True(s.StateHash() == h && s.Run.Coins == coins && s.Run.Health == hp && s.CoinsWon == 25, "no mutation (draft node)");
		Assert.True(!s.Enter("r1-apprentice").Ok && !s.Enter("r2-oni").Ok && s.StateHash() == h, "re-entry refused");
		Assert.Equal(0, events, "no events from refusals");

		// win then lose: the run ends, coins already paid stay
		Assert.True(s.LoseFight().Ok, "LoseFight after WinFight is accepted");
		Assert.True(s.IsOver && s.Run.Coins == 25, "WIN then LOSE: ends lost, keeps 25 (R8 silent)");
		Throws<InvalidOperationException>(() => s.WinFight(500), "WinFight after LoseFight");
		Assert.True(s.Run.Coins == 25, "unchanged");

		// lose then win on a fresh session
		var t = NewSession(DefaultMap());
		t.Enter("r1-apprentice"); t.LoseFight();
		ulong ht = t.StateHash();
		Throws<InvalidOperationException>(() => t.WinFight(500), "lose then win");
		Assert.True(t.StateHash() == ht && t.Run.Coins == 0, "lost duel pays nothing, nothing changed");

		// draft-less node: second win blocked by _paid
		var d = NewSession(DemoMap());
		d.Enter("demo-kitsune"); d.WinFight(500); Take(d); d.CompleteNode(); d.Enter("demo-rematch");
		d.WinFight(500);
		ulong hd = d.StateHash();
		d.WinFight(500);
		Assert.True(d.Run.Coins == 50 && d.CoinsWon == 25 && d.StateHash() == hd, "second win on a draft-less node pays nothing more");
	});

	// ---- 5. shipped table off the real map ----
	[Test]
	public static void ShippedTable_OffMapNodes_FailOpenNumbers_OPEN()
	{
		var t = Table();
		int C(int row, string type, bool elder = false) => t.Resolve(new MapNodeData($"x{row}{type}", row, type, "kitsune", elder)).Context.Coins;
		Assert.Equal(50, C(1, "elder", true), "elder row 1");
		Assert.Equal(50, C(4, "elder", true), "elder row 4");
		Assert.Equal(50, C(9, "elder", true), "elder row 9");
		Assert.Equal(0, C(8, "elder", true), "elder row 8");
		Assert.Equal(25, C(7, "duel"), "duel row 7");
		Assert.Equal(25, C(8, "duel"), "duel row 8");
		Assert.Equal(25, C(12, "duel"), "duel row 12");
	}

	// ---- own: routes, purse edges, hashes ----
	[Test]
	public static void EveryRoute_Earns150_Has75AfterRow3() => OnFixtures(() =>
	{
		var graph = DefaultMap();
		var totals = new HashSet<int>(); var at3 = new HashSet<int>(); int routes = 0;
		void Walk(RunSession s)
		{
			var reach = s.Reachable();
			if (s.IsOver) { totals.Add(s.Run.Coins); routes++; return; }
			foreach (var id in reach)
			{
				var branch = NewSession(graph, 3);
				// replay the path so far
				foreach (var v in s.Map.Visited) { branch.Enter(v); branch.WinFight(700); Take(branch); branch.CompleteNode(); }
				branch.Enter(id); branch.WinFight(700); Take(branch); branch.CompleteNode();
				if (branch.Step == 3) at3.Add(branch.Run.Coins);
				Walk(branch);
			}
		}
		Walk(NewSession(graph, 3));
		Assert.Equal(64, routes, "64 routes");
		Assert.True(totals.SetEquals(new[] { 150 }), "all routes 150");
		Assert.True(at3.SetEquals(new[] { 75 }), "75 after row 3 (GDD 6.4: about 75 at row 4)");
	});

	[Test]
	public static void Purse_Edges() => OnFixtures(() =>
	{
		var run = RunState.NewRun(FightScene.LoadAbilityPool());
		run.AddCoins(int.MaxValue);
		Assert.Equal(int.MaxValue, run.Coins, "max");
		run.AddCoins(1); Assert.Equal(int.MaxValue, run.Coins, "saturates");
		Throws<ArgumentOutOfRangeException>(() => run.AddCoins(int.MinValue), "MinValue pay");
		Assert.Equal(int.MaxValue, run.Coins, "unchanged");
		ulong h = run.StateHash();
		Assert.True(!run.TrySpend(int.MinValue) && !run.TrySpend(-1) && run.StateHash() == h, "negative spends refused, hash same");
		Assert.True(run.TrySpend(int.MaxValue) && run.Coins == 0, "exact spend");
		var never = RunState.NewRun(FightScene.LoadAbilityPool());
		Assert.Equal(never.StateHash(), run.StateHash(), "earned then spent to zero == never had coins");
		Assert.True(run.TrySpend(0) && !run.TrySpend(1), "0 free, 1 unaffordable");
		var a = RunState.NewRun(FightScene.LoadAbilityPool()); var b = RunState.NewRun(FightScene.LoadAbilityPool());
		a.AddCoins(25); b.AddCoins(50);
		Assert.True(a.StateHash() != b.StateHash(), "25 vs 50");
		Throws<ArgumentOutOfRangeException>(() => a.RestoreCoins(int.MinValue), "restore MinValue");
		Assert.Equal(25, a.Coins, "refused restore keeps purse");
	});

	[Test]
	public static void Determinism_SameSeedSameHashes_DifferentChoiceDiffers() => OnFixtures(() =>
	{
		List<ulong> Run(string second)
		{
			var s = NewSession(DefaultMap(), 11); var hs = new List<ulong>();
			foreach (var id in new[] { "r1-apprentice", second })
			{ s.Enter(id); s.WinFight(500); Take(s); s.CompleteNode(); hs.Add(s.StateHash()); }
			return hs;
		}
		Assert.True(Run("r2-oni").SequenceEqual(Run("r2-oni")), "same inputs same hashes");
		Assert.True(Run("r2-oni").Last() != Run("r2-kitsune").Last(), "different node, different hash");
	});

	[Test]
	public static void NodeOverrideZero_VersusAbsent()
	{
		string j(string nodes) => Json("{}").Replace("\"rows\": {}", $"\"rows\": {{}}, \"nodes\": {nodes}");
		var n = new MapNodeData("e", 5, "elder", "kitsune", true);
		Assert.Equal(50, NodeSceneTable.Parse(j("{}")).Resolve(n).Context.Coins, "absent: type amount");
		Assert.Equal(0, NodeSceneTable.Parse(j("""{"e":{"coins":0}}""")).Resolve(n).Context.Coins, "explicit 0 wins");
		Assert.Equal(50, NodeSceneTable.Parse(j("""{"e":{"scene":"res://scenes/fight.tscn"}}""")).Resolve(n).Context.Coins, "node entry without coins falls through");
	}
}
