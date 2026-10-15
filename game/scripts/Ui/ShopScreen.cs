using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Sim;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Ui;

/// <summary>
/// The merchant's shop (YOK-104, R6, A2, T2, T5, A12; docs/design/screens/rough/merchant-shop.png). Layout is
/// <c>scenes/ui/shop.tscn</c>; the cards and slot rows are built here from <see cref="ShopScreenModel"/>, which asks
/// <see cref="MerchantShop"/> for every price, shelf, eligibility, refusal and reason. This script holds no rule.
/// <para>
/// It is opened by <see cref="RestStopScreen"/> after <c>Merchant.Choose(Shop)</c> and raises <see cref="Closed"/> on Back.
/// Nothing is saved inside the stop, so quitting to the title and choosing Continue shows the stop as entered (M5).
/// </para>
/// Input (through the GUI pipeline, focus never leaves the screen): arrows / d-pad move, Right from a shelf enters the
/// slots, Left returns; Enter / pad A buys, picks or releases (a release asks a second Enter); Tab / pad Y swaps the
/// card text for its frame data; Esc / pad B steps back one level (a pick, an armed release, the shop).
/// </summary>
public partial class ShopScreen : Control
{
	public event Action? Closed;

	public ShopScreenModel? Model { get; private set; }
	/// <summary>The modifier whose target Ryo is choosing, or null.</summary>
	public string? Picking { get; private set; }
	/// <summary>The slot whose release awaits its confirming second Enter, or null.</summary>
	public SpecialSlot? Armed { get; private set; }
	public bool FrameDataShown { get; private set; }
	/// <summary>The merchant's line (the rest stop passes the one it shows).</summary>
	public string MerchantLine { get => L("MerchantLine").Text; set { if (_ready) L("MerchantLine").Text = value; else _line = value; } }

	public Button BackButton { get; private set; } = null!;
	public readonly Dictionary<string, KitCard> Cards = new();
	public readonly Dictionary<SpecialSlot, KitCard> Rows = new();
	public string DetailText => L("Detail").Text;

	static readonly Color Ink = new("1D1B21"), Seal = new("BD2E26"), SealGrey = new("8E8A84"), Borrowed = new("77736D"), Persimmon = new("D86320"), Band = new("2D3A5E"),
		Rod = new("5A3E2A"), Paper = new("F8F1E0"), Foxfire = new("F2A23A");
	const float CardW = 330, CardH = 236;

	bool _ready;
	string? _line;
	string _notice = "";
	Tween? _shake;

	public void Bind(RunSession session)
	{
		Model = ShopScreenModel.Create(session);
		if (Model == null) GD.PushWarning($"ShopScreen: node '{session.Node?.NodeId}' is not a merchant");
		if (_ready) Refresh(null);
	}

	public override void _Ready()
	{
		_ready = true;
		MenuInput.EnsurePadConfirm();
		BackButton = GetNode<Button>("BackButton");
		BackButton.Name = "Back";
		BackButton.Pressed += Back;
		foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" }) // the same red plate as the rest stop's Continue
			BackButton.AddThemeStyleboxOverride(state, new StyleBoxFlat { BgColor = Seal, ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 6, ContentMarginBottom = 6, BorderColor = Ink, BorderWidthBottom = state == "focus" ? 5 : 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2 });
		foreach (string c in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" }) BackButton.AddThemeColorOverride(c, new Color("F1E8D4"));
		BackButton.FocusEntered += () => ShowDetail("Back to the rest stop. Continue to the map from there.");
		GetNode<KeyChipFooter>("Footer").Alignment = BoxContainer.AlignmentMode.Begin;
		foreach (string n in new[] { "SpecialsScroll", "ModifiersScroll" }) GetNode<ScrollContainer>("Shelves/" + n).FocusMode = FocusModeEnum.None;
		if (_line != null) L("MerchantLine").Text = _line;
		Refresh(null);
	}

	Label L(string path) => GetNode<Label>(path);

	// ---- actions (the screen's whole vocabulary: ask the model, report what it says) ----

	/// <summary>Enter on a special card. A refusal changes nothing; the shop's reason is shown.</summary>
	public bool BuySpecial(string id)
	{
		if (Model == null) return false;
		var r = Model.Buy(id);
		if (!r.Ok) { Refused(id, Model.Specials.FirstOrDefault(e => e.Id == id)?.Reason); return false; }
		_notice = $"Bought {Model.Specials.First(e => e.Id == id).Name}: borrowed ink, this run only.";
		Refresh("Special_" + id);
		return true;
	}

	/// <summary>Enter on a modifier card: asks which special carries it. Refused (no change) when the shop will not sell it.</summary>
	public bool BeginPick(string modifierId)
	{
		if (Model == null || Picking != null) return false;
		var e = Model.Modifiers.FirstOrDefault(x => x.Id == modifierId);
		if (e is not { State: ShopCardState.Buyable }) { Refused(modifierId, e?.Reason); return false; }
		Picking = modifierId; Armed = null; _notice = "";
		Refresh(null);
		return true;
	}

	/// <summary>Enter on an offered slot while picking.</summary>
	public bool PickTarget(SpecialSlot slot)
	{
		if (Model == null || Picking is not { } id) return false;
		var r = Model.BuyModifier(id, slot);
		if (!r.Ok) { Refused(id, Model.TargetsFor(id).FirstOrDefault(t => t.Slot == slot)?.Reason); return false; }
		string name = Model.Slots.First(s => s.Slot == slot).Name;
		_notice = $"Bought {Model.Modifiers.First(e => e.Id == id).Name} for {name}: borrowed ink, this run only.";
		Picking = null;
		Refresh("Modifier_" + id);
		return true;
	}

	/// <summary>Enter on a slot row: the first press arms the release, the second does it. Refused (no change) when the shop refuses.</summary>
	public bool ReleaseSlot(SpecialSlot slot)
	{
		if (Model == null) return false;
		if (Picking != null) return PickTarget(slot);
		var row = Model.Slots.First(s => s.Slot == slot);
		if (!row.CanRelease) { Armed = null; Refused("Slot_" + slot, row.Reason); return false; }
		if (Armed != slot) { Armed = slot; Refresh("Slot_" + slot); return false; }
		string name = row.Name;
		var r = Model.Release(slot);
		Armed = null;
		if (!r.Ok) { Refused("Slot_" + slot, row.Reason); return false; }
		_notice = $"Released {name}: slot {slot} is free, the move loses its levels.";
		Refresh(null);
		return true;
	}

	public void ToggleFrameData() { FrameDataShown = !FrameDataShown; Refresh(CurrentKey()); }

	void Back()
	{
		if (Picking != null) { var id = Picking; Picking = null; _notice = ""; Refresh("Modifier_" + id); }
		else if (Armed != null) { var s = Armed; Armed = null; Refresh("Slot_" + s); }
		else Closed?.Invoke();
	}

	void Refused(string key, string? reason)
	{
		_notice = reason is null ? "Not now." : char.ToUpperInvariant(reason[0]) + reason[1..] + ".";
		Refresh(Cards.ContainsKey(key) || Rows.Keys.Any(k => "Slot_" + k == key) ? key : CurrentKey());
		_shake?.Kill();
		var chip = GetNode<Control>("CoinChip");
		float x = chip.Position.X;
		_shake = CreateTween();
		foreach (float dx in new[] { 8f, -8f, 5f, -5f, 0f }) _shake.TweenProperty(chip, "position:x", x + dx, 0.04f);
	}

	public override void _Input(InputEvent e)
	{
		if (!IsVisibleInTree()) return;
		var vp = GetViewport(); // Back() can leave the tree, so take it first
		if (MenuInput.FrameData(e)) { ToggleFrameData(); vp?.SetInputAsHandled(); }
		else if (MenuInput.Back(e)) { Back(); vp?.SetInputAsHandled(); }
	}

	string? CurrentKey() => GetViewport()?.GuiGetFocusOwner() is { } f && IsAncestorOf(f) ? f.Name.ToString() : null;

	void ShowDetail(string text) { L("Detail").Text = text; }

	// ---- drawing the model ----

	/// <summary>Rebuilds everything from the model, then puts focus on <paramref name="focusKey"/> or, if that is gone, the first thing that can take it.</summary>
	public void Refresh(string? focusKey)
	{
		if (!_ready || Model is not { } m) return;
		L("HealthLabel").Text = $"RYO · {m.Health} / {m.MaxHealth}";
		GetNode<ProgressBar>("HealthBar").Value = m.Health;
		L("CoinChip/CoinValue").Text = m.Coins.ToString();

		var specials = m.Specials; var mods = m.Modifiers; var slots = m.Slots;
		Cards.Clear(); Rows.Clear();
		bool both = specials.Count == 0 && mods.Count == 0;
		GetNode<Control>("Shelves").Visible = !both;
		GetNode<Control>("AllEmpty").Visible = both;
		if (both) BindAllEmpty(m);
		Fill("Specials", specials);
		Fill("Modifiers", mods);
		BuildSlots(m, slots);
		BindFooter();
		L("SlotsHeading").Text = Picking is { } pid ? "WHICH SPECIAL CARRIES IT?" : $"YOUR SLOTS · release a binding {m.ReleasePrice}";
		L("SlotNotes").Text = SlotNotes(m, slots);
		if (Picking is { } nid) L("SlotNotes").Text = $"{mods.FirstOrDefault(x => x.Id == nid)?.Name}: only the specials it applies to are offered, one modifier each.";
		SetNeighbours(specials, mods, slots);
		L("Detail").Text = _notice;
		bool picking = Picking != null;
		BackButton.FocusMode = picking ? FocusModeEnum.None : FocusModeEnum.All;
		foreach (var c in Cards.Values)
			if (picking) { c.FocusMode = FocusModeEnum.None; c.GetNode<Control>("Body").Modulate = c.Name == "Modifier_" + Picking ? Colors.White : new Color(0.55f, 0.55f, 0.58f); if (c.Name == "Modifier_" + Picking) c.Selected = true; }

		Control? target = focusKey != null && Ring().FirstOrDefault(c => c.Name == focusKey && c.FocusMode != FocusModeEnum.None) is { } k ? k
			: Ring().FirstOrDefault(c => c.FocusMode != FocusModeEnum.None);
		if (IsInsideTree() && target != null) target.GrabFocus();
	}

	/// <summary>Everything focusable in reading order: specials, modifiers, slots, Back.</summary>
	List<Control> Ring()
	{
		var list = new List<Control>();
		foreach (string row in new[] { "SpecialsRow", "ModifiersRow" })
			foreach (var c in GetNode("Shelves").GetNode(row.Replace("Row", "Scroll") + "/" + row).GetChildren().OfType<KitCard>()) list.Add(c);
		list.AddRange(Rows.OrderBy(r => r.Key).Select(r => (Control)r.Value));
		list.Add(BackButton);
		return list;
	}

	void Fill(string shelf, IReadOnlyList<ShopEntry> entries)
	{
		var row = GetNode<HBoxContainer>($"Shelves/{shelf}Scroll/{shelf}Row");
		foreach (var c in row.GetChildren()) { row.RemoveChild(c); c.Free(); }
		bool any = entries.Count > 0;
		GetNode<Control>($"Shelves/{shelf}Scroll").Visible = any;
		GetNode<Control>($"Shelves/{shelf}Label").Visible = any;
		var empty = L($"Shelves/{shelf}Empty");
		empty.Visible = !any && !(GetNode<Control>("AllEmpty").Visible);
		empty.Text = shelf == "Specials" ? "SPECIALS: none for sale. Every yokai you have met is bound (R6 sells only from unbound yokai)." : "MODIFIERS: none for sale. Every yokai you have met is bound.";
		row.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0), MouseFilter = MouseFilterEnum.Ignore, Name = "PadL" }); // room for the focus brush the scroll would clip
		foreach (var e in entries)
		{
			var card = MakeCard(e);
			row.AddChild(card);
			Cards[card.Name] = card;
		}
		row.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0), MouseFilter = MouseFilterEnum.Ignore, Name = "PadR" });
	}

	void BindAllEmpty(ShopScreenModel m)
	{
		L("AllEmpty/Box/EmptyWhy").Text = "Nothing left to sell. The merchant only sells common abilities from yokai you have not bound.";
		L("AllEmpty/Box/EmptyHint").Text = m.Slots.Any(s => s.CanRelease) ? "You can still release a binding on the right, or go back and continue." : "Nothing to buy and nothing to release yet. Go back and continue.";
		var pegs = GetNode<Control>("AllEmpty/Box/Pegs");
		if (pegs.GetChildCount() == 0) pegs.AddChild(new ShopPegs());
	}

	KitCard MakeCard(ShopEntry e)
	{
		bool bought = e.State == ShopCardState.Bought, refused = e.State == ShopCardState.Refused;
		var card = NewKitCard((e.Kind == ShopKind.Special ? "Special_" : "Modifier_") + e.Id, new Vector2(CardW, CardH));
		card.AddChild(new ShopCardRods());
		var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore, Name = "Body" };
		foreach (var (k, v) in new[] { ("left", 16), ("right", 16), ("top", 16), ("bottom", 16) }) margin.AddThemeConstantOverride("margin_" + k, v);
		var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		box.AddThemeConstantOverride("separation", 2);
		margin.AddChild(box);
		card.AddChild(margin);
		margin.Modulate = bought ? new Color(0.62f, 0.6f, 0.58f) : refused ? new Color(0.72f, 0.7f, 0.68f) : Colors.White;

		var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		top.AddThemeConstantOverride("separation", 10);
		top.AddChild(Icon(e.Kind == ShopKind.Special ? e.Id : null, e.Name, e.Kind == ShopKind.Special ? Persimmon : Ink, 46));
		var titles = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		titles.AddThemeConstantOverride("separation", -4);
		titles.AddChild(Lbl(e.Name, e.Name.Length > 13 ? 21 : 25, clip: true, name: "Name"));
		titles.AddChild(Lbl($"{e.Source} {e.Type}", 18, caption: true, name: "Sub"));
		top.AddChild(titles);
		box.AddChild(top);

		var mid = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, 30) };
		mid.AddThemeConstantOverride("separation", 8);
		string tag = bought ? "BOUGHT" : e.Kind == ShopKind.Special ? "NEW · Lv 1" : "MODIFIER";
		var tagPanel = new Panel { CustomMinimumSize = new Vector2(104, 26), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore, Name = "Tag" };
		if (bought) tagPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = SealGrey });
		else tagPanel.ThemeTypeVariation = e.Kind == ShopKind.Special ? "TagNew" : "TagModifier";
		var tagLabel = Lbl(tag, 18, name: "TagLabel"); tagLabel.ThemeTypeVariation = "PaperLabel";
		tagLabel.SetAnchorsPreset(LayoutPreset.FullRect); tagLabel.HorizontalAlignment = HorizontalAlignment.Center; tagLabel.VerticalAlignment = VerticalAlignment.Center;
		tagPanel.AddChild(tagLabel);
		mid.AddChild(tagPanel);
		mid.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
		if (!bought) mid.AddChild(PriceSeal(e.Price, refused));
		box.AddChild(mid);

		string caption = bought ? "borrowed ink, kept this run" : refused ? e.Reason ?? "" : "borrowed ink, run only";
		var cap = Lbl(caption, 16, caption: true, name: "Caption");
		cap.AutowrapMode = TextServer.AutowrapMode.WordSmart; cap.ClipText = true; cap.CustomMinimumSize = new Vector2(0, 22);
		box.AddChild(cap);

		var plain = Lbl(FrameDataShown && e.Frames.Length > 0 ? e.Frames : e.Plain, 19, name: "Plain");
		plain.AutowrapMode = TextServer.AutowrapMode.WordSmart; plain.ClipText = true; plain.SizeFlagsVertical = SizeFlags.ExpandFill; plain.CustomMinimumSize = new Vector2(0, 48);
		box.AddChild(plain);

		card.CardDisabled = bought; // a bought card is not offered again
		card.Selected = false;
		card.FocusEntered += () => ShowDetail(_notice.Length > 0 ? _notice : CardDetail(e));
		card.Activated += () => OnCardActivated(e);
		return card;
	}

	string CardDetail(ShopEntry e) => e.State switch
	{
		ShopCardState.Refused => e.Reason is { } r ? char.ToUpperInvariant(r[0]) + r[1..] + "." : "Not now.",
		ShopCardState.Bought => "Bought: borrowed ink, kept for this run only.",
		_ => e.Kind == ShopKind.Special ? $"Enter: buy {e.Name} for {e.Price} coins." : $"Enter: buy {e.Name} for {e.Price} coins, then pick the special that carries it.",
	};

	void OnCardActivated(ShopEntry e)
	{
		if (Picking != null) return;
		if (e.Kind == ShopKind.Special) BuySpecial(e.Id); else BeginPick(e.Id);
	}

	void BuildSlots(ShopScreenModel m, IReadOnlyList<ShopSlotRow> slots)
	{
		var box = GetNode<VBoxContainer>("SlotRows");
		foreach (var c in box.GetChildren()) { box.RemoveChild(c); c.Free(); }
		var targets = Picking is { } id ? m.TargetsFor(id) : null;
		foreach (var s in slots)
		{
			var offered = targets?.FirstOrDefault(t => t.Slot == s.Slot);
			var card = NewKitCard("Slot_" + s.Slot, new Vector2(480, 96));
			box.AddChild(card);
			bool picking = targets != null;
			var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore, Name = "Body" };
			foreach (var k in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + k, 8);
			var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			h.AddThemeConstantOverride("separation", 10);
			margin.AddChild(h); card.AddChild(margin);
			Rows[s.Slot] = card;
			if (s.Empty)
			{
				var emptyLabel = Lbl($"Slot {s.Slot} · empty: a bought special goes here", 20, caption: true, name: "Name", wrap: true);
				emptyLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill; emptyLabel.VerticalAlignment = VerticalAlignment.Center;
				h.AddChild(emptyLabel);
				card.FocusMode = FocusModeEnum.None; // nothing to release; not focusable (and not greyed like a disabled card)
				card.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("E3D8BD"), BorderColor = new Color("9A8F73"), BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4 });
				margin.Modulate = new Color(1, 1, 1, 0.8f);
				continue;
			}
			bool locked = picking ? offered is not { Offered: true } : s.Locked;
			h.AddChild(Icon(s.SpecialId, s.Name, Ink, 52));
			var t = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			t.AddThemeConstantOverride("separation", -2);
			var name = Lbl(s.Name, 22, clip: true, name: "Name");
			if (s.Borrowed || s.ModifierBorrowed) name.AddThemeColorOverride("font_color", Borrowed);
			t.AddChild(name);
			string sub = $"Slot {s.Slot} · Lv {s.Level}";
			if (s.ModifierName != null) sub += $" · {s.ModifierName}";
			if (s.ModifierBorrowed && !s.Borrowed) sub += " · modifier in borrowed ink";
			if (s.IsTanukiTarget) sub += " · Tanuki target";
			if (s.Borrowed) sub += " · borrowed ink";
			if (picking && offered is { Offered: false, Reason: { } why }) sub = char.ToUpperInvariant(why[0]) + why[1..];
			var subLabel = Lbl(sub, 17, caption: true, name: "Sub"); subLabel.ClipText = true;
			if (s.IsTanukiTarget) subLabel.AddThemeColorOverride("font_color", Persimmon);
			t.AddChild(subLabel);
			if (!picking && s.Locked && s.Reason is { } lockWhy) { var rl = Lbl(char.ToUpperInvariant(lockWhy[0]) + lockWhy[1..], 15, caption: true, name: "Why"); rl.ClipText = true; t.AddChild(rl); }
			h.AddChild(t);

			string badge; Color fill;
			if (picking) { badge = offered is { Offered: true } ? "Attach" : "Can't"; fill = offered is { Offered: true } ? Seal : SealGrey; }
			else if (Armed == s.Slot) { badge = $"Sure? {m.ReleasePrice}"; fill = Persimmon; }
			else if (s.CanRelease) { badge = $"Release {m.ReleasePrice}"; fill = Seal; }
			else if (s.Locked) { badge = "LOCKED"; fill = SealGrey; }
			else { badge = $"Need {m.ReleasePrice}"; fill = SealGrey; }
			var bp = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter, Name = "Badge" };
			bp.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = fill, ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 4, ContentMarginBottom = 4, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3, CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3 });
			var bl = Lbl(badge, 20, name: "BadgeLabel"); bl.ThemeTypeVariation = "PaperLabel";
			bp.AddChild(bl);
			h.AddChild(bp);

			if (s.IsTanukiTarget) card.AddThemeStyleboxOverride("panel", FlatPaper(Foxfire, 4));
			margin.Modulate = locked ? new Color(0.72f, 0.7f, 0.68f) : s.Borrowed || s.ModifierBorrowed ? new Color(0.86f, 0.85f, 0.84f) : Colors.White;
			if (picking && locked) card.FocusMode = FocusModeEnum.None;
			var slot = s.Slot;
			card.FocusEntered += () => ShowDetail(_notice.Length > 0 ? _notice : SlotDetail(m, s, offered));
			card.Activated += () => ReleaseSlot(slot);
			card.FocusExited += () => { if (Armed == slot && GetViewport()?.GuiGetFocusOwner() is { } f && f != card) { Armed = null; CallDeferred(nameof(RefreshKeepFocus)); } };
		}
	}

	void RefreshKeepFocus() => Refresh(CurrentKey());

	string SlotDetail(ShopScreenModel m, ShopSlotRow s, ShopTargetRow? target)
	{
		if (Picking != null) return target is { Offered: true } ? $"Enter: put it on {s.Name} for {m.BuyPrice} coins." : (target?.Reason is { } r ? char.ToUpperInvariant(r[0]) + r[1..] + "." : "Not this one.");
		if (Armed == s.Slot) return $"Release {s.Name} for {m.ReleasePrice} coins? Enter again to confirm, Esc to keep it.";
		if (s.CanRelease) return $"Enter: release {s.Name} for {m.ReleasePrice} coins. It loses its levels.";
		return s.Reason is { } why ? char.ToUpperInvariant(why[0]) + why[1..] + "." : "Not now.";
	}

	static string SlotNotes(ShopScreenModel m, IReadOnlyList<ShopSlotRow> slots)
	{
		string s = "Releasing frees a slot; the move loses its levels.";
		if (slots.Any(r => r.IsTanukiTarget)) s += "\nThe Tanuki's target is framed: release moves it.";
		return s;
	}

	void BindFooter()
	{
		var footer = GetNode<KeyChipFooter>("Footer");
		if (Picking != null) footer.SetHints(new("move", "Choose"), new("confirm", "Attach"), new("back", "Cancel"));
		else footer.SetHints(new("move", "Move"), new("confirm", "Buy / Release"), new("frames", "Frame data"), new("back", "Back"));
	}

	/// <summary>Explicit neighbours for every focusable control so focus can never leave the screen: nothing points outside, an edge points at itself.</summary>
	void SetNeighbours(IReadOnlyList<ShopEntry> specials, IReadOnlyList<ShopEntry> mods, IReadOnlyList<ShopSlotRow> slots)
	{
		var sRow = Cards.Values.Where(c => c.Name.ToString().StartsWith("Special_") && !c.CardDisabled).ToList();
		var mRow = Cards.Values.Where(c => c.Name.ToString().StartsWith("Modifier_") && !c.CardDisabled).ToList();
		var rows = Rows.OrderBy(r => r.Key).Select(r => r.Value).Where(r => r.FocusMode != FocusModeEnum.None).ToList();
		var shelfRows = new[] { sRow, mRow }.Where(r => r.Count > 0).ToList();
		Control? firstShelf = shelfRows.Count > 0 ? shelfRows[0][0] : null;
		Control? firstSlot = rows.Count > 0 ? rows[0] : null;
		for (int r = 0; r < shelfRows.Count; r++)
			for (int i = 0; i < shelfRows[r].Count; i++)
			{
				var c = shelfRows[r][i];
				Control? up = r > 0 ? Nearest(shelfRows[r - 1], i) : null;
				Control? down = r < shelfRows.Count - 1 ? Nearest(shelfRows[r + 1], i) : (Control?)BackButton;
				if (BackButton.FocusMode == FocusModeEnum.None) down = r < shelfRows.Count - 1 ? down : null;
				MenuInput.Neighbours(c, up, down, i > 0 ? shelfRows[r][i - 1] : null, i < shelfRows[r].Count - 1 ? shelfRows[r][i + 1] : firstSlot);
			}
		for (int i = 0; i < rows.Count; i++)
			MenuInput.Neighbours(rows[i], i > 0 ? rows[i - 1] : null, i < rows.Count - 1 ? rows[i + 1] : (BackButton.FocusMode != FocusModeEnum.None ? BackButton : null), firstShelf ?? null, null);
		Control? lastSlot = rows.Count > 0 ? rows[^1] : null;
		Control? lastShelf = shelfRows.Count > 0 ? shelfRows[^1][^1] : null;
		MenuInput.Neighbours(BackButton, lastSlot ?? lastShelf, null, lastShelf ?? lastSlot, null);
	}

	static Control Nearest(List<KitCard> row, int i) => row[Math.Min(i, row.Count - 1)];

	// ---- little builders ----

	KitCard NewKitCard(string name, Vector2 size)
	{
		var stroke = new NinePatchRect { Name = "Stroke", Visible = false, TopLevel = true, MouseFilter = MouseFilterEnum.Ignore, Texture = GD.Load<Texture2D>("res://assets/generated/ui/selection-frame.png"), PatchMarginLeft = 32, PatchMarginTop = 32, PatchMarginRight = 32, PatchMarginBottom = 32, DrawCenter = false };
		var card = new KitCard { Name = name, ThemeTypeVariation = "PaperCard", CustomMinimumSize = size, FocusMode = FocusModeEnum.All };
		card.AddChild(stroke);
		card.AddThemeStyleboxOverride("panel", FlatPaper(new Color("1D1B21"), 2));
		return card;
	}

	/// <summary>The hanging scrolls' plain paper (the rods and bands are drawn on it), instead of the kit card's brush border that eats the room the text needs.</summary>
	static StyleBoxFlat FlatPaper(Color border, int width)
	{
		var box = new StyleBoxFlat { BgColor = Paper, BorderColor = border, ContentMarginLeft = 2, ContentMarginRight = 2, ContentMarginTop = 2, ContentMarginBottom = 2 };
		box.SetBorderWidthAll(width);
		return box;
	}

	static Label Lbl(string text, int size, bool caption = false, bool clip = false, string name = "", bool wrap = false)
	{
		var l = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore, Name = name.Length > 0 ? name : "Label" };
		if (caption) l.ThemeTypeVariation = "CaptionLabel";
		l.AddThemeFontSizeOverride("font_size", size);
		l.ClipText = clip;
		if (wrap) l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		return l;
	}

	static Control Icon(string? moveId, string name, Color fill, int size)
	{
		var panel = new Panel { CustomMinimumSize = new Vector2(size, size), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore, Name = "Icon" };
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = fill, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8 });
		var glyph = Lbl(name.Length > 0 ? name[..1].ToUpperInvariant() : "?", (int)(size * 0.62f), name: "Glyph");
		glyph.ThemeTypeVariation = "PaperLabel";
		glyph.SetAnchorsPreset(LayoutPreset.FullRect); glyph.HorizontalAlignment = HorizontalAlignment.Center; glyph.VerticalAlignment = VerticalAlignment.Center;
		panel.AddChild(glyph);
		return panel;
	}

	static Control PriceSeal(int price, bool grey)
	{
		var p = new Panel { CustomMinimumSize = new Vector2(46, 46), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore, Name = "Seal" };
		p.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = grey ? SealGrey : Seal, CornerRadiusBottomLeft = 23, CornerRadiusBottomRight = 23, CornerRadiusTopLeft = 23, CornerRadiusTopRight = 23 });
		var l = Lbl(price.ToString(), 24, name: "Price"); l.ThemeTypeVariation = "PaperLabel";
		l.SetAnchorsPreset(LayoutPreset.FullRect); l.HorizontalAlignment = HorizontalAlignment.Center; l.VerticalAlignment = VerticalAlignment.Center;
		p.AddChild(l);
		return p;
	}
}

/// <summary>A shelf card's hanging-scroll rods: an indigo band with a wooden rod at the top and the bottom edge. Drawn shapes only.</summary>
public partial class ShopCardRods : Control
{
	public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; SetAnchorsPreset(LayoutPreset.FullRect); }

	public override void _Draw()
	{
		var r = new Rect2(Vector2.Zero, Size);
		DrawRect(new Rect2(4, 4, r.Size.X - 8, 14), new Color("2D3A5E"));
		DrawRect(new Rect2(4, r.Size.Y - 18, r.Size.X - 8, 14), new Color("2D3A5E"));
		foreach (float y in new[] { 0f, r.Size.Y - 8 })
		{
			RestStopScreenDecor.Bar(this, new Rect2(0, y, r.Size.X, 8), new Color("5A3E2A"), 3f);
		}
	}
}

/// <summary>Three bare pegs: the "shelf empty" mark (docs/design/screens/rough/merchant-shop.png, states strip).</summary>
public partial class ShopPegs : Control
{
	public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; SetAnchorsPreset(LayoutPreset.FullRect); }

	public override void _Draw()
	{
		float cx = Size.X / 2f;
		DrawLine(new Vector2(cx - 120, 14), new Vector2(cx + 120, 14), new Color("5A3E2A"), 4f);
		foreach (float dx in new[] { -90f, 0f, 90f })
		{
			DrawLine(new Vector2(cx + dx, 14), new Vector2(cx + dx, 46), new Color("5A3E2A"), 5f);
			DrawCircle(new Vector2(cx + dx, 52), 8, new Color("46301F"));
		}
	}
}
