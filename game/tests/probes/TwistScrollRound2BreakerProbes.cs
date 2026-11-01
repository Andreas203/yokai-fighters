using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-109 round 2 test-breaker probes: a long LEGAL intro quote (story-card.schema text maxLength 280, S2), 720p, no portrait, missing label, long names, repeated Show.</summary>
public static class TwistScrollRound2BreakerProbes
{
	const float Box = TwistScroll.QuoteBoxHeight;

	// Legal two-sentence texts of 150..280 characters, built from ordinary words.
	static string Text(int chars)
	{
		const string seed = "The old fox watched the apprentice climb the shrine steps and said nothing. Every tail he had lost was a promise kept, and Ryo would learn what that cost before the lanterns burned out. ";
		string s = string.Concat(Enumerable.Repeat(seed, 3));
		return s[..chars].TrimEnd();
	}

	static StoryCard Card(Node host)
	{
		var card = GD.Load<PackedScene>("res://scenes/ui/story_card.tscn").Instantiate<StoryCard>();
		host.AddChild(card);
		return card;
	}

	static float Needed(TwistScroll s, int size)
	{
		var q = UiFind.Get<Label>(s, "Quote");
		return q.GetThemeFont("font").GetMultilineStringSize(q.Text, HorizontalAlignment.Left, s.TextColumn.Size.X, size).Y;
	}

	// S2 / story-card.schema (text maxLength 280) + V1 (nothing cut off): a legal card of N characters must show all its text:
	// at the font the scroll settled on, the wrapped height must fit the 130 px quote box (Label.ClipText hides the rest silently).
	static void QuoteFits(Node host, int chars, bool portrait)
	{
		var card = Card(host);
		card.Display(Text(chars), "ELDER TWIST · ROW 5", "twist-armour", "E", "Elder Oni", "r", portrait ? FightHud.PortraitPath("kitsune") : null);
		var s = card.Scroll!;
		float h = Needed(s, s.QuoteFontSize);
		bool fits = h <= Box;
		bool signalled = s.TextFits == false; // the only signal the scroll has; it must at least not claim a fit
		card.QueueFree();
		Assert.True(fits, chars + " chars " + (portrait ? "with" : "without") + " portrait: quote needs " + h + " px at " + s.QuoteFontSize + " px, box is " + Box + " px, text is cut off (TextFits=" + !signalled + ")");
	}

	[Test] public static void Quote_180Chars_WithPortrait_ShowsAllText(Node host) => QuoteFits(host, 180, true);
	[Test] public static void Quote_230Chars_WithPortrait_ShowsAllText(Node host) => QuoteFits(host, 230, true);
	[Test] public static void Quote_280Chars_WithPortrait_ShowsAllText(Node host) => QuoteFits(host, 280, true);
	[Test] public static void Quote_280Chars_NoPortrait_ShowsAllText(Node host) => QuoteFits(host, 280, false);

	// Honesty: whatever the font, TextFits must not say true while the text is cut at the floor.
	[Test]
	public static void Quote_280Chars_TextFitsDoesNotLie(Node host)
	{
		var card = Card(host);
		card.Display(Text(280), "K", null, "E", "Elder Oni", null, FightHud.PortraitPath("kitsune"));
		var s = card.Scroll!;
		bool really = Needed(s, s.QuoteFontSize) <= Box;
		Assert.Equal(really, s.TextFits, "TextFits vs a real measure of the quote");
		card.QueueFree();
	}

	// Repeated Show: long, short, long, short: the size follows the current text, never the previous one.
	[Test]
	public static void Show_LongThenShortThenLong_SizeFollowsCurrentText(Node host)
	{
		var card = Card(host);
		var s = card.Scroll!;
		string longTitle = "The Exceedingly Honourable Nine-Tailed Fox Spirit Of The Hundred Shrines";
		card.Display(Text(280), "K", null, "E", longTitle, "r", FightHud.PortraitPath("kitsune"));
		int longQ = s.QuoteFontSize, longT = s.TitleFontSize;
		card.Display("Short.", "K", null, "E", "Oni", "r", FightHud.PortraitPath("kitsune"));
		Assert.Equal(TwistScroll.QuoteSize, s.QuoteFontSize, "quote size back to 34 after a long one");
		Assert.Equal(TwistScroll.TitleSize, s.TitleFontSize, "title size back to 72 after a long one");
		card.Display(Text(280), "K", null, "E", longTitle, "r", FightHud.PortraitPath("kitsune"));
		Assert.Equal(longQ, s.QuoteFontSize, "same long text gives the same quote size the second time");
		Assert.Equal(longT, s.TitleFontSize, "same long title gives the same title size the second time");
		card.QueueFree();
	}

	// Width changes between portrait and no portrait: a long quote shown with a portrait then without (wider) must re-fit to the wider column.
	[Test]
	public static void Show_PortraitThenNoPortrait_RefitsToTheWiderColumn(Node host)
	{
		var card = Card(host);
		var s = card.Scroll!;
		card.Display(Text(230), "K", null, "E", "Elder Oni", null, FightHud.PortraitPath("kitsune"));
		int withP = s.QuoteFontSize;
		card.Display(Text(230), "K", null, "E", "Elder Oni", null, null);
		Assert.True(s.QuoteFontSize >= withP, "wider column: size " + s.QuoteFontSize + " is not smaller than " + withP);
		var fresh = Card(host);
		fresh.Display(Text(230), "K", null, "E", "Elder Oni", null, null);
		Assert.Equal(fresh.Scroll!.QuoteFontSize, s.QuoteFontSize, "same text, same layout, same size however it got there");
		card.QueueFree(); fresh.QueueFree();
	}

	// A long elder name with no portrait: the whole name must fit the 1080 px column or the 44 px floor + ellipsis (title ellipsis is set).
	[Test]
	public static void Title_VeryLongName_NoPortrait_FloorAndEllipsis(Node host)
	{
		var card = Card(host);
		string name = string.Concat(Enumerable.Repeat("Nine-Tailed ", 12)).Trim();
		card.Display("q", "K", null, "E", name, null, null);
		var s = card.Scroll!;
		var t = UiFind.Get<Label>(s, "Title");
		Assert.Equal(TwistScroll.TitleSizeMin, s.TitleFontSize, "floor reached");
		Assert.True(t.TextOverrunBehavior == TextServer.OverrunBehavior.TrimEllipsis && t.ClipText, "ends in an ellipsis, never leaves the paper");
		Assert.True(!s.TextFits, "TextFits says false for a name that is cut");
		card.QueueFree();
	}

	// Missing label (null rule) after a card that had one: rule hidden, no stale text; then a label again: shown.
	[Test]
	public static void Rule_NullThenBack_NoStaleRuleLine(Node host)
	{
		var card = Card(host);
		var s = card.Scroll!;
		var line = UiFind.Get<Control>(s, "RuleLine");
		card.Display("q", "K", null, "E", "Elder", "Throws never break", null);
		Assert.True(line.Visible, "rule shown");
		card.Display("q", "K", null, "E", "Elder", null, null);
		Assert.True(!line.Visible && s.RuleText == "", "rule gone");
		card.Display("q", "K", null, "E", "Elder", "Again", null);
		Assert.True(line.Visible && s.RuleText == "Again", "rule back");
		card.QueueFree();
	}

	// 720p: the canvas is 1920x1080 stretched (canvas_items), so the scroll's layout in canvas units must not change when the window is 1280x720.
	[Test]
	public static void Window720p_SameLayoutAndFontSizes(Node host)
	{
		var win = host.GetWindow();
		var old = win.Size;
		var card = Card(host);
		card.Display(Text(280), "K", null, "E", "Elder Oni", "r", FightHud.PortraitPath("kitsune"));
		var s = card.Scroll!;
		int q1080 = s.QuoteFontSize; var col1080 = s.TextColumn; var size1080 = s.Size;
		try
		{
			win.Size = new Vector2I(1280, 720);
			card.Display(Text(280), "K", null, "E", "Elder Oni", "r", FightHud.PortraitPath("kitsune"));
			Assert.Equal(q1080, s.QuoteFontSize, "quote size at 720p");
			Assert.True(s.TextColumn == col1080, "column at 720p " + s.TextColumn + " vs " + col1080);
			Assert.True(s.Size == size1080, "scroll size at 720p " + s.Size + " vs " + size1080);
		}
		finally { win.Size = old; card.QueueFree(); }
	}
}
