using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;
using YokaiFighters.Tutorial;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-92, Y6: the guided apprentice fight. <see cref="ApprenticeTutorial"/> walks blocking, throw, throw break,
/// meter, burst and one cancel against a real <see cref="Match"/> driven by <see cref="TutorialCoach"/> (the
/// apprentice's half, AiView only) and a scripted player; skipping, the X1 cancel gap, the C6 once-per-fight
/// burst, only-on-screen counting, determinism, P6 isolation, and the fight scene / first-run gating.
/// </summary>
public static class TutorialTests
{
	const InputBits LP = InputBits.LightPunch, LK = InputBits.LightKick, MP = InputBits.MediumPunch, HP = InputBits.HeavyPunch;
	const InputBits THROW = LP | LK, BURST = LP | MP | HP;

	static T OnFixtures<T>(Func<T> body)
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		try { return body(); }
		finally { FightScene.Sources = saved; }
	}

	static Match NewMatch() => OnFixtures(() => FightScene.NewMatch(null));

	static readonly TutorialOptions Fast = new() { MinPromptTicks = 10 };

	/// <summary>The scripted player: does what each prompt asks, reading only its own fight (it is the player, not the AI).</summary>
	static FighterInput Player(Match m, ApprenticeTutorial t)
	{
		Fighter me = m.P1, foe = m.P2;
		int gap = Math.Abs(foe.X - me.X) / SimConfig.Scale, near = m.Config.BodyWidth / SimConfig.Scale + 10;
		InputBits fwd = foe.X >= me.X ? InputBits.Right : InputBits.Left, back = fwd == InputBits.Right ? InputBits.Left : InputBits.Right;
		bool even = m.Tick % 2 == 0;
		switch (t.Current)
		{
			case TutorialLesson.Block: return new(back); // C3: hold back
			case TutorialLesson.Throw: return gap > near ? new(fwd) : new(m.Tick % 20 == 0 ? THROW : InputBits.None);
			case TutorialLesson.ThrowBreak: return new(me.State == FighterState.Thrown && even ? THROW : InputBits.None);
			case TutorialLesson.Meter: return gap > near ? new(fwd) : new(m.Tick % 12 == 0 ? LP : InputBits.None);
			case TutorialLesson.Burst: return new(me.State == FighterState.Hitstun && even ? BURST : InputBits.None);
			default: return FighterInput.None;
		}
	}

	/// <summary>A guided fight to the end of the lessons (or <paramref name="ticks"/>); returns the match hash.</summary>
	static ulong Guided(out ApprenticeTutorial tut, out int completed, TutorialOptions? options = null, int ticks = 8000)
	{
		var m = NewMatch();
		var t = new ApprenticeTutorial(m, options ?? Fast);
		var coach = new TutorialCoach(m.P2, 1, m.Config, options ?? Fast);
		int done = 0;
		t.Completed += _ => done++;
		for (int i = 0; i < ticks && !t.Finished && m.Phase == MatchPhase.Fighting; i++)
		{
			FighterInput p2 = coach.Next(AiView.Capture(m), t.Current) ?? FighterInput.None;
			m.Step(Player(m, t), p2);
			t.Observe();
		}
		tut = t;
		completed = done;
		return m.StateHash();
	}

	[Test]
	public static void Lessons_AreY6sList_InItsOrder()
	{
		Assert.True(ApprenticeTutorial.Order.SequenceEqual(new[] { TutorialLesson.Block, TutorialLesson.Throw, TutorialLesson.ThrowBreak,
			TutorialLesson.Meter, TutorialLesson.Burst, TutorialLesson.Cancel }), "Y6: blocking, throw, throw break, meter, burst, one cancel");
		Assert.Equal(Enum.GetValues<TutorialLesson>().Length, ApprenticeTutorial.Order.Count, "every lesson is in the order once");
	}

	[Test]
	public static void GuidedFight_EveryLessonIsDoneByPlaying_TheCancelIsUnavailableWithoutARule()
	{
		Guided(out var t, out int completed);
		foreach (var l in new[] { TutorialLesson.Block, TutorialLesson.Throw, TutorialLesson.ThrowBreak, TutorialLesson.Meter, TutorialLesson.Burst })
			Assert.Equal(LessonOutcome.Done, t.Outcome(l), $"{l}: done by the scripted player against the coached apprentice");
		Assert.Equal(LessonOutcome.Unavailable, t.Outcome(TutorialLesson.Cancel), "X1: Ryo holds no cancel rule, so the lesson cannot be shown");
		Assert.True(t.Finished && t.Current == null && t.Prompt == "", "finished: no prompt");
		Assert.Equal(1, completed, "Completed raised once");
		Assert.True(t.Blocks >= Fast.BlocksToPass, "the blocks were counted");
	}

	[Test]
	public static void GuidedFight_IsDeterministic()
	{
		ulong a = Guided(out _, out _), b = Guided(out _, out _);
		Assert.Equal(a, b, "same inputs, same fight (F3)");
	}

	[Test]
	public static void Skip_EndsTheLessonOnScreen_SkipAllFinishes_AndDetachStopsCounting()
	{
		var m = NewMatch();
		var t = new ApprenticeTutorial(m, Fast);
		var ended = new System.Collections.Generic.List<(TutorialLesson, LessonOutcome)>();
		t.LessonEnded += (l, o) => ended.Add((l, o));
		Assert.True(t.Current == TutorialLesson.Block && t.Prompt.StartsWith(TutorialPrompts.Marker), "opens on blocking with a placeholder prompt");
		Assert.True(t.SkipLesson() && t.Current == TutorialLesson.Throw, "skip moves on");
		Assert.Equal(LessonOutcome.Skipped, t.Outcome(TutorialLesson.Block), "marked skipped");
		int completed = 0;
		t.Completed += _ => completed++;
		t.SkipAll();
		Assert.True(t.Finished && completed == 1 && !t.SkipLesson(), "skip all finishes once; nothing left to skip");
		Assert.True(ended.Count == 6 && ended.All(e => e.Item2 is LessonOutcome.Skipped or LessonOutcome.Unavailable), "every lesson ended, skipped (cancel: unavailable)");
		for (int i = 0; i < 200; i++) { m.Step(FighterInput.None, FighterInput.None); t.Observe(); }
		Assert.True(t.Finished && t.LessonTicks == 0, "a finished tutorial counts nothing more");
	}

	[Test]
	public static void OnlyWhatHappensWhileALessonIsShown_CountsForIt()
	{
		var m = NewMatch();
		var t = new ApprenticeTutorial(m, Fast);
		// A landed throw during the blocking lesson does not pass the throw lesson later.
		int i = 0;
		for (; i < 600 && t.Current == TutorialLesson.Block; i++)
		{
			int gap = Math.Abs(m.P2.X - m.P1.X) / SimConfig.Scale;
			m.Step(new(gap > m.Config.BodyWidth / SimConfig.Scale + 10 ? InputBits.Right : (m.Tick % 20 == 0 ? THROW : InputBits.None)), FighterInput.None);
			t.Observe();
			if (m.P2.State.IsDown()) break;
		}
		Assert.True(t.Current == TutorialLesson.Block && m.P2.State.IsDown(), "a throw landed while blocking was on screen");
		t.SkipLesson();
		for (int k = 0; k < 120; k++) { m.Step(FighterInput.None, FighterInput.None); t.Observe(); }
		Assert.True(t.Current == TutorialLesson.Throw, "the throw lesson still waits for a throw of its own");
	}

	[Test]
	public static void ALessonStaysUp_AtLeastMinPromptTicks_EvenWhenAlreadyMet()
	{
		var m = NewMatch();
		var t = new ApprenticeTutorial(m, new TutorialOptions { MinPromptTicks = 50 });
		t.SkipLesson(); t.SkipLesson(); t.SkipLesson(); // to the meter lesson
		m.P1.Meter = m.Config.ExCost; // already one bar
		for (int k = 1; k < 50; k++) { m.Step(FighterInput.None, FighterInput.None); t.Observe(); }
		Assert.True(t.Current == TutorialLesson.Meter, "49 ticks: still on screen");
		m.Step(FighterInput.None, FighterInput.None); t.Observe();
		Assert.True(t.Current == TutorialLesson.Burst && t.Outcome(TutorialLesson.Meter) == LessonOutcome.Done, "tick 50: done, burst next");
	}

	[Test]
	public static void Burst_SpentBeforeItsLesson_IsUnavailable()
	{
		var m = NewMatch();
		var t = new ApprenticeTutorial(m, Fast);
		m.P1.BurstUsed = true;
		for (int k = 0; k < 4; k++) t.SkipLesson();
		Assert.Equal(LessonOutcome.Unavailable, t.Outcome(TutorialLesson.Burst), "C6: once per fight");
	}

	[Test]
	public static void LentCancelRule_IsHeldForTheTutorialOnly_AndACancelPassesTheLesson()
	{
		var m = NewMatch();
		var rule = new CancelRule("test-lent", CancelFrom.HeavyNormals, CancelInto.Specials, true, true, false);
		var t = new ApprenticeTutorial(m, Fast with { LentCancel = rule });
		Assert.True(m.P1.CancelRules.Contains(rule), "lent at the start");
		for (int k = 0; k < 5; k++) t.SkipLesson();
		Assert.True(t.Current == TutorialLesson.Cancel, "the cancel lesson is shown when a rule is held");
		// No move in data has a cancel window yet; the lesson reads X3's per-combo mark, set here as a cancel would.
		m.P1.CancelUsed = 1;
		for (int k = 0; k < Fast.MinPromptTicks; k++) { t.Observe(); }
		Assert.Equal(LessonOutcome.Done, t.Outcome(TutorialLesson.Cancel), "a cancel passes it");
		Assert.True(t.Finished && !m.P1.CancelRules.Contains(rule), "the lent rule is taken back (X1: none outside the tutorial)");
	}

	[Test]
	public static void Detach_ReturnsALentRule_AndLeavesTheRestPending()
	{
		var m = NewMatch();
		var rule = new CancelRule("test-lent", CancelFrom.Specials, CancelInto.Dash, true, true, false);
		var t = new ApprenticeTutorial(m, Fast with { LentCancel = rule });
		t.Detach();
		Assert.True(!m.P1.CancelRules.Contains(rule) && t.Outcome(TutorialLesson.Block) == LessonOutcome.Pending, "detached: rule back, lessons pending");
		Assert.True(!t.SkipLesson(), "a detached tutorial takes no more input");
	}

	[Test]
	public static void Coach_SeesOnlyTheScreen_ThroughItsReactionDelay()
	{
		// P6: no Match, Fighter, input reader or buffer is kept; decisions come from AiView snapshots only.
		var banned = new[] { typeof(Match), typeof(Fighter), typeof(InputReader) };
		foreach (var f in typeof(TutorialCoach).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
			Assert.True(!banned.Contains(f.FieldType), $"TutorialCoach.{f.Name} must not hold a {f.FieldType.Name}");

		// Y4: the first move it makes comes ReactionFrames ticks after the screen it reacts to.
		var m = NewMatch();
		var coach = new TutorialCoach(m.P2, 1, m.Config, Fast, reactionFrames: 30);
		int first = -1;
		for (int k = 0; k < 60 && first < 0; k++)
		{
			var input = coach.Next(AiView.Capture(m), TutorialLesson.Block)!.Value;
			if (input != FighterInput.None) first = k;
			m.Step(FighterInput.None, FighterInput.None);
		}
		Assert.Equal(30, first, "nothing before the first screen is 30 ticks old");
		Assert.True(coach.Next(AiView.Capture(m), null) == null, "no lesson: the profile AI plays");
		Assert.True(coach.LightNormal != null && coach.ThrowRange > 0 && coach.StrikeRange > 0, "kit read from move data");
	}

	[Test]
	public static void Prompts_ArePlaceholders_OnePerLesson()
	{
		var texts = ApprenticeTutorial.Order.Select(TutorialPrompts.For).ToList();
		Assert.True(texts.All(s => s.StartsWith(TutorialPrompts.Marker) && s.Length > TutorialPrompts.Marker.Length), "every prompt is marked PLACEHOLDER (habit-writer owns the copy)");
		Assert.Equal(texts.Count, texts.Distinct().Count(), "one prompt per lesson");
	}

	[Test]
	public static void TutorialSources_UseNoGodotClockOrRandomness()
	{
		string dir = ProjectSettings.GlobalizePath("res://scripts/Tutorial");
		foreach (var file in Directory.GetFiles(dir, "*.cs"))
		{
			string src = File.ReadAllText(file);
			foreach (var bad in new[] { "using Godot", "DateTime", "Stopwatch", "System.Random", "float ", "double " })
				Assert.True(!src.Contains(bad), $"{Path.GetFileName(file)}: '{bad}' (pure, deterministic sim code)");
		}
	}

	[Test]
	public static void Guided_OnlyWhenAllowed_AndOnTheApprenticeTier()
	{
		var node = (string tier) => new NodeContext("n", "duel", 1, "kitsune", false, "kitsune", null, tier, 30, true, false, new System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>(), Array.Empty<string>());
		Assert.True(FightScene.IsGuided(true, node("apprentice")), "first run, apprentice row: guided");
		Assert.True(!FightScene.IsGuided(false, node("apprentice")), "not allowed (seen before): not guided");
		Assert.True(!FightScene.IsGuided(true, node("yokai")) && !FightScene.IsGuided(true, null), "other rows and no node: never");
	}

	static void WithBoot(Node runner, Action<GameFlow> body)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		YokaiFighters.Ui.MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = 77;
		flow.ConfigureFight = f => f.ExternalDrive = true;
		try { runner.AddChild(flow); body(flow); }
		finally
		{
			runner.RemoveChild(flow);
			flow.QueueFree();
			YokaiFighters.Ui.MenuSettings.Reset();
			FightScene.Sources = saved;
			FightScene.Run = savedRun;
			FightScene.Opponent = savedFoe;
		}
	}

	[Test]
	public static void Boot_FirstRunIsGuided_TheFlagIsSetOnce_ThenRowOneIsAPlainDuel(Node runner) => WithBoot(runner, flow =>
	{
		Assert.True(!flow.Saved.Story.Has(StoryProgress.ApprenticeTutorial), "a fresh save: first run");
		flow.Title.StartButton.EmitSignal(BaseButton.SignalName.Pressed);
		flow.ControlSelect.Start.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.True(flow.Map.Confirm(), "row 1 picked");
		var fight = flow.Fight!;
		Assert.True(fight.Node.Tier == "apprentice" && fight.Tutorial is { Current: TutorialLesson.Block }, "the apprentice duel is guided");
		Assert.True(fight.TutorialPromptText.Contains(TutorialPrompts.For(TutorialLesson.Block)), "the prompt is on screen");
		for (int k = 0; k < 300 && fight.Tutorial!.Blocks == 0; k++) fight.StepWithAi(new(InputBits.Left)); // the live P2 path
		Assert.True(fight.Tutorial!.Blocks > 0, "the scene's P2 is the coach during a lesson: the apprentice came in and swung");
		Assert.True(fight.SkipTutorialLesson() &&fight.Tutorial!.Current == TutorialLesson.Throw, "skip key action moves on");
		fight.Tutorial!.SkipAll();
		Assert.True(flow.Saved.Story.Has(StoryProgress.ApprenticeTutorial), "finished: the save remembers it");
		fight.Step(FighterInput.None, FighterInput.None);
		Assert.Equal("", fight.TutorialPromptText, "no prompt once finished");

		flow.ReturnToTitle();
		flow.Title.StartButton.EmitSignal(BaseButton.SignalName.Pressed);
		flow.ControlSelect.Start.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.True(flow.Map.Confirm(), "row 1 again on a new run");
		Assert.True(flow.Fight!.Node.Tier == "apprentice" && flow.Fight.Tutorial == null, "second run: a plain duel");
	});
}
