using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-240 breaker probes: polish of the map v2 screen (grown boxes, larger meander, lone-row tag, cumulus mist, IMapView members).</summary>
public static class MapPolishBreakerProbes
{
	static readonly Vector2[] Sizes = { new(1920, 1080), new(1280, 720), new(1280, 1024), new(2560, 1080), new(1000, 1000) };

	static StubMapView Random(int seed, int cols, int maxPer, string type = "duel", bool elder = false, string? twist = null, bool hiddenSwap = false)
	{
		var v = new StubMapView();
		var ids = new List<List<string>>();
		for (int c = 0; c < cols; c++)
		{
			int cnt = c == 0 || c == cols - 1 ? 1 : 1 + (int)(MapLayout.Hash("cnt" + seed + ":" + c) % (uint)maxPer);
			ids.Add(Enumerable.Range(0, cnt).Select(i => $"c{c}n{i}").ToList());
			foreach (var id in ids[^1])
			{
				bool swap = hiddenSwap && c >= 2; // rows 1 and 2 stay drawn; the rest are locked and hidden
				v.Add(id, c + 1, swap ? type : "duel", swap ? "Swapped " + id : "Y" + id, swap && elder, swap ? twist : null);
			}
		}
		var seen = new HashSet<(string, string)>();
		for (int c = 0; c + 1 < cols; c++)
			foreach (var a in ids[c])
			{
				int k = 1 + (int)(MapLayout.Hash("k" + seed + a) % 3);
				for (int j = 0; j < k; j++)
				{
					var tgt = ids[c + 1];
					var b = tgt[(int)(MapLayout.Hash("t" + seed + a + j) % (uint)tgt.Count)];
					if (seen.Add((a, b))) v.Route(a, b);
				}
			}
		v.Open(ids[0].ToArray());
		return v;
	}

	static MapScreen Make(Node host, IMapView view, Vector2 size)
	{
		var s = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		var stage = new Control { Size = size };
		host.AddChild(stage); stage.AddChild(s);
		s.SetView(view); s.FinishPresentation();
		return s;
	}

	static string Fingerprint(MapScreen s, IMapView v)
	{
		var sb = new System.Text.StringBuilder();
		foreach (var n in v.Nodes) sb.Append($"{n.Id}:{s.RectOf(n.Id)}:{s.SpriteRectOf(n.Id)}:{MapNodeBox.Grown(s.RectOf(n.Id), s.SpriteRectOf(n.Id).Size.Y)};");
		foreach (var kv in s.RoutePaths.OrderBy(k => k.Key.From + k.Key.To)) sb.Append($"{kv.Key}:{string.Join(",", kv.Value)};");
		foreach (var l in s.Landmarks.Plan) sb.Append($"{l.Kind}|{l.Rect}|{l.Route}|{l.OnPath};");
		foreach (var l in s.Routes.MistLines) sb.Append($"{l.A}{l.B}{string.Join(",", l.Path)};");
		return sb.ToString();
	}

	// R2 [AM19]: swapping type, yokai, elder status or twist of every hidden node changes no box, grown box, route, mist value or landmark.
	[Test]
	public static void HiddenSwapLeaksNothingAcrossSeedsAndSizes(Node host)
	{
		var leaks = new List<string>();
		foreach (var size in Sizes)
			foreach (int seed in new[] { 1, 2, 3, 4 })
			{
				int cols = 5 + seed;
				var baseV = Random(seed, cols, 5);
				var sb = Make(host, baseV, size);
				string baseline = Fingerprint(sb, baseV);
				sb.GetParent().QueueFree();
				foreach (var (type, elder, twist) in new[] { ("merchant", false, (string?)null), ("dojo", false, null), ("boss", false, null), ("elder", true, "mirror"), ("rest", false, null) })
				{
					var v = Random(seed, cols, 5, type, elder, twist, true);
					var s = Make(host, v, size);
					// ids are shared, names of hidden nodes differ, so compare geometry only (the fingerprint holds none)
					if (Fingerprint(s, v) != baseline) leaks.Add($"{size} seed {seed} '{type}'");
					s.GetParent().QueueFree();
				}
			}
		Assert.True(leaks.Count == 0, "hidden swap changes layout/mist/landmarks: " + string.Join("; ", leaks.Take(6)));
	}

	// Ticket item 1: nothing off the paper, grown boxes never overlap each other, landmarks keep clear of grown boxes, at extreme aspect ratios.
	[Test]
	public static void GrownBoxesAndRoutesStayOnPaperAndApart(Node host)
	{
		var f = new List<string>();
		foreach (var size in Sizes)
			for (int seed = 10; seed < 16; seed++)
			{
				var v = Random(seed, 6 + seed % 4, 6);
				var s = Make(host, v, size);
				var board = new Rect2(Vector2.Zero, s.Board.Size);
				var grown = v.Nodes.Select(n => (n.Id, G: MapNodeBox.Grown(s.RectOf(n.Id), s.SpriteRectOf(n.Id).Size.Y))).ToList();
				foreach (var g in grown)
					if (!board.Grow(0.5f).Encloses(g.G)) f.Add($"{size} s{seed}: grown {g.Id} {g.G} off board {board}");
				for (int i = 0; i < grown.Count; i++)
					for (int j = i + 1; j < grown.Count; j++)
						{ float depth = grown[i].G.Intersection(grown[j].G).Size.Y; if (grown[i].G.Intersects(grown[j].G) && depth > 6f) f.Add($"{size} s{seed}: grown {grown[i].Id} overlaps {grown[j].Id} by {depth:F1}px vertically"); }
				foreach (var kv in s.RoutePaths)
					if (kv.Value.Any(p => p.Y < -0.5f || p.Y > board.Size.Y + 0.5f || p.X < -0.5f || p.X > board.Size.X + 0.5f)) f.Add($"{size} s{seed}: route {kv.Key} leaves the paper");
				foreach (var l in s.Landmarks.Plan)
				{
					var hit = grown.Where(g => g.G.Intersects(l.Rect)).Select(g => g.Id).ToList();
					if (hit.Count > 0) f.Add($"{size} s{seed}: {l.Kind} on grown box of {hit[0]}");
					if (!board.Encloses(l.Rect)) f.Add($"{size} s{seed}: {l.Kind} off paper");
				}
				s.GetParent().QueueFree();
			}
		Assert.True(f.Count == 0, f.Count + " failures (" + string.Join(", ", f.GroupBy(x => x.Contains("overlaps") ? "overlap" : x.Contains("landmark") || x.Contains("on grown") ? "landmark" : x.Contains("off") || x.Contains("leaves") ? "off-paper" : "other").Select(g => g.Key + " " + g.Count())) + "); first: " + string.Join(" || ", f.Where(x => !x.Contains("overlaps")).Concat(f.Take(2)).Take(5)));
	}

	// Ticket item 2: the larger meander must keep every route on the paper (Amplitude is up to 0.10 of min(board height, gap)).
	[Test]
	public static void MeanderKeepsEveryRouteOnThePaper(Node host)
	{
		var f = new List<string>();
		foreach (var size in Sizes)
			for (int seed = 10; seed < 30; seed++)
			{
				var v = Random(seed, 6 + seed % 4, 6);
				var s = Make(host, v, size);
				var h = s.Board.Size;
				foreach (var kv in s.RoutePaths)
				{
					float lo = kv.Value.Min(p => p.Y), hi = kv.Value.Max(p => p.Y);
					if (lo < -0.5f || hi > h.Y + 0.5f) f.Add($"{size} seed {seed}: route {kv.Key} y {lo:F1}..{hi:F1} outside 0..{h.Y:F1}");
				}
				s.GetParent().QueueFree();
			}
		Assert.True(f.Count == 0, f.Count + " routes off paper; first: " + string.Join(" || ", f.Take(4)));
	}

	// AM19: the sketches depend on the last drawn node only, never on the pan.
	[Test]
	public static void SketchesAreIndependentOfPanning(Node host)
	{
		var v = Random(7, 9, 4);
		var s = Make(host, v, new Vector2(1280, 720));
		string before = Fingerprint(s, v);
		Assert.True(s.MaxScroll > 0f, "the board scrolls in this fixture");
		s.PanTo(s.MaxScroll); s.SnapScroll();
		Assert.Equal(before, Fingerprint(s, v), "panning moves no sketch, box or route");
		s.GetParent().QueueFree();
	}

	// Boss tag sits inside its box with long names, at odd sizes (lone row reserves extra lines, AM19: by row size).
	[Test]
	public static void LoneBossTagStaysInsideBoxWithLongName(Node host)
	{
		var f = new List<string>();
		foreach (var size in Sizes)
			foreach (var name in new[] { "The Tanuki", "The Great Many-Tailed Tanuki of the Eighty-Eight Hidden Temples and Their Bells" })
			{
				var v = new StubMapView();
				v.Add("a", 1, "duel", "Apprentice"); v.Add("boss", 2, "boss", name); v.Route("a", "boss"); v.Open("boss");
				var s = Make(host, v, size);
				var n = s.NodeFor("boss");
				if (n == null) { f.Add($"{size}: boss not drawn"); s.GetParent().QueueFree(); continue; }
				if (n.Tag.Position.Y + n.Tag.Size.Y > n.Size.Y + 0.5f) f.Add($"{size} '{name.Substring(0, 10)}': tag bottom {n.Tag.Position.Y + n.Tag.Size.Y} past box {n.Size.Y}");
				s.GetParent().QueueFree();
			}
		Assert.True(f.Count == 0, string.Join(" || ", f.Take(5)));
	}

	// T6/AM20: the map view carries no copy target.
	[Test]
	public static void MapViewCarriesNoCopyTarget()
	{
		foreach (var t in new[] { typeof(IMapView), typeof(StubMapView) })
			Assert.True(t.GetMembers().All(m => !m.Name.Contains("Copy")), t.Name + " exposes a Copy member");
	}
}
