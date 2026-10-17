using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-98, R7 / M5: the dojo's training trial. The choice and its heal and level on <see cref="Dojo"/>, the trial
/// fight on a real <see cref="Match"/> through <see cref="TrialRun"/>, the state hash, the save (version 6) and
/// Continue mid-trial, and the trial played on the fight scene. Trials are the shipped ones in data/trials/
/// (movesmith's); the pool and kits come from data/ (the trials name real specials).
/// </summary>
public static class DojoTrialTests
{
	const string DojoNode = "r7-dojo";
	const string WaveTrial = "spirit-wave-trial";

	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	internal static string MapDir => ContentPaths.Data("map");
	internal static TrialData[] Library => TrialLoader.LoadDirectory(ContentPaths.Data("trials"));
	internal static AbilityPool Pool => AbilityPool.LoadRepo(RepoRoot);

	static Dojo NewStop(out RunState run, TrialData[]? library = null)
	{
		var r = RunState.NewRun(Pool);
		run = r;
		var lib = library ?? Library;
		return new Dojo(r, DojoNode, null, () => lib);
	}

	internal static RunSession AtDojo(int health, uint seed = 98)
	{
		var s = RunSession.Start(Pool, FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir), seed);
		s.Trials = Library;
		for (int row = 1; row <= 6; row++)
		{
			Assert.True(s.Enter(s.Reachable()[0]).Ok, $"row {row} entered");
			var draft = s.WinFight(500);
			if (draft is { Picked: false })
			{
				// Keep the starters: take a card that is not a replacement when one exists, else the first.
				int pick = 0;
				var card = draft.Offer.Cards[pick];
				Assert.True(draft.Pick(pick, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, $"row {row}: picked {card.AbilityId}");
			}
			Assert.True(s.CompleteNode().Ok, $"row {row} completed");
		}
		Assert.True(s.Enter(DojoNode).Ok, "the dojo is reachable from row 6 (R1)");
		s.Run.SetHealth(health);
		return s;
	}

	static bool Throws<T>(Action body) where T : Exception
	{
		try { body(); }
		catch (T) { return true; }
		return false;
	}

	// ---- offers and the one choice ----

	[Test]
	public static void Offers_OneTrialPerOwnedSpecialThatHasOne()
	{
		var stop = NewStop(out var run);
		var offers = stop.Trials;
		Assert.True(offers.Count > 0, "Ryo's starters have shipped trials");
		foreach (var o in offers)
		{
			Assert.Equal(o.Trial.Special, run[o.Slot]!.Data.Id, $"{o.Trial.Id} is for the special in slot {o.Slot}");
			Assert.True(run.CanLevelUp(o.Slot), "only a special that can still level gets a trial");
		}
		Assert.True(offers.All(o => run.Owns(o.Trial.Special)), "never a trial for a special Ryo does not own (foxfire)");
		Assert.True(!offers.Any(o => o.Trial.Special == "foxfire"), "foxfire is not owned at the start");
		var none = NewStop(out _, Array.Empty<TrialData>());
		Assert.True(none.Trials.Count == 0 && !none.CanTrial, "specials without a trial get no trial option");
		Assert.True(!none.ChooseTrial(WaveTrial).Ok && none.Choice == DojoChoice.None, "and choosing one is refused");
	}

	[Test]
	public static void ChooseTrial_IsTheOneChoice_RestCloses()
	{
		var stop = NewStop(out var run);
		run.SetHealth(400);
		Assert.True(!stop.ChooseTrial("no-such-trial").Ok && stop.Choice == DojoChoice.None, "an unknown trial is refused");
		Assert.True(stop.ChooseTrial(WaveTrial).Ok, "Dojo.ChooseTrial");
		Assert.Equal(DojoChoice.Trial, stop.Choice, "the choice is the trial");
		Assert.Equal(DojoTrialResult.Running, stop.TrialResult, "running");
		Assert.Equal(0, stop.Attempts, "no attempt yet");
		Assert.Equal(400, run.Health, "nothing heals on choosing");
		Assert.True(!stop.Rest().Ok && run.Health == 400, "trial and rest are mutually exclusive (R7)");
		Assert.True(!stop.ChooseTrial(WaveTrial).Ok, "a second trial is refused");
		var rested = NewStop(out _);
		Assert.True(rested.Rest().Ok && !rested.ChooseTrial(WaveTrial).Ok && !rested.CanTrial, "after a rest the trial is closed");
	}

	// ---- R7: heal 15% either way, level when landed within 3 ----

	[Test]
	public static void LandedOnSecondAttempt_Heals15_AndLevelsTheSpecial()
	{
		var stop = NewStop(out var run);
		run.SetHealth(500);
		Assert.True(stop.ChooseTrial(WaveTrial).Ok, "chosen");
		var slot = stop.TrialSlot;
		int before = run[slot]!.Level;
		Assert.True(stop.RecordAttempt(false).Ok, "attempt 1 missed");
		Assert.Equal(500, run.Health, "no heal until the trial ends");
		Assert.Equal(DojoTrialResult.Running, stop.TrialResult, "still running after one miss");
		Assert.True(stop.RecordAttempt(true).Ok, "attempt 2 landed");
		Assert.Equal(650, run.Health, "R7: +15% of 1,000");
		Assert.Equal(Dojo.TrialHeal, stop.Healed, "healed 150");
		Assert.Equal(before + 1, run[slot]!.Level, "the special levelled");
		Assert.Equal(DojoTrialResult.Landed, stop.TrialResult, "landed");
		Assert.Equal(2, stop.Attempts, "2 attempts used");
		Assert.True(!stop.RecordAttempt(true).Ok && run.Health == 650 && run[slot]!.Level == before + 1, "a finished trial takes no more attempts");
	}

	[Test]
	public static void ThreeMisses_Heal15_NoLevel()
	{
		var stop = NewStop(out var run);
		run.SetHealth(300);
		Assert.True(stop.ChooseTrial(WaveTrial).Ok, "chosen");
		int before = run[stop.TrialSlot]!.Level;
		for (int i = 0; i < 3; i++) Assert.True(stop.RecordAttempt(false).Ok, $"miss {i + 1}");
		Assert.Equal(450, run.Health, "R7: a failed trial heals 15% too");
		Assert.Equal(before, run[stop.TrialSlot]!.Level, "no level");
		Assert.Equal(DojoTrialResult.Failed, stop.TrialResult, "failed after 3");
		Assert.True(!stop.RecordAttempt(true).Ok, "no fourth attempt (R7: within 3)");
	}

	[Test]
	public static void TrialHeal_IsCappedAtFull()
	{
		var stop = NewStop(out var run);
		run.SetHealth(950);
		Assert.True(stop.ChooseTrial(WaveTrial).Ok && stop.RecordAttempt(true).Ok, "landed first time");
		Assert.Equal(RunState.MaxHealth, run.Health, "capped at 1,000");
		Assert.Equal(50, stop.Healed, "healed only up to full");
	}

	// ---- session: leaving, hash ----

	[Test]
	public static void Session_CantLeaveMidTrial_AndHashesChoiceAndAttempts()
	{
		var s = AtDojo(600);
		var stop = s.Dojo!;
		var offer = stop.Trials.FirstOrDefault();
		Assert.True(offer is not null, "a trial is offered at the row-7 dojo");
		ulong none = s.StateHash();
		Assert.True(stop.ChooseTrial(offer!.Trial.Id).Ok, "chosen");
		ulong chosen = s.StateHash();
		Assert.True(chosen != none, "the trial choice is in StateHash");
		Assert.True(!s.CompleteNode().Ok, "the node can't be completed mid-trial");
		Assert.True(stop.RecordAttempt(false).Ok, "a miss");
		ulong oneMiss = s.StateHash();
		Assert.True(oneMiss != chosen, "the attempt count is in StateHash");
		Assert.True(stop.RecordAttempt(false).Ok && stop.RecordAttempt(false).Ok, "three misses");
		Assert.True(s.StateHash() != oneMiss, "the finished trial hashes differently");
		var same = AtDojo(600);
		Assert.True(same.Dojo!.ChooseTrial(offer.Trial.Id).Ok, "same walk, same trial");
		Assert.Equal(chosen, same.StateHash(), "deterministic: same walk and choice, same hash");
		Assert.True(s.CompleteNode().Ok, "the finished trial lets Ryo leave");
	}

	// ---- save and M5 ----

	static RunSession RoundTrip(RunSession s, System.Collections.Generic.List<string>? notes = null)
	{
		// In memory only (the file's text round trip is SaveTests.DojoTrial_*: only the save tests may name the store).
		var save = RunSave.Capture(s, "default-map.json");
		return save.Restore(Pool, FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir), notes, trials: Library);
	}

	[Test]
	public static void Save_FinishedTrialComesBackWithoutHealingTwice()
	{
		var s = AtDojo(500);
		var stop = s.Dojo!;
		var offer = stop.Trials[0];
		Assert.True(stop.ChooseTrial(offer.Trial.Id).Ok && stop.RecordAttempt(false).Ok && stop.RecordAttempt(true).Ok, "landed on attempt 2");
		var save = RunSave.Capture(s, "default-map.json");
		Assert.Equal(new DojoSave("trial", offer.Trial.Id, offer.Slot.ToString(), 2, "landed"), save.Dojo, "the trial and its attempts are in the save");
		var back = RoundTrip(s);
		Assert.Equal(650, back.Run.Health, "the heal is in the saved health, not given again");
		Assert.Equal(DojoChoice.Trial, back.Dojo!.Choice, "the choice is back");
		Assert.Equal(2, back.Dojo.Attempts, "the attempts are back");
		Assert.Equal(DojoTrialResult.Landed, back.Dojo.TrialResult, "landed");
		Assert.Equal(s.StateHash(), back.StateHash(), "same hash after the round trip");
		Assert.True(!back.Dojo.Rest().Ok && !back.Dojo.RecordAttempt(true).Ok, "nothing more to take");
	}

	[Test]
	public static void M5_QuitMidTrial_ContinueRestartsTheNode_AttemptsReset()
	{
		var s = AtDojo(500);
		var entry = RunSave.Capture(s, "default-map.json");
		var stop = s.Dojo!;
		var offer = stop.Trials[0];
		Assert.True(stop.ChooseTrial(offer.Trial.Id).Ok && stop.RecordAttempt(false).Ok && stop.RecordAttempt(false).Ok, "two misses, then quit");
		Assert.Equal(new DojoSave("trial", offer.Trial.Id, offer.Slot.ToString(), 2, "running"), RunSave.Capture(s).Dojo, "a running trial is captured as running");
		var notes = new System.Collections.Generic.List<string>();
		var back = RoundTrip(s, notes);
		Assert.Equal(DojoChoice.None, back.Dojo!.Choice, "M5: the node restarts with no choice");
		Assert.Equal(0, back.Dojo.Attempts, "attempts reset");
		Assert.True(back.Dojo.CanRest && back.Dojo.CanTrial, "rest and trial both open again");
		Assert.Equal(500, back.Run.Health, "the health Ryo entered with");
		Assert.True(notes.Any(n => n.Contains("M5")), "the restore says why");
		// The game's own save point for this quit is the node's entry save: the same restart.
		var fromEntry = entry.Restore(Pool, FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir), null, trials: Library);
		Assert.Equal(fromEntry.StateHash(), back.StateHash(), "same as Continue from the entry save");
		Assert.True(back.Dojo.ChooseTrial(offer.Trial.Id).Ok && back.Dojo.Attempts == 0, "the trial starts again from attempt 0");
	}

	[Test]
	public static void Save_RejectsATrialThatCantBe()
	{
		var s = AtDojo(500);
		var save = RunSave.Capture(s, "default-map.json");
		var graph = FileMapSource.Default(MapDir).Load();
		var scenes = NodeSceneTable.Default(MapDir);
		var slot = s.Dojo!.Trials[0].Slot.ToString();
		Assert.True(Throws<FormatException>(() => (save with { Dojo = new DojoSave("trial", WaveTrial, slot, 2, "failed") }).Restore(Pool, graph, scenes, trials: Library)), "failed needs 3 attempts");
		Assert.True(Throws<FormatException>(() => (save with { Dojo = new DojoSave("trial", "no-such-trial", slot, 1, "landed") }).Restore(Pool, graph, scenes, trials: Library)), "unknown trial");
		Assert.True(Throws<FormatException>(() => (save with { Dojo = new DojoSave("trial", WaveTrial, slot, 1, "won") }).Restore(Pool, graph, scenes, trials: Library)), "unknown result");
		var rest = (save with { Dojo = new DojoSave("rest", null, null, 0, null) }).Restore(Pool, graph, scenes, trials: Library);
		Assert.True(rest.Dojo!.Choice == DojoChoice.Rest && !rest.Dojo.CanTrial, "a saved rest comes back and closes the trial");
	}

	// ---- the trial fight on the real fight loop ----

	static (TrialRun Run, RunState State) NewTrialRun(string trialId)
	{
		var state = RunState.NewRun(Pool);
		var data = Library.Single(t => t.Id == trialId);
		var src = KitSources.Repo(RepoRoot);
		var cfg = TrialRun.Config(state.MatchConfig(), data);
		var ryo = Kit.Load(Kit.Ryo, src);
		var dummy = Kit.Load("kitsune", src);
		var m = new Match(cfg, ryo.Moves, dummy.Moves);
		dummy.Equip(m.P2);
		ryo.Equip(m.P1);
		state.ApplyTo(m.P1, cfg);
		return (new TrialRun(data, m), state);
	}

	static SpecialSlot SlotOf(RunState run, string special) => run.Owned.Single(o => o.Special.Data.Id == special).Slot;

	[Test]
	public static void TrialRun_StartsAtTheTrialsDistance()
	{
		var (run, _) = NewTrialRun(WaveTrial);
		int units = Math.Abs(run.Match.P1.X - run.Match.P2.X) / SimConfig.Scale;
		Assert.Equal(run.Trial.Distance, units, "the fighters start the trial's distance apart");
	}

	[Test]
	public static void TrialRun_SpiritWaveTwice_Lands_IdleMisses_Deterministic()
	{
		ulong Play(bool act, out bool landed, out int ticks)
		{
			var (run, state) = NewTrialRun(WaveTrial);
			var slot = SlotOf(state, "spirit-wave");
			bool? result = null;
			run.AttemptEnded += (_, l) => result = l;
			int t = 0;
			while (result is null && t < run.Trial.AttemptFrames + 5)
			{
				t++;
				run.Step(act && t % 60 == 1 ? FighterInput.Special(slot) : FighterInput.None);
			}
			landed = result == true;
			ticks = t;
			return run.Match.StateHash();
		}
		ulong a = Play(true, out bool landed, out int ticks);
		Assert.True(landed, $"two Spirit Waves from 800 units land the trial (ended after {ticks} ticks)");
		ulong b = Play(true, out _, out _);
		Assert.Equal(a, b, "same inputs, same fight (F3)");
		Play(false, out bool idle, out int idleTicks);
		Assert.True(!idle, "doing nothing misses");
		Assert.Equal(Library.Single(t => t.Id == WaveTrial).AttemptFrames, idleTicks, "the attempt ends when its time is up");
	}

	[Test]
	public static void TrialRun_ThreeIdleAttempts_FailTheDojoTrial()
	{
		var (run, state) = NewTrialRun(WaveTrial);
		state.SetHealth(400);
		var stop = new Dojo(state, DojoNode, null, () => Library);
		Assert.True(stop.ChooseTrial(WaveTrial).Ok, "chosen");
		run.AttemptEnded += (_, landed) => stop.RecordAttempt(landed);
		int guard = 0;
		while (stop.TrialRunning && guard++ < 5000)
		{
			run.Step(FighterInput.None);
			if (run.AttemptOver && stop.TrialRunning) Assert.True(run.NextAttempt(), "next attempt");
		}
		Assert.Equal(3, run.Attempt, "three attempts played");
		Assert.Equal(DojoTrialResult.Failed, stop.TrialResult, "failed");
		Assert.Equal(550, state.Health, "healed 15% anyway");
	}

	[Test]
	public static void TrialRun_RisingTalismanTrial_NeedsTheDummyAirborne()
	{
		// The dummy jumps on tick 1, 121, ... A Rising Talisman on the ground-bound dummy (pressed just before its
		// first jump lands far outside the jump) never meets the step; pressed while it rises, it does.
		bool Try(int press)
		{
			var (run, state) = NewTrialRun("rising-talisman-trial");
			var slot = SlotOf(state, "rising-talisman");
			for (int t = 1; t <= 119 && !run.AttemptOver; t++) run.Step(t == press ? FighterInput.Special(slot) : FighterInput.None);
			return run.StepsMet == 1;
		}
		bool any = false;
		for (int p = 1; p <= 40 && !any; p++) any = Try(p);
		Assert.True(any, "some press during the first jump anti-airs the dummy");
		Assert.True(!Try(100), "a press after the jump has landed does not count");
	}

	// ---- the trial on the fight scene, from the rest stop ----

	[Test]
	public static void Scene_TrialPlaysOnTheFightLoop_AndReportsToTheDojo(Node runner)
	{
		var s = AtDojo(500);
		var offer = s.Dojo!.Trials[0];
		Assert.True(s.Dojo.ChooseTrial(offer.Trial.Id).Ok, "chosen");
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		scene.Session = s;
		scene.TrialStop = s.Dojo;
		int finished = 0;
		scene.TrialFinished += () => finished++;
		runner.AddChild(scene);
		try
		{
			Assert.True(scene.Trial is not null, "the scene plays the trial");
			Assert.Equal(offer.Trial.Distance, Math.Abs(scene.Match.P1.X - scene.Match.P2.X) / SimConfig.Scale, "with the trial's setup");
			Assert.True(ReferenceEquals(scene.Trial!.Match, scene.Match), "on the scene's own match");
			int guard = 0;
			while (finished == 0 && guard++ < 3 * offer.Trial.AttemptFrames + 10) scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal(1, finished, "TrialFinished once after the third miss");
			Assert.Equal(3, s.Dojo.Attempts, "every attempt reported to the dojo");
			Assert.Equal(DojoTrialResult.Failed, s.Dojo.TrialResult, "failed");
			Assert.Equal(650, s.Run.Health, "the dojo healed 15%");
		}
		finally { scene.QueueFree(); }
	}

	[Test]
	public static void RestStop_TrialCardIsEnabled()
	{
		var s = AtDojo(500);
		var m = Ui.RestStopScreenModel.Create(s)!;
		Assert.Equal(Ui.RestStopOption.Ready, m.Second, "the dojo's trial card is takeable");
		Assert.True(m.ChooseTrial().Ok && m.ChoiceMade && m.TrialRunning, "the card chooses the offered trial");
		Assert.Equal(Ui.RestStopOption.Closed, m.Free, "the rest closes");
		Assert.Equal(Ui.RestStopOption.Taken, m.Second, "the trial is taken");
		Assert.True(!m.Leave().Ok, "can't leave mid-trial");
		var bare = AtDojo(500);
		bare.Trials = Array.Empty<TrialData>();
		Assert.Equal(Ui.RestStopOption.NoTrial, Ui.RestStopScreenModel.Create(bare)!.Second, "no trial for Ryo's specials: the card says so");
	}
}
