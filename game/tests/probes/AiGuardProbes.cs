using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Harness;
using YokaiFighters.Replay;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-165 (Y2, Y4, P6, C3, E22). Expected values come from rules.md and the designer decisions of 2026-10-20.</summary>
public static class AiGuardProbes
{
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static ReplayCatalog? _catalog;
	static ReplayCatalog Catalog => _catalog ??= ReplayCatalog.FromRepo(RepoRoot);

	static Match NewMatch(Func<SimConfig, SimConfig>? shape = null)
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		try { return FightScene.NewMatch(null, null, shape); }
		finally { FightScene.Sources = saved; }
	}

	static string Json(string range, string behaviours, string extra = "") => $$"""
		{ "kind": "profile", "id": "probe-guard", "yokai": "kitsune", "temperament": "patient",
		  "reaction": { "tier": "apprentice", "frames": 30 }, "aggression_bias": 50, "preferred_range": "{{range}}",
		  "habit": { "id": "jumps-after-knockdown", "description": "d", "when": "self_got_up", "do": "jump", "chance_pct": 60 },
		  "behaviours": [ {{behaviours}} ]{{extra}}, "rules": ["Y4"] }
		""";
	static BehaviourProfile P(string range, string behaviours, string extra = "") => BehaviourProfile.Parse(Json(range, behaviours, extra));

	static void Place(Match m, int gap)
	{
		m.P1.X = -(gap / 2) * SimConfig.Scale;
		m.P2.X = (gap - gap / 2) * SimConfig.Scale;
	}

	const string Heavy = "normal:test-ryo-heavy-punch", Medium = "normal:test-ryo-medium-punch";

	// Y4 (designer 2026-10-20): rate 100 = every jump the AI SEES is answered with an attack. The AI swings often,
	// P1 jumps every 61 ticks. Measures how many decided punishes start the move within 2 ticks: a press the sim drops
	// while P2 is still in its own move is no answer (measured, not asserted: the rule text is silent).
	[Test]
	public static void Rate100_JumpSeenDuringOwnRecovery_Measured()
	{
		var p = P("close", $$"""{ "when": "at_close_range", "do": "{{Heavy}}", "weight": 15 }, { "when": "opponent_jumping", "do": "{{Medium}}", "weight": 100 }""");
		var m = NewMatch(c => c with { P2JumpPunishPct = 100, P1MaxHealth = 1_000_000, P2MaxHealth = 1_000_000 });
		Place(m, 200);
		var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), 5, 15, guardPct: 0) { Log = new() };
		var startedMedium = new List<int>();
		var stateAt = new Dictionary<int, FighterState>();
		string? last = null;
		for (int t = 1; t <= 3000 && m.Phase != MatchPhase.Over; t++)
		{
			var o = ai.Next(AiView.Capture(m));
			m.P1.Health = m.P1.MaxHealth;
			stateAt[t] = m.P2.State;
			m.Step(t % 61 == 0 ? new FighterInput(InputBits.Up) : FighterInput.None, o);
			string? id = m.P2.State == FighterState.Attack ? m.P2.CurrentMove?.Id : null;
			if (id != null && id != last && id.Contains("medium")) startedMedium.Add(t);
			last = id;
		}
		var answers = ai.Log!.Where(d => d.Trigger == "opponent_jumping" && d.Kind == "react").ToList();
		Assert.True(answers.Count >= 30, $"the AI decided to punish many jumps ({answers.Count})");
		GD.Print("PROBE answers " + answers.Count + " started " + startedMedium.Count + " dropped " + string.Join(",", answers.Where(a => !startedMedium.Any(s => s >= a.Tick && s <= a.Tick + 2)).Take(8).Select(a => a.Tick + "/" + stateAt[a.Tick])));
		// rules.md Y4 names no jump_punish_pct, so this is not asserted as a rule: see the report (designer attention).
		int lost = answers.Count(a => !startedMedium.Any(s => s >= a.Tick && s <= a.Tick + 2));
		GD.Print($"PROBE rate 100: {lost} of {answers.Count} decided punishes never started (P2 was in its own move)");
	}

	// Determinism with an EX-capable special as the anti-air: Build draws the EX roll after the pick.
	[Test]
	public static void ExSpecialAntiAir_DrawOrderIsFixed()
	{
		string? exId = null;
		foreach (var eq in NewMatch().P2.Specials) if (eq is not null && eq.Data.HasEx) { exId = eq.Data.Id; break; }
		Assert.True(exId is not null, "the opponent kit has a special with an EX version");
		string sp = "special:" + exId;
		var p = P("mid", "{ \"when\": \"opponent_jumping\", \"do\": \"" + sp + "\", \"weight\": 20 }, { \"when\": \"opponent_jumping\", \"do\": \"" + Heavy + "\", \"weight\": 20 }",
			", \"ex_use\": { \"chance_pct\": 50, \"keep_meter\": 0 }");
		List<string> Run(uint seed)
		{
			var m = NewMatch(c => c with { P2JumpPunishPct = 100, P1MaxHealth = 1_000_000 });
			Place(m, 400);
			var kit = AiLoadout.From(m.P2);
			Assert.True(kit.CanDo(BehaviourProfile.ParseAction(sp)), "the kit performs the special");
			var ai = new ProfileAi(p, 1, kit, seed, 15, AiFightTier.Elder, guardPct: 0) { Log = new() };
			for (int t = 1; t <= 1800 && m.Phase != MatchPhase.Over; t++)
			{
				m.P2.Meter = 300;
				m.Step(t % 90 == 0 ? new FighterInput(InputBits.Up) : FighterInput.None, ai.Next(AiView.Capture(m)));
			}
			return ai.Log!.Where(d => d.Trigger == "opponent_jumping" || d.Kind.StartsWith("ex")).Select(d => $"{d.Tick}:{d.Kind}:{d.Action}:{d.Roll}/{d.Pct}").ToList();
		}
		var a = Run(3); var b = Run(3);
		Assert.True(a.Count >= 20, $"decisions logged ({a.Count})");
		Assert.True(a.SequenceEqual(b), "same seed, same decisions");
		Assert.True(a.Any(x => x.Contains(":ex:")) && a.Any(x => x.Contains(":ex_no:")), "the EX roll happens for the special, both outcomes seen");
		Assert.True(!Run(4).SequenceEqual(a), "a different seed gives different rolls");
	}

	// C3 + P6: opt-in 100. A cross-up seen during the self_attacked tail is followed (back re-read each tick).
	[Test]
	public static void GuardTail_FollowsCrossUp_AfterTheAIsOwnMove()
	{
		var p = P("close", $$"""{ "when": "at_close_range", "do": "{{Heavy}}", "weight": 100 }""");
		var m = NewMatch(c => c with { P1MaxHealth = 1_000_000 });
		Place(m, 200);
		const int reaction = 2;
		var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), 9, reaction, guardPct: 100);
		int pressTick = -1, crossTick = -1, wrongWay = 0, checkedTicks = 0;
		for (int t = 1; t <= 400; t++)
		{
			var o = ai.Next(AiView.Capture(m));
			bool attackPress = o.Move != 0;
			if (attackPress && pressTick < 0) pressTick = t;
			bool cross = pressTick > 0 && crossTick < 0 && t == pressTick + 3;
			if (cross) crossTick = t;
			if (crossTick > 0 && t > crossTick + reaction + 2 && t < crossTick + 14 && (o.Bits & InputBits.Down) != 0 && !attackPress)
			{
				checkedTicks++;
				bool p1Left = m.P1.X < m.P2.X; // back is Right then
				bool backIsRight = (o.Bits & InputBits.Right) != 0 && (o.Bits & InputBits.Left) == 0;
				bool backIsLeft = (o.Bits & InputBits.Left) != 0 && (o.Bits & InputBits.Right) == 0;
				if (p1Left ? !backIsRight : !backIsLeft) wrongWay++;
			}
			m.Step(cross ? new FighterInput(InputBits.DebugCrossUp) : FighterInput.None, o);
		}
		Assert.True(pressTick > 0, "the AI attacked");
		Assert.True(checkedTicks > 0, $"a guard tail was running after the cross-up ({checkedTicks} ticks checked)");
		Assert.Equal(0, wrongWay, "C3: the tail guard holds away from the opponent after the cross-up shows");
	}

	// Y2: the guard must not change the AI's own attack: the first P2 hit lands on the same tick with guard 100 as with guard 0.
	[Test]
	public static void Guard100_DoesNotSwallowOrDelayTheAIsOwnAttack()
	{
		var p = P("close", $$"""{ "when": "at_close_range", "do": "{{Heavy}}", "weight": 100 }""");
		int FirstHit(int guard)
		{
			var m = NewMatch(c => c with { P1MaxHealth = 1_000_000 });
			Place(m, m.Config.BodyWidth / SimConfig.Scale);
			int hit = -1;
			m.Hit += (_, e) => { if (e.Attacker == 1 && hit < 0) hit = m.Tick; };
			var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), 9, 15, guardPct: guard);
			for (int t = 1; t <= 300 && hit < 0; t++) m.Step(FighterInput.None, ai.Next(AiView.Capture(m)));
			return hit;
		}
		int off = FirstHit(0), on = FirstHit(100);
		Assert.True(off > 0, "the AI's swing connects with the guard off");
		Assert.Equal(off, on, "guard 100: the first swing lands on the same tick");
	}

	// Replay: a guard 100 + tier 100 recording round-trips; a recorded guard outside 0-100 is refused.
	[Test]
	public static void Replay_Guard100Tier100_RoundTrips_BadGuardRefused()
	{
		var prof = BehaviourProfile.LoadFile(ProjectSettings.GlobalizePath(FightScene.FixtureProfilesDir) + "/test-kitsune-patient.json");
		var m = NewMatch(c => c with { P2JumpPunishPct = 100 });
		var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 77, guardPct: 100);
		var rec = ReplayRecorder.Begin(m, ai, 77, null);
		var rng = new Random(77);
		for (int t = 0; t < 2500 && m.Phase != MatchPhase.Over; t++)
		{
			int d = rng.Next(6);
			var a = new FighterInput(d == 0 ? InputBits.Right : d == 1 ? InputBits.Left : d == 2 ? InputBits.Up : InputBits.None);
			var b = ai.Next(AiView.Capture(m));
			m.Step(a, b);
			rec.Record(a, b, m.StateHash());
		}
		var done = rec.Finish(m);
		Assert.True(Replayer.Run(ReplayRecording.FromBytes(done.ToBytes()), Catalog).Ok, "round trip");
		foreach (int bad in new[] { -1, 101 })
		{
			var r = ReplayRecording.FromBytes(done.ToBytes());
			r.Header.Ai!.GuardPct = bad;
			bool refused = false;
			try { refused = !Replayer.Run(r, Catalog).Ok; } catch (ReplaySetupException) { refused = true; }
			Assert.True(refused, $"guard {bad} in a recording is refused");
		}
	}

	// Y4 whole-run: in the real node fights of a harness run, the AI's jump-punish decisions are logged at the tier rate.
	[Test]
	public static void WholeRun_TierRateReachesTheAi_PerTier()
	{
		var content = HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
		var bot = BotConfig.LoadDefault(ContentPaths.DataDir);
		var seenPct = new Dictionary<string, HashSet<int>>();
		var expected = new Dictionary<string, int> { ["apprentice"] = 50, ["yokai"] = 50, ["elder"] = 100, ["tanuki"] = 100 };
		FightRunner runner = (c, b, s, runSeed) =>
		{
			var node = s.Node!;
			var m = NodeFight.NewMatch(c.Sources, s.Run, s, node.Fighter, b.Scheme);
			var profiles = c.Profiles(node.Fighter);
			var prof = node.Profile is { } want ? profiles.FirstOrDefault(x => x.Id == want) ?? profiles[0] : profiles[0];
			var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 1234, node.ReactionFrames, AiFightTiers.Of(node)) { Log = new() };
			for (int t = 1; t <= 2700 && m.Phase != MatchPhase.Over; t++)
			{
				m.P1.Health = m.P1.MaxHealth;
				m.Step(t % 90 == 0 ? new FighterInput(InputBits.Up) : FighterInput.None, ai.Next(AiView.Capture(m)));
			}
			var set = seenPct.TryGetValue(node.Tier, out var h) ? h : seenPct[node.Tier] = new();
			foreach (var d in ai.Log!.Where(d => d.Trigger == "opponent_jumping" && d.Kind == "react" && d.Action != "block"))
				set.Add(d.Pct);
			return new FightRecord { Profile = prof.Id, Won = true, Ticks = 600, DamageTaken = 10, HealthLeft = Math.Max(1, s.Run.Health - 10), Bot = new BotStats() };
		};
		RunHarness.PlayRun(content, bot, 3, runner);
		int verified = 0;
		foreach (var (tier, pct) in expected)
		{
			if (!seenPct.TryGetValue(tier, out var set) || set.Count == 0) continue; // that profile has no attacking jump answer
			verified++;
			Assert.True(set.All(x => x == pct), $"tier {tier}: anti-air draws are at {pct}, saw {string.Join(",", set)}");
		}
		Assert.True(verified >= 1, "at least one tier's AI made a measurable anti-air draw");
	}

	// Determinism: same seed, rate and guard twice give the same StateHash; changing either changes the fight.
	[Test]
	public static void Determinism_GuardAndRate_SameTwice_DifferentWhenChanged()
	{
		var p = P("close", $$"""{ "when": "at_close_range", "do": "{{Heavy}}", "weight": 40 }, { "when": "opponent_jumping", "do": "block", "weight": 60 }, { "when": "opponent_jumping", "do": "{{Medium}}", "weight": 10 }""");
		ulong Hash(int guard, int? rate)
		{
			var m = NewMatch(c => c with { P2JumpPunishPct = rate, P1MaxHealth = 1_000_000 });
			Place(m, 300);
			var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), 31, 15, guardPct: guard);
			for (int t = 1; t <= 1500 && m.Phase != MatchPhase.Over; t++)
				m.Step(t % 61 == 0 ? new FighterInput(InputBits.Up) : FighterInput.None, ai.Next(AiView.Capture(m)));
			return m.StateHash();
		}
		Assert.Equal(Hash(100, 100), Hash(100, 100), "same inputs, same hash");
		Assert.True(Hash(100, 100) != Hash(0, 100), "the guard changes the fight");
		Assert.True(Hash(0, 100) != Hash(0, 0), "the rate changes the fight");
	}
}
