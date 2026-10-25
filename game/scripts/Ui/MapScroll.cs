using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-109 (designer, 2025-10-25): the map's paper. An unrolled handscroll (emakimono) drawn in code, no texture of its own beyond the
/// shared rice-paper grain: aged paper between two lacquered rollers, a pine-green silk mount along the top and bottom edge, paper-sheet
/// seams, faint sumi hills and pines along the bottom. It carries no information (no labels, no rows), so blank paper is all a hidden
/// row shows. Everything is deterministic (no random calls), so the paper looks the same every frame and every run.
/// <see cref="MapScreen"/> sizes it to the whole scroll (many screens wide) and slides it inside a clipping viewport.
/// </summary>
public partial class MapScroll : Control
{
	public static readonly Color PineGreen = new(0.15f, 0.27f, 0.22f), PineLight = new(0.26f, 0.40f, 0.31f), Gold = new(0.82f, 0.66f, 0.30f),
		Lacquer = new(0.40f, 0.19f, 0.13f), Indigo = new(0.12f, 0.14f, 0.26f), Sumi = new(0.16f, 0.17f, 0.22f);
	public static readonly Color PaperTone = new(0.90f, 0.84f, 0.70f);

	/// <summary>Width of one roller; the paper starts after it plus <see cref="RollWidth"/> of rolled-up paper.</summary>
	public const float RollerWidth = 54f, RollWidth = 30f;
	/// <summary>Where the walkable paper begins and ends along x (inside the rollers), for the screen's layout.</summary>
	public static float PaperLeft => RollerWidth + RollWidth;

	static Texture2D? _grain;
	static Texture2D? Grain => _grain ??= GD.Load<Texture2D>("res://assets/generated/ui/paper-panel.png");

	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	/// <summary>Deterministic 0..1 noise from an integer.</summary>
	static float Noise(int n)
	{
		uint h = (uint)n * 2654435761u; h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
		return (h & 0xFFFF) / 65535f;
	}

	/// <summary>Value noise along <paramref name="x"/>, one lattice point per <paramref name="period"/> px.</summary>
	static float Smooth(int seed, float x, float period)
	{
		float f = x / period; int i = Mathf.FloorToInt(f); float t = f - i; t = t * t * (3 - 2 * t);
		return Mathf.Lerp(Noise(seed + i * 977), Noise(seed + (i + 1) * 977), t);
	}

	public override void _Draw()
	{
		float w = Size.X, h = Size.Y;
		if (w < 4 * RollerWidth || h < 80) return;
		float mount = Mathf.Clamp(h * 0.045f, 18f, 36f);
		float x0 = RollerWidth, x1 = w - RollerWidth;
		var paper = new Rect2(x0, mount, x1 - x0, h - 2 * mount);
		DrawPaper(paper);
		DrawWash(paper);
		DrawEdgeAging(paper);
		DrawMount(x0, x1, h, mount);
		DrawRolledEnd(x0, h, mount, true);
		DrawRolledEnd(x1, h, mount, false);
		DrawRoller(0, h);
		DrawRoller(w - RollerWidth, h);
	}

	void DrawPaper(Rect2 r)
	{
		DrawRect(r, PaperTone);
		var tex = Grain;
		if (tex != null)
		{
			// the grain sheet has a blank margin; its flat middle repeats without a visible seam
			const float src = 760f, off = 132f;
			for (float x = r.Position.X; x < r.End.X; x += src)
				for (float y = r.Position.Y; y < r.End.Y; y += src)
				{
					var dst = new Rect2(x, y, Mathf.Min(src, r.End.X - x), Mathf.Min(src, r.End.Y - y));
					DrawTextureRectRegion(tex, dst, new Rect2(off, off, dst.Size.X, dst.Size.Y), new Color(0.97f, 0.92f, 0.80f));
				}
		}
		// paper sheets are joined every two screens-ish: a faint seam and a slightly different tone per sheet
		float sheet = Mathf.Max(400f, r.Size.Y * 1.25f);
		int k = 0;
		for (float x = r.Position.X; x < r.End.X; x += sheet, k++)
		{
			float sw = Mathf.Min(sheet, r.End.X - x);
			float tone = (Noise(k * 31 + 7) - 0.5f) * 0.10f;
			DrawRect(new Rect2(x, r.Position.Y, sw, r.Size.Y), tone > 0 ? new Color(1f, 0.97f, 0.86f, tone) : new Color(0.35f, 0.25f, 0.12f, -tone));
			if (k > 0)
			{
				DrawLine(new Vector2(x, r.Position.Y), new Vector2(x + 1.5f, r.End.Y), new Color(0.35f, 0.26f, 0.14f, 0.20f), 2f);
				DrawLine(new Vector2(x + 2.5f, r.Position.Y), new Vector2(x + 4f, r.End.Y), new Color(1f, 0.97f, 0.86f, 0.28f), 1.5f);
			}
		}
	}

	/// <summary>Soft brown stains, sumi hills and pines: all low-contrast, behind everything the map says.</summary>
	void DrawWash(Rect2 r)
	{
		for (int i = 0; i < (int)(r.Size.X / 420f); i++) // foxing: a few soft stains, built from fading rings
		{
			var c = new Vector2(r.Position.X + (i + Noise(i * 13 + 3)) * 420f, r.Position.Y + r.Size.Y * (0.15f + 0.7f * Noise(i * 29 + 5)));
			float rad = 40f + 70f * Noise(i * 17 + 9);
			for (int ring = 6; ring >= 1; ring--) DrawCircle(c, rad * ring / 6f, new Color(0.45f, 0.30f, 0.12f, 0.018f));
		}
		float floor = r.End.Y;
		var tints = new[] { new Color(0.25f, 0.33f, 0.34f, 0.12f), new Color(0.20f, 0.27f, 0.28f, 0.10f), new Color(0.15f, 0.22f, 0.22f, 0.10f) };
		for (int layer = 0; layer < 3; layer++)
		{
			float baseH = r.Size.Y * (0.10f + 0.045f * (2 - layer)), amp = r.Size.Y * (0.10f + 0.03f * layer);
			var pts = new System.Collections.Generic.List<Vector2>();
			for (float x = r.Position.X; x < r.End.X + 40f; x += 40f)
			{
				float xx = Mathf.Min(x, r.End.X - 1f); // a strictly rising x, so the polygon is simple (no repeated end point)
				if (pts.Count > 0 && xx <= pts[^1].X) break;
				pts.Add(new Vector2(xx, floor - baseH - amp * Smooth(layer * 101 + 3, xx - r.Position.X, 260f + 120f * layer) * (0.5f + 0.5f * Smooth(layer * 53 + 1, xx, 900f))));
			}
			pts.Add(new Vector2(pts[^1].X, floor)); pts.Add(new Vector2(r.Position.X, floor));
			DrawColoredPolygon(pts.ToArray(), tints[layer]);
		}
		// pines on the foreground hills, spaced unevenly, never the same height twice
		for (float x = r.Position.X + 140f, i = 0; x < r.End.X - 90f; i++)
		{
			float ph = r.Size.Y * (0.18f + 0.12f * Noise((int)i * 7 + 2));
			Pine(new Vector2(x, floor - r.Size.Y * 0.03f * Noise((int)i * 5 + 1)), ph, (int)i);
			x += 300f + 380f * Noise((int)i * 11 + 4);
		}
	}

	/// <summary>One sumi pine in the old style: a crooked trunk and flat, ragged tiers of needles (squashed clouds).</summary>
	void Pine(Vector2 root, float height, int seed)
	{
		var ink = new Color(0.12f, 0.17f, 0.17f, 0.20f);
		float lean = (Noise(seed * 3 + 1) - 0.5f) * height * 0.30f;
		var trunk = new Vector2[6];
		for (int i = 0; i < 6; i++)
		{
			float t = i / 5f;
			trunk[i] = root + new Vector2(lean * t + Mathf.Sin(t * 6f + seed) * height * 0.05f, -height * t * 0.9f);
		}
		DrawPolyline(trunk, new Color(ink, 0.30f), Mathf.Max(3f, height * 0.05f), true);
		for (int tier = 0; tier < 4; tier++)
		{
			float t = 0.30f + 0.20f * tier;
			var c = trunk[Mathf.Clamp((int)(t * 5f), 0, 5)] + new Vector2((Noise(seed * 23 + tier) - 0.5f) * height * 0.2f, 0);
			float rx = height * (0.42f - 0.07f * tier) * (0.85f + 0.3f * Noise(seed * 19 + tier)), ry = rx * 0.30f;
			var pts = new Vector2[28];
			for (int i = 0; i < pts.Length; i++) // a ragged squashed ellipse: needles stick out unevenly
			{
				float ang = i / (float)pts.Length * Mathf.Tau;
				float jag = 0.82f + 0.30f * Noise(seed * 101 + tier * 31 + i);
				pts[i] = c + new Vector2(Mathf.Cos(ang) * rx * jag, Mathf.Sin(ang) * ry * jag); // star-shaped about the centre, so the polygon is always simple
			}
			DrawColoredPolygon(pts, ink);
		}
	}

	/// <summary>The paper darkens toward the silk, like a sheet that has hung in smoke for years.</summary>
	void DrawEdgeAging(Rect2 r)
	{
		float depth = Mathf.Min(44f, r.Size.Y * 0.08f);
		const int steps = 10;
		for (int i = 0; i < steps; i++)
		{
			float a = 0.16f * Mathf.Pow(1f - i / (float)steps, 2f);
			float y = i * depth / steps, hh = depth / steps + 0.5f;
			var c = new Color(0.38f, 0.26f, 0.10f, a);
			DrawRect(new Rect2(r.Position.X, r.Position.Y + y, r.Size.X, hh), c);
			DrawRect(new Rect2(r.Position.X, r.End.Y - y - hh, r.Size.X, hh), c);
		}
	}

	/// <summary>The silk border along the top and bottom of the paper, with a fine gold line each side.</summary>
	void DrawMount(float x0, float x1, float h, float mount)
	{
		foreach (float y in new[] { 0f, h - mount })
		{
			DrawRect(new Rect2(x0, y, x1 - x0, mount), PineGreen);
			for (float x = x0 - mount; x < x1; x += 14f) // a hint of woven brocade: a diagonal lattice in a lighter pine
			{
				DrawLine(new Vector2(Mathf.Max(x0, x), y + (x < x0 ? x0 - x : 0)), new Vector2(Mathf.Min(x1, x + mount), y + mount - (x + mount > x1 ? x + mount - x1 : 0)), new Color(PineLight, 0.35f), 1.5f);
			}
			DrawRect(new Rect2(x0, y, x1 - x0, mount), new Color(Colors.Black, 0.10f), false, 2f);
		}
		float goldTop = mount - 3f, goldBot = h - mount + 2f;
		DrawLine(new Vector2(x0, goldTop), new Vector2(x1, goldTop), new Color(Gold, 0.9f), 2f);
		DrawLine(new Vector2(x0, goldBot), new Vector2(x1, goldBot), new Color(Gold, 0.9f), 2f);
	}

	/// <summary>The part of the sheet still rolled up on a roller: a cylinder of paper, shaded across its width.</summary>
	void DrawRolledEnd(float x, float h, float mount, bool left)
	{
		const int strips = 10;
		float sw = RollWidth / strips;
		for (int i = 0; i < strips; i++)
		{
			float t = (i + 0.5f) / strips;                // 0 against the roller, 1 at the open paper
			float dark = (1f - t) * (1f - t);
			float sx = left ? x + i * sw : x - (i + 1) * sw;
			DrawRect(new Rect2(sx, 0, sw + 0.5f, h), PaperTone.Darkened(0.10f + 0.25f * dark));
			DrawRect(new Rect2(sx, 0, sw + 0.5f, h), new Color(0.30f, 0.21f, 0.10f, 0.50f * dark));
		}
		float edge = left ? x + RollWidth : x - RollWidth;
		DrawLine(new Vector2(edge, mount), new Vector2(edge, h - mount), new Color(0.2f, 0.12f, 0.05f, 0.4f), 1.5f);
	}

	/// <summary>A lacquered wooden roller, shaded as a cylinder, with indigo caps and a gold band at each end.</summary>
	void DrawRoller(float x, float h)
	{
		const int strips = 14;
		float sw = RollerWidth / strips;
		for (int i = 0; i < strips; i++)
		{
			float t = (i + 0.5f) / strips;
			float lit = Mathf.Sin(t * Mathf.Pi);                    // dark at the rim, bright in the middle
			float shine = Mathf.Exp(-Mathf.Pow((t - 0.34f) / 0.07f, 2f)); // one glossy streak
			var c = Lacquer.Darkened(0.55f * (1f - lit)).Lerp(new Color(0.95f, 0.78f, 0.62f), shine * 0.45f);
			DrawRect(new Rect2(x + i * sw, 0, sw + 0.5f, h), c);
		}
		DrawRect(new Rect2(x, 0, RollerWidth, h), new Color(Colors.Black, 0.35f), false, 2f);
		float cap = Mathf.Clamp(h * 0.05f, 18f, 34f);
		foreach (float y in new[] { 0f, h - cap })
		{
			DrawRect(new Rect2(x, y, RollerWidth, cap), Indigo);
			DrawRect(new Rect2(x, y + (y == 0 ? cap - 6 : 0), RollerWidth, 6), Gold);
			DrawRect(new Rect2(x + 6, y + cap * 0.28f, RollerWidth - 12, cap * 0.18f), new Color(1f, 1f, 1f, 0.10f));
			DrawRect(new Rect2(x, y, RollerWidth, cap), new Color(Colors.Black, 0.45f), false, 2f);
		}
	}
}
