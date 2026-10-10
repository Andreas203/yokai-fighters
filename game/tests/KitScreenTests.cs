using System.Linq;
using Godot;
using YokaiFighters.Ui;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Tests;

/// <summary>Screen kit: glyph switching, focus and navigation, the paper style, and the gallery layout at 1080p and 720p.</summary>
public static class KitScreenTests
{
	static InputEventKey Key(Godot.Key k) => new() { Keycode = k, Pressed = true };

	// ---- glyph switching ----

	[Test]
	public static void Glyphs_LastDeviceFollowsInput()
	{
		InputGlyphs.Set(InputDevice.Keyboard);
		Assert.Equal(InputDevice.Pad, InputGlyphs.Observe(new InputEventJoypadButton { ButtonIndex = JoyButton.A, Pressed = true })!.Value, "pad button");
		Assert.Equal(InputDevice.Pad, InputGlyphs.Last, "last = pad");
		Assert.True(InputGlyphs.Observe(new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = 0.1f }) == null, "stick drift ignored");
		Assert.Equal(InputDevice.Pad, InputGlyphs.Last, "still pad after drift");
		InputGlyphs.Observe(Key(Godot.Key.Enter));
		Assert.Equal(InputDevice.Keyboard, InputGlyphs.Last, "key = keyboard");
		InputGlyphs.Observe(new InputEventJoypadMotion { Axis = JoyAxis.LeftY, AxisValue = -0.9f });
		Assert.Equal(InputDevice.Pad, InputGlyphs.Last, "stick push = pad");
		InputGlyphs.Observe(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
		Assert.Equal(InputDevice.Keyboard, InputGlyphs.Last, "mouse = keyboard glyphs");
	}

	[Test]
	public static void Glyphs_EveryMenuActionHasBothTexts()
	{
		foreach (var a in new[] { "confirm", "back", "pause", "frames", "scheme", "choose", "move", "tab" })
		{
			string k = InputGlyphs.Glyph(a, InputDevice.Keyboard), p = InputGlyphs.Glyph(a, InputDevice.Pad);
			Assert.True(k != p, $"{a} differs per device ({k} vs {p})");
		}
		Assert.Equal("ENTER", InputGlyphs.Glyph("confirm", InputDevice.Keyboard), "keyboard confirm");
		Assert.Equal("A", InputGlyphs.Glyph("confirm", InputDevice.Pad), "pad confirm");
		Assert.Equal("NOPE", InputGlyphs.Glyph("nope", InputDevice.Pad), "unknown shows upper-cased id");
	}

	[Test]
	public static void Footer_ChipsSwitchWithLastDevice(Node host)
	{
		InputGlyphs.Set(InputDevice.Keyboard);
		var f = GD.Load<PackedScene>("res://scenes/ui/kit/key_chip_footer.tscn").Instantiate<KeyChipFooter>();
		host.AddChild(f);
		f.SetHints(new KeyHint("confirm", "Confirm"), new KeyHint("back", "Resume"));
		Assert.Equal("ENTER|ESC", string.Join("|", f.ChipTexts()), "keyboard chips");
		string Shown() => string.Join("|", f.GetChildren().OfType<PanelContainer>().Select(c => c.GetNode<Label>("Glyph").Text));
		Assert.Equal("ENTER|ESC", Shown(), "keyboard labels shown");
		f._Input(new InputEventJoypadButton { ButtonIndex = JoyButton.B, Pressed = true });
		Assert.Equal("A|B", string.Join("|", f.ChipTexts()), "pad chips");
		Assert.Equal("A|B", Shown(), "pad labels shown after the device change");
		Assert.Equal("Confirm|Resume", string.Join("|", f.GetChildren().OfType<Label>().Select(l => l.Text)), "action words stay");
		f.Pinned = FooterDevice.Keyboard;
		Assert.Equal("ENTER|ESC", Shown(), "pinned keyboard ignores the last device");
		f.Free();
		InputGlyphs.Set(InputDevice.Keyboard);
	}

	// ---- focus and navigation ----

	static KitBrushButton Row(string t) { var r = GD.Load<PackedScene>("res://scenes/ui/kit/menu_row.tscn").Instantiate<KitBrushButton>(); r.Text = t; return r; }

	[Test]
	public static void MenuRow_FocusLightsStroke_DisabledIsSkipped(Node host)
	{
		var box = new VBoxContainer();
		host.AddChild(box);
		KitBrushButton a = Row("A"), b = Row("B"), c = Row("C");
		box.AddChild(a); box.AddChild(b); box.AddChild(c);
		b.Disabled = true; b.FocusMode = Control.FocusModeEnum.None;
		MenuInput.WrapVertical(a, b, c);
		Assert.True(!a.StrokeVisible, "no stroke before focus");
		a.GrabFocus();
		Assert.True(a.HasFocus() && a.StrokeVisible && a.Effective == KitState.Focused, "focused row shows the red stroke");
		Assert.True(!c.StrokeVisible, "other rows stay plain");
		c.GrabFocus();
		Assert.True(c.StrokeVisible && !a.StrokeVisible, "stroke follows focus");
		b.Refresh();
		Assert.True(b.Effective == KitState.Disabled && !b.StrokeVisible, "disabled row: no stroke");
		Assert.True(b.FocusMode == Control.FocusModeEnum.None, "disabled row cannot take focus");
		box.Free();
	}

	[Test]
	public static void Buttons_PreviewPinsEveryState(Node host)
	{
		foreach (var scene in new[] { "menu_row", "red_button", "ghost_button" })
		foreach (var s in new[] { KitState.Normal, KitState.Focused, KitState.Disabled, KitState.Pressed })
		{
			var b = GD.Load<PackedScene>($"res://scenes/ui/kit/{scene}.tscn").Instantiate<KitBrushButton>();
			host.AddChild(b);
			b.Preview = s;
			Assert.Equal(s, b.Effective, $"{scene} {s}");
			if (s == KitState.Disabled) Assert.True(b.Disabled, scene + " disabled flag");
			if (scene == "red_button") Assert.True(b.StrokeVisible, "red button always has its stroke");
			if (scene == "ghost_button") Assert.True(!b.StrokeVisible, "ghost has no stroke");
			if (scene == "menu_row") Assert.Equal(s is KitState.Focused or KitState.Pressed, b.StrokeVisible, $"row stroke in {s}");
			b.Free();
		}
	}

	[Test]
	public static void Card_FocusSelectDisableActivate(Node host)
	{
		var card = GD.Load<PackedScene>("res://scenes/ui/kit/kit_card.tscn").Instantiate<KitCard>();
		host.AddChild(card);
		int hits = 0;
		card.Activated += () => hits++;
		Assert.True(!card.StrokeVisible, "plain at rest");
		card.GrabFocus();
		Assert.True(card.StrokeVisible && card.Effective == KitState.Focused, "focus lights it");
		card._GuiInput(new InputEventAction { Action = "ui_accept", Pressed = true });
		Assert.Equal(1, hits, "Enter activates");
		card._GuiInput(new InputEventAction { Action = "ui_accept", Pressed = false });
		card.ReleaseFocus();
		card.Selected = true;
		Assert.True(card.StrokeVisible, "selected stays lit without focus");
		card.CardDisabled = true;
		Assert.True(card.Effective == KitState.Disabled && !card.StrokeVisible, "disabled: grey, no stroke");
		Assert.True(card.FocusMode == Control.FocusModeEnum.None, "disabled card takes no focus");
		card._GuiInput(new InputEventAction { Action = "ui_accept", Pressed = true });
		Assert.Equal(1, hits, "disabled card ignores Enter");
		card.Free();
	}

	[Test]
	public static void Dialog_OpenFocusesConfirm_EscCancels_FocusStaysInside(Node host)
	{
		var d = GD.Load<PackedScene>("res://scenes/ui/kit/paper_dialog.tscn").Instantiate<PaperDialog>();
		host.AddChild(d);
		int ok = 0, no = 0;
		d.Confirmed += () => ok++; d.Cancelled += () => no++;
		d.Open("Restart run?", "Your current run will be lost.");
		Assert.True(d.Visible && d.ConfirmButton!.HasFocus(), "opens on Confirm");
		Assert.Equal("Restart run?", d.GetNode<Label>("%Title").Text, "title set");
		Assert.True(d.ConfirmButton.FocusNeighborRight == d.ConfirmButton.GetPathTo(d.CancelButton!), "right goes to Cancel");
		Assert.True(d.ConfirmButton.FocusNeighborTop == d.ConfirmButton.GetPathTo(d.ConfirmButton), "up cannot leave the dialog");
		d.ConfirmButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.True(ok == 1 && no == 0 && !d.Visible, "Confirm closes and signals");
		d.Open("Restart run?", "x");
		d._UnhandledInput(new InputEventKey { Keycode = Godot.Key.Escape, Pressed = true });
		Assert.True(no == 1 && !d.Visible, "Esc cancels");
		d.Free();
	}

	// ---- paper style ----

	[Test]
	public static void PaperStyle_TornEdgeIsDeterministicAndInsideRect()
	{
		var r = new Rect2(10, 10, 600, 300);
		var a = PaperStyleBox.TornOutline(r, 5f, 20f, 7);
		var b = PaperStyleBox.TornOutline(r, 5f, 20f, 7);
		Assert.True(a.SequenceEqual(b), "same size and seed tear the same way");
		Assert.True(!a.SequenceEqual(PaperStyleBox.TornOutline(r, 5f, 20f, 8)), "a different seed tears differently");
		Assert.True(a.All(p => r.Grow(5.01f).HasPoint(p)), "outline stays within amplitude of the rect");
		Assert.Equal(4, PaperStyleBox.TornOutline(r, 0f, 20f, 1).Length, "amplitude 0 = plain rectangle");
	}

	[Test]
	public static void Theme_PaperSheetAndCardUseThePaperStyle(Node host)
	{
		var theme = GD.Load<Theme>("res://ui/talisman_theme.tres");
		foreach (var v in new[] { "PaperSheet", "PaperCard", "PaperDialog" })
			Assert.True(theme.GetStylebox("panel", v) is StyleBoxTexture, v + " panel uses nine-slice texture");
		var sheet = GD.Load<PackedScene>("res://scenes/ui/kit/paper_sheet.tscn").Instantiate<PaperSheet>();
		host.AddChild(sheet);
		sheet.Card = true;
		Assert.True(sheet.GetNode<Control>("Panel").ThemeTypeVariation == "PaperCard" && !sheet.GetNode<Control>("CornerTL").Visible, "card variant hides ornaments");
		sheet.Card = false;
		Assert.True(sheet.GetNode<Control>("CornerTL").Visible, "sheet shows ornament slots");
		sheet.Free();
	}

	[Test]
	public static void Backdrop_DimAndArtSlot(Node host)
	{
		var b = GD.Load<PackedScene>("res://scenes/ui/kit/kit_backdrop.tscn").Instantiate<KitBackdrop>();
		host.AddChild(b);
		Assert.True(!b.GetNode<Control>("DimLayer").Visible && b.GetNode<Control>("Art").Visible, "clear with night art by default");
		b.Dim = 0.5f;
		Assert.True(b.GetNode<ColorRect>("DimLayer").Visible && Mathf.IsEqualApprox(b.GetNode<ColorRect>("DimLayer").Color.A, 0.5f), "dim shows");
		b.Art = GD.Load<Texture2D>("res://assets/generated/ui/paper-panel.png");
		Assert.True(b.GetNode<Control>("Art").Visible, "art slot shows when set");
		b.Free();
	}

	// ---- layout ----

	[Test]
	public static void Gallery_BothPagesFitTheDesignCanvas_At1080And720(Node host)
	{
		// The game stretches a 1920x1080 canvas (canvas_items), so 720p lays out identically and only shrinks the pixels:
		// the audit checks bounds and text fit on the 1920x1080 canvas, and that no text drops under 15 px once scaled to 720p.
		var g = GD.Load<PackedScene>("res://scenes/ui/kit/kit_gallery.tscn").Instantiate<KitGallery>();
		g.Size = KitGallery.Design;
		host.AddChild(g);
		for (int page = 0; page < g.PageCount; page++)
		{
			g.ShowPage(page);
			var root = g.PageRoot(page);
			root.Size = KitGallery.Design;
			KitGallery.ForceLayout(root);
			var problems = KitGallery.Audit(root);
			Assert.True(problems.Count == 0, $"page {page}: " + string.Join("; ", problems.Take(5)));
		}
		g.Free();
	}

	[Test]
	public static void Gallery_ShowsEveryComponentInEveryState(Node host)
	{
		var g = GD.Load<PackedScene>("res://scenes/ui/kit/kit_gallery.tscn").Instantiate<KitGallery>();
		host.AddChild(g);
		var a = g.PageRoot(0);
		Assert.Equal(12, Count<KitBrushButton>(a), "page A: 4 menu rows + 4 red + 4 ghost buttons");
		var cards = g.PageRoot(1).GetChildren().OfType<KitCard>().Select(c => c.Effective).ToArray();
		Assert.Equal("Normal,Focused,Disabled,Pressed", string.Join(",", cards), "card states");
		Assert.Equal(3, Count<KeyChipFooter>(a), "keyboard, pad and choose footers");
		Assert.Equal(8, Count<CornerOrnament>(g.PageRoot(1)), "ornaments: 2 in the dialog, 2 in the sheet instance, 4 slots");
		g.Free();
	}

	[Test]
	public static void Plates_AreBoundedAndHaveRealAlpha()
	{
		foreach (var name in new[] { "rice-sheet", "rice-card", "red-brush", "ink-underline", "title-wordmark", "fox-mask", "ryo-portrait", "burst-blank", "selection-frame", "blossom-corner" })
		{
			using var image = GD.Load<Texture2D>($"res://assets/generated/ui/{name}.png").GetImage();
			Assert.True(image.GetWidth() <= 1024 && image.GetHeight() <= 640, name + " bounded for UI use");
			Assert.True(image.DetectAlpha() != Image.AlphaMode.None, name + " has real alpha");
		}
		using var frame = GD.Load<Texture2D>("res://assets/generated/ui/selection-frame.png").GetImage();
		Assert.True(frame.GetPixel(frame.GetWidth() / 2, frame.GetHeight() / 2).A < 0.01f, "selection centre transparent");
	}

	[Test]
	public static void Plates_ResourceOverridesAndOrnamentVisibility(Node host)
	{
		var art = GD.Load<Texture2D>("res://assets/generated/ui/fox-mask.png");
		var card = GD.Load<PackedScene>("res://scenes/ui/kit/kit_card.tscn").Instantiate<KitCard>();
		host.AddChild(card); card.SelectionArt = art;
		Assert.True(card.GetNode<NinePatchRect>("Stroke").Texture == art, "card frame swappable");
		Assert.True(!card.GetNode<NinePatchRect>("Stroke").DrawCenter, "frame cannot cover live card text");
		card.Free();
		var row = Row("Start"); host.AddChild(row); row.StrokeArt = art;
		Assert.True(row.GetNode<TextureRect>("Stroke").Texture == art, "button stroke swappable"); row.Free();
		var heading = GD.Load<PackedScene>("res://scenes/ui/kit/kit_heading.tscn").Instantiate<KitHeading>();
		host.AddChild(heading); heading.UnderlineArt = art;
		Assert.True(heading.GetNode<TextureRect>("Underline").Texture == art, "heading underline swappable"); heading.Free();
		var sheet = GD.Load<PackedScene>("res://scenes/ui/kit/paper_sheet.tscn").Instantiate<PaperSheet>();
		host.AddChild(sheet); sheet.Ornaments = false;
		Assert.True(!sheet.GetNode<Control>("CornerTL").Visible && !sheet.GetNode<Control>("CornerBR").Visible, "ornaments hide at runtime");
		sheet.Ornaments = true; Assert.True(sheet.GetNode<Control>("CornerTL").Visible, "ornaments restore"); sheet.Free();
	}

	static int Count<T>(Node n) where T : Node => (n is T ? 1 : 0) + n.GetChildren().Sum(Count<T>);
}
