using System;
using System.Collections.Generic;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-211 probes: RunMapView.HabitSentence and TwistScroll.Show's label trim. The rule behind the expectations is the
/// ticket's: the habit is the first sentence of the description (sentence ends at . ! ? followed by space and a new sentence;
/// not at an abbreviation, a decimal or an ellipsis); empty = null; no end = the whole text.
/// </summary>
public static class HabitSentenceBreakerProbes
{
	static string S(string? s) => s ?? "<null>";

	// YOK-211 ticket: whitespace of any kind between sentences ends the first one; empty and blank text is no habit.
	[Test]
	public static void HabitSentence_TabNewlineAndBlankInputs()
	{
		foreach (var (text, want) in new (string, string?)[]
		{
			("", null), (" ", null), ("\t", null), ("\r\n \t", null), ("\u00A0", null),
			("Jumps.\nHit her.", "Jumps."), ("Jumps.\tHit her.", "Jumps."), ("Jumps.\r\nHit her.", "Jumps."),
			("Jumps!\nHit her.", "Jumps!"), ("Jumps?\nHit her.", "Jumps?"),
			("  \n Jumps a lot.\n\nHit her. ", "Jumps a lot."),
			("!", "!"), ("?", "?"), (".", "."), ("...", "..."), ("?!", "?!"),
			("Waits...", "Waits..."), ("Waits... then strikes", "Waits... then strikes"), ("No terminator at all", "No terminator at all"),
			("Lv. 2 hits hurt. Block.", "Lv. 2 hits hurt."), ("LV. 2 hits hurt. Block.", "LV. 2 hits hurt."),
			("Grabs at 1.5x range. Break it.", "Grabs at 1.5x range."), ("Grabs at 1.5. Break it.", "Grabs at 1.5."), ("Grabs at v2.0.1 range. Break it.", "Grabs at v2.0.1 range."),
			("Jumps (a lot!) then grabs. Block.", "Jumps (a lot!) then grabs."), ("Grabs [60%]. Block.", "Grabs [60%]."),
			("Shouts \u201CHa!\u201D then jumps. Punish.", "Shouts \u201CHa!\u201D then jumps."),
			("Shouts 'Ha!' then jumps. Punish.", "Shouts 'Ha!' then jumps."),
			("Jumps. 3 hits follow.", "Jumps."), ("Jumps.  \t Hit.", "Jumps."),
			("Jumps.Hit her. Then block.", "Jumps.Hit her."), // no space after the stop: not an end
		})
			Assert.Equal(S(want), S(RunMapView.HabitSentence(text)), $"'{text.Replace("\n", "\\n").Replace("\t", "\\t").Replace("\r", "\\r")}'");
	}

	// Property over every string up to length 6 of a small hostile alphabet: never throws, null only for blank text, the
	// result is a non-empty trimmed prefix of the trimmed input, and a second pass changes nothing.
	[Test]
	public static void HabitSentence_NeverThrows_ResultIsATrimmedPrefix_AndIdempotent()
	{
		char[] alpha = { 'a', 'A', ' ', '.', '!', '?', '"', '\n', '1', ')' };
		var buf = new List<char>();
		int n = 0;
		void Walk(int depth)
		{
			string text = new(buf.ToArray());
			string d = text.Trim();
			string? got;
			try { got = RunMapView.HabitSentence(text); }
			catch (Exception e) { throw new AssertFailed($"'{text.Replace("\n", "\\n")}' threw {e.GetType().Name}"); }
			if (d.Length == 0) Assert.True(got == null, $"blank '{text.Replace("\n", "\\n")}' must be null, got '{got}'");
			else
			{
				Assert.True(got != null && got.Length > 0 && d.StartsWith(got, StringComparison.Ordinal), $"'{text.Replace("\n", "\\n")}' gave '{got}', not a prefix of the trimmed text");
				Assert.True(got!.Trim() == got, $"'{text.Replace("\n", "\\n")}' gave '{got}' with padding");
				Assert.Equal(got, S(RunMapView.HabitSentence(got)), $"'{text.Replace("\n", "\\n")}' -> '{got}' is not stable");
			}
			n++;
			if (depth == 6) return;
			foreach (char c in alpha) { buf.Add(c); Walk(depth + 1); buf.RemoveAt(buf.Count - 1); }
		}
		Walk(0);
		Assert.True(n > 100000, $"strings walked ({n})");
	}

	// YOK-211: the shipped descriptions give the same line the ". " split gave (no data edit changes a map line silently).
	// (Author test covers this too; kept as a bound: every shipped habit line is one sentence, ends in a stop, has no newline.)
	[Test]
	public static void HabitSentence_ShippedProfiles_AreOneLineEndingInAStop()
	{
		foreach (var p in RunMapView.Profiles!)
		{
			string? h = RunMapView.HabitSentence(p.Habit.Description);
			Assert.True(h != null && !h.Contains('\n') && (h.EndsWith('.') || h.EndsWith('!') || h.EndsWith('?')), $"{p.Id}: '{h}'");
		}
	}
}
