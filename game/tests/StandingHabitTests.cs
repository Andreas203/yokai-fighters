using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Replay;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-177 (Y2, P6): a habit whose trigger is a standing condition (the apprentice's <c>at_close_range</c>, the
/// Tanuki's <c>opponent_blocking</c>) rolls when the condition becomes true on the AI's view and again every
/// <see cref="ProfileAi.StandingHabitRearmTicks"/> while it holds. Before YOK-177 it never rolled. Event-triggered
/// habits are untouched. Real profiles on the real Kitsune kit (the apprentice plays it); the Tanuki on Ryo's kit, as T0 builds it
/// (with no copy locked).
/// </summary>
public static class StandingHabitTests
{
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static ReplayCatalog? _catalog;
	static ReplayCatalog Catalog => _catalog ??= ReplayCatalog.FromRepo(RepoRoot);
	static BehaviourProfile Profile(string id) => BehaviourProfile.LoadFile(ContentPaths.Data("profiles") + "/" + id + ".json");

	static Match NewMatch(string opponent = "kitsune", int? jumpPunishPct = null)
	{
		FightScene.Run = null;
		return NodeFight.NewMatch(FightScene.Sources, null, null, opponent, FightScene.DefaultScheme,
			shape: jumpPunishPct is null ? null : c => c with { P2JumpPunishPct = jumpPunishPct });
	}

	sealed class Tally
	{
		public int Rolls, Fired, Starts;
		public int MinGap = int.MaxValue;
		public readonly List<AiDecision> Log = new();
		public uint Hash = 2166136261u;
	}

	/// <summary>Plays <paramref name="ticks"/> ticks (rounds restarted with a new AI as AiHarness does); counts the habit's
	/// rolls and fires, and the AI's starts of <paramref name="startMove"/>.</summary>
	static Tally Play(BehaviourProfile p, Func<Match, FighterInput> p1, int ticks, uint seed, string startMove, bool standing = true,
		AiFightTier tier = AiFightTier.Standard, string opponent = "kitsune")
	{
		var m = NewMatch(opponent);
		var t = new Tally();
		int round = 0;
		ProfileAi NewAi() => new(p, 1, AiLoadout.From(m.P2), seed + (uint)round * 7919u, tier: tier, standingHabits: standing) { Log = t.Log };
		var ai = NewAi();
		int lastRoll = -1, from = 0;
		FighterState prevState = m.P2.State;
		string? prevMove = null;
		for (int i = 0; i < ticks; i++)
		{
			var b = ai.Next(AiView.Capture(m));
			m.Step(p1(m), b);
			t.Hash = (t.Hash ^ (uint)(m.StateHash() & 0xFFFFFFFF)) * 16777619u;
			var v = AiView.Capture(m).Fighter(1);
			if (v.State == FighterState.Attack && v.MoveId == startMove && (prevState != FighterState.Attack || prevMove != startMove)) t.Starts++;
			prevState = v.State; prevMove = v.MoveId;
			for (; from < t.Log.Count; from++)
			{
				var d = t.Log[from];
				if (!d.Kind.StartsWith("habit")) continue;
				t.Rolls++;
				if (d.Kind == "habit") t.Fired++;
				if (lastRoll >= 0) t.MinGap = Math.Min(t.MinGap, d.ViewTick - lastRoll);
				lastRoll = d.ViewTick;
			}
			if (m.Phase == MatchPhase.Over)
			{
				m.Reset();
				round++;
				ai = NewAi();
				lastRoll = -1;
				prevState = m.P2.State; prevMove = null;
			}
		}
		return t;
	}

	static FighterInput Idle(Match m) => FighterInput.None;

	/// <summary>Ryo holds back (stand guard) whenever he can: every Tanuki strike puts him in blockstun.</summary>
	static FighterInput Turtle(Match m) => new(m.P1.X < m.P2.X ? InputBits.Left : InputBits.Right);

	static Func<Match, FighterInput> RandomP1(uint seed)
	{
		var rng = new SimRng(seed);
		InputBits held = InputBits.None;
		int left = 0;
		return _ => AiHarness.RandomInput(rng, ref held, ref left);
	}

	static void InBand(Tally t, int pct, string who)
	{
		double rate = t.Rolls == 0 ? 0 : 100.0 * t.Fired / t.Rolls;
		double sd = Math.Sqrt(pct * (100.0 - pct) / Math.Max(1, t.Rolls)); // binomial, in percent points
		Assert.True(Math.Abs(rate - pct) <= 3.5 * sd, $"{who}: fired {t.Fired}/{t.Rolls} = {rate:0.0}%, stated {pct}% (±{3.5 * sd:0.0})");
	}

	[Test]
	public static void Apprentice_HeavyPunchUpClose_FiresAtItsStatedRate()
	{
		var p = Profile("apprentice");
		Assert.True(p.Habit.When == Observation.AtCloseRange, "the apprentice's habit is the standing at_close_range");
		var t = Play(p, Idle, 30000, 177, "kitsune-heavy-punch");
		Assert.True(t.Rolls >= 150, $"the habit rolls while Ryo stands in her face ({t.Rolls} rolls)");
		GD.Print($"YOK-177 apprentice: {t.Fired}/{t.Rolls} fired, {t.Starts} heavy punches started, min gap {t.MinGap}");
		InBand(t, p.Habit.ChancePct, "apprentice");
		Assert.True(t.MinGap >= ProfileAi.StandingHabitRearmTicks, $"never re-rolled within the re-arm period (min gap {t.MinGap})");
		// The fired habit comes out (the AI waits out its own move first); she has no other heavy punch with Ryo grounded.
		Assert.True(t.Starts >= t.Fired * 9 / 10 && t.Starts <= t.Fired, $"heavy punches started {t.Starts} of {t.Fired} fired");
	}

	[Test]
	public static void Tanuki_ThrowsAtATurtle_FiresAtItsStatedRate()
	{
		var p = Profile("tanuki-boss");
		Assert.True(p.Habit.When == Observation.OpponentBlocking, "the Tanuki's habit is the standing opponent_blocking");
		var t = Play(p, Turtle, 45000, 177, "ryo-generic-throw", tier: AiFightTier.Tanuki, opponent: "ryo");
		Assert.True(t.Rolls >= 100, $"the habit rolls while Ryo keeps blocking ({t.Rolls} rolls)");
		GD.Print($"YOK-177 tanuki: {t.Fired}/{t.Rolls} fired, {t.Starts} throws started");
		InBand(t, p.Habit.ChancePct, "tanuki-boss");
		Assert.True(t.MinGap >= ProfileAi.StandingHabitRearmTicks, $"never re-rolled within the re-arm period (min gap {t.MinGap})");
		var throws = t.Log.Count(d => d.Action == "throw" && d.Kind is "neutral" or "react");
		Assert.True(t.Starts >= t.Fired * 9 / 10, $"the throws come out: {t.Starts} throws started, {t.Fired} habit fires, {throws} other throw picks");
	}

	[Test]
	public static void OldRule_NeverRollsAStandingHabit()
	{
		Assert.Equal(0, Play(Profile("apprentice"), Idle, 6000, 177, "kitsune-heavy-punch", standing: false).Rolls, "apprentice, old rule");
		Assert.Equal(0, Play(Profile("tanuki-boss"), Turtle, 6000, 177, "ryo-generic-throw", false, AiFightTier.Tanuki, "ryo").Rolls, "tanuki, old rule");
	}

	[Test]
	public static void EventHabits_AreUnchanged()
	{
		int checkedCount = 0;
		foreach (var p in BehaviourProfile.LoadDirectory(ContentPaths.Data("profiles")))
		{
			if (!BehaviourProfile.IsEvent(p.Habit.When)) continue;
			var on = Play(p, RandomP1(5), 5000, 9, "kitsune-throw");
			var off = Play(p, RandomP1(5), 5000, 9, "kitsune-throw", standing: false);
			Assert.Equal(off.Hash, on.Hash, $"{p.Id}: the fight is the same with the YOK-177 rule");
			Assert.True(on.Log.SequenceEqual(off.Log), $"{p.Id}: the same decisions");
			checkedCount++;
		}
		Assert.True(checkedCount >= 6, $"every event-habit profile checked ({checkedCount})");
	}

	[Test]
	public static void Deterministic_SameSeedSameRolls()
	{
		var p = Profile("apprentice");
		var a = Play(p, RandomP1(3), 8000, 41, "kitsune-heavy-punch");
		var b = Play(p, RandomP1(3), 8000, 41, "kitsune-heavy-punch");
		Assert.Equal(a.Hash, b.Hash, "same hash");
		Assert.True(a.Log.SequenceEqual(b.Log), "same decisions");
		Assert.True(a.Rolls > 0, "with habit rolls in it");
	}

	static ReplayRecording Record(BehaviourProfile p, bool standing, uint seed, int steps)
	{
		var m = NewMatch();
		var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), seed, standingHabits: standing);
		var rec = ReplayRecorder.Begin(m, ai, seed, null);
		for (int t = 0; t < steps && m.Phase != MatchPhase.Over; t++)
		{
			var a = FighterInput.None;
			var b = ai.Next(AiView.Capture(m));
			m.Step(a, b);
			rec.Record(a, b, m.StateHash());
		}
		return rec.Finish(m);
	}

	[Test]
	public static void Replays_NewAndOldRecordings()
	{
		var p = Profile("apprentice");
		var rec = Record(p, true, 177, 1500);
		Assert.True(rec.Header.Ai!.StandingHabits, "a new recording says the AI rolls standing habits");
		var r = Replayer.Run(ReplayRecording.FromBytes(rec.ToBytes()), Catalog);
		Assert.True(r.Ok, $"a new recording replays: {r}");
		var stripped = ReplayRecording.FromBytes(rec.ToBytes());
		stripped.Header.Ai!.StandingHabits = false;
		Assert.True(!Replayer.Run(stripped, Catalog).Ok, "read with the old rule, it does not match (the habit rolled)");

		// A recording as made before YOK-177: the field is not written, and it replays with the old rule.
		var old = Record(p, false, 177, 1500);
		string header = System.Text.Json.JsonSerializer.Serialize(old.Header);
		Assert.True(!header.Contains("StandingHabits"), $"the field is not written for the old rule: {header}");
		Assert.True(Replayer.Run(ReplayRecording.FromBytes(old.ToBytes()), Catalog).Ok, "an old-style recording replays");
	}

	static bool IsJumpAnswer(AiDecision d) => d.Kind == "react" && d.Trigger == "opponent_jumping" && d.Action.StartsWith("normal:");

	/// <summary>
	/// Code-critic YOK-177 (Y4, YOK-165: the Tanuki punishes every seen jump): Ryo blocks until the throw habit fires, then
	/// jumps out. The running habit plan must not stop the jump being answered: one jump-punish roll per jump at the
	/// Tanuki's 100, and the anti-air comes out.
	/// </summary>
	[Test]
	public static void Tanuki_JumpOverItsThrowHabit_IsStillAnswered()
	{
		var p = Profile("tanuki-boss");
		var m = NewMatch("ryo", jumpPunishPct: 100);
		var log = new List<AiDecision>();
		var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), 1771, tier: AiFightTier.Tanuki) { Log = log };
		bool jumping = false, wasAir = false;
		int seenHabits = 0, jumps = 0, overHabit = 0, answered = 0, started = 0, jumpTick = -1, from = 0;
		string? answer = null;
		for (int i = 0; i < 40000 && m.Phase == MatchPhase.Fighting; i++)
		{
			var b = ai.Next(AiView.Capture(m));
			int fired = log.Count(d => d.Kind == "habit");
			if (fired > seenHabits) { seenHabits = fired; jumping = true; }
			var back = m.P1.X < m.P2.X ? InputBits.Left : InputBits.Right;
			var a = new FighterInput(jumping && m.P1.Actionable ? InputBits.Up : back);
			m.Step(a, b);
			if (m.P1.Airborne && !wasAir)
			{
				jumps++; jumpTick = m.Tick; jumping = false; answer = null;
				if (ai.HabitPlanRunning) overHabit++;
			}
			for (; from < log.Count; from++)
				if (jumpTick >= 0 && IsJumpAnswer(log[from]) && log[from].ViewTick >= jumpTick) { answered++; answer = log[from].Action[7..]; }
			var v = AiView.Capture(m).Fighter(1);
			if (answer is not null && v.State == FighterState.Attack && v.MoveId == answer) { started++; answer = null; }
			if (!m.P1.Airborne && wasAir) jumpTick = -1;
			wasAir = m.P1.Airborne;
		}
		GD.Print($"YOK-177 tanuki jump-over-habit: {jumps} jumps, {overHabit} while the habit ran, {answered} answered, {started} answers started");
		Assert.True(overHabit >= 5, $"jumps out of a running throw habit ({overHabit} of {jumps})");
		Assert.True(answered >= jumps - 1, $"every jump answered at the Tanuki's 100 ({answered} of {jumps})");
		Assert.True(started * 10 >= answered * 8, $"the anti-air comes out ({started} of {answered})");
	}

	/// <summary>
	/// Code-critic YOK-177 advisory: a standing habit does not overwrite a held jump answer (YOK-165) while it waits or
	/// presses. Ryo jumps up in the apprentice's face; between an answer and its start (or Ryo landing) no habit rolls.
	/// </summary>
	[Test]
	public static void Apprentice_HabitDoesNotOverwriteAHeldJumpAnswer()
	{
		var p = Profile("apprentice");
		var m = NewMatch("kitsune", jumpPunishPct: 100);
		var log = new List<AiDecision>();
		var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), 1772) { Log = log };
		int from = 0, answers = 0, habitsWhilePending = 0, round = 0;
		string? pending = null;
		for (int i = 0; i < 30000; i++)
		{
			var b = ai.Next(AiView.Capture(m));
			int gap = Math.Abs(m.P2.X - m.P1.X) / SimConfig.Scale;
			var fwd = m.P1.X < m.P2.X ? InputBits.Right : InputBits.Left;
			var a = new FighterInput(!m.P1.Actionable ? InputBits.None : gap >= ProfileAi.CloseRange ? fwd : m.Tick % 45 == 0 ? InputBits.Up : InputBits.None);
			m.Step(a, b);
			for (; from < log.Count; from++)
			{
				var d = log[from];
				if (IsJumpAnswer(d)) { answers++; pending = d.Action[7..]; }
				else if (d.Kind.StartsWith("habit") && pending is not null) habitsWhilePending++;
			}
			var v = AiView.Capture(m).Fighter(1);
			if (pending is not null && ((v.State == FighterState.Attack && v.MoveId == pending) || !m.P1.Airborne)) pending = null;
			if (m.Phase == MatchPhase.Over)
			{
				m.Reset(); round++;
				ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), 1772 + (uint)round) { Log = log };
				pending = null;
			}
		}
		GD.Print($"YOK-177 apprentice held answers: {answers} answers, {log.Count(d => d.Kind == "habit")} habits fired, {habitsWhilePending} while pending");
		Assert.True(answers >= 30, $"jumps answered in her face ({answers})");
		Assert.True(log.Any(d => d.Kind == "habit"), "and her habit fires in the same fight");
		Assert.Equal(0, habitsWhilePending, "no habit roll while a jump answer is still to come out");
	}
}
