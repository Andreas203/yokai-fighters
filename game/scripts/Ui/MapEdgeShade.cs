using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-109: the soft shadow at the left or right edge of the map's viewport, so the scroll seems to curve away, plus a small ink
/// chevron while there is more scroll that way. Presentation only; <see cref="MapScreen"/> sets <see cref="More"/>.
/// </summary>
public partial class MapEdgeShade : Control
{
	bool _more;
	[Export] public bool Left { get; set; }
	/// <summary>True while the scroll can still pan in this direction (the chevron shows).</summary>
	public bool More { get => _more; set { if (_more == value) return; _more = value; QueueRedraw(); } }

	public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;
	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	public override void _Draw()
	{
		const int steps = 14;
		float sw = Size.X / steps;
		for (int i = 0; i < steps; i++)
		{
			float t = 1f - (i + 0.5f) / steps; // 1 at the screen edge, 0 inward
			float x = Left ? i * sw : Size.X - (i + 1) * sw;
			DrawRect(new Rect2(x, 0, sw + 0.5f, Size.Y), new Color(0.05f, 0.05f, 0.09f, 0.62f * t * t));
		}
		if (!_more) return;
		float cx = Left ? 30f : Size.X - 30f, cy = Size.Y / 2f, d = Left ? 1f : -1f;
		var chevron = new[] { new Vector2(cx + 12f * d, cy - 22f), new Vector2(cx - 8f * d, cy), new Vector2(cx + 12f * d, cy + 22f) };
		DrawPolyline(chevron, new Color(0.945f, 0.91f, 0.831f, 0.9f), 6f, true);
	}
}
