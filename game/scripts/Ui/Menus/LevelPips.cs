using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Ui;

/// <summary>Three small square pips for a special's level (A3, Lv 1-3): filled red up to <see cref="Level"/>, ink outline for the rest.</summary>
[Tool]
public partial class LevelPips : Control
{
	private int _level = 1, _max = 3;
	[Export] public int Level { get => _level; set { _level = value; QueueRedraw(); } }
	[Export] public int Max { get => _max; set { _max = value; QueueRedraw(); } }

	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	public override void _Draw()
	{
		float s = Mathf.Min(Size.Y, 22f), gap = 8f;
		float x = Size.X - 3 * (s + gap) + gap;
		for (int i = 0; i < 3; i++)
		{
			var r = new Rect2(x + i * (s + gap), (Size.Y - s) / 2f, s, s);
			if (i < _level) DrawRect(r, FightHud.Seal);
			DrawRect(r, i < _max ? FightHud.Ink : new Color(FightHud.Ink, 0.25f), false, 2f);
		}
	}
}
