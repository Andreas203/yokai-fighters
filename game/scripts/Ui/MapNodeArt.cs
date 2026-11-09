using System;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>The marks a map node can carry (YOK-238); the legend shows the same four.</summary>
public enum MapMark { Cleared, Here, Reachable, Selected }

/// <summary>The colours of the map's nodes and marks, shared by the nodes and the legend's swatches so the two cannot drift.</summary>
public static class MapNodePalette
{
	public static readonly Color Paper = new(0.945f, 0.91f, 0.831f), Ink = new(0.114f, 0.106f, 0.129f), Persimmon = new(0.89f, 0.42f, 0.24f),
		ElderRed = new(0.72f, 0.2f, 0.16f), Gold = new(0.98f, 0.8f, 0.3f), Indigo = new(0.176f, 0.227f, 0.369f), Foxfire = new(1f, 0.62f, 0.2f);
}

/// <summary>
/// YOK-238: the brush marks drawn over a map node and in the legend, in code from deterministic noise: the loose persimmon ring (here), the thin
/// indigo ring (reachable), the foxfire glow with its pointer (selected) and the red tick (cleared). Pure drawing; no state.
/// </summary>
public static class MapNodeArt
{
	static Vector2 OnEllipse(Vector2 c, Vector2 r, float ang, float scale) => c + new Vector2(Mathf.Cos(ang) * r.X * scale, Mathf.Sin(ang) * r.Y * scale);

	/// <summary>A hand-painted ring about <paramref name="c"/>: a little more than one turn, thick in the middle of the stroke and thin at both ends, the radius drifting as the brush does.</summary>
	public static void Ring(CanvasItem g, Vector2 c, Vector2 r, Color col, float width, uint seed, float turns = 1.12f)
	{
		const int n = 60;
		float a0 = MapLayout.Unit(seed, 1) * Mathf.Tau, phase = MapLayout.Unit(seed, 2) * Mathf.Tau;
		Vector2 prev = default;
		for (int i = 0; i <= n; i++)
		{
			float t = i / (float)n, ang = a0 + t * turns * Mathf.Tau;
			float scale = 1f + 0.045f * Mathf.Sin(ang * 2f + phase) + 0.07f * (t - 0.5f);
			var p = OnEllipse(c, r, ang, scale);
			float w = width * (0.45f + 0.8f * Mathf.Pow(Mathf.Sin(Mathf.Pi * t), 0.45f)) * (0.88f + 0.24f * MapLayout.Unit(seed, 10 + i / 3));
			if (i > 0) g.DrawLine(prev, p, col, w, true);
			g.DrawCircle(p, w / 2f, col);
			prev = p;
		}
	}

	/// <summary>The selected node's foxfire: soft concentric glows and one bright brass ring.</summary>
	public static void Halo(CanvasItem g, Vector2 c, Vector2 r, float width)
	{
		for (int k = 5; k >= 1; k--)
		{
			var pts = new Vector2[41];
			for (int i = 0; i <= 40; i++) pts[i] = OnEllipse(c, r, i / 40f * Mathf.Tau, 1f);
			g.DrawPolyline(pts, new Color(MapNodePalette.Foxfire, 0.07f + 0.015f * (5 - k)), width + k * width * 1.5f, true);
		}
		var core = new Vector2[41];
		for (int i = 0; i <= 40; i++) core[i] = OnEllipse(c, r, i / 40f * Mathf.Tau, 1f);
		g.DrawPolyline(core, MapNodePalette.Gold, width * 1.1f, true);
		g.DrawPolyline(core, new Color(1f, 0.97f, 0.8f, 0.8f), width * 0.35f, true);
	}

	/// <summary>The small persimmon triangle that points at the selected node from its left, centred on <paramref name="y"/>, tip at <paramref name="tipX"/>.</summary>
	public static void Pointer(CanvasItem g, float tipX, float y, float size)
	{
		var tri = new[] { new Vector2(tipX - size, y - size * 0.8f), new Vector2(tipX - size, y + size * 0.8f), new Vector2(tipX, y) };
		g.DrawColoredPolygon(tri, MapNodePalette.Persimmon);
		g.DrawPolyline(new[] { tri[0], tri[1], tri[2], tri[0] }, MapNodePalette.Ink, 1.6f, true);
	}

	/// <summary>The cleared tick: a red check in a small red box (as on the concept's tags).</summary>
	public static void Tick(CanvasItem g, Rect2 r)
	{
		var red = MapNodePalette.ElderRed;
		g.DrawRect(r.Grow(-1f), red, false, Mathf.Max(1.5f, r.Size.X * 0.09f));
		var a = r.Position + r.Size * new Vector2(0.22f, 0.52f); var b = r.Position + r.Size * new Vector2(0.43f, 0.74f); var c = r.Position + r.Size * new Vector2(0.85f, 0.18f);
		g.DrawPolyline(new[] { a, b, c }, red, Mathf.Max(2f, r.Size.X * 0.15f), true);
	}

	/// <summary>Draws one mark (as the legend shows it) centred in <paramref name="r"/>; <paramref name="blueOnDark"/> brightens the indigo for the legend's dark panel.</summary>
	public static void Swatch(CanvasItem g, Rect2 r, MapMark mark, bool blueOnDark = true)
	{
		var c = r.GetCenter();
		var rad = r.Size * 0.44f;
		switch (mark)
		{
			case MapMark.Cleared: Tick(g, new Rect2(c - new Vector2(r.Size.Y, r.Size.Y) * 0.4f, new Vector2(r.Size.Y, r.Size.Y) * 0.8f)); break;
			case MapMark.Here: Ring(g, c, rad, MapNodePalette.Persimmon, 4.5f, 7); break;
			case MapMark.Reachable: Ring(g, c, rad, blueOnDark ? MapNodePalette.Indigo.Lightened(0.45f) : MapNodePalette.Indigo, 2.8f, 9); break;
			default: Halo(g, c, rad * 0.9f, 2.5f); break;
		}
	}
}
