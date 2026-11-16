using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-98 (R7): the dojo trial's quest during its attempts, on a small unrolled handscroll under the fight HUD: the Astra
/// plate <c>ui/dojo-trial-scroll.png</c> (rollers with red caps at the ends, indigo mounting, blank rice paper;
/// <c>assets/specs/ui/dojo-trial-scroll.md</c>), drawn nine-slice at half scale. Red kicker (the special and the attempt)
/// over the quest in ink. The opening card draws the same plate (<see cref="TwistScroll.SetPlate"/>). Presentation only:
/// the host passes every word.
/// </summary>
public partial class TrialScrollBanner : Control
{
	static readonly Color Ink = new("1D1B21"), Red = new(0.71f, 0.2f, 0.17f);

	public const string PlatePath = "res://assets/generated/ui/dojo-trial-scroll.png";
	/// <summary>The plate's nine-slice margins in its own pixels (1200 x 391): rollers and caps at the sides, deckled edges top and bottom.</summary>
	public const int PlateMarginX = 112, PlateMarginY = 60;
	public const float BannerWidth = 1180f, BannerHeight = 190f, PlateScale = 0.5f, Pad = 40f;
	public const int KickerSize = 24, QuestSize = 28, QuestSizeMin = 20;

	readonly Label _kicker, _quest;
	StyleBox? _plate;

	public string Kicker => _kicker.Text;
	public string Quest => _quest.Text;
	public int QuestFontSize => _quest.GetThemeFontSize("font_size");
	/// <summary>The blank paper between the rollers, in this node's coordinates.</summary>
	public static Rect2 Paper => new(PlateMarginX * PlateScale + 4f, 26f, BannerWidth - 2 * (PlateMarginX * PlateScale + 4f), BannerHeight - 52f);
	/// <summary>The quest's box under the kicker: two lines at <see cref="QuestSize"/>.</summary>
	static Rect2 QuestBox => new(Paper.Position.X + Pad, Paper.Position.Y + 40f, Paper.Size.X - 2 * Pad, Paper.Size.Y - 44f);

	/// <summary>The plate as a nine-slice style box (drawn 1:1 in the plate's pixels); null when the file is missing.</summary>
	public static StyleBoxTexture? LoadPlate()
	{
		var tex = UiTextures.Load(PlatePath);
		if (tex == null) return null;
		var box = new StyleBoxTexture { Texture = tex };
		box.SetTextureMargin(Side.Left, PlateMarginX); box.SetTextureMargin(Side.Right, PlateMarginX);
		box.SetTextureMargin(Side.Top, PlateMarginY); box.SetTextureMargin(Side.Bottom, PlateMarginY);
		return box;
	}

	public TrialScrollBanner()
	{
		Name = "TrialScroll";
		MouseFilter = MouseFilterEnum.Ignore;
		Size = new Vector2(BannerWidth, BannerHeight);
		_kicker = new Label { Name = "Kicker", HorizontalAlignment = HorizontalAlignment.Center, Position = new Vector2(Paper.Position.X + Pad, Paper.Position.Y + 6f), Size = new Vector2(Paper.Size.X - 2 * Pad, 34f), MouseFilter = MouseFilterEnum.Ignore };
		_kicker.AddThemeColorOverride("font_color", Red);
		_kicker.AddThemeFontSizeOverride("font_size", KickerSize);
		_kicker.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		_kicker.ClipText = true;
		_quest = new Label { Name = "Quest", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Position = QuestBox.Position, Size = QuestBox.Size, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
		_quest.AddThemeColorOverride("font_color", Ink);
		_quest.AddThemeFontSizeOverride("font_size", QuestSize);
		_quest.ClipText = true;
		AddChild(_kicker);
		AddChild(_quest);
	}

	/// <summary>Fill the strip; a long quest steps down to <see cref="QuestSizeMin"/> so it stays on the paper.</summary>
	public void Show(string kicker, string quest)
	{
		_kicker.Text = kicker;
		_quest.Text = quest;
		var font = _quest.GetThemeFont("font");
		int size = QuestSize;
		while (size > QuestSizeMin && font.GetMultilineStringSize(quest, HorizontalAlignment.Center, QuestBox.Size.X, size).Y > QuestBox.Size.Y) size--;
		_quest.AddThemeFontSizeOverride("font_size", size);
		QueueRedraw();
	}

	/// <summary>True when the quest fits its box at its current size (nothing clipped off the paper).</summary>
	public bool QuestFits => _quest.GetThemeFont("font").GetMultilineStringSize(_quest.Text, HorizontalAlignment.Center, QuestBox.Size.X, QuestFontSize).Y <= QuestBox.Size.Y;

	public override void _Draw()
	{
		_plate ??= LoadPlate();
		if (_plate == null) { DrawRect(Paper, TwistSeal.Paper); return; }
		// Nine-slice at half scale: the rollers keep their shape, the paper stretches to the strip.
		DrawSetTransform(Vector2.Zero, 0f, new Vector2(PlateScale, PlateScale));
		DrawStyleBox(_plate, new Rect2(0, 0, BannerWidth / PlateScale, BannerHeight / PlateScale));
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}
}
