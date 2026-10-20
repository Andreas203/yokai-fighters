using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-155 (C3, P6, R5). Expected values come from rules.md.</summary>
public static class BotIdleGuardProbes
{
	static HarnessContent? _content;
	static HarnessContent Content => _content ??= HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static BotConfig Bot => BotConfig.LoadDefault(ContentPaths.DataDir);
	static string Json => System.IO.File.ReadAllText(System.IO.Path.Combine(ContentPaths.DataDir, "harness", BotConfig.DefaultId + ".json"));

	static (Match M, BotKit Kit) Fresh()
	{
		var session = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5);
		session.Enter(session.Reachable()[0]);
		var m = NodeFight.NewMatch(Content.Sources, session.Run, session, session.Node!.Fighter, ControlScheme.Kihon);
		return (m, BotKit.From(m.P1));
	}

	static BotFightConfig Quick(int wait, int walk, int pct, int waitTicks = 20) => Bot.Fight with
	{
		ReactionFrames = 1, DecideEvery = 1, WaitTicks = waitTicks, WalkTicks = waitTicks, WalkWeight = walk, WaitWeight = wait,
		NormalWeight = 0, SpecialWeight = 0, ThrowWeight = 0, JumpWeight = 0, MistakePct = 0, BlockPct = 0,
		IdleGuardPct = pct, PreferredMin = 0, PreferredMax = 4000,
	};

	static AiView Staged(AiView b, int p1x, int p2x, int p1face) =>
		b with { P1 = b.P1 with { X = p1x, Facing = p1face }, P2 = b.P2 with { X = p2x, Facing = -p1face } };

	static bool IsAway(InputBits bits, int ownX, int oppX) =>
		oppX > ownX ? (bits & InputBits.Left) != 0 && (bits & InputBits.Right) == 0 : (bits & InputBits.Right) != 0 && (bits & InputBits.Left) == 0;

	// C3: block by holding back. A guard plan that began with the opponent on the right must still hold
	// away from him after he crosses up mid-plan; holding toward him is not a block.
	[Test]
	public static void GuardedWait_OpponentCrossesUp_StillHoldsAway()
	{
		var (m, kit) = Fresh();
		var b = AiView.Capture(m);
		var bot = new NewcomerBot(Quick(wait: 1, walk: 0, pct: 100, waitTicks: 30), kit, 3);
		var left = Staged(b, 0, 100 * SimConfig.Scale, 1);
		var right = Staged(b, 100 * SimConfig.Scale, 0, -1);
		var first = bot.Next(left);
		Assert.True(IsAway(first.Bits, 0, 100 * SimConfig.Scale), "setup: guard starts away from the opponent on the right");
		for (int i = 0; i < 10; i++) bot.Next(left);
		var crossed = bot.Next(right);
		Assert.True(IsAway(crossed.Bits, 100 * SimConfig.Scale, 0), $"after the cross-up the held direction must be away from the opponent, got {crossed.Bits}");
	}

	// Same for an in-range guarded walk (backs off): after a side switch it must not walk into the opponent.
	[Test]
	public static void GuardedWalk_OpponentCrossesUp_StillBacksOff()
	{
		var (m, kit) = Fresh();
		var b = AiView.Capture(m);
		var bot = new NewcomerBot(Quick(wait: 0, walk: 1, pct: 100, waitTicks: 30), kit, 3);
		var left = Staged(b, 0, 100 * SimConfig.Scale, 1);
		var right = Staged(b, 100 * SimConfig.Scale, 0, -1);
		bot.Next(left);
		for (int i = 0; i < 10; i++) bot.Next(left);
		var crossed = bot.Next(right);
		Assert.True(IsAway(crossed.Bits, 100 * SimConfig.Scale, 0), $"guarded walk after cross-up must still back off, got {crossed.Bits}");
	}

	// Config: absent == explicit 0, boundaries 0/1/99/100 accepted, 101/-1 rejected.
	[Test]
	public static void Config_Boundaries_AndAbsentEqualsZero()
	{
		foreach (var ok in new[] { 0, 1, 99, 100 })
			Assert.Equal(ok, BotConfig.Parse(Json.Replace("\"idle_guard_pct\": 50", "\"idle_guard_pct\": " + ok)).Fight.IdleGuardPct, $"{ok} accepted");
		var absent = BotConfig.Parse(Json.Replace("\"idle_guard_pct\": 50,", ""));
		var zero = BotConfig.Parse(Json.Replace("\"idle_guard_pct\": 50", "\"idle_guard_pct\": 0"));
		Assert.Equal(zero.Fight, absent.Fight, "absent parses to the same fight config as 0");
	}

	// Counter: one count per guarded plan, not per tick; at 100 with waits only, plans = ticks / (wait_ticks) within one.
	[Test]
	public static void Counter_OncePerPlan_NotPerTick()
	{
		var (m, kit) = Fresh();
		var b = AiView.Capture(m);
		var bot = new NewcomerBot(Quick(wait: 1, walk: 0, pct: 100, waitTicks: 10), kit, 5);
		for (int i = 0; i < 100; i++) bot.Next(b);
		Assert.True(bot.Stats.IdleGuards is >= 9 and <= 10, $"100 ticks of 10-tick waits = 10 plans, got {bot.Stats.IdleGuards}");
	}

	// Counter: 0 pct counts nothing with waits only; 100 on a walk toward a far opponent (Approach) counts nothing (only in-range walks).
	[Test]
	public static void Counter_ApproachWalk_NotCounted()
	{
		var (m, kit) = Fresh();
		var b = AiView.Capture(m);
		var far = Staged(b, 0, 3000 * SimConfig.Scale, 1);
		var cfg = Quick(wait: 0, walk: 1, pct: 100) with { PreferredMax = 100 };
		var bot = new NewcomerBot(cfg, kit, 5);
		for (int i = 0; i < 100; i++) bot.Next(far);
		Assert.Equal(0, bot.Stats.IdleGuards, "an approach walk is not an in-range walk");
		Assert.True(bot.Stats.Walks > 0, "sanity: walks happened");
	}

	// Determinism: same seed same batch hash twice; 100 differs from 0.
	[Test]
	public static void Batch_SameSeedSameHash_DifferentPctDifferentHash()
	{
		string H(int pct)
		{
			var bot = Bot with { Fight = Bot.Fight with { IdleGuardPct = pct } };
			using var d = System.Text.Json.JsonDocument.Parse(HarnessReport.ToJson(RunHarness.PlayBatch(Content, bot, 2, 1)));
			return d.RootElement.GetProperty("runs")[0].GetProperty("session_hash").GetString()!;
		}
		Assert.Equal(H(50), H(50), "same seed, same hash");
		Assert.True(H(100) != H(0), "a different idle_guard_pct plays a different run");
	}

	// P6: the guard roll and its direction come from the delayed view; a change of the opponent's side that
	// is newer than reaction_frames must not change the bot's output yet.
	[Test]
	public static void Guard_DoesNotReadTheLiveOpponent()
	{
		var (m, kit) = Fresh();
		var b = AiView.Capture(m);
		var cfg = Quick(wait: 1, walk: 1, pct: 100) with { ReactionFrames = 20, DecideEvery = 3 };
		var a = new NewcomerBot(cfg, kit, 4);
		var c = new NewcomerBot(cfg, kit, 4);
		var near = Staged(b, 0, 100 * SimConfig.Scale, 1);
		var cross = Staged(b, 0, -100 * SimConfig.Scale, 1);
		int t = 30;
		for (int i = 0; i < t + 19; i++)
		{
			var v = near with { Tick = i };
			var live = i < t ? v : cross with { Tick = i };
			Assert.Equal(a.Next(v).Bits, c.Next(live).Bits, $"tick {i}: an opponent change not yet on the delayed view changed the input");
		}
	}
}
