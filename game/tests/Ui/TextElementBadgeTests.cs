using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-205: the first-letter badge on the reward card, modifier-target card, shop tag and shop slot icon is the whole
/// first text element, never half a surrogate pair or a letter stripped of its combining mark (YOK-162).
/// </summary>
public static class TextElementBadgeTests
{
	const string Astral = "\U0001D504";                              // MATHEMATICAL FRAKTUR CAPITAL A, a surrogate pair
	const string Combined = "é";                                // e + combining acute
	const string Zwj = "\U0001F468‍\U0001F469‍\U0001F467"; // family emoji, one element of 8 code units

	// name, expected badge (ToUpperInvariant of the first element)
	static readonly (string Name, string Badge)[] Cases =
	{
		(Astral + "bc", Astral), (Combined + "xyz", Combined.ToUpperInvariant()), (Zwj + " Fox", Zwj), ("Spirit Wave", "S"), ("é", "É"),
	};

	static void NoLoneSurrogate(string glyph, string what)
	{
		for (int i = 0; i < glyph.Length; i++)
		{
			if (char.IsHighSurrogate(glyph[i])) Assert.True(i + 1 < glyph.Length && char.IsLowSurrogate(glyph[i + 1]), $"{what}: unpaired high surrogate in '{glyph}'");
			if (char.IsLowSurrogate(glyph[i])) Assert.True(i > 0 && char.IsHighSurrogate(glyph[i - 1]), $"{what}: unpaired low surrogate in '{glyph}'");
		}
	}

	[Test]
	public static void Helper_TakesWholeFirstElement_AndSurvivesEmpty()
	{
		foreach (var (n, b) in Cases) Assert.Equal(b, TextCuts.Badge(n), $"badge of '{n}'");
		Assert.Equal("?", TextCuts.Badge(""), "empty name");
		Assert.Equal("?", TextCuts.Badge(null), "null name");
		Assert.Equal("+", TextCuts.Badge("", "+"), "custom empty mark");
		Assert.Equal(TextCuts.Cuts("a" + Astral).Length, CopyTalisman.Cuts("a" + Astral).Length, "CopyTalisman.Cuts forwards");
	}

	[Test]
	public static void RewardCard_BadgeIsFirstTextElement(Node host)
	{
		var c = GD.Load<PackedScene>("res://scenes/ui/reward_card.tscn").Instantiate<RewardCard>();
		host.AddChild(c);
		var crest = UiFind.Get<SealStamp>(c, "Crest");
		foreach (var (n, b) in Cases)
		{
			c.Bind(new CardView(CardKind.NewMove, "spirit-wave", n, "s", "p", "f", "NEW - Lv 1"));
			Assert.Equal(b, crest.Glyph, $"reward crest for '{n}'");
			Assert.Equal(b, UiFind.Get<SealStamp>(c, "Fallback").Glyph, $"reward fallback for '{n}'");
			NoLoneSurrogate(crest.Glyph, "reward");
		}
		c.Bind(new CardView(CardKind.NewMove, "spirit-wave", "", "s", "p", "f", "NEW - Lv 1"));
		Assert.Equal("?", crest.Glyph, "empty name");
		c.Free();
	}

	[Test]
	public static void TargetCard_BadgeIsFirstTextElement(Node host)
	{
		var t = GD.Load<PackedScene>("res://scenes/ui/target_card.tscn").Instantiate<TargetCard>();
		host.AddChild(t);
		var fb = UiFind.Get<SealStamp>(t, "Fallback");
		foreach (var (n, b) in Cases)
		{
			t.Bind(new TargetView("no-art-move", "A", n, 1, null, true));
			Assert.Equal(b, fb.Glyph, $"target badge for '{n}'");
			NoLoneSurrogate(fb.Glyph, "target");
		}
		t.Bind(new TargetView("no-art-move", "A", "", 1, null, true));
		Assert.Equal("?", fb.Glyph, "empty name with an id");
		t.Bind(new TargetView("", "C", "Empty", 0, null, false, "Empty slot"));
		Assert.Equal("+", fb.Glyph, "empty slot keeps +");
		t.Free();
	}

	[Test]
	public static void ShopItem_BadgeIsFirstTextElement(Node host)
	{
		var item = new ShopItem();
		host.AddChild(item);
		foreach (var (n, b) in Cases.Append(("", "?")))
		{
			item.Bind(new ShopEntry(ShopKind.Special, "spirit-wave", n, "src", "Special", "plain", "frames", 30, ShopCardState.Buyable, null), 0);
			Assert.Equal(b, item.GlyphLabel.Text, $"shop tag badge for '{n}'");
			NoLoneSurrogate(item.GlyphLabel.Text, "shop tag");
		}
		item.Free();
	}

	[Test]
	public static void ShopScreenIcon_BadgeIsFirstTextElement()
	{
		foreach (var (n, b) in Cases.Append(("", "?")))
		{
			var icon = ShopScreen.Icon(n, Colors.Red, 46);
			var glyph = icon.FindChild("Glyph", true, false) as Label;
			Assert.True(glyph != null, "icon has a Glyph label");
			Assert.Equal(b, glyph!.Text, $"slot icon badge for '{n}'");
			icon.Free();
		}
	}
}
