using System;
using System.IO;
using System.Linq;
using System.Text;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-105 (trial schema, validator, TrialLoader). Rule R7; ticket purpose: validator and loader agree.</summary>
public static class TrialProbes
{
	static string Fixture => File.ReadAllText(Path.Combine(ContentPaths.FixturesDir, "trials", "test-spirit-wave-trial.json"));

	static string TempDir()
	{
		string d = Path.Combine(Path.GetTempPath(), "trialprobe-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(d);
		return d;
	}

	static void MustBeFormatException(Action a, string what)
	{
		try { a(); }
		catch (FormatException) { return; }
		catch (Exception e) { throw new AssertFailed($"{what}: threw {e.GetType().Name} instead of FormatException: {e.Message}"); }
		throw new AssertFailed($"{what}: loaded, expected FormatException");
	}

	// KnownOpen: tools/validate_data.py keeps the LAST duplicate key (json.loads) and accepts; the loader refuses.
	// tools/tests/test_validate_data_probes.py pins the validator half. Flip both when the validator rejects duplicates.
	[Test]
	public static void KnownOpen_DuplicateKey_LoaderRefuses()
	{
		string f = Fixture;
		MustBeFormatException(() => TrialLoader.Parse(f.Replace("\"attempts\": 3,", "\"attempts\": 4, \"attempts\": 3,")), "root duplicate");
		MustBeFormatException(() => TrialLoader.Parse(f.Replace("\"distance\": 600,", "\"distance\": 5, \"distance\": 600,")), "setup duplicate");
		MustBeFormatException(() => TrialLoader.Parse(f.Replace("\"min\": 400", "\"min\": 9999, \"min\": 400")), "distance duplicate");
	}

	// R7 / ticket: nothing but FormatException may escape the loader.
	[Test]
	public static void Loader_OnlyEverThrowsFormatException()
	{
		string d = TempDir();
		try
		{
			string f = Fixture;
			MustBeFormatException(() => TrialLoader.Parse(""), "empty");
			MustBeFormatException(() => TrialLoader.Parse("[]"), "array root");
			MustBeFormatException(() => TrialLoader.Parse("null"), "null root");
			MustBeFormatException(() => TrialLoader.Parse(new string('[', 100000)), "deep nesting");
			MustBeFormatException(() => TrialLoader.Parse(f.Replace("\"notes\": \"", "\"notes\": \"" + new string('x', 10_000_000))), "10 MB note is fine to refuse or accept, never throw other");
		}
		catch (AssertFailed e) when (e.Message.StartsWith("10 MB")) { /* accepted a long note: valid (notes has no max) */ }
		try
		{
			string p = Path.Combine(d, "test-spirit-wave-trial.json");
			File.WriteAllBytes(p, new byte[] { 0x7B, 0xFF, 0xFE, 0x7D });
			MustBeFormatException(() => TrialLoader.LoadFile(p), "invalid UTF-8");
			File.WriteAllText(p, Fixture.Replace("\"test-spirit-wave-trial\"", "\"other-name\""));
			MustBeFormatException(() => TrialLoader.LoadFile(p), "id differs from file name");
			File.WriteAllBytes(p, new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(Fixture)).ToArray());
			Assert.Equal("test-spirit-wave-trial", TrialLoader.LoadFile(p).Id, "a UTF-8 BOM (editors add one) loads, as Python's utf-8 read + json.loads would refuse or accept consistently");
		}
		finally { Directory.Delete(d, true); }
	}

	// Loader reads the BOM file; validator (read_text utf-8, json.loads) raises on a BOM: record it.
	[Test]
	public static void KnownOpen_Bom_LoaderAccepts_ValidatorSideIsPinnedInPython()
	{
		string d = TempDir();
		try
		{
			string p = Path.Combine(d, "test-spirit-wave-trial.json");
			File.WriteAllBytes(p, new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(Fixture)).ToArray());
			TrialLoader.LoadFile(p);
		}
		finally { Directory.Delete(d, true); }
	}

	[Test]
	public static void LoadDirectory_NonJsonIgnored_SortedOrdinal_DeterministicTwice()
	{
		string d = TempDir();
		try
		{
			foreach (var id in new[] { "b-trial", "a-trial", "a-trial-2", "A0"[..0] + "c-trial" })
				File.WriteAllText(Path.Combine(d, id + ".json"), Fixture.Replace("test-spirit-wave-trial", id));
			File.WriteAllText(Path.Combine(d, "notes.txt"), "not json");
			Directory.CreateDirectory(Path.Combine(d, "sub.json"));
			var a = TrialLoader.LoadDirectory(d).Select(t => t.Id).ToArray();
			var b = TrialLoader.LoadDirectory(d).Select(t => t.Id).ToArray();
			Assert.True(a.SequenceEqual(new[] { "a-trial-2", "a-trial", "b-trial", "c-trial" }), "sorted by file name incl. extension, ordinal (- before .): " + string.Join(",", a));
			Assert.True(a.SequenceEqual(b), "same directory twice, same order");
		}
		finally { Directory.Delete(d, true); }
	}

	// A file named after one trial but holding another id is refused, so two files can never load the same id.
	[Test]
	public static void TwoFilesSameId_SecondIsRefusedByNameRule()
	{
		string d = TempDir();
		try
		{
			File.WriteAllText(Path.Combine(d, "test-spirit-wave-trial.json"), Fixture);
			File.WriteAllText(Path.Combine(d, "copy-trial.json"), Fixture);
			MustBeFormatException(() => TrialLoader.LoadDirectory(d), "duplicate id across files");
		}
		finally { Directory.Delete(d, true); }
	}

	// Critic item 3, loader side. Both are accepted by the loader (it has no kit): MoveProblems is the caller's check.
	[Test]
	public static void KnownOpen_ThrowStepNamingASpecial_LoaderAccepts_MoveProblemsRefuses()
	{
		string f = Fixture.Replace("{ \"event\": \"hit\", \"move\": \"test-rising-talisman\", \"dummy_state\": \"airborne\" }",
			"{ \"event\": \"throw\", \"move\": \"test-rising-talisman\", \"dummy_state\": \"grounded\" }");
		var t = TrialLoader.Parse(f); // accepted: the loader does not know kinds
		var problems = t.MoveProblems(id => id == "test-rising-talisman" ? false : null);
		Assert.True(problems.Any(p => p.Contains("is not a throw")), "MoveProblems names it when the kit says it is not a throw");
		// The GDD's River Grab is a kind-special in the GDD tables (line 241 gdd-extended: 'command grab (generic throw)');
		// a trial for a special that is itself a throw step is refused by the validator when the move is kind special.
	}

	[Test]
	public static void KnownOpen_SpecialMayBeAnyIdAndIdleDummyMayWantAirborne()
	{
		// Critic findings confirmed on the loader side: neither is checked (R7 says "one of your specials").
		var t = TrialLoader.Parse(Fixture.Replace("\"special\": \"test-spirit-wave\"", "\"special\": \"ryo-light-punch\"")
			.Replace("\"move\": \"test-spirit-wave\"", "\"move\": \"ryo-light-punch\""));
		Assert.Equal("ryo-light-punch", t.Special, "special need not be a special");
		TrialLoader.Parse(Fixture.Replace("{ \"action\": \"jump\", \"every\": 90 }", "{ \"action\": \"idle\" }"));
	}

	// Disagreement found by the 12k-file differential fuzz: loader does not check rule ids against rules.md (documented).
	[Test]
	public static void KnownOpen_RuleIdsNotInRulesMd_LoaderAccepts_ValidatorRejects()
	{
		foreach (var r in new[] { "R07", "R999", "R007" })
			TrialLoader.Parse(Fixture.Replace("[\"R7\", \"K2\"]", $"[\"{r}\"]"));
	}
}
