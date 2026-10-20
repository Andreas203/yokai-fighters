using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-155 (R5, Y4, P6): the newcomer bot holds a standing guard through <c>idle_guard_pct</c> of its wait and
/// in-range walk plans. Real row-1 fight against an opponent that never presses anything, so no threat ever shows
/// and every guard seen here comes from the idle guard, not from <c>block_pct</c>.
/// </summary>
public static class BotIdleGuardTests
{
	static HarnessContent? _content;
	static HarnessContent Content => _content ??= HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static BotConfig Bot => BotConfig.LoadDefault(ContentPaths.DataDir);

	static BotFightConfig Only(int walk = 0, int normal = 0, int wait = 0, int idleGuard = 100) => Bot.Fight with
	{
		WalkWeight = walk, NormalWeight = normal, SpecialWeight = 0, ThrowWeight = 0, JumpWeight = 0, WaitWeight = wait,
		MistakePct = 0, IdleGuardPct = idleGuard, PreferredMin = 0, PreferredMax = 4000, PokeRange = 4000,
	};

	/// <summary>Per tick: Ryo's input, then whether he guarded and attacked on that tick. The opponent stands still.</summary>
	static (List<(FighterInput In, bool Guarding, bool Attacking)> Ticks, NewcomerBot Bot) Play(BotFightConfig cfg, int ticks = 900, uint seed = 7)
	{
		var session = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5);
		Assert.True(session.Enter(session.Reachable()[0]).Ok, "enter row 1");
		var m = NodeFight.NewMatch(Content.Sources, session.Run, session, session.Node!.Fighter, ControlScheme.Kihon);
		var bot = new NewcomerBot(cfg, BotKit.From(m.P1), seed);
		var log = new List<(FighterInput, bool, bool)>();
		for (int t = 0; t < ticks && m.Phase != MatchPhase.Over; t++)
		{
			var input = bot.Next(AiView.Capture(m));
			m.Step(input, FighterInput.None);
			if (m.Phase == MatchPhase.Fighting) log.Add((input, m.P1.Guarding, m.P1.State == FighterState.Attack));
		}
		return (log, bot);
	}

	static InputBits BackFor(int facing) => facing >= 0 ? InputBits.Left : InputBits.Right;

	[Test]
	public static void IdleGuard_Config_ReadFromData_AbsentIsZero_OutOfRangeRejected()
	{
		Assert.Equal(50, Bot.Fight.IdleGuardPct, "data/harness/newcomer-kihon-bot.json starts idle_guard_pct at 50");
		string json = System.IO.File.ReadAllText(System.IO.Path.Combine(ContentPaths.DataDir, "harness", BotConfig.DefaultId + ".json"));
		Assert.True(json.Contains("\"idle_guard_pct\": 50"), "the default file names it");
		Assert.Equal(0, BotConfig.Parse(json.Replace("\"idle_guard_pct\": 50,", "")).Fight.IdleGuardPct, "absent = 0, the YOK-26 bot");
		foreach (var bad in new[] { "101", "-1" })
		{
			bool threw = false;
			try { BotConfig.Parse(json.Replace("\"idle_guard_pct\": 50", "\"idle_guard_pct\": " + bad)); }
			catch (FormatException) { threw = true; }
			Assert.True(threw, $"idle_guard_pct {bad} is rejected");
		}
	}

	[Test]
	public static void IdleGuard_100_HeldThroughEveryWait()
	{
		var (ticks, bot) = Play(Only(wait: 1));
		int first = ticks.FindIndex(t => t.In.Bits != InputBits.None);
		Assert.True(first >= 0 && first < 60, $"the first wait plan starts within a second ({first})");
		for (int i = first; i < ticks.Count; i++)
		{
			Assert.True((ticks[i].In.Bits & ~InputBits.Directions) == InputBits.None, $"tick {i}: a wait presses no button");
			Assert.True(ticks[i].In.Bits is InputBits.Left or InputBits.Right, $"tick {i}: back held, no gap ({ticks[i].In.Bits})");
			Assert.True(ticks[i].Guarding, $"tick {i}: standing guard up (C3)");
		}
		Assert.True(bot.Stats.IdleGuards > 0 && bot.Stats.Blocks == 0, $"idle guards counted ({bot.Stats.IdleGuards}), no reactive blocks");
	}

	[Test]
	public static void IdleGuard_100_InRangeWalksBackOff_0_WalksBothWays()
	{
		var (on, onBot) = Play(Only(walk: 1));
		var (off, offBot) = Play(Only(walk: 1, idleGuard: 0));
		int first = on.FindIndex(t => t.In.Bits != InputBits.None);
		Assert.True(first >= 0, "walks start");
		Assert.True(on.Skip(first).All(t => t.Guarding), "every guarded in-range walk holds back");
		Assert.Equal(onBot.Stats.Walks, onBot.Stats.IdleGuards, "every in-range walk rolled a guard at 100");
		Assert.True(off.Any(t => !t.Guarding && t.In.Bits != InputBits.None), "at 0 some walks go forward, unguarded");
		Assert.Equal(0, offBot.Stats.IdleGuards, "at 0 nothing is counted");
	}

	[Test]
	public static void IdleGuard_DroppedToAttack()
	{
		var (ticks, bot) = Play(Only(normal: 1, wait: 1), 1800);
		int drops = 0;
		for (int i = 1; i < ticks.Count; i++)
			if (ticks[i - 1].Guarding && (ticks[i].In.Bits & InputBits.Attacks) != 0)
			{
				drops++;
				Assert.True(ticks[i].Attacking && !ticks[i].Guarding, $"tick {i}: the guard drops and the normal comes out");
			}
		Assert.True(drops > 0 && bot.Stats.Normals > 0 && bot.Stats.IdleGuards > 0, $"a guarded wait goes straight into a normal ({drops} times)");
	}

	[Test]
	public static void IdleGuard_SameSeed_SameInputs_AndPartGuards()
	{
		var a = Play(Only(walk: 1, normal: 1, wait: 1, idleGuard: 50), 1800);
		var b = Play(Only(walk: 1, normal: 1, wait: 1, idleGuard: 50), 1800);
		Assert.True(a.Ticks.Select(t => t.In).SequenceEqual(b.Ticks.Select(t => t.In)), "same seed, same inputs");
		int plans = a.Bot.Stats.Walks + a.Bot.Stats.Normals;
		Assert.True(a.Bot.Stats.IdleGuards > 0 && a.Bot.Stats.IdleGuards < plans, $"50% guards some plans, not all ({a.Bot.Stats.IdleGuards})");
	}

	/// <summary>
	/// 0 is the YOK-26 bot exactly: the first runs of the committed main batch (seed 1, idle guard absent, YOK-154
	/// re-ran it byte-identical) play to the same session hashes, so a 0 roll draws nothing from the RNG.
	/// </summary>
	[Test]
	public static void IdleGuard_0_PlaysTheOldBatch()
	{
		var bot = Bot with { Fight = Bot.Fight with { IdleGuardPct = 0 } };
		var batch = RunHarness.PlayBatch(Content, bot, 1, 3);
		var want = new[] { "c7b99e223b9a0450", "28a9873a8981ac73", "09aec0317e926252" };
		using var doc = System.Text.Json.JsonDocument.Parse(HarnessReport.ToJson(batch));
		var runs = doc.RootElement.GetProperty("runs");
		for (int i = 0; i < want.Length; i++)
			Assert.Equal(want[i], runs[i].GetProperty("session_hash").GetString(), $"run seed {i + 1}");
		foreach (var run in runs.EnumerateArray())
			foreach (var node in run.GetProperty("nodes").EnumerateArray())
				if (node.TryGetProperty("fight", out var f))
					Assert.Equal(0, f.GetProperty("bot").GetProperty("idle_guards").GetInt32(), "no idle guards at 0");
	}
}
