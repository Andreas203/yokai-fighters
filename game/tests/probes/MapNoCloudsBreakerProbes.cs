using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-245 breaker probes: sketches replace the old cloud bank (AM19, AM22, R2).</summary>
public static class MapNoCloudsBreakerProbes
{
	static readonly Vector2[] Sizes = { new(1920, 1080), new(1280, 720), new(1280, 1024), new(2560, 1080), new(1000, 1000) };
	const float Skip = 0.02f;

	static MapScreen Make(Node host, IMapView view, Vector2 size)
	{
		var s = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		var stage = new Control { Size = size };
		host.AddChild(stage); stage.AddChild(s);
		s.SetView(view); s.FinishPresentation();
		return s;
	}

	static StubMapView Advance(int rows, int per, int steps, bool swapHidden = false)
	{
		var v = StubMapView.Grid(rows, per);
		for (int i = 0; i < steps; i++)
		{
			var next = v.Nodes.First(n => n.State == MapNodeState.Reachable);
			v.Select(next.Id); v.Complete();
		}
		return v;
	}

	static StubMapView Swapped(int rows, int per, int steps, string type, bool elder, string? twist, string yokai)
	{
		var v = new StubMapView();
		var g = StubMapView.Grid(rows, per);
		foreach (var n in g.Nodes)
		{
			bool hidden = n.Row > steps + 2;
			v.Add(n.Id, n.Row, hidden ? type : n.Type, hidden ? yokai : n.Yokai, hidden ? elder : n.Elder, hidden ? twist : null);
		}
		foreach (var r in g.Routes) v.Route(r.From, r.To);
		v.Open(g.Nodes.Where(n => n.Row == 1).Select(n => n.Id).ToArray());
		for (int i = 0; i < steps; i++) { var nx = v.Nodes.First(n => n.State == MapNodeState.Reachable); v.Select(nx.Id); v.Complete(); }
		return v;
	}

	static string Print(MapScreen s, IMapView v)
	{
		var sb = new System.Text.StringBuilder();
		foreach (var l in s.Routes.Sketches) sb.Append($"{l.A}{l.B}{string.Join(",", l.Path)};");
		foreach (var n in v.Nodes) sb.Append($"{n.Id}:{s.RectOf(n.Id)};");
		foreach (var kv in s.RoutePaths.OrderBy(k => k.Key.From + k.Key.To)) sb.Append($"{kv.Key}:{string.Join(",", kv.Value)};");
		foreach (var l in s.Landmarks.Plan) sb.Append($"{l.Kind}|{l.Rect}|{l.Route}|{l.OnPath};");
		return sb.ToString();
	}

	// R2 / AM19 / AM22: hidden node type, yokai, elder status and twist change no sketch, alpha, path or landmark.
	[Test]
	public static void HiddenSwapLeavesSketchesIdentical(Node host)
	{
		var bad = new List<string>();
		foreach (var size in Sizes)
			foreach (int steps in new[] { 0, 1, 3, 5 })
			{
				var a = Advance(9, 3, steps); var sa = Make(host, a, size); string baseline = Print(sa, a); sa.GetParent().QueueFree();
				foreach (var (t, e, tw, y) in new[] { ("merchant", false, (string?)null, (string?)null), ("elder", true, "mirror", "Kitsune"), ("boss", false, null, "Oni"), ("rest", false, "x", null) })
				{
					var b = Swapped(9, 3, steps, t, e, tw, y!);
					var sb = Make(host, b, size);
					if (Print(sb, b) != baseline) bad.Add($"{size} steps={steps} {t}");
					sb.GetParent().QueueFree();
				}
			}
		Assert.True(bad.Count == 0, string.Join(" || ", bad.Take(5)));
	}

	// AM22: every route ahead is sketched to its full end (incl. into the Tanuki), none skipped, none leaving the board vertically.
	[Test]
	public static void EveryRouteAheadIsSketchedToItsEnd(Node host)
	{
		var bad = new List<string>();
		foreach (var size in Sizes)
			foreach (var (rows, per, steps) in new[] { (9, 3, 0), (9, 3, 1), (9, 3, 4), (9, 3, 7), (9, 2, 7), (9, 4, 4), (3, 1, 0), (2, 1, 0), (9, 1, 7) })
			{
				var v = Advance(rows, per, steps);
				var s = Make(host, v, size);
				string tag = $"{size} {rows}x{per} steps={steps}";
				var state = v.Nodes.ToDictionary(n => n.Id, n => n.State);
				int expect = v.Routes.Count(r => MapReveal.IsAhead(state[r.From], state[r.To]));
				if (s.Routes.Sketches.Count != expect) bad.Add($"{tag}: {s.Routes.Sketches.Count} sketches, {expect} routes ahead");
				foreach (var l in s.Routes.Sketches)
				{
					if (l.Path.Length < 2) { bad.Add($"{tag}: sketch {l.A}->{l.B} has {l.Path.Length} points"); continue; }
					if (l.Path.Any(p => float.IsNaN(p.X) || float.IsNaN(p.Y) || p.Y < 0f || p.Y > s.Board.Size.Y)) bad.Add($"{tag}: sketch {l.A}->{l.B} leaves the board vertically");
					if (System.Math.Abs(l.Path[^1].X - l.B.X) > 2f || System.Math.Abs(l.Path[0].X - l.A.X) > 2f) bad.Add($"{tag}: sketch {l.A}->{l.B} truncated: path {l.Path[0]}..{l.Path[^1]}");
				}
				// the route into the Tanuki (last row) is sketched while Ryo has not reached it
				if (steps < rows - 1 && v.Routes.Any(r => v.Nodes.First(n => n.Id == r.To).Row == rows && MapReveal.IsAhead(state[r.From], state[r.To])) && !s.Routes.Sketches.Any(l => l.B.X >= s.RectOf(v.Nodes.First(n => n.Row == rows).Id).GetCenter().X - 2f)) bad.Add($"{tag}: no sketch reaches the Tanuki node");
				s.GetParent().QueueFree();
			}
		Assert.True(bad.Count == 0, bad.Count + ": " + string.Join(" || ", bad.Take(6)));
	}

	// AM22: one constant ink, a visible strength.
	[Test]
	public static void SketchInkIsOneVisibleConstant()
	{
		Assert.True(MapRoutes.SketchInk > 0.02f && MapRoutes.SketchInk <= 1f, "SketchInk " + MapRoutes.SketchInk);
	}

	// AM22: nothing named mist or cloud remains in the map scene or its scripts.
	[Test]
	public static void NoMistOrCloudLeftInMapScene(Node host)
	{
		var s = Make(host, StubMapView.Grid(5, 3), new Vector2(1280, 720));
		var hits = new List<string>();
		void Walk(Node n)
		{
			string sc = n.GetScript().AsGodotObject() is Script scr ? scr.ResourcePath : "";
			if (n.Name.ToString().ToLower().Contains("mist") || n.Name.ToString().ToLower().Contains("cloud") || sc.ToLower().Contains("mist") || sc.ToLower().Contains("cloud")) hits.Add(n.GetPath() + " " + sc);
			foreach (var c in n.GetChildren()) Walk(c);
		}
		Walk(s);
		var txt = FileAccess.GetFileAsString("res://scenes/ui/map_screen.tscn").ToLower();
		if (txt.Contains("mist") || txt.Contains("cloud")) hits.Add("map_screen.tscn text");
		Assert.True(hits.Count == 0, string.Join(" || ", hits));
		s.GetParent().QueueFree();
	}
}
