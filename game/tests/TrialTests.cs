using System;
using System.IO;
using System.Linq;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-105: the dojo trial schema's C# loader (R7). Fixture only; real trials are YOK-98's.</summary>
public static class TrialTests
{
	static string FixturePath => Path.Combine(ContentPaths.FixturesDir, "trials", "test-spirit-wave-trial.json");
	static string Fixture => File.ReadAllText(FixturePath);

	static void Rejects(string json, string expect, string msg)
	{
		try { TrialLoader.Parse(json); }
		catch (FormatException e)
		{
			Assert.True(e.Message.Contains(expect), $"{msg}: message '{e.Message}' should contain '{expect}'");
			return;
		}
		throw new AssertFailed($"{msg}: loaded, expected a FormatException");
	}

	static string Swap(string from, string to)
	{
		string f = Fixture;
		Assert.True(f.Contains(from), $"fixture contains '{from}'");
		return f.Replace(from, to);
	}

	/// <summary>The fixture with a dummy that attacks (block and counterhit steps need one).</summary>
	static string Attacking(string json) => json.Replace("{ \"action\": \"jump\", \"every\": 90 }",
		"{ \"action\": \"attack\", \"moves\": [\"test-jab\", \"test-sweep\"], \"every\": 120 }");

	const string Step2 = "{ \"event\": \"hit\", \"move\": \"test-rising-talisman\", \"dummy_state\": \"airborne\" }";

	[Test]
	public static void Fixture_LoadsEveryField()
	{
		var t = TrialLoader.LoadFile(FixturePath);
		Assert.Equal("test-spirit-wave-trial", t.Id, "id");
		Assert.Equal("test-spirit-wave", t.Special, "special it levels");
		Assert.Equal(3, t.Attempts, "R7: 3 attempts");
		Assert.Equal(600, t.Distance, "start distance");
		Assert.Equal(600, t.AttemptFrames, "attempt length");
		Assert.Equal(new TrialDummy(TrialDummyAction.Jump, Array.Empty<string>(), 90).Action, t.Dummy.Action, "dummy jumps");
		Assert.Equal(90, t.Dummy.Every, "every 90 ticks");
		Assert.Equal(0, t.Dummy.Moves.Count, "a jumping dummy has no string");
		Assert.Equal("Hit the dummy with Spirit Wave at mid range, then anti-air its jump with Rising Talisman.", t.Prompt, "prompt");
		Assert.Equal(TrialSample.Event, t.Steps[0].Sample, "a hit is sampled when it connects");
		Assert.Equal(2, t.Steps.Count, "two steps");
		Assert.Equal(new TrialStep(TrialEvent.Hit, "test-spirit-wave", 1, false, TrialDummyState.Grounded, 0, 400, 900), t.Steps[0], "step 1: Spirit Wave at mid range");
		Assert.Equal(new TrialStep(TrialEvent.Hit, "test-rising-talisman", 1, false, TrialDummyState.Airborne, 0, null, null), t.Steps[1], "step 2: anti-air");
		Assert.True(t.Rules.SequenceEqual(new[] { "R7", "K2" }), "rules");
		Assert.Equal("proposed", t.Status, "status");

		var all = TrialLoader.LoadDirectory(Path.Combine(ContentPaths.FixturesDir, "trials"));
		Assert.Equal(1, all.Length, "fixture folder has the one trial");
		Assert.Equal(0, TrialLoader.LoadDirectory(Path.Combine(Path.GetTempPath(), "no-such-trial-dir")).Length, "absent folder = none");
	}

	[Test]
	public static void MissingField_Rejected()
	{
		Rejects(Swap("\"special\": \"test-spirit-wave\",", ""), "special", "no special");
		Rejects(Swap("\"attempts\": 3,", ""), "attempts", "no attempts");
		Rejects(Swap("\"distance\": 600,", ""), "setup.distance", "no start distance");
		Rejects(Swap("\"attempt_frames\": 600,", ""), "setup.attempt_frames", "no attempt length");
		Rejects(Swap(",\n    \"dummy\": { \"action\": \"jump\", \"every\": 90 }", ""), "setup.dummy", "no dummy");
		Rejects(Swap("\"action\": \"jump\", \"every\": 90", "\"action\": \"jump\""), "setup.dummy.every", "jump without a period");
		Rejects(Swap("\"prompt\": \"Hit the dummy with Spirit Wave at mid range, then anti-air its jump with Rising Talisman.\",", ""), "prompt", "no prompt");
		Rejects(Swap("\"rules\": [\"R7\", \"K2\"],", ""), "rules", "no rules");
		Rejects(Swap("\"event\": \"hit\", \"move\": \"test-rising-talisman\"", "\"move\": \"test-rising-talisman\""), "goal.steps.1.event", "step without an event");
		Rejects(Swap("\"kind\": \"trial\"", "\"kind\": \"special\""), "kind", "wrong kind");
		Rejects(Swap("\"id\": \"test-spirit-wave-trial\",", ""), "id: required", "no id");
		Rejects(Swap("\"status\": \"proposed\",", ""), "status: required", "no status");
		string noSetup = Fixture[..Fixture.IndexOf("\"setup\"", StringComparison.Ordinal)] + Fixture[Fixture.IndexOf("\"goal\"", StringComparison.Ordinal)..];
		Rejects(noSetup, "setup: required object", "no setup");
		string noGoal = Fixture[..Fixture.IndexOf("\"goal\"", StringComparison.Ordinal)] + Fixture[Fixture.IndexOf("\"rules\"", StringComparison.Ordinal)..];
		Rejects(noGoal, "goal: required object", "no goal");
		Rejects(noGoal.Replace("\"rules\"", "\"goal\": {}, \"rules\""), "goal.steps", "no steps");
	}

	[Test]
	public static void GddSampleTrials_FitTheVocabulary()
	{
		// GDD 6.3's other two samples (the fixture is the first). Ids and numbers are made up for the test.
		string Trial(string id, string special, string dummy, string steps) => $$"""
			{ "kind": "trial", "id": "{{id}}", "status": "proposed", "special": "{{special}}", "prompt": "x", "attempts": 3,
			  "setup": { "distance": 300, "attempt_frames": 600, "dummy": {{dummy}} },
			  "goal": { "steps": [ {{steps}} ] }, "rules": ["R7"] }
			""";
		var club = TrialLoader.Parse(Trial("iron-club-trial", "iron-club-charge",
			"{ \"action\": \"attack\", \"moves\": [\"oni-jab\", \"oni-swing\"], \"every\": 150 }",
			"{ \"event\": \"block\", \"count\": 2 }, { \"event\": \"hit\", \"move\": \"iron-club-charge\", \"counter\": true }"));
		Assert.True(club.Steps[0] == new TrialStep(TrialEvent.Block, null, 2, false, TrialDummyState.Any, 0, null, null)
			&& club.Steps[1] == new TrialStep(TrialEvent.Hit, "iron-club-charge", 1, true, TrialDummyState.Any, 0, null, null),
			"block the dummy's string, then punish with the charge before it recovers");
		var grab = TrialLoader.Parse(Trial("river-grab-trial", "river-grab", "{ \"action\": \"jump\", \"every\": 90 }",
			"{ \"event\": \"throw\", \"move\": \"river-grab\", \"dummy_state\": \"landed\", \"landed_within\": 6 }"));
		Assert.True(grab.Steps[0] == new TrialStep(TrialEvent.Throw, "river-grab", 1, false, TrialDummyState.Landed, 6, null, null)
			&& grab.Steps[0].Sample == TrialSample.Grab, "grab the dummy as it lands from a jump");
	}

	[Test]
	public static void BlockAndCounter_NeedADummyThatAttacks()
	{
		// The sim raises a blocked Hit from the dummy, and HitEvent.Counter (C7), only against a dummy in a move.
		foreach (var action in new[] { "{ \"action\": \"idle\" }", "{ \"action\": \"crouch\" }", "{ \"action\": \"guard\" }", "{ \"action\": \"crouch_guard\" }", "{ \"action\": \"jump\", \"every\": 90 }" })
		{
			string With(string step) => Swap(Step2, step).Replace("{ \"action\": \"jump\", \"every\": 90 }", action);
			Rejects(With("{ \"event\": \"block\", \"count\": 3 }"), "block needs a dummy with action 'attack'", $"block vs {action}");
			Rejects(With("{ \"event\": \"hit\", \"move\": \"test-spirit-wave\", \"counter\": true }"), "counterhit needs a dummy with action 'attack'", $"counterhit vs {action}");
			TrialLoader.Parse(With("{ \"event\": \"hit\", \"counter\": false }")); // not asking for one is fine
		}
		var ok = TrialLoader.Parse(Attacking(Swap(Step2, "{ \"event\": \"block\", \"count\": 2 }, { \"event\": \"hit\", \"move\": \"test-rising-talisman\", \"counter\": true }")));
		Assert.Equal(3, ok.Steps.Count, "block the string, then punish: loads against an attacking dummy");
	}

	[Test]
	public static void ThrowStep_IsSampledOnTheGrab_SoThrowAsItLandsCanBeMet()
	{
		var moves = MoveLoader.LoadDirectory(Path.Combine(ContentPaths.FixturesDir, "ryo-normals"))
			.Concat(MoveLoader.LoadDirectory(Path.Combine(ContentPaths.FixturesDir, "throws"))).ToArray();
		int breakWindow = moves.Single(m => m.IsThrow).BreakWindow;
		// P2 jumps on tick 1; P1 throws on every later tick in turn. Gap = sampled tick - P2's last airborne tick.
		int bestGrab = int.MaxValue, bestLand = int.MaxValue;
		for (int press = 20; press <= 60; press++)
		{
			var m = new Match(null, moves, moves);
			m.P1.X = -m.Config.BodyWidth / 2;
			m.P2.X = m.Config.BodyWidth / 2;
			int tick = 0, lastAir = -1000, grabGap = 0;
			m.Throw += (_, e) =>
			{
				if (e.Attacker != 0) return;
				if (e.Outcome == ThrowOutcome.Grab) grabGap = tick - lastAir;
				if (e.Outcome != ThrowOutcome.Land) return;
				bestGrab = Math.Min(bestGrab, grabGap);
				bestLand = Math.Min(bestLand, tick - lastAir);
			};
			for (tick = 1; tick <= press + 30; tick++)
			{
				m.Step(new FighterInput(tick == press ? InputBits.LightPunch | InputBits.LightKick : 0), new FighterInput(tick == 1 ? InputBits.Up : 0));
				if (m.P2.Airborne) lastAir = tick;
			}
		}
		Assert.True(bestGrab <= 3, $"a throw can grab within 3 ticks of the touchdown (best {bestGrab})");
		Assert.Equal(bestGrab + breakWindow + 1, bestLand, "the land is break window + 1 ticks after the grab (C4)");

		var step = TrialLoader.Parse(Swap(Step2, "{ \"event\": \"throw\", \"dummy_state\": \"landed\", \"landed_within\": 3 }")).Steps[1];
		Assert.Equal(TrialSample.Grab, step.Sample, "a throw step reads the dummy on the grab tick");
		Assert.True(step.DummyStateMet(false, bestGrab), "throw as it lands is met on the grab");
		Assert.True(!step.DummyStateMet(false, bestLand), "and could never be met if it were read when the throw lands");
		Assert.True(!step.DummyStateMet(true, 0), "airborne is not landed");
		Assert.True(!step.DummyStateMet(false, 4) && !step.DummyStateMet(false, 2000), "too long ago, or never jumped");
		Assert.True(step.DistanceMet(0) && step.DistanceMet(1520), "no distance asked");
		var ranged = TrialLoader.LoadFile(FixturePath).Steps[0];
		Assert.True(ranged.DistanceMet(400) && ranged.DistanceMet(900) && !ranged.DistanceMet(399) && !ranged.DistanceMet(901), "distance bounds are inclusive");
	}

	[Test]
	public static void Combo_IsTheDummyHeld_NotJustUnableToAct()
	{
		foreach (FighterState s in Enum.GetValues<FighterState>())
		{
			bool held = s is FighterState.Hitstun or FighterState.Knockdown or FighterState.Thrown;
			Assert.Equal(held, TrialStep.ComboHolds(s), $"combo holds in {s}");
		}
		var f = new Fighter();
		f.Reset(0, 1, 1000);
		f.State = FighterState.Jump;
		Assert.True(!f.Actionable && !TrialStep.ComboHolds(f.State), "a dummy that recovered and jumped is not actionable, but the combo is over");
	}

	[Test]
	public static void ThrowsMeetOnlyThrowSteps()
	{
		bool? Kind(string id) => id switch { "test-ryo-throw" => true, "test-spirit-wave" or "test-rising-talisman" => false, _ => null };
		Assert.Equal(0, TrialLoader.LoadFile(FixturePath).MoveProblems(Kind).Count, "the fixture names strikes in hit steps");
		var hitThrow = TrialLoader.Parse(Swap(Step2, "{ \"event\": \"hit\", \"move\": \"test-ryo-throw\" }")).MoveProblems(Kind);
		Assert.True(hitThrow.Count == 1 && hitThrow[0].Contains("goal.steps.1.move") && hitThrow[0].Contains("is a throw"), "a throw never meets a hit step");
		var comboThrow = TrialLoader.Parse(Swap(Step2, "{ \"event\": \"combo\", \"count\": 2, \"move\": \"test-ryo-throw\" }")).MoveProblems(Kind);
		Assert.True(comboThrow.Count == 1 && comboThrow[0].Contains("is a throw"), "nor a combo step");
		var throwStrike = TrialLoader.Parse(Swap(Step2, "{ \"event\": \"throw\", \"move\": \"test-rising-talisman\" }")).MoveProblems(Kind);
		Assert.True(throwStrike.Count == 1 && throwStrike[0].Contains("is not a throw"), "a strike never meets a throw step");
		var unknown = TrialLoader.Parse(Swap(Step2, "{ \"event\": \"hit\", \"move\": \"no-such-move\" }")).MoveProblems(Kind);
		Assert.True(unknown.Count == 1 && unknown[0].Contains("unknown move"), "a move the kit lacks is reported");
		Assert.Equal(0, TrialLoader.Parse(Swap(Step2, "{ \"event\": \"throw\", \"move\": \"test-ryo-throw\" }")).MoveProblems(Kind).Count, "a throw in a throw step is fine");
	}

	[Test]
	public static void Loader_RejectsWhatTheValidatorRejects()
	{
		Rejects(Swap("Hit the dummy with Spirit Wave", new string('x', 161)), "prompt: longer than 160", "a prompt over 160 characters");
		Rejects(Swap("\"special\": \"test-spirit-wave\"", "\"special\": \"Test_Spirit Wave\""), "special: 'Test_Spirit Wave' is not a lowercase kebab-case id", "special id shape");
		Rejects(Swap("\"move\": \"test-rising-talisman\"", "\"move\": \"Rising Talisman\""), "goal.steps.1.move", "move id shape");
		Rejects(Attacking(Fixture).Replace("\"test-sweep\"", "\"Test Sweep\""), "setup.dummy.moves.1", "dummy move id shape");
		Rejects(Attacking(Fixture).Replace("\"test-sweep\"", "7"), "setup.dummy.moves.1", "dummy move not text");
		Rejects(Swap("\"id\": \"test-spirit-wave-trial\"", "\"id\": \"Trial One\""), "id:", "id shape");
		Rejects(Swap("[\"R7\", \"K2\"]", "[\"R7\", \"R7\"]"), "rules.1: 'R7' is listed twice", "duplicate rule");
		Rejects(Swap("[\"R7\", \"K2\"]", "[\"R7\", \"seven\"]"), "rules.1", "not a rule id");
		Rejects(Swap("[\"R7\", \"K2\"]", "[1]"), "rules.0", "a number in rules is a FormatException");
		Rejects(Swap("[\"R7\", \"K2\"]", "[null]"), "rules.0", "a null in rules is a FormatException");
		Rejects(Swap("[\"R7\", \"K2\"]", "\"R7\""), "rules", "rules not a list");
		Rejects(Fixture[..Fixture.IndexOf("\"notes\"", StringComparison.Ordinal)] + "\"notes\": 5 }", "notes", "notes not text");
		Rejects(Swap("\"every\": 90", "\"every\": 601"), "setup.dummy.every", "every beyond 600");
		Rejects(Swap("\"attempt_frames\": 600", "\"attempt_frames\": 60"), "90 is longer than the attempt (60 ticks)", "a jump period longer than the attempt");
		Rejects(Swap("\"min\": 400, \"max\": 900", "\"min\": 900.0, \"max\": 400"), "min 900 is greater than max 400", "900.0 is the whole number 900");
		try { Assert.Equal(3, TrialLoader.Parse(Swap("\"attempts\": 3,", "\"attempts\": 3, \"attempts\": 3,")).Attempts, "a field written twice reads once"); }
		catch (FormatException) { } // or is refused, but never with another exception type
		Rejects("[]", "", "not an object");
		Rejects("null", "", "null");
		Rejects("{", "", "not JSON");

		string dir = Path.Combine(Path.GetTempPath(), "yok105-trials-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try
		{
			string path = Path.Combine(dir, "other-name.json");
			File.WriteAllText(path, Fixture);
			bool threw = false;
			try { TrialLoader.LoadFile(path); }
			catch (FormatException e) { threw = e.Message.Contains("must equal the file name 'other-name'") && e.Message.Contains(path); }
			Assert.True(threw, "id must equal the file name, and the error names the file");
			threw = false;
			try { TrialLoader.LoadDirectory(dir); } catch (FormatException) { threw = true; }
			Assert.True(threw, "LoadDirectory reports it too");
		}
		finally { Directory.Delete(dir, true); }
	}

	[Test]
	public static void AttemptsOtherThanThree_Rejected()
	{
		foreach (var n in new[] { "0", "1", "2", "4", "99", "-3", "3.5", "\"3\"" })
			Rejects(Swap("\"attempts\": 3", $"\"attempts\": {n}"), "attempts", $"attempts {n} (R7 fixes 3)");
		var three = TrialLoader.Parse(Swap("\"attempts\": 3", "\"attempts\": 3.0"));
		Assert.Equal(3, three.Attempts, "3.0 is the whole number 3, as the schema validator reads it");
	}

	[Test]
	public static void GoalNeedingKataInput_Rejected()
	{
		Rejects(Swap(Step2, "{ \"event\": \"hit\", \"move\": \"test-rising-talisman\", \"motion\": \"623\" }"), "Kihon-completable", "a motion is Kata only (K1)");
		Rejects(Swap(Step2, "{ \"event\": \"hit\", \"move\": \"test-rising-talisman\", \"precision\": true }"), "Kihon-completable", "the precision bonus is Kata only (K1)");
		Rejects(Swap(Step2, "{ \"event\": \"hit\", \"move\": \"test-rising-talisman\", \"kata\": { \"motion\": \"623\" } }"), "Kihon-completable", "a Kata input block");
		Rejects(Swap(Step2, "{ \"event\": \"hit\", \"move\": \"test-rising-talisman\", \"button\": \"HP\" }"), "unknown field", "a button is not an observation");
		Rejects(Swap(Step2, "{ \"event\": \"press\", \"move\": \"test-rising-talisman\" }"), "unknown event", "an input event");
	}

	[Test]
	public static void GoalVocabulary_ParsesAndRejectsImpossibleSteps()
	{
		string Steps(string s) => Swap(Step2, s);
		var combo = TrialLoader.Parse(Steps("{ \"event\": \"combo\", \"count\": 3, \"move\": \"test-rising-talisman\" }")).Steps[1];
		Assert.Equal(TrialEvent.Combo, combo.Event, "combo");
		Assert.Equal(3, combo.Count, "of 3");
		var counter = TrialLoader.Parse(Attacking(Steps("{ \"event\": \"hit\", \"counter\": true }"))).Steps[1];
		Assert.True(counter.Counter && counter.Move is null, "counterhit with any move (C7)");
		var grab = TrialLoader.Parse(Steps("{ \"event\": \"throw\", \"dummy_state\": \"landed\", \"landed_within\": 6 }")).Steps[1];
		Assert.Equal(TrialDummyState.Landed, grab.DummyState, "throw as it lands");
		Assert.Equal(6, grab.LandedWithin, "within 6 ticks");
		var block = TrialLoader.Parse(Attacking(Steps("{ \"event\": \"block\", \"count\": 3 }"))).Steps[1];
		Assert.Equal(TrialEvent.Block, block.Event, "block the dummy's string");
		var attack = TrialLoader.Parse(Attacking(Fixture));
		Assert.True(attack.Dummy.Action == TrialDummyAction.Attack && attack.Dummy.Moves.SequenceEqual(new[] { "test-jab", "test-sweep" }), "dummy string");

		Rejects(Steps("{ \"event\": \"combo\", \"count\": 1 }"), "combo needs count 2", "a one-hit combo");
		Rejects(Steps("{ \"event\": \"combo\" }"), "combo needs count 2", "a combo with no count");
		Rejects(Steps("{ \"event\": \"throw\", \"dummy_state\": \"airborne\" }"), "C4", "throwing an airborne dummy can never happen");
		Rejects(Steps("{ \"event\": \"throw\", \"counter\": true }"), "counterhit", "a counterhit throw can never happen");
		Rejects(Steps("{ \"event\": \"block\", \"move\": \"test-rising-talisman\" }"), "not for block", "block names no player move");
		Rejects(Steps("{ \"event\": \"hit\", \"dummy_state\": \"landed\" }"), "landed_within", "landed without a window");
		Rejects(Steps("{ \"event\": \"hit\", \"dummy_state\": \"airborne\", \"landed_within\": 4 }"), "only with dummy_state 'landed'", "window without landed");
		Rejects(Steps("{ \"event\": \"hit\", \"distance\": { \"min\": 900, \"max\": 400 } }"), "greater than max", "min > max");
		Rejects(Steps("{ \"event\": \"hit\", \"distance\": {} }"), "give min, max or both", "empty distance");
		Rejects(Swap("\"move\": \"test-spirit-wave\", ", ""), "no step names the trial's special", "the trial must land its own special");
		Rejects(Swap("\"distance\": 600,", "\"distance\": 100,"), "setup.distance", "closer than the body width");
		Rejects(Swap("\"distance\": 600,", "\"distance\": 1600,"), "setup.distance", "beyond max separation");
		Rejects(Swap("\"every\": 90", "\"every\": 90, \"moves\": [\"test-jab\"]"), "only for attack", "moves on a jumping dummy");
		Rejects(Swap("\"action\": \"jump\"", "\"action\": \"attack\""), "setup.dummy.moves", "attack with no string");
		Rejects(Swap("\"status\": \"proposed\",", "\"status\": \"proposed\", \"heal_pct\": 15,"), "unknown field", "R7's heal is not data");
	}
}
