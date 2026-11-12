using System;
using System.IO;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-226: trial event <c>cast</c>, for a special that never hits (Fox Mirage's teleport, 0 damage, R7). The trial
/// JSON is a TEST FIXTURE; the special is the real data/moves/fox-mirage.json.
/// </summary>
public static class TrialCastTests
{
	static string Root => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");

	static string Trial(string steps) => $$"""
	{ "kind": "trial", "id": "test-cast-trial", "status": "proposed", "special": "fox-mirage",
	  "prompt": "TEST FIXTURE: cast the mirage.", "attempts": 3,
	  "setup": { "distance": 400, "attempt_frames": 120, "dummy": { "action": "idle" } },
	  "goal": { "steps": [ {{steps}} ] }, "rules": ["R7"] }
	""";

	static TrialRun Run(string steps)
	{
		var t = TrialLoader.Parse(Trial(steps));
		var mirage = SpecialLoader.LoadFile(Path.Combine(Root, "data", "moves", "fox-mirage.json"));
		var m = new Match(TrialRun.Config(SimConfig.Default, t));
		var run = new TrialRun(t, m);
		m.P1.Equip(mirage.Slot, mirage, 1);
		m.P1.Meter = 300;
		return run;
	}

	[Test]
	public static void Cast_CountsEachStartOfTheNamedSpecial_OnceAUse_EvenWithAnExUpgrade()
	{
		var mirage = SpecialLoader.LoadFile(Path.Combine(Root, "data", "moves", "fox-mirage.json"));
		var r = Run("""{ "event": "cast", "move": "fox-mirage", "count": 2 }""");
		r.Step(FighterInput.Special(mirage.Slot));
		Assert.Equal(0, r.StepsMet, "one cast of two");
		for (int i = 0; i < 40 && !r.Match.P1.Actionable; i++) r.Step(FighterInput.None);
		r.Step(FighterInput.Special(mirage.Slot, ex: true));
		Assert.Equal(1, r.StepsMet, "the second cast meets the step");
		Assert.True(r.Landed, "the trial lands");

		// The wrong special, or none, never counts.
		var other = Run("""{ "event": "cast", "move": "foxfire" }, { "event": "cast", "move": "fox-mirage" }""");
		other.Step(FighterInput.Special(mirage.Slot));
		for (int i = 0; i < 40; i++) other.Step(FighterInput.None);
		Assert.Equal(0, other.StepsMet, "fox-mirage is not foxfire");

		// A distance sampled on the start tick.
		var far = Run("""{ "event": "cast", "move": "fox-mirage", "distance": { "min": 500 } }""");
		far.Step(FighterInput.Special(mirage.Slot));
		Assert.Equal(0, far.StepsMet, "cast from 400 units: min 500 not met");
	}

	[Test]
	public static void Cast_NeedsAMove_AndTakesNoCounter()
	{
		foreach (string bad in new[] { """{ "event": "cast" }""", """{ "event": "cast", "move": "fox-mirage", "counter": true }""" })
		{
			try { TrialLoader.Parse(Trial(bad)); }
			catch (FormatException) { continue; }
			throw new AssertFailed($"{bad}: expected FormatException");
		}
	}
}
