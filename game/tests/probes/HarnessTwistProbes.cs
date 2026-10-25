using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-179 probes: elder twists on every elder node, stalls (no round timer, designer 2026-10-21; P5, Y3, R5), exchange ratio = hits.</summary>
public static class HarnessTwistProbes
{
	static HarnessContent? _content;
	static HarnessContent Content => _content ??= HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static BotConfig Bot => BotConfig.LoadDefault(ContentPaths.DataDir);

	static FightRecord Rec(RunSession s, bool won, bool stalled = false, FightLog? log = null) => new()
	{
		Profile = "stub", Won = won, Stalled = stalled, Ticks = 600, DamageTaken = 10, DamageDealt = 20, HealthLeft = Math.Max(1, s.Run.Health - 10), Bot = new BotStats(), Log = log,
	};

	static JsonElement Summary(BatchResult b, out JsonDocument doc)
	{
		doc = JsonDocument.Parse(HarnessReport.ToJson(b));
		return doc.RootElement.GetProperty("summary");
	}

	static uint SeedEntering(string nodeId)
	{
		for (uint seed = 1; seed < 300; seed++)
		{
			bool hit = false;
			RunHarness.PlayRun(Content, Bot, seed, (_, _, s, _) => { hit |= s.Node!.NodeId == nodeId; return Rec(s, true); });
			if (hit) return seed;
		}
		throw new InvalidOperationException($"no seed enters {nodeId}");
	}

	// P5, Y3: every elder node's fight, built by the game scene and by the harness, plays out identically.
	[Test]
	public static void EveryElderNode_GameBuiltAndHarnessBuiltFightsMatch(Godot.Node runner)
	{
		int checkedNodes = 0;
		string saved = FightScene.Opponent;
		foreach (var mapNode in Content.Map.Nodes.Where(n => n.Elder))
		{
			uint seed = SeedEntering(mapNode.Id);
			FightRecord? harness = null, game = null;
			ulong hStart = 0, sStart = 0;
			string? sceneTwist = null;
			RunHarness.PlayRun(Content, Bot, seed, (c, b, s, runSeed) =>
			{
				if (s.Node!.NodeId != mapNode.Id) return Rec(s, true);
				hStart = RunHarness.NewNodeMatch(c, b, s).StateHash();
				harness = RunHarness.FightNode(c, b, s, runSeed);
				var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
				scene.ExternalDrive = true; scene.PlayerScheme = b.Scheme; scene.Session = s;
				try
				{
					runner.AddChild(scene);
					sceneTwist = scene.Twist?.Id; sStart = scene.Match.StateHash();
					game = RunHarness.PlayFight(c, b, s, runSeed, scene.Match);
				}
				finally { scene.QueueFree(); FightScene.Opponent = saved; }
				return harness;
			});
			Assert.True(harness is not null && game is not null, $"{mapNode.Id} fought");
			Assert.True(harness!.Twist is not null, $"{mapNode.Id}: has a twist");
			Assert.Equal(sceneTwist, harness.Twist, $"{mapNode.Id}: same twist as the scene");
			Assert.Equal(sStart, hStart, $"{mapNode.Id}: same start hash");
			Assert.Equal(game!.MatchHash, harness.MatchHash, $"{mapNode.Id}: same final hash");
			Assert.Equal(game.Ticks, harness.Ticks, $"{mapNode.Id}: same ticks");
			Assert.Equal(game.TwistFired, harness.TwistFired, $"{mapNode.Id}: same twist count");
			checkedNodes++;
		}
		Assert.True(checkedNodes > 0, "the map has elder nodes");
	}

	// Non-elder fights unchanged: every non-elder fighting node starts hash-identical to NodeFight's own match, and plays as it did.
	[Test]
	public static void NonElderNodes_StartIdenticalToNodeFight_AndPlayTheSame()
	{
		foreach (var mapNode in Content.Map.Nodes.Where(n => !n.Elder && n.Type is not (NodeTypeRegistry.Merchant or NodeTypeRegistry.Dojo)))
		{
			uint seed = SeedEntering(mapNode.Id);
			RunHarness.PlayRun(Content, Bot, seed, (c, b, s, runSeed) =>
			{
				if (s.Node!.NodeId != mapNode.Id) return Rec(s, true);
				var plain = NodeFight.NewMatch(c.Sources, s.Run, s, s.Node.Fighter, b.Scheme);
				Assert.Equal(plain.StateHash(), RunHarness.NewNodeMatch(c, b, s).StateHash(), $"{mapNode.Id}: untouched");
				var a = RunHarness.FightNode(c, b, s, runSeed);
				var p = RunHarness.PlayFight(c, b, s, runSeed, plain);
				Assert.True(a.Twist is null && a.TwistFired == 0, $"{mapNode.Id}: no twist");
				Assert.Equal(p.MatchHash, a.MatchHash, $"{mapNode.Id}: same fight");
				return Rec(s, true);
			});
		}
	}

	// A stall AT the Tanuki: the run reached it, so it counts for reach, and leaves the beat denominator.
	[Test]
	public static void StallInTheTanukiFight_CountsAsReached_LeavesBeatDenominator()
	{
		var batch = new BatchResult(Bot, 1);
		for (uint i = 0; i < 2; i++)
			batch.Runs.Add(RunHarness.PlayRun(Content, Bot, 1 + i, (_, _, s, _) => s.Node!.Type == NodeTypeRegistry.Boss ? Rec(s, false, true) : Rec(s, true)));
		Assert.True(batch.Runs.All(r => r.Stalled && r.ReachedTanuki && !r.Won), "stalled at the boss, reached");
		var sum = Summary(batch, out var doc);
		using (doc)
		{
			Assert.Equal(100.0, sum.GetProperty("reach_tanuki_pct").GetDouble(), "reach: 2 of 2 (the stall at the Tanuki is not before it)");
			Assert.Equal(0, sum.GetProperty("reach_excluded_stalls").GetInt32(), "none left out of reach");
			Assert.Equal(JsonValueKind.Null, sum.GetProperty("beat_tanuki_pct_of_reached").ValueKind, "beat: 0 decided, null");
			Assert.Equal(2, sum.GetProperty("beat_excluded_stalls").GetInt32(), "both left out of beat");
			Assert.Equal(0, sum.GetProperty("runs_ended_by_row").EnumerateObject().Count(), "no lost runs");
			Assert.Equal(2, sum.GetProperty("runs_stalled_by_row").EnumerateObject().Sum(p => p.Value.GetInt32()), "stalled by row");
		}
	}


	// Mixed batch: one run won, one stalled mid-run, one lost mid-run, one won; only the stall leaves the denominators.
	[Test]
	public static void MixedBatch_MidRunStallAndLoss_PercentagesExcludeOnlyTheStall()
	{
		var levels = BotLevels.LoadDefault(ContentPaths.DataDir);
		Assert.True(levels.Levels.Count >= 2, "bot-levels has several levels");
		var batch = new BatchResult(levels.Levels[0], 1, "t-mixed");
		// run i is played at level (i mod 2): runs 0,2 at the first level, 1,3 at the second
		for (int i = 0; i < 4; i++)
		{
			int fights = 0, k = i;
			batch.Runs.Add(RunHarness.PlayRun(Content, levels.Levels[i % 2], 1 + (uint)i, (_, _, s, _) =>
			{
				fights++;
				if (fights == 2 && k == 1) return Rec(s, false, true);
				if (fights == 2 && k == 2) return Rec(s, false);
				return Rec(s, true);
			}));
		}
		Assert.True(batch.Runs[0].Won && batch.Runs[3].Won && batch.Runs[1].Stalled && !batch.Runs[2].Stalled && !batch.Runs[2].Won, "setup: win, stall, loss, win");
		var sum = Summary(batch, out var doc);
		using (doc)
		{
			var lv = sum.GetProperty("levels");
			var first = lv.GetProperty(levels.Levels[0].Level!);   // runs 0 (won) and 2 (lost): 50%, nothing excluded
			Assert.Equal(50.0, first.GetProperty("run_win_pct").GetDouble(), "first level: 1 of 2");
			Assert.Equal(0, first.GetProperty("runs_stalled").GetInt32(), "first level: no stall");
			var second = lv.GetProperty(levels.Levels[1].Level!);  // runs 1 (stalled) and 3 (won): 1 of 1
			Assert.Equal(100.0, second.GetProperty("run_win_pct").GetDouble(), "second level: the stall is out of the denominator");
			Assert.Equal(1, second.GetProperty("runs_stalled").GetInt32(), "second level: one stall");
			Assert.Equal(1, second.GetProperty("duels_stalled").GetInt32(), "second level: one stalled duel");
			Assert.Equal(1, sum.GetProperty("runs_stalled").GetInt32(), "overall stalls");
			Assert.Equal(1, sum.GetProperty("runs_ended_by_row").EnumerateObject().Sum(p => p.Value.GetInt32()), "one lost run only");
			Assert.Equal(1, sum.GetProperty("runs_stalled_by_row").EnumerateObject().Sum(p => p.Value.GetInt32()), "one stalled run");
			Assert.True(Math.Abs(100.0 * 2 / 3 - sum.GetProperty("reach_tanuki_pct").GetDouble()) < 0.06, "reach: 2 reached of 3 decided runs (the stall is out), got " + sum.GetProperty("reach_tanuki_pct").GetRawText() + " reached=" + sum.GetProperty("reached_tanuki").GetRawText());
		}
	}

	static FightLog LogWith(int ryoHits, int ryoBlocked, int foeHits, int foeBlocked)
	{
		var log = new FightLog(false);
		log.Moves[0]["a"] = new MoveTally { Hits = ryoHits, Blocked = ryoBlocked };
		log.Moves[1]["b"] = new MoveTally { Hits = foeHits, Blocked = foeBlocked };
		return log;
	}

	static JsonElement LevelAfter(Func<RunSession, FightRecord> fight, out JsonDocument doc)
	{
		var bot = Bot;
		var batch = new BatchResult(bot, 1);
		batch.Runs.Add(RunHarness.PlayRun(Content, bot, 1, (_, _, s, _) => fight(s)));
		var sum = Summary(batch, out doc);
		return sum.GetProperty("levels").GetProperty(bot.Level ?? "none");
	}

	// Designer 2026-10-21: exchange_ratio = unblocked hits landed / taken; x/0 is null, never a number or a crash.
	[Test]
	public static void ExchangeRatio_ZeroUnblockedHitsTaken_IsNull_AndBlockedHitsDoNotCount()
	{
		var l = LevelAfter(s => Rec(s, false, false, LogWith(5, 0, 3, 3)), out var d1); // taken 3 hits, all blocked: 0 clean
		using (d1)
		{
			Assert.Equal(JsonValueKind.Null, l.GetProperty("exchange_ratio").ValueKind, "0 unblocked hits taken: null");
			Assert.True(l.GetProperty("damage_ratio").ValueKind == JsonValueKind.Number, "damage ratio still a number (dealt 20 / taken 10)");
		}
		var l2 = LevelAfter(s => Rec(s, false, false, LogWith(6, 2, 4, 2)), out var d2); // 4 clean / 2 clean
		using (d2) Assert.Equal(2.0, l2.GetProperty("exchange_ratio").GetDouble(), "4 clean landed / 2 clean taken");
		var l3 = LevelAfter(s => Rec(s, false, false, LogWith(0, 0, 0, 0)), out var d3);
		using (d3) Assert.Equal(JsonValueKind.Null, l3.GetProperty("exchange_ratio").ValueKind, "0/0 null");
	}

	[Test]
	public static void ExchangeRatio_NullWithoutLog_AndOldFieldGone()
	{
		var l = LevelAfter(s => Rec(s, false), out var d);
		using (d)
		{
			Assert.True(l.TryGetProperty("exchange_ratio", out var er) && er.ValueKind == JsonValueKind.Null, "exchange_ratio present and null without the log");
			Assert.True(!l.TryGetProperty("hit_exchange_ratio", out _), "hit_exchange_ratio removed");
		}
	}

	[Test]
	public static void AllFightsStalled_EveryRateNull_NoDivisionByZero()
	{
		var bot = Bot;
		var batch = new BatchResult(bot, 1);
		for (uint i = 1; i <= 3; i++) batch.Runs.Add(RunHarness.PlayRun(Content, bot, i, (_, _, s, _) => Rec(s, false, true)));
		var sum = Summary(batch, out var doc);
		using (doc)
		{
			Assert.Equal(JsonValueKind.Null, sum.GetProperty("reach_tanuki_pct").ValueKind, "reach null");
			Assert.Equal(JsonValueKind.Null, sum.GetProperty("beat_tanuki_pct_of_reached").ValueKind, "beat null");
			var l = sum.GetProperty("levels").GetProperty(bot.Level ?? "none");
			Assert.Equal(JsonValueKind.Null, l.GetProperty("run_win_pct").ValueKind, "run win null");
			Assert.Equal(JsonValueKind.Null, l.GetProperty("duel_win_pct").ValueKind, "duel win null");
			Assert.Equal(3, l.GetProperty("duels_stalled").GetInt32(), "3 stalled duels");
		}
	}

	// Determinism of whole batches (real fights, twists on): same seed twice is the same JSON; another seed is not.
	[Test]
	public static void RealBatch_SameSeedSameJson_OtherSeedDiffers()
	{
		var bot = Bot;
		string a = HarnessReport.ToJson(RunHarness.PlayBatch(Content, bot, 7, 3));
		string b = HarnessReport.ToJson(RunHarness.PlayBatch(Content, bot, 7, 3));
		string c = HarnessReport.ToJson(RunHarness.PlayBatch(Content, bot, 8, 3));
		Assert.True(a == b, "the same seed and bot give byte-identical results");
		Assert.True(a != c, "another seed gives other results");
	}
}
