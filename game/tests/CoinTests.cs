using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-85, R8: coins. The purse on <see cref="RunState"/> (pay, afford, spend, restore, hash), what each node of
/// the default map pays (data/map/node-scenes.json) and <see cref="RunSession"/> paying a won duel once.
/// No fight is simulated: the session is told the result, so every number here is exact.
/// </summary>
public static class CoinTests
{
	static string MapDir => ContentPaths.Data("map");
	static NodeSceneTable Table() => NodeSceneTable.Default(MapDir);
	static MapGraph DefaultMap() => FileMapSource.Default(MapDir).Load();

	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		try { body(); }
		finally { FightScene.Sources = saved; }
	}

	static void Throws<T>(Action body, string what) where T : Exception
	{
		try { body(); }
		catch (T) { return; }
		throw new AssertFailed($"{what}: expected {typeof(T).Name}");
	}

	static RunSession NewSession(MapGraph? graph = null, uint seed = 5) =>
		RunSession.Start(FightScene.LoadAbilityPool(), graph ?? DefaultMap(), Table(), seed);

	/// <summary>Wins the entered node with <paramref name="health"/> left, takes the first card and completes it.</summary>
	static void WinAndLeave(RunSession s, int health = 600)
	{
		var draft = s.WinFight(health);
		if (draft is not null)
		{
			var card = draft.Offer.Cards[0];
			Assert.True(draft.Pick(0, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, $"{s.Node!.NodeId}: picked");
		}
		Assert.True(s.CompleteNode().Ok, "completed");
	}

	// ---- the purse ----

	[Test]
	public static void Purse_StartsEmpty_PaysAndSpends_RefusesWhatRyoCannotAfford() => OnFixtures(() =>
	{
		var run = RunState.NewRun(FightScene.LoadAbilityPool());
		Assert.Equal(0, run.Coins, "a new run has no coins");
		Assert.True(run.CanAfford(0) && !run.CanAfford(1) && !run.TrySpend(1) && run.Coins == 0, "nothing to spend yet");

		run.AddCoins(25);
		run.AddCoins(50);
		run.AddCoins(0);
		Assert.Equal(75, run.Coins, "25 + 50 + 0");
		Assert.True(run.CanAfford(75) && !run.CanAfford(76), "afford = cost <= coins");

		ulong before = run.StateHash();
		Assert.True(!run.TrySpend(76), "76 of 75 is refused");
		Assert.True(run.Coins == 75 && run.StateHash() == before, "a refused spend changes nothing");
		Assert.True(!run.CanAfford(-1) && !run.TrySpend(-40) && run.Coins == 75, "a negative cost is refused, never a gift");
		Assert.True(!run.CanAfford(int.MinValue) && !run.TrySpend(int.MinValue) && run.Coins == 75, "nor int.MinValue");

		Assert.True(run.TrySpend(50) && run.Coins == 25, "50 spent, 25 left (R6 shop price)");
		Assert.True(!run.TrySpend(40) && run.Coins == 25, "40 of 25 is refused (R6 release price)");
		Assert.True(run.TrySpend(25) && run.Coins == 0 && run.TrySpend(0), "the last coin can be spent; a free thing is always affordable");

		Throws<ArgumentOutOfRangeException>(() => run.AddCoins(-1), "negative pay");
		Assert.Equal(0, run.Coins, "and it changed nothing");
		Assert.Equal(RunState.MaxHealth, run.Health, "coins never touch health");
	});

	[Test]
	public static void Purse_NeverWraps_AndRestores() => OnFixtures(() =>
	{
		var run = RunState.NewRun(FightScene.LoadAbilityPool());
		run.RestoreCoins(int.MaxValue - 10);
		run.AddCoins(25);
		Assert.Equal(int.MaxValue, run.Coins, "the purse stops at int.MaxValue");
		run.AddCoins(int.MaxValue);
		Assert.Equal(int.MaxValue, run.Coins, "and stays there");
		Assert.True(run.TrySpend(int.MaxValue) && run.Coins == 0, "all of it can be spent");

		run.RestoreCoins(125);
		Assert.Equal(125, run.Coins, "a saved purse is put back as it was");
		run.RestoreCoins(0);
		Assert.Equal(0, run.Coins, "including an empty one");
		Throws<ArgumentOutOfRangeException>(() => run.RestoreCoins(-5), "a negative saved purse");
		Assert.Equal(0, run.Coins, "is refused whole");
	});

	[Test]
	public static void Purse_IsRunState_InTheHash_EmptyPurseKeepsTheOldHash() => OnFixtures(() =>
	{
		var pool = FightScene.LoadAbilityPool();
		var a = RunState.NewRun(pool);
		var b = RunState.NewRun(pool);
		ulong empty = a.StateHash();
		Assert.Equal(empty, b.StateHash(), "two fresh runs");

		a.AddCoins(25);
		Assert.True(a.StateHash() != empty, "coins are part of the run's hash");
		b.AddCoins(25);
		Assert.Equal(a.StateHash(), b.StateHash(), "same coins, same hash");
		b.AddCoins(25);
		Assert.True(a.StateHash() != b.StateHash(), "25 and 50 differ");

		Assert.True(a.TrySpend(25) && a.StateHash() == empty, "spent back to nothing = the hash of a run that never had coins");
		a.AddCoins(0);
		Assert.Equal(empty, a.StateHash(), "paying nothing is not a change");

		var mapped = RunState.NewRun(pool, DefaultMap());
		ulong was = mapped.StateHash();
		mapped.AddCoins(50);
		Assert.True(mapped.StateHash() != was, "also on a run with a map");
	});

	// ---- what a node pays (data) ----

	[Test]
	public static void Table_DefaultMap_PaysR8_ByRowAndType()
	{
		var table = Table();
		var graph = DefaultMap();
		foreach (var node in graph.Nodes)
		{
			int expected = node.Type switch
			{
				"duel" when node.Row is >= 1 and <= 6 => 25, // R8: 25 per duel on rows 1-6
				"elder" when node.Row == 5 => 50,            // R8: 50 for the row-5 elder
				_ => 0,                                      // R8: the row-8 elder pays a rare; merchant, dojo and boss pay none
			};
			Assert.Equal(expected, table.Resolve(node).Context.Coins, $"{node.Id} (row {node.Row}, {node.Type})");
		}
		Assert.True(graph.Nodes.Count(n => n.Row == 8 && n.Type == "elder") == 2 && graph.Nodes.Count(n => n.Row == 5 && n.Type == "elder") == 2,
			"the map has both elder rows, so both amounts were checked");
		Assert.True(graph.Nodes.Where(n => n.Type == "duel").Select(n => n.Row).Distinct().OrderBy(r => r).SequenceEqual(new[] { 1, 2, 3, 6 }),
			"and duels on rows 1, 2, 3 and 6 (R1)");
		foreach (var node in graph.Nodes.Where(n => n.Row == 8))
			Assert.True(table.Resolve(node).Context is { Binds: true, Elder: true }, $"{node.Id}: the row-8 elder still binds, so its rare draft is the reward");
	}

	const string Custom = """
	{
	  "kind": "node_scenes", "id": "node-scenes", "schema_version": 1,
	  "default_scene": "res://scenes/fight.tscn", "fallback_fighter": "kitsune",
	  "tiers": { "yokai": { "reaction_frames": 22 } },
	  "types": {
	    "duel": { "tier": "yokai", "binds": true, "coins": 7 },
	    "elder": { "tier": "yokai", "binds": true, "coins": 11 },
	    "boss": { "tier": "yokai" }
	  },
	  "rows": { "3": { "types": { "elder": { "coins": 0 }, "duel": { "coins": 99 } } } },
	  "nodes": { "special": { "coins": 3 }, "free": { "coins": 0 } }
	}
	""";

	[Test]
	public static void Table_Coins_NodeBeatsRowBeatsType_DefaultNone_BadValuesRejected()
	{
		var table = NodeSceneTable.Parse(Custom);
		int Coins(string id, int row, string type) => table.Resolve(new MapNodeData(id, row, type, "kitsune", type == "elder")).Context.Coins;
		Assert.Equal(7, Coins("a", 1, "duel"), "the type's amount");
		Assert.Equal(11, Coins("b", 2, "elder"), "the elder type's amount");
		Assert.Equal(0, Coins("c", 3, "elder"), "the row replaces the type's amount, also with 0");
		Assert.Equal(99, Coins("d", 3, "duel"), "per type on that row");
		Assert.Equal(11, Coins("e", 4, "elder"), "only on that row");
		Assert.Equal(3, Coins("special", 3, "elder"), "a node's own amount wins over its row");
		Assert.Equal(0, Coins("free", 1, "duel"), "a node's own 0 wins over its type");
		Assert.Equal(0, Coins("f", 9, "boss"), "a type that names none pays none");

		string With(string old, string @new) => Custom.Replace(old, @new);
		void Rejected(string json, string what)
		{
			try { NodeSceneTable.Parse(json); }
			catch (FormatException e)
			{
				Assert.True(e.Message.Contains("coins") || e.Message.Contains("row"), $"{what}: the message says what is wrong, got '{e.Message}'");
				return;
			}
			throw new AssertFailed($"{what}: expected FormatException");
		}
		Rejected(With("\"coins\": 7", "\"coins\": -1"), "negative coins");
		Rejected(With("\"coins\": 7", "\"coins\": 2.5"), "fractional coins");
		Rejected(With("\"coins\": 7", "\"coins\": \"25\""), "coins as text");
		Rejected(With("\"coins\": 7", "\"coins\": true"), "coins as a flag");
		Rejected(With("\"coins\": 7", "\"coins\": 3000000000"), "coins beyond int");
		Rejected(With("\"coins\": 3 }", "\"coins\": -3 }"), "negative coins on a node");
		Rejected(With("\"coins\": 99", "\"coins\": -99"), "negative coins on a row");
		Rejected(With("\"3\": {", "\"0\": {"), "row 0");
		Rejected(With("\"3\": {", "\"-3\": {"), "a negative row");
		Rejected(With("\"3\": {", "\"three\": {"), "a row that is not a number");
		Rejected(With("\"rows\": { \"3\": { \"types\": { \"elder\": { \"coins\": 0 }, \"duel\": { \"coins\": 99 } } } }", "\"rows\": []"), "rows as a list");
	}

	// ---- the session pays a won duel ----

	[Test]
	public static void Session_PaysOnTheWin_NotOnEntry_NotOnALoss_NotTwice() => OnFixtures(() =>
	{
		var s = NewSession();
		Assert.True(s.Run.Coins == 0 && s.CoinsWon == 0, "a new run has no coins");
		s.Enter("r1-apprentice");
		Assert.True(s.Run.Coins == 0 && s.CoinsWon == 0, "nothing on entry");
		var draft = s.WinFight(400)!;
		Assert.True(s.Run.Coins == 25 && s.CoinsWon == 25, "R8: 25 for the row-1 duel, paid with the win");
		Throws<InvalidOperationException>(() => s.WinFight(400), "a node is won once");
		Assert.Equal(25, s.Run.Coins, "the refused second win paid nothing");
		Assert.True(!s.CompleteNode().Ok && s.Run.Coins == 25, "nor does a refused completion");
		draft.Pick(0, draft.Offer.Cards[0].Targets.Count > 0 ? draft.Offer.Cards[0].Targets[0].Slot : null);
		Assert.True(s.CompleteNode().Ok && s.Run.Coins == 25 && s.CoinsWon == 0, "the coins stay with the run; the node's payout is cleared on the map");
		Assert.True(!s.CompleteNode().Ok && !s.Enter("r1-apprentice").Ok && s.Run.Coins == 25, "refused steps on the map pay nothing");

		s.Enter("r2-oni");
		Assert.True(s.LoseFight().Ok && s.IsOver, "lost on row 2");
		Assert.True(s.Run.Coins == 25 && s.CoinsWon == 0, "a lost duel pays nothing");
		Throws<InvalidOperationException>(() => s.WinFight(400), "no win after the run is over");
		Assert.Equal(25, s.Run.Coins, "and no coins for it");

		// A node with no draft can have its win reported again (nothing refuses it): it still pays once.
		var two = NewSession(new FileMapSource(System.IO.Path.Combine(MapDir, "demo-map.json")).Load());
		two.Enter("demo-kitsune");
		WinAndLeave(two);
		Assert.Equal(25, two.Run.Coins, "the demo map's duel pays like any row-1 duel");
		two.Enter("demo-rematch");
		Assert.True(two.WinFight(500) == null && two.Run.Coins == 50 && two.CoinsWon == 25, "the last node gives no draft and pays its 25");
		Assert.True(two.WinFight(500) == null && two.Run.Coins == 50 && two.CoinsWon == 25, "reported again: still 50");
		Assert.True(two.CompleteNode().Ok && two.Ended is { Won: true } && two.Run.Coins == 50, "the won run keeps its coins");
	});

	[Test]
	public static void Session_WholeRun_EveryRoutePays150_RowByRow() => OnFixtures(() =>
	{
		var graph = DefaultMap();
		// Every walk of the default map, by always taking the i-th route out where there is one.
		var totals = new HashSet<int>();
		var seenNodes = new HashSet<string>();
		for (int variant = 0; variant < 3; variant++)
		{
			var s = NewSession(graph, 77);
			int expected = 0;
			var perRow = new List<string>();
			while (!s.IsOver)
			{
				var reachable = s.Reachable();
				string id = reachable[Math.Min(variant, reachable.Count - 1)];
				Assert.True(s.Enter(id).Ok, $"enter {id}");
				var node = s.Node!;
				seenNodes.Add(id);
				int before = s.Run.Coins;
				int pay = node.Row switch { 5 => 50, 4 or 7 or 8 or 9 => 0, _ => 25 }; // R8 written out by row (R1: one node per row)
				s.WinFight(600);
				Assert.Equal(before + pay, s.Run.Coins, $"{id}: row {node.Row} pays {pay}");
				Assert.Equal(pay, s.CoinsWon, $"{id}: the node's payout");
				expected += pay;
				perRow.Add($"{node.Row}:{s.Run.Coins}");
				if (node.Row == 8)
					Assert.True(s.Draft is { } d && d.Offer.Yokai == node.Fighter, $"{id}: the row-8 elder pays with its draft instead (R8)");
				var draft = s.Draft;
				if (draft is not null)
				{
					var card = draft.Offer.Cards[0];
					draft.Pick(0, card.Targets.Count > 0 ? card.Targets[0].Slot : null);
				}
				Assert.True(s.CompleteNode().Ok, $"{id}: completed");
			}
			Assert.True(s.Ended is { Won: true, RowReached: 9 }, "the walk reached the Tanuki's node");
			Assert.Equal("1:25, 2:50, 3:75, 4:75, 5:125, 6:150, 7:150, 8:150, 9:150", string.Join(", ", perRow), $"walk {variant}: coins after each row");
			Assert.Equal(150, expected, "25 x 4 duels + 50 for the row-5 elder");
			totals.Add(s.Run.Coins);
		}
		Assert.True(totals.SetEquals(new[] { 150 }), "every route arrives at the Tanuki with 150 coins");
		Assert.True(seenNodes.Count >= 12, $"the three walks cover most of the map ({seenNodes.Count} nodes)");
	});

	[Test]
	public static void Session_SameRunSameCoinsAndHash_CoinsChangeTheSessionHash() => OnFixtures(() =>
	{
		(RunSession S, List<ulong> Hashes) Walk(Action<RunSession>? afterRow1 = null)
		{
			var s = NewSession(seed: 9);
			var hashes = new List<ulong>();
			foreach (string id in new[] { "r1-apprentice", "r2-kitsune", "r3-oni" })
			{
				s.Enter(id);
				WinAndLeave(s, 500);
				if (id == "r1-apprentice") afterRow1?.Invoke(s);
				hashes.Add(s.StateHash());
			}
			return (s, hashes);
		}
		var (a, ha) = Walk();
		var (b, hb) = Walk();
		Assert.True(a.Run.Coins == 75 && b.Run.Coins == 75, "three duels, 75 coins");
		Assert.True(ha.SequenceEqual(hb), "the same run hashes the same at every node");

		var (c, hc) = Walk(s => Assert.True(s.Run.TrySpend(25), "the shop took 25"));
		Assert.Equal(50, c.Run.Coins, "75 earned, 25 spent");
		Assert.True(!ha.Zip(hc).Any(p => p.First == p.Second), "a run that spent differs from one that did not, at every node after");
	});
}
