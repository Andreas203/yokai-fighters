using System;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// One thing on the shelf (YOK-159): a standing talisman on a small wooden cradle with a price tag hung from the shelf
/// lip. The shelf shows only an icon, name and price (A12: the description is the merchant's to speak). Focus (keys, pad
/// or mouse hover, one focus for all) lifts the talisman 12 px and frames it and its tag in foxfire. A bought item rolls
/// up in place: tied, sealed, grey borrowed ink. Enter / pad A / click raise <see cref="Activated"/>; the arrows raise
/// <see cref="Navigate"/> because the shelf grid, not Godot's geometry, decides where focus goes (shelves scroll).
/// </summary>
public partial class ShopItem : Control
{
	public const float W = 160, H = 175, TagY = 127, TagH = 44, Lift = 12;
	static readonly Color Ink = new("1D1B21"), Paper = new("F8F1E0"), Seal = new("BD2E26"), Persimmon = new("D86320"), Band = new("2D3A5E"), Foxfire = new("F2A23A"), Grey = new("9A958E");

	public ShopEntry Entry { get; private set; } = null!;
	public int Index { get; private set; }
	public Label NameLabel { get; } = new() { Name = "Name" };
	public Label PriceLabel { get; } = new() { Name = "Price" };
	/// <summary>The talisman's top band text, drawn on the band (SPECIAL or MODIFIER).</summary>
	public string TypeText { get; private set; } = "";
	public Label GlyphLabel { get; } = new() { Name = "Glyph" };
	bool _disabled;

	/// <summary>Bought: rolled up, not offered again, not focusable.</summary>
	public bool CardDisabled { get => _disabled; set { _disabled = value; FocusMode = value ? FocusModeEnum.None : FocusModeEnum.All; QueueRedraw(); } }
	bool _marked;
	/// <summary>Drawn lifted and framed without focus: the modifier whose target Ryo is choosing.</summary>
	public bool Marked { get => _marked; set { _marked = value; Place(); } }
	public bool Lifted => (HasFocus() || _marked) && !_disabled;
	/// <summary>The tag's second line, as shown: the price, "Need 50" when coins are short, or "SOLD · borrowed ink".</summary>
	public string PriceText => PriceLabel.Text;

	public event Action? Activated;
	/// <summary>0 up, 1 down, 2 left, 3 right.</summary>
	public event Action<int>? Navigate;

	public ShopItem() { CustomMinimumSize = new Vector2(W, H); Size = CustomMinimumSize; FocusMode = FocusModeEnum.All; }

	public void Bind(ShopEntry e, int index)
	{
		Entry = e; Index = index;
		bool bought = e.State == ShopCardState.Bought, coins = e.Refusal == ShopRefusal.Coins;
		Name = (e.Kind == ShopKind.Special ? "Special_" : "Modifier_") + e.Id;
		MouseFilter = MouseFilterEnum.Stop;
		foreach (var l in new[] { NameLabel, PriceLabel, GlyphLabel }) { l.MouseFilter = MouseFilterEnum.Ignore; if (l.GetParent() == null) AddChild(l); }
		NameLabel.Text = e.Name; NameLabel.HorizontalAlignment = HorizontalAlignment.Center; NameLabel.ClipText = true;
		NameLabel.AddThemeFontSizeOverride("font_size", e.Name.Length > 12 ? 15 : 17);
		NameLabel.Position = new Vector2(16, TagY + 2); NameLabel.Size = new Vector2(W - 22, 22);
		PriceLabel.Text = bought ? "SOLD · borrowed ink" : coins ? $"Need {e.Price}" : e.Price.ToString();
		PriceLabel.AddThemeFontSizeOverride("font_size", bought ? 12 : 17); PriceLabel.ClipText = true;
		PriceLabel.HorizontalAlignment = bought || coins ? HorizontalAlignment.Center : HorizontalAlignment.Left;
		PriceLabel.Position = new Vector2(bought || coins ? 4 : 84, TagY + 22); PriceLabel.Size = new Vector2(bought || coins ? W - 8 : 60, 20);
		TypeText = e.Kind == ShopKind.Special ? "SPECIAL" : "MODIFIER";
		GlyphLabel.Text = e.Name.Length > 0 ? e.Name[..1].ToUpperInvariant() : "?";
		GlyphLabel.ThemeTypeVariation = "PaperLabel"; GlyphLabel.HorizontalAlignment = HorizontalAlignment.Center; GlyphLabel.VerticalAlignment = VerticalAlignment.Center;
		GlyphLabel.AddThemeFontSizeOverride("font_size", 24);
		GlyphLabel.Size = new Vector2(36, 36); GlyphLabel.ClipText = true;
		GlyphLabel.Visible = !bought;
		// a refused item is dimmed (its tag says why), a bought one is grey borrowed ink
		Modulate = bought ? new Color(0.7f, 0.69f, 0.67f) : e.State == ShopCardState.Refused ? new Color(0.72f, 0.7f, 0.68f) : Colors.White;
		CardDisabled = bought;
		Place();
	}

	void Place()
	{
		float up = Lifted ? Lift : 0;
		GlyphLabel.Position = new Vector2(62, 34 - up);
		QueueRedraw();
	}

	public override void _Ready()
	{
		FocusEntered += Place; FocusExited += Place;
	}

	public override void _GuiInput(InputEvent e)
	{
		if (_disabled) return;
		if (e.IsActionPressed("ui_accept") || e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			if (e is InputEventMouseButton) GrabFocus();
			Activated?.Invoke(); AcceptEvent();
		}
		else if (e.IsActionPressed("ui_up")) { Navigate?.Invoke(0); AcceptEvent(); }
		else if (e.IsActionPressed("ui_down")) { Navigate?.Invoke(1); AcceptEvent(); }
		else if (e.IsActionPressed("ui_left")) { Navigate?.Invoke(2); AcceptEvent(); }
		else if (e.IsActionPressed("ui_right")) { Navigate?.Invoke(3); AcceptEvent(); }
	}

	void Box(Rect2 r, Color fill, float radius = 0, int border = 2)
	{
		var b = new StyleBoxFlat { BgColor = fill, BorderColor = Ink, CornerRadiusBottomLeft = (int)radius, CornerRadiusBottomRight = (int)radius, CornerRadiusTopLeft = (int)radius, CornerRadiusTopRight = (int)radius };
		b.SetBorderWidthAll(border);
		DrawStyleBox(b, r);
	}

	public override void _Draw()
	{
		bool bought = Entry?.State == ShopCardState.Bought, lit = Lifted;
		float up = lit ? Lift : 0;
		if (lit) DrawColoredPolygon(Ellipse(new Vector2(80, 108), 52, 9), new Color(Foxfire, 0.55f)); // glow on the board
		DrawLine(new Vector2(80, 122), new Vector2(80, TagY), Ink, 2f); // the string the tag hangs on
		DrawCircle(new Vector2(80, 122), 3f, new Color("B08A48"));
		if (bought) DrawRolled(); else DrawTalisman(up);
		Box(new Rect2(0, TagY, W, TagH), Paper, 3);
		DrawArc(new Vector2(9, TagY + 11), 3.5f, 0, Mathf.Tau, 12, Ink, 1.5f); // the tag's string hole
		if (!bought && Entry?.Refusal != ShopRefusal.Coins) DrawCoin(new Vector2(70, TagY + 32));
		if (lit)
		{
			var frame = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = Foxfire, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6 };
			frame.SetBorderWidthAll(4);
			DrawStyleBox(frame, new Rect2(42, 6 - up, 76, 106 + up - 0));
			DrawStyleBox(frame, new Rect2(-3, TagY - 3, W + 6, TagH + 6));
		}
	}

	void DrawTalisman(float up)
	{
		bool special = Entry?.Kind == ShopKind.Special;
		Box(new Rect2(44, 96, 72, 10), new Color("5A3E2A"), 3); // the cradle
		Box(new Rect2(48, 12 - up, 64, 90), Paper);
		DrawRect(new Rect2(49, 13 - up, 62, 15), special ? Seal : Band);
		DrawString(GetThemeDefaultFont(), new Vector2(48, 24 - up), TypeText, HorizontalAlignment.Center, 64, 10, Paper);
		Box(new Rect2(62, 34 - up, 36, 36), special ? Persimmon : Band, 5);
		DrawRect(new Rect2(76, 78 - up, 8, 8), Seal);
	}

	void DrawRolled()
	{
		DrawRect(new Rect2(36, 98, 88, 4), new Color(0, 0, 0, 0.25f));
		Box(new Rect2(34, 68, 92, 30), Grey, 12);
		Box(new Rect2(28, 66, 14, 34), new Color("B3AEA6"), 6);
		Box(new Rect2(118, 66, 14, 34), new Color("B3AEA6"), 6);
		DrawRect(new Rect2(74, 68, 12, 30), new Color("77736D")); // the cord
		DrawCircle(new Vector2(80, 83), 8, new Color("8E4B47")); // a seal gone grey-red
	}

	void DrawCoin(Vector2 c)
	{
		DrawCircle(c, 8, new Color("C9A04E"));
		DrawArc(c, 8, 0, Mathf.Tau, 16, Ink, 1.5f);
		DrawRect(new Rect2(c - new Vector2(2.5f, 2.5f), new Vector2(5, 5)), new Color("8A6A2A"), false, 1.2f);
	}

	static Vector2[] Ellipse(Vector2 c, float rx, float ry)
	{
		var pts = new Vector2[24];
		for (int i = 0; i < pts.Length; i++) { float a = i * Mathf.Tau / pts.Length; pts[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry); }
		return pts;
	}
}
