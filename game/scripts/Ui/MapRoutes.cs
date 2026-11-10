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
/// YOK-238 (map v2): strokes wind along the centre lines of <see cref="MapRouteGeometry"/> (routes only join and split, never cross), and <see cref="SetMistLines"/> adds the
/// real routes still ahead as thin dashed sketches that fade out along x (AM19); they are not <see cref="Lines"/> and carry no kind.
/// <see cref="Paint"/> makes chosen strokes paint in from their start over a short time (presentation only, <see cref="FinishPainting"/>
/// ends it at once; nothing reads it back).
/// </summary>
public partial class MapRoutes : Control
{
	readonly List<(Vector2 A, Vector2 B, RouteKind Kind)> _lines = new();
	readonly Dictionary<(Vector2, Vector2), float> _progress = new(); // 0..1 while a stroke paints in; absent = fully painted
	float _paintSeconds = 0.6f;
	readonly List<(Vector2 A, Vector2 B, Vector2[] Path)> _mist = new();
	readonly Dictionary<(Vector2, Vector2), Vector2[]> _paths = new();
	float _mistFrom, _mistSpan = 1500f;
	public IReadOnlyList<(Vector2 A, Vector2 B, RouteKind Kind)> Lines => _lines;
	/// <summary>AM19: the routes still ahead, drawn as faint dashed sketches (end points in the same coordinates as <see cref="Lines"/>).</summary>
	public IReadOnlyList<(Vector2 A, Vector2 B, Vector2[] Path)> MistLines => _mist;

	/// <summary>AM19: sketch these routes; the ink fades from full at x = <paramref name="fadeFrom"/> to nearly nothing <paramref name="fadeSpan"/> px further on.</summary>
	public void SetMistLines(IEnumerable<(Vector2 A, Vector2 B, Vector2[] Path)> lines, float fadeFrom, float fadeSpan = 1500f)
	{
		_mist.Clear(); _mist.AddRange(lines);
		_mistFrom = fadeFrom; _mistSpan = Mathf.Max(100f, fadeSpan);
		QueueRedraw();
	}
	/// <summary>True while any stroke is still painting in.</summary>
	public bool IsPainting => _progress.Count > 0;

	public override void _Ready() => SetProcess(false);

	/// <summary>The lines, with the centre line each is drawn along (<see cref="MapRouteGeometry"/>); a line with no path is drawn as a plain winding curve.</summary>
	public void SetLines(IEnumerable<(Vector2 A, Vector2 B, RouteKind Kind)> lines, IReadOnlyDictionary<(Vector2, Vector2), Vector2[]>? paths = null)
	{
		_lines.Clear(); _lines.AddRange(lines);
		_paths.Clear(); if (paths != null) foreach (var kv in paths) _paths[kv.Key] = kv.Value;
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

	static float Ease(float t) => 1f - (1f - t) * (1f - t);

	public override void _Draw()
	{
		foreach (var (a, b, path) in _mist) DrawSketch(path.Length > 1 ? path : Path(a, b, 72, out _));
		foreach (var kind in new[] { RouteKind.Idle, RouteKind.Open, RouteKind.Walked }) // bold ink over faint ink, never the other way round
			foreach (var (a, b, k) in _lines)
				if (k == kind) DrawBrushStroke(_paths.TryGetValue((a, b), out var pts) ? pts : Path(a, b, 48, out _), Style(k), _progress.TryGetValue((a, b), out float p) ? Ease(p) : 1f);
	}

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

	/// <summary>Deterministic 0..1 noise from an integer, so a stroke looks the same every frame and every run.</summary>
	static float Noise(int n)
	{
		uint h = (uint)n * 2654435761u; h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
		return (h & 0xFFFF) / 65535f;
	}

	/// <summary>Noise that changes slowly along <paramref name="x"/> (value noise, one lattice point every <paramref name="period"/> units): long runs, not speckle.</summary>
	static float Smooth(int seed, float x, float period)
	{
		float f = x / period; int i = Mathf.FloorToInt(f); float t = f - i; t = t * t * (3 - 2 * t);
		return Mathf.Lerp(Noise(seed + i * 977), Noise(seed + (i + 1) * 977), t);
	}

	/// <summary>
	/// A plain winding curve from a to b (a cubic plus one S-meander), used only for a line the screen gave no centre line for.
	/// Normals out through <paramref name="normals"/>.
	/// </summary>
	public static Vector2[] Path(Vector2 a, Vector2 b, int samples, out Vector2[] normals)
	{
		int seed = (int)(a.X * 31 + a.Y * 17 + b.X * 13 + b.Y * 7);
		float len = a.DistanceTo(b), dx = Mathf.Max(24f, b.X - a.X);
		Vector2 c1 = a + new Vector2(dx * 0.45f, 0), c2 = b - new Vector2(dx * 0.45f, 0);
		float amp = Mathf.Clamp(len * 0.09f, 0f, 34f) * (Noise(seed + 3) > 0.5f ? 1f : -1f), turns = Noise(seed + 5) > 0.5f ? 1f : 1.5f;
		var p = new Vector2[samples + 1]; normals = new Vector2[samples + 1];
		for (int i = 0; i <= samples; i++)
		{
			float t = i / (float)samples, u = 1 - t;
			Vector2 d = 3 * u * u * (c1 - a) + 6 * u * t * (c2 - c1) + 3 * t * t * (b - c2);
			var n = d.Length() > 0.001f ? new Vector2(-d.Y, d.X).Normalized() : Vector2.Down;
			normals[i] = n;
			float bend = amp * Mathf.Sin(Mathf.Tau * turns * t) * Mathf.Sin(Mathf.Pi * t);
			p[i] = u * u * u * a + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * b + n * bend;
		}
		return p;
	}

	/// <summary>Normals of a polyline (the left-hand perpendicular of the local direction).</summary>
	static Vector2[] Normals(Vector2[] p)
	{
		var n = new Vector2[p.Length];
		for (int i = 0; i < p.Length; i++)
		{
			var d = p[Mathf.Min(p.Length - 1, i + 1)] - p[Mathf.Max(0, i - 1)];
			n[i] = d.Length() > 0.001f ? new Vector2(-d.Y, d.X).Normalized() : Vector2.Down;
		}
		return n;
	}

	/// <summary>The same polyline with evenly spaced points (about 6 px apart), so a stroke's look does not depend on how its path was sampled.</summary>
	static Vector2[] Resample(Vector2[] p, out float length)
	{
		float len = 0f; for (int i = 0; i + 1 < p.Length; i++) len += p[i].DistanceTo(p[i + 1]);
		length = len;
		int m = Mathf.Clamp(Mathf.RoundToInt(len / 6f), 12, 240);
		var o = new Vector2[m + 1];
		float step = len / m, acc = 0f; int seg = 0;
		o[0] = p[0];
		for (int k = 1; k <= m; k++)
		{
			float want = k * step;
			while (seg + 1 < p.Length - 1 && acc + p[seg].DistanceTo(p[seg + 1]) < want) { acc += p[seg].DistanceTo(p[seg + 1]); seg++; }
			float sl = p[seg].DistanceTo(p[seg + 1]);
			o[k] = sl < 0.0001f ? p[seg + 1] : p[seg].Lerp(p[seg + 1], Mathf.Clamp((want - acc) / sl, 0f, 1f));
		}
		o[m] = p[^1];
		return o;
	}

	/// <summary>
	/// AM19: one route ahead as a thin dashed pencil-and-ink line that grows fainter the further right it runs, so it trails into the mist.
	/// Dashes and drift depend on the x position only, so routes that share a trunk draw one and the same dashed line.
	/// </summary>
	void DrawSketch(Vector2[] path)
	{
		var p = Resample(path, out _); var n = Normals(p);
		for (int i = 0; i < p.Length - 1; i++)
		{
			float mx = (p[i].X + p[i + 1].X) * 0.5f;
			if (((mx % 26f) + 26f) % 26f > 15f) continue;
			float fade = Mathf.Clamp((p[i].X - _mistFrom) / _mistSpan, 0f, 1f);
			float alpha = Mathf.Lerp(0.62f, 0.3f, fade);
			var o = n[i] * (Smooth(41, p[i].X / 6f, 6f) - 0.5f) * 2.2f;
			DrawLine(p[i] + o, p[i + 1] + o, new Color(0.25f, 0.23f, 0.24f, alpha), 2.4f, true);
		}
	}

	/// <summary>One stroke along a centre line: thick in the middle, a point at both ends, an uneven edge, bristle streaks and dry gaps; <paramref name="progress"/> paints it from the start.</summary>
	void DrawBrushStroke(Vector2[] path, StrokeStyle st, float progress)
	{
		if (path.Length < 2 || progress <= 0f) return;
		var ext = new List<Vector2>(path); // the ink runs a little under each node, so the tapered ends read as part of the stroke
		ext.Insert(0, path[0] - new Vector2(10f, 0)); ext.Add(path[^1] + new Vector2(10f, 0));
		var c = Resample(ext.ToArray(), out float length);
		int count = c.Length - 1;
		var nrm = Normals(c);
		const int seed = 11; // not per route: routes that share a trunk wobble, swell and streak identically along it
		float width = Mathf.Clamp(length * 0.09f, st.MinWidth, st.MaxWidth);
		var p = new Vector2[count + 1]; var w = new float[count + 1];
		for (int i = 0; i <= count; i++)
		{
			float t = i / (float)count, sx = c[i].X / 6f;
			float taper = Mathf.Max(0.12f, Mathf.Pow(Mathf.Sin(Mathf.Pi * t), 0.5f));
			w[i] = width * taper * (0.78f + 0.4f * Smooth(seed, sx, 5f)); // an uneven edge: the brush swells and thins along the stroke
			p[i] = c[i] + nrm[i] * (Smooth(seed + 7, sx, 9f) - 0.5f) * width * 0.5f; // the hand wanders off the line
		}
		int last = Mathf.Min(count, Mathf.FloorToInt(progress * count));
		for (int i = 0; i < last; i++) // body: segments with round joints
		{
			DrawLine(p[i], p[i + 1], st.Body, w[i], true);
			DrawCircle(p[i], w[i] / 2f, st.Body);
		}
		if (last >= count) DrawCircle(p[count], w[count] / 2f, st.Body);
		else if (last > 0) DrawCircle(p[last], w[last] / 2f, st.Body); // the brush head, round while it is still moving
		for (int k = 0; k < 4; k++) // bristle streaks: broken thin lines along the stroke, darker and lighter
		{
			float off = (k - 1.5f) * 0.26f;
			var col = k % 2 == 0 ? st.Dark : st.Light;
			col.A = 0.55f;
			for (int i = 2; i < Mathf.Min(last, count - 2); i++)
			{
				float sx = c[i].X / 6f;
				if (Smooth(seed + k * 101, sx, 7f) < 0.4f) continue; // a gap in the streak: the dry brush skips
				DrawLine(p[i] + nrm[i] * off * w[i], p[i + 1] + nrm[i + 1] * off * w[i + 1], col, 1.5f + 1.2f * Smooth(seed + k, sx, 9f), true);
			}
		}
		var paper = new Color(MapScroll.PaperTone.Lightened(0.15f), 0.55f);
		for (int k = 0; k < 3; k++) // dry brush: the paper shows through in long thin gaps, mostly where the stroke thins out
		{
			float off = (k - 1f) * 0.30f + (Noise(seed + k) - 0.5f) * 0.1f;
			for (int i = 3; i < Mathf.Min(last, count - 3); i++)
			{
				float t = i / (float)count, edge = 1f - Mathf.Sin(Mathf.Pi * t), sx = c[i].X / 6f;
				if (Smooth(seed + 400 + k * 53, sx, 4f) > st.Gaps * (0.20f + 0.45f * edge) + 0.12f) continue;
				DrawLine(p[i] + nrm[i] * off * w[i], p[i + 1] + nrm[i + 1] * off * w[i + 1], paper, 1.2f + 1.6f * Smooth(seed + 500 + k, sx, 5f), true);
			}
		}
		if (st.MaxWidth > 20f && last >= count && count > 6) // a few flicked droplets at the end of a bold stroke
			for (int k = 0; k < 3; k++)
				DrawCircle(p[count - 2 - k] + nrm[count - 2 - k] * (Noise(seed + 90 + k) - 0.5f) * width * 2.4f + new Vector2(6f + 8f * k, 0), 1.6f + 1.8f * Noise(seed + 70 + k), st.Body);
	}
}
