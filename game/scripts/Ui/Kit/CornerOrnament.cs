using Godot;

namespace YokaiFighters.Ui.Kit;

public enum OrnamentCorner { TopLeft, TopRight, BottomLeft, BottomRight }

/// <summary>
/// Slot for a corner decoration (blossom branch, ink cloud, fox-mask motif). Set <see cref="Art"/> to a texture drawn for the
/// top-left corner; the other corners mirror it. With no art it draws a plain placeholder: two ink strokes and a red dot.
/// </summary>
[Tool]
public partial class CornerOrnament : Control
{
	private OrnamentCorner _corner = OrnamentCorner.TopLeft;
	private Texture2D? _art;

	[Export] public OrnamentCorner Corner { get => _corner; set { _corner = value; QueueRedraw(); } }
	[Export] public Texture2D? Art { get => _art; set { _art = value; QueueRedraw(); } }

	public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; }
	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	public override void _Draw()
	{
		bool fx = _corner is OrnamentCorner.TopRight or OrnamentCorner.BottomRight;
		bool fy = _corner is OrnamentCorner.BottomLeft or OrnamentCorner.BottomRight;
		// Draw in top-left space, then mirror by transform so the art and the placeholder share one path.
		DrawSetTransform(new Vector2(fx ? Size.X : 0f, fy ? Size.Y : 0f), 0f, new Vector2(fx ? -1f : 1f, fy ? -1f : 1f));
		if (_art != null) { DrawTextureRect(_art, new Rect2(Vector2.Zero, Size), false); return; }
		var ink = new Color(0.114f, 0.106f, 0.129f, 0.9f);
		float w = Size.X, h = Size.Y;
		DrawLine(new Vector2(0, 3), new Vector2(w * 0.7f, 3), ink, 6f, true);
		DrawLine(new Vector2(3, 0), new Vector2(3, h * 0.7f), ink, 6f, true);
		DrawLine(new Vector2(0, 14), new Vector2(w * 0.35f, 14), ink, 3f, true);
		DrawLine(new Vector2(14, 0), new Vector2(14, h * 0.35f), ink, 3f, true);
		DrawCircle(new Vector2(w * 0.28f, h * 0.28f), 7f, new Color(0.710f, 0.200f, 0.169f));
	}
}
