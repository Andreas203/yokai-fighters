using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-156 probes (test-breaker). Y4: the apprentice (P2) deals half, rounded down per hit; Ryo (P1) full.</summary>
public static class ApprenticeDamageProbes
{
	const InputBits MK = InputBits.MediumKick;
	static readonly SimConfig Half = SimConfig.Default with { P2DamagePct = 50 };

	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();

	static Match Touching(SimConfig cfg, MoveData[]? p1 = null, MoveData[]? p2 = null)
	{
		var m = new Match(cfg, p1 ?? Moves(), p2 ?? Moves());
		m.P1.X = -m.Config.BodyWidth / 2;
		m.P2.X = m.Config.BodyWidth / 2;
		return m;
	}

	static FighterInput On1(int t, InputBits b) => t == 1 ? new FighterInput(b) : FighterInput.None;

	// TEST FIXTURE quake with an aftershock at Lv 3 (made-up numbers; same shape as PushBoxAftershockTests).
	const string QuakeJson = """
	{ "kind": "special", "id": "probe-quake", "name": "TEST FIXTURE: quake", "source": "probe", "starter": false,
	  "rarity": "rare", "locked": true, "clip": "probe-quake", "rules": ["A5"],
	  "input": { "kata": { "slot": "D", "motion": "22" }, "kihon": { "slot": "D", "direction": "down" } },
	  "card": { "plain": "TEST FIXTURE", "frames": "9 / 3 / 20" },
	  "levels": {
	    "1": { "frame_data": { "startup": 9, "active": 3, "recovery": 20, "damage": 51, "hitstun": 20, "blockstun": 15,
	      "hitboxes": [ { "frames": [10, 12], "rect": { "x": 0, "y": 0, "w": 300, "h": 110 } } ],
	      "hurtboxes": [ { "frames": [1, 32], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } } ],
	      "properties": { "low": true } } },
	    "2": { "status": "proposed", "tuning": { "target": "recovery", "op": "add", "value": -2 } },
	    "3": { "evolution": { "id": "probe-aftershock", "name": "TEST FIXTURE", "preset": "probe-aftershock",
	      "changes": [ { "target": "properties.aftershock_delay", "op": "set", "value": 30 } ] } } } }
	""";

	// Y4: an aftershock is P2's hit too: 51 -> 25 for the quake AND the aftershock (rounded down each, not 51*2/2 = 51).
	[Test]
	public static void Aftershock_P2QuakeAndAftershockBothScaled()
	{
		var move = SpecialLoader.Parse(QuakeJson).Build(3, false);
		var hits = new List<HitEvent>();
		var m = new Match(Half with { HitstopEnabled = false }, new[] { move }, new[] { move });
		m.P1.X = -60 * SimConfig.Scale; m.P2.X = 60 * SimConfig.Scale;
		m.Hit += (_, e) => hits.Add(e);
		for (int t = 1; t <= 60; t++) m.Step(FighterInput.None, t == 1 ? FighterInput.Attack(0) : FighterInput.None);
		var p2 = hits.Where(h => h.Attacker == 1).ToList();
		Assert.Equal(2, p2.Count, "quake and aftershock both land");
		Assert.True(p2.All(h => h.Damage == 25), "each 51 at 50% = 25");
		Assert.Equal(m.P1.MaxHealth - 50, m.P1.Health, "health lost = 25 + 25");
	}

	// Y4: a trade, both MK on the same tick: Ryo takes 27, the apprentice takes the full 55.
	[Test]
	public static void Trade_RyoFull_ApprenticeHalf()
	{
		var hits = new List<HitEvent>();
		var m = Touching(Half);
		m.Hit += (_, e) => hits.Add(e);
		for (int t = 1; t <= 40; t++) m.Step(On1(t, MK), On1(t, MK));
		var r = hits.First(h => h.Attacker == 0); var a = hits.First(h => h.Attacker == 1);
		Assert.Equal(r.Counter ? 66 : 55, r.Damage, "Ryo's MK is full (55, 66 on counterhit)");
		Assert.Equal(a.Counter ? 33 : 27, a.Damage, "apprentice's MK is half (27, 33 on counterhit)");
		Assert.Equal(m.P1.MaxHealth - hits.Where(h => h.Attacker == 1).Sum(h => h.Damage), m.P1.Health, "Ryo's health matches events");
	}

	// Y4 + health 0: scaled damage that exactly kills KOs (27 of 27) and one more point of health survives, never negative.
	[Test]
	public static void ScaledHit_KoBoundary_NoNegativeHealth()
	{
		foreach (var (start, ko) in new[] { (27, true), (28, false), (1, true), (20, true) })
		{
			var m = Touching(Half with { P1StartHealth = start });
			for (int t = 1; t <= 30; t++) m.Step(FighterInput.None, On1(t, MK));
			Assert.True(m.P1.Health >= 0, $"start {start}: health never negative");
			Assert.Equal(ko, m.P1.KnockedOut, $"start {start}: KO iff 27 >= health");
			if (!ko) Assert.Equal(1, m.P1.Health, $"start {start}: left with 1");
		}
	}

	// Y4: both fighters at lethal health on one tick: double KO is still decided (Winner -1) at 50%.
	[Test]
	public static void Trade_DoubleKoAtHalf()
	{
		var m = Touching(Half with { P1StartHealth = 27, P2StartHealth = 55 });
		for (int t = 1; t <= 40; t++) m.Step(On1(t, MK), On1(t, MK));
		Assert.True(m.P1.KnockedOut && m.P2.KnockedOut, "both KO");
		Assert.Equal(-1, m.Winner, "draw");
	}

	// Y4: a 1-damage hit scales to 0: it still connects (event, meter, hitstun) and nothing goes negative or KOs.
	[Test]
	public static void OneDamageHit_ScalesToZero_StillConnects()
	{
		var mk = Moves().First(mv => mv.Damage == 55);
		var tiny = mk with { Damage = 1 };
		var moves = Moves().Select(mv => mv.Id == mk.Id ? tiny : mv).ToArray();
		var hits = new List<HitEvent>();
		var m = Touching(Half, moves, moves);
		m.Hit += (_, e) => hits.Add(e);
		for (int t = 1; t <= 30; t++) m.Step(FighterInput.None, On1(t, MK));
		var h = hits.Single(e => e.Attacker == 1);
		Assert.Equal(0, h.Damage, "1 at 50% = 0");
		Assert.True(!h.Blocked, "it still hits");
		Assert.Equal(m.P1.MaxHealth, m.P1.Health, "no health lost");
		Assert.True(m.P2.Meter > 0, "the attacker still gains meter on a 0-damage hit (C5)");
		Assert.True(!m.P1.KnockedOut, "no KO");
	}

	// Determinism: same inputs twice give identical hash every tick; 50% differs from 100%.
	[Test]
	public static void Determinism_SameInputsSameHash_PctChangesIt()
	{
		List<ulong> Play(SimConfig cfg)
		{
			var m = Touching(cfg);
			var l = new List<ulong>();
			for (int t = 1; t <= 120; t++) { m.Step(On1(t % 40 + 1, MK), On1(t % 30 + 1, MK)); l.Add(m.StateHash()); }
			return l;
		}
		Assert.True(Play(Half).SequenceEqual(Play(Half)), "repeat run identical");
		Assert.True(!Play(Half).SequenceEqual(Play(SimConfig.Default)), "50% diverges from 100%");
	}

	// Y4: Ryo (P1) is never scaled in a whole-run harness; row 1 is 50%, every row >= 2 is not (rows by node).
	[Test]
	public static void WholeRun_OnlyRowOneIsHalf_AllSeeds()
	{
		var content = HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
		var bot = BotConfig.LoadDefault(ContentPaths.DataDir);
		foreach (int seed in new[] { 1, 2, 3, 7 })
		{
			var seen = new List<(int row, int? p1, int? p2)>();
			FightRunner probe = (c, b, s, _) =>
			{
				var m = NodeFight.NewMatch(c.Sources, s.Run, s, s.Node!.Fighter, b.Scheme);
				seen.Add((s.Node.Row, m.Config.P1DamagePct, m.Config.P2DamagePct));
				return new FightRecord { Profile = "stub", Won = true, Ticks = 600, DamageTaken = 10, HealthLeft = Math.Max(1, s.Run.Health - 10), Bot = new BotStats() };
			};
			RunHarness.PlayRun(content, bot, (uint)seed, probe);
			Assert.True(seen.Count > 1, $"seed {seed}: fought several nodes");
			foreach (var (row, p1, p2) in seen)
			{
				Assert.True(p1 is null, $"seed {seed} row {row}: Ryo unscaled");
				Assert.Equal(row == 1 ? (int?)50 : null, p2, $"seed {seed} row {row}: P2 pct");
			}
		}
	}
}
