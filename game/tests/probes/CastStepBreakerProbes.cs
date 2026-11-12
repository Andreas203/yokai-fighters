using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-226 Test Breaker probes: the trial step <c>cast</c> over every shipped special (R7).</summary>
public static class CastStepBreakerProbes
{
	static string Root => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");

	static TrialRun Run(SpecialData sp, string step, int level = 1)
	{
		var t = TrialLoader.Parse($$"""
		{ "kind": "trial", "id": "probe-cast-trial", "status": "proposed", "special": "{{sp.Id}}",
		  "prompt": "PROBE FIXTURE.", "attempts": 3,
		  "setup": { "distance": 400, "attempt_frames": 300, "dummy": { "action": "idle" } },
		  "goal": { "steps": [ {{step}} ] }, "rules": ["R7"] }
		""");
		var m = new Match(TrialRun.Config(SimConfig.Default, t));
		var run = new TrialRun(t, m);
		m.P1.Equip(sp.Slot, sp, level);
		m.P1.Meter = 300;
		return run;
	}

	// R7: every shipped special, command grabs included, can be the object of a cast step: a plain start counts once,
	// and an EX start counts once (the EX move's id must not make it a different move).
	[Test]
	public static void Cast_EveryShippedSpecial_PlainAndExStart_CountOnce()
	{
		var specials = SpecialLoader.LoadDirectory(Path.Combine(Root, "data", "moves"));
		Assert.True(specials.Any(s => s.Id == "river-grab"), "setup: a command grab exists");
		foreach (var sp in specials)
		{
			string step = $$"""{ "event": "cast", "move": "{{sp.Id}}" }""";
			var plain = Run(sp, step);
			plain.Step(FighterInput.Special(sp.Slot));
			Assert.Equal(1, plain.StepsMet, $"{sp.Id}: a plain start meets the cast step");
			if (!sp.HasEx) continue;
			var ex = Run(sp, step);
			ex.Step(FighterInput.Special(sp.Slot, ex: true));
			Assert.Equal(1, ex.StepsMet, $"{sp.Id}: an EX start meets the cast step");
		}
	}

	// R7: a plain start upgraded to EX on the next tick is one use, so count 2 is not met by it.
	[Test]
	public static void Cast_PlainStartUpgradedToEx_IsOneUse()
	{
		var sp = SpecialLoader.LoadFile(Path.Combine(Root, "data", "moves", "fox-mirage.json"));
		var r = Run(sp, """{ "event": "cast", "move": "fox-mirage", "count": 2 }""");
		foreach (var inp in InputParserTests.Record("2 5 2+LK")) r.Step(inp); // Kata 22 + LK: the plain Fox Mirage
		r.Step(new FighterInput(InputBits.Down | InputBits.LightKick | InputBits.MediumKick)); // second kick one tick late
		Assert.True(r.Match.P1.ActiveEx, "setup: the second kick upgraded the cast to EX");
		Assert.Equal(0, r.StepsMet, "the upgrade is the same use: 1 cast of 2");
	}
}
