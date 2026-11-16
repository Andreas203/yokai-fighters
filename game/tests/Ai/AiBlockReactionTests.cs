using System;
using System.Collections.Generic;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-252 (Y4, P6, E22): a block reaction becomes a guard. Its <see cref="ProfileAi.BlockTicks"/> (E22: 20) are counted
/// from the first tick the AI can guard (after its own move, dash or jump, or a stun it sees), and never more (E22). While
/// it runs with the opponent's projectile in flight, a recovery or whiff punish does not replace it; every other reaction
/// still rolls. A committed move is never interrupted (the sim has no cancel into guard, Match: canGuard).
/// The old rule (<c>heldBlock: false</c>, recordings made before YOK-252) is pinned alongside, and the proposed
/// <c>holdBlockThroughThreat</c> (off, not in E22) is checked on its own.
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
	static List<HitEvent> WaveAt(BehaviourProfile prof, int gap, bool heldBlock, int ticks = 150, bool hold = false)
	{
		var m = NewMatch();
		Place(m, gap);
		var hits = new List<HitEvent>();
		m.Hit += (_, e) => { if (e.Attacker == 0) hits.Add(e); };
		var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 7, heldBlock: heldBlock, holdBlockThroughThreat: hold);
		for (int t = 1; t <= ticks && m.Phase == MatchPhase.Fighting; t++)
			m.Step(t == 30 ? FighterInput.Special(SpecialSlot.A) : FighterInput.None, ai.Next(AiView.Capture(m)));
		return hits;
	}

	/// <summary>
	/// E22 (block plans 20): the held block lasts BlockTicks from its first actionable tick and no longer, so a Spirit Wave
	/// from the edge of mid range, still in flight after those 20 ticks, hits (as under the old rule). Only the proposed
	/// <c>holdBlockThroughThreat</c> (off; not in E22) keeps guarding until it arrives.
	/// </summary>
	[Test]
	public static void HeldBlock_LastsBlockTicks_OnlyTheProposedHoldOutlastsThem()
	{
		var prof = Profile("mid", 22, BlockOnAttack);
		var now = WaveAt(prof, 590, heldBlock: true);
		Assert.True(now.Count == 1 && !now[0].Blocked, $"E22: the guard ends after its 20 ticks, the late wave hits ({Describe(now)})");
		var old = WaveAt(prof, 590, heldBlock: false);
		Assert.True(old.Count == 1 && !old[0].Blocked, "old rule (pinned): the block plan ran out with the wave in flight, so it hit");
		var hold = WaveAt(prof, 590, heldBlock: true, hold: true);
		Assert.True(hold.Count == 1 && hold[0].Blocked, $"proposed hold: guarding until the wave arrives blocks it ({Describe(hold)})");
		Assert.True(!new ProfileAi(prof, 1, AiLoadout.From(NewMatch().P2), 1).HoldBlockThroughThreat, "the proposed hold is off by default");
	}

	/// <summary>E22 bound (code critic r1): with a threat showing for a long time (a Spirit Wave crossing the whole screen)
	/// the held block still guards exactly BlockTicks ticks; it never turtles past them. The proposed hold guards longer.</summary>
	[Test]
	public static void HeldBlock_GuardsExactlyBlockTicks_WhileAThreatShowsLonger()
	{
		var prof = Profile("full_screen", 4, BlockOnAttack);
		foreach (bool hold in new[] { false, true })
		{
			var m = NewMatch();
			Place(m, 1000);
			var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 3, holdBlockThroughThreat: hold);
			int guarded = 0, shotTicks = 0;
			for (int t = 1; t <= 200 && m.Phase == MatchPhase.Fighting; t++)
			{
				m.Step(t == 10 ? FighterInput.Special(SpecialSlot.A) : FighterInput.None, ai.Next(AiView.Capture(m)));
				if (m.P2.Guarding) guarded++;
				if (m.Projectiles.Count > 0) shotTicks++;
			}
			Assert.True(shotTicks > ProfileAi.BlockTicks + 10, $"setup: the wave shows longer than a block plan ({shotTicks})");
			if (hold) Assert.True(guarded > ProfileAi.BlockTicks, $"proposed hold: guards past BlockTicks while the wave shows ({guarded})");
			else Assert.Equal(ProfileAi.BlockTicks, guarded, "E22: exactly BlockTicks guard ticks, however long the threat shows");
		}
	}

	/// <summary>
	/// Code critic r1 (E22: reactions are data): a held block does not swallow other reactions. Spirit Wave on screen
	/// rolls the profile's <c>projectile_on_screen</c> answer as before and it replaces the guard (here a jump, 100%).
	/// </summary>
	[Test]
	public static void HeldBlock_ProjectileOnScreenReaction_StillRollsAndReplacesIt()
	{
		var prof = Profile("mid", 22, BlockOnAttack + """, { "when": "projectile_on_screen", "do": "jump", "weight": 100 }""");
		var m = NewMatch();
		Place(m, 590);
		var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 7);
		ai.Log = new();
		bool jumped = false;
		for (int t = 1; t <= 120 && m.Phase == MatchPhase.Fighting; t++)
		{
			m.Step(t == 30 ? FighterInput.Special(SpecialSlot.A) : FighterInput.None, ai.Next(AiView.Capture(m)));
			jumped |= m.P2.Airborne;
		}
		Assert.True(ai.Log.Exists(d => d.Kind == "react" && d.Trigger == "projectile_on_screen" && d.Action == "jump"), "the projectile reaction rolled and fired");
		Assert.True(jumped, "and replaced the guard: the AI jumped");
	}

	/// <summary>
	/// Root cause 2: a later event reaction replaces the running block plan. Spirit Wave's recovery starts while the wave
	/// is still in flight, so <c>opponent_in_recovery → attack</c> (oni-patient's heavy punch) turns the guard into a
	/// swing into the wave. By default every reaction rolls at its data weight (E22), so the punish still rolls during a
	/// held block. Only the proposed <c>holdBlockThroughThreat</c> (off) skips it while the shot shows; with it on, the
	/// wave is then blocked.
	/// </summary>
	[Test]
	public static void RecoveryPunish_RollsDuringAHeldBlock_SkippedOnlyByTheProposedOption()
	{
		var prof = Profile("mid", 22, BlockOnAttack + """, { "when": "opponent_in_recovery", "do": "normal:test-ryo-heavy-punch", "weight": 100 }""");
		List<AiDecision> Log(bool held, bool hold)
		{
			var m = NewMatch();
			Place(m, 450);
			var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 7, heldBlock: held, holdBlockThroughThreat: hold) { Log = new() };
			for (int t = 1; t <= 100 && m.Phase == MatchPhase.Fighting; t++)
				m.Step(t == 30 ? FighterInput.Special(SpecialSlot.A) : FighterInput.None, ai.Next(AiView.Capture(m)));
			return ai.Log!;
		}
		static bool Punished(List<AiDecision> log) => log.Exists(d => d.Trigger == "opponent_in_recovery");
		Assert.True(Log(true, false).Exists(d => d.Kind == "react" && d.Action == "block"), "setup: the block reaction fired");
		Assert.True(Punished(Log(true, false)), "default (E22): the recovery punish rolls at its data weight during the held block");
		Assert.True(Punished(Log(false, false)), "old rule (pinned): the recovery punish rolled and replaced the guard");
		Assert.True(!Punished(Log(true, true)), "proposed option: the recovery punish is skipped while the wave is on screen (no roll)");
		var hold = WaveAt(prof, 450, heldBlock: true, hold: true);
		Assert.True(hold.Count >= 1 && hold[0].Blocked, $"proposed option: the wave is blocked ({Describe(hold)})");
		var old = WaveAt(prof, 450, heldBlock: false);
		Assert.True(old.Count >= 1 && !old[0].Blocked, $"old rule (pinned): the punish swung into the wave ({Describe(old)})");
	}

	/// <summary>Code critic r2 (choice: BlockTicks are free ticks): a held block that blocks a hit keeps holding back through
	/// the whole blockstun (never open in blockstun), and its idle guard ticks before and after the stun add up to BlockTicks.</summary>
	[Test]
	public static void HeldBlock_HoldsThroughBlockstun_ThenBlockTicksFreeTicks()
	{
		var prof = Profile("close", 4, BlockOnAttack);
		var m = NewMatch();
		Place(m, m.Config.BodyWidth / SimConfig.Scale); // touching, close range is its band: it holds still, so only the block guards
		var hits = new List<HitEvent>();
		m.Hit += (_, e) => { if (e.Attacker == 0) hits.Add(e); };
		var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 3);
		int idleGuard = 0, stunTicks = 0, stunOpen = 0;
		for (int t = 1; t <= 200 && m.Phase == MatchPhase.Fighting; t++)
		{
			var o = ai.Next(AiView.Capture(m));
			bool back = m.P1.X < m.P2.X ? (o.Bits & InputBits.Right) != 0 : (o.Bits & InputBits.Left) != 0; // P2's back is away from P1
			if (m.P2.State == FighterState.Blockstun) { stunTicks++; if (!back) stunOpen++; }
			m.Step(t == 10 ? new FighterInput(InputBits.MediumKick) : FighterInput.None, o);
			if (m.P2.Guarding && m.P2.State == FighterState.Idle) idleGuard++;
		}
		Assert.True(hits.Count == 1 && hits[0].Blocked && stunTicks > 0, $"setup: the medium kick is blocked ({Describe(hits)})");
		Assert.Equal(0, stunOpen, $"it holds back through the whole blockstun ({stunTicks} stun ticks)");
		// The AI sees its own blockstun reaction frames late (P6), so up to that many of its counted ticks fall inside the real
		// stun's first ticks: BlockTicks free ticks on its own screen, BlockTicks - reaction (4) to BlockTicks in the sim.
		Assert.True(idleGuard >= ProfileAi.BlockTicks - 4 && idleGuard <= ProfileAi.BlockTicks,
			$"then BlockTicks free guard ticks in all, before and after the stun, as its delayed screen counts them ({idleGuard})");
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

	/// <summary>Code critic r1: the same for the AI's own jump (no count shows in the air; the AI knows its own jump): the
	/// guard ticks are not spent in the air, so it guards BlockTicks after landing.</summary>
	[Test]
	public static void BlockReaction_DuringOwnJump_GuardsFromItsLanding()
	{
		var prof = Profile("full_screen", 2, """{ "when": "at_full_screen", "do": "jump", "weight": 100 }, """ + BlockOnAttack);
		var m = NewMatch();
		Place(m, 900);
		var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 3);
		int start = -1, end = -1, guardAfter = 0, p1Press = -1;
		for (int t = 1; t <= 300 && m.Phase == MatchPhase.Fighting; t++)
		{
			var aiIn = ai.Next(AiView.Capture(m));
			var p1 = start > 0 && p1Press < 0 && t == start + 1 ? new FighterInput(InputBits.LightPunch) : FighterInput.None;
			if (p1.Bits != InputBits.None) p1Press = t;
			m.Step(p1, aiIn);
			if (start < 0 && m.P2.Airborne) start = t;
			else if (start > 0 && end < 0 && !m.P2.Airborne) end = t;
			if (end > 0 && t < end + ProfileAi.BlockTicks && m.P2.Guarding) guardAfter++;
			if (end > 0 && t >= end + ProfileAi.BlockTicks) break;
		}
		Assert.True(start > 0 && end - start > ProfileAi.BlockTicks, $"setup: the AI's jump ({end - start} ticks) outlasts a block plan");
		Assert.Equal(ProfileAi.BlockTicks, guardAfter, "held block: guards BlockTicks from its landing");
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
