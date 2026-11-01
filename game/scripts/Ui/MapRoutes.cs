using System.Collections.Generic;
using System.Linq;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>How a route is drawn: walked (both ends done), open (leads out of Ryo to a node he can pick) or idle.</summary>
public enum RouteKind { Idle, Open, Walked }

/// <summary>
/// Draws the map's routes as brush strokes on the scroll (YOK-109, designer 2026-10-25). The walked route is a bold persimmon stroke, an open
/// route (the choice ahead) a fainter sumi one, an idle one fainter still; all are tapered at both ends with a ragged edge, bristle streaks
/// and dry gaps, drawn in code from deterministic noise, no texture. Lines come from <see cref="MapScreen"/>; this knows no map rules.
/// <see cref="Paint"/> makes chosen strokes paint in from their start over a short time (presentation only, <see cref="FinishPainting"/>
/// ends it at once; nothing reads it back).
/// </summary>
public partial class MapRoutes : Control
{
	readonly List<(Vector2 A, Vector2 B, RouteKind Kind)> _lines = new();
	readonly Dictionary<(Vector2, Vector2), float> _progress = new(); // 0..1 while a stroke paints in; absent = fully painted
	float _paintSeconds = 0.6f;
	public IReadOnlyList<(Vector2 A, Vector2 B, RouteKind Kind)> Lines => _lines;
	/// <summary>True while any stroke is still painting in.</summary>
	public bool IsPainting => _progress.Count > 0;

	public override void _Ready() => SetProcess(false);

	public void SetLines(IEnumerable<(Vector2 A, Vector2 B, RouteKind Kind)> lines)
	{
		_lines.Clear(); _lines.AddRange(lines);
		foreach (var k in _progress.Keys.Where(k => !_lines.Any(l => l.A == k.Item1 && l.B == k.Item2)).ToList()) _progress.Remove(k); // a stroke that is gone stops painting
		SetProcess(_progress.Count > 0);
		QueueRedraw();
	}

	/// <summary>Start painting these strokes (by their end points, as given to <see cref="SetLines"/>) from nothing to whole in <paramref name="seconds"/>.</summary>
	public void Paint(IEnumerable<(Vector2 A, Vector2 B)> strokes, float seconds = 0.6f)
	{
		foreach (var (a, b) in strokes) if (_lines.Any(l => l.A == a && l.B == b)) _progress[(a, b)] = 0f;
		_paintSeconds = Mathf.Max(0.05f, seconds);
		SetProcess(_progress.Count > 0);
		QueueRedraw();
	}

	/// <summary>Skip the animation: every stroke is whole now.</summary>
	public void FinishPainting() { if (_progress.Count == 0) return; _progress.Clear(); SetProcess(false); QueueRedraw(); }

	public override void _Process(double delta)
	{
		foreach (var k in _progress.Keys.ToList())
		{
			float p = _progress[k] + (float)delta / _paintSeconds;
			if (p >= 1f) _progress.Remove(k); else _progress[k] = p;
		}
		if (_progress.Count == 0) SetProcess(false);
		QueueRedraw();
	}

	public override void _Draw()
	{
		foreach (var kind in new[] { RouteKind.Idle, RouteKind.Open, RouteKind.Walked }) // bold ink over faint ink, never the other way round
			foreach (var (a, b, k) in _lines)
				if (k == kind) DrawBrushStroke(a, b, Style(k), _progress.TryGetValue((a, b), out float p) ? Ease(p) : 1f);
	}

	static float Ease(float t) => 1f - (1f - t) * (1f - t);

	/// <summary>How a stroke looks: body colour, streak colours, width limits, and the paper colour dry gaps show.</summary>
	readonly record struct StrokeStyle(Color Body, Color Dark, Color Light, float MinWidth, float MaxWidth, float Gaps);

	public static readonly Color InkPersimmon = new(0.89f, 0.42f, 0.24f), InkDark = new(0.66f, 0.26f, 0.12f), InkLight = new(0.98f, 0.66f, 0.44f);
	public static readonly Color InkSumi = new(0.36f, 0.32f, 0.30f), InkSumiDark = new(0.20f, 0.18f, 0.19f), InkSumiLight = new(0.55f, 0.50f, 0.45f);

	static StrokeStyle Style(RouteKind k) => k switch
	{
		RouteKind.Walked => new(InkPersimmon, InkDark, InkLight, 12f, 26f, 0.35f),
		RouteKind.Open => new(InkSumi, InkSumiDark, InkSumiLight, 8f, 15f, 0.9f),
		_ => new(new Color(0.55f, 0.50f, 0.44f), InkSumi, InkSumiLight, 5f, 9f, 1.0f),
	};

	const int BrushSamples = 48;

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

	/// <summary>One stroke: thick in the middle, a point at both ends, an uneven edge, bristle streaks and dry gaps; <paramref name="progress"/> paints it from a to b.</summary>
	void DrawBrushStroke(Vector2 a, Vector2 b, StrokeStyle st, float progress)
	{
		int seed = (int)(a.X * 31 + a.Y * 17 + b.X * 13 + b.Y * 7);
		a -= new Vector2(10f, 0); b += new Vector2(10f, 0); // the ink runs a little under each card, so the tapered ends read as part of the stroke
		float width = Mathf.Clamp(a.DistanceTo(b) * 0.09f, st.MinWidth, st.MaxWidth);
		float dx = Mathf.Max(24f, b.X - a.X);
		Vector2 c1 = a + new Vector2(dx * 0.45f, 0), c2 = b - new Vector2(dx * 0.45f, 0);
		var p = new Vector2[BrushSamples + 1];
		var n = new Vector2[BrushSamples + 1];
		var w = new float[BrushSamples + 1];
		for (int i = 0; i <= BrushSamples; i++)
		{
			float t = i / (float)BrushSamples, u = 1 - t;
			Vector2 d = 3 * u * u * (c1 - a) + 6 * u * t * (c2 - c1) + 3 * t * t * (b - c2);
			n[i] = d.Length() > 0.001f ? new Vector2(-d.Y, d.X).Normalized() : Vector2.Down;
			float taper = Mathf.Max(0.12f, Mathf.Pow(Mathf.Sin(Mathf.Pi * t), 0.5f));
			w[i] = width * taper * (0.78f + 0.4f * Smooth(seed, i, 5f)); // an uneven edge: the brush swells and thins along the stroke
			p[i] = u * u * u * a + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * b + n[i] * (Smooth(seed + 7, i, 9f) - 0.5f) * width * 0.5f; // the hand wanders off the curve
		}
		float painted = progress * BrushSamples;
		int last = Mathf.Min(BrushSamples, Mathf.FloorToInt(painted));
		if (progress <= 0f) return;
		for (int i = 0; i < last; i++) // body: segments with round joints
		{
			DrawLine(p[i], p[i + 1], st.Body, w[i], true);
			DrawCircle(p[i], w[i] / 2f, st.Body);
		}
		if (last >= BrushSamples) DrawCircle(p[BrushSamples], w[BrushSamples] / 2f, st.Body);
		else if (last > 0) DrawCircle(p[last], w[last] / 2f, st.Body); // the brush head, round while it is still moving
		for (int k = 0; k < 4; k++) // bristle streaks: broken thin lines along the stroke, darker and lighter
		{
			float off = (k - 1.5f) * 0.26f;
			var col = k % 2 == 0 ? st.Dark : st.Light;
			col.A = 0.55f;
			for (int i = 2; i < Mathf.Min(last, BrushSamples - 2); i++)
			{
				if (Smooth(seed + k * 101, i, 7f) < 0.4f) continue; // a gap in the streak: the dry brush skips
				DrawLine(p[i] + n[i] * off * w[i], p[i + 1] + n[i + 1] * off * w[i + 1], col, 1.5f + 1.2f * Smooth(seed + k, i, 9f), true);
			}
		}
		var paper = new Color(MapScroll.PaperTone.Lightened(0.15f), 0.55f);
		for (int k = 0; k < 3; k++) // dry brush: the paper shows through in long thin gaps, mostly where the stroke thins out
		{
			float off = (k - 1f) * 0.30f + (Noise(seed + k) - 0.5f) * 0.1f;
			for (int i = 3; i < Mathf.Min(last, BrushSamples - 3); i++)
			{
				float t = i / (float)BrushSamples, edge = 1f - Mathf.Sin(Mathf.Pi * t);
				if (Smooth(seed + 400 + k * 53, i, 4f) > st.Gaps * (0.20f + 0.45f * edge) + 0.12f) continue;
				DrawLine(p[i] + n[i] * off * w[i], p[i + 1] + n[i + 1] * off * w[i + 1], paper, 1.2f + 1.6f * Smooth(seed + 500 + k, i, 5f), true);
			}
		}
		if (st.MaxWidth > 20f && last >= BrushSamples) // a few flicked droplets at the end of a bold stroke
			for (int k = 0; k < 3; k++)
				DrawCircle(p[BrushSamples - 2 - k] + n[BrushSamples - 2 - k] * (Noise(seed + 90 + k) - 0.5f) * width * 2.4f + new Vector2(6f + 8f * k, 0), 1.6f + 1.8f * Noise(seed + 70 + k), st.Body);
	}
}
