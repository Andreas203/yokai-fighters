using System;
using System.Collections.Generic;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// Test Breaker round 2 for YOK-221: the loader half of ExactnessProbes in tools/tests/test_forward_travel_probes.py.
/// The expected verdict is the whole-number rule (a number is whole when its written value is an integer in int32,
/// so 300.00000000000000000000000000000001 is a fraction whatever the parser's precision), not what either side does.
/// </summary>
public static class ForwardTravelExactBreakerProbes
{
	static string Special(string travel) => $$"""
		{
		  "kind": "special", "id": "bp-exact", "name": "TEST FIXTURE: exact travel (not game content)", "status": "proposed",
		  "source": "oni", "starter": false, "rarity": "common", "locked": false, "clip": "oni-heavy-punch", "rules": ["A10"],
		  "input": { "kata": { "slot": "B", "motion": "623" }, "kihon": { "slot": "B", "direction": "forward" } },
		  "card": { "plain": "Test.", "frames": "Test." },
		  "levels": { "1": { "frame_data": { "startup": 12, "active": 6, "recovery": 14, "damage": 110, "hitstun": 20, "blockstun": 14,
		      "hitboxes": [], "hurtboxes": [{ "frames": [1, 32], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } }],
		      "properties": { "forward_travel": {{travel}} } } } }
		}
		""";

	static readonly (string raw, bool whole)[] Values =
	{
		("300.00000000000000000000000000000000", true), ("300.00000000000000000000000000000001", false),
		("3.0000000000000000000000000000001e2", false), ("0.0000000000000000000000000000000001", false),
		("-0.0000000000000000000000000000000000001", false), ("1e-400", false), ("0e999999999", true), ("1e999999999", false),
		("2147483647.0", true), ("2147483647.5", false), ("2147483648.0", false),
		("2147483647.0000000000000000000000000000000001", false),
		("-0.0e5", true), ("1E2", true), ("1e+2", true), ("0.3e3", true), ("3000e-1", true),
	};

	// Not one probe per value: a failing value names itself in the message (F3: the loader reads the number as written).
	[Test]
	public static void Loader_JudgesLongAndExtremeNumbersByTheirWrittenValue()
	{
		var wrong = new List<string>();
		foreach (var (raw, whole) in Values)
		{
			bool ok;
			try { MoveLoader.Parse(Special(raw)); ok = true; }
			catch (FormatException) { ok = false; }
			catch (System.Text.Json.JsonException) { ok = false; }
			if (ok != whole) wrong.Add($"{raw}: loader {(ok ? "accepts" : "refuses")}, a whole number is {whole}");
		}
		Assert.True(wrong.Count == 0, string.Join("; ", wrong));
	}

	[Test]
	public static void Loader_RefusesNaNAndInfinityLiterals()
	{
		foreach (var raw in new[] { "NaN", "Infinity", "-Infinity" })
		{
			bool ok;
			try { MoveLoader.Parse(Special(raw)); ok = true; }
			catch (FormatException) { ok = false; }
			catch (System.Text.Json.JsonException) { ok = false; }
			Assert.True(!ok, $"{raw} is not a number of units");
		}
	}
}
