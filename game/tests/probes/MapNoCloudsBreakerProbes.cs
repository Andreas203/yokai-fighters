using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-245 breaker probes: sketches replace the mist (AM19, AM22, R2).</summary>
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

	static (float from, float span) Fade(MapScreen s)
	{
		var t = typeof(MapRoutes);
		var f = (float)t.GetField("_mistFrom", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s.Routes)!;
		var sp = (float)t.GetField("_mistSpan", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s.Routes)!;
		return (f, sp);
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
		var (f, sp) = Fade(s);
		sb.Append($"{f}/{sp};");
		foreach (var l in s.Routes.MistLines) sb.Append($"{l.A}{l.B}{string.Join(",", l.Path)};");
		for (float x = -100; x < s.Board.Size.X + 300; x += 23f) sb.Append(MapRoutes.SketchAlpha(x, f, sp).ToString("R")).Append(',');
		foreach (var n in v.Nodes) sb.Append($"{n.Id}:{s.RectOf(n.Id)};");
		foreach (var kv in s.RoutePaths.OrderBy(k => k.Key.From + k.Key.To)) sb.Append($"{kv.Key}:{string.Join(",", kv.Value)};");
		foreach (var l in s.Landmarks.Plan) sb.Append($"{l.Kind}|{l.Rect}|{l.Route}|{l.OnPath};");
		return sb.ToString();
	}

	// R2 / AM19 / AM22: hidden node type, yokai, elder status and twist change no sketch, alpha, path or landmark.
	[Test]
	public static void HiddenSwapLeavesSketchAlphaAndLinesIdentical(Node host)
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

	// AM19: SketchAlpha is within [0, 0.6], non-increasing and finite for any odd span or fadeFrom.
	[Test]
	public static void SketchAlphaBoundedMonotonicFinite()
	{
		var bad = new List<string>();
		foreach (var (from, span) in new[] { (0f, 0f), (100f, -50f), (100f, 1f), (5000f, 240f), (-300f, 300f), (0f, 1e9f), (300f, float.Epsilon), (50f, 1e-30f) })
		{
			float last = float.MaxValue;
			for (float x = -2000; x < 9000; x += 3f)
			{
				float a = MapRoutes.SketchAlpha(x, from, span);
				if (float.IsNaN(a) || float.IsInfinity(a) || a < 0f || a > 0.6f + 1e-6f) bad.Add($"({from},{span}) x={x}: {a}");
				if (a > last + 1e-6f) { bad.Add($"({from},{span}) x={x}: rises {last}->{a}"); break; }
				last = a;
			}
			if (MapRoutes.SketchAlpha(from + 1000f * System.Math.Max(1f, span) , from, span) != 0f && span < 1e6f) bad.Add($"({from},{span}) never reaches 0");
		}
		Assert.True(bad.Count == 0, string.Join(" || ", bad.Take(5)));
	}

	// AM19: every sketch out of a reachable node keeps a visible dash span; the fade is 0 before the paper's end (incl. Ryo on row 8, Tanuki next).
	[Test]
	public static void NoSketchVanishesAndFadeEndsBeforePaperEnd(Node host)
	{
		var bad = new List<string>();
		foreach (var size in Sizes)
			foreach (var (rows, per, steps) in new[] { (9, 3, 0), (9, 3, 1), (9, 3, 4), (9, 3, 7), (9, 2, 7), (9, 4, 7), (3, 1, 0), (2, 1, 0), (9, 1, 7) })
			{
				var v = Advance(rows, per, steps);
				var s = Make(host, v, size);
				var (f, sp) = Fade(s);
				string tag = $"{size} {rows}x{per} steps={steps}";
				float W = s.Board.Size.X;
				if (MapRoutes.SketchAlpha(W, f, sp) >= Skip) bad.Add($"{tag}: alpha at paper end {MapRoutes.SketchAlpha(W, f, sp)} (from {f}, span {sp}, W {W})");
				float visX = f; for (float x = f; x < f + sp + 5f; x += 1f) if (MapRoutes.SketchAlpha(x, f, sp) >= Skip) visX = x;
				var reach = v.Nodes.Where(n => n.State == MapNodeState.Reachable).Select(n => n.Id).ToHashSet();
				foreach (var l in s.Routes.MistLines.Where(q => q.A.X <= f))
				{
					float minX = l.Path.Length > 0 ? l.Path.Min(p => p.X) : l.A.X, maxX = l.Path.Length > 0 ? l.Path.Max(p => p.X) : l.B.X;
					float vis = System.Math.Min(maxX, visX) - minX;
					if (vis < 26f) bad.Add($"{tag}: sketch {l.A}->{l.B} visible span {vis}");
				}
				if (steps < rows - 1 && s.Routes.MistLines.Count == 0 && v.Routes.Any(r => reach.Contains(r.From))) bad.Add($"{tag}: no sketches although routes leave reachable nodes");
				s.GetParent().QueueFree();
			}
		Assert.True(bad.Count == 0, bad.Count + ": " + string.Join(" || ", bad.Take(6)));
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
