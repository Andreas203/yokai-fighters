using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Sim;
using YokaiFighters.Story;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Ui;

/// <summary>
/// The merchant's shop (YOK-159 rev 3 on YOK-104; R6, A2, T2, T5, A12, S2; docs/design/screens/rough/merchant-shop.png).
/// Layout is <c>scenes/ui/shop.tscn</c>: a wooden cabinet with three items per shelf (specials first, then modifiers),
/// hanging tags that show only an icon, name and price, the merchant's speech bubble, the ledger slip over the portrait
/// and the "Your slots" scroll. Items and slot rows are built here from <see cref="ShopScreenModel"/>, which asks
/// <see cref="MerchantShop"/> for every price, shelf, eligibility, refusal and reason. This script holds no rule.
/// <para>
/// The merchant speaks for the one focused thing (keys, pad and mouse hover share a single focus): a lead-in joined to the
/// item's plain card line, verbatim; events (greeting, bought, cannot afford, slot full, release, T5 lock, shelf empty)
/// replace it until the next focus change. Lines come from <see cref="MerchantShopLines"/> (data). Frame data (Tab / pad Y)
/// shows on a ledger slip and is never spoken (A12).
/// </para>
/// It is opened by <see cref="RestStopScreen"/> after <c>Merchant.Choose(Shop)</c> and raises <see cref="Closed"/> on Esc / B.
/// Nothing is saved inside the stop, so quitting to the title and choosing Continue shows the stop as entered (M5).
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
	/// <summary>What the merchant's bubble says now (empty = the bubble is hidden).</summary>
	public string MerchantLine => _bubble;
	/// <summary>The merchant's lines; loaded from <c>data/story/shop/</c> on first use, replaceable for tests.</summary>
	public MerchantShopLines Lines { get => _lines ??= LoadLines(); set => _lines = value; }

	public readonly Dictionary<string, ShopItem> Cards = new();
	public readonly Dictionary<SpecialSlot, KitCard> Rows = new();
	public string DetailText => L("Detail").Text;
	public bool SlipVisible => GetNode<Control>("Slip").Visible;
	/// <summary>The shelves of the cabinet that show, and the first one shown (more items than shelves scroll one shelf at a time).</summary>
	public int Tiers => GetNode<ShopCabinet>("Cabinet").Tiers;
	public int FirstShelf => _scroll;
	public int ShelfCount => (_items.Count + ShopCabinet.Columns - 1) / ShopCabinet.Columns;
	public IReadOnlyList<ShopItem> Items => _items;

	static readonly Color Ink = new("1D1B21"), Seal = new("BD2E26"), SealGrey = new("8E8A84"), Borrowed = new("77736D"), Persimmon = new("D86320"), Band = new("2D3A5E"),
		Paper = new("F8F1E0"), Foxfire = new("F2A23A");
	const float RowW = 284, RowH = 80;

	bool _ready;
	string _bubble = "", _notice = "", _emptyHint = "";
	MerchantShopLines? _lines;
	readonly List<ShopItem> _items = new();
	int _scroll, _lastIndex, _pick;
	string? _lastItemKey;
	bool _silent, _greeted;
	Tween? _shake;

	public void Bind(RunSession session)
	{
		Model = ShopScreenModel.Create(session);
		if (Model == null) GD.PushWarning($"ShopScreen: node '{session.Node?.NodeId}' is not a merchant");
		if (_ready) Refresh(null);
	}

	static MerchantShopLines LoadLines()
	{
		try { return MerchantShopLines.LoadDirectory(ContentPaths.Data("story")); }
		catch (Exception e) when (e is System.IO.IOException or FormatException or System.Text.Json.JsonException)
		{
			GD.PushWarning($"ShopScreen: no merchant lines: {e.Message}");
			return MerchantShopLines.Empty;
		}
	}

	public override void _Ready()
	{
		_ready = true;
		MenuInput.EnsurePadConfirm();
		var footer = GetNode<KeyChipFooter>("Footer");
		footer.Alignment = BoxContainer.AlignmentMode.Begin;
		// four chips and their words must fit the footer's width beside the notice line: the kit's footer text one size down
		var small = new Theme();
		small.SetFontSize("font_size", "FooterLabelInk", 19);
		small.SetFontSize("font_size", "KeyChipLabel", 16);
		footer.Theme = small;
		GetNode<Control>("Slip").Visible = false;
		Refresh(null);
	}

	Label L(string path) => GetNode<Label>(path);

	// ---- the merchant's bubble ----

	void Speak(string line)
	{
		_bubble = line;
		var bubble = GetNode<Control>("Bubble");
		bubble.Visible = line.Length > 0;
		var label = L("Bubble/MerchantLine");
		label.Text = line;
		var font = label.GetThemeFont("font");
		int size = 26;
		while (size > 17 && font.GetMultilineStringSize(line, HorizontalAlignment.Center, label.Size.X, size).Y > label.Size.Y) size -= 1;
		label.AddThemeFontSizeOverride("font_size", size);
	}

	void SpeakEvent(string key) => Speak(Lines.Event(key, _pick++ + (Model?.Seed ?? 0)));

	void SpeakItem(ShopEntry e) => Speak(Lines.ForItem(e.Name, e.Plain, Stable(e.Id)));

	static int Stable(string s) { int h = 17; foreach (char c in s) h = unchecked(h * 31 + c); return h & 0x7fffffff; }

	// ---- actions (the screen's whole vocabulary: ask the model, report what it says) ----

	/// <summary>Enter on a special. A refusal changes nothing; the shop's reason is shown and the merchant says why in a line.</summary>
	public bool BuySpecial(string id)
	{
		if (Model == null) return false;
		var entry = Model.Specials.FirstOrDefault(e => e.Id == id);
		var r = Model.Buy(id);
		if (!r.Ok) { Refused("Special_" + id, entry); return false; }
		_notice = $"Bought {entry!.Name}: borrowed ink, this run only.";
		Refresh("Special_" + id);
		SpeakEvent("bought_special");
		return true;
	}

	/// <summary>Enter on a modifier: asks which special carries it. Refused (no change) when the shop will not sell it.</summary>
	public bool BeginPick(string modifierId)
	{
		if (Model == null || Picking != null) return false;
		var e = Model.Modifiers.FirstOrDefault(x => x.Id == modifierId);
		if (e is not { State: ShopCardState.Buyable }) { Refused("Modifier_" + modifierId, e); return false; }
		Picking = modifierId; Armed = null; _notice = "";
		Refresh(null);
		return true;
	}

	/// <summary>Enter on an offered slot while picking.</summary>
	public bool PickTarget(SpecialSlot slot)
	{
		if (Model == null || Picking is not { } id) return false;
		var r = Model.BuyModifier(id, slot);
		if (!r.Ok) { Refused("Modifier_" + id, null, Model.TargetsFor(id).FirstOrDefault(t => t.Slot == slot)?.Reason); return false; }
		string name = Model.Slots.First(s => s.Slot == slot).Name;
		_notice = $"Bought {Model.Modifiers.First(e => e.Id == id).Name} for {name}: borrowed ink, this run only.";
		Picking = null;
		Refresh("Modifier_" + id);
		SpeakEvent("bought_modifier");
		return true;
	}

	/// <summary>Enter on a slot row: the first press arms the release, the second does it. Refused (no change) when the shop refuses.</summary>
	public bool ReleaseSlot(SpecialSlot slot)
	{
		if (Model == null) return false;
		if (Picking != null) return PickTarget(slot);
		var row = Model.Slots.First(s => s.Slot == slot);
		if (!row.CanRelease)
		{
			Armed = null;
			string? line = Model.HoldsLastSpecial && row.Locked ? "release_last_special" : !row.Locked && !row.Empty ? "cannot_afford" : null;
			Refused("Slot_" + slot, null, row.Reason, line);
			return false;
		}
		if (Armed != slot) { Armed = slot; Refresh("Slot_" + slot); return false; }
		string name = row.Name;
		var r = Model.Release(slot);
		Armed = null;
		if (!r.Ok) { Refused("Slot_" + slot, null, row.Reason); return false; }
		_notice = $"Released {name}: slot {slot} is free, the move loses its levels.";
		Refresh(null);
		SpeakEvent("release_binding");
		return true;
	}

	public void ToggleFrameData() { FrameDataShown = !FrameDataShown; Refresh(CurrentKey()); }

	void Back()
	{
		if (Picking != null) { var id = Picking; Picking = null; _notice = ""; Refresh("Modifier_" + id); if (FocusedItem() is { } it) SpeakItem(it.Entry); }
		else if (Armed != null) { var s = Armed; Armed = null; Refresh("Slot_" + s); }
		else Closed?.Invoke();
	}

	/// <summary>A refused action: the notice says why, the merchant answers (coins, slot full) and the coin chip shakes. Nothing changed.</summary>
	void Refused(string key, ShopEntry? entry, string? reason = null, string? line = null)
	{
		reason ??= entry?.Reason;
		_notice = reason is null ? "Not now." : char.ToUpperInvariant(reason[0]) + reason[1..] + ".";
		Refresh(Cards.ContainsKey(key) || Rows.Keys.Any(k => "Slot_" + k == key) ? key : CurrentKey());
		line ??= entry?.Refusal switch { ShopRefusal.Coins => "cannot_afford", ShopRefusal.SlotFull => "slot_full", _ => null };
		if (line != null) SpeakEvent(line);
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
		else if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Pageup or Key.Pagedown } k) { if (Page(k.Keycode == Key.Pagedown ? 1 : -1)) vp?.SetInputAsHandled(); }
		else if (e is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.LeftShoulder or JoyButton.RightShoulder } b) { if (Page(b.ButtonIndex == JoyButton.RightShoulder ? 1 : -1)) vp?.SetInputAsHandled(); }
	}

	string? CurrentKey() => GetViewport()?.GuiGetFocusOwner() is { } f && IsAncestorOf(f) ? f.Name.ToString() : null;

	ShopItem? FocusedItem() => GetViewport()?.GuiGetFocusOwner() as ShopItem is { } i && _items.Contains(i) ? i : (_lastItemKey != null && Cards.TryGetValue(_lastItemKey, out var l) ? l : null);

	// ---- the shelf grid: focus moves by the grid, shelves scroll to follow it ----

	int Tier(int count) => count == 0 ? ShopCabinet.MaxTiers : Math.Clamp((count + ShopCabinet.Columns - 1) / ShopCabinet.Columns, 2, ShopCabinet.MaxTiers);

	/// <summary>Puts every item on its shelf for the current scroll; items past the visible shelves hide.</summary>
	void Layout()
	{
		var cab = GetNode<ShopCabinet>("Cabinet");
		int tiers = cab.Tiers, rows = ShelfCount;
		_scroll = Math.Clamp(_scroll, 0, Math.Max(0, rows - tiers));
		cab.Show(Tier(_items.Count), _scroll, rows);
		foreach (var it in _items)
		{
			int row = it.Index / ShopCabinet.Columns - _scroll;
			it.Visible = row >= 0 && row < cab.Tiers;
			it.Position = ShopCabinet.ItemPos(it.Index % ShopCabinet.Columns, Math.Max(row, 0));
		}
		var more = L("ShelfNote");
		more.Text = rows > cab.Tiers
			? $"Shelves {_scroll + 1}-{_scroll + cab.Tiers} of {rows}{(_scroll + cab.Tiers < rows ? " · more below" : "")} · PgUp / PgDn"
			: "Specials first, then modifiers. Bought abilities are borrowed ink: grey, this run only.";
	}

	/// <summary>Scrolls so <paramref name="it"/>'s shelf shows, then focuses it.</summary>
	void FocusItem(ShopItem it)
	{
		int row = it.Index / ShopCabinet.Columns, tiers = GetNode<ShopCabinet>("Cabinet").Tiers;
		if (row < _scroll) _scroll = row; else if (row >= _scroll + tiers) _scroll = row - tiers + 1;
		Layout();
		it.GrabFocus();
	}

	/// <summary>Page down / up: one visible page of shelves, focus on the nearest item there. False when there is nothing to scroll.</summary>
	bool Page(int dir)
	{
		var cab = GetNode<ShopCabinet>("Cabinet");
		if (Picking != null || ShelfCount <= cab.Tiers || FocusedItem() is not { } cur) return false;
		int target = Math.Clamp(_scroll + dir * cab.Tiers, 0, ShelfCount - cab.Tiers);
		if (target == _scroll) return true;
		int col = cur.Index % ShopCabinet.Columns;
		_scroll = target;
		Layout();
		var pick = NearestEnabled(_scroll, col, 1) ?? NearestEnabled(_scroll, col, -1);
		if (pick != null) FocusItem(pick);
		return true;
	}

	/// <summary>The enabled item on shelf <paramref name="row"/> (or the next shelves in <paramref name="step"/> direction) closest to <paramref name="col"/>.</summary>
	ShopItem? NearestEnabled(int row, int col, int step)
	{
		int rows = ShelfCount;
		for (int r = row; r >= 0 && r < rows; r += step)
		{
			var best = _items.Where(i => i.Index / ShopCabinet.Columns == r && !i.CardDisabled).OrderBy(i => Math.Abs(i.Index % ShopCabinet.Columns - col)).ThenBy(i => i.Index).FirstOrDefault();
			if (best != null) return best;
		}
		return null;
	}

	void Navigate(ShopItem from, int dir)
	{
		int r = from.Index / ShopCabinet.Columns, c = from.Index % ShopCabinet.Columns;
		switch (dir)
		{
			case 0: if (NearestEnabled(r - 1, c, -1) is { } up) FocusItem(up); break;
			case 1: if (NearestEnabled(r + 1, c, 1) is { } down) FocusItem(down); break;
			case 2: if (_items.Where(i => i.Index / ShopCabinet.Columns == r && !i.CardDisabled && i.Index % ShopCabinet.Columns < c).OrderByDescending(i => i.Index).FirstOrDefault() is { } left) FocusItem(left); break;
			default:
				if (_items.Where(i => i.Index / ShopCabinet.Columns == r && !i.CardDisabled && i.Index % ShopCabinet.Columns > c).OrderBy(i => i.Index).FirstOrDefault() is { } right) FocusItem(right);
				else EnterSlots(from);
				break;
		}
	}

	/// <summary>Right off the shelf: the slot nearest to the item's height (an empty slot cannot take focus).</summary>
	void EnterSlots(ShopItem from)
	{
		float y = from.GlobalPosition.Y + ShopItem.H / 2;
		var rows = Rows.Values.Where(r => r.FocusMode != Control.FocusModeEnum.None).OrderBy(r => Math.Abs(r.GlobalPosition.Y + r.Size.Y / 2 - y)).ToList();
		if (rows.Count > 0) rows[0].GrabFocus();
	}

	// ---- drawing the model ----

	/// <summary>Rebuilds everything from the model, then puts focus on <paramref name="focusKey"/> or, if that is gone, the nearest thing that can take it. Focus put back this way is silent: the merchant keeps the line the action gave.</summary>
	public void Refresh(string? focusKey)
	{
		if (!_ready || Model is not { } m) return;
		L("HealthLabel").Text = $"RYO · {m.Health} / {m.MaxHealth}";
		GetNode<ProgressBar>("HealthBar").Value = m.Health;
		L("CoinChip/CoinValue").Text = m.Coins.ToString();

		var specials = m.Specials; var mods = m.Modifiers; var slots = m.Slots;
		var entries = specials.Concat(mods).ToList();
		Cards.Clear(); Rows.Clear(); _items.Clear();
		var cab = GetNode<ShopCabinet>("Cabinet");
		foreach (var c in cab.GetChildren().OfType<ShopItem>().ToList()) Discard(c);
		for (int i = 0; i < entries.Count; i++)
		{
			var item = MakeItem(entries[i], i);
			cab.AddChild(item);
			_items.Add(item);
			Cards[item.Name] = item;
		}
		Layout();
		bool empty = entries.Count == 0;
		GetNode<Control>("Cabinet/AllEmpty").Visible = empty;
		if (empty) BindAllEmpty(m);
		L("ShelfHeading").Text = Heading(m, entries);
		BuildSlots(m, slots);
		BindFooter(empty, slots);
		L("SlotsHeading").Text = Picking != null ? "WHICH SPECIAL?" : "YOUR SLOTS";
		L("SlotsHeading").AddThemeFontSizeOverride("font_size", Picking != null ? 26 : 32);
		L("SlotsCaption").Text = Picking is { } pid ? $"to carry {mods.FirstOrDefault(x => x.Id == pid)?.Name}" : $"release a binding · {m.ReleasePrice}";
		L("SlotNotes").Text = Picking is { } nid ? "Only the specials it applies to are offered, one modifier each." : SlotNotes(slots);
		bool picking = Picking != null;
		foreach (var it in _items)
			if (picking) { it.FocusMode = Control.FocusModeEnum.None; it.Marked = it.Name == "Modifier_" + Picking; it.Modulate = it.Marked ? Colors.White : new Color(0.55f, 0.55f, 0.58f); }

		_silent = true;
		try { PickFocus(focusKey)?.GrabFocus(); }
		finally { _silent = false; }
		if (!_greeted) { _greeted = true; SpeakEvent(empty ? "shelf_empty" : m.Row == 4 ? "greeting_row4" : "greeting_row7"); }
		UpdateDetail();
		UpdateSlip();
	}

	static string Heading(ShopScreenModel m, IReadOnlyList<ShopEntry> entries)
	{
		var sources = entries.Select(e => e.Source).Where(s => s.Length > 0).Distinct().ToList();
		return $"ABILITIES · {m.BuyPrice} coins · common · from yokai you have not bound" + (sources.Count > 0 ? $" ({string.Join(", ", sources)})" : "");
	}

	Control? PickFocus(string? key)
	{
		if (key != null)
		{
			if (Cards.TryGetValue(key, out var it) && it.FocusMode != Control.FocusModeEnum.None) return it;
			if (Rows.Values.FirstOrDefault(r => r.Name == key && r.FocusMode != Control.FocusModeEnum.None) is { } row) return row;
			if (key.StartsWith("Special_") || key.StartsWith("Modifier_")) // the item went inert (bought): the next one along, else the one before
			{
				var enabled = _items.Where(i => i.FocusMode != Control.FocusModeEnum.None).ToList();
				var near = enabled.FirstOrDefault(i => i.Index >= _lastIndex) ?? enabled.LastOrDefault();
				if (near != null) { FocusScroll(near); return near; }
			}
		}
		if (Picking == null)
		{
			var first = _items.FirstOrDefault(i => i.Entry.State == ShopCardState.Buyable) ?? _items.FirstOrDefault(i => i.FocusMode != Control.FocusModeEnum.None);
			if (first != null) { FocusScroll(first); return first; }
		}
		return Rows.OrderBy(r => r.Key).Select(r => r.Value).FirstOrDefault(r => r.FocusMode != Control.FocusModeEnum.None);
	}

	void FocusScroll(ShopItem it)
	{
		int row = it.Index / ShopCabinet.Columns, tiers = GetNode<ShopCabinet>("Cabinet").Tiers;
		if (row < _scroll) _scroll = row; else if (row >= _scroll + tiers) _scroll = row - tiers + 1;
		Layout();
	}

	/// <summary>The card whose Activated signal is being handled: Godot refuses to free an object mid-emit, so it leaves the shelf hidden and is freed with the frame.</summary>
	Control? _emitter;

	void Discard(Node c)
	{
		if (c == _emitter) { c.Name = "_gone"; _emitter.Visible = false; _emitter.FocusMode = Control.FocusModeEnum.None; c.QueueFree(); return; }
		c.GetParent().RemoveChild(c);
		c.Free();
	}

	void BindAllEmpty(ShopScreenModel m)
	{
		L("Cabinet/AllEmpty/EmptyWhy").Text = ""; L("Cabinet/AllEmpty/EmptyHint").Text = ""; // the merchant says it in the bubble; the plain hint goes on the notice line
		_emptyHint = m.Slots.Any(s => s.CanRelease) ? "Nothing left to sell. You can still release a binding, or go back and continue." : "Nothing left to sell, nothing to release. Go back and continue.";
	}

	ShopItem MakeItem(ShopEntry e, int index)
	{
		var item = new ShopItem();
		item.Bind(e, index);
		MenuInput.Neighbours(item, null, null, null, null);
		item.FocusEntered += () => OnItemFocus(item);
		item.MouseEntered += () => // one focus for mouse and keys; hovering the item that already has focus still lets the merchant speak it
		{
			if (Picking != null || item.CardDisabled) return;
			if (!item.HasFocus()) item.GrabFocus(); else { _notice = ""; SpeakItem(item.Entry); UpdateDetail(); }
		};
		item.Activated += () => { _emitter = item; try { OnItemActivated(e); } finally { _emitter = null; } };
		item.Navigate += d => Navigate(item, d);
		return item;
	}

	void OnItemFocus(ShopItem item)
	{
		_lastItemKey = item.Name; _lastIndex = item.Index;
		if (!_silent && Picking == null) { _notice = ""; SpeakItem(item.Entry); }
		UpdateDetail(); UpdateSlip();
	}

	void OnItemActivated(ShopEntry e)
	{
		if (Picking != null) return;
		if (e.Kind == ShopKind.Special) BuySpecial(e.Id); else BeginPick(e.Id);
	}

	/// <summary>The plain line under the shelf (right of the footer): the notice of the last action, else what Enter does for the focused thing.</summary>
	void UpdateDetail()
	{
		string text = _notice;
		if (text.Length == 0 && _items.Count == 0 && Picking == null && _emptyHint.Length > 0 && Armed == null) text = _emptyHint;
		if (text.Length == 0 && GetViewport()?.GuiGetFocusOwner() is { } f && IsAncestorOf(f))
		{
			if (f is ShopItem it) text = CardDetail(it.Entry);
			else if (Model is { } m && Rows.FirstOrDefault(r => r.Value == f).Key is var slot && slot != SpecialSlot.None && m.Slots.FirstOrDefault(s => s.Slot == slot) is { } row)
				text = SlotDetail(m, row, Picking is { } id ? m.TargetsFor(id).FirstOrDefault(t => t.Slot == slot) : null);
		}
		if (text.Length == 0 && Picking is { } pid && Model != null) text = $"{Model.Modifiers.FirstOrDefault(x => x.Id == pid)?.Name}: pick the special that carries it.";
		L("Detail").Text = text;
	}

	string CardDetail(ShopEntry e) => e.State switch
	{
		ShopCardState.Refused => e.Reason is { } r ? char.ToUpperInvariant(r[0]) + r[1..] + "." : "Not now.",
		ShopCardState.Bought => "Bought: borrowed ink, kept for this run only.",
		_ => e.Kind == ShopKind.Special ? $"Enter: buy {e.Name} for {e.Price} coins." : $"Enter: buy {e.Name} for {e.Price} coins, then pick the special that carries it.",
	};

	/// <summary>The ledger slip: frame data for the focused item, over the portrait's body. A special shows its frames text; a modifier the change only. Never spoken (A12).</summary>
	void UpdateSlip()
	{
		var slip = GetNode<Control>("Slip");
		var it = FocusedItem();
		slip.Visible = FrameDataShown && it != null && Picking == null;
		GetNode<Control>("SlipPin").Visible = slip.Visible;
		if (!slip.Visible || it == null) return;
		var e = it.Entry;
		L("Slip/Column/Pad/Box/Title").Text = e.Name;
		L("Slip/Column/Pad/Box/Sub").Text = $"{e.Source} {(e.Kind == ShopKind.Special ? "special" : "modifier")} · common";
		var body = GetNode<VBoxContainer>("Slip/Column/Pad/Box/Body");
		foreach (var c in body.GetChildren()) { body.RemoveChild(c); c.Free(); }
		if (e.Kind == ShopKind.Special) SlipRow(body, "FRAME DATA", e.Frames.Length > 0 ? e.Frames : "No frame data on this card yet.");
		else
		{
			SlipRow(body, "CHANGE", e.Frames.Length > 0 ? e.Frames : "No change listed.");
			SlipRow(body, "NUMBERS", "Frame numbers unchanged");
			SlipRow(body, "RIDES ON", "the special you pick");
		}
	}

	static void SlipRow(Container body, string heading, string text)
	{
		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Name = "Row" };
		row.AddThemeConstantOverride("separation", 10);
		var h = Lbl(heading, 14, name: "Heading"); h.CustomMinimumSize = new Vector2(92, 0); h.AddThemeColorOverride("font_color", Seal); h.VerticalAlignment = VerticalAlignment.Top;
		var t = Lbl(text, 18, name: "Text", wrap: true); t.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		row.AddChild(h); row.AddChild(t);
		body.AddChild(row);
	}

	void BuildSlots(ShopScreenModel m, IReadOnlyList<ShopSlotRow> slots)
	{
		var box = GetNode<VBoxContainer>("SlotRows");
		foreach (var c in box.GetChildren()) Discard(c);
		var targets = Picking is { } id ? m.TargetsFor(id) : null;
		bool picking = targets != null;
		var focusable = new List<KitCard>();
		int n = 0;
		foreach (var s in slots)
		{
			n++;
			var offered = targets?.FirstOrDefault(t => t.Slot == s.Slot);
			var card = NewKitCard("Slot_" + s.Slot, new Vector2(RowW, RowH));
			box.AddChild(card);
			var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore, Name = "Body" };
			foreach (var k in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + k, 8);
			var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			h.AddThemeConstantOverride("separation", 10);
			margin.AddChild(h); card.AddChild(margin);
			Rows[s.Slot] = card;
			if (s.Empty)
			{
				var t0 = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
				t0.AddChild(Lbl($"SLOT {n}", 13, caption: true, name: "Cap"));
				var emptyLabel = Lbl("empty: a bought special goes here", 15, caption: true, name: "Name", wrap: true);
				t0.AddChild(emptyLabel);
				h.AddChild(t0);
				card.FocusMode = Control.FocusModeEnum.None; // nothing to release; not focusable (and not greyed like a disabled card)
				var dashed = new StyleBoxFlat { BgColor = new Color("EFE6CF"), BorderColor = new Color("9A8F73"), CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4 };
				dashed.SetBorderWidthAll(2);
				card.AddThemeStyleboxOverride("panel", dashed);
				margin.Modulate = new Color(1, 1, 1, 0.85f);
				continue;
			}
			bool t5 = s.Locked && m.HoldsLastSpecial;
			bool locked = picking ? offered is not { Offered: true } : s.Locked;
			h.AddChild(Icon(s.Name, Persimmon, 46));
			var t = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			t.AddThemeConstantOverride("separation", -2);
			var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			var cap = Lbl(t5 && !picking ? $"SLOT {n} · last special" : $"SLOT {n}", 12, caption: true, name: t5 && !picking ? "Why" : "Cap");
			cap.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; cap.ClipText = true;
			top.AddChild(cap);

			string badge; Color fill;
			if (picking) { badge = offered is { Offered: true } ? "Attach" : "Can't"; fill = offered is { Offered: true } ? Seal : SealGrey; }
			else if (Armed == s.Slot) { badge = $"Sure? {m.ReleasePrice}"; fill = Persimmon; }
			else if (s.CanRelease) { badge = $"Release {m.ReleasePrice}"; fill = Seal; }
			else if (s.Locked) { badge = "LOCKED"; fill = SealGrey; }
			else { badge = $"Need {m.ReleasePrice}"; fill = SealGrey; }
			var bp = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, Name = "Badge" };
			bp.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = fill, ContentMarginLeft = 6, ContentMarginRight = 6, ContentMarginTop = 1, ContentMarginBottom = 1, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3, CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3 });
			var bl = Lbl(badge, 13, name: "BadgeLabel"); bl.ThemeTypeVariation = "PaperLabel";
			bp.AddChild(bl);
			top.AddChild(bp);
			// the release chip shows on the focused slot only; a picked-over, armed or T5-locked slot always shows its state
			bool always = picking || Armed == s.Slot || t5;
			bp.Visible = always;
			t.AddChild(top);

			var name = Lbl(s.Name, 21, clip: true, name: "Name");
			if (s.Borrowed || s.ModifierBorrowed) name.AddThemeColorOverride("font_color", Borrowed);
			t.AddChild(name);
			string sub = $"Lv {s.Level}";
			if (s.IsTanukiTarget) sub += " · Tanuki target";
			if (s.Borrowed) sub += " · borrowed ink";
			if (s.ModifierName != null) sub += $" · +{s.ModifierName}" + (s.ModifierBorrowed && !s.Borrowed ? " (borrowed ink)" : "");
			if (picking && offered is { Offered: false, Reason: { } why }) sub = char.ToUpperInvariant(why[0]) + why[1..];
			var subLabel = Lbl(sub, 15, caption: true, name: "Sub"); subLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; subLabel.ClipText = true;
			if (s.IsTanukiTarget) subLabel.AddThemeColorOverride("font_color", Persimmon);
			t.AddChild(subLabel);
			h.AddChild(t);

			var resting = s.IsTanukiTarget ? FlatPaper(Foxfire, 4) : FlatPaper(Ink, 2);
			var focused = FlatPaper(Seal, 4); focused.BgColor = new Color("FFF7E0"); // the kit's brush frame is too big for a row this size: a red border says "here"
			card.AddThemeStyleboxOverride("panel", resting);
			card.FocusEntered += () => card.AddThemeStyleboxOverride("panel", focused);
			card.FocusExited += () => card.AddThemeStyleboxOverride("panel", resting);
			margin.Modulate = locked && picking ? new Color(0.72f, 0.7f, 0.68f) : s.Borrowed || s.ModifierBorrowed ? new Color(0.86f, 0.85f, 0.84f) : Colors.White;
			if (t5) card.AddChild(new ShopSealMark { Name = "SealMark" }); // drawn across the row: tied cord and seal
			if (picking && locked) card.FocusMode = Control.FocusModeEnum.None;
			else focusable.Add(card);
			var slot = s.Slot;
			card.FocusEntered += () => { if (!_silent) _notice = ""; if (!always) bp.Visible = true; UpdateDetail(); };
			card.FocusExited += () => { if (!always && IsInstanceValid(bp)) bp.Visible = false; };
			card.MouseEntered += () => { if (card.FocusMode != Control.FocusModeEnum.None && !card.HasFocus()) card.GrabFocus(); };
			card.Activated += () => { _emitter = card; try { ReleaseSlot(slot); } finally { _emitter = null; } };
			card.FocusExited += () => { if (Armed == slot && GetViewport()?.GuiGetFocusOwner() is { } f && f != card) { Armed = null; CallDeferred(nameof(RefreshKeepFocus)); } };
		}
		// focus between slots: Up / Down along the list; Left goes back to the shelf, Right stays (nothing leaves the screen)
		for (int i = 0; i < focusable.Count; i++)
		{
			var card = focusable[i];
			MenuInput.Neighbours(card, i > 0 ? focusable[i - 1] : null, i < focusable.Count - 1 ? focusable[i + 1] : null, null, null);
			card.GuiInput += e =>
			{
				if (e.IsActionPressed("ui_left") || e.IsActionPressed("ui_right"))
				{
					if (e.IsActionPressed("ui_left") && Picking == null && (_lastItemKey != null && Cards.TryGetValue(_lastItemKey, out var back) && !back.CardDisabled ? back : _items.FirstOrDefault(x => !x.CardDisabled)) is { } target) FocusItem(target);
					card.AcceptEvent();
				}
			};
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

	static string SlotNotes(IReadOnlyList<ShopSlotRow> slots)
	{
		string s = "Releasing frees a slot; the move loses its levels.";
		if (slots.FirstOrDefault(r => r.IsTanukiTarget) is { } t) s += $" {t.Name} is the Tanuki's target: release moves it.";
		return s;
	}

	string? _footerKey;

	void BindFooter(bool shelfEmpty, IReadOnlyList<ShopSlotRow> slots)
	{
		var footer = GetNode<KeyChipFooter>("Footer");
		bool picking = Picking != null;
		string key = picking ? "pick" : shelfEmpty ? "empty" + slots.Any(s => s.CanRelease) : "shop" + FrameDataShown;
		if (_footerKey == key) return; // the kit footer rebuilds its chips on every set; only set when the hints change
		_footerKey = key;
		if (picking) footer.SetHints(new("route", "Choose"), new("confirm", "Attach"), new("back", "Cancel"));
		else if (shelfEmpty) footer.SetHints(slots.Any(s => s.CanRelease) ? new KeyHint[] { new("route", "Move"), new("confirm", "Release"), new("back", "Back to rest stop") } : new KeyHint[] { new("back", "Back to rest stop") });
		else footer.SetHints(new("route", "Move"), new("confirm", "Buy / Release"), new("frames", FrameDataShown ? "Frame data: ON" : "Frame data"), new("back", "Back to rest stop"));
	}

	// ---- little builders ----

	KitCard NewKitCard(string name, Vector2 size)
	{
		var card = new KitCard { Name = name, ThemeTypeVariation = "PaperCard", CustomMinimumSize = size, FocusMode = Control.FocusModeEnum.All };
		card.AddThemeStyleboxOverride("panel", FlatPaper(new Color("1D1B21"), 2));
		return card;
	}

	/// <summary>Plain paper (the scroll's rods and bands are drawn on it), instead of the kit card's brush border that eats the room the text needs.</summary>
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

	static Control Icon(string name, Color fill, int size)
	{
		var panel = new Panel { CustomMinimumSize = new Vector2(size, size), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore, Name = "Icon" };
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = fill, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6 });
		var glyph = Lbl(name.Length > 0 ? name[..1].ToUpperInvariant() : "?", (int)(size * 0.62f), name: "Glyph");
		glyph.ThemeTypeVariation = "PaperLabel";
		glyph.SetAnchorsPreset(LayoutPreset.FullRect); glyph.HorizontalAlignment = HorizontalAlignment.Center; glyph.VerticalAlignment = VerticalAlignment.Center;
		panel.AddChild(glyph);
		return panel;
	}
}
