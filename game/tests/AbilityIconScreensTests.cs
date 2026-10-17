using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-81: reward cards, modifier-target cards and move-list strips read the shared lookup by move id (never slot, name or tag).</summary>
public static class AbilityIconScreensTests
{
	static RewardCard NewCard(Node host)
	{
		var c = GD.Load<PackedScene>("res://scenes/ui/reward_card.tscn").Instantiate<RewardCard>();
		host.AddChild(c);
		return c;
	}

	static Texture2D Art(Node n, string name = "Illustration") => UiFind.Get<TextureRect>(n, name).Texture;

	[Test]
	public static void RewardCardUsesMoveIdNotName(Node host)
	{
		var c = NewCard(host);
		c.Bind(new CardView(CardKind.NewMove, "spirit-wave", "Foxfire", "Kitsune - special", "p", "f", "NEW - Lv 1"));
		Assert.True(Art(c) == AbilityArt.ForId("spirit-wave"), "id picks the art, the name 'Foxfire' does not");
		Assert.True(Art(c) != AbilityArt.ForId("foxfire"), "name not looked up");
		c.Bind(new CardView(CardKind.NewMove, "unknown-move", "Foxfire", "s", "p", "f", "NEW - Lv 1"));
		Assert.True(Art(c) == AbilityArt.Fallback, "unknown id shows fallback");
		c.SetState(false, false);
		Assert.True(UiFind.Get<Control>(c, "Illustration").Visible, "icon in the card art area");
		c.Free();
	}

	[Test]
	public static void ModifierCardUsesFallbackEvenWithMappedId(Node host)
	{
		var c = NewCard(host);
		c.Bind(new CardView(CardKind.Modifier, "foxfire", "Fox's Patience", "Kitsune - modifier", "p", "f", "MODIFIER"));
		Assert.True(Art(c) == AbilityArt.Fallback, "modifier gets the generic diamond");
		Assert.True(!UiFind.Get<Control>(c, "Pips").Visible, "no pips on a modifier");
		c.Free();
	}

	[Test]
	public static void Level3EvolutionShowsBaseIconWithPipsAndPlainTextStaysClear(Node host)
	{
		var c = NewCard(host);
		c.Bind(new CardView(CardKind.Upgrade, "foxfire", "Foxfire Evolved", "Your special", "p", "f", "UPGRADE - Lv 2 to 3", null, 3));
		c.SetState(false, false);
		Assert.True(Art(c) == AbilityArt.ForId("foxfire"), "base icon");
		var pips = UiFind.Get<LevelPips>(c, "Pips");
		Assert.True(pips.Visible && pips.Level == 3, "pips show Lv 3");
		c.SetState(false, true);
		Assert.True(!pips.Visible, "frame data hides the pips with the art");
		c.SetState(false, false);
		Assert.True(pips.Visible, "pips return after the frame-data toggle is turned off");
		c.Bind(new CardView(CardKind.Upgrade, "foxfire", "Foxfire", "Your special", "p", "f", "UPGRADE - Lv 1 to 2", null, 2));
		Assert.True(!UiFind.Get<Control>(c, "Pips").Visible, "Lv 2 upgrade has no pips");
		// A12: the icon sits above the plain-language text, never over it.
		var art = UiFind.Get<Control>(c, "Illustration"); var plain = UiFind.Get<Control>(c, "Plain");
		Assert.True(art.Position.Y + art.Size.Y <= plain.Position.Y + 0.5f, "icon does not overlap the plain line");
		c.SetState(false, true);
		Assert.True(!UiFind.Get<Control>(c, "Illustration").Visible, "frame data replaces the art");
		c.Free();
	}

	[Test]
	public static void TargetCardUsesMoveIdAndEmptyKeepsPlus(Node host)
	{
		var t = GD.Load<PackedScene>("res://scenes/ui/target_card.tscn").Instantiate<TargetCard>();
		host.AddChild(t);
		t.Bind(new TargetView("spirit-wave", "A", "Foxfire", 2, null, true));
		Assert.True(Art(t) == AbilityArt.ForId("spirit-wave"), "move id, not slot 'A' or name");
		t.Bind(new TargetView("no-art-move", "B", "Foxfire", 1, null, true));
		Assert.True(Art(t) == AbilityArt.Fallback, "unknown id shows fallback");
		t.Bind(new TargetView("", "C", "Empty", 0, null, false, "Empty slot"));
		Assert.True(!UiFind.Get<Control>(t, "Illustration").Visible && UiFind.Get<SealStamp>(t, "Fallback").Glyph == "+", "empty slot keeps the + diamond");
		t.Bind(new TargetView("spirit-wave", "D", "Spirit Wave", 1, null, false, "Needs a projectile"));
		t.SetSelected(false);
		Assert.True(UiFind.Get<TextureRect>(t, "Illustration").Modulate.R < 1, "locked slot dims its icon with the card");
		t.Free();
	}

	[Test]
	public static void MoveListStripUsesMoveId(Node host)
	{
		var card = GD.Load<PackedScene>("res://scenes/ui/move_slot_card.tscn").Instantiate<MoveSlotCard>();
		host.AddChild(card);
		card.Bind(new SlotRow("A", false, "Foxfire", 1, 3, "k", "k", "p", "f", MoveId: "spirit-wave"), true, false);
		Assert.True(Art(card, "Icon") == AbilityArt.ForId("spirit-wave"), "move id, not slot or name");
		card.Bind(new SlotRow("B", false, "Spirit Wave", 1, 3, "k", "k", "p", "f", MoveId: "mystery"), true, false);
		Assert.True(Art(card, "Icon") == AbilityArt.Fallback, "unknown id shows fallback");
		card.Bind(new SlotRow("C", true, "Empty", 0, 3, "k", "k", "p", ""), true, false);
		Assert.True(!UiFind.Get<Control>(card, "Icon").Visible, "empty slot has no icon");
		card.Free();
	}

	[Test]
	public static void OnlyTheLookupNamesAbilityIconFiles(Node host)
	{
		string dir = ProjectSettings.GlobalizePath("res://") + "scripts/Ui";
		foreach (var f in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories).Where(f => !f.EndsWith("AbilityArt.cs")))
		{
			string text = File.ReadAllText(f);
			if (!f.EndsWith("CopyTalisman.cs")) Assert.True(!text.Contains("RewardArt."), Path.GetFileName(f) + " uses RewardArt instead of AbilityArt");
			Assert.True(!text.Contains("pause-icon") && !text.Contains("ability-icon") && !text.Contains("ability_art"), Path.GetFileName(f) + " has its own ability art path");
		}
	}
}
