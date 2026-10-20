using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// The scroll framing of the rest stop (docs/design/screens/rough/rest-stop.png, scroll theme): roller rods and pine
/// cloth around the sheet, and three hanging scrolls on pegs. Pure decoration drawn from the rectangles of marker
/// Controls in the scene (<c>FreeHanger</c>, <c>SecondHanger</c>, <c>ForecastHanger</c>, <c>Portrait</c>), so the
/// layout stays in <c>rest_stop.tscn</c>. Drawn shapes only (the plates have no alpha); nothing here carries text or state.
/// </summary>
public partial class RestStopScreenDecor : Control
{
	static readonly Color Rod = new("5A3E2A"), Knob = new("46301F"), Ink = new("1D1B21"), Cloth = new("34483B"), Band = new("2D3A5E"),
		Paper = new("F1E8D4"), Cord = new("5A3E2A"), Dash = new("9A8F73");

	public static readonly string[] Hangers = { "FreeHanger", "SecondHanger", "ForecastHanger" };

	/// <summary>The marker Controls whose rectangles get a hanging scroll; the shop screen (YOK-104) overrides it.</summary>
	protected virtual string[] HangerNames => Hangers;

	public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

	internal static void Bar(CanvasItem c, Rect2 r, Color fill, float radius = 6f)
	{
		var box = new StyleBoxFlat { BgColor = fill, BorderColor = Ink, CornerRadiusBottomLeft = (int)radius, CornerRadiusBottomRight = (int)radius, CornerRadiusTopLeft = (int)radius, CornerRadiusTopRight = (int)radius };
		box.SetBorderWidthAll(2);
		c.DrawStyleBox(box, r);
	}

	/// <summary>The labelled dashed box for a portrait with no art yet; the shop has its plate and turns it off.</summary>
	protected virtual bool PortraitPlaceholder => true;

	/// <summary>Set by the rest stop when the portrait box holds art (the merchant's plate); the dojo master has none yet.</summary>
	public bool PortraitFilled
	{
		get => _portraitFilled;
		set { if (_portraitFilled == value) return; _portraitFilled = value; QueueRedraw(); }
	}
	private bool _portraitFilled;

	public override void _Draw()
	{
		// The unrolled handscroll: pine cloth above and below, rods at both sides sticking out past the paper.
		DrawRect(new Rect2(132, 100, 1656, 880), Cloth);
		DrawRect(new Rect2(132, 100, 1656, 880), Ink, false, 2f);
		foreach (float x in new[] { 94f, 1788f })
		{
			Bar(this, new Rect2(x, 84, 38, 912), Rod);
			Bar(this, new Rect2(x - 6, 66, 50, 20), Knob, 10f);
			Bar(this, new Rect2(x - 6, 994, 50, 20), Knob, 10f);
		}
		DrawRect(new Rect2(132, 122, 1656, 836), Paper);
		foreach (float x in new[] { 137f, 142f, 1778f, 1783f }) DrawLine(new Vector2(x, 126), new Vector2(x, 954), new Color("C9BE9F"), 2f);

		foreach (string name in HangerNames)
			if (GetParent()?.GetNodeOrNull<Control>(name) is { } h) Hanging(h.GetRect());

		if (PortraitPlaceholder && !PortraitFilled && GetParent()?.GetNodeOrNull<Control>("Portrait") is { } p) Placeholder(p.GetRect());
	}

	/// <summary>One hanging scroll: cord to a peg, a rod and an indigo band at top and bottom, paper between.</summary>
	void Hanging(Rect2 r)
	{
		float cx = r.Position.X + r.Size.X / 2f, peg = r.Position.Y - 20f;
		DrawLine(new Vector2(cx, peg), new Vector2(r.Position.X + r.Size.X * 0.2f, r.Position.Y + 4), Cord, 2f);
		DrawLine(new Vector2(cx, peg), new Vector2(r.End.X - r.Size.X * 0.2f, r.Position.Y + 4), Cord, 2f);
		DrawCircle(new Vector2(cx, peg - 2), 6, Rod);
		DrawArc(new Vector2(cx, peg - 2), 6, 0, Mathf.Tau, 16, Ink, 1.5f);
		DrawRect(new Rect2(r.Position.X + 10, r.Position.Y + 8, r.Size.X - 20, r.Size.Y - 16), new Color("F8F1E0"));
		DrawRect(new Rect2(r.Position.X + 10, r.Position.Y + 10, r.Size.X - 20, 22), Band);
		DrawRect(new Rect2(r.Position.X + 10, r.End.Y - 32, r.Size.X - 20, 22), Band);
		foreach (float y in new[] { r.Position.Y, r.End.Y - 12 })
		{
			Bar(this, new Rect2(r.Position.X + 4, y, r.Size.X - 8, 12), Rod, 3f);
			Bar(this, new Rect2(r.Position.X - 6, y - 2, 12, 16), Knob, 6f);
			Bar(this, new Rect2(r.End.X - 6, y - 2, 12, 16), Knob, 6f);
		}
	}

	/// <summary>Labelled dashed box for the portrait that art will fill (no generated plate exists for the human guise).</summary>
	void Placeholder(Rect2 r)
	{
		DrawRect(r, new Color("E3D8BD"));
		DrawLine(r.Position, r.End, new Color("C9BE9F"), 1.5f);
		DrawLine(new Vector2(r.End.X, r.Position.Y), new Vector2(r.Position.X, r.End.Y), new Color("C9BE9F"), 1.5f);
		var tl = r.Position; var tr = new Vector2(r.End.X, r.Position.Y); var br = r.End; var bl = new Vector2(r.Position.X, r.End.Y);
		foreach (var (a, b) in new[] { (tl, tr), (tr, br), (br, bl), (bl, tl) }) DrawDashedLine(a, b, Dash, 2f, 10f);
	}
}
