using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-58 dusk backdrop for the title screen: indigo sky gradient, a pale moon with soft halo, two ink-wash mountain
/// ridges and drifting mist bands. Godot-drawn placeholder (a generated plate can replace it later); static, so it
/// costs nothing per frame and never competes with the menu for contrast.
/// </summary>
[Tool]
public partial class TitleBackdrop : Control
{
	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	public override void _Draw()
	{
		float w = Size.X, h = Size.Y;
		var top = new Color(0.08f, 0.09f, 0.19f); var mid = new Color(0.17f, 0.18f, 0.32f); var low = new Color(0.36f, 0.27f, 0.36f);
		DrawPolygon(new[] { new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h * 0.55f), new Vector2(0, h * 0.55f) }, new[] { top, top, mid, mid });
		DrawPolygon(new[] { new Vector2(0, h * 0.55f), new Vector2(w, h * 0.55f), new Vector2(w, h), new Vector2(0, h) }, new[] { mid, mid, low, low });
		// moon
		var moon = new Vector2(w * 0.93f, h * 0.15f);
		for (int i = 6; i >= 1; i--) DrawCircle(moon, h * (0.06f + i * 0.025f), new Color(0.93f, 0.89f, 0.78f, 0.035f));
		DrawCircle(moon, h * 0.06f, new Color(0.93f, 0.89f, 0.78f, 0.92f));
		// ridges (far lighter, near darker), then mist
		Ridge(h * 0.66f, h * 0.1f, 0.0035f, 0f, new Color(0.24f, 0.25f, 0.42f, 0.8f));
		Ridge(h * 0.8f, h * 0.12f, 0.0027f, 2.1f, new Color(0.12f, 0.13f, 0.26f, 0.95f));
		for (int i = 0; i < 4; i++)
		{
			float y = h * (0.62f + i * 0.09f);
			DrawColoredPolygon(Ellipse(new Vector2(w * (0.2f + i * 0.22f), y), w * 0.3f, h * 0.018f), new Color(0.93f, 0.89f, 0.78f, 0.06f));
		}
		// vignette strips at the top and bottom keep the logo and hint lines legible
		DrawPolygon(new[] { new Vector2(0, h - h * 0.14f), new Vector2(w, h - h * 0.14f), new Vector2(w, h), new Vector2(0, h) }, new[] { Colors.Transparent, Colors.Transparent, new Color(0.05f, 0.06f, 0.14f, 0.7f), new Color(0.05f, 0.06f, 0.14f, 0.7f) });
	}

	private void Ridge(float baseY, float amp, float freq, float phase, Color col)
	{
		float w = Size.X, h = Size.Y;
		var pts = new System.Collections.Generic.List<Vector2>();
		for (float x = 0; x <= w + 20; x += 20)
			pts.Add(new Vector2(x, baseY - amp * (0.55f * Mathf.Sin(x * freq + phase) + 0.3f * Mathf.Sin(x * freq * 2.7f + phase * 1.7f) + 0.5f)));
		pts.Add(new Vector2(w, h)); pts.Add(new Vector2(0, h));
		DrawColoredPolygon(pts.ToArray(), col);
	}

	private static Vector2[] Ellipse(Vector2 c, float rx, float ry)
	{
		var p = new Vector2[24];
		for (int i = 0; i < 24; i++) p[i] = c + new Vector2(Mathf.Cos(Mathf.Tau * i / 24) * rx, Mathf.Sin(Mathf.Tau * i / 24) * ry);
		return p;
	}
}
