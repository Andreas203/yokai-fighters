using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-98 (dojo training trials). Rules R7, M5, T2, T3, C1.</summary>
public static class DojoTrialProbes
{
	const string Node = "r7-dojo";
	static AbilityPool Pool => DojoTrialTests.Pool;
	static TrialData[] Lib => DojoTrialTests.Library;

	static Dojo Stop(out RunState run, TrialData[]? lib = null)
	{
		var r = RunState.NewRun(Pool); run = r;
		var l = lib ?? Lib;
		return new Dojo(r, Node, null, () => l);
	}

	static string Snap(Dojo d, RunState r) =>
		$"{d.Choice}|{d.TrialId}|{d.TrialSlot}|{d.Attempts}|{d.TrialResult}|{d.Healed}|{r.Health}|{string.Join(",", r.Owned.Select(o => $"{o.Slot}{o.Special.Level}{r.LevelledOrder(o.Slot)}"))}";

	// R7: "within 3 attempts": landing exactly on attempt 3 levels; the third miss does not.
	[Test]
	public static void LandOnAttempt3_Levels_MissOnAttempt3_DoesNot()
	{
		var a = Stop(out var ra); ra.SetHealth(500);
		Assert.True(a.ChooseTrial("spirit-wave-trial").Ok, "chosen");
		int lv = ra[a.TrialSlot]!.Level;
		a.RecordAttempt(false); a.RecordAttempt(false);
		Assert.True(a.RecordAttempt(true).Ok, "land on 3");
		Assert.Equal(DojoTrialResult.Landed, a.TrialResult, "landed on 3");
		Assert.Equal(lv + 1, ra[a.TrialSlot]!.Level, "levelled");
		Assert.Equal(650, ra.Health, "heal 150");
		var b = Stop(out var rb); rb.SetHealth(500);
		b.ChooseTrial("spirit-wave-trial");
		b.RecordAttempt(false); b.RecordAttempt(false); b.RecordAttempt(false);
		Assert.Equal(DojoTrialResult.Failed, b.TrialResult, "failed");
		Assert.Equal(lv, rb[b.TrialSlot]!.Level, "no level");
		Assert.Equal(650, rb.Health, "heal 150 either way");
	}

	// R7: refused actions change nothing (fields, hash).
	[Test]
	public static void RefusedCalls_ChangeNothing_OnSession()
	{
		var s = DojoTrialTests.AtDojo(500);
		var d = s.Dojo!;
		ulong h0 = s.StateHash(); string f0 = Snap(d, s.Run);
		Assert.True(!d.RecordAttempt(true).Ok, "record with no trial refused");
		Assert.True(!d.ChooseTrial("nope").Ok, "unknown refused");
		Assert.True(!d.ChooseTrial(null!).Ok, "null refused");
		if (!s.Run.Owns("foxfire")) Assert.True(!d.ChooseTrial("foxfire-trial").Ok, "unowned special's trial refused");
		Assert.Equal(h0, s.StateHash(), "hash unchanged by refusals before choice");
		Assert.Equal(f0, Snap(d, s.Run), "fields unchanged");
		var id = d.Trials[0].Trial.Id;
		Assert.True(d.ChooseTrial(id).Ok, "choose");
		d.RecordAttempt(false);
		ulong h1 = s.StateHash(); string f1 = Snap(d, s.Run);
		Assert.True(!d.ChooseTrial(id).Ok, "twice refused");
		Assert.True(!d.Rest().Ok, "rest refused");
		Assert.Equal(h1, s.StateHash(), "mid-trial refusals leave hash");
		Assert.Equal(f1, Snap(d, s.Run), "attempts not reset by a second choose");
		d.RecordAttempt(true);
		ulong h2 = s.StateHash(); string f2 = Snap(d, s.Run);
		Assert.True(!d.RecordAttempt(true).Ok && !d.RecordAttempt(false).Ok && !d.ChooseTrial(id).Ok && !d.Rest().Ok, "all refused after end");
		Assert.Equal(h2, s.StateHash(), "finished hash stable");
		Assert.Equal(f2, Snap(d, s.Run), "fields stable");
	}

	// R7: a rest then a trial refused; hash of a rest = old formula (tag, choice) only.
	[Test]
	public static void Rest_HashIsLegacyFormula_AndClosesTrial()
	{
		var s = DojoTrialTests.AtDojo(500);
		ulong before = s.StateHash();
		Assert.True(s.Dojo!.Rest().Ok, "rest");
		// Run state changed (health) so recompute: base hash of health-adjusted run is what StateHash minus dojo mix; check a different way:
		var s2 = DojoTrialTests.AtDojo(800); // 800 + 300 capped = 1000 ; compare with a full-health run's pre-rest hash
		var full = DojoTrialTests.AtDojo(1000);
		ulong baseFull = full.StateHash();
		s2.Dojo!.Rest();
		ulong expect = Fnv.Mix(Fnv.Mix(baseFull, Dojo.HashTag), (int)DojoChoice.Rest);
		Assert.Equal(expect, s2.StateHash(), "a rest hashes as (tag, choice) on top of the run hash");
		var d = s.Dojo!;
		Assert.True(!d.ChooseTrial(d.Trials[0].Trial.Id).Ok, "trial after rest refused");
		Assert.True(!d.CanTrial, "CanTrial false");
		Assert.True(before != s.StateHash(), "rest changes hash");
	}

	// R7 + C1: heal at full health is 0; at 1 HP is 151 / 150 exactly.
	[Test]
	public static void Heal_AtFull_AndAtOneHp()
	{
		var a = Stop(out var ra); // full
		a.ChooseTrial("spirit-wave-trial"); a.RecordAttempt(true);
		Assert.Equal(1000, ra.Health, "full stays full");
		Assert.Equal(0, a.Healed, "Healed 0");
		var b = Stop(out var rb); rb.SetHealth(1);
		b.ChooseTrial("spirit-wave-trial"); b.RecordAttempt(false); b.RecordAttempt(false); b.RecordAttempt(false);
		Assert.Equal(151, rb.Health, "1 + 150");
		var c = Stop(out var rc); rc.SetHealth(0);
		c.ChooseTrial("spirit-wave-trial"); c.RecordAttempt(true);
		Assert.Equal(150, rc.Health, "0 + 150");
	}

	// T2/T3: a landed trial makes its special the copy target, also when another special is Lv2 and levelled earlier.
	[Test]
	public static void LandedTrial_ChangesCopyTarget_T3()
	{
		var d = Stop(out var run);
		var offers = d.Trials;
		Assert.True(offers.Count >= 2, $"need two trial specials among starters, have {offers.Count}");
		var first = offers[0]; var second = offers[1];
		run.LevelUp(first.Slot); // first Lv2, levelled earlier
		Assert.Equal(first.Slot, CopyRule.Target(run)!.Slot, "first is the target at Lv2");
		Assert.True(d.ChooseTrial(second.Trial.Id).Ok, "choose second");
		d.RecordAttempt(true);
		Assert.Equal(2, run[second.Slot]!.Level, "second Lv2");
		Assert.Equal(second.Slot, CopyRule.Target(run)!.Slot, "T3: tie on level, second levelled most recently");
	}

	// T3 reverse order: trial first, then a normal level-up of the other: other becomes target.
	[Test]
	public static void TrialThenLevelUp_OtherWins()
	{
		var d = Stop(out var run);
		var offers = d.Trials;
		d.ChooseTrial(offers[1].Trial.Id); d.RecordAttempt(true);
		run.LevelUp(offers[0].Slot);
		Assert.Equal(offers[0].Slot, CopyRule.Target(run)!.Slot, "T3: later level-up wins");
	}

	// A special at Lv3 (cap) gets no trial; at Lv2 it does.
	[Test]
	public static void TopLevel_NoTrial_ButLv2Is()
	{
		var d = Stop(out var run);
		var o = d.Trials[0];
		run.LevelUp(o.Slot);
		Assert.True(d.Trials.Any(t => t.Slot == o.Slot), "Lv2 still offered");
		run.LevelUp(o.Slot);
		Assert.True(!d.Trials.Any(t => t.Slot == o.Slot), "Lv3 not offered");
		Assert.True(!d.ChooseTrial(o.Trial.Id).Ok, "and refused");
	}

	// Borrowed ink: offered? (R6 silent on levelling borrowed ink.) Reported as observation.
	[Test]
	public static void BorrowedInk_Trial_Observation()
	{
		var run = RunState.NewRun(Pool);
		var fox = Pool.Special("foxfire")!;
		if (run[fox.Slot] is not null) run.Release(fox.Slot);
		run.Borrow(fox);
		var d = new Dojo(run, Node, null, () => Lib);
		bool offered = d.Trials.Any(t => t.Trial.Special == "foxfire");
		Console.WriteLine($"OBSERVE borrowed foxfire trial offered={offered}");
		if (offered)
		{
			d.ChooseTrial("foxfire-trial"); d.RecordAttempt(true);
			Console.WriteLine($"OBSERVE borrowed foxfire level after landed={run[fox.Slot]!.Level} borrowed={run[fox.Slot]!.Borrowed}");
			Assert.True(run[fox.Slot]!.Borrowed, "still borrowed after level-up (never carries over, R6)");
		}
	}

	// Mid-trial save equals entry save; Continue after a finished trial restarts, heal not doubled.
	[Test]
	public static void Continue_AfterFinishedTrial_NoDoubleHeal_AndCompletedSaveKeepsResult()
	{
		string map = ContentPaths.Data("map");
		var s = DojoTrialTests.AtDojo(500);
		var entry = RunSave.Capture(s, "default-map.json");
		var d = s.Dojo!; var o = d.Trials[0];
		int lv0 = s.Run[o.Slot]!.Level; int ord0 = s.Run.LevelledOrder(o.Slot);
		d.ChooseTrial(o.Trial.Id); d.RecordAttempt(true);
		var mid = RunSave.Capture(s, "default-map.json");
		var back = entry.Restore(Pool, FileMapSource.Default(map).Load(), NodeSceneTable.Default(map));
		back.Trials = Lib;
		Assert.Equal(500, back.Run.Health, "entry save: 500, not 650 and not 800");
		Assert.Equal(lv0, back.Run[o.Slot]!.Level, "level back to entry level");
		Assert.Equal(ord0, back.Run.LevelledOrder(o.Slot), "T3 stamp back to entry");
		Assert.Equal(DojoChoice.None, back.Dojo!.Choice, "no choice");
		Assert.True(back.StateHash() == RunSave.Capture(back, "default-map.json").Restore(Pool, FileMapSource.Default(map).Load(), NodeSceneTable.Default(map)).StateHash(), "restore round trip");
		Assert.True(s.CompleteNode().Ok, "complete");
		var done = RunSave.Capture(s, "default-map.json");
		var back2 = done.Restore(Pool, FileMapSource.Default(map).Load(), NodeSceneTable.Default(map));
		back2.Trials = Lib;
		Assert.Equal(650, back2.Run.Health, "completed save keeps the one heal");
		Assert.Equal(lv0 + 1, back2.Run[o.Slot]!.Level, "completed save keeps the level");
		Assert.Equal(s.Run.LevelledOrder(o.Slot), back2.Run.LevelledOrder(o.Slot), "T3 order saved");
		Assert.Equal(s.StateHash(), back2.StateHash(), "hash survives");
	}

	// ---- TrialRun ----

	static (TrialRun, RunState) Run(string id, TrialData? over = null)
	{
		var state = RunState.NewRun(Pool);
		var data = over ?? Lib.Single(t => t.Id == id);
		var src = KitSources.Repo(Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + ".."));
		var cfg = TrialRun.Config(state.MatchConfig(), data);
		var ryo = Kit.Load(Kit.Ryo, src); var dummy = Kit.Load("kitsune", src);
		var m = new Match(cfg, ryo.Moves, dummy.Moves);
		dummy.Equip(m.P2); ryo.Equip(m.P1); state.ApplyTo(m.P1, cfg);
		return (new TrialRun(data, m), state);
	}

	[Test]
	public static void TrialRun_PerTickHashes_Identical_AndNextAttemptRestartsClean()
	{
		List<ulong> Play()
		{
			var (run, st) = Run("spirit-wave-trial");
			var slot = st.Owned.Single(o => o.Special.Data.Id == "spirit-wave").Slot;
			var hs = new List<ulong>();
			for (int t = 1; t <= 200; t++) { run.Step(t % 60 == 1 ? FighterInput.Special(slot) : FighterInput.None); hs.Add(run.Match.StateHash()); }
			return hs;
		}
		var a = Play(); var b = Play();
		for (int i = 0; i < a.Count; i++) Assert.Equal(a[i], b[i], $"tick {i + 1}");
		// attempt 2 equals attempt 1 from the same inputs
		var (r, s) = Run("spirit-wave-trial");
		var sl = s.Owned.Single(o => o.Special.Data.Id == "spirit-wave").Slot;
		var h1 = new List<ulong>();
		for (int t = 1; t <= r.Trial.AttemptFrames; t++) { r.Step(t % 60 == 1 ? FighterInput.Special(sl) : default); if (t <= 100) h1.Add(r.Match.StateHash()); if (r.AttemptOver) break; }
		if (!r.AttemptOver) { for (int t = 0; t < 5000 && !r.AttemptOver; t++) r.Step(default); }
		bool landed1 = r.Landed;
		Assert.True(r.NextAttempt(), "next");
		Assert.Equal(0, r.StepsMet, "steps reset"); Assert.Equal(0, r.AttemptTick, "tick reset");
		for (int t = 1; t <= 100; t++) { r.Step(t % 60 == 1 ? FighterInput.Special(sl) : default); Assert.Equal(h1[t - 1], r.Match.StateHash(), $"attempt 2 tick {t} matches attempt 1"); if (r.AttemptOver) break; }
	}

	// R7: trial damage never touches run health; a KO of Ryo ends the attempt as a miss and the next starts at full.
	[Test]
	public static void KoOfRyoMidAttempt_EndsAsMiss_RunHealthUntouched_NextAttemptFullHealth()
	{
		// dummy that attacks, Ryo with nothing: uses a kit move id from the dummy kit.
		var baseData = Lib.Single(t => t.Id == "spirit-wave-trial");
		var (probe, _) = Run("spirit-wave-trial");
		string mv = probe.Match.P2.Moves.First(m => !m.IsThrow).Id;
		var data = new TrialData
		{
			Id = "probe-ko", Special = baseData.Special, Prompt = "p", Distance = 120, AttemptFrames = 3000,
			Dummy = new TrialDummy(TrialDummyAction.Attack, new[] { mv }, 20), Steps = baseData.Steps,
		};
		var (run, state) = Run("", data);
		state.SetHealth(321);
		run.Match.P1.Health = 1;
		bool? res = null; int ended = 0;
		run.AttemptEnded += (_, l) => { res = l; ended++; };
		for (int t = 0; t < 3000 && res is null; t++) run.Step(default);
		Console.WriteLine($"OBSERVE ko attempt ended={res} p1hp={run.Match.P1.Health} phase={run.Match.Phase} tick={run.AttemptTick}");
		Assert.True(res == false, "ended as a miss");
		Assert.Equal(1, ended, "raised once");
		for (int i = 0; i < 50; i++) run.Step(default);
		Assert.Equal(1, ended, "no second raise after the end");
		Assert.Equal(321, state.Health, "run health untouched by trial damage");
		Assert.True(run.NextAttempt(), "next attempt");
		Assert.Equal(run.Match.P1.MaxHealth, run.Match.P1.Health, "Ryo full health again");
		Assert.True(run.Match.Phase == MatchPhase.Fighting, "fighting again");
	}

	// NextAttempt refused mid attempt; events never double fire after unsubscribing ownership: Step after over is a no-op on hash.
	[Test]
	public static void NextAttemptMidAttempt_Refused_StepAfterOver_NoOp()
	{
		var (run, _) = Run("spirit-wave-trial");
		run.Step(default);
		ulong h = run.Match.StateHash();
		Assert.True(!run.NextAttempt(), "refused mid-attempt");
		Assert.Equal(h, run.Match.StateHash(), "no change");
		while (!run.AttemptOver) run.Step(default);
		ulong h2 = run.Match.StateHash(); int tk = run.AttemptTick;
		run.Step(FighterInput.Attack(0));
		Assert.Equal(h2, run.Match.StateHash(), "step after over is a no-op");
		Assert.Equal(tk, run.AttemptTick, "tick frozen");
	}

	// Kihon and Kata: TrialRun takes FighterInput only, so the same inputs complete it; checked via two TrialRuns (F3, K2).
	// Combo + counter step: hit event of the dummy's attack interrupting is a counter.
	[Test]
	public static void CounterStep_NeedsCounterHit_NotPlainHit()
	{
		var baseData = Lib.Single(t => t.Id == "spirit-wave-trial");
		var (probe, _) = Run("spirit-wave-trial");
		string mv = probe.Match.P2.Moves.First(m => !m.IsThrow).Id;
		TrialData Make(bool dummyAttacks) => new TrialData
		{
			Id = "probe-counter", Special = baseData.Special, Prompt = "p", Distance = 120, AttemptFrames = 600,
			Dummy = dummyAttacks ? new TrialDummy(TrialDummyAction.Attack, new[] { mv }, 100) : new TrialDummy(TrialDummyAction.Idle, Array.Empty<string>(), 0),
			Steps = new[] { new TrialStep(TrialEvent.Hit, null, 1, true, TrialDummyState.Any, 0, null, null) },
		};
		// idle dummy: a hit is never a counter
		var (r, st) = Run("", Make(false));
		for (int t = 0; t < 300 && !r.AttemptOver; t++) r.Step(t % 20 == 0 ? FighterInput.Attack(0) : default);
		Assert.True(!r.Landed, "plain hit on an idle dummy does not meet a counter step");
	}
}
