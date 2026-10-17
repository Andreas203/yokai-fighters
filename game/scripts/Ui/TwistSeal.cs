using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-139 (Y3): the elder-twist icon, drawn in code as a red seal with a paper-coloured shape: PLACEHOLDER art in the
/// paper-talisman style (final icons are the Astra pass). The shape comes from the twist's <c>icon</c> id
/// (<c>twist-grip</c>, <c>twist-armour</c>, <c>twist-reflect</c>); an id with no shape here shows <see cref="Fallback"/>
/// (the map styles' glyph), so a new twist needs only a styles entry.
/// </summary>
[Tool]
public partial class TwistSeal : Control
{
	string _icon = "", _fallback = "E";
	Color _fill = new(0.72f, 0.2f, 0.16f);

	[Export] public string Icon { get => _icon; set { _icon = value; QueueRedraw(); } }
	[Export] public string Fallback { get => _fallback; set { _fallback = value; QueueRedraw(); } }
	[Export] public Color Fill { get => _fill; set { _fill = value; QueueRedraw(); } }

	public static readonly Color Paper = new(0.945f, 0.91f, 0.831f);

	/// <summary>True when <paramref name="icon"/> has a drawn shape (else the fallback glyph shows).</summary>
	public static bool HasShape(string icon) => icon is "twist-grip" or "twist-armour" or "twist-reflect";

	public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;
	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	public override void _Draw()
	{
		float s = Mathf.Min(Size.X, Size.Y);
		var o = (Size - new Vector2(s, s)) / 2f;
		int r = (int)(s * 0.2f);
		var box = new StyleBoxFlat { BgColor = _fill, BorderColor = new Color(0.114f, 0.106f, 0.129f), CornerRadiusTopLeft = r, CornerRadiusTopRight = r, CornerRadiusBottomLeft = r, CornerRadiusBottomRight = r };
		box.SetBorderWidthAll(Mathf.Max(1, (int)(s / 14f)));
		DrawStyleBox(box, new Rect2(o, new Vector2(s, s)));
		Vector2 P(float x, float y) => o + new Vector2(x, y) * s;
		float w = Mathf.Max(1.5f, s / 11f);
		switch (_icon)
		{
			case "twist-grip": // two linked rings: a hold that cannot be broken
				DrawArc(P(0.38f, 0.5f), s * 0.2f, 0, Mathf.Tau, 28, Paper, w, true);
				DrawArc(P(0.62f, 0.5f), s * 0.2f, 0, Mathf.Tau, 28, Paper, w, true);
				break;
			case "twist-armour": // a shield
				DrawPolyline(new[] { P(0.5f, 0.2f), P(0.78f, 0.3f), P(0.74f, 0.6f), P(0.5f, 0.82f), P(0.26f, 0.6f), P(0.22f, 0.3f), P(0.5f, 0.2f) }, Paper, w, true);
				DrawLine(P(0.5f, 0.28f), P(0.5f, 0.74f), Paper, w * 0.7f, true);
				break;
			case "twist-reflect": // two arrows turning back on each other
				DrawLine(P(0.2f, 0.36f), P(0.78f, 0.36f), Paper, w, true);
				DrawPolyline(new[] { P(0.64f, 0.22f), P(0.8f, 0.36f), P(0.64f, 0.5f) }, Paper, w, true);
				DrawLine(P(0.8f, 0.66f), P(0.22f, 0.66f), Paper, w, true);
				DrawPolyline(new[] { P(0.36f, 0.52f), P(0.2f, 0.66f), P(0.36f, 0.8f) }, Paper, w, true);
				break;
			default:
				var font = ThemeDB.FallbackFont;
				int fs = (int)(s * 0.6f);
				var sz = font.GetStringSize(_fallback, fontSize: fs);
				DrawString(font, P(0.5f, 0.5f) + new Vector2(-sz.X / 2f, sz.Y * 0.3f), _fallback, HorizontalAlignment.Left, -1, fs, Paper);
				break;
		}
	}
}
