using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-81 probes: icon wiring on reward cards, modifier-target cards and move-list strips. Expected values come from the ticket claims (YOK-79 lookup by move id; Lv 3 shows pips; frame data hides art and pips).</summary>
public static class AbilityIconScreensProbes
{
	static RewardCard Card(Node host) { var c = GD.Load<PackedScene>("res://scenes/ui/reward_card.tscn").Instantiate<RewardCard>(); host.AddChild(c); return c; }
	static TargetCard Target(Node host) { var t = GD.Load<PackedScene>("res://scenes/ui/target_card.tscn").Instantiate<TargetCard>(); host.AddChild(t); return t; }
	static MoveSlotCard Slot(Node host) { var s = GD.Load<PackedScene>("res://scenes/ui/move_slot_card.tscn").Instantiate<MoveSlotCard>(); host.AddChild(s); return s; }
	static CardView Up(int to, string id = "foxfire") => new(CardKind.Upgrade, id, "N", "s", "p", "f", "UPGRADE", null, to);
	static CardView Mod() => new(CardKind.Modifier, "foxfire", "M", "s", "p", "f", "MODIFIER");
	static SlotRow Row(string slot, bool empty, string id, string name = "Foxfire") => new(slot, empty, name, 1, 3, "k", "k", "p", "f", MoveId: id);

	// YOK-81: pips are for a Lv 3 upgrade only; rebinding the same node to a modifier must clear them.
	[Test]
	public static void RebindLv3ToModifierClearsPipsAndUsesFallback(Node host)
	{
		var c = Card(host);
		c.Bind(Up(3)); c.SetState(false, false);
		c.Bind(Mod()); c.SetState(false, false);
		Assert.True(!UiFind.Get<Control>(c, "Pips").Visible, "pips leaked onto modifier card");
		Assert.True(UiFind.Get<TextureRect>(c, "Illustration").Texture == AbilityArt.Fallback, "modifier shows fallback");
		c.Free();
	}

	// YOK-81: frame data hides the pips; rebinding while frame data is on must not bring them back until it is off.
	[Test]
	public static void BindWhileFrameDataOnKeepsPipsHidden(Node host)
	{
		var c = Card(host);
		c.Bind(Up(2)); c.SetState(false, true);
		c.Bind(Up(3)); // no SetState after bind
		Assert.True(!UiFind.Get<Control>(c, "Pips").Visible, "pips visible over frame data after rebind");
		c.SetState(false, false);
		Assert.True(UiFind.Get<Control>(c, "Pips").Visible, "pips restored once frame data is off");
		c.Free();
	}

	// YOK-81: only ToLevel == 3 shows pips (0 = unknown multi-target, 2 and 4 are not Lv 3).
	[Test]
	public static void PipsOnlyAtLevelThree(Node host)
	{
		var c = Card(host);
		foreach (int lv in new[] { 0, 1, 2, 4 })
		{
			c.Bind(Up(lv)); c.SetState(false, false);
			Assert.True(!UiFind.Get<Control>(c, "Pips").Visible, $"ToLevel {lv} shows pips");
		}
		// NewMove with a stray ToLevel 3 is not an upgrade.
		c.Bind(new CardView(CardKind.NewMove, "foxfire", "N", "s", "p", "f", "NEW", null, 3)); c.SetState(false, false);
		Assert.True(!UiFind.Get<Control>(c, "Pips").Visible, "new move shows pips");
		c.Free();
	}

	// YOK-79: ids are exact; wrong case, empty or whitespace ids get the fallback, never a mapped icon.
	[Test]
	public static void IdsAreExactAndEmptyIdFallsBack(Node host)
	{
		var c = Card(host);
		foreach (var id in new[] { "Foxfire", "FOXFIRE", "", " foxfire", "foxfire " })
		{
			c.Bind(new CardView(CardKind.NewMove, id, "Foxfire", "s", "p", "f", "NEW"));
			Assert.True(UiFind.Get<TextureRect>(c, "Illustration").Texture == AbilityArt.Fallback, $"id '{id}' did not fall back");
		}
		c.Free();
	}

	// YOK-81: multi-target upgrade (ToLevel 0) still shows the base icon by id, with no pips.
	[Test]
	public static void MultiTargetUpgradeShowsBaseIconNoPips(Node host)
	{
		var c = Card(host);
		c.Bind(Up(0, "spirit-wave")); c.SetState(true, false);
		Assert.True(UiFind.Get<TextureRect>(c, "Illustration").Texture == AbilityArt.ForId("spirit-wave"), "base icon");
		Assert.True(!UiFind.Get<Control>(c, "Pips").Visible, "no pips");
		c.Free();
	}

	// YOK-81: art area returns after frame data off following a rebind made while frame data was on.
	[Test]
	public static void ArtReturnsAfterFrameDataRebind(Node host)
	{
		var c = Card(host);
		c.Bind(Up(2)); c.SetState(false, true);
		c.Bind(Mod());
		Assert.True(!UiFind.Get<Control>(c, "Illustration").Visible, "art hidden while frame data on");
		c.SetState(false, false);
		Assert.True(UiFind.Get<Control>(c, "Illustration").Visible, "art back");
		c.Free();
	}

	// YOK-81: empty slot keeps the + diamond; refilling the same node shows the icon and white modulate; locked+selected stays dim.
	[Test]
	public static void TargetCardRebindEmptyFilledAndLockedSelected(Node host)
	{
		var t = Target(host);
		var ill = UiFind.Get<TextureRect>(t, "Illustration");
		t.Bind(new TargetView("foxfire", "A", "Foxfire", 1, null, true));
		t.Bind(new TargetView("", "B", "Empty", 0, null, false, "Empty slot"));
		Assert.True(!ill.Visible && ill.Texture == null, "empty slot hides icon");
		Assert.True(UiFind.Get<SealStamp>(t, "Fallback").Visible && UiFind.Get<SealStamp>(t, "Fallback").Glyph == "+", "plus diamond shown");
		t.Bind(new TargetView("foxfire", "C", "Foxfire", 1, null, false, "Needs X"));
		t.SetSelected(true);
		Assert.True(ill.Modulate.R < 1f, "locked + selected still dims");
		Assert.True(ill.Visible && !UiFind.Get<SealStamp>(t, "Fallback").Visible, "icon shown, plus hidden");
		t.Bind(new TargetView("foxfire", "A", "Foxfire", 1, null, true));
		Assert.True(ill.Modulate.R >= 1f, "dim leaked to eligible slot after rebind");
		Assert.True(ill.Texture == AbilityArt.ForId("foxfire"), "icon by id");
		t.Free();
	}

	// YOK-79/81: the target lookup uses the id, never slot letter or display name.
	[Test]
	public static void TargetCardIgnoresSlotAndNameWhenIdUnknown(Node host)
	{
		var t = Target(host);
		t.Bind(new TargetView("not-a-move", "A", "foxfire", 1, null, true));
		Assert.True(UiFind.Get<TextureRect>(t, "Illustration").Texture == AbilityArt.Fallback, "name 'foxfire' must not map");
		t.Free();
	}

	// YOK-81: move list with four empty slots shows no icons; refilling shows them; emptying again clears; duplicate special gives the same icon.
	[Test]
	public static void MoveListEmptyFilledEmptyAndDuplicate(Node host)
	{
		var cards = new[] { Slot(host), Slot(host), Slot(host), Slot(host) };
		string[] letters = { "A", "B", "C", "D" };
		for (int i = 0; i < 4; i++) cards[i].Bind(Row(letters[i], true, ""), true, false);
		foreach (var c in cards) Assert.True(!UiFind.Get<Control>(c, "Icon").Visible && UiFind.Get<TextureRect>(c, "Icon").Texture == null, "empty slot shows icon");
		cards[0].Bind(Row("A", false, "foxfire"), true, false);
		cards[1].Bind(Row("B", false, "foxfire"), true, true);
		Assert.True(UiFind.Get<TextureRect>(cards[0], "Icon").Texture == UiFind.Get<TextureRect>(cards[1], "Icon").Texture, "duplicate special same icon");
		Assert.True(UiFind.Get<TextureRect>(cards[1], "Icon").Visible, "icon stays visible with frame data (strip)");
		cards[0].Bind(Row("A", true, "foxfire"), true, false); // empty flag wins over a stale id
		Assert.True(!UiFind.Get<Control>(cards[0], "Icon").Visible && UiFind.Get<TextureRect>(cards[0], "Icon").Texture == null, "emptied slot kept icon");
		cards[2].Bind(Row("C", false, ""), true, false);
		Assert.True(UiFind.Get<TextureRect>(cards[2], "Icon").Texture == AbilityArt.Fallback, "owned slot with empty id gets fallback");
		cards[3].Bind(Row("D", false, "Foxfire", "foxfire"), true, false);
		Assert.True(UiFind.Get<TextureRect>(cards[3], "Icon").Texture == AbilityArt.Fallback, "case-different id falls back");
		foreach (var c in cards) c.Free();
	}
}
