using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-179 (P5, Y3): the whole-run harness fights an elder node with its twist, built as the fight scene builds it;
/// and (designer 2026-10-21, no round timer) a fight the safety cap stops is a stall, its own outcome, never a loss.
/// </summary>
public static class HarnessTwistTests
{
	static HarnessContent? _content;
	static HarnessContent Content => _content ??= HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static BotConfig Bot => BotConfig.LoadDefault(ContentPaths.DataDir);

	static FightRecord Stub(RunSession s) => new()
	{
		Profile = "stub", Won = true, Ticks = 600, DamageTaken = 10, HealthLeft = Math.Max(1, s.Run.Health - 10), Bot = new BotStats(),
	};

	/// <summary>The first seed from 1 whose bot route (every fight stubbed as won) enters <paramref name="nodeId"/>.</summary>
	static uint SeedEntering(string nodeId)
	{
		for (uint seed = 1; seed < 200; seed++)
		{
			bool hit = false;
			RunHarness.PlayRun(Content, Bot, seed, (_, _, s, _) => { hit |= s.Node!.NodeId == nodeId; return Stub(s); });
			if (hit) return seed;
		}
		throw new InvalidOperationException($"no seed enters {nodeId}");
	}

	[Test]
	public static void ElderFight_HasItsTwist_AndPlaysExactlyAsTheGameBuiltFight(Node runner)
	{
		const string nineTails = "r5-nine-tails";
		uint seed = SeedEntering(nineTails);
		FightRecord? harness = null, game = null;
		string? sceneTwist = null;
		ulong harnessStart = 0, sceneStart = 0;
		string savedOpponent = FightScene.Opponent;
		RunHarness.PlayRun(Content, Bot, seed, (c, b, s, runSeed) =>
		{
			if (s.Node!.NodeId != nineTails) return Stub(s);
			harnessStart = RunHarness.NewNodeMatch(c, b, s).StateHash();
			harness = RunHarness.FightNode(c, b, s, runSeed);
			// The game's build of the same node: fight.tscn on this session, as the boot flow hands it a node.
			var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			scene.ExternalDrive = true;
			scene.PlayerScheme = b.Scheme;
			scene.Session = s;
			try
			{
				runner.AddChild(scene);
				sceneTwist = scene.Twist?.Id;
				sceneStart = scene.Match.StateHash();
				game = RunHarness.PlayFight(c, b, s, runSeed, scene.Match); // the same bot and AI, on the scene's match
			}
			finally { scene.QueueFree(); FightScene.Opponent = savedOpponent; }
			return harness;
		});
		Assert.True(harness is not null && game is not null, "the Nine-Tails node was fought");
		string expected = Content.Twists.All.Single(t => t.Elder == "nine-tailed-kitsune").Id;
		Assert.Equal(expected, harness!.Twist, "Y3: the harness's elder fight has the Nine-Tails twist");
		Assert.Equal(expected, sceneTwist, "the fight scene puts the same twist on the same node");
		Assert.Equal(sceneStart, harnessStart, "the harness's match starts hash-identical to the scene's");
		Assert.True(harness.TwistFired > 0, $"P5: the twist fired in the fight (seed {seed}): the bot repeats a special and it is reflected");
		Assert.Equal(game!.MatchHash, harness.MatchHash, "played out the same: identical final hash");
		Assert.Equal(game.Ticks, harness.Ticks, "same length");
		Assert.Equal(game.TwistFired, harness.TwistFired, "same twist triggers");
		Assert.Equal(game.Won, harness.Won, "same result");
	}

	[Test]
	public static void EveryElderNode_GetsItsOwnTwist_AndNoOtherNodeHasOne()
	{
		foreach (var mapNode in Content.Map.Nodes)
		{
			if (mapNode.Type is NodeTypeRegistry.Merchant or NodeTypeRegistry.Dojo or NodeTypeRegistry.Boss) continue;
			uint seed = SeedEntering(mapNode.Id);
			Match? m = null;
			RunHarness.PlayRun(Content, Bot, seed, (c, b, s, _) =>
			{
				if (s.Node!.NodeId == mapNode.Id) m = RunHarness.NewNodeMatch(c, b, s);
				return Stub(s);
			});
			var want = Content.Twists.ForNode(mapNode);
			Assert.Equal(mapNode.Elder, want is not null, $"{mapNode.Id}: a twist exactly on elder nodes");
			Assert.Equal(want?.Id, m!.P2.Twist?.Id, $"{mapNode.Id}: P2 fights with the node's twist");
			Assert.True(m.P1.Twist is null, $"{mapNode.Id}: Ryo never holds a twist");
		}
	}

	[Test]
	public static void NonElderFight_IsUnchanged_NoTwistNoTrigger()
	{
		FightRecord? first = null;
		ulong built = 0, plain = 0;
		RunHarness.PlayRun(Content, Bot, 1, (c, b, s, runSeed) =>
		{
			if (first is not null) return Stub(s);
			built = RunHarness.NewNodeMatch(c, b, s).StateHash();
			plain = NodeFight.NewMatch(c.Sources, s.Run, s, s.Node!.Fighter, b.Scheme).StateHash();
			return first = RunHarness.FightNode(c, b, s, runSeed);
		});
		Assert.Equal(plain, built, "row 1: the match is NodeFight's own, untouched");
		Assert.True(first!.Twist is null && first.TwistFired == 0, "no twist, nothing fired");
	}

	[Test]
	public static void SafetyCap_IsLong_AndAFightItStopsIsAStall_NotALoss()
	{
		Assert.True(Bot.Fight.MaxFightTicks >= 108000, "no round timer: the data cap is at least 30 minutes (60 ticks a second)");
		string json = File.ReadAllText(Path.Combine(ContentPaths.DataDir, "harness", BotConfig.DefaultId + ".json"));
		var shortCap = BotConfig.Parse(json.Replace($"\"max_fight_ticks\": {Bot.Fight.MaxFightTicks}", "\"max_fight_ticks\": 600"));
		Assert.Equal(600, shortCap.Fight.MaxFightTicks, "test bot: a 10-second cap so the first fight stalls");

		var batch = RunHarness.PlayBatch(Content, shortCap, 1, 3);
		foreach (var run in batch.Runs)
		{
			var f = run.Nodes[^1].Fight!;
			Assert.True(f.Stalled && !f.Won && f.Ticks == 600, $"seed {run.Seed}: the capped fight is a stall");
			Assert.True(run.Stalled && !run.Won, $"seed {run.Seed}: the run ended on the stall");
		}
		using var doc = JsonDocument.Parse(HarnessReport.ToJson(batch));
		var root = doc.RootElement;
		foreach (var run in root.GetProperty("runs").EnumerateArray())
		{
			Assert.Equal("stalled", run.GetProperty("outcome").GetString(), "the run's outcome is stalled, not lost");
			var fight = run.GetProperty("nodes").EnumerateArray().Last().GetProperty("fight");
			Assert.Equal("stalled", fight.GetProperty("result").GetString(), "the fight's result is stalled");
		}
		var sum = root.GetProperty("summary");
		Assert.Equal(3, sum.GetProperty("fights_stalled").GetInt32(), "fights_stalled");
		Assert.Equal(3, sum.GetProperty("runs_stalled").GetInt32(), "runs_stalled");
		Assert.Equal(600, sum.GetProperty("max_fight_ticks").GetInt32(), "the cap is reported");
		int byProfile = sum.GetProperty("stalls_by_profile").EnumerateObject().Sum(p => p.Value.GetInt32());
		Assert.Equal(3, byProfile, "stalls counted per opponent profile");
		Assert.Equal(3, sum.GetProperty("levels").GetProperty(shortCap.Level ?? "none").GetProperty("duels_stalled").GetInt32(), "stalls counted per level");
		// C1: a stall is neither a win nor a loss, so it leaves every win/reach denominator (here all three runs did).
		Assert.Equal(JsonValueKind.Null, sum.GetProperty("reach_tanuki_pct").ValueKind, "reach: 0 decided runs, null");
		Assert.Equal(3, sum.GetProperty("reach_excluded_stalls").GetInt32(), "the stalls left out of reach");
		Assert.Equal(0, sum.GetProperty("runs_ended_by_row").EnumerateObject().Count(), "no run was lost");
		Assert.Equal(3, sum.GetProperty("runs_stalled_by_row").GetProperty("1").GetInt32(), "three runs stalled on row 1");
		var lvl = sum.GetProperty("levels").GetProperty(shortCap.Level ?? "none");
		Assert.Equal(JsonValueKind.Null, lvl.GetProperty("run_win_pct").ValueKind, "run win %: no decided run");
		Assert.Equal(JsonValueKind.Null, lvl.GetProperty("duel_win_pct").ValueKind, "duel win %: no decided duel");
		Assert.True(root.GetProperty("caveats").GetString()!.Contains("stall"), "the caveats say a stall is not a loss");
	}
}
