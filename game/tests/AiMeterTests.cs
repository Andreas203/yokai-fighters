using System;
using System.IO;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-57: the AI spends meter on EX specials (E16, C5) and bursts (C6, E13) only when its profile says so, through the
/// same FighterInput a player sends, from on-screen state only (P6). Fixture kits only (as AiTests), so the
/// test-foxfire EX is the one in tests/fixtures/rewards/.
/// </summary>
public static class AiMeterTests
{
	static BehaviourProfile Fixture(string id) =>
		BehaviourProfile.LoadFile(Path.Combine(ProjectSettings.GlobalizePath(FightScene.FixtureProfilesDir), id + ".json"));
	static BehaviourProfile Aggressive => Fixture("test-kitsune-aggressive");
	static BehaviourProfile Patient => Fixture("test-kitsune-patient");

	/// <summary>The aggressive fixture plus a neutral Foxfire, so it builds meter with normals and throws specials often.</summary>
	static BehaviourProfile MeterProfile(ExUse? ex = null, BurstUse? burst = null) => Aggressive with
	{
		Behaviours = new System.Collections.Generic.List<Behaviour>(Aggressive.Behaviours)
			{ new(Observation.Neutral, BehaviourProfile.ParseAction("special:test-foxfire"), 40) },
		ExUse = ex, BurstUse = burst,
	};

	static Match NewMatch()
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		try { return FightScene.NewMatch(); }
		finally { FightScene.Sources = saved; }
	}

	/// <summary>The same fixture fight with 10x health, so a fight lasts long enough to build meter many times over
	/// (a 1000-health fight is often over before a bar fills): the rate test needs many EX chances.</summary>
	static Match LongMatch()
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		try
		{
			var ryo = FightScene.LoadKit(Kit.Ryo);
			var foe = FightScene.LoadKit("kitsune");
			var m = new Match(SimConfig.Default with { P1MaxHealth = 10000, P2MaxHealth = 10000 }, ryo.Moves, foe.Moves);
			foe.Equip(m.P2);
			ryo.Equip(m.P1);
			return m;
		}
		finally { FightScene.Sources = saved; }
	}

	static bool IsEx(FighterInput o) => o.IsSpecialRequest(out _, out bool ex) && ex;
	static bool IsBurst(FighterInput o) => (o.Bits & ProfileAi.BurstButtons) == ProfileAi.BurstButtons;

	/// <summary>Sum of harness results over seeds (each seed its own fights).</summary>
	static (int exRolls, int exAsked, int exStarted, int exNoMeter, int bRolls, int bPressed, int bFired, int maxPerRound, int bThrown, int rounds)
		Over(BehaviourProfile p, P1Script script, int seeds, int ticks, Func<Match>? newMatch = null)
	{
		var t = (0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
		for (uint s = 1; s <= seeds; s++)
		{
			var r = AiHarness.Run(newMatch ?? NewMatch, p, script, s, ticks);
			t = (t.Item1 + r.ExRolls, t.Item2 + r.ExAsked, t.Item3 + r.ExStarted, t.Item4 + r.ExWithoutMeter,
				t.Item5 + r.BurstRolls, t.Item6 + r.BurstPressed, t.Item7 + r.BurstsFired, Math.Max(t.Item8, r.MaxBurstsInRound),
				t.Item9 + r.BurstWhileThrown, t.Item10 + r.Rounds + 1);
		}
		return t;
	}

	static string Json(string extra) => $$"""
		{ "kind": "profile", "id": "test-meter", "yokai": "kitsune", "temperament": "aggressive",
		  "reaction": { "tier": "elder", "frames": 18 }, "aggression_bias": 70, "preferred_range": "close",
		  "habit": { "id": "h", "description": "d", "when": "self_got_up", "do": "jump", "chance_pct": 60 },
		  "behaviours": [ { "when": "neutral", "do": "special:test-foxfire", "weight": 50 } ],
		  {{extra}}
		  "rules": ["E16", "C6"] }
		""";

	[Test]
	public static void Profile_ExAndBurstFieldsAreOptionalData()
	{
		Assert.True(Aggressive.ExUse is null && Aggressive.BurstUse is null, "fixture (standard-tier) profiles name neither: no meter use (E22)");
		var p = BehaviourProfile.Parse(Json("""
			"ex_use": { "chance_pct": 35, "keep_meter": 100 }, "burst_use": { "chance_pct": 70, "min_combo_hits": 3, "max_health_pct": 50 },
			"""));
		Assert.Equal(new ExUse(35, 100), p.ExUse, "ex_use from data");
		Assert.Equal(new BurstUse(70, 3, 50), p.BurstUse, "burst_use from data");
		var d = BehaviourProfile.Parse(Json("""
			"ex_use": { "chance_pct": 20 }, "burst_use": { "chance_pct": 10 },
			"""));
		Assert.Equal(new ExUse(20, 0), d.ExUse, "keep_meter defaults to 0");
		Assert.Equal(new BurstUse(10, 1, 100), d.BurstUse, "min_combo_hits 1, max_health_pct 100 by default");
		foreach (var bad in new[] { "\"ex_use\": { \"chance_pct\": 101 },", "\"ex_use\": { },", "\"burst_use\": { \"chance_pct\": 50, \"min_combo_hits\": 0 },",
			"\"burst_use\": { \"chance_pct\": 50, \"max_health_pct\": 0 },", "\"ex_use\": { \"chance_pct\": 50, \"keep_meter\": 301 }," })
		{
			bool threw = false;
			try { BehaviourProfile.Parse(Json(bad)); } catch (FormatException) { threw = true; }
			Assert.True(threw, $"rejects {bad}");
		}
	}

	/// <summary>E22: a profile without ex_use/burst_use never asks for EX nor presses the burst, whatever it sees.</summary>
	[Test]
	public static void StandardProfiles_NeverSpendMeter()
	{
		foreach (var p in new[] { Aggressive, Patient })
		{
			var m = NewMatch();
			var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), 3);
			var p1Rng = new SimRng(77);
			InputBits held = InputBits.None; int hold = 0;
			for (int k = 0; k < 6000; k++)
			{
				m.P2.Meter = m.Config.MeterMax; // a full bar on screen all fight: still no spending
				var o = ai.Next(AiView.Capture(m));
				Assert.True(!IsEx(o) && !IsBurst(o), $"{p.Id}: tick {k}: no EX request, no burst press");
				m.Step(AiHarness.RandomInput(p1Rng, ref held, ref hold), o);
				if (m.Phase == MatchPhase.Over) m.Reset();
			}
			var r = Over(p, P1Script.Random, 3, 3000);
			Assert.Equal(0, r.exRolls + r.exStarted + r.bRolls + r.bFired, $"{p.Id}: no EX or burst in the harness");
		}
	}

	/// <summary>E16/C5: no meter on screen, no EX request (and the sim never starts an EX without the meter); a full bar
	/// with chance 100 turns every special into an EX request through the player's slot-request path.</summary>
	[Test]
	public static void Ex_NeedsMeterOnScreen()
	{
		var p = BehaviourProfile.Parse(Json("\"ex_use\": { \"chance_pct\": 100 },"));
		foreach (int meter in new[] { 0, 99, 100, 300 })
		{
			var m = NewMatch();
			var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), 5) { Log = new() };
			int exStarted = 0, specials = 0, exAsked = 0;
			m.SpecialStarted += (_, e) => { if (e.Fighter == 1) { specials++; if (e.Ex) exStarted++; } };
			for (int k = 0; k < 2400; k++)
			{
				m.P2.Meter = meter;
				var o = ai.Next(AiView.Capture(m));
				if (IsEx(o)) exAsked++;
				m.Step(FighterInput.None, o);
				if (m.Phase == MatchPhase.Over) m.Reset();
			}
			Assert.True(specials >= 10, $"meter {meter}: the AI threw specials ({specials})");
			if (meter < 100)
				Assert.Equal(0, exAsked + exStarted, $"meter {meter}: below one bar, no EX asked or started");
			else
			{
				Assert.Equal(specials, exStarted, $"meter {meter}: every special came out EX");
				Assert.True(exStarted <= exAsked, $"meter {meter}: EX specials {exStarted} only from EX requests {exAsked} (a request while busy or with Foxfire still out does nothing)");
			}
		}
		// keep_meter: with 1 bar showing and 1 bar to keep back, no EX.
		var keep = BehaviourProfile.Parse(Json("\"ex_use\": { \"chance_pct\": 100, \"keep_meter\": 100 },"));
		var m2 = NewMatch();
		var ai2 = new ProfileAi(keep, 1, AiLoadout.From(m2.P2), 5);
		for (int k = 0; k < 1200; k++) { m2.P2.Meter = 150; var o = ai2.Next(AiView.Capture(m2)); Assert.True(!IsEx(o), "keep_meter holds the reserve"); m2.Step(FighterInput.None, o); }
	}

	/// <summary>Acceptance: over many seeded fights the EX rate matches the profile's chance, and never without meter.</summary>
	[Test]
	public static void Ex_RateMatchesProfile()
	{
		foreach (int pct in new[] { 25, 75 })
		{
			var r = Over(MeterProfile(new ExUse(pct)), P1Script.Random, 20, 9000, LongMatch);
			Assert.True(r.exRolls >= 60, $"ex {pct}%: enough chances to read the rate ({r.exRolls})");
			double rate = 100.0 * r.exAsked / r.exRolls;
			GD.Print($"AiMeterTests: ex_use {pct}%: asked {r.exAsked}/{r.exRolls} = {rate:0.0}%, started {r.exStarted}, without meter {r.exNoMeter}");
			Assert.True(Math.Abs(rate - pct) <= 12, $"ex {pct}%: asked {r.exAsked}/{r.exRolls} = {rate:0.0}%");
			Assert.Equal(0, r.exNoMeter, $"ex {pct}%: no EX started without the meter");
			// A request can land while the AI is still in a stun it hasn't seen yet, or with its Foxfire still out: then
			// nothing starts (as for a player). Every EX that does start came from an EX request.
			Assert.True(r.exStarted > 0 && r.exStarted <= r.exAsked, $"ex {pct}%: the sim started {r.exStarted} of {r.exAsked} asked");
		}
	}

	/// <summary>Acceptance: burst rolls match the profile, a burst at most once per fight, never out of a throw.</summary>
	[Test]
	public static void Burst_RateMatchesProfile_OncePerFight()
	{
		var r = Over(Aggressive with { BurstUse = new BurstUse(50) }, P1Script.Random, 30, 2400);
		Assert.True(r.bRolls >= 30, $"enough burst chances ({r.bRolls})");
		double rate = 100.0 * r.bPressed / r.bRolls;
		GD.Print($"AiMeterTests: burst_use 50%: pressed {r.bPressed}/{r.bRolls} = {rate:0.0}%, fired {r.bFired} over {r.rounds} fights, max per fight {r.maxPerRound}");
		Assert.True(rate is >= 35 and <= 65, $"burst 50%: pressed {r.bPressed}/{r.bRolls} = {rate:0.0}%");
		Assert.True(r.maxPerRound <= 1, $"C6: at most one burst per fight (saw {r.maxPerRound})");
		Assert.True(r.bFired <= r.bPressed && r.bFired * 10 >= r.bPressed * 8, $"presses land in hitstun: fired {r.bFired} of {r.bPressed}");
		Assert.True(r.bFired <= r.rounds, $"bursts {r.bFired} <= fights {r.rounds}");

		var always = Over(Aggressive with { BurstUse = new BurstUse(100) }, P1Script.Thrower, 6, 3600);
		Assert.Equal(0, always.bThrown, "a thrown fighter cannot burst (E13)");
		Assert.True(always.maxPerRound <= 1, "once per fight against the thrower too");

		var never = Over(Aggressive with { BurstUse = new BurstUse(0) }, P1Script.Random, 4, 2400);
		Assert.Equal(0, never.bRolls + never.bFired, "chance 0 = never");

		// Conditions are data: min_combo_hits above anything the random P1 lands means no rolls.
		var combo = Over(Aggressive with { BurstUse = new BurstUse(100, MinComboHits: 20) }, P1Script.Random, 4, 2400);
		Assert.Equal(0, combo.bRolls, "min_combo_hits 20: never reached");
	}

	/// <summary>Determinism: the same seed gives the same fight with EX and burst in play; another seed differs.</summary>
	[Test]
	public static void MeterAi_IsDeterministic()
	{
		var p = MeterProfile(new ExUse(50), new BurstUse(50));
		var a = AiHarness.Run(NewMatch, p, P1Script.Random, 21, 5000);
		var b = AiHarness.Run(NewMatch, p, P1Script.Random, 21, 5000);
		var c = AiHarness.Run(NewMatch, p, P1Script.Random, 22, 5000);
		Assert.Equal(a.FinalHash, b.FinalHash, "same seed, same hash");
		Assert.Equal(a.DecisionsCsv(), b.DecisionsCsv(), "same seed, same decisions");
		Assert.True(a.ExRolls + a.BurstRolls > 0, "meter decisions were made");
		Assert.True(a.DecisionsCsv() != c.DecisionsCsv(), "another seed plays differently");
	}
}
