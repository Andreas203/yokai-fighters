using Godot;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-109 (Y3, S2): the elder-twist intro card as a hanging scroll (concept E1, <c>docs/design/screens/elder-twist-card.png</c>).
/// <c>scenes/ui/twist_scroll.tscn</c> holds the live nodes (kicker, title, quote, rule line, seal stamp, portrait, key chip);
/// <see cref="_Draw"/> paints the frame from the node's size with the kit's own pieces: pine cloth (style.md #34483B), wooden
/// rods with knobs, a cord to a peg and the rice-paper inset (<c>paper_sheet_style.tres</c>). No game rule here: the host
/// passes the wording from story data and the icon id from twist data. An opponent with no portrait art leaves the
/// space out (the text column widens), never an empty box.
/// </summary>
public partial class TwistScroll : Control
{
	static readonly Color Pine = new("34483B"), PineEdge = new("4B6355"), Wood = new("7A5535"), WoodDark = new("4A3425"), Ink = new("1D1B21");

	/// <summary>Frame geometry in this node's own coordinates: the peg sits in the head room above the top rod.</summary>
	public const float RodTop = 90f, RodHeight = 24f, KnobExtra = 8f, KnobWidth = 30f;
	public static readonly Rect2 RiceInset = new(84, 140, 1332, 424);
	/// <summary>Text column: left edge and width with a portrait, and without one (the portrait's space is left out).</summary>
	public const float ColumnLeftWithPortrait = 400f, ColumnWidthWithPortrait = 830f, ColumnLeftAlone = 150f, ColumnWidthAlone = 1080f;
	public const int TitleSize = 72, TitleSizeMin = 44, QuoteSize = 34, QuoteSizeMin = 28;

	Label _kicker = null!, _title = null!, _quote = null!, _rule = null!;
	Control _textCol = null!, _portrait = null!, _ruleLine = null!;
	TextureRect _portraitArt = null!;
	TwistSeal _stamp = null!;
	KeyChipFooter _footer = null!;
	StyleBox? _rice;
	bool _built;

	public string Kicker => _kicker.Text;
	public string TitleText => _title.Text;
	public string Quote => _quote.Text;
	public string RuleText => _rule.Text;
	public string StampIcon => _stamp.Icon;
	public bool HasPortrait => _portrait.Visible;
	public Rect2 TextColumn => new(_textCol.Position.X, 0, _textCol.Size.X, Size.Y);
	public int QuoteFontSize => _quote.GetThemeFontSize("font_size");
	public int TitleFontSize => _title.GetThemeFontSize("font_size");
	public KeyChipFooter Footer => _footer;

	/// <summary>True when the title is on one line inside the column and the quote fits its box at its current size (V1: nothing clipped, nothing off the paper).</summary>
	public bool TextFits =>
		_title.GetThemeFont("font").GetStringSize(_title.Text, fontSize: TitleFontSize).X <= _title.Size.X &&
		_quote.GetThemeFont("font").GetMultilineStringSize(_quote.Text, HorizontalAlignment.Left, _quote.Size.X, QuoteFontSize).Y <= _quote.Size.Y;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Bind();
		_footer.SetHints(new KeyHint("confirm", "Continue"));
		_footer.Alignment = BoxContainer.AlignmentMode.Begin;
		MakeItalic(_quote);
	}

	void Bind()
	{
		if (_built) return;
		_built = true;
		_portrait = UiFind.Get<Control>(this, "Portrait");
		_portraitArt = UiFind.Get<TextureRect>(this, "PortraitArt");
		_textCol = UiFind.Get<Control>(this, "TextCol");
		_kicker = UiFind.Get<Label>(this, "Kicker");
		_title = UiFind.Get<Label>(this, "Title");
		_quote = UiFind.Get<Label>(this, "Quote");
		_ruleLine = UiFind.Get<Control>(this, "RuleLine");
		_rule = UiFind.Get<Label>(this, "Rule");
		_stamp = UiFind.Get<TwistSeal>(this, "Stamp");
		_footer = UiFind.Get<KeyChipFooter>(this, "Footer");
	}

	/// <summary>The kit has one bold serif and no italic: the quote leans by skewing the glyphs.</summary>
	static void MakeItalic(Label l)
	{
		var italic = new FontVariation { BaseFont = l.GetThemeFont("font"), VariationTransform = new Transform2D(new Vector2(1, 0), new Vector2(-0.2f, 1), Vector2.Zero) };
		l.AddThemeFontOverride("font", italic);
	}

	/// <summary>Fill the scroll. <paramref name="title"/> null/empty = no title row text; <paramref name="rule"/> null/empty hides the red rule line; a null or missing <paramref name="portraitPath"/> leaves the portrait out.</summary>
	public void Show(string quote, string kicker, string? iconId, string fallbackGlyph, string? title, string? rule, string? portraitPath)
	{
		Bind();
		_kicker.Text = kicker;
		_title.Text = title ?? "";
		_quote.Text = quote;
		_rule.Text = rule ?? "";
		_rule.Visible = _ruleLine.Visible = !string.IsNullOrEmpty(rule);
		_stamp.Icon = iconId ?? ""; _stamp.Fallback = fallbackGlyph;
		Texture2D? art = !string.IsNullOrEmpty(portraitPath) && ResourceLoader.Exists(portraitPath) ? GD.Load<Texture2D>(portraitPath) : null;
		_portraitArt.Texture = art;
		_portrait.Visible = art != null;
		float left = art != null ? ColumnLeftWithPortrait : ColumnLeftAlone, width = art != null ? ColumnWidthWithPortrait : ColumnWidthAlone;
		_textCol.Position = new Vector2(left, _textCol.Position.Y);
		_textCol.Size = new Vector2(width, _textCol.Size.Y);
		foreach (var c in _textCol.GetChildren()) if (c is Control k) k.Size = new Vector2(width, k.Size.Y);
		Fit();
		QueueRedraw();
	}

	/// <summary>Long names and quotes shrink to a floor instead of clipping or leaving the paper.</summary>
	void Fit()
	{
		var tf = _title.GetThemeFont("font");
		int ts = TitleSize;
		while (ts > TitleSizeMin && tf.GetStringSize(_title.Text, fontSize: ts).X > _title.Size.X) ts--;
		_title.AddThemeFontSizeOverride("font_size", ts);
		var qf = _quote.GetThemeFont("font");
		int qs = QuoteSize;
		while (qs > QuoteSizeMin && qf.GetMultilineStringSize(_quote.Text, HorizontalAlignment.Left, _quote.Size.X, qs).Y > _quote.Size.Y) qs--;
		_quote.AddThemeFontSizeOverride("font_size", qs);
	}

	public override void _Draw()
	{
		float w = Size.X, h = Size.Y;
		float bottom = h - RodHeight - KnobExtra; // bottom rod top edge
		// cord and peg
		var peg = new Vector2(w / 2, 16);
		DrawPolyline(new[] { new Vector2(130, RodTop), peg, new Vector2(w - 130, RodTop) }, WoodDark, 5f, true);
		DrawCircle(peg, 14, WoodDark); DrawArc(peg, 14, 0, Mathf.Tau, 24, Ink, 3f, true);
		// pine cloth between the rods
		var cloth = new Rect2(KnobWidth + 6, RodTop + RodHeight, w - 2 * (KnobWidth + 6), bottom - RodTop - RodHeight);
		DrawRect(cloth, Pine);
		DrawRect(cloth.Grow(-10), PineEdge, false, 2f);
		DrawRect(cloth, Ink, false, 3f);
		// rice-paper inset: an offset paper edge line beneath, then the torn-paper sheet
		DrawRect(new Rect2(RiceInset.Position + new Vector2(6, 7), RiceInset.Size), new Color(Ink, 0.35f), false, 2f);
		_rice ??= GD.Load<StyleBox>("res://ui/kit/paper_sheet_style.tres");
		if (_rice != null) DrawStyleBox(_rice, RiceInset);
		// rods with projecting knobs
		Rod(new Rect2(KnobWidth - 10, RodTop, w - 2 * (KnobWidth - 10), RodHeight));
		Rod(new Rect2(KnobWidth - 10, bottom, w - 2 * (KnobWidth - 10), RodHeight));
		foreach (float y in new[] { RodTop, bottom })
		{
			Knob(new Rect2(0, y - KnobExtra, KnobWidth, RodHeight + 2 * KnobExtra));
			Knob(new Rect2(w - KnobWidth, y - KnobExtra, KnobWidth, RodHeight + 2 * KnobExtra));
		}
	}

	void Rod(Rect2 r) => Bar(r, Wood, 6);
	void Knob(Rect2 r) => Bar(r, WoodDark, 9);

	void Bar(Rect2 r, Color fill, int radius)
	{
		var box = new StyleBoxFlat { BgColor = fill, BorderColor = Ink, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius };
		box.SetBorderWidthAll(3);
		DrawStyleBox(box, r);
	}
}
