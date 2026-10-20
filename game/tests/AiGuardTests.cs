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
	/// The harness content with the opponent as it was before YOK-156 and YOK-165: no tier jump-punish rate (the
	/// profiles' own weights) and full damage (no apprentice half). Profiles stay as loaded (GuardPct untouched, so their
	/// fingerprints too): the guard is off by default and the legacy test below checks no shipped profile opts in. For
	/// tests that pin whole-run hashes of older batches (BotIdleGuardTests' pins predate YOK-156, which is why that test
	/// failed on main before this).
	/// </summary>
	public static HarnessContent LegacyOpponent(HarnessContent c) => c with
	{
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
		foreach (var p in BehaviourProfile.LoadDirectory(ContentPaths.Data("profiles")))
			Assert.True(p.GuardPct is null or 0, $"{p.Id}: no shipped profile opts in to the guard (else LegacyOpponent is not legacy)");
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
		Assert.Equal(0, ProfileAi.DefaultGuardPct, "designer 2026-10-20: off by default, profiles opt in");
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

	/// <summary>Code-critic advisory 4: a guard step re-reads "back" every tick from the AI's own view, so once a
	/// cross-up shows (reaction frames later) it keeps guarding the new side instead of holding down-forward.</summary>
	[Test]
	public static void Guard_FollowsACrossUp_OnceItShows()
	{
		var prof = BehaviourProfile.Parse(Json("close", BlockOnAttack));
		var m = NewMatch();
		Place(m, 150);
		const int reaction = 15, cross = 61;
		var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 3, reaction, guardPct: 100);
		int open = 0, side = Math.Sign(m.P1.X - m.P2.X);
		for (int t = 1; t <= cross + reaction + 40; t++)
		{
			m.Step(t == cross ? new FighterInput(InputBits.DebugCrossUp) : FighterInput.None, ai.Next(AiView.Capture(m)));
			if (t == cross) Assert.True(Math.Sign(m.P1.X - m.P2.X) == -side, "P1 crossed to the other side");
			if (t > cross + reaction + 2 && !(m.P2.Guarding && m.P2.Crouching)) open++;
		}
		Assert.Equal(0, open, "guarding the new side from the tick the cross-up shows (+2 for the view and the step)");
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

	/// <summary>P1 jumps every 90 ticks; counts the AI's attacking and blocking answers to those jumps.</summary>
	static (int Jumps, int Attacks, int Blocks, int Medium) Jumps(BehaviourProfile p, int? pct, uint seed = 11)
	{
		var m = NewMatch(c => c with { P2JumpPunishPct = pct, P1MaxHealth = 1_000_000 });
		Place(m, 400);
		var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), seed, 15, guardPct: 0) { Log = new() };
		int jumps = 0; bool air = false;
		for (int t = 1; t <= 9000 && m.Phase != MatchPhase.Over; t++)
		{
			m.Step(t % 90 == 0 ? new FighterInput(InputBits.Up) : FighterInput.None, ai.Next(AiView.Capture(m)));
			if (m.P1.Airborne && !air) jumps++;
			air = m.P1.Airborne;
		}
		var answers = ai.Log!.Where(d => d.Trigger == "opponent_jumping" && d.Kind == "react").ToList();
		return (jumps, answers.Count(d => d.Action.StartsWith("normal:")), answers.Count(d => d.Action == "block"), answers.Count(d => d.Action == "normal:test-ryo-medium-punch"));
	}

	/// <summary>A patient-style profile: the block listed first (60), two attacking answers (30 and 10).</summary>
	static BehaviourProfile Mixed => BehaviourProfile.Parse(Json("mid",
		"""{ "when": "opponent_jumping", "do": "block", "weight": 60 }, """ +
		"""{ "when": "opponent_jumping", "do": "normal:test-ryo-heavy-punch", "weight": 30 }, """ +
		"""{ "when": "opponent_jumping", "do": "normal:test-ryo-medium-punch", "weight": 10 }"""));

	/// <summary>The tier rate is the absolute chance a jump is answered with an attack (designer 2026-10-20): an elder
	/// (100) with a block-weighted profile punishes every jump; the block only rolls after a miss.</summary>
	[Test]
	public static void JumpPunish_Elder100_PunishesEveryJump_DespiteTheBlock()
	{
		var (jumps, attacks, blocks, medium) = Jumps(Mixed, 100);
		Assert.True(jumps >= 80, $"P1 jumped ({jumps})");
		Assert.True(attacks >= jumps - 2, $"elder 100: every jump seen is answered with an attack ({attacks} of {jumps})");
		Assert.Equal(0, blocks, "elder 100: the block never comes first");
		Assert.True(medium > 0 && medium * 100 < attacks * 45, $"one roll, then a weighted pick 30:10 ({medium} medium punches of {attacks})");
	}

	[Test]
	public static void JumpPunish_Apprentice50_AboutHalf_MissesGoToTheBlock()
	{
		int jumps = 0, attacks = 0, blocks = 0;
		foreach (uint seed in new uint[] { 11, 12, 13 })
		{
			var r = Jumps(Mixed, 50, seed);
			jumps += r.Jumps; attacks += r.Attacks; blocks += r.Blocks;
		}
		Assert.True(jumps >= 240, $"P1 jumped ({jumps})");
		Assert.True(attacks * 100 >= jumps * 40 && attacks * 100 <= jumps * 60, $"apprentice 50: about half the jumps punished ({attacks} of {jumps})");
		Assert.True(blocks > 0 && attacks + blocks <= jumps + 6, $"a miss falls to the block at its own weight ({blocks} blocks)");
	}

	/// <summary>No tier rate: the profile's weights decide, in list order, as before YOK-165 (block 60 first, so most
	/// jumps are blocked and few punished). Rate 0 never punishes; a profile with no attacking answer draws nothing extra.</summary>
	[Test]
	public static void JumpPunish_NullKeepsTheProfileWeights_ZeroNeverPunishes()
	{
		var (jn, an, bn, _) = Jumps(Mixed, null);
		Assert.True(bn > an && an * 100 < jn * 35, $"null: the old weights (block 60 first): {an} attacks, {bn} blocks of {jn}");
		var (j0, a0, b0, _) = Jumps(Mixed, 0);
		Assert.True(a0 == 0 && b0 > 0, $"rate 0: never punishes, still blocks ({b0} of {j0})");
		var blockOnly = BehaviourProfile.Parse(Json("mid", """{ "when": "opponent_jumping", "do": "block", "weight": 100 }"""));
		var (jb, ab, bb, _) = Jumps(blockOnly, 100);
		Assert.True(ab == 0 && bb >= jb - 2, $"no attacking answer: nothing to punish with, the block keeps its weight ({bb} of {jb})");
		Assert.True(Jumps(blockOnly, 100) == Jumps(blockOnly, null), "and no extra draw: identical to no tier rate");
	}

	/// <summary>
	/// Test-breaker probe Rate100_JumpSeenDuringOwnRecovery_Measured, made an assertion: at rate 100, a jump the AI sees
	/// while in its own move is answered on its first actionable tick (the press is held, not dropped by the sim), unless
	/// the jumper shows landed on the AI's view by then. The hold itself draws nothing (the roll is when the jump is seen).
	/// </summary>
	[Test]
	public static void Rate100_JumpSeenDuringOwnMove_StartsOnFirstActionableTick()
	{
		const string Heavy = "normal:test-ryo-heavy-punch", Medium = "normal:test-ryo-medium-punch";
		const int reaction = 15;
		var p = BehaviourProfile.Parse(Json("close", $$"""{ "when": "at_close_range", "do": "{{Heavy}}", "weight": 15 }, { "when": "opponent_jumping", "do": "{{Medium}}", "weight": 100 }"""));
		var m = NewMatch(c => c with { P2JumpPunishPct = 100, P1MaxHealth = 1_000_000, P2MaxHealth = 1_000_000 });
		Place(m, 200);
		var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), 5, reaction, guardPct: 0) { Log = new() };
		var starts = new List<int>();
		var freeAfter = new Dictionary<int, bool>(); // P2 idle (can act on the next step) after step t
		var p1Air = new Dictionary<int, bool>();
		string? last = null;
		for (int t = 1; t <= 3000 && m.Phase != MatchPhase.Over; t++)
		{
			var o = ai.Next(AiView.Capture(m));
			m.P1.Health = m.P1.MaxHealth;
			m.Step(t % 61 == 0 ? new FighterInput(InputBits.Up) : FighterInput.None, o);
			string? id = m.P2.State == FighterState.Attack ? m.P2.CurrentMove?.Id : null;
			if (id != null && id != last && id.Contains("medium")) starts.Add(t);
			last = id;
			bool attackEnds = m.P2.State == FighterState.Attack && m.P2.MoveFrame >= m.P2.CurrentMove!.TotalFrames;
			freeAfter[t] = m.P2.State == FighterState.Idle || attackEnds;
			p1Air[t] = m.P1.Airborne;
		}
		var answers = ai.Log!.Where(d => d.Trigger == "opponent_jumping" && d.Kind == "react").ToList();
		Assert.True(answers.Count >= 30, $"the AI decided to punish many jumps ({answers.Count})");
		int started = 0, landed = 0;
		var bad = new List<string>();
		foreach (var a in answers)
		{
			int free = a.Tick;
			while (free > 1 && freeAfter.ContainsKey(free - 1) && !freeAfter[free - 1] && free < a.Tick + 120) free++;
			int s = starts.FirstOrDefault(x => x >= a.Tick && x <= free + 1);
			if (s != 0) { started++; if (s > free + 1) bad.Add($"{a.Tick}: started {s}, free {free}"); }
			else if (!p1Air.GetValueOrDefault(free - reaction - 1, true)) landed++;
			else bad.Add($"{a.Tick}: never started (free {free})");
		}
		GD.Print($"YOK-165 rate 100: {started} of {answers.Count} started on the first actionable tick, {landed} dropped (jumper landed)");
		Assert.True(bad.Count == 0, $"every punish starts on the first actionable tick or the jumper had landed: {string.Join("; ", bad.Take(6))}");
		Assert.True(started * 10 >= answers.Count * 8, $"most start ({started} of {answers.Count})");
	}

	/// <summary>
	/// Plays <paramref name="p1"/> against <paramref name="ai"/> and checks every jump punish (react on opponent_jumping
	/// with <paramref name="answer"/>) starts on the AI's first actionable step (within +1), or the jumper showed landed
	/// on the AI's view by then. Returns the punish count and any misses.
	/// </summary>
	static (int Answers, int Started, List<string> Bad) PunishTiming(Match m, ProfileAi ai, string answer, int reaction, int ticks,
		Func<int, Match, FighterInput> p1, Action<Match>? each = null)
	{
		var starts = new List<int>();
		var freeAfter = new Dictionary<int, bool>();
		var p1Air = new Dictionary<int, bool>();
		string? last = null; int lastFrame = 0;
		for (int t = 1; t <= ticks && m.Phase != MatchPhase.Over; t++)
		{
			each?.Invoke(m);
			var o = ai.Next(AiView.Capture(m));
			m.Step(p1(t, m), o);
			string? id = m.P2.State == FighterState.Attack ? m.P2.CurrentMove?.Id : null;
			int frame = id is null ? 0 : m.P2.MoveFrame;
			if (id != null && answer.EndsWith(id) && frame == 1 && (id != last || lastFrame != 1)) starts.Add(t); // back-to-back starts of the same move count too
			lastFrame = frame;
			last = id;
			bool attackEnds = m.P2.State == FighterState.Attack && m.P2.MoveFrame >= m.P2.CurrentMove!.TotalFrames;
			bool stunEnds = m.P2.State is FighterState.Burst or FighterState.Hitstun or FighterState.Blockstun && m.P2.StunLeft == 0 && !m.P2.Airborne;
			freeAfter[t] = m.P2.State == FighterState.Idle || attackEnds || stunEnds;
			p1Air[t] = m.P1.Airborne;
		}
		var answers = ai.Log!.Where(d => d.Trigger == "opponent_jumping" && d.Kind == "react").ToList();
		int started = 0;
		var bad = new List<string>();
		foreach (var a in answers)
		{
			int free = a.Tick;
			while (freeAfter.TryGetValue(free - 1, out bool f) && !f && free < a.Tick + 120) free++;
			if (free >= freeAfter.Count - 1) continue; // the run ended before it could act
			int s = starts.FirstOrDefault(x => x >= a.Tick && x <= free + 1);
			if (s != 0) started++;
			else if (p1Air.GetValueOrDefault(free - reaction - 1, true)) bad.Add($"{a.Tick}: never started (free {free})");
		}
		return (answers.Count, started, bad);
	}

	/// <summary>Code-critic round 2: an EX special keeps the AI busy for the EX move's own length (stored apart from the
	/// plain one, which shares its id; the screen shows which, <see cref="FighterView.MoveIsEx"/>). An elder at rate 100
	/// that throws its EX special constantly still answers every jump on its first actionable tick.</summary>
	[Test]
	public static void Rate100_JumpDuringOwnExSpecial_StillStarts()
	{
		// The fixture EX specials keep their plain length, so the TEST FIXTURE Spirit Wave gets an EX 20 frames longer here.
		string json = File.ReadAllText(Path.Combine(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir), "test-spirit-wave.json"));
		const string anchor = "{ \"target\": \"projectile.speed\", \"op\": \"add\", \"value\": 4 }";
		Assert.True(json.Contains(anchor), "the fixture's EX changes are where the test expects");
		var data = SpecialLoader.Parse(json.Replace(anchor, anchor + ", { \"target\": \"recovery\", \"op\": \"add\", \"value\": 20 }"));
		var m = NewMatch(c => c with { P2JumpPunishPct = 100, P1MaxHealth = 1_000_000, P2MaxHealth = 1_000_000 });
		m.P2.Equip(SpecialSlot.B, data);
		var ex = m.P2.Specials[(int)SpecialSlot.B]!;
		Assert.Equal(ex.Move.TotalFrames + 20, ex.ExMove!.TotalFrames, "the EX is 20 frames longer");
		var kit = AiLoadout.From(m.P2);
		bool havePlain = kit.TryGetTotalFrames(ex.Move.Id, false, out int plain), haveEx = kit.TryGetTotalFrames(ex.ExMove.Id, true, out int exLen);
		Assert.True(havePlain && haveEx && exLen == plain + 20, $"the loadout keeps both lengths under the shared id ({plain} / {exLen})");
		string sp = "special:" + data.Id, Medium = "normal:test-ryo-medium-punch";
		var p = BehaviourProfile.Parse(Json("mid", $$"""{ "when": "neutral", "do": "{{sp}}", "weight": 100 }, { "when": "opponent_jumping", "do": "{{Medium}}", "weight": 100 }""",
			", \"ex_use\": { \"chance_pct\": 100 }"));
		Place(m, 400);
		var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), 7, 15, AiFightTier.Elder, guardPct: 0) { Log = new() };
		var (answers, started, bad) = PunishTiming(m, ai, Medium, 15, 3000, (t, _) => t % 53 == 0 ? new FighterInput(InputBits.Up) : FighterInput.None,
			mm => { mm.P2.Meter = 300; mm.P1.Health = mm.P1.MaxHealth; });
		Assert.True(ai.Log!.Count(d => d.Kind == "ex") >= 10, "the AI kept throwing the EX special");
		Assert.True(answers >= 20, $"many jumps punished ({answers})");
		GD.Print($"YOK-165 EX: {started} of {answers} started on the first actionable tick");
		Assert.True(bad.Count == 0, $"every punish starts on the first actionable tick: {string.Join("; ", bad.Take(6))}");
	}

	/// <summary>Code-critic round 2: the AI's own burst press, made after the screen it sees, keeps it busy for the burst's
	/// length when it times an anti-air (the screen still shows it in hitstun).</summary>
	[Test]
	public static void Rate100_JumpSeenBeforeOwnBurstShows_StartsAfterTheBurst()
	{
		const int reaction = 16;
		string Medium = "normal:test-ryo-medium-punch";
		var p = BehaviourProfile.Parse(Json("close", $$"""{ "when": "opponent_jumping", "do": "{{Medium}}", "weight": 100 }""",
			", \"burst_use\": { \"chance_pct\": 100, \"min_combo_hits\": 1 }"));
		int bursts = 0, checkedRuns = 0, jumpsDuringBurstWindow = 0;
		var bad = new List<string>();
		foreach (int hitAt in new[] { 40, 41, 42, 43, 44, 45 })
		{
			var m = NewMatch(c => c with { P2JumpPunishPct = 100, P1MaxHealth = 1_000_000, P2MaxHealth = 1_000_000 });
			Place(m, m.Config.BodyWidth / SimConfig.Scale);
			m.BurstFired += (_, i) => { if (i == 1) bursts++; };
			var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), 3, reaction, AiFightTier.Elder, guardPct: 0) { Log = new() };
			var r = PunishTiming(m, ai, Medium, reaction, 160, (t, mm) =>
				t == hitAt ? new FighterInput(InputBits.LightPunch) : t > hitAt + 6 && !mm.P1.Airborne && t < hitAt + 30 ? new FighterInput(InputBits.Up) : FighterInput.None);
			var burst = ai.Log!.FirstOrDefault(d => d.Kind == "burst");
			if (burst.Kind is null || r.Answers == 0) continue;
			checkedRuns++;
			if (ai.Log!.Any(d => d.Trigger == "opponent_jumping" && d.Kind == "react" && d.ViewTick < burst.Tick)) jumpsDuringBurstWindow++;
			bad.AddRange(r.Bad.Select(b => $"hit {hitAt}: {b}"));
		}
		Assert.True(bursts > 0 && checkedRuns > 0, $"the AI burst and punished a jump ({bursts} bursts, {checkedRuns} runs)");
		Assert.True(jumpsDuringBurstWindow > 0, "a jump was decided on from a screen older than the AI's own burst press");
		Assert.True(bad.Count == 0, $"the anti-air starts after the burst, on the first actionable tick: {string.Join("; ", bad.Take(6))}");
	}

	/// <summary>The AI's decisions on the jumps (tick, kind, action, roll, pct), for pinning draw order.</summary>
	static List<string> JumpLog(BehaviourProfile p, int? pct, uint seed = 11, int ticks = 1800)
	{
		var m = NewMatch(c => c with { P2JumpPunishPct = pct, P1MaxHealth = 1_000_000 });
		Place(m, 400);
		var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), seed, 15, guardPct: 0) { Log = new() };
		for (int t = 1; t <= ticks && m.Phase != MatchPhase.Over; t++)
			m.Step(t % 90 == 0 ? new FighterInput(InputBits.Up) : FighterInput.None, ai.Next(AiView.Capture(m)));
		return ai.Log!.Where(d => d.Trigger == "opponent_jumping").Select(d => $"{d.Tick}:{d.Kind}:{d.Action}:{d.Roll}/{d.Pct}").ToList();
	}

	static BehaviourProfile JumpProfile(string behaviours) => BehaviourProfile.Parse(Json("mid", behaviours));
	const string Block60 = """{ "when": "opponent_jumping", "do": "block", "weight": 60 }""";

	/// <summary>Code-critic advisory: a block-only profile with a tier rate makes no extra draw: its whole decision log
	/// (every roll) is the one it makes with no rate.</summary>
	[Test]
	public static void JumpPunish_BlockOnly_WithARate_NoExtraDraws()
	{
		var p = JumpProfile(Block60);
		var none = JumpLog(p, null);
		Assert.True(none.Count >= 10, $"the block rolled ({none.Count})");
		Assert.True(none.SequenceEqual(JumpLog(p, 100)), "rate 100: the same decisions, rolls and ticks");
		Assert.True(none.SequenceEqual(JumpLog(p, 50)), "rate 50: the same");
	}

	/// <summary>An attacking answer the kit cannot perform (a special not in the loadout) is left out of the pick
	/// without a draw: alone, the profile plays exactly as a block-only one; next to a performable attack, rate 100
	/// still answers every jump with that attack and never logs "unavailable".</summary>
	[Test]
	public static void JumpPunish_UnperformableAnswers_AreLeftOut_WithoutADraw()
	{
		const string missing = """{ "when": "opponent_jumping", "do": "special:no-such-special", "weight": 90 }""";
		var blockOnly = JumpLog(JumpProfile(Block60), null);
		Assert.True(blockOnly.SequenceEqual(JumpLog(JumpProfile(Block60 + ", " + missing), 100)),
			"only an unperformable attack: no draw, the block decides as in a block-only profile");
		var (jumps, attacks, blocks, _) = Jumps(JumpProfile(Block60 + ", " + missing +
			""", { "when": "opponent_jumping", "do": "normal:test-ryo-heavy-punch", "weight": 10 }"""), 100);
		Assert.True(attacks >= jumps - 2 && blocks == 0, $"rate 100: every jump answered by the performable attack ({attacks} of {jumps}, {blocks} blocks)");
		var log = JumpLog(JumpProfile(Block60 + ", " + missing + """, { "when": "opponent_jumping", "do": "normal:test-ryo-heavy-punch", "weight": 10 }"""), 100);
		Assert.True(log.Count > 0 && log.All(l => l.Contains(":react:normal:test-ryo-heavy-punch:")), $"never 'unavailable': {string.Join(" ", log.Take(4))}");
	}

	/// <summary>Several attacking answers: one draw at the rate, then one weighted draw; the log is pinned so a change in
	/// draw order shows up here.</summary>
	[Test]
	public static void JumpPunish_SeveralAttacks_PinnedDecisionLog()
	{
		var p = JumpProfile(Block60 +
			""", { "when": "opponent_jumping", "do": "normal:test-ryo-heavy-punch", "weight": 30 }""" +
			""", { "when": "opponent_jumping", "do": "normal:test-ryo-medium-punch", "weight": 10 }""");
		var log = JumpLog(p, 50, 11, 900);
		string got = string.Join(" ", log);
		Assert.Equal(PinnedSeveralAttacks, got, "the decision log (tick:kind:action:roll/pct) is unchanged");
		Assert.True(log.All(l => l.EndsWith("/50") || l.Contains(":block:")), "attacks are logged at the tier rate, blocks at their weight");
	}

	// Seed 11, rate 50, 900 ticks: rolls under 50 are attacks (heavy 30 : medium 10 by the second draw); misses roll the block at 60.
	const string PinnedSeveralAttacks =
		"105:react:block:47/60 195:react:normal:test-ryo-heavy-punch:32/50 285:react:normal:test-ryo-medium-punch:22/50 " +
		"375:react:block:47/60 465:react:normal:test-ryo-medium-punch:37/50 555:react:block:29/60 " +
		"645:react:normal:test-ryo-medium-punch:2/50 735:react:block:24/60 825:react:block:9/60";

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
		Assert.True(prof.GuardPct is null, "the fixture names no guard_pct: the default (off) applies");
		var m = NewMatch(c => c with { P2JumpPunishPct = 50 });
		var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 165, guardPct: 50); // opted in
		var rec = Play(m, ai, 165, 3000);
		Assert.Equal((int?)50, rec.Header.P2JumpPunishPct, "the recording keeps the jump-punish pct");
		Assert.Equal(50, rec.Header.Ai!.GuardPct, "and the guard the AI played with");
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
		var old = Play(oldM, new ProfileAi(prof, 1, AiLoadout.From(oldM.P2), 7), 7, 1500); // the default: guard off
		string header = System.Text.Json.JsonSerializer.Serialize(old.Header);
		Assert.True(!header.Contains("GuardPct") && !header.Contains("JumpPunishPct"), $"neither field is written: {header}");
		Assert.True(Replayer.Run(ReplayRecording.FromBytes(old.ToBytes()), Catalog).Ok, "an old-style recording replays (absent guard = 0)");
		Assert.Equal(Fingerprint.Of(SimConfig.Default), Fingerprint.Of(SimConfig.Default with { P2JumpPunishPct = null }), "no pct fingerprints as before YOK-165");
		Assert.True(Fingerprint.Of(SimConfig.Default) != Fingerprint.Of(SimConfig.Default with { P2JumpPunishPct = 50 }), "a pct changes the fingerprint");
	}
}
