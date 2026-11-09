using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-109 round 4 test-breaker probes: 260-character intro prose at 720p, over-wide words anywhere in the quote, the overflow edge, repeated Show.</summary>
public static class TwistScrollRound4BreakerProbes
{
	const float Box = TwistScroll.QuoteBoxHeight;
	const string Sentence = "The old fox watched the apprentice climb the shrine steps and said nothing. ";

	static string Rep(string unit, int chars)
	{
		string t = string.Concat(Enumerable.Repeat(unit, chars / unit.Length + 1))[..chars];
		if (char.IsHighSurrogate(t[^1])) t = t[..^1];
		return t.TrimEnd();
	}

	static StoryCard Card(Node host)
	{
		var card = GD.Load<PackedScene>("res://scenes/ui/story_card.tscn").Instantiate<StoryCard>();
		host.AddChild(card);
		return card;
	}

	static string Portrait(bool p) => p ? FightHud.PortraitPath("kitsune") : "";

	static Vector2 Measure(TwistScroll s)
	{
		var q = UiFind.Get<Label>(s, "Quote");
		return q.GetThemeFont("font").GetMultilineStringSize(q.Text, HorizontalAlignment.Left, s.TextColumn.Size.X, s.QuoteFontSize);
	}

	// What is on screen must be whole, or visibly shortened and flagged; never wider or taller than the box.
	static string Verdict(TwistScroll s, string original)
	{
		var m = Measure(s);
		string ctx = " [" + original.Length + " chars -> " + s.Quote.Length + ", " + m.X + "x" + m.Y + " px at " + s.QuoteFontSize + " px, column " + s.TextColumn.Size.X + ", box " + Box + ", trimmed " + s.QuoteTrimmed + ", fits " + s.TextFits + "]";
		if (m.Y > Box || m.X > s.TextColumn.Size.X + 0.5f) return "shown quote overflows the box or column" + ctx;
		if (s.QuoteTrimmed)
		{
			if (!s.Quote.EndsWith("…") || !original.StartsWith(s.Quote[..^1])) return "trimmed quote is not an ellipsis-ended prefix" + ctx;
			if (s.TextFits) return "TextFits true on a trimmed quote" + ctx;
		}
		else
		{
			if (s.Quote != original) return "quote changed without QuoteTrimmed" + ctx;
			if (!s.TextFits) return "whole quote but TextFits false" + ctx;
		}
		return "";
	}

	// Y3 + schema (-intro cards <= 260): ordinary 260-character prose is shown whole with a portrait, and the same at 720p.
	[Test]
	public static void Intro260Chars_WithPortrait_WholeAtBothResolutions(Node host)
	{
		var win = host.GetWindow(); var old = win.Size;
		var bad = new System.Collections.Generic.List<string>();
		try
		{
			foreach (string unit in new[] { Sentence, "Kitsune-no-yōkai, the nine-tailed wanderer; ", "ii l i l ", "Ryo, an apprentice exorcist, binds the spirits he defeats. ", "Nine tails stir behind the mask: \"Show me a trick once, little exorcist.\" " })
				foreach (var size in new[] { old, new Vector2I(1280, 720) })
				{
					win.Size = size;
					var card = Card(host);
					string text = Rep(unit, 260);
					card.Display(text, "ELDER TWIST · ROW 8", "twist-reflect", "E", "Nine-Tailed Kitsune", "A rule label", Portrait(true));
					string v = Verdict(card.Scroll!, text);
					if (v != "" || card.Scroll.QuoteTrimmed) bad.Add("'" + unit[..12] + "' at " + size + ": " + (v == "" ? "trimmed although legal" : v));
					card.QueueFree();
				}
		}
		finally { win.Size = old; }
		Assert.True(bad.Count == 0, string.Join(" | ", bad));
	}

	// V1: one over-wide word at the start, in the middle and at the end of the quote: whole, or shortened and flagged; never silently clipped.
	[Test]
	public static void OverWideWord_StartMiddleEnd_NeverSilentlyClipped(Node host)
	{
		var bad = new System.Collections.Generic.List<string>();
		string w60 = new string('a', 60), w200 = new string('m', 200);
		string[] words = { w60, w200 };
		foreach (string word in words)
			foreach (bool p in new[] { true, false })
				foreach (string text in new[] { word + " and then he left the shrine.", "He said " + word + " and left.", "He left the shrine without a word to " + word })
				{
					var card = Card(host);
					card.Display(text, "K", null, "E", "Elder Oni", "Rule", Portrait(p));
					string v = Verdict(card.Scroll!, text);
					if (v != "") bad.Add("word " + word.Length + " portrait " + p + " '" + text[..Math.Min(20, text.Length)] + "...': " + v);
					card.QueueFree();
				}
		Assert.True(bad.Count == 0, string.Join(" | ", bad));
	}

	// Edge sweep: grow a quote one word at a time (and an unbroken word one letter at a time): at every length the verdict holds,
	// and once the quote is trimmed, adding more text never un-trims it. A quote that exactly fills the box is whole.
	[Test]
	public static void OverflowEdge_SweepByWords_AndByLetters_IsConsistent(Node host)
	{
		var bad = new System.Collections.Generic.List<string>();
		foreach (bool p in new[] { true, false })
		{
			bool seenTrim = false; int firstTrim = -1;
			for (int n = 1; n <= 80; n++)
			{
				var card = Card(host);
				string text = string.Join(" ", Enumerable.Repeat("fox", n));
				card.Display(text, "K", null, "E", "Elder Oni", "Rule", Portrait(p));
				var s = card.Scroll!;
				string v = Verdict(s, text);
				if (v != "") bad.Add("words " + n + " portrait " + p + ": " + v);
				if (s.QuoteTrimmed) { if (!seenTrim) firstTrim = n; seenTrim = true; }
				else if (seenTrim) bad.Add("words " + n + " portrait " + p + ": untrimmed again after trimming at " + firstTrim);
				if (s.QuoteTrimmed && s.QuoteFontSize != TwistScroll.QuoteSizeMin) bad.Add("words " + n + ": trimmed before the font reached the floor (" + s.QuoteFontSize + ")");
				card.QueueFree();
			}
			seenTrim = false; firstTrim = -1;
			for (int n = 20; n <= 90; n++)
			{
				var card = Card(host);
				string text = "He said " + new string('a', n);
				card.Display(text, "K", null, "E", "Elder Oni", "Rule", Portrait(p));
				var s = card.Scroll!;
				string v = Verdict(s, text);
				if (v != "") bad.Add("letters " + n + " portrait " + p + ": " + v);
				if (s.QuoteTrimmed) { if (!seenTrim) firstTrim = n; seenTrim = true; }
				else if (seenTrim) bad.Add("letters " + n + " portrait " + p + ": untrimmed again after trimming at " + firstTrim);
				card.QueueFree();
			}
		}
		Assert.True(bad.Count == 0, string.Join(" | ", bad.Take(6)) + (bad.Count > 6 ? " (+" + (bad.Count - 6) + " more)" : ""));
	}

	// Long, short, long again, then the long one on a different layout and back: nothing carries over.
	[Test]
	public static void Show_LongShortLong_IdenticalToTheFirstLong(Node host)
	{
		var card = Card(host);
		var s = card.Scroll!;
		var rl = UiFind.Get<Control>(s, "RuleLine"); var rule = UiFind.Get<Control>(s, "Rule");
		string[] longs = { Rep(Sentence, 700), "He said " + new string('a', 120) + " and left." };
		foreach (string lng in longs)
		{
			card.Display(lng, "K", null, "E", "Elder Oni", "Rule", Portrait(true));
			string q1 = s.Quote; int f1 = s.QuoteFontSize; bool t1 = s.QuoteTrimmed; var rl1 = rl.Position; var ru1 = rule.Position;
			card.Display("Short.", "K", null, "E", "Elder Oni", "Rule", Portrait(true));
			Assert.True(!s.QuoteTrimmed && s.Quote == "Short." && s.TextFits && s.QuoteFontSize == TwistScroll.QuoteSize, "short card is clean after a trimmed one");
			card.Display(lng, "K", null, "E", "Elder Oni", "Rule", Portrait(true));
			Assert.Equal(q1, s.Quote, "same shown text the second time");
			Assert.Equal(f1, s.QuoteFontSize, "same size");
			Assert.True(t1 == s.QuoteTrimmed && rl.Position == rl1 && rule.Position == ru1, "same flag and rule position");
		}
		card.QueueFree();
	}
}
