using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-211 probes: TwistScroll.Show trims the rule label; a blank one hides the red rule line (E1 label optional).</summary>
public static class TwistLabelBreakerProbes
{
	static StoryCard Card(Node host)
	{
		var card = GD.Load<PackedScene>("res://scenes/ui/story_card.tscn").Instantiate<StoryCard>();
		host.AddChild(card);
		return card;
	}

	[Test]
	public static void RuleLabel_AnyWhitespaceHides_PaddingIsTrimmed_InnerTextKept(Node host)
	{
		var card = Card(host);
		var scroll = card.Scroll!;
		bool Shown() => scroll.GetNode<Control>("TextCol/Rule").Visible || scroll.GetNode<Control>("TextCol/RuleLine").Visible;
		bool Both() => scroll.GetNode<Control>("TextCol/Rule").Visible && scroll.GetNode<Control>("TextCol/RuleLine").Visible;
		foreach (string blank in new[] { "\r\n", "\u00A0", "\u3000", " \t\r\n ", "\n" })
		{
			card.Display("q", "ELDER TWIST · ROW 5", "twist-armour", "E", "Elder Oni", blank, null);
			Assert.True(!Shown(), $"blank label {blank.Length} chars: no rule line");
			Assert.Equal("", scroll.RuleText, "blank label leaves no text behind");
		}
		foreach (var (label, want) in new[] { ("\tArmour on every special\r\n", "Armour on every special"), ("\u00A0Armour\u00A0", "Armour"), ("Armour on\nevery special", "Armour on\nevery special"), (" a ", "a") })
		{
			card.Display("q", "ELDER TWIST · ROW 5", "twist-armour", "E", "Elder Oni", label, null);
			Assert.True(Both(), $"'{label}': rule line shows");
			Assert.Equal(want, scroll.RuleText, "trimmed outside only");
		}
		// state does not leak: a label, then a blank, then null, then a label again
		card.Display("q", "k", "twist-armour", "E", "Elder Oni", "X", null);
		card.Display("q", "k", "twist-armour", "E", "Elder Oni", "  ", null);
		Assert.True(!Shown() && scroll.RuleText == "", "label then blank: hidden, text cleared");
		card.Display("q", "k", "twist-armour", "E", "Elder Oni", null, null);
		Assert.True(!Shown(), "null: hidden");
		card.Display("q", "k", "twist-armour", "E", "Elder Oni", " Y ", null);
		Assert.True(Both() && scroll.RuleText == "Y", "shown again");
		card.QueueFree();
	}
}
