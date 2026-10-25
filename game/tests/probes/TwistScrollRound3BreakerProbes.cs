using System;
using System.Globalization;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-109 round 3 test-breaker probes: the quote trim path (QuoteTrimmed, "…"), unbroken words, emoji and combining marks at the cut, repeated Show, 720p.</summary>
public static class TwistScrollRound3BreakerProbes
{
	const float Box = TwistScroll.QuoteBoxHeight;
	const string Sentence = "The old fox watched the apprentice climb the shrine steps and said nothing. ";

	static string Rep(string unit, int chars)
	{
		string t = string.Concat(Enumerable.Repeat(unit, chars / unit.Length + 1))[..chars];
		if (char.IsHighSurrogate(t[^1])) t = t[..^1]; // valid text only: never a lone surrogate in the input
		return t.TrimEnd();
	}

	static StoryCard Card(Node host)
	{
		var card = GD.Load<PackedScene>("res://scenes/ui/story_card.tscn").Instantiate<StoryCard>();
		host.AddChild(card);
		return card;
	}

	static Label Q(TwistScroll s) => UiFind.Get<Label>(s, "Quote");

	static (float W, float H) Measure(TwistScroll s)
	{
		var q = Q(s);
		var sz = q.GetThemeFont("font").GetMultilineStringSize(q.Text, HorizontalAlignment.Left, s.TextColumn.Size.X, s.QuoteFontSize);
		return (sz.X, sz.Y);
	}

	static string Portrait(bool p) => p ? FightHud.PortraitPath("kitsune") : "";

	// V1 / S2: past the schema limit, the card keeps a readable prefix of the quote: ends in "…", is a prefix of the original,
	// fits the box, is raised as QuoteTrimmed, and the rule line and footer stay on the paper.
	static void TrimmedProperly(Node host, string text, bool portrait)
	{
		var card = Card(host);
		card.Display(text, "K", null, "E", "Elder Oni", "A rule", Portrait(portrait));
		var s = card.Scroll!;
		string shown = s.Quote;
		(float w, float h) = Measure(s);
		string why = text.Length + " chars, portrait " + portrait + ", shown " + shown.Length + " chars at " + s.QuoteFontSize + " px, " + w + "x" + h + " px, column " + s.TextColumn.Size.X;
		Assert.True(s.QuoteTrimmed, "QuoteTrimmed raised: " + why);
		Assert.True(shown.EndsWith("…"), "ends in an ellipsis: " + why);
		string prefix = shown[..^1];
		Assert.True(text.StartsWith(prefix), "shown text is a prefix of the original: " + why);
		Assert.True(prefix.Length > 20, "more than a stub survives: " + why);
		Assert.True(h <= Box, "trimmed quote fits the box: " + why);
		Assert.True(w <= s.TextColumn.Size.X + 0.5f, "trimmed quote inside the column: " + why);
		Assert.True(!s.TextFits, "TextFits is false when trimmed");
		var rule = UiFind.Get<Control>(s, "Rule"); var footer = s.Footer;
		Assert.True(rule.GetGlobalRect().End.Y <= footer.GetGlobalRect().Position.Y + 0.5f, "rule label stays above the footer: " + why);
		card.QueueFree();
	}

	[Test] public static void Trim_700Chars_WithPortrait(Node host) => TrimmedProperly(host, Rep(Sentence, 700), true);
	[Test] public static void Trim_700Chars_NoPortrait(Node host) => TrimmedProperly(host, Rep(Sentence, 700), false);

	// S2: legal 280 characters, with and without portrait, is shown whole: not trimmed, same text, fits.
	[Test]
	public static void Legal280Chars_NeverTrimmed(Node host)
	{
		var bad = new System.Collections.Generic.List<string>();
		foreach (bool p in new[] { true, false })
			foreach (string unit in new[] { Sentence, "ii l i l ", "Kitsune-no-yōkai, the nine-tailed wanderer; " })
			{
				var card = Card(host);
				string text = Rep(unit, 280);
				card.Display(text, "K", null, "E", "Elder Oni", "A rule", Portrait(p));
				var s = card.Scroll!;
				var q = Q(s);
				float need = q.GetThemeFont("font").GetMultilineStringSize(text, HorizontalAlignment.Left, s.TextColumn.Size.X, TwistScroll.QuoteSizeMin).Y;
				if (s.QuoteTrimmed || s.Quote != text || !s.TextFits) bad.Add("'" + unit.Trim() + "' portrait " + p + ": trimmed=" + s.QuoteTrimmed + ", whole text needs " + need + " px at the 28 px floor, box " + Box + ", shown " + s.Quote.Length + " of 280 chars");
				card.QueueFree();
			}
		Assert.True(bad.Count == 0, string.Join(" | ", bad));
	}

	// V1: one unbroken word (no break opportunity) inside a legal 280-character quote: it must not run past the column
	// (Label.ClipText hides the overflow silently) or it must be signalled (QuoteTrimmed / TextFits false).
	[Test]
	public static void UnbrokenWord_InsideLegalQuote_IsNotSilentlyCut(Node host)
	{
		foreach (int len in new[] { 60, 100, 280 })
			foreach (bool p in new[] { true, false })
			{
				var card = Card(host);
				string text = len == 280 ? new string('a', 280) : "He said " + new string('a', len) + " and left.";
				card.Display(text, "K", null, "E", "Elder Oni", null, Portrait(p));
				var s = card.Scroll!;
				(float w, float h) = Measure(s);
				bool visuallyWhole = w <= s.TextColumn.Size.X + 0.5f && h <= Box && s.Quote == text;
				bool signalled = s.QuoteTrimmed && !s.TextFits;
				Assert.True(visuallyWhole || signalled, "unbroken " + len + " (portrait " + p + "): text is " + w + "x" + h + " px at " + s.QuoteFontSize + " px in a " + s.TextColumn.Size.X + " px column, trimmed=" + s.QuoteTrimmed + ", TextFits=" + s.TextFits + ", quote len " + s.Quote.Length);
				card.QueueFree();
			}
	}

	// Emoji, ZWJ sequences and combining marks at the cut: the cut falls between text elements (no lone surrogate, no mark that starts the tail).
	[Test]
	public static void Trim_EmojiAndCombiningMarks_NeverSplitAnElement(Node host)
	{
		string[] units = { "ab \U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466 ", "ab \U0001F1EF\U0001F1F5 ", "abc a\u0301\u0302\u0303 ", "ab \U0001F98A ", "ééé ", "\U0001F468‍\U0001F469‍\U0001F467‍\U0001F466 ", "\U0001F1EF\U0001F1F5 ", "á̂̃̄ ", "\U0001F98A fox " };
		foreach (string unit in units)
			for (int extra = 0; extra < 6; extra++)
			{
				var card = Card(host);
				string text = new string('x', extra) + Rep(unit, 700);
				card.Display(text, "K", null, "E", "Elder Oni", null, FightHud.PortraitPath("kitsune"));
				var s = card.Scroll!;
				string shown = s.Quote;
				if (!s.QuoteTrimmed) { card.QueueFree(); continue; } // glyphs the font lacks measure as nothing: not a trim case
				Assert.True(shown.EndsWith("…"), "unit " + System.Text.Json.JsonSerializer.Serialize(unit) + ": trimmed with an ellipsis");
				string prefix = shown[..^1];
				Assert.True(text.StartsWith(prefix), "prefix of the original");
				Assert.True(YokaiFighters.Ui.TextCuts.Cuts(text).Contains(prefix.Length), "cut at a text-element boundary: " + prefix.Length + " (extra " + extra + ", unit " + System.Text.Json.JsonSerializer.Serialize(unit) + ")");
				Assert.True(prefix.Length == 0 || !char.IsLowSurrogate(shown[0]), "no lone surrogate");
				Assert.True(prefix.Length == 0 || !char.IsHighSurrogate(prefix[^1]), "last char before the ellipsis is not a lone high surrogate");
				card.QueueFree();
			}
	}

	// Long then short: QuoteTrimmed cleared, text restored in full, sizes and rule line position back to the short card's.
	[Test]
	public static void Show_LongThenShort_ClearsQuoteTrimmed_AndMatchesAFreshScroll(Node host)
	{
		var card = Card(host); var fresh = Card(host);
		var s = card.Scroll!;
		card.Display(Rep(Sentence, 700), "K", null, "E", "Elder Oni", "Rule", Portrait(true));
		Assert.True(s.QuoteTrimmed, "precondition: trimmed");
		card.Display("Short.", "K", null, "E", "Elder Oni", "Rule", Portrait(true));
		fresh.Display("Short.", "K", null, "E", "Elder Oni", "Rule", Portrait(true));
		var f = fresh.Scroll!;
		Assert.True(!s.QuoteTrimmed, "QuoteTrimmed cleared");
		Assert.Equal("Short.", s.Quote, "short text shown whole, not the old prefix");
		Assert.True(s.TextFits, "TextFits true again");
		Assert.Equal(f.QuoteFontSize, s.QuoteFontSize, "quote size");
		Assert.True(UiFind.Get<Control>(s, "RuleLine").Position == UiFind.Get<Control>(f, "RuleLine").Position, "rule line position matches a fresh scroll: " + UiFind.Get<Control>(s, "RuleLine").Position + " vs " + UiFind.Get<Control>(f, "RuleLine").Position);
		Assert.True(UiFind.Get<Control>(s, "Rule").Position == UiFind.Get<Control>(f, "Rule").Position, "rule label position matches a fresh scroll");
		card.QueueFree(); fresh.QueueFree();
	}

	// Same trimmed card twice and after a different card in between: the same text (deterministic, no dependence on history).
	[Test]
	public static void Trim_SameInputTwice_SameShownText(Node host)
	{
		var card = Card(host);
		var s = card.Scroll!;
		string text = Rep(Sentence, 700);
		card.Display(text, "K", null, "E", "Elder Oni", "r", Portrait(true));
		string first = s.Quote;
		card.Display(Rep("Wide Wwww ", 700), "K", null, "E", "Elder Oni", "r", Portrait(false));
		card.Display(text, "K", null, "E", "Elder Oni", "r", Portrait(true));
		Assert.Equal(first, s.Quote, "same shown text");
		card.QueueFree();
	}

	// The rule line sits under the quote's real height and never inside the quote, whatever the length.
	[Test]
	public static void RuleLine_NeverOverlapsTheQuote(Node host)
	{
		var card = Card(host);
		var s = card.Scroll!;
		var line = UiFind.Get<Control>(s, "RuleLine");
		foreach (int n in new[] { 5, 60, 114, 180, 280, 700 })
		{
			card.Display(Rep(Sentence, n), "K", null, "E", "Elder Oni", "Rule", Portrait(n % 2 == 0));
			(_, float h) = Measure(s);
			Assert.True(line.Position.Y >= Q(s).Position.Y + Mathf.Min(h, Box) - 0.5f, n + " chars: rule line y " + line.Position.Y + " is above the end of the quote (" + (Q(s).Position.Y + h) + ")");
		}
		card.QueueFree();
	}

	// 720p with and without a portrait: layout in canvas units and the trim result are unchanged.
	[Test]
	public static void Window720p_TrimAndLayoutUnchanged_WithAndWithoutPortrait(Node host)
	{
		var win = host.GetWindow(); var old = win.Size;
		try
		{
			foreach (bool p in new[] { true, false })
				foreach (int n in new[] { 280, 700 })
				{
					var card = Card(host);
					var s = card.Scroll!;
					string text = Rep(Sentence, n);
					win.Size = old;
					card.Display(text, "K", null, "E", "Elder Oni", "Rule", Portrait(p));
					string shown = s.Quote; int qs = s.QuoteFontSize; bool tr = s.QuoteTrimmed; var col = s.TextColumn; var size = s.Size;
					var rl = UiFind.Get<Control>(s, "RuleLine").Position;
					win.Size = new Vector2I(1280, 720);
					card.Display(text, "K", null, "E", "Elder Oni", "Rule", Portrait(p));
					string tag = n + " chars portrait " + p + " at 720p";
					Assert.Equal(shown, s.Quote, tag + ": shown text");
					Assert.Equal(qs, s.QuoteFontSize, tag + ": quote size");
					Assert.True(tr == s.QuoteTrimmed, tag + ": trimmed flag");
					Assert.True(s.TextColumn == col && s.Size == size, tag + ": column and size");
					Assert.True(UiFind.Get<Control>(s, "RuleLine").Position == rl, tag + ": rule line");
					card.QueueFree();
				}
		}
		finally { win.Size = old; }
	}
}
