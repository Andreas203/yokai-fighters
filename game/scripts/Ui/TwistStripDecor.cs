using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// Look of the HUD's bottom strip (Y3 elder twist standing reminder, T6 Tanuki copy line; concepts E1 and H1, YOK-109): a flat
/// rice-paper note with an ink outline and a red edge bar, no rods or cord (the fight strips carry no hanging fittings).
/// Pure decoration drawn from this node's own size; the labels sit on top of it.
/// </summary>
public partial class TwistStripDecor : Control
{
	static readonly Color Ink = new("1D1B21"), Red = new("B5332B"), Paper = new("F8F1E0"), Edge = new("D9CBA8");

	public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;
	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	public override void _Draw()
	{
		float w = Size.X, h = Size.Y;
		DrawRect(new Rect2(4, 4, w - 4, h - 4), new Color(Ink, 0.3f));        // offset paper edge
		DrawRect(new Rect2(0, 0, w - 4, h - 4), Paper);
		DrawRect(new Rect2(14, 6, w - 26, h - 16), Edge, false, 1.5f);          // inner paper line
		DrawRect(new Rect2(0, 0, 10, h - 4), Red);                             // red edge bar
		DrawRect(new Rect2(0, 0, w - 4, h - 4), Ink, false, 3f);
	}
}
