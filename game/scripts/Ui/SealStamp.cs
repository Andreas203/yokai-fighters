using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Ui;

/// <summary>Square red seal with an inset border and a short glyph (the fallback font has no CJK). Fills the node's smaller side.</summary>
[Tool]
public partial class SealStamp : Control
{
	private string _glyph = "";
	private Color _fill = FightHud.Seal;

	[Export] public string Glyph { get => _glyph; set { _glyph = value; QueueRedraw(); } }
	[Export] public Color Fill { get => _fill; set { _fill = value; QueueRedraw(); } }

	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	public override void _Draw() => Paint.Seal(this, Size / 2f, Mathf.Min(Size.X, Size.Y) / 2f, _glyph, _fill);
}
