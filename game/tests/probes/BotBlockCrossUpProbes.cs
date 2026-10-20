using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-166 (C3 block by holding back, P6 reaction delay, R5). Expected values come from rules.md.</summary>
public static class BotBlockCrossUpProbes
{
	static HarnessContent? _content;
	static HarnessContent Content => _content ??= HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static BotConfig Bot => BotConfig.LoadDefault(ContentPaths.DataDir);
	static readonly int Far = 100 * SimConfig.Scale;

	static (Match M, BotKit Kit) Fresh()
	{
		var session = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5);
		session.Enter(session.Reachable()[0]);
		var m = NodeFight.NewMatch(Content.Sources, session.Run, session, session.Node!.Fighter, ControlScheme.Kihon);
		return (m, BotKit.From(m.P1));
	}

	static BotFightConfig Cfg(int delay, int blockTicks = 30, int blockPct = 100, int idleGuard = 0, int antiAir = 0) => Bot.Fight with
	{
		ReactionFrames = delay, DecideEvery = 1, WaitTicks = 20, WalkTicks = 20, BlockTicks = blockTicks, BlockPct = blockPct,
		WalkWeight = 0, NormalWeight = 0, SpecialWeight = 0, ThrowWeight = 0, JumpWeight = 0, WaitWeight = 1,
		MistakePct = 0, IdleGuardPct = idleGuard, AntiAirPct = antiAir, PreferredMin = 0, PreferredMax = 4000,
	};

	/// <summary>Ryo at p1x, the opponent at p2x mid-swing (threatening), facing each other.</summary>
	static AiView Swing(AiView b, int tick, int p1x, int p2x, bool airborne = false) => b with
	{
		Tick = tick,
		P1 = b.P1 with { X = p1x, Facing = p2x >= p1x ? 1 : -1 },
		P2 = b.P2 with { X = p2x, Facing = p1x > p2x ? 1 : -1, State = FighterState.Attack, MoveId = "swing", MoveFrame = 1, MoveLastActive = 500, Airborne = airborne },
	};

	static AiView Quiet(AiView b, int tick, int p1x, int p2x) => b with
	{
		Tick = tick,
		P1 = b.P1 with { X = p1x, Facing = p2x >= p1x ? 1 : -1 },
		P2 = b.P2 with { X = p2x, Facing = p1x > p2x ? 1 : -1, State = FighterState.Idle, MoveId = null, MoveFrame = 0, MoveLastActive = 0 },
	};

	static InputBits Away(int side) => side > 0 ? InputBits.Left : InputBits.Right;
	static InputBits Dir(InputBits b) => b & (InputBits.Left | InputBits.Right);

	// C3 + P6: several cross-ups inside ONE block plan. Output on tick i = away from the side shown on tick i-(delay-1).
	[Test]
	public static void Block_ManyCrossUps_EachFollowedExactlyDelayLate_OneRoll()
	{
		foreach (int delay in new[] { 1, 2, 3, 8 })
		{
			var (m, kit) = Fresh();
			var b = AiView.Capture(m);
			var bot = new NewcomerBot(Cfg(delay, blockTicks: 200), kit, 5);
			var sides = new List<int>();
			for (int i = 0; i < 60; i++) sides.Add((i / 7) % 2 == 0 ? 1 : -1);
			for (int i = 0; i < 60; i++)
			{
				int side = sides[i];
				var o = bot.Next(Swing(b, i, side > 0 ? 0 : Far, side > 0 ? Far : 0));
				if (i >= 2 * delay)
					Assert.Equal(Away(sides[i - (delay - 1)]), Dir(o.Bits), $"delay {delay} tick {i}");
			}
			Assert.Equal(1, bot.Stats.Blocks, $"delay {delay}: one block plan, no new roll");
		}
	}

	// R5 determinism: a block plan with mid-plan cross-ups draws exactly one RNG value (the block roll), never more.
	[Test]
	public static void Block_CrossUps_DrawNothingAfterTheBlockRoll()
	{
		var (m, kit) = Fresh();
		var b = AiView.Capture(m);
		var bot = new NewcomerBot(Cfg(3, blockTicks: 200), kit, 9);
		var r = new SimRng(9); r.NextUInt();
		for (int i = 0; i < 40; i++) bot.Next(Swing(b, i, (i / 5) % 2 == 0 ? 0 : Far, (i / 5) % 2 == 0 ? Far : 0));
		Assert.Equal(r.State, bot.RngState, "exactly one draw (the block roll) over 40 ticks of block with cross-ups");
	}

	// Side(): equal X falls back to the facing of Ryo on the delayed view (existing tie rule).
	[Test]
	public static void Block_EqualX_FallsBackToFacing()
	{
		foreach (int facing in new[] { 1, -1 })
		{
			var (m, kit) = Fresh();
			var b = AiView.Capture(m);
			var bot = new NewcomerBot(Cfg(1), kit, 5);
			AiView V(int t) => Swing(b, t, 50 * SimConfig.Scale, 50 * SimConfig.Scale) with { P1 = b.P1 with { X = 50 * SimConfig.Scale, Facing = facing } };
			var o = bot.Next(V(0));
			Assert.Equal(1, bot.Stats.Blocks, "blocked");
			Assert.Equal(Away(facing), Dir(o.Bits), $"facing {facing}: tie means opponent counts as in front, hold back");
			Assert.Equal(Away(facing), Dir(bot.Next(V(1)).Bits), "and stays so");
		}
	}

	// A block plan is not interrupted by an anti-air roll (opponent airborne mid-swing): still one Block, no AntiAir, still follows.
	[Test]
	public static void Block_OpponentJumpsMidBlock_NotInterrupted_StillFollows()
	{
		var (m, kit) = Fresh();
		var b = AiView.Capture(m);
		var bot = new NewcomerBot(Cfg(1, blockTicks: 40, antiAir: 100), kit, 5);
		bot.Next(Swing(b, 0, 0, Far));
		for (int i = 1; i < 10; i++) bot.Next(Swing(b, i, 0, Far, airborne: true));
		var o = bot.Next(Swing(b, 10, Far, 0, airborne: true));
		Assert.Equal(1, bot.Stats.Blocks, "one block");
		Assert.Equal(0, bot.Stats.AntiAirs, "block plan is not interruptible by an anti-air");
		Assert.Equal(InputBits.Right, Dir(o.Bits), "after the cross-up the held back is right");
	}

	// After the block ends, nothing stale: a following unguarded wait (idle_guard 0) is neutral even after a cross-up happened in the block.
	[Test]
	public static void Block_Ends_NextPlanHasNoStaleHoldAway()
	{
		var (m, kit) = Fresh();
		var b = AiView.Capture(m);
		var bot = new NewcomerBot(Cfg(1, blockTicks: 5, idleGuard: 0), kit, 5);
		bot.Next(Swing(b, 0, 0, Far));
		bot.Next(Swing(b, 1, Far, 0));
		var last = FighterInput.None;
		for (int i = 2; i < 40; i++) last = bot.Next(Quiet(b, i, Far, 0));
		Assert.Equal(InputBits.None, Dir(last.Bits), "idle_guard 0 wait after a block is neutral, not holding the old guard");
	}

	// Block cleared by a non-Fighting phase (round over): the next plan does not inherit the guard.
	[Test]
	public static void Block_ClearedByPhaseChange_DoesNotLeakIntoNextPlan()
	{
		var (m, kit) = Fresh();
		var b = AiView.Capture(m);
		var bot = new NewcomerBot(Cfg(1, blockTicks: 100, idleGuard: 0), kit, 5);
		bot.Next(Swing(b, 0, 0, Far));
		bot.Next(b with { Tick = 1, Phase = MatchPhase.Over });
		var o = FighterInput.None;
		for (int i = 2; i < 10; i++) o = bot.Next(Quiet(b, i, Far, 0));
		Assert.Equal(InputBits.None, Dir(o.Bits), "fresh unguarded wait after Clear");
	}

	// Read-only accessors: reading RngState does not move the bot, same seed gives the same state trace.
	[Test]
	public static void RngState_IsReadOnly_AndSameSeedSameRun()
	{
		string Run()
		{
			var (m, kit) = Fresh();
			var b = AiView.Capture(m);
			var bot = new NewcomerBot(Cfg(2, blockTicks: 30), kit, 21);
			var states = new List<uint>();
			for (int i = 0; i < 50; i++)
			{
				_ = bot.RngState; _ = bot.RngState;
				bot.Next((i / 9) % 2 == 0 ? Swing(b, i, 0, Far) : Quiet(b, i, 0, Far));
				states.Add(bot.RngState);
			}
			return string.Join(",", states);
		}
		Assert.Equal(Run(), Run(), "same seed, same states");
		var rng = new SimRng(5);
		uint s0 = rng.State;
		_ = rng.State; _ = rng.State;
		Assert.Equal(s0, rng.State, "reading State does not draw");
	}

	// idle_guard_pct 0 vs 100 do not change a block plan (the block is its own plan).
	[Test]
	public static void Block_IdleGuard0And100_SameBlockOutputs()
	{
		string Run(int pct)
		{
			var (m, kit) = Fresh();
			var b = AiView.Capture(m);
			var bot = new NewcomerBot(Cfg(3, blockTicks: 30, idleGuard: pct), kit, 5);
			var outs = new List<InputBits>();
			for (int i = 0; i < 25; i++) outs.Add(Dir(bot.Next(Swing(b, i, i < 10 ? 0 : Far, i < 10 ? Far : 0)).Bits));
			return string.Join(",", outs);
		}
		Assert.Equal(Run(0), Run(100), "block direction trace independent of idle_guard_pct");
	}
}
