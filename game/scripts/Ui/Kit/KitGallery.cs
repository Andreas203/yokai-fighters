using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Ui.Kit;

/// <summary>
/// Dev scene showing every kit component in every state (normal, focused, disabled, pressed) on two pages; keys 1 / 2 switch.
/// Components are the kit scenes themselves, instanced here with a pinned <see cref="KitState"/>; the gallery is not shipped UI,
/// so it assembles them in code. <see cref="Audit"/> reports controls that fall outside the 1920x1080 design canvas, text that does not
/// fit its control, and text that would be under <see cref="MinPx720"/> px at 1280x720.
/// </summary>
public partial class KitGallery : Control
{
	public const float MinPx720 = 15f;
	public static readonly Vector2 Design = new(1920, 1080);
	private static readonly KitState[] States = { KitState.Normal, KitState.Focused, KitState.Disabled, KitState.Pressed };

	private readonly Control[] _pages = new Control[2];
	public int Page { get; private set; }
	public Control PageRoot(int i) => _pages[i];

	private static PackedScene Scene(string n) => GD.Load<PackedScene>($"res://scenes/ui/kit/{n}.tscn");

	public override void _Ready()
	{
		var back = Scene("kit_backdrop").Instantiate<KitBackdrop>();
		back.Name = "Backdrop";
		AddChild(back);
		_pages[0] = BuildPageA(); _pages[1] = BuildPageB();
		foreach (var p in _pages) { p.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(p); }
		ShowPage(0);
	}

	public void ShowPage(int i)
	{
		Page = i;
		for (int k = 0; k < _pages.Length; k++) _pages[k].Visible = k == i;
	}

	public override void _UnhandledKeyInput(InputEvent e)
	{
		if (e is InputEventKey { Pressed: true, Keycode: Key.Key1 }) ShowPage(0);
		if (e is InputEventKey { Pressed: true, Keycode: Key.Key2 }) ShowPage(1);
	}

	// ---- helpers ----

	private static Label Text(string t, string variation, HorizontalAlignment h = HorizontalAlignment.Left) => new() { Text = t, ThemeTypeVariation = variation, HorizontalAlignment = h };

	private static KitBrushButton Btn(string scene, string text, KitState s)
	{
		var b = Scene(scene).Instantiate<KitBrushButton>();
		b.Text = text; b.Preview = s;
		return b;
	}

	private static string StateName(KitState s) => s.ToString().ToLowerInvariant();

	private static PaperSheet Sheet(Vector2 pos, Vector2 size, bool card = false)
	{
		var s = Scene("paper_sheet").Instantiate<PaperSheet>();
		s.Card = card; s.Position = pos; s.CustomMinimumSize = size; s.Size = size;
		return s;
	}

	// ---- page A: text, rows, buttons, footers ----

	private Control BuildPageA()
	{
		var page = new Control { Name = "PageA" };
		var sheet = Sheet(new Vector2(60, 50), new Vector2(1120, 980));
		sheet.Name = "WideSheet";
		page.AddChild(sheet);
		var c = sheet.Content!;
		c.AddThemeConstantOverride("separation", 12);
		var heading = Scene("kit_heading").Instantiate<KitHeading>();
		heading.Text = "Paused"; heading.UnderlineWidth = 360;
		c.AddChild(heading);
		c.AddChild(Text("MENU ROWS", "EyebrowLabel"));
		foreach (var s in States) c.AddChild(Btn("menu_row", $"{System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(StateName(s))} row", s));
		c.AddChild(Text("RED BUTTON  /  GHOST BUTTON", "EyebrowLabel"));
		foreach (var kind in new[] { "red_button", "ghost_button" })
		{
			var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 20);
			foreach (var s in States)
			{
				var b = Btn(kind, StateName(s), s);
				b.CustomMinimumSize = new Vector2(230, 84);
				b.AddThemeFontSizeOverride("font_size", 36);
				row.AddChild(b);
			}
			c.AddChild(row);
		}

		var right = new VBoxContainer { Position = new Vector2(1240, 50), CustomMinimumSize = new Vector2(620, 0), Name = "TypeColumn" };
		right.AddThemeConstantOverride("separation", 14);
		page.AddChild(right);
		var type = Sheet(Vector2.Zero, new Vector2(620, 520), true);
		type.Content!.AddChild(Text("Heading", "HeadingLabel"));
		type.Content.AddChild(Text("Subheading", "SubheadingLabel"));
		type.Content.AddChild(Text("Body: bold serif for text you read.", "BodyLabel"));
		type.Content.AddChild(Text("Caption: smaller, faded ink.", "CaptionLabel"));
		type.Content.AddChild(Text("EYEBROW LABEL", "EyebrowLabel"));
		var sw = new HBoxContainer(); sw.AddThemeConstantOverride("separation", 8);
		foreach (var (hex, col) in new[] { ("indigo", "2D3A5E"), ("ink", "1D1B21"), ("rice", "F1E8D4"), ("pine", "34483B"), ("red", "B5332B"), ("persimmon", "D8632C") })
		{
			var v = new VBoxContainer();
			v.AddChild(new ColorRect { Color = new Color(col), CustomMinimumSize = new Vector2(80, 56) });
			v.AddChild(Text(hex, "CaptionLabel", HorizontalAlignment.Center));
			sw.AddChild(v);
		}
		type.Content.AddChild(sw);
		right.AddChild(type);

		right.AddChild(Text("KEY CHIP FOOTERS", "FooterLabel"));
		foreach (var pin in new[] { FooterDevice.Keyboard, FooterDevice.Pad })
		{
			var f = Scene("key_chip_footer").Instantiate<KeyChipFooter>();
			f.Pinned = pin; f.Name = "Footer" + pin;
			f.SetHints(new KeyHint("confirm", "Confirm"), new KeyHint("back", "Back"));
			right.AddChild(f);
		}
		var f3 = Scene("key_chip_footer").Instantiate<KeyChipFooter>();
		f3.Pinned = FooterDevice.Keyboard; f3.Name = "FooterChoose";
		f3.SetHints(new KeyHint("choose", "Choose"), new KeyHint("confirm", "Take"));
		right.AddChild(f3);
		return page;
	}

	// ---- page B: cards, dialog, ornaments, backdrop ----

	private Control BuildPageB()
	{
		var page = new Control { Name = "PageB" };
		int x = 60;
		foreach (var s in States)
		{
			var card = Scene("kit_card").Instantiate<KitCard>();
			card.Position = new Vector2(x, 70); card.Size = new Vector2(420, 280);
			card.GetNode<Label>("Box/Title").Text = $"{StateName(s)} card";
			card.GetNode<Label>("Box/Body").Text = s switch { KitState.Disabled => "Greyed out: needs a projectile.", KitState.Focused => "Red brush under, persimmon frame.", KitState.Pressed => "Pushed in while held.", _ => "A plain paper card." };
			card.Preview = s;
			card.Name = "Card" + s;
			page.AddChild(card);
			x += 460;
		}
		var dialog = Scene("paper_dialog").Instantiate<PaperDialog>();
		dialog.Name = "InlineDialog";
		dialog.ShowDim = false;
		dialog.Visible = true; dialog.SetAnchorsPreset(LayoutPreset.TopLeft);
		dialog.Position = new Vector2(30, 400); dialog.Size = new Vector2(940, 600);
		var back = Scene("kit_backdrop").Instantiate<KitBackdrop>();
		back.SetAnchorsPreset(LayoutPreset.TopLeft); back.Name = "DialogBackdrop"; back.Position = new Vector2(30, 400); back.Size = new Vector2(940, 600); back.Dim = 0.4f;
		page.AddChild(back);
		page.AddChild(dialog);

		// corner slots on a small sheet
		var slots = Sheet(new Vector2(1010, 400), new Vector2(380, 270), true);
		slots.Name = "OrnamentSheet";
		page.AddChild(slots);
		foreach (OrnamentCorner k in System.Enum.GetValues<OrnamentCorner>())
		{
			var o = Scene("corner_ornament").Instantiate<CornerOrnament>();
			o.Corner = k; o.CustomMinimumSize = new Vector2(100, 100); o.Size = new Vector2(100, 100);
			o.Position = new Vector2(k is OrnamentCorner.TopRight or OrnamentCorner.BottomRight ? 380 - 110 : 10, k is OrnamentCorner.BottomLeft or OrnamentCorner.BottomRight ? 270 - 110 : 10);
			slots.AddChild(o);
		}
		var cap = Text("Corner ornament slots", "CaptionLabel", HorizontalAlignment.Center);
		cap.SizeFlagsVertical = Control.SizeFlags.ExpandFill; cap.VerticalAlignment = VerticalAlignment.Center;
		slots.Content!.AddChild(cap);

		for (int i = 0; i < 2; i++)
		{
			var b = Scene("kit_backdrop").Instantiate<KitBackdrop>();
			b.SetAnchorsPreset(LayoutPreset.TopLeft); b.Name = i == 0 ? "BackdropClear" : "BackdropDim";
			b.Position = new Vector2(1010 + i * 440, 700); b.Size = new Vector2(400, 225); b.Dim = i * 0.6f;
			page.AddChild(b);
			var l = Text(i == 0 ? "Backdrop, dim 0" : "Backdrop, dim 0.6", "FooterLabel");
			l.Position = b.Position + new Vector2(0, 232);
			page.AddChild(l);
		}
		return page;
	}

	// ---- audit ----

	/// <summary>Problems found on the page, empty when the layout holds. Call after the page has been laid out at <see cref="Design"/> size.</summary>
	public static List<string> Audit(Control page)
	{
		var bad = new List<string>();
		var canvas = new Rect2(Vector2.Zero, Design);
		void Walk(Node n)
		{
			if (n is Control { Visible: true } c && n != page)
			{
				var r = c.GetGlobalRect();
				if (c is not KitBackdrop && c.GetParent() is not KitBackdrop && c.Name != "Stroke" && !canvas.Grow(1).Encloses(r)) bad.Add($"{PathOf(page, c)} outside canvas {r}");
				if (c is Label l && l.Text.Length > 0)
				{
					var min = l.GetMinimumSize();
					if (l.AutowrapMode == TextServer.AutowrapMode.Off && min.X > l.Size.X + 1f) bad.Add($"{PathOf(page, c)} text clipped ({min.X} > {l.Size.X})");
					if (l.GetThemeFontSize("font_size") * 720f / 1080f < MinPx720 && l.Name != "Glyph") bad.Add($"{PathOf(page, c)} font {l.GetThemeFontSize("font_size")} below {MinPx720}px at 720p");
				}
				if (c is Button b && b.Text.Length > 0 && b.GetMinimumSize().X > b.Size.X + 1f) bad.Add($"{PathOf(page, c)} label wider than button");
			}
			foreach (var ch in n.GetChildren()) Walk(ch);
		}
		Walk(page);
		return bad;
	}

	private static string PathOf(Node root, Node n) => root.GetPathTo(n).ToString();

	/// <summary>Resolves container layout synchronously (tests and the capture tool cannot wait for the idle sort).</summary>
	public static void ForceLayout(Control c)
	{
		foreach (var ch in c.GetChildren()) if (ch is Control cc) ForceLayout(cc);
		c.Notification((int)Container.NotificationSortChildren);
		c.UpdateMinimumSize();
	}
}
