using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Harness;
using YokaiFighters.Replay;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-156, Y4: the apprentice (row 1) deals half damage. The tier's <c>damage_dealt_pct</c> in
/// data/map/node-scenes.json reaches the sim through <see cref="NodeFight"/> (game and harness alike) as
/// <see cref="SimConfig.P2DamagePct"/>; every hit and throw P2 lands is scaled by it, rounded down per hit, after
/// counterhit and precision. Other tiers deal full damage. Fight checks use the TEST FIXTURE kit
/// (MK 55, throw 120, Spirit Wave 60, Rising Talisman 90).
/// </summary>
public static class ApprenticeDamageTests
{
	const InputBits LP = InputBits.LightPunch, LK = InputBits.LightKick, MK = InputBits.MediumKick, HK = InputBits.HeavyKick;
	static readonly SimConfig Half = SimConfig.Default with { P2DamagePct = 50 };
	static string MapDir => ContentPaths.Data("map");

	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();
	static SpecialData[] Specials() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));

	/// <summary>Fixture kits with starters; P1 at <paramref name="p1x"/>, P2 at <paramref name="p2x"/> (units).</summary>
	static Match Setup(SimConfig? cfg, int p1x, int p2x, List<HitEvent> hits, List<ThrowEvent>? throws = null)
	{
		var m = new Match(cfg, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Specials());
		m.P1.X = p1x * SimConfig.Scale;
		m.P2.X = p2x * SimConfig.Scale;
		m.Hit += (_, e) => hits.Add(e);
		if (throws != null) m.Throw += (_, e) => throws.Add(e);
		return m;
	}

	static void Run(Match m, int n, Func<int, FighterInput> p1, Func<int, FighterInput> p2)
	{
		for (int t = 1; t <= n; t++) m.Step(p1(t), p2(t));
	}

	static Match Touching(SimConfig? cfg, List<HitEvent> hits, List<ThrowEvent>? throws = null)
	{
		var m = Setup(cfg, 0, 0, hits, throws);
		m.P1.X = -m.Config.BodyWidth / 2;
		m.P2.X = m.Config.BodyWidth / 2;
		return m;
	}

	static FighterInput On1(int t, InputBits b) => t == 1 ? new FighterInput(b) : FighterInput.None;

	// --- The scale itself -------------------------------------------------------------------------

	[Test]
	public static void ScaleDamage_HalvesP2Only_RoundingDown()
	{
		Assert.Equal(27, Half.ScaleDamage(1, 55), "55 at 50% = 27 (rounded down)");
		Assert.Equal(12, Half.ScaleDamage(1, 25), "25 at 50% = 12");
		Assert.Equal(0, Half.ScaleDamage(1, 1), "1 at 50% = 0");
		Assert.Equal(60, Half.ScaleDamage(1, 120), "throw 120 at 50% = 60");
		Assert.Equal(55, Half.ScaleDamage(0, 55), "P1 (Ryo) unscaled");
		Assert.Equal(55, SimConfig.Default.ScaleDamage(1, 55), "no pct = full damage");
		Assert.True(SimConfig.Default.P1DamagePct is null && SimConfig.Default.P2DamagePct is null, "default config names no pct");
	}

	// --- In the fight -----------------------------------------------------------------------------

	[Test]
	public static void ApprenticeStrike_DealsHalf_RyoDealsFull()
	{
		foreach (var (cfg, p2Dmg) in new[] { (Half, 27), ((SimConfig?)null, 55) })
		{
			var hits = new List<HitEvent>();
			var m = Touching(cfg, hits);
			Run(m, 40, _ => FighterInput.None, t => On1(t, MK));
			var h = hits.Single();
			Assert.True(h.Attacker == 1 && !h.Blocked && !h.Counter, "P2's MK lands clean");
			Assert.Equal(p2Dmg, h.Damage, $"P2 MK 55 at {cfg?.P2DamagePct ?? 100}%");
			Assert.Equal(1000 - p2Dmg, m.P1.Health, "health loses the scaled damage");
		}
		// Ryo's hit in the same apprentice config is not scaled.
		var hits2 = new List<HitEvent>();
		var m2 = Touching(Half, hits2);
		Run(m2, 40, t => On1(t, MK), _ => FighterInput.None);
		Assert.Equal(55, hits2.Single().Damage, "Ryo's MK stays 55 against the apprentice");
		Assert.Equal(1000 - 55, m2.P2.Health, "the apprentice takes full damage");
	}

	[Test]
	public static void ApprenticeCounterHit_ScaledAfterTheBonus()
	{
		// P1 starts a slow HK; P2's quicker MK catches it in startup: a counterhit (C7, +20%): 55 → 66 → 33 at 50%.
		var hits = new List<HitEvent>();
		var m = Touching(Half, hits);
		Run(m, 40, t => On1(t, HK), t => On1(t, MK));
		var h = hits.First(e => e.Attacker == 1);
		Assert.True(h.Counter, "P2's MK counterhits the HK in startup");
		Assert.Equal(33, h.Damage, "counterhit 66 at 50% = 33");
	}

	[Test]
	public static void ApprenticeThrow_DealsHalf()
	{
		foreach (var (cfg, dmg) in new[] { (Half, 60), ((SimConfig?)null, 120) })
		{
			var hits = new List<HitEvent>();
			var throws = new List<ThrowEvent>();
			var m = Touching(cfg, hits, throws);
			Run(m, 30, _ => FighterInput.None, t => On1(t, LP | LK));
			var land = throws.Single(e => e.Outcome == ThrowOutcome.Land);
			Assert.Equal(1, land.Attacker, "P2 throws");
			Assert.Equal(dmg, land.Damage, $"throw event at {cfg?.P2DamagePct ?? 100}%");
			Assert.Equal(dmg, hits.Single(e => e.Attacker == 1).Damage, "the throw's hit event reports what landed");
			Assert.Equal(1000 - dmg, m.P1.Health, "health loses the scaled throw");
		}
	}

	[Test]
	public static void ApprenticeSpecials_DealHalf()
	{
		// Spirit Wave (projectile, 60) from 600 units; Rising Talisman Lv 2 (99, odd: rounds down to 49) from 100.
		var hits = new List<HitEvent>();
		var m = Setup(Half, -300, 300, hits);
		Run(m, 80, _ => FighterInput.None, t => t == 1 ? FighterInput.Special(SpecialSlot.A, false) : FighterInput.None);
		Assert.Equal(30, hits.Single().Damage, "Spirit Wave 60 at 50%");
		Assert.Equal(970, m.P1.Health, "projectile damage applied");

		var hits2 = new List<HitEvent>();
		var m2 = Setup(Half, -300, -200, hits2);
		m2.P2.LevelUp(SpecialSlot.B);
		Run(m2, 20, _ => FighterInput.None, t => t == 1 ? FighterInput.Special(SpecialSlot.B, false) : FighterInput.None);
		Assert.Equal(49, hits2.First().Damage, "Lv 2 Rising Talisman 99 at 50% = 49 (rounded down)");

		// Ryo's projectile against the apprentice is full.
		var hits3 = new List<HitEvent>();
		var m3 = Setup(Half, -300, 300, hits3);
		Run(m3, 80, t => t == 1 ? FighterInput.Special(SpecialSlot.A, false) : FighterInput.None, _ => FighterInput.None);
		Assert.Equal(60, hits3.Single().Damage, "Ryo's Spirit Wave stays 60");
	}

	// --- Data and NodeFight -----------------------------------------------------------------------

	[Test]
	public static void Data_OnlyTheApprenticeTierDealsHalf()
	{
		var table = NodeSceneTable.Default(MapDir);
		var graph = FileMapSource.Default(MapDir).Load();
		int apprentices = 0;
		foreach (var n in graph.Nodes)
		{
			var ctx = table.Resolve(n).Context;
			if (ctx.Tier == "apprentice") { apprentices++; Assert.Equal(50, ctx.DamageDealtPct, $"{n.Id}: the apprentice deals half (Y4)"); }
			else Assert.Equal(SimConfig.FullDamagePct, ctx.DamageDealtPct, $"{n.Id}: tier {ctx.Tier} deals full damage");
			if (n.Row == 1) Assert.Equal("apprentice", ctx.Tier, $"{n.Id}: row 1 is the apprentice (R1)");
		}
		Assert.True(apprentices > 0, "the map has an apprentice node");
	}

	[Test]
	public static void Data_BadDamagePctIsRejected()
	{
		string json = File.ReadAllText(Path.Combine(MapDir, NodeSceneTable.DefaultFile));
		const string good = "\"damage_dealt_pct\": 50";
		Assert.True(json.Contains(good), "the apprentice tier names damage_dealt_pct 50");
		foreach (var bad in new[] { "0", "101", "-5", "50.5", "\"50\"", "null" })
		{
			bool threw = false;
			try { NodeSceneTable.Parse(json.Replace(good, "\"damage_dealt_pct\": " + bad)); }
			catch (FormatException) { threw = true; }
			Assert.True(threw, $"damage_dealt_pct {bad} is rejected");
		}
		var full = NodeSceneTable.Parse(json.Replace(good, "\"damage_dealt_pct\": 100"));
		Assert.True(full is not null, "100 is allowed");
	}

	static FightRecord StubWin(RunSession s) => new()
	{
		Profile = "stub", Won = true, Ticks = 600, DamageTaken = 10, HealthLeft = Math.Max(1, s.Run.Health - 10), Bot = new BotStats(),
	};

	[Test]
	public static void NodeFight_ScalesP2ByTheNodesTier_GameAndHarness()
	{
		var content = HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
		var bot = BotConfig.LoadDefault(ContentPaths.DataDir);
		var seen = new Dictionary<string, int?>();
		FightRunner probe = (c, b, s, _) =>
		{
			var m = NodeFight.NewMatch(c.Sources, s.Run, s, s.Node!.Fighter, b.Scheme);
			Assert.True(m.Config.P1DamagePct is null, $"{s.Node.NodeId}: Ryo's damage is never scaled");
			int? pct = m.Config.P2DamagePct;
			if (seen.TryGetValue(s.Node.Tier, out var was)) Assert.Equal(was, pct, $"tier {s.Node.Tier} is consistent");
			seen[s.Node.Tier] = pct;
			return StubWin(s);
		};
		RunHarness.PlayRun(content, bot, 3, probe);
		Assert.Equal((int?)50, seen.GetValueOrDefault("apprentice"), "apprentice node: P2 deals 50%");
		foreach (var tier in new[] { "yokai", "elder", "tanuki" })
		{
			Assert.True(seen.ContainsKey(tier), $"the run fought a {tier} node");
			Assert.True(seen[tier] is null, $"{tier}: full damage (no pct)");
		}

		// The fight scene builds through the same NodeFight: the apprentice node gives the same config.
		var session = RunSession.Start(content.Pool, content.Map, content.Scenes, 5);
		Assert.True(session.Enter(session.Reachable()[0]).Ok, "enter row 1");
		Assert.Equal("apprentice", session.Node!.Tier, "row 1 is the apprentice");
		var fm = FightScene.NewMatch(session.Run, session);
		Assert.Equal((int?)50, fm.Config.P2DamagePct, "FightScene.NewMatch: the apprentice deals 50%");
	}

	// --- Replays ------------------------------------------------------------------------------------

	static ReplayCatalog? _catalog;
	static ReplayCatalog Catalog => _catalog ??= ReplayCatalog.FromRepo(Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + ".."));

	static ReplayRecording Record(Match m, int seed, int steps)
	{
		var rng = new Random(seed); // seeded: the same inputs every run
		var buttons = new[] { InputBits.LightPunch, InputBits.MediumKick, InputBits.HeavyPunch, LP | LK };
		FighterInput Next()
		{
			var bits = (InputBits)0;
			int d = rng.Next(5);
			if (d == 0) bits |= InputBits.Right; else if (d == 1) bits |= InputBits.Left;
			if (rng.Next(3) == 0) bits |= buttons[rng.Next(buttons.Length)];
			if (rng.Next(60) == 0) return FighterInput.Special(SpecialSlot.A, false, bits);
			return new FighterInput(bits);
		}
		var rec = ReplayRecorder.Begin(m, null, 0, null);
		for (int t = 0; t < steps && m.Phase != MatchPhase.Over; t++)
		{
			var a = Next();
			var b = Next();
			m.Step(a, b);
			rec.Record(a, b, m.StateHash());
		}
		return rec.Finish(m);
	}

	[Test]
	public static void ApprenticeFight_ReplaysIdentically_AndThePctIsPartOfIt()
	{
		Match Build() => FightScene.NewMatch(null, null, c => c with { P2DamagePct = 50 });
		var rec = Record(Build(), 156, 3000);
		Assert.Equal((int?)50, rec.Header.P2DamagePct, "the recording keeps P2's damage pct");
		var back = ReplayRecording.FromBytes(rec.ToBytes());
		Assert.Equal((int?)50, back.Header.P2DamagePct, "the pct survives the file");
		var r = Replayer.Run(back, Catalog);
		Assert.True(r.Ok, $"the apprentice fight replays: {r}");
		Assert.Equal(rec.Steps, r.StepsRun, "every step replayed");

		var again = Record(Build(), 156, 3000);
		Assert.True(rec.Hashes.SequenceEqual(again.Hashes), "same seed, same fight: identical state hash every tick");

		var stripped = ReplayRecording.FromBytes(rec.ToBytes());
		stripped.Header.P2DamagePct = null;
		Assert.True(!Replayer.Run(stripped, Catalog).Ok, "the same inputs at full damage do not replay as recorded");

		var plain = Record(FightScene.NewMatch(null), 156, 300);
		string header = System.Text.Json.JsonSerializer.Serialize(plain.Header);
		Assert.True(!header.Contains("DamagePct"), "full damage: the field is not written (older headers)");
		Assert.Equal(Fingerprint.Of(SimConfig.Default), Fingerprint.Of(SimConfig.Default with { P2DamagePct = null }), "no pct fingerprints as before YOK-156");
		Assert.True(Fingerprint.Of(SimConfig.Default) != Fingerprint.Of(Half), "a pct changes the config fingerprint");
		Assert.True(Replayer.Run(ReplayRecording.FromBytes(plain.ToBytes()), Catalog).Ok, "a full-damage recording replays");
	}
}
