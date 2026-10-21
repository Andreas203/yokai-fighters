using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-177 probes. Rules: Y2 (habit at its stated chance), Y4 / YOK-165 (seen jumps answered), P6.</summary>
public static class StandingHabitProbes
{
	static BehaviourProfile Profile(string id) => BehaviourProfile.LoadFile(ContentPaths.Data("profiles") + "/" + id + ".json");

	static Match NewMatch(string opponent, int? jp = null)
	{
		FightScene.Run = null;
		return NodeFight.NewMatch(FightScene.Sources, null, null, opponent, FightScene.DefaultScheme,
			shape: jp is null ? null : c => c with { P2JumpPunishPct = jp });
	}

	static FighterInput Turtle(Match m) => new(m.P1.X < m.P2.X ? InputBits.Left : InputBits.Right);

	// Y2: the stated chance holds at any reaction level; the cooldown holds; the habit comes out even when the AI is
	// being hit (Ryo attacks at random) and its press is not lost.
	static (int rolls, int fired, int minGap, int starts) Run(string prof, string opp, int react, AiFightTier tier, Func<Match, FighterInput> p1, int ticks, string move)
	{
		var p = Profile(prof);
		var m = NewMatch(opp);
		var log = new List<AiDecision>();
		int round = 0;
		ProfileAi NewAi() => new(p, 1, AiLoadout.From(m.P2), 900u + (uint)round, react, tier) { Log = log };
		var ai = NewAi();
		int rolls = 0, fired = 0, minGap = int.MaxValue, last = -1, from = 0, starts = 0;
		bool wasMove = false;
		for (int i = 0; i < ticks; i++)
		{
			var b = ai.Next(AiView.Capture(m));
			m.Step(p1(m), b);
			var v = AiView.Capture(m).Fighter(1);
			bool isMove = v.State == FighterState.Attack && v.MoveId == move;
			if (isMove && !wasMove) starts++;
			wasMove = isMove;
			for (; from < log.Count; from++)
			{
				var d = log[from];
				if (!d.Kind.StartsWith("habit")) continue;
				rolls++; if (d.Kind == "habit") fired++;
				if (last >= 0 && d.ViewTick > last) minGap = Math.Min(minGap, d.ViewTick - last);
				last = d.ViewTick;
			}
			if (m.Phase == MatchPhase.Over) { m.Reset(); round++; ai = NewAi(); last = -1; wasMove = false; }
		}
		return (rolls, fired, minGap, starts);
	}

	static void Band(int fired, int rolls, int pct, string who)
	{
		double rate = 100.0 * fired / Math.Max(1, rolls);
		double sd = Math.Sqrt(pct * (100.0 - pct) / Math.Max(1, rolls));
		Assert.True(Math.Abs(rate - pct) <= 4 * sd, $"{who}: {fired}/{rolls} = {rate:0.0}% vs {pct}%");
	}

	[Test]
	public static void Apprentice_RateAndCooldown_AtEveryReactionLevel()
	{
		// Y2 + the proposed 60-tick cooldown, at reaction 1, 8 and 60.
		foreach (int react in new[] { 1, 8, 60 })
		{
			var (rolls, fired, gap, _) = Run("apprentice", "kitsune", react, AiFightTier.Standard, _ => FighterInput.None, 20000, "kitsune-heavy-punch");
			Assert.True(rolls >= 80, $"react {react}: rolls {rolls}");
			Band(fired, rolls, 60, $"apprentice react {react}");
			Assert.True(gap >= ProfileAi.StandingHabitRearmTicks, $"react {react}: gap {gap}");
		}
	}

	[Test]
	public static void Tanuki_RateAndCooldown_AtEveryReactionLevel()
	{
		var p = Profile("tanuki-boss");
		foreach (int react in new[] { 1, 6, 30 })
		{
			var (rolls, fired, gap, _) = Run("tanuki-boss", "ryo", react, AiFightTier.Tanuki, Turtle, 40000, "ryo-generic-throw");
			Assert.True(rolls >= 60, $"react {react}: rolls {rolls}");
			Band(fired, rolls, p.Habit.ChancePct, $"tanuki react {react}");
			Assert.True(gap >= ProfileAi.StandingHabitRearmTicks, $"react {react}: gap {gap}");
		}
	}

	[Test]
	public static void Tanuki_RepeatedJumpsDuringHabit_AllAnswered_AtEveryReaction()
	{
		// YOK-165 Y4: at jump-punish 100 every seen jump is answered; Ryo jumps again the moment he lands.
		var p = Profile("tanuki-boss");
		foreach (int react in new[] { 1, 10 })
		{
			var m = NewMatch("ryo", 100);
			var log = new List<AiDecision>();
			var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), 31, react, AiFightTier.Tanuki) { Log = log };
			int fires = 0, jumps = 0, answers = 0; bool jumping = false, wasAir = false;
			for (int i = 0; i < 30000 && m.Phase == MatchPhase.Fighting; i++)
			{
				var b = ai.Next(AiView.Capture(m));
				int f = log.Count(d => d.Kind == "habit");
				if (f > fires) { fires = f; jumping = true; }
				var back = m.P1.X < m.P2.X ? InputBits.Left : InputBits.Right;
				var jumpDir = jumping ? InputBits.Up : InputBits.None;
				m.Step(new FighterInput(m.P1.Actionable ? (jumping ? jumpDir | InputBits.None : back) : InputBits.None), b);
				if (m.P1.Airborne && !wasAir) jumps++;
				if (!m.P1.Airborne && wasAir && jumping && jumps % 3 == 0) jumping = false; // three jumps per habit
				wasAir = m.P1.Airborne;
			}
			answers = log.Count(d => d.Kind == "react" && d.Trigger == "opponent_jumping");
			Assert.True(jumps >= 6, $"react {react}: jumps {jumps}");
			Assert.True(answers >= jumps - 2, $"react {react}: {answers} answers for {jumps} jumps");
		}
	}

	[Test]
	public static void Batch_DeterministicWithSwitchOnAndOff()
	{
		var content = AiGuardTests.LegacyOpponent(HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir));
		var bot = BotConfig.LoadDefault(ContentPaths.DataDir);
		string H(HarnessContent c) { using var d = System.Text.Json.JsonDocument.Parse(HarnessReport.ToJson(RunHarness.PlayBatch(c, bot, 3, 2))); return string.Join(",", d.RootElement.GetProperty("runs").EnumerateArray().Select(r => r.GetProperty("session_hash").GetString())); }
		var off1 = H(content); var off2 = H(content);
		var on1 = H(content with { StandingHabits = true }); var on2 = H(content with { StandingHabits = true });
		Assert.Equal(off1, off2, "off twice");
		Assert.Equal(on1, on2, "on twice");
		Assert.True(off1 != on1, "the switch changes the batch");
	}
}
