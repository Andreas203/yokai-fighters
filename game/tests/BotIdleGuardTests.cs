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

	/// <summary>Per tick: Ryo's input, then whether he guarded and attacked on that tick. The opponent stands still,
	/// or with <paramref name="hopper"/> jumps forward over Ryo whenever it is grounded and idle (cross-ups).</summary>
	static (List<(FighterInput In, bool Guarding, bool Attacking)> Ticks, NewcomerBot Bot) Play(BotFightConfig cfg, int ticks = 900, uint seed = 7, List<int>? sides = null, bool hopper = false)
	{
		var session = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5);
		Assert.True(session.Enter(session.Reachable()[0]).Ok, "enter row 1");
		var m = NodeFight.NewMatch(Content.Sources, session.Run, session, session.Node!.Fighter, ControlScheme.Kihon);
		var bot = new NewcomerBot(cfg, BotKit.From(m.P1), seed);
		var log = new List<(FighterInput, bool, bool)>();
		for (int t = 0; t < ticks && m.Phase != MatchPhase.Over; t++)
		{
			var input = bot.Next(AiView.Capture(m));
			var p2 = FighterInput.None;
			if (hopper && !m.P2.Airborne && m.P2.State == FighterState.Idle)
				p2 = new FighterInput(InputBits.Up | (m.P1.X < m.P2.X ? InputBits.Left : InputBits.Right));
			m.Step(input, p2);
			if (m.Phase == MatchPhase.Fighting) sides?.Add(Math.Sign(m.P2.X - m.P1.X));
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

	/// <summary>
	/// Test Breaker round 1: the opponent keeps jumping over Ryo. Once the delayed view has seen a side switch (P6: not
	/// before) the held direction is away from the opponent again, so the guard is never stuck walking into it.
	/// </summary>
	[Test]
	public static void IdleGuard_100_CrossUp_GuardFollowsTheDelayedSide()
	{
		var sides = new List<int>();
		var cfg = Only(wait: 1) with { AntiAirPct = 0 }; // the hops would draw anti-airs, which drop the guard on purpose
		var (ticks, _) = Play(cfg, 1200, 7, sides, hopper: true);
		int first = ticks.FindIndex(t => t.In.Bits != InputBits.None), lag = cfg.ReactionFrames + 1, switches = 0, checkedTicks = 0;
		for (int i = first + lag + 1; i < ticks.Count; i++)
		{
			int seen = sides[i - lag];
			if (seen != sides[i - lag - 1]) switches++;
			bool stable = true;
			for (int k = i - lag; k <= i; k++) stable &= sides[k] == seen && seen != 0;
			if (!stable) continue;
			checkedTicks++;
			InputBits away = seen > 0 ? InputBits.Left : InputBits.Right;
			Assert.Equal(away, ticks[i].In.Bits, $"tick {i}: held away from the opponent on the side the bot saw");
		}
		Assert.True(switches >= 2, $"the opponent crossed Ryo ({switches} side switches)");
		Assert.True(checkedTicks > 200, $"enough settled ticks checked ({checkedTicks})");
	}

	/// <summary>
	/// YOK-166 (designer 2026-10-20, C3, P6): the reactive Block plan follows a cross-up the same way. Staged screens: the
	/// opponent swings from Ryo's right, the bot blocks (holds left); mid-swing it shows on his left. The block is the same
	/// plan (one roll) and, once the delayed view (P6: <c>reaction_frames</c> later, not sooner) shows the new side, holds
	/// right. The old code fixed the direction when the block started and walked into the opponent here.
	/// </summary>
	[Test]
	public static void Block_100_CrossUp_GuardFollowsTheDelayedSide()
	{
		var session = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5);
		Assert.True(session.Enter(session.Reachable()[0]).Ok, "enter row 1");
		var m = NodeFight.NewMatch(Content.Sources, session.Run, session, session.Node!.Fighter, ControlScheme.Kihon);
		const int Delay = 4;
		var cfg = Only(wait: 1, idleGuard: 0) with { ReactionFrames = Delay, DecideEvery = 1, AntiAirPct = 0, BlockPct = 100, BlockTicks = 30 };
		var bot = new NewcomerBot(cfg, BotKit.From(m.P1), 7);
		var b = AiView.Capture(m);
		int far = 100 * SimConfig.Scale;
		AiView Swing(int tick, int p1x, int p2x) => b with
		{
			Tick = tick,
			P1 = b.P1 with { X = p1x, Facing = Math.Sign(p2x - p1x) },
			P2 = b.P2 with { X = p2x, Facing = Math.Sign(p1x - p2x), State = FighterState.Attack, MoveId = "swing", MoveFrame = 1, MoveLastActive = 200 },
		};
		int t = 0;
		var outs = new List<InputBits>();
		for (int i = 0; i < 10; i++) outs.Add(bot.Next(Swing(t++, 0, far)).Bits);
		Assert.Equal(1, bot.Stats.Blocks, "one block rolled for the swing");
		Assert.Equal(InputBits.Left, outs[^1], "the block holds away from the opponent on the right");
		outs.Clear();
		for (int i = 0; i < 12; i++) outs.Add(bot.Next(Swing(t++, far, 0)).Bits);
		for (int i = 0; i < Delay - 1; i++)
			Assert.Equal(InputBits.Left, outs[i], $"cross-up tick {i}: the bot has not seen it yet (P6), still holds left");
		for (int i = Delay - 1; i < outs.Count; i++)
			Assert.Equal(InputBits.Right, outs[i], $"cross-up tick {i}: seen, the block holds away from the opponent on the left");
		Assert.Equal(1, bot.Stats.Blocks, "the same block plan, no new roll");
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
	/// A regression pin, not a proof: the first runs of the seed-1 batch at idle_guard_pct 0 play to pinned session hashes,
	/// so any later change to how the bot plays at 0 shows. The pins were taken from this code (YOK-166, designer 2026-10-20,
	/// new R5 baseline: the reactive Block follows a cross-up, which changes 25 of the 200 runs at 0, run 3 among them; the
	/// old pins were already stale on main after YOK-156/157). That a 0 roll draws nothing from the RNG is proved directly by
	/// <see cref="IdleGuard_0_DrawsNothingFromTheRng"/>.
	/// </summary>
	/// <summary>
	/// YOK-155, checked directly (code-critic, YOK-166): an idle_guard_pct of 0 (written or absent) draws nothing from the
	/// bot's RNG. With waits only, no mistakes and no threat, one decision draws exactly the weight pick at 0, and one more
	/// (the guard roll) at 100. Compared with a SimRng of the same seed advanced by hand.
	/// </summary>
	[Test]
	public static void IdleGuard_0_DrawsNothingFromTheRng()
	{
		var session = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5);
		Assert.True(session.Enter(session.Reachable()[0]).Ok, "enter row 1");
		var m = NodeFight.NewMatch(Content.Sources, session.Run, session, session.Node!.Fighter, ControlScheme.Kihon);
		var view = AiView.Capture(m);
		Assert.True(view.Phase == MatchPhase.Fighting && !view.P2.Threatening && !view.P2.Airborne, "a quiet screen: no threat, no jump");
		string json = System.IO.File.ReadAllText(System.IO.Path.Combine(ContentPaths.DataDir, "harness", BotConfig.DefaultId + ".json"));
		var absent = BotConfig.Parse(json.Replace("\"idle_guard_pct\": 50,", "")).Fight;
		const uint Seed = 11;
		uint After(int draws) { var r = new SimRng(Seed); for (int i = 0; i < draws; i++) r.NextUInt(); return r.State; }
		foreach (var (name, fight, draws) in new[]
		{
			("written 0", Only(wait: 1, idleGuard: 0) with { DecideEvery = 1, BlockPct = 0, AntiAirPct = 0 }, 1),
			("absent", absent with { WalkWeight = 0, NormalWeight = 0, SpecialWeight = 0, ThrowWeight = 0, JumpWeight = 0, WaitWeight = 1, MistakePct = 0, DecideEvery = 1, BlockPct = 0, AntiAirPct = 0 }, 1),
			("100", Only(wait: 1, idleGuard: 100) with { DecideEvery = 1, BlockPct = 0, AntiAirPct = 0 }, 2),
		})
		{
			var bot = new NewcomerBot(fight, BotKit.From(m.P1), Seed);
			Assert.Equal(After(0), bot.RngState, $"{name}: nothing drawn before the first decision");
			bot.Next(view);
			Assert.Equal(After(draws), bot.RngState, $"{name}: one wait decision draws {draws}");
		}
		Assert.Equal(0, absent.IdleGuardPct, "absent reads as 0");
	}

	[Test]
	public static void IdleGuard_0_PlaysTheOldBatch()
	{
		var bot = Bot with { Fight = Bot.Fight with { IdleGuardPct = 0 } };
		var batch = RunHarness.PlayBatch(Content, bot, 1, 3);
		// YOK-174: seed 2 re-pinned (cd0500c43b604051, was e75276f37981d48f): its row-2 node now rolls its temperament (designer, Y1/Y2).
		var want = new[] { "1144460045766550", "cd0500c43b604051", "8091329b164ab95a" };
		using var doc = System.Text.Json.JsonDocument.Parse(HarnessReport.ToJson(batch));
		var runs = doc.RootElement.GetProperty("runs");
		var got = Enumerable.Range(0, want.Length).Select(i => runs[i].GetProperty("session_hash").GetString()).ToArray();
		Assert.Equal(string.Join(" ", want), string.Join(" ", got), "runs seed 1-3");
		foreach (var run in runs.EnumerateArray())
			foreach (var node in run.GetProperty("nodes").EnumerateArray())
				if (node.TryGetProperty("fight", out var f))
					Assert.Equal(0, f.GetProperty("bot").GetProperty("idle_guards").GetInt32(), "no idle guards at 0");
	}
}
