using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Ui;

/// <summary>
/// HSlider drawn as ink on paper: dark track, red brush fill, square seal grabber, persimmon frame when focused.
/// Keeps the stock HSlider behaviour (Left/Right on focus, mouse drag, pad d-pad) and blanks its own drawing.
/// </summary>
public partial class InkSlider : HSlider
{
	private Texture2D? _brush;
	public override void _Ready()
	{
		_brush = GD.Load<Texture2D>("res://assets/generated/ui/red-brush.png");
		var empty = new StyleBoxEmpty();
		foreach (string s in new[] { "slider", "grabber_area", "grabber_area_highlight" }) AddThemeStyleboxOverride(s, empty);
		var blank = ImageTexture.CreateFromImage(Image.CreateEmpty(1, 1, false, Image.Format.Rgba8));
		foreach (string i in new[] { "grabber", "grabber_highlight", "grabber_disabled" }) AddThemeIconOverride(i, blank);
		FocusMode = FocusModeEnum.All;
		Step = 5;
		ValueChanged += _ => QueueRedraw();
		FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
	}

	public override void _Draw()
	{
		float h = Size.Y, w = Size.X, pad = h * 0.5f;
		float frac = (float)((Value - MinValue) / (MaxValue - MinValue));
		float x = pad + (w - 2 * pad) * frac, cy = h / 2f;
		if (_brush != null)
		{
			DrawTextureRect(_brush, new Rect2(pad, cy - 12, w - 2 * pad, 24), false, FightHud.Ink);
			if (x > pad) DrawTextureRect(_brush, new Rect2(pad, cy - 12, x - pad, 24), false, FightHud.Seal);
		}
		DrawCircle(new Vector2(x, cy), 23, FightHud.Ink);
		DrawCircle(new Vector2(x, cy), 19, FightHud.Seal);
		if (HasFocus()) DrawArc(new Vector2(x, cy), 26, 0, Mathf.Tau, 48, FightHud.Persimmon, 3, true);
	}
}
