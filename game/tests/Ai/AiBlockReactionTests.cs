using System;
using System.Collections.Generic;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-252 (Y4, P6, E22): a block reaction becomes a guard. Its <see cref="ProfileAi.BlockTicks"/> are counted from the
/// first tick the AI can guard (after its own move, its own dash or a stun it sees), it is held while the threat it
/// answers still shows (the opponent's swing, or a projectile in flight), and no later attacking reaction replaces it
/// while that threat shows. A committed move is never interrupted (the sim has no cancel into guard, Match: canGuard).
/// The old rule (<c>heldBlock: false</c>, recordings made before YOK-252) is pinned alongside.
/// Fight checks use the TEST FIXTURE kits (as AiTests): P1 is Ryo's fixture kit, P2 the fixture Kitsune.
/// </summary>
public static class AiBlockReactionTests
{
	static Match NewMatch()
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		try { return FightScene.NewMatch(); }
		finally { FightScene.Sources = saved; }
	}

	static void Place(Match m, int gapUnits)
	{
		m.P1.X = -(gapUnits / 2) * SimConfig.Scale;
		m.P2.X = (gapUnits - gapUnits / 2) * SimConfig.Scale;
	}

	static BehaviourProfile Profile(string range, int reaction, string behaviours) => BehaviourProfile.Parse($$"""
		{ "kind": "profile", "id": "test-block-reaction", "yokai": "kitsune", "temperament": "patient",
		  "reaction": { "tier": "yokai", "frames": {{reaction}} }, "aggression_bias": 50, "preferred_range": "{{range}}",
		  "habit": { "id": "jumps-after-knockdown", "description": "d", "when": "self_got_up", "do": "jump", "chance_pct": 60 },
		  "behaviours": [ {{behaviours}} ], "rules": ["Y4"] }
		""");

	const string BlockOnAttack = """{ "when": "opponent_attacking", "do": "block", "weight": 100 }""";

	/// <summary>Ryo's Spirit Wave (fixture, slot A) from <paramref name="gap"/> units at tick 30; returns its connects on P2.</summary>
	static List<HitEvent> WaveAt(BehaviourProfile prof, int gap, bool heldBlock, int ticks = 150)
	{
		var m = NewMatch();
		Place(m, gap);
		var hits = new List<HitEvent>();
		m.Hit += (_, e) => { if (e.Attacker == 0) hits.Add(e); };
		var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 7, heldBlock: heldBlock);
		for (int t = 1; t <= ticks && m.Phase == MatchPhase.Fighting; t++)
			m.Step(t == 30 ? FighterInput.Special(SpecialSlot.A) : FighterInput.None, ai.Next(AiView.Capture(m)));
		return hits;
	}

	/// <summary>
	/// Root cause 1: the block plan ran a fixed <see cref="ProfileAi.BlockTicks"/> from the decision, so a Spirit Wave from
	/// mid range, still in flight when those ticks ran out, hit an AI that had gone back to neutral (it holds still in
	/// its band). The guard is now held while the projectile shows.
	/// </summary>
	[Test]
	public static void BlockReaction_HeldWhileItsProjectileIsStillInFlight()
	{
		var prof = Profile("mid", 22, BlockOnAttack);
		var now = WaveAt(prof, 590, heldBlock: true);
		Assert.True(now.Count == 1, $"the wave connects once ({now.Count})");
		Assert.True(now[0].Blocked, "held block: the wave that showed 22+ ticks before it arrived is blocked");
		var old = WaveAt(prof, 590, heldBlock: false);
		Assert.True(old.Count == 1 && !old[0].Blocked, "old rule (pinned): the block plan ran out with the wave in flight, so it hit");
	}

	/// <summary>
	/// Root cause 2: a later event reaction replaced the running block plan. Spirit Wave's recovery starts while the wave
	/// is still in flight, so <c>opponent_in_recovery → attack</c> (oni-patient's heavy punch) turned the guard into a
	/// swing that walked into the wave. While the threat shows, only another guard (or a jump answer, Y4) replaces it.
	/// </summary>
	[Test]
	public static void BlockReaction_NotReplacedByAnAttackReaction_WhileItsThreatShows()
	{
		var prof = Profile("mid", 22, BlockOnAttack + """, { "when": "opponent_in_recovery", "do": "normal:test-ryo-heavy-punch", "weight": 100 }""");
		var now = WaveAt(prof, 450, heldBlock: true);
		Assert.True(now.Count >= 1 && now[0].Blocked, $"held block: the wave is blocked ({Describe(now)})");
		var old = WaveAt(prof, 450, heldBlock: false);
		Assert.True(old.Count >= 1 && !old[0].Blocked, $"old rule (pinned): the recovery punish replaced the guard and the wave hit ({Describe(old)})");
	}

	static string Describe(List<HitEvent> hits) => hits.Count == 0 ? "no connect" : string.Join(", ", hits.ConvertAll(h => $"{h.MoveId} blocked={h.Blocked}"));

	/// <summary>
	/// Root cause 3 (the sparring partner's suspicion, partly): a block reaction that fires while the AI is in its own
	/// move held back for <see cref="ProfileAi.BlockTicks"/> from the decision, so a move longer than that left no guard
	/// at all after it. The move itself is committed (no guard while it plays: the sim only guards from idle); the guard
	/// now starts on its first actionable tick and lasts BlockTicks.
	/// </summary>
	[Test]
	public static void BlockReaction_DuringOwnMove_GuardsFromItsFirstActionableTick()
	{
		var prof = Profile("close", 4, """{ "when": "at_close_range", "do": "normal:test-ryo-heavy-punch", "weight": 100 }, """ + BlockOnAttack);
		foreach (bool held in new[] { true, false })
		{
			var m = NewMatch();
			Place(m, 240);
			var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 3, heldBlock: held);
			int start = -1, end = -1, guardAfter = 0, guardDuring = 0, p1Press = -1;
			for (int t = 1; t <= 200 && m.Phase == MatchPhase.Fighting; t++)
			{
				var aiIn = ai.Next(AiView.Capture(m));
				// P1 swings (out of reach: a jab at 240 units whiffs) on the AI's second move frame.
				var p1 = start > 0 && p1Press < 0 && t == start + 1 ? new FighterInput(InputBits.LightPunch) : FighterInput.None;
				if (p1.Bits != InputBits.None) p1Press = t;
				m.Step(p1, aiIn);
				if (start < 0 && m.P2.State == FighterState.Attack) start = t;
				else if (start > 0 && end < 0 && m.P2.State != FighterState.Attack) end = t;
				if (start > 0 && end < 0 && m.P2.Guarding) guardDuring++;
				if (end > 0 && t < end + ProfileAi.BlockTicks && m.P2.Guarding) guardAfter++;
				if (end > 0 && t >= end + ProfileAi.BlockTicks) break;
			}
			Assert.True(start > 0 && end - start >= ProfileAi.BlockTicks, $"setup: the AI's own move ({end - start} ticks) outlasts a block plan");
			Assert.Equal(0, guardDuring, $"held={held}: its own move is committed (no guard while it plays)");
			if (held) Assert.Equal(ProfileAi.BlockTicks, guardAfter, "held block: guards BlockTicks from its first actionable tick");
			else Assert.Equal(0, guardAfter, "old rule (pinned): the block plan ran out inside its own move");
		}
	}

	/// <summary>The same for the AI's own dash (no count shows on screen; the AI knows its own dash): guard for BlockTicks after it.</summary>
	[Test]
	public static void BlockReaction_DuringOwnDash_GuardsFromItsFirstActionableTick()
	{
		var prof = Profile("close", 2, """{ "when": "at_full_screen", "do": "dash_forward", "weight": 100 }, """ + BlockOnAttack);
		var m = NewMatch();
		Place(m, 900);
		var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 3, heldBlock: true);
		int start = -1, end = -1, guardAfter = 0, p1Press = -1;
		for (int t = 1; t <= 200 && m.Phase == MatchPhase.Fighting; t++)
		{
			var aiIn = ai.Next(AiView.Capture(m));
			var p1 = start > 0 && p1Press < 0 && t == start + 1 ? new FighterInput(InputBits.LightPunch) : FighterInput.None;
			if (p1.Bits != InputBits.None) p1Press = t;
			m.Step(p1, aiIn);
			if (start < 0 && m.P2.State == FighterState.Dash) start = t;
			else if (start > 0 && end < 0 && m.P2.State != FighterState.Dash) end = t;
			if (end > 0 && t < end + ProfileAi.BlockTicks && m.P2.Guarding) guardAfter++;
			if (end > 0 && t >= end + ProfileAi.BlockTicks) break;
		}
		Assert.True(start > 0 && end > start, $"setup: the AI dashed ({start}..{end})");
		Assert.Equal(ProfileAi.BlockTicks, guardAfter, "held block: guards BlockTicks from the end of its own dash");
	}

	/// <summary>A walk is not committed: a block reaction during a walk forward guards on the reaction tick itself (Y4: reaction frames after the swing shows).</summary>
	[Test]
	public static void BlockReaction_DuringWalk_GuardsAtTheReactionTick()
	{
		const int reaction = 6;
		var prof = Profile("close", reaction, """{ "when": "at_mid_range", "do": "walk_forward", "weight": 100 }, """ + BlockOnAttack);
		var m = NewMatch();
		Place(m, 500);
		var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 3, heldBlock: true);
		int press = -1, firstGuard = -1;
		for (int t = 1; t <= 120 && m.Phase == MatchPhase.Fighting; t++)
		{
			var aiIn = ai.Next(AiView.Capture(m));
			var p1 = t == 20 ? new FighterInput(InputBits.LightPunch) : FighterInput.None;
			if (t == 20) press = t;
			bool walking = m.P2.WalkDir != 0;
			m.Step(p1, aiIn);
			if (t == 20) Assert.True(walking, "setup: the AI is walking when the swing starts");
			if (press > 0 && firstGuard < 0 && m.P2.Guarding) firstGuard = t;
		}
		Assert.Equal(press + reaction, firstGuard, "it guards exactly reaction frames after the swing shows, never earlier (P6)");
	}

	/// <summary>Determinism: the same seed gives the same AI output, held block on.</summary>
	[Test]
	public static void HeldBlock_IsDeterministic()
	{
		var prof = Profile("mid", 22, BlockOnAttack + """, { "when": "opponent_in_recovery", "do": "normal:test-ryo-heavy-punch", "weight": 60 }""");
		string Run()
		{
			var m = NewMatch();
			Place(m, 450);
			var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 11);
			var sb = new System.Text.StringBuilder();
			for (int t = 1; t <= 600 && m.Phase == MatchPhase.Fighting; t++)
			{
				var o = ai.Next(AiView.Capture(m));
				sb.Append((int)o.Bits).Append(',').Append(o.Move).Append(';');
				m.Step(t % 45 == 0 ? FighterInput.Special(SpecialSlot.A) : t % 45 == 20 ? new FighterInput(InputBits.MediumKick) : FighterInput.None, o);
			}
			return sb.Append(m.StateHash()).ToString();
		}
		Assert.Equal(Run(), Run(), "same seed, same output");
		Assert.True(new ProfileAi(prof, 1, AiLoadout.From(NewMatch().P2), 1).HeldBlock, "on by default (every new fight)");
	}
}
