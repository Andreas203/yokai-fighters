using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-238 breaker probes, round 2: routes only join and split (never cross), landmarks sit on or beside their own route, nothing leaks from hidden
/// nodes (R2, AM19), layout and scenery are deterministic and independent of reveal state.
/// </summary>
public static class MapV2RouteBreakerProbes
{
	static readonly Vector2 P1080 = new(1920, 1080), P720 = new(1280, 720);

	// ---- helpers -------------------------------------------------------------------------------------------------------------------

	static StubMapView Cols(params int[] counts)
	{
		var v = new StubMapView();
		for (int c = 0; c < counts.Length; c++) for (int i = 0; i < counts[c]; i++) v.Add($"c{c}n{i}", c + 1, "duel", $"Y{c}.{i}");
		return v;
	}

	static StubMapView Random(int seed, int cols, int maxPer, bool skip = false)
	{
		var v = new StubMapView();
		var ids = new List<List<string>>();
		for (int c = 0; c < cols; c++)
		{
			int cnt = 1 + (int)(MapLayout.Hash("cnt" + seed + ":" + c) % (uint)maxPer);
			ids.Add(Enumerable.Range(0, cnt).Select(i => $"c{c}n{i}").ToList());
			foreach (var id in ids[^1]) v.Add(id, c + 1, "duel", id);
		}
		var seen = new HashSet<(string, string)>();
		for (int c = 0; c + 1 < cols; c++)
			foreach (var a in ids[c])
			{
				int k = 1 + (int)(MapLayout.Hash("k" + seed + a) % 3);
				for (int j = 0; j < k; j++)
				{
					int tc = skip && j == 2 && c + 2 < cols ? c + 2 : c + 1;
					var tgt = ids[tc];
					var b = tgt[(int)(MapLayout.Hash("t" + seed + a + j) % (uint)tgt.Count)];
					if (seen.Add((a, b))) v.Route(a, b);
				}
			}
		return v;
	}

	static List<(string Name, Vector2[] Path)> Named(IReadOnlyDictionary<(string From, string To), Vector2[]> p) => p.Select(kv => (kv.Key.From + ">" + kv.Key.To, kv.Value)).ToList();

	static List<string> CrossFailures(IReadOnlyList<(string Name, Vector2[] Path)> r, string where)
	{
		var o = new List<string>();
		for (int i = 0; i < r.Count; i++)
			for (int j = i + 1; j < r.Count; j++)
				if (MapRouteGeometry.Crosses(r[i].Path, r[j].Path)) o.Add($"{where}: {r[i].Name} crosses {r[j].Name}");
		return o;
	}

	static Rect2 BoundsOf(Vector2[] pts) { float x0 = pts.Min(p => p.X), x1 = pts.Max(p => p.X), y0 = pts.Min(p => p.Y), y1 = pts.Max(p => p.Y); return new Rect2(x0, y0, x1 - x0, y1 - y0); }

	static bool Near(Vector2[] path, Vector2 q, float d) => path.Any(p => p.DistanceSquaredTo(q) <= d * d);

	/// <summary>Every landmark rule that can be read from the plan alone.</summary>
	static List<string> LandmarkFailures(Vector2 board, IEnumerable<Rect2> nodeRects, IReadOnlyList<Vector2[]> paths, List<MapLandmark> plan, string where)
	{
		var o = new List<string>();
		float reserve = MapNodeBox.LineHeight(board.Y * MapLayout.SpriteShare);
		var boxes = nodeRects.Select(r => new Rect2(r.Position, r.Size + new Vector2(0, reserve))).ToList();
		var bounds = new Rect2(Vector2.Zero, board);
		float screen = MapLandmarkPlan.ScreenWidth(board);
		for (int i = 0; i < plan.Count; i++)
		{
			var l = plan[i]; string n = $"{where}: {l.Kind}#{i} (route {l.Route}) at {l.Rect}";
			if (!bounds.Encloses(l.Rect)) o.Add(n + " leaves the board");
			if (boxes.Any(b => b.Intersects(l.Rect))) o.Add(n + " sits on a node box or tag");
			if (l.Stream != null && boxes.Any(b => b.Intersects(BoundsOf(l.Stream)))) o.Add(n + " stream sits on a node box");
			var host = paths[l.Route];
			for (int k = 0; k < paths.Count; k++)
			{
				if (k == l.Route && !l.InFront) { if (host.Any(q => l.Rect.HasPoint(q))) o.Add(n + " hides its own route"); continue; }
				if (k == l.Route) continue;
				if (paths[k].Any(q => l.Rect.HasPoint(q) && !Near(host, q, 10f))) o.Add(n + $" covers foreign route {k}");
				if (l.Stream != null)
				{
					var sb = BoundsOf(l.Stream);
					if (paths[k].Any(q => sb.HasPoint(q) && !Near(host, q, 10f))) o.Add(n + $" stream touches foreign route {k}");
				}
			}
			if (l.InFront && !Near(host, l.OnPath, 12f)) o.Add(n + " is not on its host route");
			if (l.Kind == MapLandmarkKind.Bridge)
			{
				if (l.Stream == null) o.Add(n + " has no stream");
				else if (!MapRouteGeometry.Crosses(host, l.Stream)) o.Add(n + " stream does not cross its route");
			}
			if (l.Kind == MapLandmarkKind.Torii)
			{
				float cx = l.Rect.GetCenter().X;
				if (!(host.Any(q => l.Rect.HasPoint(q) && q.X < cx) && host.Any(q => l.Rect.HasPoint(q) && q.X >= cx - 8f))) o.Add(n + " route does not pass through it");
			}
			if (!l.InFront && MapLandmarkPlan.DistanceToStroke(l.Rect, paths) > 30.5f) o.Add(n + " floats in open paper");
			for (int j = i + 1; j < plan.Count; j++)
			{
				var m = plan[j];
				if (l.Rect.Intersects(m.Rect)) o.Add(n + $" overlaps {m.Kind}#{j}");
				if (l.Kind == m.Kind && System.Math.Abs(l.Rect.GetCenter().X - m.Rect.GetCenter().X) < screen) o.Add(n + $" same kind twice within a screen width ({m.Kind}#{j})");
			}
		}
		return o;
	}

	static void Check(IMapView v, Vector2 board, string where, bool landmarks = true)
	{
		var layout = MapLayout.Compute(v.Nodes, board);
		var paths = MapRouteGeometry.Compute(v.Nodes, v.Routes, layout, board.Y);
		float reserve = MapNodeBox.LineHeight(board.Y * MapLayout.SpriteShare);
		var plan = MapLandmarkPlan.Compute(board, layout.Rects.Values, paths.Values.ToList(), reserve);
		var f = CrossFailures(Named(paths), where);
		if (landmarks) f.AddRange(LandmarkFailures(board, layout.Rects.Values, paths.Values.ToList(), plan, where));
		var plan2 = MapLandmarkPlan.Compute(board, layout.Rects.Values, paths.Values.ToList(), reserve);
		if (plan.Count != plan2.Count || plan.Zip(plan2).Any(p => p.First.Kind != p.Second.Kind || p.First.Rect != p.Second.Rect)) f.Add(where + ": landmark plan not deterministic");
		Assert.True(f.Count == 0, f.Count + " failures; first: " + string.Join(" || ", f.Take(4)));
	}

	// ---- geometry: shapes the author's grids and random graphs do not cover ------------------------------------------------------------

	// Designer rule: routes never cross. A hub: one node fans out to a wide column and back in again.
	[Test]
	public static void HubFanOutAndBackInNeverCrosses()
	{
		foreach (var cols in new[] { new[] { 1, 7, 1 }, new[] { 1, 5, 1, 5, 1 }, new[] { 1, 12, 1 } })
		{
			var v = Cols(cols);
			for (int c = 0; c + 1 < cols.Length; c++)
				for (int i = 0; i < cols[c]; i++) for (int j = 0; j < cols[c + 1]; j++) v.Route($"c{c}n{i}", $"c{c + 1}n{j}");
			Check(v, new Vector2(cols.Length * 595f, 612f), "hub " + string.Join("-", cols));
			Check(v, new Vector2(cols.Length * 427f, 408f), "hub720 " + string.Join("-", cols));
		}
	}

	// Designer rule: join and split only. Nodes with 3+ out and 3+ in inside one gap, irregular (not complete) bipartite.
	[Test]
	public static void DenseIrregularBipartiteGapNeverCrosses()
	{
		var v = Cols(4, 4, 3);
		string[] g0 = { "0>0", "0>1", "0>2", "1>3", "2>0", "2>3", "3>1", "3>2", "3>3" }, g1 = { "0>2", "1>0", "1>1", "1>2", "2>0", "2>1", "3>0" };
		foreach (var e in g0) v.Route($"c0n{e[0]}", $"c1n{e[2]}");
		foreach (var e in g1) v.Route($"c1n{e[0]}", $"c2n{e[2]}");
		Check(v, new Vector2(3 * 595f, 612f), "dense 4-4-3");
		Check(v, new Vector2(3 * 427f, 408f), "dense 4-4-3 @720");
	}

	// Designer rule: two buses in one gap are two trunks, they must not run into each other.
	[Test]
	public static void TwoDisjointBusesInOneGapNeverCross()
	{
		var v = Cols(4, 4);
		v.Route("c0n0", "c1n1").Route("c0n1", "c1n0").Route("c0n2", "c1n3").Route("c0n3", "c1n2");
		Check(v, new Vector2(2 * 595f, 612f), "two buses");
		var w = Cols(6, 6);
		for (int i = 0; i < 6; i += 2) { w.Route($"c0n{i}", $"c1n{i + 1}"); w.Route($"c0n{i + 1}", $"c1n{i}"); }
		Check(w, new Vector2(2 * 595f, 612f), "three buses");
	}

	// Many nodes in one column, a lone node either side, in a chain.
	[Test]
	public static void ManyNodesInOneColumnNeverCross()
	{
		int[] cs = { 1, 14, 2, 14, 1 };
		var tmp = Cols(cs);
		var routes = new List<(string, string)>();
		for (int c = 0; c + 1 < cs.Length; c++)
			for (int i = 0; i < cs[c]; i++)
			{
				routes.Add(($"c{c}n{i}", $"c{c + 1}n{i * cs[c + 1] / cs[c] % cs[c + 1]}"));
				routes.Add(($"c{c}n{i}", $"c{c + 1}n{(i * 3 + 1) % cs[c + 1]}"));
			}
		foreach (var r in routes.Distinct()) tmp.Route(r.Item1, r.Item2);
		Check(tmp, new Vector2(5 * 595f, 612f), "many in one column");
	}

	// Tall, short, narrow and wide boards.
	[Test]
	public static void ExtremeBoardSizesNeverCross()
	{
		foreach (var board in new[] { new Vector2(1800f, 2000f), new Vector2(3600f, 300f), new Vector2(900f, 612f), new Vector2(6000f, 612f), new Vector2(2400f, 140f) })
			for (int seed = 1; seed <= 6; seed++)
				Check(Random(seed, 4, 6), board, $"random {seed} on {board}");
	}

	// A sweep of random graphs, 2..10 columns, up to 8 per column, 1080p and 720p scale.
	[Test]
	public static void RandomSweepNeverCrossesAndLandmarksStayClean()
	{
		for (int seed = 100; seed < 160; seed++)
		{
			int cols = 2 + seed % 9, per = 2 + seed % 7;
			Check(Random(seed, cols, per), new Vector2(cols * 595f, 612f), $"sweep {seed} ({cols}x{per})");
			Check(Random(seed, cols, per), new Vector2(cols * 427f, 408f), $"sweep720 {seed}");
		}
	}

	// The map loader (MapGraph validation) must REJECT a route that skips a row, so the geometry never has to draw one (MapGraph: "route not going to the next row").
	[Test]
	public static void MapGraphRejectsARouteThatSkipsARow()
	{
		static string Json(string extraRoute) => "{\"kind\":\"run_map\",\"id\":\"skip-probe\",\"schema_version\":1,\"nodes\":["
			+ "{\"id\":\"a\",\"row\":1,\"type\":\"duel\",\"yokai\":\"Apprentice\"},{\"id\":\"b\",\"row\":2,\"type\":\"duel\",\"yokai\":\"Oni\"},"
			+ "{\"id\":\"c\",\"row\":3,\"type\":\"duel\",\"yokai\":\"Kappa\"}],\"routes\":[{\"from\":\"a\",\"to\":\"b\"},{\"from\":\"b\",\"to\":\"c\"}" + extraRoute + "]}";
		string okErr = null!;
		try { MapLoader.Parse(Json("")); }
		catch (System.Exception e) { okErr = e.Message; }
		Assert.True(okErr == null, "control: the same map without the skipping route must load, but: " + okErr);
		string err = null!;
		try { MapLoader.Parse(Json(",{\"from\":\"a\",\"to\":\"c\"}")); }
		catch (System.FormatException e) { err = e.Message; }
		catch (System.Exception e) { err = "wrong exception type " + e.GetType().Name + ": " + e.Message; }
		Assert.True(err != null, "a route a -> c from row 1 to row 3 skips row 2 and must be rejected by the loader, but the map loaded");
		Assert.True(err!.Contains("skips a row"), "the rejection must name the problem 'skips a row', got: " + err);
	}

	// ---- screen: every reveal state of a walk, drawn strokes plus mist sketches ----------------------------------------------------------------

	static MapScreen Make(Node host, IMapView view, Vector2? size = null)
	{
		var s = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		var stage = new Control { Size = size ?? P1080 };
		host.AddChild(stage); stage.AddChild(s);
		s.SetView(view); s.FinishPresentation();
		return s;
	}

	static StubMapView WalkMap(int seed, int cols = 8, int per = 4)
	{
		var src = Random(seed, cols, per);
		var v = new StubMapView();
		foreach (var n in src.Nodes) v.Add(n.Id, n.Row, n.Type, n.Yokai);
		foreach (var r in src.Routes) v.Route(r.From, r.To);
		v.Open(v.Nodes.Where(n => n.Row == 1).Select(n => n.Id).ToArray());
		return v;
	}

	static string Sig(IEnumerable<MapLandmark> plan) => string.Join(";", plan.Select(l => $"{l.Kind}|{l.Rect}|{l.Route}|{l.OnPath}|{(l.Stream == null ? "" : string.Join(",", l.Stream))}"));

	static string PathSig(IReadOnlyDictionary<(string From, string To), Vector2[]> p) => string.Join(";", p.OrderBy(kv => kv.Key.From + ">" + kv.Key.To).Select(kv => kv.Key.From + ">" + kv.Key.To + ":" + string.Join(",", kv.Value)));

	static Vector2[] DrawnPath(MapScreen s, (Vector2 A, Vector2 B, RouteKind Kind) line) =>
		s.RoutePaths.Values.FirstOrDefault(p => p[0].DistanceTo(line.A) < 0.5f && p[^1].DistanceTo(line.B) < 0.5f) ?? System.Array.Empty<Vector2>();

	// Designer rule: no crossing for drawn strokes and mist sketches alike, at every reveal state; reveal never moves a route or the scenery.
	[Test]
	public static void WalkNeverCrossesAndNothingMovesOrPopsIn(Node host)
	{
		foreach (var (seed, size) in new[] { (11, P1080), (12, P720), (13, P1080), (14, P1080) })
		{
			var v = WalkMap(seed);
			var s = Make(host, v, size);
			string paths0 = PathSig(s.RoutePaths), plan0 = Sig(s.Landmarks.Plan);
			int step = 0;
			while (true)
			{
				var drawn = s.Routes.Lines.Select((l, i) => ($"line{i}", DrawnPath(s, l))).Where(d => d.Item2.Length > 1)
					.Concat(s.Routes.MistLines.Select((m, i) => ($"mist{i}", m.Path))).ToList();
				var f = CrossFailures(drawn, $"seed {seed} {size} step {step}");
				Assert.True(f.Count == 0, string.Join(" || ", f.Take(3)));
				Assert.True(PathSig(s.RoutePaths) == paths0, $"seed {seed} step {step}: a route moved when nodes were revealed");
				Assert.True(Sig(s.Landmarks.Plan) == plan0, $"seed {seed} step {step}: the scenery changed when nodes were revealed");
				Assert.True(Sig(s.LandmarksFront.Plan) == plan0, $"seed {seed} step {step}: front scenery differs from back");
				var reach = v.Nodes.Where(n => n.State == MapNodeState.Reachable).Select(n => n.Id).ToList();
				if (reach.Count == 0) break;
				v.Select(reach[step % reach.Count]); v.Complete();
				s.SetView(v); s.FinishPresentation();
				step++;
			}
			Assert.True(step >= 4, "the walk went several rows");
			s.GetParent().QueueFree();
		}
	}

	// R2 [AM18, AM19]: swapping the type and yokai of nodes that are still hidden must change no stroke, sketch or landmark.
	[Test]
	public static void HiddenNodeTypeAndYokaiMoveNothing(Node host)
	{
		StubMapView Make1(string type, string? yokai, bool elder)
		{
			var v = new StubMapView();
			v.Add("a", 1, "duel", "Apprentice");
			v.Add("b0", 2, type, yokai, elder); v.Add("b1", 2, "duel", "Plain"); v.Add("b2", 2, type, yokai, elder);
			v.Add("c0", 3, type, yokai, elder); v.Add("c1", 3, type, yokai, elder);
			v.Add("boss", 4, type == "boss" ? "boss" : "duel", type == "boss" ? "The Tanuki" : "Z");
			foreach (var b in new[] { "b0", "b1", "b2" }) v.Route("a", b);
			v.Route("b0", "c1").Route("b1", "c0").Route("b2", "c0").Route("c0", "boss").Route("c1", "boss");
			v.Open("a");
			return v;
		}
		string? basePaths = null, basePlan = null, baseMist = null, baseLines = null;
		var leaks = new List<string>();
		foreach (var (type, yokai, elder) in new[] { ("duel", "Plain", false), ("elder", "Elder Oni", true), ("merchant", (string?)null, false), ("boss", "The Tanuki", false), ("kihon", "Kihon", false) })
		{
			var s = Make(host, Make1(type, yokai, elder));
			string p = PathSig(s.RoutePaths), l = Sig(s.Landmarks.Plan), m = string.Join(";", s.Routes.MistLines.Select(x => $"{x.A}{x.B}{string.Join(",", x.Path)}"));
			string ln = string.Join(";", s.Routes.Lines.Select(x => $"{x.A}{x.B}{x.Kind}"));
			if (basePaths == null) { basePaths = p; basePlan = l; baseMist = m; baseLines = ln; }
			else
			{
				if (p != basePaths) leaks.Add($"'{type}' changes the route centre lines");
				if (l != basePlan) leaks.Add($"'{type}' changes the landmarks");
				if (m != baseMist) leaks.Add($"'{type}' changes the mist sketches");
				if (ln != baseLines) leaks.Add($"'{type}' changes the drawn strokes");
			}
			s.GetParent().QueueFree();
		}
		Assert.True(leaks.Count == 0, "hidden node type/yokai moves geometry: " + string.Join("; ", leaks));
	}

	// Landmarks on the real screen obey the placement rules on a larger walked map (boxes from the screen's own layout).
	[Test]
	public static void ScreenLandmarksObeyPlacementRules(Node host)
	{
		foreach (var (seed, size) in new[] { (21, P1080), (22, P720), (23, P1080) })
		{
			var v = WalkMap(seed, 9, 5);
			var s = Make(host, v, size);
			var rects = v.Nodes.Select(n => s.RectOf(n.Id)).ToList();
			var f = LandmarkFailures(s.Board.Size, rects, s.RoutePaths.Values.ToList(), s.Landmarks.Plan.ToList(), $"screen {seed} {size}");
			Assert.True(f.Count == 0, f.Count + " failures; first: " + string.Join(" || ", f.Take(4)));
			s.GetParent().QueueFree();
		}
	}
}
