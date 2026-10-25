using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>How bright a route is drawn: walked (both ends done), open (leads out of Ryo to a reachable node) or idle.</summary>
public enum RouteKind { Idle, Open, Walked }

/// <summary>Draws the map's routes as lines between node edges. Lines come from <see cref="MapScreen"/>; this knows no map rules.</summary>
public partial class MapRoutes : Control
{
	readonly List<(Vector2 A, Vector2 B, RouteKind Kind)> _lines = new();
	public IReadOnlyList<(Vector2 A, Vector2 B, RouteKind Kind)> Lines => _lines;

	public void SetLines(IEnumerable<(Vector2 A, Vector2 B, RouteKind Kind)> lines)
	{
		_lines.Clear(); _lines.AddRange(lines); QueueRedraw();
	}

	public override void _Draw()
	{
		foreach (var (a, b, kind) in _lines)
		{
			if (kind == RouteKind.Walked) continue; // brushed over the others below, so an ink stroke never sits under a plain line
			Color c = kind == RouteKind.Open ? new Color(0.945f, 0.91f, 0.831f) : new Color(0.945f, 0.91f, 0.831f, 0.4f);
			DrawLine(a, b, c, kind == RouteKind.Idle ? 2f : 4f, true);
		}
		foreach (var (a, b, kind) in _lines)
			if (kind == RouteKind.Walked) DrawBrushStroke(a, b);
	}

	public static readonly Color InkPersimmon = new(0.89f, 0.42f, 0.24f), InkDark = new(0.66f, 0.26f, 0.12f), InkLight = new(0.98f, 0.66f, 0.44f);
	const int BrushSamples = 40;
	const float BrushWidth = 17f;

	/// <summary>Deterministic 0..1 noise from an integer, so a stroke looks the same every frame and every run.</summary>
	static float Noise(int n)
	{
		uint h = (uint)n * 2654435761u; h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
		return (h & 0xFFFF) / 65535f;
	}

	/// <summary>Noise that changes slowly along <paramref name="x"/> (value noise, one lattice point every <paramref name="period"/> samples): long runs, not speckle.</summary>
	static float Smooth(int seed, float x, float period)
	{
		float f = x / period; int i = Mathf.FloorToInt(f); float t = f - i; t = t * t * (3 - 2 * t);
		return Mathf.Lerp(Noise(seed + i * 977), Noise(seed + (i + 1) * 977), t);
	}

	/// <summary>The walked route (YOK-109): one persimmon ink stroke, thick in the middle, tapering to a point at both ends, with a ragged edge and dry-brush streaks. Drawn in code, no texture.</summary>
	void DrawBrushStroke(Vector2 a, Vector2 b)
	{
		int seed = (int)(a.X * 31 + a.Y * 17 + b.X * 13 + b.Y * 7);
		a -= new Vector2(10f, 0); b += new Vector2(10f, 0); // the ink runs a little under each card, so the tapered ends read as part of the stroke
		float width = Mathf.Clamp(a.DistanceTo(b) * 0.24f, 10f, BrushWidth);
		float dx = Mathf.Max(24f, b.X - a.X);
		Vector2 c1 = a + new Vector2(dx * 0.45f, 0), c2 = b - new Vector2(dx * 0.45f, 0);
		var p = new Vector2[BrushSamples + 1];
		var n = new Vector2[BrushSamples + 1];
		var w = new float[BrushSamples + 1];
		for (int i = 0; i <= BrushSamples; i++)
		{
			float t = i / (float)BrushSamples, u = 1 - t;
			p[i] = u * u * u * a + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * b;
			Vector2 d = 3 * u * u * (c1 - a) + 6 * u * t * (c2 - c1) + 3 * t * t * (b - c2);
			n[i] = d.Length() > 0.001f ? new Vector2(-d.Y, d.X).Normalized() : Vector2.Down;
			float taper = Mathf.Max(0.18f, Mathf.Pow(Mathf.Sin(Mathf.Pi * t), 0.45f));
			w[i] = width * taper * (0.84f + 0.3f * Smooth(seed, i, 5f)); // an uneven edge: the brush swells and thins along the stroke
		}
		for (int i = 0; i < BrushSamples; i++) // body: segments with round joints
		{
			DrawLine(p[i], p[i + 1], InkPersimmon, w[i], true);
			DrawCircle(p[i], w[i] / 2f, InkPersimmon);
		}
		DrawCircle(p[BrushSamples], w[BrushSamples] / 2f, InkPersimmon);
		for (int k = 0; k < 4; k++) // dry-brush bristle streaks: broken thin lines along the stroke, darker and lighter
		{
			float off = (k - 1.5f) * 0.26f;
			var col = k % 2 == 0 ? InkDark : InkLight;
			col.A = 0.55f;
			for (int i = 2; i < BrushSamples - 2; i++)
			{
				if (Smooth(seed + k * 101, i, 7f) < 0.4f) continue; // a gap in the streak: the dry brush skips
				DrawLine(p[i] + n[i] * off * w[i], p[i + 1] + n[i + 1] * off * w[i + 1], col, 1.5f + 1.2f * Smooth(seed + k, i, 9f), true);
			}
		}
	}
}
