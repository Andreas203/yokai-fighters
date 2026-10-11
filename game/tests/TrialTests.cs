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
	public static void GoalVocabulary_ObservationsTheSimMakes()
	{
		string Steps(string s) => Swap(Step2, s);
		var combo = TrialLoader.Parse(Steps("{ \"event\": \"combo\", \"count\": 3, \"move\": \"test-rising-talisman\" }")).Steps[1];
		Assert.Equal(TrialEvent.Combo, combo.Event, "combo");
		Assert.Equal(3, combo.Count, "of 3");
		var counter = TrialLoader.Parse(Steps("{ \"event\": \"hit\", \"counter\": true }")).Steps[1];
		Assert.True(counter.Counter && counter.Move is null, "counterhit with any move (C7)");
		var grab = TrialLoader.Parse(Steps("{ \"event\": \"throw\", \"dummy_state\": \"landed\", \"landed_within\": 6 }")).Steps[1];
		Assert.Equal(TrialDummyState.Landed, grab.DummyState, "throw as it lands");
		Assert.Equal(6, grab.LandedWithin, "within 6 ticks");
		var block = TrialLoader.Parse(Steps("{ \"event\": \"block\", \"count\": 3 }")).Steps[1];
		Assert.Equal(TrialEvent.Block, block.Event, "block the dummy's string");
		var attack = TrialLoader.Parse(Swap("{ \"action\": \"jump\", \"every\": 90 }", "{ \"action\": \"attack\", \"moves\": [\"test-jab\", \"test-sweep\"], \"every\": 120 }"));
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
