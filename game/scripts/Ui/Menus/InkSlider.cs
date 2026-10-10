using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Ui;

/// <summary>
/// HSlider drawn as ink on paper: dark track, red brush fill, square seal grabber, persimmon frame when focused.
/// Keeps the stock HSlider behaviour (Left/Right on focus, mouse drag, pad d-pad) and blanks its own drawing.
/// </summary>
public partial class InkSlider : HSlider
{
	public override void _Ready()
	{
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
		DrawRect(new Rect2(pad, cy - 5, w - 2 * pad, 10), FightHud.Ink);
		DrawRect(new Rect2(pad, cy - 5, Mathf.Max(0, x - pad), 10), FightHud.Seal);
		Paint.Seal(this, new Vector2(x, cy), h * 0.36f, "");
		if (HasFocus()) DrawRect(new Rect2(Vector2.Zero, Size), FightHud.Persimmon, false, 4f);
	}
}
