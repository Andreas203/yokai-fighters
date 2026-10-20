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

/// <summary>
/// YOK-165 (Y4, P6, E22): (1) the AI's pre-emptive guard, <c>guard_pct</c> in a profile (default
/// <see cref="ProfileAi.DefaultGuardPct"/>): settled in its band to wait, or through its own move's recovery, it holds
/// crouch guard before any attack shows; (2) the node tier's <c>jump_punish_pct</c> in node-scenes.json (designer
/// 2026-10-20: apprentice and yokai 50, elder and Tanuki 100) sets how often its anti-air answers a jump. Both stay
/// replay-safe: absent = the behaviour from before YOK-165, left out of the file and the fingerprint.
/// Fight checks use the TEST FIXTURE kits (as AiTests).
/// </summary>
public static class AiGuardTests
{
	static string MapDir => ContentPaths.Data("map");
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static ReplayCatalog? _catalog;
	static ReplayCatalog Catalog => _catalog ??= ReplayCatalog.FromRepo(RepoRoot);
	static BehaviourProfile Fixture(string id) =>
		BehaviourProfile.LoadFile(ProjectSettings.GlobalizePath(FightScene.FixtureProfilesDir) + "/" + id + ".json");

	static Match NewMatch(Func<SimConfig, SimConfig>? shape = null)
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		try { return FightScene.NewMatch(null, null, shape); }
		finally { FightScene.Sources = saved; }
	}

	/// <summary>A profile with one event behaviour (so neutral is only spacing), the given range and extras.</summary>
	static string Json(string range, string behaviours, string extra = "") => $$"""
		{ "kind": "profile", "id": "test-guard", "yokai": "kitsune", "temperament": "patient",
		  "reaction": { "tier": "apprentice", "frames": 30 }, "aggression_bias": 50, "preferred_range": "{{range}}",
		  "habit": { "id": "jumps-after-knockdown", "description": "d", "when": "self_got_up", "do": "jump", "chance_pct": 60 },
		  "behaviours": [ {{behaviours}} ]{{extra}}, "rules": ["Y4"] }
		""";
	const string BlockOnAttack = """{ "when": "opponent_attacking", "do": "block", "weight": 1 }""";

	static void Place(Match m, int gapUnits)
	{
		m.P1.X = -(gapUnits / 2) * SimConfig.Scale;
		m.P2.X = (gapUnits - gapUnits / 2) * SimConfig.Scale;
	}

	/// <summary>
	/// The harness content with the opponent as it was before YOK-156 and YOK-165: no pre-emptive guard (every profile's
	/// guard_pct 0, so no draw), no tier jump-punish rate (the profiles' own weights) and full damage (no apprentice
	/// half). For tests that pin whole-run hashes of older batches (BotIdleGuardTests' pins predate YOK-156, which is why
	/// that test failed on main before this).
	/// </summary>
	public static HarnessContent LegacyOpponent(HarnessContent c) => c with
	{
		Profiles = f => c.Profiles(f).Select(p => p with { GuardPct = 0 }).ToArray(),
		Scenes = new LegacyScenes(c.Scenes),
	};

	sealed class LegacyScenes(INodeSceneResolver inner) : INodeSceneResolver
	{
		public NodeScene Resolve(MapNodeData node)
		{
			var s = inner.Resolve(node);
			return s with { Context = s.Context with { JumpPunishPct = null, DamageDealtPct = SimConfig.FullDamagePct } };
		}
	}

	[Test]
	public static void LegacyOpponent_PlaysTheOldHashes()
	{
		// The first run of the main batch committed before YOK-156 (BotIdleGuardTests pins it too), bot idle guard 0.
		var content = LegacyOpponent(HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir));
		var bot = BotConfig.LoadDefault(ContentPaths.DataDir);
		var batch = RunHarness.PlayBatch(content, bot with { Fight = bot.Fight with { IdleGuardPct = 0 } }, 1, 1);
		using var doc = System.Text.Json.JsonDocument.Parse(HarnessReport.ToJson(batch));
		Assert.Equal("c7b99e223b9a0450", doc.RootElement.GetProperty("runs")[0].GetProperty("session_hash").GetString(), "guard 0, no tier rate, full damage = the opponent from before YOK-156 and YOK-165");
	}

	// --- The profile field --------------------------------------------------------------------------

	[Test]
	public static void GuardPct_LoadsStrict_DefaultsAndOverrides()
	{
		var none = BehaviourProfile.Parse(Json("mid", BlockOnAttack));
		Assert.True(none.GuardPct is null, "left out = null in the profile");
		var kit = AiLoadout.From(NewMatch().P2);
		Assert.Equal(ProfileAi.DefaultGuardPct, new ProfileAi(none, 1, kit, 1).GuardPct, "left out = the default");
		Assert.Equal(50, ProfileAi.DefaultGuardPct, "the proposed default is 50");
		var seventy = BehaviourProfile.Parse(Json("mid", BlockOnAttack, ", \"guard_pct\": 70"));
		Assert.Equal((int?)70, seventy.GuardPct, "guard_pct 70 loads");
		Assert.Equal(70, new ProfileAi(seventy, 1, kit, 1).GuardPct, "the profile's value");
		Assert.Equal(0, new ProfileAi(seventy, 1, kit, 1, guardPct: 0).GuardPct, "the ctor argument wins (replays pass the recorded one)");
		Assert.Equal(0, BehaviourProfile.Parse(Json("mid", BlockOnAttack, ", \"guard_pct\": 0")).GuardPct ?? -1, "0 is allowed");
		foreach (var bad in new[] { "-1", "101", "50.5", "\"50\"", "null" })
		{
			bool threw = false;
			try { BehaviourProfile.Parse(Json("mid", BlockOnAttack, ", \"guard_pct\": " + bad)); }
			catch (FormatException) { threw = true; }
			Assert.True(threw, $"guard_pct {bad} is rejected");
		}
		Assert.Equal(Fingerprint.Of(none), Fingerprint.Of(none with { GuardPct = null }), "no guard_pct: the fingerprint is the one from before YOK-165");
		Assert.True(Fingerprint.Of(none) != Fingerprint.Of(none with { GuardPct = 50 }), "a guard_pct changes the profile fingerprint");
	}

	// --- The guard in the fight ---------------------------------------------------------------------

	/// <summary>Settled at its preferred range with nothing to react to, a guard-100 AI holds down-back (crouch guard);
	/// a guard-0 AI stands open, exactly as before YOK-165.</summary>
	[Test]
	public static void Waiting_InItsBand_HoldsCrouchGuard()
	{
		var prof = BehaviourProfile.Parse(Json("close", BlockOnAttack));
		foreach (int pct in new[] { 100, 0 })
		{
			var m = NewMatch();
			Place(m, 150); // close range, both idle
			var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 3, guardPct: pct);
			int guarded = 0, open = 0;
			for (int t = 0; t < 120; t++)
			{
				var o = ai.Next(AiView.Capture(m));
				m.Step(FighterInput.None, o);
				if (m.P2.Guarding && m.P2.Crouching) guarded++; else open++;
			}
			if (pct == 100) Assert.True(guarded >= 119, $"guard 100: crouch guard while waiting ({guarded} of 120, {open} open)");
			else Assert.Equal(0, guarded, "guard 0: no pre-emptive guard");
		}
	}

	/// <summary>The point of the guard (YOK-154 F1): a 30-frame AI cannot see a fast normal in time, so only a guard held
	/// before the swing blocks it. Ryo's medium kick (fixture: startup under 15) from touching range: blocked at guard 100, hits at guard 0.</summary>
	[Test]
	public static void PreEmptiveGuard_BlocksAStrikeFasterThanReaction()
	{
		var prof = BehaviourProfile.Parse(Json("close", BlockOnAttack));
		foreach (int pct in new[] { 100, 0 })
		{
			var m = NewMatch();
			Place(m, m.Config.BodyWidth / SimConfig.Scale); // touching
			var hits = new List<HitEvent>();
			m.Hit += (_, e) => { if (e.Attacker == 0) hits.Add(e); };
			var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 3, guardPct: pct);
			for (int t = 1; t <= 90; t++)
				m.Step(t == 60 ? new FighterInput(InputBits.MediumKick) : FighterInput.None, ai.Next(AiView.Capture(m)));
			Assert.True(hits.Count == 1, $"guard {pct}: Ryo's medium kick connects once ({hits.Count})");
			Assert.Equal(pct == 100, hits[0].Blocked, $"guard {pct}: blocked = {pct == 100}");
		}
	}

	/// <summary>After its own move it holds crouch guard through the recovery: guarding on its first free frame.</summary>
	[Test]
	public static void OwnMove_RecoveryEndsInGuard_ButNotTheHabit()
	{
		var prof = BehaviourProfile.Parse(Json("close", """{ "when": "at_close_range", "do": "normal:test-ryo-light-punch", "weight": 100 }"""));
		foreach (int pct in new[] { 100, 0 })
		{
			var m = NewMatch();
			Place(m, 200); // close range: it keeps swinging (mostly whiffs at this gap)
			var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 9, guardPct: pct) { Log = new() };
			int firstFree = 0, freeGuarded = 0;
			bool wasAttacking = false;
			for (int t = 0; t < 240; t++)
			{
				m.Step(FighterInput.None, ai.Next(AiView.Capture(m)));
				bool attacking = m.P2.State == FighterState.Attack;
				if (wasAttacking && !attacking) { firstFree++; if (m.P2.Guarding && m.P2.Crouching) freeGuarded++; }
				wasAttacking = attacking;
			}
			Assert.True(firstFree > 0, $"guard {pct}: the AI attacked and recovered ({firstFree})");
			if (pct == 100)
			{
				Assert.Equal(firstFree, freeGuarded, "guard 100: every recovery ends in crouch guard");
				Assert.True(ai.Log!.Any(d => d.Kind == "guard" && d.Trigger == "self_attacked"), "the guard is logged");
			}
			else Assert.Equal(0, freeGuarded, "guard 0: it stands open after its move");
		}
		// The habit plan never gets a guard tail (Y2: it stays as punishable as written). Habit: anti-air every jump.
		var habit = BehaviourProfile.Parse(Json("mid", BlockOnAttack).Replace(
			"\"when\": \"self_got_up\", \"do\": \"jump\", \"chance_pct\": 60", "\"when\": \"opponent_jumping\", \"do\": \"normal:test-ryo-light-punch\", \"chance_pct\": 100"));
		var hm = NewMatch();
		Place(hm, 400);
		var hai = new ProfileAi(habit, 1, AiLoadout.From(hm.P2), 9, guardPct: 100) { Log = new() };
		for (int t = 1; t <= 900 && hm.Phase != MatchPhase.Over; t++)
			hm.Step(t % 90 == 0 ? new FighterInput(InputBits.Up) : FighterInput.None, hai.Next(AiView.Capture(hm)));
		Assert.True(hai.Log!.Count(d => d.Kind == "habit") >= 3, "the habit fired");
		Assert.True(!hai.Log!.Any(d => d.Kind == "guard" && d.Trigger == "self_attacked"), "no guard tail after the habit's move");
	}

	/// <summary>P6: the guard is rolled on the AI's own decisions, so two P1 streams identical until t give identical AI
	/// output until t + reaction frames, with the guard on.</summary>
	[Test]
	public static void Guard_NeverReadsAPress()
	{
		var prof = BehaviourProfile.Parse(Json("close", BlockOnAttack + """, { "when": "at_close_range", "do": "normal:test-ryo-light-punch", "weight": 30 }"""));
		foreach (int t in new[] { 40, 100 })
		{
			List<FighterInput> Play(bool press)
			{
				var m = NewMatch();
				Place(m, 200);
				var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 21, guardPct: 100);
				var outs = new List<FighterInput>();
				for (int k = 1; k <= 200; k++)
				{
					var o = ai.Next(AiView.Capture(m));
					outs.Add(o);
					m.Step(press && k >= t ? new FighterInput(InputBits.LightPunch | InputBits.Left) : FighterInput.None, o);
				}
				return outs;
			}
			var a = Play(false); var b = Play(true);
			for (int k = 0; k < t + 30 - 1; k++) Assert.True(a[k] == b[k], $"t {t}: identical AI output at step {k + 1} (before the press could show)");
		}
	}

	// --- Jump punish by tier ------------------------------------------------------------------------

	[Test]
	public static void Data_JumpPunishPctPerTier_StrictLoader()
	{
		var table = NodeSceneTable.Default(MapDir);
		var map = FileMapSource.Default(MapDir).Load();
		var byTier = new Dictionary<string, int?>();
		foreach (var n in map.Nodes)
		{
			var ctx = table.Resolve(n).Context;
			if (n.Type is "merchant" or "dojo") continue;
			byTier[ctx.Tier] = ctx.JumpPunishPct;
		}
		Assert.Equal((int?)50, byTier.GetValueOrDefault("apprentice"), "apprentice 50");
		Assert.Equal((int?)50, byTier.GetValueOrDefault("yokai"), "yokai 50");
		Assert.Equal((int?)100, byTier.GetValueOrDefault("elder"), "elder 100");
		Assert.Equal((int?)100, byTier.GetValueOrDefault("tanuki"), "tanuki 100");

		string json = File.ReadAllText(Path.Combine(MapDir, NodeSceneTable.DefaultFile));
		const string good = "\"yokai\": { \"reaction_frames\": 22, \"jump_punish_pct\": 50 }";
		Assert.True(json.Contains(good), "the yokai tier names jump_punish_pct 50");
		foreach (var bad in new[] { "-1", "101", "50.5", "\"50\"", "null" })
		{
			bool threw = false;
			try { NodeSceneTable.Parse(json.Replace(good, "\"yokai\": { \"reaction_frames\": 22, \"jump_punish_pct\": " + bad + " }")); }
			catch (FormatException) { threw = true; }
			Assert.True(threw, $"jump_punish_pct {bad} is rejected");
		}
		var absent = NodeSceneTable.Parse(json.Replace(good, "\"yokai\": { \"reaction_frames\": 22 }"));
		var duel = map.Nodes.First(n => table.Resolve(n).Context.Tier == "yokai" && n.Type == "duel");
		Assert.True(absent.Resolve(duel).Context.JumpPunishPct is null, "left out = null (the profile's own weights)");
		Assert.Equal((int?)0, NodeSceneTable.Parse(json.Replace(good, "\"yokai\": { \"reaction_frames\": 22, \"jump_punish_pct\": 0 }")).Resolve(duel).Context.JumpPunishPct, "0 is allowed");
	}

	static FightRecord StubWin(RunSession s) => new()
	{
		Profile = "stub", Won = true, Ticks = 600, DamageTaken = 10, HealthLeft = Math.Max(1, s.Run.Health - 10), Bot = new BotStats(),
	};

	/// <summary>NodeFight hands the tier's rate to P2's AI through the match config (game and harness alike); P1 never gets one.</summary>
	[Test]
	public static void NodeFight_SetsP2JumpPunishPctByTier()
	{
		var content = HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
		var bot = BotConfig.LoadDefault(ContentPaths.DataDir);
		var seen = new Dictionary<string, int?>();
		FightRunner probe = (c, b, s, _) =>
		{
			var m = NodeFight.NewMatch(c.Sources, s.Run, s, s.Node!.Fighter, b.Scheme);
			Assert.True(m.Config.P1JumpPunishPct is null, "P1 never gets one");
			seen[s.Node.Tier] = m.Config.P2JumpPunishPct;
			Assert.Equal(m.Config.P2JumpPunishPct, AiView.Capture(m).JumpPunishPct(1), "the AI's view carries it");
			return StubWin(s);
		};
		RunHarness.PlayRun(content, bot, 3, probe);
		Assert.Equal((int?)50, seen.GetValueOrDefault("apprentice"), "apprentice");
		Assert.Equal((int?)50, seen.GetValueOrDefault("yokai"), "yokai");
		Assert.Equal((int?)100, seen.GetValueOrDefault("elder"), "elder");
		Assert.Equal((int?)100, seen.GetValueOrDefault("tanuki"), "tanuki");
	}

	/// <summary>P1 jumps every 90 ticks against an AI whose profile anti-airs at 100 and blocks jumps at 100. The tier
	/// rate replaces the anti-air's weight only: 0 never anti-airs, 100 answers every jump, 50 some; the block keeps its own.</summary>
	[Test]
	public static void JumpPunishPct_SetsTheAntiAirRate_NotTheBlock()
	{
		var anti = BehaviourProfile.Parse(Json("mid", """{ "when": "opponent_jumping", "do": "normal:test-ryo-heavy-punch", "weight": 100 }"""));
		var block = BehaviourProfile.Parse(Json("mid", """{ "when": "opponent_jumping", "do": "block", "weight": 100 }"""));
		(int jumps, int answers) Run(BehaviourProfile p, int? pct)
		{
			var m = NewMatch(c => c with { P2JumpPunishPct = pct });
			Place(m, 400);
			var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), 11, 15, guardPct: 0) { Log = new() };
			int jumps = 0; bool air = false;
			for (int t = 1; t <= 3600 && m.Phase != MatchPhase.Over; t++)
			{
				m.Step(t % 90 == 0 ? new FighterInput(InputBits.Up) : FighterInput.None, ai.Next(AiView.Capture(m)));
				if (m.P1.Airborne && !air) jumps++;
				air = m.P1.Airborne;
			}
			return (jumps, ai.Log!.Count(d => d.Trigger == "opponent_jumping" && d.Kind is "react" or "unavailable"));
		}
		var (j0, a0) = Run(anti, 0);
		var (j100, a100) = Run(anti, 100);
		var (j50, a50) = Run(anti, 50);
		var (jn, an) = Run(anti, null);
		Assert.True(j0 >= 30, $"P1 jumped ({j0})");
		Assert.Equal(0, a0, "rate 0: never anti-airs");
		Assert.True(a100 >= j100 - 2, $"rate 100: answers every jump seen ({a100} of {j100})");
		Assert.True(a50 > j50 / 5 && a50 < j50 * 4 / 5, $"rate 50: some ({a50} of {j50})");
		Assert.True(an >= jn - 2, $"no tier rate: the profile's weight 100 ({an} of {jn})");
		var (jb, ab) = Run(block, 0);
		Assert.True(ab >= jb - 2, $"a block answer keeps its weight 100 at tier rate 0 ({ab} of {jb})");
	}

	// --- Replays ------------------------------------------------------------------------------------

	static ReplayRecording Play(Match m, ProfileAi ai, uint seed, int steps)
	{
		var rng = new Random((int)seed);
		var rec = ReplayRecorder.Begin(m, ai, seed, null);
		for (int t = 0; t < steps && m.Phase != MatchPhase.Over; t++)
		{
			int d = rng.Next(6);
			var bits = d == 0 ? InputBits.Right : d == 1 ? InputBits.Left : d == 2 ? InputBits.Up : InputBits.None;
			if (rng.Next(4) == 0) bits |= InputBits.LightPunch;
			var a = new FighterInput(bits);
			var b = ai.Next(AiView.Capture(m));
			m.Step(a, b);
			rec.Record(a, b, m.StateHash());
		}
		return rec.Finish(m);
	}

	[Test]
	public static void GuardAndJumpPunish_ReplayIdentically_OldRecordingsStillRun()
	{
		var prof = Fixture("test-kitsune-aggressive");
		Assert.True(prof.GuardPct is null, "the fixture names no guard_pct: the default applies");
		var m = NewMatch(c => c with { P2JumpPunishPct = 50 });
		var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 165);
		Assert.Equal(ProfileAi.DefaultGuardPct, ai.GuardPct, "default guard");
		var rec = Play(m, ai, 165, 3000);
		Assert.Equal((int?)50, rec.Header.P2JumpPunishPct, "the recording keeps the jump-punish pct");
		Assert.Equal(ProfileAi.DefaultGuardPct, rec.Header.Ai!.GuardPct, "and the guard the AI played with");
		var back = ReplayRecording.FromBytes(rec.ToBytes());
		var r = Replayer.Run(back, Catalog);
		Assert.True(r.Ok, $"replays: {r}");
		Assert.Equal(rec.Steps, r.StepsRun, "every step");
		Assert.True(rec.Steps > 300, $"a real fight ({rec.Steps} steps)");

		var noGuard = ReplayRecording.FromBytes(rec.ToBytes());
		noGuard.Header.Ai!.GuardPct = 0;
		Assert.True(!Replayer.Run(noGuard, Catalog).Ok, "the same recording without its guard does not match the AI");
		var noPct = ReplayRecording.FromBytes(rec.ToBytes());
		noPct.Header.P2JumpPunishPct = null;
		Assert.True(!Replayer.Run(noPct, Catalog).Ok, "nor without its jump-punish pct (config fingerprint)");

		// A recording as made before YOK-165: no guard, no tier rate. Neither is written, and it replays.
		var oldM = NewMatch();
		var old = Play(oldM, new ProfileAi(prof, 1, AiLoadout.From(oldM.P2), 7, guardPct: 0), 7, 1500);
		string header = System.Text.Json.JsonSerializer.Serialize(old.Header);
		Assert.True(!header.Contains("GuardPct") && !header.Contains("JumpPunishPct"), $"neither field is written: {header}");
		Assert.True(Replayer.Run(ReplayRecording.FromBytes(old.ToBytes()), Catalog).Ok, "an old-style recording replays (absent guard = 0)");
		Assert.Equal(Fingerprint.Of(SimConfig.Default), Fingerprint.Of(SimConfig.Default with { P2JumpPunishPct = null }), "no pct fingerprints as before YOK-165");
		Assert.True(Fingerprint.Of(SimConfig.Default) != Fingerprint.Of(SimConfig.Default with { P2JumpPunishPct = 50 }), "a pct changes the fingerprint");
	}
}
