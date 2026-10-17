using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using YokaiFighters.Ai;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-26 (G1, R5): the newcomer Kihon bot and the headless whole-run harness. Real content from data/ (the
/// harness plays what the game plays); the plumbing tests swap the fight for a stub so a whole walk is certain.
/// </summary>
public static class RunHarnessTests
{
	static HarnessContent? _content;
	static HarnessContent Content => _content ??= HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static BotConfig Bot => BotConfig.LoadDefault(ContentPaths.DataDir);

	/// <summary>Every fight won, Ryo losing <paramref name="damage"/> of the health he arrived with.</summary>
	static FightRunner Win(int damage) => (_, _, session, _) => new FightRecord
	{
		Profile = "stub", Won = true, Ticks = 600, DamageTaken = damage, HealthLeft = Math.Max(1, session.Run.Health - damage), Bot = new BotStats(),
	};

	[Test]
	public static void Config_DefaultBot_LoadsFromData_AndBadConfigsAreRejected()
	{
		var bot = Bot;
		Assert.Equal(BotConfig.DefaultId, bot.Id, "the default bot is data/harness/newcomer-kihon-bot.json");
		Assert.Equal(ControlScheme.Kihon, bot.Scheme, "K2: the bot plays Kihon");
		Assert.True(bot.Fight.ReactionFrames > 0 && bot.Fight.MaxFightTicks >= 600, "fight numbers read");
		Assert.Equal(CardKind.Upgrade, bot.Run.RewardOrder[0], "reward rule read");
		string json = System.IO.File.ReadAllText(System.IO.Path.Combine(ContentPaths.DataDir, "harness", BotConfig.DefaultId + ".json"));
		foreach (var (from, to, why) in new[]
		{
			("\"kihon\"", "\"kata\"", "a Kata bot (the bot presses Kihon specials)"),
			("\"harness_bot\"", "\"profile\"", "the wrong kind"),
			("\"preferred_min\": 150", "\"preferred_min\": 9999", "preferred_min above preferred_max"),
			("\"mistake_pct\": 15", "\"mistake_pct\": 101", "a percent above 100"),
			("\"merchant\": \"heal\"", "\"merchant\": \"trade\"", "an unknown merchant plan"),
		})
		{
			Assert.True(json.Contains(from), $"fixture text '{from}' is in the config");
			bool threw = false;
			try { BotConfig.Parse(json.Replace(from, to)); } catch (FormatException) { threw = true; }
			Assert.True(threw, $"rejected: {why}");
		}
	}

	[Test]
	public static void Bot_PlaysAWholeRun_StopsTakeTheirHeal_RewardsArePicked_TheBossIsEntered()
	{
		var run = RunHarness.PlayRun(Content, Bot, 7, Win(100));
		Assert.True(run.Won, "every fight won → the run is won");
		Assert.Equal(Content.Map.Rows.Count, run.Nodes.Count, "one node per row (R1)");
		Assert.True(run.Nodes.Select(n => n.Row).SequenceEqual(Content.Map.Rows.OrderBy(r => r)), "rows in order");
		var row4 = run.Nodes.Single(n => n.Row == 4);
		Assert.Equal(NodeTypeRegistry.Merchant, row4.Type, "row 4 is the merchant");
		Assert.Equal("heal", row4.Action, "R6: the bot takes the free heal");
		Assert.Equal(Math.Min(RunState.MaxHealth, row4.ArrivalHealth + Merchant.HealAmount), row4.LeaveHealth, "R6: +25%");
		Assert.True(row4.Fight is null, "a stop is not a fight");
		var row7 = run.Nodes.Single(n => n.Row == 7);
		Assert.Equal(NodeTypeRegistry.Dojo, row7.Type, "the route rule prefers the dojo (data: prefer_types)");
		Assert.Equal("rest", row7.Action, "R7: rest");
		Assert.Equal(Math.Min(RunState.MaxHealth, row7.ArrivalHealth + Dojo.RestHeal), row7.LeaveHealth, "R7: +30%");
		int binds = run.Nodes.Count(n => n.Type is NodeTypeRegistry.Duel or NodeTypeRegistry.ElderDuel);
		Assert.Equal(binds, run.Nodes.Count(n => n.Card is not null), "a card picked after every binding fight (A7)");
		foreach (var n in run.Nodes.Where(n => n.Card is not null))
		{
			string want = new[] { "UPGRADE", "NEW", "MODIFIER" }.First(b => n.Card!.Offered.Any(o => o.StartsWith(b + " ")));
			Assert.Equal(want, n.Card!.Badge, $"{n.Id}: reward rule = the first kind in the order that was offered ({string.Join(", ", n.Card.Offered)})");
		}
		Assert.True(run.ReachedTanuki && run.BeatTanuki, "the boss node was fought and won");
		Assert.True(run.CopyLocked is not null, "T1: the copy was locked on entering the boss node");
		Assert.Equal(run.Nodes.Sum(n => n.CoinsWon), run.Coins, "coins = what the nodes paid (R8)");
		Assert.True(run.Coins > 0, "duels pay coins");
		foreach (var n in run.Nodes.Where(n => n.Fight is not null))
			Assert.Equal(n.ArrivalHealth - 100 + (n.Type is NodeTypeRegistry.Duel or NodeTypeRegistry.ElderDuel ? RunState.BindHeal : 0),
				n.LeaveHealth, $"{n.Id}: health carries (C1) + the bind heal (R3)");
	}

	[Test]
	public static void Bot_LosingAFight_EndsTheRunThere()
	{
		int fights = 0;
		FightRunner loseThird = (c, b, s, seed) => ++fights < 3 ? Win(50)(c, b, s, seed)
			: new FightRecord { Profile = "stub", Won = false, Ticks = 900, DamageTaken = s.Run.Health, HealthLeft = 0, Bot = new BotStats() };
		var run = RunHarness.PlayRun(Content, Bot, 3, loseThird);
		Assert.True(!run.Won && !run.ReachedTanuki, "lost on the third fight");
		Assert.Equal(3, run.RowReached, "the run ended on row 3");
		Assert.Equal(run.Nodes[^1].Id, run.EndNode, "the last node entered is where it ended");
	}

	[Test]
	public static void TanukiFight_IsPlayedWithTheTanukiKit_AndTheLockedCopy()
	{
		FightRecord? boss = null;
		CopyTarget? locked = null;
		FightRunner fights = (c, b, s, seed) =>
		{
			if (s.Node!.Type != NodeTypeRegistry.Boss) return Win(50)(c, b, s, seed);
			locked = s.LockedCopy;
			return boss = RunHarness.FightNode(c, b, s, seed);
		};
		var run = RunHarness.PlayRun(Content, Bot, 11, fights);
		Assert.True(boss is not null && locked is not null, "the boss node was fought for real with a locked copy");
		Assert.Equal(locked!.Slot, boss!.OpponentCopiedSlot, "T0/T1: the Tanuki fought with TanukiKit and the copy in Ryo's slot");
		Assert.Equal(15, boss.ReactionFrames, "Y4: the Tanuki reacts in 15 frames");
		Assert.Equal("tanuki-boss", boss.Profile, "the Tanuki's own profile");
		Assert.True(boss.Ticks > 0 && (boss.Won || boss.TimedOut || boss.HealthLeft == 0), "the fight ran to an end");
		Assert.Equal(boss.Won, run.Won, "the run's outcome is the boss fight's");
	}

	[Test]
	public static void Bot_PressesKihonButtonsOnly_AndItsSpecialsComeOut()
	{
		var session = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5);
		Assert.True(session.Enter(session.Reachable()[0]).Ok, "enter row 1");
		var bot = Bot;
		var m = NodeFight.NewMatch(Content.Sources, session.Run, session, session.Node!.Fighter, bot.Scheme);
		Assert.Equal(ControlScheme.Kihon, m.P1.Input.Scheme, "Ryo is on Kihon");
		var ryo = new NewcomerBot(bot.Fight, BotKit.From(m.P1), 99);
		var ai = new ProfileAi(Content.Profiles(session.Node.Fighter)[0], 1, AiLoadout.From(m.P2), 1, session.Node.ReactionFrames);
		int specials = 0, requests = 0;
		m.SpecialStarted += (_, e) => { if (e.Fighter == 0) { specials++; Assert.True(!e.Precision, "Kihon: no precision bonus (K2)"); } };
		for (int t = 0; t < 3600 && m.Phase != MatchPhase.Over; t++)
		{
			var view = AiView.Capture(m);
			var input = ryo.Next(view);
			if (input.Move != 0) requests++;
			m.Step(input, ai.Next(view));
		}
		Assert.Equal(0, requests, "raw buttons only: no direct slot requests");
		Assert.True(ryo.Stats.Specials > 0 && specials > 0, $"Special + direction brings out Ryo's specials ({ryo.Stats.Specials} pressed, {specials} came out)");
	}

	[Test]
	public static void Bot_SeesOnlyTheScreen_P6()
	{
		// Like ProfileAi (AiTests): nothing in the bot can hold the match, a fighter or an input reader.
		var banned = new[] { typeof(Match), typeof(Fighter), typeof(InputReader), typeof(InputBuffer) };
		foreach (var type in new[] { typeof(NewcomerBot), typeof(BotKit) })
			foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
				Assert.True(!banned.Contains(f.FieldType), $"{type.Name}.{f.Name} is a {f.FieldType.Name}");
		var next = typeof(NewcomerBot).GetMethod(nameof(NewcomerBot.Next))!;
		Assert.True(next.GetParameters().Single().ParameterType == typeof(AiView), "the bot is fed AiView snapshots only");
	}

	[Test]
	public static void Batch_SameSeed_IdenticalFile_DifferentSeed_Differs()
	{
		var bot = Bot;
		string a = HarnessReport.ToJson(RunHarness.PlayBatch(Content, bot, 1, 10));
		string b = HarnessReport.ToJson(RunHarness.PlayBatch(Content, bot, 1, 10));
		Assert.True(a == b, "same seed and data → the same bytes");
		string c = HarnessReport.ToJson(RunHarness.PlayBatch(Content, bot, 1001, 10));
		Assert.True(a != c, "another seed plays other runs");
	}

	[Test]
	public static void Batch_ResultsFile_HasTheSchema()
	{
		var batch = RunHarness.PlayBatch(Content, Bot, 1, 10);
		Assert.Equal($"{BotConfig.DefaultId}-n10-s1.json", HarnessReport.FileName(batch), "file name = bot, runs, seed");
		using var doc = JsonDocument.Parse(HarnessReport.ToJson(batch));
		var root = doc.RootElement;
		Assert.Equal(HarnessReport.Kind, root.GetProperty("kind").GetString(), "kind");
		Assert.Equal(1, root.GetProperty("schema_version").GetInt32(), "schema version");
		Assert.Equal(10, root.GetProperty("runs").GetArrayLength(), "every run");
		var s = root.GetProperty("summary");
		foreach (string k in new[] { "runs", "reached_tanuki", "beat_tanuki", "reach_tanuki_pct", "beat_tanuki_pct_of_reached", "R5_reach_target_pct",
			"R5_beat_target_pct", "runs_ended_by_row", "fights_timed_out", "arrival_health", "damage_per_duel", "run_length", "copy_locked", "cards_picked", "routes" })
			Assert.True(s.TryGetProperty(k, out _), $"summary.{k}");
		foreach (string k in new[] { "row_4", "row_7", "tanuki", "R4_target_heal4_rest7" })
			Assert.True(s.GetProperty("arrival_health").TryGetProperty(k, out _), $"arrival_health.{k}");
		foreach (string tier in new[] { "apprentice", "yokai", "elder", "tanuki" })
			Assert.True(s.GetProperty("damage_per_duel").GetProperty(tier).TryGetProperty("Y4_newcomer_target_taken", out _), $"damage_per_duel.{tier}");
		Assert.True(s.GetProperty("run_length").TryGetProperty("R9_target_minutes", out _), "R9 target");
		foreach (var run in root.GetProperty("runs").EnumerateArray())
		{
			foreach (string k in new[] { "seed", "outcome", "row_reached", "route", "final_health", "coins", "fight_ticks", "estimated_minutes", "copy_locked", "cards_picked", "nodes" })
				Assert.True(run.TryGetProperty(k, out _), $"run.{k}");
			string outcome = run.GetProperty("outcome").GetString()!;
			Assert.True(outcome is "won" or "lost", "outcome");
			foreach (var node in run.GetProperty("nodes").EnumerateArray())
			{
				foreach (string k in new[] { "id", "row", "type", "tier", "action", "arrival_health", "leave_health", "coins_won" })
					Assert.True(node.TryGetProperty(k, out _), $"node.{k}");
				if (node.GetProperty("action").GetString() == "fight")
					foreach (string k in new[] { "result", "ticks", "damage_taken", "damage_dealt", "health_left", "bot" })
						Assert.True(node.GetProperty("fight").TryGetProperty(k, out _), $"fight.{k}");
			}
		}
	}
}
