using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Replay;
using YokaiFighters.Sim;
using YokaiFighters.Tutorial;

namespace YokaiFighters.Tests;

/// <summary>YOK-92 probes: try to break the Y6 guided fight. Expected values come from rules.md (Y6, Y4, P6, C3-C6, X1, F3) and the branch's stated pass conditions.</summary>
public static class TutorialProbes
{
	const InputBits LP = InputBits.LightPunch, LK = InputBits.LightKick;
	const InputBits THROW = LP | LK;
	static readonly TutorialOptions Fast = new() { MinPromptTicks = 10 };

	static T OnFixtures<T>(Func<T> body)
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		try { return body(); } finally { FightScene.Sources = saved; }
	}
	static Match NewMatch() => OnFixtures(() => FightScene.NewMatch(null));
	static void Tick(Match m, ApprenticeTutorial t, FighterInput p1, FighterInput p2) { m.Step(p1, p2); t.Observe(); }
	static void SkipTo(ApprenticeTutorial t, TutorialLesson l) { while (t.Current != l && t.SkipLesson()) { } }

	static int LightSlot(Fighter f)
	{
		int best = -1;
		for (int i = 0; i < f.Moves.Length; i++)
		{
			var mv = f.Moves[i];
			if (mv.IsNormal && !mv.Air && !mv.Low && mv.DirectionMask == 0 && (best < 0 || mv.Startup < f.Moves[best].Startup)) best = i;
		}
		return best;
	}

	/// <summary>Walks P1 beside P2 (no hits); returns when touching.</summary>
	static void Close(Match m, ApprenticeTutorial t)
	{
		for (int i = 0; i < 400; i++)
		{
			int gap = Math.Abs(m.P2.X - m.P1.X) / SimConfig.Scale;
			if (gap <= m.Config.BodyWidth / SimConfig.Scale + 10) return;
			Tick(m, t, new(InputBits.Right), FighterInput.None);
		}
	}

	// Y6 / only what happens while a lesson is up counts: the player's own blocked-looking hits (P2 blocks P1) are not "blocks".
	[Test]
	public static void BlockLesson_PlayersOwnHitsBlockedByTheApprentice_DoNotCount()
	{
		var m = NewMatch();
		var t = new ApprenticeTutorial(m, Fast);
		int blockedEvents = 0;
		m.Hit += (_, e) => { if (e.Attacker == 0 && e.Blocked) blockedEvents++; };
		int slot = LightSlot(m.P1);
		for (int i = 0; i < 900 && blockedEvents < 3; i++)
		{
			int gap = Math.Abs(m.P2.X - m.P1.X) / SimConfig.Scale;
			FighterInput p1 = gap > m.Config.BodyWidth / SimConfig.Scale + 10 ? new(InputBits.Right) : (i % 30 == 0 ? FighterInput.Attack(slot) : FighterInput.None);
			Tick(m, t, p1, new(InputBits.Right)); // P2 stands right of P1: Right = back = block (C3)
		}
		Assert.True(blockedEvents >= 1, "precondition: P2 blocked at least one of P1's hits");
		Assert.Equal(0, t.Blocks, "Y6: only hits Ryo blocks count");
		Assert.True(t.Current == TutorialLesson.Block, "still on the blocking lesson");
	}

	// Y6: a block outside the blocking lesson does not count, and does not rewrite the lesson.
	[Test]
	public static void Blocks_OutsideTheBlockLesson_AreNotCounted()
	{
		var m = NewMatch();
		var t = new ApprenticeTutorial(m, Fast);
		var coach = new TutorialCoach(m.P2, 1, m.Config, Fast, 30);
		t.SkipLesson(); // Throw is on screen
		int blocked = 0;
		m.Hit += (_, e) => { if (e.Blocked && e.Attacker == 1) blocked++; };
		for (int i = 0; i < 600 && blocked < 2; i++)
		{
			var p2 = coach.Next(AiView.Capture(m), TutorialLesson.Block)!.Value; // the apprentice swings as if teaching blocking
			Tick(m, t, new(InputBits.Left), p2);
		}
		Assert.True(blocked >= 1, "precondition: Ryo blocked an apprentice hit");
		Assert.Equal(0, t.Blocks, "Y6: blocks during another lesson are not counted");
		Assert.Equal(LessonOutcome.Skipped, t.Outcome(TutorialLesson.Block), "the skipped lesson stays skipped");
	}

	// Y6 pass condition: "a clash doesn't count" for the throw-break lesson.
	[Test]
	public static void ThrowBreakLesson_ClashOnTheSameFrame_DoesNotPass()
	{
		var m = NewMatch();
		var t = new ApprenticeTutorial(m, Fast);
		SkipTo(t, TutorialLesson.ThrowBreak);
		var outcomes = new List<ThrowOutcome>();
		m.Throw += (_, e) => outcomes.Add(e.Outcome);
		Close(m, t);
		for (int i = 0; i < 6 && !outcomes.Contains(ThrowOutcome.Clash); i++)
			Tick(m, t, new(THROW), new(THROW));
		for (int i = 0; i < 20; i++) Tick(m, t, FighterInput.None, FighterInput.None);
		Assert.True(outcomes.Contains(ThrowOutcome.Clash), "precondition: both grabs clashed");
		Assert.True(!outcomes.Contains(ThrowOutcome.Break), "precondition: no real break happened");
		Assert.Equal(TutorialLesson.ThrowBreak, t.Current ?? TutorialLesson.Cancel, "Y6: a clash does not pass the throw-break lesson");
	}

	// Y6: "break the apprentice's throw": Ryo throwing the apprentice, who breaks it, is not Ryo breaking.
	[Test]
	public static void ThrowBreakLesson_ApprenticeBreaksRyosThrow_DoesNotPass()
	{
		var m = NewMatch();
		var t = new ApprenticeTutorial(m, Fast);
		SkipTo(t, TutorialLesson.ThrowBreak);
		var outcomes = new List<ThrowEvent>();
		m.Throw += (_, e) => outcomes.Add(e);
		Close(m, t);
		for (int i = 0; i < 40; i++)
		{
			bool thrown = m.P2.State == FighterState.Thrown;
			FighterInput p1 = outcomes.Count == 0 && i % 20 == 0 ? new(THROW) : FighterInput.None;
			Tick(m, t, p1, thrown && i % 2 == 0 ? new(THROW) : FighterInput.None);
		}
		Assert.True(outcomes.Any(e => e.Outcome == ThrowOutcome.Break && e.Attacker == 0), "precondition: the apprentice broke Ryo's throw");
		Assert.Equal(TutorialLesson.ThrowBreak, t.Current ?? TutorialLesson.Cancel, "Y6: only Ryo breaking the apprentice's throw passes it");
	}

	// Y6 / only what counts while up: the cancel mark left from a combo before the lesson must not pass it.
	[Test]
	public static void CancelLesson_MarkLeftFromBeforeTheLesson_DoesNotPass()
	{
		var m = NewMatch();
		var rule = new CancelRule("probe-lent", CancelFrom.HeavyNormals, CancelInto.Specials, true, true, false);
		var t = new ApprenticeTutorial(m, Fast with { LentCancel = rule });
		SkipTo(t, TutorialLesson.Burst);
		m.P1.CancelUsed = 1; // a cancel made during the burst lesson; the combo is still going when the cancel lesson opens
		t.SkipLesson();
		Assert.True(t.Current == TutorialLesson.Cancel, "precondition: cancel lesson up");
		for (int i = 0; i < 40; i++) t.Observe();
		Assert.True(t.Outcome(TutorialLesson.Cancel) != LessonOutcome.Done, "Y6: a cancel made before the lesson was shown does not count for it");
	}

	// Y6 meter boundary: C5 one bar = ExCost; one point short does not pass, exactly the bar does.
	[Test]
	public static void MeterLesson_OnePointShortOfTheBar_DoesNotPass_ExactlyTheBarDoes()
	{
		var m = NewMatch();
		var t = new ApprenticeTutorial(m, Fast);
		SkipTo(t, TutorialLesson.Meter);
		m.P1.Meter = m.Config.ExCost - 1;
		for (int i = 0; i < 40; i++) t.Observe();
		Assert.True(t.Current == TutorialLesson.Meter, "99 of 100 is not a bar");
		m.P1.Meter = m.Config.ExCost;
		for (int i = 0; i < 40; i++) t.Observe();
		Assert.Equal(LessonOutcome.Done, t.Outcome(TutorialLesson.Meter), "exactly one bar passes");
	}

	// Y6 / C6: burst spent at entry makes the lesson unavailable even when skipping all.
	[Test]
	public static void BurstSpentAtEntry_SkipAll_StillFinishesOnce_BurstUnavailable()
	{
		var m = NewMatch();
		var t = new ApprenticeTutorial(m, Fast);
		m.P1.BurstUsed = true;
		int completed = 0, ended = 0;
		t.Completed += _ => completed++;
		t.LessonEnded += (_, _) => ended++;
		t.SkipAll();
		Assert.True(t.Finished && completed == 1 && ended == 6, "finishes once, six endings");
		Assert.Equal(LessonOutcome.Unavailable, t.Outcome(TutorialLesson.Burst), "C6: already spent");
		Assert.Equal(LessonOutcome.Skipped, t.Outcome(TutorialLesson.Meter), "the lesson before it was skipped");
	}

	// Y6: skipping a lesson that was already met must not leak "met" into the next one.
	[Test]
	public static void Skip_OnTheTickALessonWouldPass_DoesNotCarryMetIntoTheNextLesson()
	{
		var m = NewMatch();
		var t = new ApprenticeTutorial(m, Fast);
		SkipTo(t, TutorialLesson.Meter);
		m.P1.Meter = m.Config.ExCost + 6; // would pass at once...
		for (int i = 0; i < Fast.MinPromptTicks - 1; i++) t.Observe();
		t.SkipLesson(); // ...skipped one tick before it would
		Assert.Equal(LessonOutcome.Skipped, t.Outcome(TutorialLesson.Meter), "skipped, not done");
		for (int i = 0; i < 60; i++) t.Observe();
		Assert.True(t.Current == TutorialLesson.Burst, "burst lesson not passed by anything left from meter");
	}

	// F3: two identical guided fights agree on every tick's hash, coach included.
	[Test]
	public static void GuidedFight_StateHashAgreesEveryTick()
	{
		List<ulong> Run()
		{
			var m = NewMatch();
			var t = new ApprenticeTutorial(m, Fast);
			var coach = new TutorialCoach(m.P2, 1, m.Config, Fast, 30);
			var hs = new List<ulong>();
			for (int i = 0; i < 900; i++)
			{
				var p1 = i % 90 < 45 ? new FighterInput(InputBits.Left) : new FighterInput(i % 7 == 0 ? THROW : InputBits.None);
				if (i == 300) t.SkipLesson();
				var p2 = coach.Next(AiView.Capture(m), t.Current) ?? FighterInput.None;
				Tick(m, t, p1, p2);
				hs.Add(m.StateHash());
			}
			return hs;
		}
		var a = Run(); var b = Run();
		Assert.True(a.SequenceEqual(b), "F3: same fight, same hash on every tick");
	}

	// Old recordings: no Tutorial entry in the header, replays unchanged.
	[Test]
	public static void OldRecording_HasNoTutorialEntry_AndReplays()
	{
		var m = NewMatch();
		var rec = ReplayRecorder.Begin(m, null, 1);
		for (int i = 0; i < 120; i++) { var p1 = new FighterInput(i % 3 == 0 ? InputBits.Right : InputBits.None); m.Step(p1, FighterInput.None); rec.Record(p1, FighterInput.None, m.StateHash()); }
		var done = rec.Finish(m);
		string path = ProjectSettings.GlobalizePath("user://yok92-probe-old.yfr");
		done.Save(path);
		var bytes = File.ReadAllBytes(path);
		string text = System.Text.Encoding.UTF8.GetString(bytes);
		var loaded = ReplayRecording.Load(path);
		File.Delete(path);
		Assert.True(!text.Contains("\"Tutorial\""), "a non-guided header carries no Tutorial key");
		Assert.True(loaded.Header.Tutorial == null, "loads as not guided");
		var cat = ReplayCatalog.FromRepo(Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + ".."));
		Assert.True(Replayer.Run(loaded, cat).Ok, "replays unchanged");
	}

	// F3 + Y6: AI switched off mid-fight and back on: the guided replay still matches (scene mimic: coach only runs while the AI is on).
	[Test]
	public static void Replay_GuidedFight_AiToggledOffAndOn_StillReplays()
	{
		var m = NewMatch();
		var t = new ApprenticeTutorial(m, Fast);
		var coach = new TutorialCoach(m.P2, 1, m.Config, Fast, 30);
		var prof = OnFixtures(() => FightScene.LoadProfiles("kitsune"))[0];
		ProfileAi? ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 92, 30);
		var rec = ReplayRecorder.Begin(m, ai, 92);
		rec.OnTutorial(t, coach);
		for (int i = 0; i < 900 && m.Phase != MatchPhase.Over; i++)
		{
			if (i == 200) { ai = null; rec.OnAi(null, 92); }
			if (i == 500) { ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 93, 30); rec.OnAi(ai, 93); }
			if (i == 350 && t.SkipLesson()) rec.OnTutorialSkip();
			FighterInput p2;
			if (ai is null) p2 = i % 40 < 5 ? new(InputBits.Left) : FighterInput.None;
			else { var view = AiView.Capture(m); var fromAi = ai.Next(view); p2 = coach.Next(view, t.Current) ?? fromAi; }
			var p1 = new FighterInput(i % 50 < 25 ? InputBits.Left : InputBits.None);
			m.Step(p1, p2); t.Observe();
			if (m.Phase == MatchPhase.Over) t.Detach();
			rec.Record(p1, p2, m.StateHash());
		}
		var cat = ReplayCatalog.FromRepo(Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + ".."));
		var report = Replayer.Run(rec.Finish(m), cat);
		Assert.True(report.Ok, "replay with the AI toggled: " + report);
	}

	// X1 / F3: a lent rule is gone from the fighter after Detach, and a second guided match built fresh does not inherit it.
	[Test]
	public static void LentRule_DoesNotLeakIntoTheNextMatch()
	{
		var rule = new CancelRule("probe-lent2", CancelFrom.HeavyNormals, CancelInto.Specials, true, true, false);
		var m1 = NewMatch();
		var t = new ApprenticeTutorial(m1, Fast with { LentCancel = rule });
		Assert.True(m1.P1.CancelRules.Contains(rule), "lent");
		t.Detach();
		var m2 = NewMatch();
		Assert.Equal(0, m2.P1.CancelRules.Count, "X1: a fresh match starts with no cancel rule");
		Assert.Equal(0, m1.P1.CancelRules.Count, "and the first has it back");
		// a rule Ryo already holds is never taken away by Detach
		var m3 = NewMatch();
		m3.P1.CancelRules.Add(rule);
		var t3 = new ApprenticeTutorial(m3, Fast with { LentCancel = rule });
		t3.Detach();
		Assert.True(m3.P1.CancelRules.Contains(rule), "an owned rule is not lent, so not taken back");
	}

	// Y4/P6: with a KO, nothing crashes and the lessons left stay pending; skipping after is refused.
	[Test]
	public static void SkipAfterKo_IsRefused_AndLessonsStayPending()
	{
		var m = NewMatch();
		var t = new ApprenticeTutorial(m, Fast);
		m.P1.Health = 0;
		for (int i = 0; i < 400 && m.Phase != MatchPhase.Over; i++) Tick(m, t, FighterInput.None, FighterInput.None);
		t.Detach();
		var before = m.StateHash();
		Assert.True(!t.SkipLesson(), "refused after the match ended");
		t.SkipAll();
		Assert.True(t.Index == 0 && t.Outcome(TutorialLesson.Block) == LessonOutcome.Pending, "nothing changed");
		Assert.Equal(before, m.StateHash(), "sim untouched");
	}
}
