using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// Scroll look of the HUD's bottom strip (Y3 elder twist, T6 Tanuki copy line; YOK-121), matching the rest stop's
/// scroll decor (<see cref="RestStopScreenDecor"/>): roller rods with knobs at both ends, pine cloth above and below,
/// indigo bands, rice paper between. Pure decoration drawn from this node's own size; the labels sit on top of it.
/// </summary>
public partial class TwistStripDecor : Control
{
	static readonly Color Rod = new("5A3E2A"), Knob = new("46301F"), Ink = new("1D1B21"), Cloth = new("34483B"), Band = new("2D3A5E"), Paper = new("F8F1E0");

	public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;
	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	static void Bar(CanvasItem c, Rect2 r, Color fill, float radius)
	{
		var box = new StyleBoxFlat { BgColor = fill, BorderColor = Ink, CornerRadiusBottomLeft = (int)radius, CornerRadiusBottomRight = (int)radius, CornerRadiusTopLeft = (int)radius, CornerRadiusTopRight = (int)radius };
		box.SetBorderWidthAll(2);
		c.DrawStyleBox(box, r);
	}

	public override void _Draw()
	{
		float w = Size.X, h = Size.Y, rod = 18f;
		DrawRect(new Rect2(rod, 0, w - 2 * rod, h), Cloth);                       // pine cloth
		DrawRect(new Rect2(rod, 0, w - 2 * rod, h), Ink, false, 2f);
		DrawRect(new Rect2(rod + 2, 6, w - 2 * rod - 4, h - 12), Paper);          // rice paper
		DrawRect(new Rect2(rod + 2, 6, w - 2 * rod - 4, 4), Band);                // indigo bands
		DrawRect(new Rect2(rod + 2, h - 10, w - 2 * rod - 4, 4), Band);
		foreach (float x in new[] { 0f, w - rod })                                // roller rods with knobs
		{
			Bar(this, new Rect2(x, 4, rod, h - 8), Rod, 5f);
			Bar(this, new Rect2(x - 1, 0, rod + 2, 8), Knob, 4f);
			Bar(this, new Rect2(x - 1, h - 8, rod + 2, 8), Knob, 4f);
		}
	}
}
