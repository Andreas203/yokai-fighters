using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// Test Breaker round 2 for YOK-221: the loader half of ExactnessProbes in tools/tests/test_forward_travel_probes.py.
/// Parity with the validator on edge values, as .NET decimal reads them (designer ruling 2026-11-02).
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

	// Designer ruling 2026-11-02: a whole number is what .NET decimal reads (rounds past ~28 digits, tiny exponents are 0).
	// Each row is the loader's verdict (true = accepted); tools/tests/test_forward_travel_probes.py DECIMAL_VALUES carries
	// the same rows (less 1e999999999, which hangs its validator today) and checks the validator gives the same verdict.
	static readonly (string raw, bool accepted)[] Values =
	{
		("300.00000000000000000000000000000000", true), ("300.00000000000000000000000000000001", true),
		("3.0000000000000000000000000000001e2", true), ("0.0000000000000000000000000000000001", true),
		("-0.0000000000000000000000000000000000001", true), ("1e-400", true), ("0e999999999", true), ("1e999999999", false),
		("2147483647.0", true), ("2147483647.5", false), ("2147483648.0", false),
		("2147483647.0000000000000000000000000000000001", true),
		("-0.0e5", true), ("1E2", true), ("1e+2", true), ("0.3e3", true), ("3000e-1", true),
		("300.0000000000000000001", false), ("-0.5", false), ("1e400", false),
	};

	[Test]
	public static void Loader_AndValidatorAgreeOnLongAndExtremeNumbers()
	{
		var wrong = new List<string>();
		foreach (var (raw, accepted) in Values)
		{
			bool ok;
			try { MoveLoader.Parse(Special(raw)); ok = true; }
			catch (FormatException) { ok = false; }
			catch (System.Text.Json.JsonException) { ok = false; }
			if (ok != accepted) wrong.Add($"{raw}: loader {(ok ? "accepts" : "refuses")}, the shared list says {(accepted ? "accepts" : "refuses")}");
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

/// <summary>
/// Test Breaker round 3 for YOK-221: tools/validate_data.py <c>dotnet_decimal</c> against .NET's own
/// <c>JsonElement.TryGetDecimal</c> (what MoveLoader's WholeInt and Effects.Whole read; designer ruling 2026-11-02).
/// ForwardTravelDecimalGolden.json holds [text, value-or-null] for every text in ForwardTravelDecimalCases.txt (96-bit
/// mantissa edge, half-to-even at the 28th digit, negatives, exponent forms, seeded random digit strings), taken from
/// .NET; this test checks the golden still is what .NET gives, and test_dotnet_decimal_matches_the_loader_golden checks Python.
/// </summary>
public static class ForwardTravelDecimalBreakerProbes
{
	static string Dir => Godot.ProjectSettings.GlobalizePath("res://tests/probes/");

	public static string Read(string text)
	{
		using var doc = System.Text.Json.JsonDocument.Parse(text);
		return doc.RootElement.TryGetDecimal(out decimal d) ? d.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null";
	}

	[Test]
	public static void Golden_IsWhatDotNetReadsForEveryCase()
	{
		var lines = System.IO.File.ReadAllLines(Dir + "ForwardTravelDecimalCases.txt").Where(l => l.Length > 0).ToArray();
		var golden = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(Dir + "ForwardTravelDecimalGolden.json")).RootElement;
		Assert.Equal(lines.Length, golden.GetArrayLength(), "one golden row per case");
		var wrong = new List<string>();
		int i = 0;
		foreach (var row in golden.EnumerateArray())
		{
			string text = row[0].GetString()!, want = row[1].ValueKind == System.Text.Json.JsonValueKind.Null ? "null" : row[1].GetString()!;
			if (text != lines[i++] || Read(text) != want) wrong.Add($"{text}: golden {want}, .NET {Read(text)}");
		}
		Assert.True(wrong.Count == 0, string.Join("; ", wrong.Take(5)));
	}

	// One-off generator (run once as a test with -- only=Golden_Write to make the golden); not in the suite.
	public static void Golden_Write()
	{
		var rows = System.IO.File.ReadAllLines(Dir + "ForwardTravelDecimalCases.txt").Where(l => l.Length > 0)
			.Select(t => new System.Text.Json.Nodes.JsonArray(t, Read(t) == "null" ? null : (System.Text.Json.Nodes.JsonNode)Read(t))).ToArray();
		var arr = new System.Text.Json.Nodes.JsonArray(rows.Select(r => (System.Text.Json.Nodes.JsonNode?)r).ToArray());
		System.IO.File.WriteAllText(Dir + "ForwardTravelDecimalGolden.json", arr.ToJsonString());
	}
}
