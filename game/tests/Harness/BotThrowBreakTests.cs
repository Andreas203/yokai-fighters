using System;
using System.Collections.Generic;
using YokaiFighters.Ai;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-184 (C4, Y6, R5): the newcomer bot answers <c>throw_break_pct</c> of the grabs on Ryo with the throw-break press
/// inside the C4 window. Real row-1 fight against a scripted opponent that walks in and throws whenever it is free;
/// Ryo only waits (no guard, block, anti-air or mistake), so every break seen comes from the new roll.
/// </summary>
public static class BotThrowBreakTests
{
	static HarnessContent? _content;
	static HarnessContent Content => _content ??= HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static BotConfig Bot => BotConfig.LoadDefault(ContentPaths.DataDir);

	static BotFightConfig Waiter(int breakPct) => Bot.Fight with
	{
		WalkWeight = 0, NormalWeight = 0, SpecialWeight = 0, ThrowWeight = 0, JumpWeight = 0, WaitWeight = 1,
		MistakePct = 0, BlockPct = 0, IdleGuardPct = 0, AntiAirPct = 0, ThrowBreakPct = breakPct,
	};

	sealed record Result(int Grabs, int Breaks, int Lands, NewcomerBot Bot, List<string> Trace);

	/// <summary>The opponent walks forward until close (bodies stop 120 units apart), then presses its throw buttons, whenever it is idle and grounded.</summary>
	static Result Play(int breakPct, uint seed = 11, int ticks = 2400)
	{
		var session = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5);
		Assert.True(session.Enter(session.Reachable()[0]).Ok, "enter row 1");
		var m = NodeFight.NewMatch(Content.Sources, session.Run, session, session.Node!.Fighter, ControlScheme.Kihon);
		InputBits oppThrow = InputBits.None;
		foreach (var mv in m.P2.Moves) if (mv.IsThrow) { oppThrow = mv.ThrowButtons; break; }
		Assert.True(oppThrow != InputBits.None, "the row-1 opponent has a throw");
		var bot = new NewcomerBot(Waiter(breakPct), BotKit.From(m.P1), seed);
		int grabs = 0, breaks = 0, lands = 0;
		var trace = new List<string>();
		m.Throw += (_, e) =>
		{
			if (e.Attacker != 1) return;
			trace.Add($"{m.Tick}:{e.Outcome}");
			switch (e.Outcome)
			{
				case ThrowOutcome.Grab: grabs++; break;
				case ThrowOutcome.Break: breaks++; break;
				case ThrowOutcome.Land: lands++; break;
			}
		};
		bool pressedLast = false;
		for (int t = 0; t < ticks && m.Phase != MatchPhase.Over; t++)
		{
			var input = bot.Next(AiView.Capture(m));
			trace.Add(((int)input.Bits).ToString());
			var p2 = FighterInput.None;
			if (m.Phase == MatchPhase.Fighting && m.P2.State == FighterState.Idle && !m.P2.Airborne)
			{
				int dist = Math.Abs(m.P1.X - m.P2.X) / SimConfig.Scale;
				InputBits toward = m.P1.X < m.P2.X ? InputBits.Left : InputBits.Right;
				p2 = new FighterInput(dist > 130 ? toward : pressedLast ? InputBits.None : oppThrow);
			}
			pressedLast = (p2.Bits & oppThrow) != 0;
			m.Step(input, p2);
		}
		return new Result(grabs, breaks, lands, bot, trace);
	}

	[Test]
	public static void ThrowBreak_Config_ReadFromDataAndLevels_AbsentIsZero_OutOfRangeRejected()
	{
		Assert.Equal(15, Bot.Fight.ThrowBreakPct, "data/harness/newcomer-kihon-bot.json proposes throw_break_pct 15");
		string json = System.IO.File.ReadAllText(System.IO.Path.Combine(ContentPaths.DataDir, "harness", BotConfig.DefaultId + ".json"));
		Assert.True(json.Contains("\"throw_break_pct\": 15,"), "the default file names it");
		Assert.Equal(0, BotConfig.Parse(json.Replace("\"throw_break_pct\": 15,", "")).Fight.ThrowBreakPct, "absent = 0, the pre-YOK-184 bot");
		foreach (var bad in new[] { "101", "-1" })
		{
			bool threw = false;
			try { BotConfig.Parse(json.Replace("\"throw_break_pct\": 15", "\"throw_break_pct\": " + bad)); }
			catch (FormatException) { threw = true; }
			Assert.True(threw, $"throw_break_pct {bad} is rejected");
		}
		var levels = BotLevels.LoadDefault(ContentPaths.DataDir);
		Assert.Equal(15, levels["newcomer"].Fight.ThrowBreakPct, "newcomer = the base file's value");
		Assert.Equal(30, levels["intermediate"].Fight.ThrowBreakPct, "intermediate overrides to 30");
		Assert.Equal(45, levels["skilled"].Fight.ThrowBreakPct, "skilled overrides to 45");
	}

	[Test]
	public static void ThrowBreak_0_NeverBreaks()
	{
		var r = Play(0);
		Assert.True(r.Grabs >= 3, $"the opponent grabs Ryo several times ({r.Grabs})");
		Assert.Equal(0, r.Breaks, "no break at 0");
		Assert.Equal(0, r.Bot.Stats.ThrowBreakPresses, "no break press at 0");
		Assert.True(r.Lands >= 3, $"the throws land ({r.Lands})");
	}

	[Test]
	public static void ThrowBreak_100_BreaksEveryThrow()
	{
		var r = Play(100);
		Assert.True(r.Grabs >= 5, $"the opponent grabs Ryo several times ({r.Grabs})");
		Assert.Equal(r.Grabs, r.Breaks, "every grab is broken (C4 window)");
		Assert.Equal(0, r.Lands, "no throw lands");
		Assert.Equal(r.Grabs, r.Bot.Stats.ThrowBreakPresses, "one press per grab");
	}

	/// <summary>
	/// Code Critic advisory (C4, E12 edge): Ryo's own throw press goes out on the very tick he is grabbed, so the throw
	/// buttons are down the tick before the window opens. The break must still be a fresh press (release, then press on
	/// the window's second frame), and must break. Ryo decides every 600 ticks and only throws, so he presses throw at
	/// steps 0 and 600 only; the opponent walks into reach, stands, and throws once at a scanned step so that one of the
	/// scanned timings grabs Ryo on step 600.
	/// </summary>
	[Test]
	public static void ThrowBreak_100_BreaksWhenRyoPressedThrowOnTheGrabTick()
	{
		var cfg = Waiter(100) with { WaitWeight = 0, ThrowWeight = 1, ThrowRange = 4000, DecideEvery = 600 };
		int coincident = 0;
		for (int pressAt = 585; pressAt <= 600; pressAt++)
		{
			var session = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5);
			Assert.True(session.Enter(session.Reachable()[0]).Ok, "enter row 1");
			var m = NodeFight.NewMatch(Content.Sources, session.Run, session, session.Node!.Fighter, ControlScheme.Kihon);
			InputBits oppThrow = InputBits.None;
			foreach (var mv in m.P2.Moves) if (mv.IsThrow) { oppThrow = mv.ThrowButtons; break; }
			var kit = BotKit.From(m.P1);
			var bot = new NewcomerBot(cfg, kit, 3);
			InputBits sent = InputBits.None;
			bool watching = false, resolved = false;
			m.Throw += (_, e) =>
			{
				if (e.Attacker != 1) return;
				if (e.Outcome == ThrowOutcome.Grab && (sent & kit.Throw.Bits) == kit.Throw.Bits) { watching = true; coincident++; }
				else if (watching && e.Outcome is ThrowOutcome.Break or ThrowOutcome.Land)
				{
					Assert.Equal(ThrowOutcome.Break, e.Outcome, $"opponent press at step {pressAt}: grabbed on the tick Ryo pressed throw, still broken");
					watching = false;
					resolved = true;
				}
			};
			for (int t = 0; t < 700 && m.Phase != MatchPhase.Over; t++)
			{
				var input = bot.Next(AiView.Capture(m));
				var p2 = FighterInput.None;
				if (t == pressAt) p2 = new FighterInput(oppThrow);
				else if (t < pressAt && m.P2.State == FighterState.Idle && Math.Abs(m.P1.X - m.P2.X) / SimConfig.Scale > 130)
					p2 = new FighterInput(m.P1.X < m.P2.X ? InputBits.Left : InputBits.Right);
				sent = input.Bits;
				m.Step(input, p2);
			}
			Assert.True(!watching, $"opponent press at step {pressAt}: the coincident grab resolved");
			if (watching || resolved) Assert.Equal(1, bot.Stats.ThrowBreakPresses, $"opponent press at step {pressAt}: one break press");
		}
		Assert.True(coincident > 0, $"one scanned timing grabs Ryo on the tick he pressed throw ({coincident})");
	}

	[Test]
	public static void ThrowBreak_FixedSeed_Repeatable_AndRollsPerGrab()
	{
		var a = Play(50, seed: 11);
		var b = Play(50, seed: 11);
		Assert.Equal(string.Join(",", a.Trace), string.Join(",", b.Trace), "same seed, same inputs and throw outcomes");
		Assert.Equal(a.Bot.RngState, b.Bot.RngState, "same RNG state");
		Assert.True(a.Breaks > 0 && a.Lands > 0, $"at 50 some grabs break and some land ({a.Breaks} broken, {a.Lands} landed of {a.Grabs})");
		Assert.Equal(a.Breaks, a.Bot.Stats.ThrowBreakPresses, "every press broke (no elder twist here)");
	}
}
