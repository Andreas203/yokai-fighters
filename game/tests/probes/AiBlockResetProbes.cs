using System;
using System.Collections.Generic;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-252 round-3 probes (Test Breaker). Fixture kits only; expected values from E22 (block plans 20), Y4, P6.</summary>
public static class AiBlockResetProbes
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

	static BehaviourProfile Profile(int reaction, string behaviours, string range = "close") => BehaviourProfile.Parse($$"""
		{ "kind": "profile", "id": "probe-reset", "yokai": "kitsune", "temperament": "patient",
		  "reaction": { "tier": "yokai", "frames": {{reaction}} }, "aggression_bias": 50, "preferred_range": "{{range}}",
		  "habit": { "id": "jumps-after-knockdown", "description": "d", "when": "self_got_up", "do": "jump", "chance_pct": 60 },
		  "behaviours": [ {{behaviours}} ], "rules": ["Y4"] }
		""");

	const string BlockOnAttack = """{ "when": "opponent_attacking", "do": "block", "weight": 100 }""";

	// E22 / Y4: reset at any offset into a block, any reaction delay: round 2 has no threat, so after the delayed screen
	// catches up the AI must not guard. Reaction 22 with an early reset is where view.Tick > now.Tick can stop holding.
	[Test]
	public static void Reset_AtEveryOffsetIntoABlock_NoGuardAfterTheLag() => ResetOffsets(new[] { 4, 12, 22 });

	static void ResetOffsets(int[] reactions)
	{
		var bad = new List<string>();
		foreach (int reaction in reactions)
		foreach (int off in new[] { 0, 1, 3, 6, 10, 15 })
		{
			var m = NewMatch();
			Place(m, 240);
			var ai = new ProfileAi(Profile(reaction, BlockOnAttack), 1, AiLoadout.From(m.P2), 5);
			int firstGuard = -1, resetAt = -1, late = 0;
			for (int t = 1; t <= 220 && m.Phase == MatchPhase.Fighting; t++)
			{
				var aiIn = ai.Next(AiView.Capture(m));
				m.Step(t == 10 ? new FighterInput(InputBits.LightPunch) : FighterInput.None, aiIn);
				if (firstGuard < 0 && m.P2.Guarding) firstGuard = t;
				if (firstGuard > 0 && resetAt < 0 && t == firstGuard + off) { resetAt = t; m.Reset(); Place(m, 240); continue; }
				if (resetAt > 0 && t > resetAt + reaction + 2 && m.P2.Guarding) late++;
			}
			if (resetAt < 0) bad.Add($"r{reaction} off{off}: setup, no guard");
			else if (late != 0) bad.Add($"r{reaction} off{off}: {late} guard ticks after the lag");
		}
		Assert.True(bad.Count == 0, string.Join("; ", bad));
	}

	// Y4: several resets in a row (every 2 ticks, 6 times), then a quiet round: no guard, no throw, no input after the lag.
	[Test]
	public static void SeveralResetsInARow_LeaveNoPlan()
	{
		foreach (int reaction in new[] { 4, 22 })
		{
			var m = NewMatch();
			Place(m, 240);
			var ai = new ProfileAi(Profile(reaction, BlockOnAttack), 1, AiLoadout.From(m.P2), 5);
			int firstGuard = -1, resets = 0, lastReset = -1, late = 0;
			for (int t = 1; t <= 200 && m.Phase == MatchPhase.Fighting; t++)
			{
				var aiIn = ai.Next(AiView.Capture(m));
				m.Step(t == 10 ? new FighterInput(InputBits.LightPunch) : FighterInput.None, aiIn);
				if (firstGuard < 0 && m.P2.Guarding) firstGuard = t;
				if (firstGuard > 0 && resets < 6 && t >= firstGuard + 2 * (resets + 1)) { resets++; lastReset = t; m.Reset(); Place(m, 240); continue; }
				if (lastReset > 0 && resets == 6 && t > lastReset + reaction + 2 && m.P2.Guarding) late++;
			}
			Assert.True(resets == 6, $"setup (reaction {reaction}): six resets ({resets})");
			Assert.Equal(0, late, $"reaction {reaction}: no guard after six resets and the lag");
		}
	}

	// Y4 / P6: a reset in normal play must not blind the AI forever: in round 2 a real attack is still answered by a block.
	[Test]
	public static void AfterAReset_ARealAttackInTheNextRound_StillBlocks()
	{
		foreach (int reaction in new[] { 4, 22 })
		{
			var m = NewMatch();
			Place(m, 240);
			var ai = new ProfileAi(Profile(reaction, BlockOnAttack), 1, AiLoadout.From(m.P2), 5);
			int firstGuard = -1, resetAt = -1, guard2 = 0;
			for (int t = 1; t <= 300 && m.Phase == MatchPhase.Fighting; t++)
			{
				var aiIn = ai.Next(AiView.Capture(m));
				FighterInput p1 = FighterInput.None;
				if (resetAt < 0 && t == 10) p1 = new FighterInput(InputBits.LightPunch);
				if (resetAt > 0 && t == resetAt + 60) p1 = new FighterInput(InputBits.LightPunch);
				m.Step(p1, aiIn);
				if (firstGuard < 0 && m.P2.Guarding) firstGuard = t;
				if (firstGuard > 0 && resetAt < 0 && t == firstGuard + 5) { resetAt = t; m.Reset(); Place(m, 240); continue; }
				if (resetAt > 0 && t > resetAt + 60 && m.P2.Guarding) guard2++;
			}
			Assert.True(resetAt > 0, "setup");
			Assert.True(guard2 > 0, $"reaction {reaction}: round 2's attack is answered with a guard");
		}
	}

	// Determinism: the same run with resets twice gives the same StateHash and inputs; and with HeldBlock off the
	// HoldBlockThroughThreat flag changes nothing (no new path, no extra draw).
	[Test]
	public static void FlagsOff_HoldFlagIgnored_AcrossAReset_SameHashAndInputs()
	{
		ulong Run(bool hold, out string inputs)
		{
			var m = NewMatch();
			Place(m, 240);
			var ai = new ProfileAi(Profile(4, BlockOnAttack + """, { "when": "opponent_in_recovery", "do": "block", "weight": 50 }"""), 1, AiLoadout.From(m.P2), 9, heldBlock: false, holdBlockThroughThreat: hold);
			var sb = new System.Text.StringBuilder();
			for (int t = 1; t <= 150 && m.Phase == MatchPhase.Fighting; t++)
			{
				var o = ai.Next(AiView.Capture(m));
				sb.Append((int)o.Bits).Append(',');
				m.Step(t % 40 == 10 ? new FighterInput(InputBits.LightPunch) : FighterInput.None, o);
				if (t == 70) { m.Reset(); Place(m, 240); }
			}
			inputs = sb.ToString();
			return m.StateHash();
		}
		ulong a = Run(false, out var ia), b = Run(true, out var ib), c = Run(false, out var ic);
		Assert.True(ia == ic && a == c, "same flags, same run, same hash and inputs");
		Assert.True(ia == ib && a == b, "HeldBlock off: HoldBlockThroughThreat changes neither inputs nor hash");
	}

	// Default (E22: reactions are data): during a held block, an opponent_whiffed answer that is not a guard still rolls.
	[Test]
	public static void OptionOff_WhiffPunish_RollsDuringAHeldBlock()
	{
		var prof = Profile(4, BlockOnAttack + """, { "when": "opponent_whiffed", "do": "normal:test-ryo-heavy-punch", "weight": 100 }""", "mid");
		var m = NewMatch();
		Place(m, 450); // a light punch from here whiffs
		var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 7) { Log = new() };
		for (int t = 1; t <= 120 && m.Phase == MatchPhase.Fighting; t++)
			m.Step(t == 10 ? new FighterInput(InputBits.LightPunch) : FighterInput.None, ai.Next(AiView.Capture(m)));
		Assert.True(ai.Log!.Exists(d => d.Trigger == "opponent_whiffed"), "the whiff punish rolled (its trigger is logged) with the option off");
	}

	// E22 / Y4: a multi-hit string against a held block: never open in blockstun, on any hit.
	[Test]
	public static void BlockstunOnAString_NeverOpen_OnAnyHit()
	{
		var m = NewMatch();
		Place(m, m.Config.BodyWidth / SimConfig.Scale);
		var ai = new ProfileAi(Profile(4, BlockOnAttack), 1, AiLoadout.From(m.P2), 3);
		int stunTicks = 0, stunOpen = 0, blocked = 0;
		m.Hit += (_, e) => { if (e.Attacker == 0 && e.Blocked) blocked++; };
		for (int t = 1; t <= 200 && m.Phase == MatchPhase.Fighting; t++)
		{
			var o = ai.Next(AiView.Capture(m));
			bool back = m.P1.X < m.P2.X ? (o.Bits & InputBits.Right) != 0 : (o.Bits & InputBits.Left) != 0;
			if (m.P2.State == FighterState.Blockstun) { stunTicks++; if (!back) stunOpen++; }
			m.Step(t == 10 || t == 30 || t == 50 ? new FighterInput(InputBits.MediumKick) : FighterInput.None, o);
		}
		Assert.True(blocked >= 1 && stunTicks > 0, $"setup: blocked {blocked}, stun ticks {stunTicks}");
		Assert.Equal(0, stunOpen, "holds back through every blockstun of the string");
	}
}
