using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Ui;

/// <summary>Tapered brush stroke (thick bar, thin tail) across the node's width, centred on its height. Set colour and thickness in the editor.</summary>
[Tool]
public partial class BrushStroke : Control
{
	private Color _ink = FightHud.Ink;
	private float _thickness = 8f;

	[Export] public Color Ink { get => _ink; set { _ink = value; QueueRedraw(); } }
	[Export] public float Thickness { get => _thickness; set { _thickness = value; QueueRedraw(); } }

	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	public override void _Draw() => Paint.Brush(this, new Vector2(0f, Size.Y / 2f), Size.X, _thickness, _ink);
}
