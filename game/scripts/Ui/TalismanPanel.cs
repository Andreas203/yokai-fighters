using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Ui;

/// <summary>
/// Paper talisman background (V1): drop shadow, paper, faint fibre lines, ink border, inset red line. A small
/// custom-drawn node placed in the UI scenes; resize it in the editor, colours are exported.
/// <see cref="Highlight"/> adds the persimmon "selected" frame around it.
/// </summary>
[Tool]
public partial class TalismanPanel : Control
{
	private Color _paper = FightHud.Paper, _border = FightHud.Ink;
	private bool _highlight;

	[Export] public Color Paper { get => _paper; set { _paper = value; QueueRedraw(); } }
	[Export] public Color Border { get => _border; set { _border = value; QueueRedraw(); } }
	[Export] public bool Highlight { get => _highlight; set { _highlight = value; QueueRedraw(); } }

	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	public override void _Draw()
	{
		var r = new Rect2(Vector2.Zero, Size);
		Paint.Talisman(this, r, _paper, _border);
		if (_highlight) DrawRect(r.Grow(8), FightHud.Persimmon, false, 6f);
	}
}
