using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Ui;

/// <summary>Godot-drawn paper-talisman shapes (V1). The generated paper/seal textures have no alpha (RGB; seal is a known retake), so shapes are drawn.</summary>
public static class Paint
{
	public static void Talisman(CanvasItem c, Rect2 r, Color paper, Color? border = null)
	{
		c.DrawRect(new Rect2(r.Position + new Vector2(8, 10), r.Size), new Color(0, 0, 0, 0.35f));
		c.DrawRect(r, paper);
		for (int i = 1; i < 6; i++)
			c.DrawLine(r.Position + new Vector2(20, r.Size.Y * i / 6f), r.Position + new Vector2(r.Size.X - 20, r.Size.Y * i / 6f + (i % 2 == 0 ? 3 : -3)), new Color(0.6f, 0.52f, 0.38f, 0.12f), 2f);
		c.DrawRect(r, border ?? FightHud.Ink, false, 5f);
		c.DrawRect(r.Grow(-12), FightHud.Seal, false, 2f);
	}

	/// <summary>Tapered brush stroke: a thick bar with a thin tail.</summary>
	public static void Brush(CanvasItem c, Vector2 from, float length, float thick, Color col)
	{
		c.DrawPolygon(new[] { from + new Vector2(0, -thick / 2), from + new Vector2(length * 0.8f, -thick * 0.35f), from + new Vector2(length, 0), from + new Vector2(length * 0.8f, thick * 0.4f), from + new Vector2(0, thick / 2) }, new[] { col });
	}

	/// <summary>Square red seal with an inset border; a short Latin glyph (the fallback font has no CJK).</summary>
	public static void Seal(CanvasItem c, Vector2 centre, float radius, string glyph, Color? fill = null)
	{
		c.DrawRect(new Rect2(centre - new Vector2(radius, radius), new Vector2(radius * 2, radius * 2)), fill ?? FightHud.Seal);
		c.DrawRect(new Rect2(centre - new Vector2(radius - 5, radius - 5), new Vector2(radius * 2 - 10, radius * 2 - 10)), FightHud.Paper, false, 2f);
		if (glyph.Length == 0) return;
		int size = (int)(radius * 1.2f);
		c.DrawString(ThemeDB.FallbackFont, centre + new Vector2(-radius, size * 0.35f), glyph, HorizontalAlignment.Center, radius * 2, size, FightHud.Paper);
	}
}
