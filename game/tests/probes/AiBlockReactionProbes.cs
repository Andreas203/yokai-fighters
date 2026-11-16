using System;
using System.Collections.Generic;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Replay;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-252 probes (Test Breaker). Fixture kits only; expected values from E22 (block plans 20), Y4, P6.</summary>
public static class AiBlockReactionProbes
{
	static Match NewMatch()
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		try { return FightScene.NewMatch(); }
		finally { FightScene.Sources = saved; }
	}

	static void Place(Match m, int gap)
	{
		m.P1.X = -(gap / 2) * SimConfig.Scale;
		m.P2.X = (gap - gap / 2) * SimConfig.Scale;
	}

	static BehaviourProfile Profile(int reaction, string behaviours) => BehaviourProfile.Parse($$"""
		{ "kind": "profile", "id": "probe-block", "yokai": "kitsune", "temperament": "patient",
		  "reaction": { "tier": "yokai", "frames": {{reaction}} }, "aggression_bias": 50, "preferred_range": "close",
		  "habit": { "id": "jumps-after-knockdown", "description": "d", "when": "self_got_up", "do": "jump", "chance_pct": 60 },
		  "behaviours": [ {{behaviours}} ], "rules": ["Y4"] }
		""");

	const string BlockOnAttack = """{ "when": "opponent_attacking", "do": "block", "weight": 100 }""";

	// E22 / contract 7: a Reset in the middle of a block must not leave guard ticks in the next round.
	// Round 2 has no threat at all, so after the delayed view has caught up the AI must not guard.
	[Test]
	public static void Reset_MidBlock_LeavesNoGuardInTheNextRound()
	{
		foreach (int reaction in new[] { 4, 22 })
		{
			var prof = Profile(reaction, BlockOnAttack);
			var m = NewMatch();
			Place(m, 240);
			var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 5);
			int swing = 10, firstGuard = -1, resetAt = -1, guardAfterCatchUp = 0;
			for (int t = 1; t <= 200 && m.Phase == MatchPhase.Fighting; t++)
			{
				var aiIn = ai.Next(AiView.Capture(m));
				m.Step(t == swing ? new FighterInput(InputBits.LightPunch) : FighterInput.None, aiIn);
				if (firstGuard < 0 && m.P2.Guarding) firstGuard = t;
				if (firstGuard > 0 && resetAt < 0 && t == firstGuard + 8) { resetAt = t; m.Reset(); Place(m, 240); }
				else if (resetAt > 0 && t > resetAt + reaction + 1 && m.P2.Guarding) guardAfterCatchUp++;
			}
			Assert.True(firstGuard > 0, $"setup (reaction {reaction}): the block fired");
			Assert.Equal(0, guardAfterCatchUp, $"reaction {reaction}: no threat in round 2, so no guard left over from round 1's block");
		}
	}

	// E22 / contract 7: the same, measured from the first tick after the reset, allowing only the delayed screen's lag (Y4).
	[Test]
	public static void Reset_MidBlock_GuardEndsWithinReactionFramesOfTheReset()
	{
		var prof = Profile(4, BlockOnAttack);
		var m = NewMatch();
		Place(m, 240);
		var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 5);
		int firstGuard = -1, resetAt = -1, guardTicksAfterReset = 0;
		for (int t = 1; t <= 120 && m.Phase == MatchPhase.Fighting; t++)
		{
			var aiIn = ai.Next(AiView.Capture(m));
			m.Step(t == 10 ? new FighterInput(InputBits.LightPunch) : FighterInput.None, aiIn);
			if (firstGuard < 0 && m.P2.Guarding) firstGuard = t;
			if (firstGuard > 0 && resetAt < 0 && t == firstGuard + 2) { resetAt = t; m.Reset(); Place(m, 240); continue; }
			if (resetAt > 0 && m.P2.Guarding) guardTicksAfterReset++;
		}
		Assert.True(resetAt > 0, "setup: reset mid-block");
		Assert.True(guardTicksAfterReset <= 4 + 2, $"the old block ran {guardTicksAfterReset} more ticks into the new round (view lag is 4)");
	}

	// E22 / contract 3,9: a block reaction at every offset around the end of the AI's own move: the AI never guards
	// while its move plays, and the whole fight shows exactly BlockTicks guard ticks (one reaction, one block).
	[Test]
	public static void BlockReaction_AtEveryOffsetAroundOwnMoveEnd_GuardsExactlyBlockTicks()
	{
		var prof = Profile(4, """{ "when": "at_close_range", "do": "normal:test-ryo-heavy-punch", "weight": 100 }, """ + BlockOnAttack);
		var bad = new List<string>();
		for (int off = 1; off <= 40; off++)
		{
			var m = NewMatch();
			Place(m, 240);
			var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 3);
			int start = -1, guardDuring = 0, guard = 0, p1 = -1;
			for (int t = 1; t <= 160 && m.Phase == MatchPhase.Fighting; t++)
			{
				var aiIn = ai.Next(AiView.Capture(m));
				var in1 = start > 0 && p1 < 0 && t == start + off ? new FighterInput(InputBits.LightPunch) : FighterInput.None;
				if (in1.Bits != InputBits.None) p1 = t;
				m.Step(in1, aiIn);
				if (start < 0 && m.P2.State == FighterState.Attack) start = t;
				if (m.P2.State == FighterState.Attack && m.P2.Guarding) guardDuring++;
				if (m.P2.Guarding) guard++;
				if (start > 0 && p1 > 0 && m.P2.State == FighterState.Attack && p1 > 0 && t > p1 + 200) break;
			}
			if (guardDuring != 0) bad.Add($"off{off}: guarded during own move");
			if (p1 > 0 && guard < ProfileAi.BlockTicks) bad.Add($"off{off}: only {guard} guard ticks");
		}
		Assert.True(bad.Count == 0, string.Join("; ", bad));
	}

	// E22 / contract 9: a block reaction during the AI's own dash_back: no guard while it dashes, BlockTicks after.
	[Test]
	public static void BlockReaction_DuringOwnDashBack_GuardsFromItsFirstActionableTick()
	{
		var prof = Profile(2, """{ "when": "at_close_range", "do": "dash_back", "weight": 100 }, """ + BlockOnAttack);
		var m = NewMatch();
		Place(m, 240);
		var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 3);
		int start = -1, end = -1, guardAfter = 0, guardDuring = 0, p1 = -1;
		for (int t = 1; t <= 200 && m.Phase == MatchPhase.Fighting; t++)
		{
			var aiIn = ai.Next(AiView.Capture(m));
			var in1 = start > 0 && p1 < 0 && t == start + 1 ? new FighterInput(InputBits.LightPunch) : FighterInput.None;
			if (in1.Bits != InputBits.None) p1 = t;
			m.Step(in1, aiIn);
			if (start < 0 && m.P2.State == FighterState.Dash) start = t;
			else if (start > 0 && end < 0 && m.P2.State != FighterState.Dash) end = t;
			if (start > 0 && end < 0 && m.P2.Guarding) guardDuring++;
			if (end > 0 && t < end + ProfileAi.BlockTicks && m.P2.Guarding) guardAfter++;
			if (end > 0 && t >= end + ProfileAi.BlockTicks) break;
		}
		Assert.True(start > 0 && end > start, $"setup: the AI dashed back ({start}..{end})");
		Assert.Equal(0, guardDuring, "no guard inside its own dash");
		Assert.Equal(ProfileAi.BlockTicks, guardAfter, "guards BlockTicks from the end of its dash_back");
	}

	// Contract 6: both flags are captured in the recording, for either value.
	[Test]
	public static void Recording_CapturesBothFlags_ForEveryCombination()
	{
		var prof = Profile(4, BlockOnAttack);
		foreach (bool held in new[] { true, false })
		foreach (bool hold in new[] { true, false })
		{
			var ai = new ProfileAi(prof, 1, AiLoadout.From(NewMatch().P2), 2, heldBlock: held, holdBlockThroughThreat: hold);
			var setup = ReplayRecorder.AiOf(ai, 2)!;
			Assert.Equal(held, setup.HeldBlock, $"HeldBlock recorded (held={held}, hold={hold})");
			Assert.Equal(held && hold, setup.HoldBlockThroughThreat, $"HoldBlockThroughThreat recorded (held={held}, hold={hold}); hold needs held");
		}
	}

	// Contract 5: with HeldBlock off the proposed hold cannot switch on, and the plan is the old fixed one: a block
	// reaction guards at most BlockTicks ticks from the decision and no BlockLeft effect shows after a reset mid-plan.
	[Test]
	public static void FlagOff_GuardsAtMostBlockTicks_AndHoldIsIgnored()
	{
		var prof = Profile(4, BlockOnAttack);
		foreach (bool hold in new[] { false, true })
		{
			var m = NewMatch();
			Place(m, 240);
			var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 3, heldBlock: false, holdBlockThroughThreat: hold);
			Assert.True(!ai.HoldBlockThroughThreat, "hold is off when HeldBlock is off");
			int guard = 0;
			for (int t = 1; t <= 120 && m.Phase == MatchPhase.Fighting; t++)
			{
				var aiIn = ai.Next(AiView.Capture(m));
				m.Step(t == 10 ? new FighterInput(InputBits.LightPunch) : FighterInput.None, aiIn);
				if (m.P2.Guarding) guard++;
			}
			Assert.True(guard > 0 && guard <= ProfileAi.BlockTicks, $"old rule: fixed block plan, {guard} guard ticks");
		}
	}

	// Contract 8: same seed and same round-reset history give the same hash; a different seed's input stream differs somewhere.
	[Test]
	public static void HeldBlock_WithReset_SameSeedSameHash()
	{
		var prof = Profile(10, BlockOnAttack + """, { "when": "opponent_in_recovery", "do": "normal:test-ryo-heavy-punch", "weight": 60 }, { "when": "projectile_on_screen", "do": "jump", "weight": 30 }""");
		string Run(uint seed)
		{
			var m = NewMatch();
			Place(m, 450);
			var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), seed);
			var sb = new System.Text.StringBuilder();
			for (int t = 1; t <= 600; t++)
			{
				if (t % 137 == 0) { m.Reset(); Place(m, 450); }
				var o = ai.Next(AiView.Capture(m));
				sb.Append((int)o.Bits).Append(',').Append(o.Move).Append(';');
				m.Step(t % 45 == 0 ? FighterInput.Special(SpecialSlot.A) : t % 45 == 20 ? new FighterInput(InputBits.MediumKick) : FighterInput.None, o);
			}
			return sb.Append(m.StateHash()).ToString();
		}
		Assert.Equal(Run(11), Run(11), "same seed, same output across resets");
	}
}
