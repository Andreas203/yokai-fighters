using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-184 (C4 break window and press, Y6, R5, P6): the test bot's throw_break_pct.</summary>
public static class BotThrowBreakBreakerProbes
{
	static HarnessContent? _content;
	static HarnessContent Content => _content ??= HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static BotConfig Bot => BotConfig.LoadDefault(ContentPaths.DataDir);

	static BotFightConfig Cfg(int pct) => Bot.Fight with
	{
		WalkWeight = 0, NormalWeight = 0, SpecialWeight = 0, ThrowWeight = 0, JumpWeight = 0, WaitWeight = 1,
		MistakePct = 0, BlockPct = 0, IdleGuardPct = 0, AntiAirPct = 0, ThrowBreakPct = pct,
	};

	sealed record R(int Grabs, int Breaks, int Lands, int Twists, NewcomerBot Bot, ulong Hash, int Thrown);

	/// <summary>Row-1 fight; the opponent closes in and throws when free (<paramref name="oppThrows"/>) or only punches.</summary>
	static R Play(BotFightConfig cfg, uint seed = 11, int ticks = 1800, bool oppThrows = true, bool twist = false, bool adjacent = false)
	{
		var session = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5);
		Assert.True(session.Enter(session.Reachable()[0]).Ok, "enter row 1");
		var m = NodeFight.NewMatch(Content.Sources, session.Run, session, session.Node!.Fighter, ControlScheme.Kihon);
		if (twist) m.P2.Twist = new ElderTwist("t-grip", "elder-kappa", TwistEffect.UnbreakableThrows, 0, 0, "twist.t", null, null, null);
		InputBits oppThrow = InputBits.None, oppNormal = InputBits.None;
		foreach (var mv in m.P2.Moves)
		{
			if (mv.IsThrow && oppThrow == InputBits.None) oppThrow = mv.ThrowButtons;
			if (mv.IsNormal && oppNormal == InputBits.None) oppNormal = mv.Button;
		}
		var bot = new NewcomerBot(cfg, BotKit.From(m.P1), seed);
		int grabs = 0, breaks = 0, lands = 0, twists = 0, thrown = 0;
		m.Throw += (_, e) =>
		{
			if (e.Attacker != 1) return;
			if (e.Outcome == ThrowOutcome.Grab) grabs++; else if (e.Outcome == ThrowOutcome.Break) breaks++; else if (e.Outcome == ThrowOutcome.Land) lands++;
		};
		m.TwistTriggered += (_, _) => twists++;
		bool pressedLast = false;
		for (int t = 0; t < ticks && m.Phase != MatchPhase.Over; t++)
		{
			var input = bot.Next(AiView.Capture(m));
			var p2 = FighterInput.None;
			if (m.Phase == MatchPhase.Fighting && m.P2.State == FighterState.Idle && !m.P2.Airborne)
			{
				int dist = Math.Abs(m.P1.X - m.P2.X) / SimConfig.Scale;
				InputBits toward = m.P1.X < m.P2.X ? InputBits.Left : InputBits.Right;
				InputBits press = oppThrows ? oppThrow : oppNormal;
				p2 = new FighterInput(dist > 130 ? toward : pressedLast ? InputBits.None : press);
			}
			pressedLast = (p2.Bits & (oppThrows ? oppThrow : oppNormal)) != 0;
			m.Step(input, p2);
			if (m.P1.State == FighterState.Thrown) thrown++;
		}
		return new R(grabs, breaks, lands, twists, bot, m.StateHash(), thrown);
	}

	// C4/E12 (one roll per grab; 0 draws nothing): the RNG ends in the same state whether or not Ryo was ever grabbed.
	[Test]
	public static void Pct0_DrawsNoRng_WhetherOrNotGrabbed()
	{
		var grabbed = Play(Cfg(0), ticks: 900, oppThrows: true);
		var punched = Play(Cfg(0), ticks: 900, oppThrows: false);
		Assert.True(grabbed.Grabs > 0 && punched.Grabs == 0, "one fight has grabs, the other none");
		Assert.Equal(punched.Bot.RngState, grabbed.Bot.RngState, "pct 0: grabs consume no RNG");
		Assert.Equal(0, grabbed.Bot.Stats.ThrowBreakPresses, "pct 0: no press");
	}

	// C4: no break press when Ryo is never Thrown (hitstun, blockstun, knockdown are not grabs).
	[Test]
	public static void Pct100_NoPressWhenOnlyStruckNeverGrabbed()
	{
		var r = Play(Cfg(100), oppThrows: false);
		Assert.Equal(0, r.Grabs, "the opponent only strikes");
		Assert.Equal(0, r.Bot.Stats.ThrowBreakPresses, "no grab, no break press");
	}

	// C4: one press per grab, never a second roll for the same grab: under Elder Kappa's twist (refused press,
	// Thrown lasts the whole window) pct 100 still sends exactly one press per grab and the throws land.
	[Test]
	public static void Pct100_ElderTwist_OnePressPerGrab_AndNoRepeat()
	{
		var r = Play(Cfg(100), twist: true);
		Assert.True(r.Grabs >= 3, $"grabs ({r.Grabs})");
		Assert.Equal(r.Grabs, r.Bot.Stats.ThrowBreakPresses, "one press per grab even when refused");
		Assert.Equal(0, r.Breaks, "YOK-90: the elder's throws cannot be broken");
		Assert.True(r.Twists >= r.Grabs, "the twist refused the presses");
	}

	// Determinism: same seed twice = same hash and RNG state; breaking vs not breaking differ.
	[Test]
	public static void Determinism_SameSeedSameHash_DifferentPctDifferentHash()
	{
		var a = Play(Cfg(50), seed: 7);
		var b = Play(Cfg(50), seed: 7);
		Assert.Equal(a.Hash, b.Hash, "same seed, same StateHash");
		Assert.Equal(a.Bot.RngState, b.Bot.RngState, "same RNG");
		var z = Play(Cfg(0), seed: 7);
		var f = Play(Cfg(100), seed: 7);
		Assert.True(z.Hash != f.Hash, "never breaking and always breaking end differently");
	}

	// C4: the press must register wherever the grab falls in Ryo's own plan (normals held / released / thrown).
	[Test]
	public static void Pct100_BreaksEveryGrab_WhateverRyoWasDoing()
	{
		var busy = Cfg(100) with { WaitWeight = 0, NormalWeight = 1, ThrowWeight = 1, ThrowRange = 4000, DecideEvery = 2, PokeRange = 4000 };
		foreach (uint seed in new uint[] { 1, 2, 3, 4, 5 })
		{
			var r = Play(busy, seed: seed, ticks: 1500);
			Assert.True(r.Grabs > 0, $"seed {seed}: grabs ({r.Grabs})");
			Assert.Equal(0, r.Lands, $"seed {seed}: no throw lands at pct 100 ({r.Lands} of {r.Grabs})");
		}
	}

	// A grab right at the start of a fight (opponent adjacent, throw on the first step) is still broken.
	[Test]
	public static void Pct100_FirstTickGrab_Broken()
	{
		var session = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5);
		Assert.True(session.Enter(session.Reachable()[0]).Ok, "enter row 1");
		var m = NodeFight.NewMatch(Content.Sources, session.Run, session, session.Node!.Fighter, ControlScheme.Kihon);
		InputBits oppThrow = m.P2.Moves.First(x => x.IsThrow).ThrowButtons;
		m.P2.X = m.P1.X + 100 * SimConfig.Scale;
		var bot = new NewcomerBot(Cfg(100), BotKit.From(m.P1), 3);
		int breaks = 0, lands = 0;
		m.Throw += (_, e) => { if (e.Outcome == ThrowOutcome.Break) breaks++; if (e.Outcome == ThrowOutcome.Land) lands++; };
		for (int t = 0; t < 200 && m.Phase != MatchPhase.Over; t++)
		{
			var input = bot.Next(AiView.Capture(m));
			m.Step(input, t == 0 ? new FighterInput(oppThrow) : FighterInput.None);
		}
		Assert.Equal(0, lands, "the first-tick throw does not land");
		Assert.True(breaks >= 1, $"it is broken ({breaks}, presses {bot.Stats.ThrowBreakPresses})");
	}

	static string BaseJson => File.ReadAllText(Path.Combine(ContentPaths.DataDir, "harness", BotConfig.DefaultId + ".json"));
	static string LevelsJson => File.ReadAllText(Path.Combine(ContentPaths.DataDir, "harness", BotLevels.DefaultFile));

	static string Levels(string fightBody) => $$"""
		{ "kind": "harness_bot_levels", "id": "tb-levels", "base": "newcomer-kihon-bot", "levels": [
		  { "level": "a", "bot_id": "tb-a", "status": "proposed" },
		  { "level": "b", "bot_id": "tb-b", "status": "proposed", "fight": { {{fightBody}} } } ] }
		""";

	// Precedence: a level's value beats the base; an override of 0 beats a base of 15; no override = base.
	[Test]
	public static void Levels_OverridePrecedence_ZeroBeatsBase_AbsentIsBase()
	{
		var lv = BotLevels.Parse(Levels("\"throw_break_pct\": 0"), id => BaseJson);
		Assert.Equal(15, lv["a"].Fight.ThrowBreakPct, "no override = the base's 15");
		Assert.Equal(0, lv["b"].Fight.ThrowBreakPct, "override 0 beats base 15");
		var noKey = BaseJson.Replace("\"throw_break_pct\": 15,", "");
		var lv2 = BotLevels.Parse(Levels("\"throw_break_pct\": 30"), id => noKey);
		Assert.Equal(0, lv2["a"].Fight.ThrowBreakPct, "base absent, no override = 0");
		Assert.Equal(30, lv2["b"].Fight.ThrowBreakPct, "override over an absent base");
	}

	// Strict validation: out of range / non-integer values are a FormatException, in the base and in a level.
	[Test]
	public static void Validation_OutOfRangeOrNonInteger_Rejected()
	{
		foreach (var bad in new[] { "101", "-1", "15.5", "\"15\"", "true", "1e2" })
		{
			bool baseThrew = false, levelThrew = false;
			try { BotConfig.Parse(BaseJson.Replace("\"throw_break_pct\": 15", "\"throw_break_pct\": " + bad)); }
			catch (Exception e) when (e is FormatException) { baseThrew = true; }
			try { BotLevels.Parse(Levels("\"throw_break_pct\": " + bad), id => BaseJson); }
			catch (Exception e) when (e is FormatException) { levelThrew = true; }
			Assert.True(baseThrew, $"base file: throw_break_pct {bad} rejected");
			Assert.True(levelThrew, $"level override: throw_break_pct {bad} rejected");
		}
		foreach (var ok in new[] { "0", "100" })
			Assert.Equal(int.Parse(ok), BotLevels.Parse(Levels("\"throw_break_pct\": " + ok), id => BaseJson)["b"].Fight.ThrowBreakPct, $"boundary {ok} accepted");
	}

	// Report: pct 0 prints none of the YOK-184 keys; nonzero prints the top-level key.
	[Test]
	public static void Report_Pct0_NoKeys_NonzeroTopLevelKey()
	{
		var legacy = AiGuardTests.LegacyOpponent(Content);
		var b0 = Bot with { Fight = Bot.Fight with { ThrowBreakPct = 0 } };
		string j0 = HarnessReport.ToJson(RunHarness.PlayBatch(legacy, b0, 1, 2));
		Assert.True(!j0.Contains("\"throw_break_pct\"") && !j0.Contains("\"throw_break_presses\""), "pct 0: no YOK-184 key anywhere");
		var b45 = Bot with { Fight = Bot.Fight with { ThrowBreakPct = 45 } };
		string j45 = HarnessReport.ToJson(RunHarness.PlayBatch(legacy, b45, 1, 2));
		Assert.True(j45.Contains("\"throw_break_pct\": 45") || j45.Contains("\"throw_break_pct\":45"), "pct 45: top-level key");
	}
}
