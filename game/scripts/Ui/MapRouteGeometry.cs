using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-238 (designer review 2026-11-09): the centre line of every route, pure and deterministic, so that no two routes ever cross or run side by
/// side: they may only JOIN (several routes merge into one trunk) and SPLIT (one trunk forks). It reads only node positions and the route list, never
/// reveal state, so nothing moves when a node is revealed.
/// How: between two columns every route leaves its node by a short horizontal stub to a common x, then runs over the same smootherstep curve to a
/// common arrival x and a stub into its target. Two such curves differ by a linear mix of their start and end height differences, so they cross
/// exactly when the order of their sources differs from the order of their targets. Those routes (and any route that shares a node with them, and
/// any pair found crossing afterwards) form a bus: all its sources merge into one junction in the middle of the gap, then split to the targets.
/// A winding meander common to the whole gap is added to every curve, which keeps the differences, so the order cannot change mid-way.
/// </summary>
public static class MapRouteGeometry
{
	const int Samples = 40;

	sealed class Edge
	{
		public string From = "", To = "";
		public int Ca, Cb;
		public float Ya, Yb;
		public Vector2 Start, End;
		public bool Ok;
	}

	static float Smoother(float u) => u * u * u * (u * (6f * u - 15f) + 10f);

	/// <summary>The paper-space centre lines, keyed by (from, to); each begins at the source's illustration edge and ends on the target's, at mid-height.</summary>
	public static Dictionary<(string From, string To), Vector2[]> Compute(IReadOnlyList<MapNodeView> nodes, IReadOnlyList<MapRouteView> routes, MapLayoutResult layout, float boardHeight)
	{
		var result = new Dictionary<(string, string), Vector2[]>();
		var col = new Dictionary<string, int>();
		var rowIndex = layout.Columns.Select((c, i) => (c.Row, i)).ToDictionary(p => p.Row, p => p.i);
		foreach (var n in nodes) if (rowIndex.TryGetValue(n.Row, out int ci)) col[n.Id] = ci;
		var edges = new List<Edge>();
		foreach (var r in routes)
		{
			var e = new Edge { From = r.From, To = r.To };
			if (layout.Sprites.TryGetValue(r.From, out var a) && layout.Sprites.TryGetValue(r.To, out var b) && col.ContainsKey(r.From) && col.ContainsKey(r.To))
			{
				e.Ca = col[r.From]; e.Cb = col[r.To];
				e.Start = new Vector2(a.End.X - a.Size.X * 0.08f, a.GetCenter().Y); e.End = new Vector2(b.Position.X + b.Size.X * 0.08f, b.GetCenter().Y);
				e.Ya = e.Start.Y; e.Yb = e.End.Y; e.Ok = e.Cb > e.Ca;
			}
			edges.Add(e);
		}
		// gap x range per column pair: out of the right-most box of the left column into the left-most box of the right one
		float XOut(int c) => layout.Rects.Where(kv => col.TryGetValue(kv.Key, out int k) && k == c).Select(kv => kv.Value.End.X).DefaultIfEmpty(0f).Max() + 8f;
		float XIn(int c) => layout.Rects.Where(kv => col.TryGetValue(kv.Key, out int k) && k == c).Select(kv => kv.Value.Position.X).DefaultIfEmpty(0f).Min() - 8f;
		var xo = new Dictionary<int, float>(); var xi = new Dictionary<int, float>();
		foreach (var e in edges.Where(e => e.Ok)) { if (!xo.ContainsKey(e.Ca)) xo[e.Ca] = XOut(e.Ca); if (!xi.ContainsKey(e.Cb)) xi[e.Cb] = XIn(e.Cb); }
		foreach (var e in edges.Where(e => e.Ok && xi[e.Cb] <= xo[e.Ca] + 20f)) e.Ok = false; // no room between the columns: a plain line

		int n0 = edges.Count;
		var uf = Enumerable.Range(0, n0).ToArray();
		int Find(int i) { while (uf[i] != i) { uf[i] = uf[uf[i]]; i = uf[i]; } return i; }
		void Union(int i, int j) { uf[Find(i)] = Find(j); }
		for (int i = 0; i < n0; i++)
			for (int j = i + 1; j < n0; j++)
			{
				var p = edges[i]; var q = edges[j];
				if (!p.Ok || !q.Ok || p.Ca != q.Ca || p.Cb != q.Cb || p.From == q.From || p.To == q.To) continue;
				if ((p.Ya - q.Ya) * (p.Yb - q.Yb) < 0f) Union(i, j); // the order of the sources differs from the order of the targets: a bus
			}

		Vector2[][] paths = new Vector2[n0][];
		for (int round = 0; round < 12; round++)
		{
			// a bus takes in every route that touches one of its nodes, so a trunk is never shared with half of a node's routes
			bool grew = true;
			while (grew)
			{
				grew = false;
				for (int i = 0; i < n0; i++)
					for (int j = i + 1; j < n0; j++)
					{
						var p = edges[i]; var q = edges[j];
						if (!p.Ok || !q.Ok || p.Ca != q.Ca || p.Cb != q.Cb || Find(i) == Find(j)) continue;
						if ((p.From == q.From || p.To == q.To) && (Size(Find(i)) > 1 || Size(Find(j)) > 1)) { Union(i, j); grew = true; }
					}
			}
			int Size(int root) { int c = 0; for (int k = 0; k < n0; k++) if (Find(k) == root && edges[k].Ok) c++; return c; }
			var jy = new Dictionary<int, float>();
			foreach (var g in Enumerable.Range(0, n0).Where(i => edges[i].Ok).GroupBy(Find))
				if (g.Count() > 1)
				{
					var ys = new Dictionary<string, float>();
					foreach (int i in g) { ys[edges[i].From] = edges[i].Ya; ys[edges[i].To] = edges[i].Yb; }
					jy[g.Key] = ys.Values.Average();
				}
			for (int i = 0; i < n0; i++)
				paths[i] = Build(edges[i], jy.TryGetValue(Find(i), out float y) ? y : (float?)null, edges[i].Ok ? xo[edges[i].Ca] : 0f, edges[i].Ok ? xi[edges[i].Cb] : 0f, boardHeight);
			// routes of different buses that still cross: merge them and build again
			bool merged = false;
			for (int i = 0; i < n0 && !merged; i++)
				for (int j = i + 1; j < n0; j++)
				{
					if (!edges[i].Ok || !edges[j].Ok || Find(i) == Find(j) || edges[i].Ca != edges[j].Ca || edges[i].Cb != edges[j].Cb) continue;
					if (Crosses(paths[i], paths[j])) { Union(i, j); merged = true; break; }
				}
			if (!merged) break;
		}
		for (int i = 0; i < n0; i++) result[(edges[i].From, edges[i].To)] = paths[i];
		return result;
	}

	static Vector2[] Build(Edge e, float? junction, float xOut, float xIn, float boardHeight)
	{
		if (!e.Ok) return Densify(new List<Vector2> { e.Start, e.End });
		var pts = new List<Vector2> { e.Start, new Vector2(xOut, e.Ya) };
		uint h = MapLayout.Hash("gap" + e.Ca + ":" + e.Cb);
		float amp = (MapLayout.Unit(h, 1) > 0.5f ? 1f : -1f) * (0.035f + 0.03f * MapLayout.Unit(h, 2)) * boardHeight, turns = MapLayout.Unit(h, 3) > 0.5f ? 1f : 1.5f;
		for (int i = 1; i < Samples; i++)
		{
			float u = i / (float)Samples;
			float m = amp * Mathf.Sin(Mathf.Tau * turns * u) * Mathf.Sin(Mathf.Pi * u);
			float y;
			if (junction is { } j) y = u <= 0.5f ? e.Ya + (j - e.Ya) * Smoother(u * 2f) : j + (e.Yb - j) * Smoother(u * 2f - 1f);
			else y = e.Ya + (e.Yb - e.Ya) * Smoother(u);
			pts.Add(new Vector2(Mathf.Lerp(xOut, xIn, u), y + m));
		}
		pts.Add(new Vector2(xIn, e.Yb)); pts.Add(e.End);
		return Densify(pts);
	}

	/// <summary>No two neighbouring points further apart than 8 px (so a stub is as finely sampled as the curve).</summary>
	public static Vector2[] Densify(List<Vector2> p)
	{
		var o = new List<Vector2> { p[0] };
		for (int i = 1; i < p.Count; i++)
		{
			int k = Mathf.Max(1, Mathf.CeilToInt(p[i - 1].DistanceTo(p[i]) / 8f));
			for (int s = 1; s <= k; s++) o.Add(p[i - 1].Lerp(p[i], s / (float)k));
		}
		return o.ToArray();
	}

	/// <summary>True when the two centre lines properly cross each other (touching or running together at shared points does not count).</summary>
	public static bool Crosses(Vector2[] a, Vector2[] b)
	{
		for (int i = 0; i + 1 < a.Length; i++)
		{
			float x0 = Mathf.Min(a[i].X, a[i + 1].X), x1 = Mathf.Max(a[i].X, a[i + 1].X), y0 = Mathf.Min(a[i].Y, a[i + 1].Y), y1 = Mathf.Max(a[i].Y, a[i + 1].Y);
			for (int j = 0; j + 1 < b.Length; j++)
			{
				if (Mathf.Max(b[j].X, b[j + 1].X) < x0 || Mathf.Min(b[j].X, b[j + 1].X) > x1 || Mathf.Max(b[j].Y, b[j + 1].Y) < y0 || Mathf.Min(b[j].Y, b[j + 1].Y) > y1) continue;
				if (SegmentsCross(a[i], a[i + 1], b[j], b[j + 1])) return true;
			}
		}
		return false;
	}

	static float Side(Vector2 p, Vector2 q, Vector2 r) => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);

	static bool SegmentsCross(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
	{
		const float eps = 0.02f;
		float d1 = Side(p1, p2, q1), d2 = Side(p1, p2, q2), d3 = Side(q1, q2, p1), d4 = Side(q1, q2, p2);
		return ((d1 > eps && d2 < -eps) || (d1 < -eps && d2 > eps)) && ((d3 > eps && d4 < -eps) || (d3 < -eps && d4 > eps));
	}
}
