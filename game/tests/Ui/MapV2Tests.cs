using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-238: the map v2 rebuild (designer decisions 2026-11-09). Nodes are illustrated objects with a name tag (look per type, yokai and elder, the
/// Tanuki never drawn), laid out at staggered heights by a deterministic hash, with landmarks that never sit on a node; the routes wind; the
/// real routes ahead are sketched on bare paper with no node control anywhere past the reveal (AM19); the paper is the title scroll.
/// </summary>
public static class MapV2Tests
{
	static readonly Vector2 P1080 = new(1920, 1080), P720 = new(1280, 720);

	static MapScreen Make(Node host, IMapView view, Vector2? size = null)
	{
		var s = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		var stage = new Control { Size = size ?? P1080 };
		host.AddChild(stage);
		stage.AddChild(s);
		s.SetView(view);
		return s;
	}

	static RunSession Start(uint seed = 5)
	{
		string md = ContentPaths.Data("map");
		return RunSession.Start(YokaiFighters.Fight.FightScene.LoadAbilityPool(), FileMapSource.Default(md).Load(), NodeSceneTable.Default(md), seed);
	}

	static void Walk(RunSession s, params string[] ids)
	{
		foreach (var id in ids)
		{
			s.Enter(id);
			if (s.Node!.Type is not ("merchant" or "dojo"))
			{
				var d = s.WinFight(840);
				if (d != null) { var c = d.Offer.Cards[0]; d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null); }
			}
			s.CompleteNode();
		}
	}

	static string Sprite(MapNode n) => n.SpriteTexture?.ResourcePath.Split('/').Last() ?? "(none)";
	static MapNodeStyles Styles() => GD.Load<MapNodeStyles>("res://ui/map_node_styles.tres");

	[Test]
	public static void NodeLookPerTypeYokaiAndElder(Node host)
	{
		var sess = Start();
		Walk(sess, "r1-apprentice", "r2-kitsune", "r3-oni", "r4-merchant");
		var s = Make(host, new SessionMapView(sess));
		Assert.Equal("mask-apprentice.png", Sprite(s.NodeFor("r1-apprentice")!), "the apprentice wears the plain mask");
		Assert.Equal("mask-kitsune.png", Sprite(s.NodeFor("r2-kitsune")!), "a kitsune duel node is the kitsune mask");
		Assert.Equal("mask-oni.png", Sprite(s.NodeFor("r3-oni")!), "an oni duel node is the oni mask");
		Assert.Equal("node-merchant.png", Sprite(s.NodeFor("r4-merchant")!), "the merchant is the stall");
		Assert.Equal("mask-elder-oni.png", Sprite(s.NodeFor("r5-elder-oni")!), "an elder uses the elder mask of its yokai (elder-oni)");
		Assert.Equal("mask-nine-tails.png", Sprite(s.NodeFor("r5-nine-tails")!), "nine-tailed-kitsune -> the nine-tails mask");
		foreach (var id in new[] { "r5-elder-oni", "r5-nine-tails" })
		{
			var n = s.NodeFor(id)!;
			Assert.True(n.ElderShown && n.TwistIconId != "", id + ": the elder keeps its red twist seal (Y3)");
			var seal = n.GetNode<Control>("ElderMark");
			var sprite = n.SpriteRect;
			Assert.True(seal.Position.X > sprite.GetCenter().X && seal.Position.Y < sprite.GetCenter().Y, id + ": the seal sits at the mask's upper right");
		}
		Assert.True(!s.NodeFor("r3-oni")!.ElderShown && !s.NodeFor("r2-kitsune")!.ElderShown, "plain duel nodes carry no seal");
		Walk(sess, "r5-elder-oni", "r6-kappa", "r7-dojo", "r8-elder-kappa");
		s.SetView(new SessionMapView(sess));
		Assert.Equal("mask-elder-kappa.png", Sprite(s.NodeFor("r8-elder-kappa")!), "elder-kappa -> its elder mask");
		Assert.Equal("mask-kappa.png", Sprite(s.NodeFor("r6-kappa")!), "kappa -> the kappa mask");
		Assert.Equal("node-dojo.png", Sprite(s.NodeFor("r7-dojo")!), "the dojo");
		Assert.Equal("node-tanuki.png", Sprite(s.NodeFor("boss-tanuki")!), "the boss is the tea house");
		Assert.True(Styles().MaskFor("tanuki", "The Tanuki") == null, "no mask exists for the Tanuki: he is never drawn");
		Assert.Equal("The Tanuki", s.NodeFor("boss-tanuki")!.YokaiText, "the tag keeps today's boss text");
		Assert.Equal("waits at the end\ncopies your best move", s.NodeFor("boss-tanuki")!.TagText, "and its two lines");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void UnknownYokaiAndTypeFallBackToThePlainMask()
	{
		var styles = Styles();
		var unknown = styles.Look(new MapNodeView("x", 1, "duel", "Zashiki-warashi", false, MapNodeState.Reachable) { YokaiId = "zashiki-warashi" });
		Assert.True(unknown.Sprite != null && unknown.Sprite.ResourcePath.EndsWith("mask-apprentice.png"), "an unknown yokai falls back to the plain mask, not a hole");
		var odd = styles.Look(new MapNodeView("y", 1, "shrine", null, false, MapNodeState.Reachable));
		Assert.Equal("Shrine", odd.Label, "an unknown type keeps its own name");
		Assert.True(odd.Sprite != null, "and still gets a sprite");
		Assert.True(styles.MaskFor("KITSUNE", null) != null && styles.MaskFor(null, "Nine-Tails") != null, "lookup is by id, else by display name, case-blind");
		Assert.True(styles.MaskFor(null, null) == null && styles.MaskFor("", "") == null, "no yokai: no mask");
	}

	[Test]
	public static void MarksAndTagStatesOnTheNodes(Node host)
	{
		var sess = Start();
		Walk(sess, "r1-apprentice", "r2-oni");
		var s = Make(host, new SessionMapView(sess));
		var cleared = s.NodeFor("r1-apprentice")!; var here = s.NodeFor("r2-oni")!;
		Assert.True(cleared.ClearedShown && cleared.Mark == null && cleared.StateText == "", "cleared: a red tick on the tag, no ring");
		Assert.True(here.Mark == MapMark.Here && here.StateText == "YOU ARE HERE" && !here.ClearedShown, "here: the persimmon ring and the caption");
		var a = s.NodeFor("r3-oni")!; var b = s.NodeFor("r3-kappa")!;
		Assert.True(a.Mark == MapMark.Selected && b.Mark == MapMark.Reachable, "the cursor node is selected (halo and pointer), the other reachable (thin indigo ring)");
		s.Move(1);
		Assert.True(b.Mark == MapMark.Selected && a.Mark == MapMark.Reachable, "the halo follows the cursor");
		Assert.True(a.TypeText == "D" && a.YokaiText == "Oni", "the tag reads type letter then name");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void HitAreaCoversTheSpriteAndTheTagAndNothingElseIsPressable(Node host)
	{
		var sess = Start();
		Walk(sess, "r1-apprentice");
		var s = Make(host, new SessionMapView(sess));
		foreach (var n in s.Nodes)
		{
			var box = new Rect2(Vector2.Zero, n.Size);
			Assert.True(box.Encloses(n.SpriteRect), n.NodeId + ": the sprite is inside the hit area");
			var tag = new Rect2(n.Tag.Position, n.Tag.Size);
			Assert.True(tag.Position.Y >= n.SpriteRect.Position.Y && tag.Position.X >= -1f && tag.End.X <= n.Size.X + 24f, n.NodeId + ": the tag sits under the sprite, inside the hit width: " + tag + " in " + n.Size);
			Assert.True(n.MouseFilter != Control.MouseFilterEnum.Ignore, n.NodeId + ": the button takes the mouse");
			Assert.True(n.GetChildren().OfType<Control>().All(c => c.MouseFilter == Control.MouseFilterEnum.Ignore), n.NodeId + ": nothing inside it eats the click");
		}
		var fired = new List<string>();
		s.NodeChosen += id => fired.Add(id);
		var node = s.NodeFor("r2-oni")!;
		node.EmitSignal(BaseButton.SignalName.MouseEntered);
		Assert.Equal("r2-oni", s.CursorId, "hover moves the cursor");
		node.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Equal("r2-oni", string.Join(",", fired), "a press on the node picks it");
		s.GetParent().QueueFree();
	}

	// ---- layout --------------------------------------------------------------------------------------------------------------------

	static MapLayoutResult DefaultLayout(Vector2 board)
	{
		var view = new SessionMapView(Start());
		return MapLayout.Compute(view.Nodes, board);
	}

	[Test]
	public static void StaggerIsDeterministicAndNotOnALine()
	{
		var board = new Vector2(4600, 612);
		var a = DefaultLayout(board); var b = DefaultLayout(board);
		Assert.True(a.Rects.Keys.All(k => a.Rects[k] == b.Rects[k]), "the same map lays out the same every time");
		var other = MapLayout.Compute(new SessionMapView(Start(99)).Nodes, board);
		Assert.True(other.Rects.Keys.All(k => a.Rects[k] == other.Rects[k]), "and the seed of the run does not move nodes (the layout is a function of the map)");
		var ys = a.Rects.Values.Select(r => r.GetCenter().Y).ToList();
		Assert.True(ys.Max() - ys.Min() > board.Y * 0.4f, $"nodes sit at staggered heights, spread {ys.Max() - ys.Min():F0} of {board.Y}");
		foreach (var row in new[] { 1, 4, 9 }) // the single-node rows: their heights must not all be the same
		{
			var id = row == 1 ? "r1-apprentice" : row == 4 ? "r4-merchant" : "boss-tanuki";
			Assert.True(a.Rects.ContainsKey(id), id);
		}
		var singles = new[] { "r1-apprentice", "r4-merchant", "boss-tanuki" }.Select(id => a.Rects[id].GetCenter().Y).ToList();
		Assert.True(singles.Max() - singles.Min() > board.Y * 0.08f, "lone nodes wander up and down: " + string.Join(", ", singles.Select(y => y.ToString("F0"))));
		// a different map is a different wander
		var tiny = new List<MapNodeView> { new("a", 1, "duel", null, false, MapNodeState.Locked), new("b", 2, "duel", null, false, MapNodeState.Locked), new("c", 3, "duel", null, false, MapNodeState.Locked) };
		var tiny2 = new List<MapNodeView> { new("p", 1, "duel", null, false, MapNodeState.Locked), new("q", 2, "duel", null, false, MapNodeState.Locked), new("r", 3, "duel", null, false, MapNodeState.Locked) };
		Assert.True(MapLayout.Compute(tiny, board).Rects["b"].Position.Y != MapLayout.Compute(tiny2, board).Rects["q"].Position.Y, "different node ids hash to a different wander");
	}

	[Test]
	public static void LayoutDoesNotChangeWithTheStateOfTheRun(Node host)
	{
		var sess = Start();
		var s = Make(host, new SessionMapView(sess));
		var before = sess.Map.Graph.Nodes.ToDictionary(n => n.Id, n => s.RectOf(n.Id));
		Walk(sess, "r1-apprentice", "r2-oni", "r3-kappa");
		s.SetView(new SessionMapView(sess));
		Assert.True(before.All(kv => s.RectOf(kv.Key) == kv.Value), "walking the map moves no node, drawn or hidden");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void NoNodeOverlapsAnotherOrLeavesTheBoardAt1080pAnd720p(Node host)
	{
		foreach (var size in new[] { P1080, P720 })
		{
			var s = Make(host, new SessionMapView(Start()), size);
			var ids = s.View!.Nodes.Select(n => n.Id).ToList();
			var board = new Rect2(Vector2.Zero, s.Board.Size);
			foreach (var id in ids)
			{
				Assert.True(board.Encloses(s.RectOf(id)), $"{size}: {id} inside the board");
				Assert.True(s.RectOf(id).Encloses(s.SpriteRectOf(id)), $"{size}: {id} sprite inside its box");
			}
			for (int i = 0; i < ids.Count; i++)
				for (int j = i + 1; j < ids.Count; j++)
					Assert.True(!s.SpriteRectOf(ids[i]).Intersects(s.SpriteRectOf(ids[j])), $"{size}: sprites of {ids[i]} and {ids[j]} overlap");
			s.GetParent().QueueFree();
		}
	}

	// ---- landmarks -----------------------------------------------------------------------------------------------------------------

	[Test]
	public static void LandmarksAreDeterministicAndAlongTheRoutes(Node host)
	{
		foreach (var size in new[] { P1080, P720 })
		{
			var s = Make(host, new SessionMapView(Start()), size);
			var plan = s.Landmarks.Plan;
			var paths = s.RoutePaths.Values.ToList();
			Assert.True(plan.Count >= 5, $"{size}: the routes are dressed with a few landmarks, not a crowd ({plan.Count})");
			Assert.True(s.LandmarksFront.Plan.Count == plan.Count, $"{size}: the front layer holds the same plan");
			var kinds = plan.Select(l => l.Kind).ToHashSet();
			Assert.True((kinds.Contains(MapLandmarkKind.PineTall) || kinds.Contains(MapLandmarkKind.PinesWide)) && kinds.Contains(MapLandmarkKind.Rocks) && kinds.Contains(MapLandmarkKind.Bridge) && kinds.Contains(MapLandmarkKind.Torii), $"{size}: pines, rocks, a footbridge and a torii all appear: " + string.Join(",", plan.Select(l => l.Kind)));
			var again = MapLandmarkPlan.Compute(s.Board.Size, s.View!.Nodes.Select(n => MapNodeBox.Grown(s.RectOf(n.Id), s.SpriteRectOf(n.Id).Size.Y)), paths, MapNodeBox.LineHeight(s.Board.Size.Y * MapLayout.SpriteShare));
			Assert.True(again.Select(l => (l.Kind, l.Rect)).SequenceEqual(plan.Select(l => (l.Kind, l.Rect))), $"{size}: the same board and routes give the same scenery");
			var board = new Rect2(Vector2.Zero, s.Board.Size);
			for (int i = 0; i < plan.Count; i++)
			{
				var l = plan[i];
				Assert.True(board.Encloses(l.Rect), $"{size}: {l.Kind} at {l.Rect} stays on the board");
				foreach (var n in s.View.Nodes)
					Assert.True(!l.Rect.Intersects(s.RectOf(n.Id)), $"{size}: {l.Kind} at {l.Rect} overlaps node {n.Id} at {s.RectOf(n.Id)}");
				for (int j = i + 1; j < plan.Count; j++) Assert.True(!l.Rect.Intersects(plan[j].Rect), $"{size}: {l.Kind} and {plan[j].Kind} overlap");
				float near = paths.Min(p => p.Min(q => q.DistanceTo(l.OnPath)));
				Assert.True(near < 5f, $"{size}: {l.Kind} is anchored on a route ({near:F1} px)");
				if (l.InFront)
				{
					Assert.True(l.Rect.HasPoint(l.OnPath), $"{size}: the route runs through the {l.Kind}");
					// the stroke of any other route must not run through the bridge or torii
					foreach (var p in paths)
						foreach (var q in p)
							if (l.Rect.HasPoint(q)) Assert.True(paths.Any(own => own.Any(o => o.DistanceTo(l.OnPath) < 5f) && own.Any(o => o.DistanceTo(q) < 10f)), $"{size}: a foreign route runs through the {l.Kind} at {q}");
				}
				else
				{
					foreach (var p in paths) Assert.True(!p.Any(q => l.Rect.HasPoint(q)), $"{size}: a {l.Kind} sits at the path's edge, not on a stroke");
					float far = MapLandmarkPlan.DistanceToStroke(l.Rect, paths);
					Assert.True(far <= 30f, $"{size}: a {l.Kind} at {l.Rect} is {far:F0} px from the nearest stroke (open paper)");
				}
				if (l.Kind == MapLandmarkKind.Torii)
				{
					var own = paths.First(p => p.Any(o => o.DistanceTo(l.OnPath) < 5f));
					float ht = l.Rect.Size.Y, top = l.Rect.Position.Y, feet = top + MapLandmarkPlan.ToriiFeet * ht;
					float xl = l.Rect.Position.X + MapLandmarkPlan.ToriiPostInset * l.Rect.Size.X, xr = l.Rect.End.X - MapLandmarkPlan.ToriiPostInset * l.Rect.Size.X, xc = l.Rect.GetCenter().X;
					var yl = MapLandmarkPlan.YAt(own, xl); var yc = MapLandmarkPlan.YAt(own, xc); var yr = MapLandmarkPlan.YAt(own, xr);
					Assert.True(yl != null && yc != null && yr != null, $"{size}: the route's x range covers both posts and the centre");
					foreach (var y in new[] { yl!.Value, yc!.Value, yr!.Value }) Assert.True(y >= top && y <= feet, $"{size}: the route at a post or the centre is at y {y:F0}, outside the posts' span {top:F0}..{feet:F0}");
					float fc = (yc.Value - top) / ht;
					Assert.True(fc >= 0.60f && fc <= 0.85f, $"{size}: the route crosses the gate at {fc:P0} of the posts' height, not in the lower third");
				}
				if (l.Kind == MapLandmarkKind.Bridge)
				{
					Assert.True(l.Stream != null && l.Stream.Length > 3, $"{size}: a bridge spans a stream");
					var own = paths.First(p => p.Any(o => o.DistanceTo(l.OnPath) < 5f));
					Assert.True(MapRouteGeometry.Crosses(own, l.Stream!), $"{size}: the route crosses the stream under its bridge");
					foreach (var n in s.View.Nodes) Assert.True(!s.RectOf(n.Id).Intersects(Rect(l.Stream!)), $"{size}: the stream reaches node {n.Id}");
				}
				else Assert.True(l.Stream == null, $"{size}: only a bridge has a stream");
			}
			float screen = MapLandmarkPlan.ScreenWidth(s.Board.Size);
			foreach (var l in plan)
			{
				var within = plan.Where(o => System.Math.Abs(o.Rect.GetCenter().X - l.Rect.GetCenter().X) < screen).ToList();
				Assert.Equal(1, within.Count(o => o.Kind == l.Kind), $"{size}: no {l.Kind} twice within one screen width");
				var window = plan.Where(o => System.Math.Abs(o.Rect.GetCenter().X - l.Rect.GetCenter().X) < screen / 2f).ToList(); // a window one screen wide centred on it
				Assert.True(window.Count(o => o.Kind == MapLandmarkKind.Bridge) <= 1 && window.Count(o => o.Kind == MapLandmarkKind.Torii) <= 1, $"{size}: at most one bridge and one torii per screen width");
				Assert.True(window.Count(o => !o.InFront) <= MapLandmarkPlan.MaxSmallPerScreen, $"{size}: at most {MapLandmarkPlan.MaxSmallPerScreen} pines and rocks per screen width");
			}
			s.GetParent().QueueFree();
		}
	}

	static Rect2 Rect(Vector2[] p) => new(p.Min(q => q.X), p.Min(q => q.Y), p.Max(q => q.X) - p.Min(q => q.X), p.Max(q => q.Y) - p.Min(q => q.Y));

	// ---- no crossing, no overlap ---------------------------------------------------------------------------------------------------

	/// <summary>Routes only join and split: no two centre lines properly cross, and two that come within 4 px of each other do so where they share a trunk vertex.</summary>
	static void AssertNoCrossing(IReadOnlyList<(string Name, Vector2[] Path)> routes, string where)
	{
		for (int i = 0; i < routes.Count; i++)
			for (int j = i + 1; j < routes.Count; j++)
			{
				var a = routes[i].Path; var b = routes[j].Path;
				Assert.True(!MapRouteGeometry.Crosses(a, b), $"{where}: {routes[i].Name} and {routes[j].Name} cross");
				var shared = a.Where(p => b.Contains(p)).ToList(); // exact common vertices: a junction or a stub both routes share
				foreach (var p in a)
				{
					if (p.X < 0) continue;
					var q = b.OrderBy(x => x.DistanceSquaredTo(p)).First();
					if (q.DistanceTo(p) >= 4f) continue;
					Assert.True(shared.Any(v => System.Math.Abs(v.X - p.X) < 100f), $"{where}: {routes[i].Name} and {routes[j].Name} run on top of each other at {p} with no shared trunk near");
				}
			}
	}

	/// <summary>The path resampled at even 8 px steps.</summary>
	static List<Vector2> Even(Vector2[] p)
	{
		var o = new List<Vector2> { p[0] }; float acc = 0f; var last = p[0];
		for (int i = 1; i < p.Length; i++)
		{
			float seg = last.DistanceTo(p[i]);
			while (acc + seg >= 8f) { float t = (8f - acc) / seg; last = last.Lerp(p[i], t); o.Add(last); seg = last.DistanceTo(p[i]); acc = 0f; }
			acc += seg; last = p[i];
		}
		return o;
	}

	static float Turn(Vector2 a, Vector2 b) => Mathf.RadToDeg(System.Math.Abs(a.AngleTo(b)));

	/// <summary>No sharp corners: at 8 px steps no path turns more than 35 degrees in one step, nor more than 60 degrees over 24 px (the default map; the dense synthetic shapes get 70).</summary>
	static void AssertSmooth(IReadOnlyList<(string Name, Vector2[] Path)> routes, string where, float window = 60f)
	{
		foreach (var (name, path) in routes)
		{
			var e = Even(path);
			var d = Enumerable.Range(0, e.Count - 1).Select(i => (e[i + 1] - e[i]).Normalized()).ToList();
			for (int i = 0; i + 1 < d.Count; i++)
				Assert.True(Turn(d[i], d[i + 1]) <= 35f, $"{where}: {name} turns {Turn(d[i], d[i + 1]):F0} degrees in one 8 px step at {e[i + 1]}");
			for (int i = 0; i + 3 < d.Count; i++)
				Assert.True(Turn(d[i], d[i + 3]) <= window, $"{where}: {name} turns {Turn(d[i], d[i + 3]):F0} degrees over 24 px at {e[i + 1]}");
		}
	}

	/// <summary>
	/// Junctions are shared trunks, never points and never an X: for every pair of routes with different sources and targets that share vertices, the shared run is
	/// at least <paramref name="minTrunk"/> px long, and in both routes the part before it lies to its left and the part after it to its right (join from one side, split to the other).
	/// </summary>
	static void AssertTrunks(IReadOnlyList<(string Name, Vector2[] Path)> routes, float minTrunk, string where)
	{
		for (int i = 0; i < routes.Count; i++)
			for (int j = i + 1; j < routes.Count; j++)
			{
				var a = routes[i].Path; var b = routes[j].Path;
				var common = new HashSet<Vector2>(a.Where(p => b.Contains(p)));
				if (common.Count == 0) continue;
				// the shared run as indices in a
				int i0 = System.Array.FindIndex(a, common.Contains), i1 = System.Array.FindLastIndex(a, common.Contains);
				bool atNode = i0 == 0 || i1 == a.Length - 1 || b[0] == a[i0] || b[^1] == a[i1]; // a shared source or target: a plain fork or merge at the node itself
				float len = 0f; for (int k = i0; k < i1; k++) len += a[k].DistanceTo(a[k + 1]);
				string pair = $"{where}: {routes[i].Name} and {routes[j].Name}";
				Assert.True(a.Skip(i0).Take(i1 - i0 + 1).All(common.Contains), pair + " share one unbroken run");
				if (!atNode) Assert.True(len >= minTrunk, $"{pair} meet in a trunk of only {len:F0} px (a point reads as an X)");
				int j0 = System.Array.IndexOf(b, a[i0]), j1 = System.Array.IndexOf(b, a[i1]);
				if (i0 > 0 && j0 > 0) Assert.True(a[i0 - 1].X < a[i0].X && b[j0 - 1].X < b[j0].X, pair + ": both come into the trunk from its left");
				if (i1 < a.Length - 1 && j1 < b.Length - 1) Assert.True(a[i1 + 1].X > a[i1].X && b[j1 + 1].X > b[j1].X, pair + ": both leave it to its right");
			}
	}

	static void AssertGeometry(IMapView view, Vector2 board, string where)
	{
		var layout = MapLayout.Compute(view.Nodes, board);
		var paths = MapRouteGeometry.Compute(view.Nodes, view.Routes, layout, board.Y);
		Assert.Equal(view.Routes.Count, paths.Count, where + ": a centre line per route");
		var named = paths.Select(kv => (kv.Key.From + ">" + kv.Key.To, kv.Value)).ToList();
		AssertNoCrossing(named, where);
		AssertSmooth(named, where, 70f); // the synthetic maps include complete 3x3 buses squeezed into one gap, harsher than the real map
		AssertTrunks(named, 55f, where);
		var again = MapRouteGeometry.Compute(view.Nodes, view.Routes, layout, board.Y);
		Assert.True(paths.All(kv => kv.Value.SequenceEqual(again[kv.Key])), where + ": deterministic");
	}

	[Test]
	public static void RoutesOfTheDefaultMapNeverCrossAtAnySizeOrReveal(Node host)
	{
		foreach (var size in new[] { P1080, P720 })
		{
			var sess = Start();
			var s = Make(host, new SessionMapView(sess), size);
			var baseline = s.RoutePaths.ToDictionary(kv => kv.Key, kv => kv.Value);
			Assert.Equal(sess.Map.Graph.Routes.Count, baseline.Count, "a centre line per route");
			int step = 0;
			while (true)
			{
				// everything the screen draws now: the brushed routes and the sketches
				var drawn = s.Routes.Lines.Select((l, i) => ($"line{i}", DrawnPath(s, l))).Concat(s.Routes.Sketches.Select((m, i) => ($"sketch{i}", m.Path))).ToList();
				AssertNoCrossing(drawn, $"{size} step {step}");
				AssertSmooth(drawn.Where(d => d.Item2.Length > 1).ToList(), $"{size} step {step} (drawn and sketches)");
				AssertTrunks(s.RoutePaths.Select(kv => (kv.Key.From + ">" + kv.Key.To, kv.Value)).ToList(), 55f, $"{size} step {step}");
				AssertNoCrossing(s.RoutePaths.Select(kv => (kv.Key.From + ">" + kv.Key.To, kv.Value)).ToList(), $"{size} step {step} (all routes)");
				Assert.True(s.RoutePaths.All(kv => kv.Value.SequenceEqual(baseline[kv.Key])), $"{size} step {step}: no route moved when nodes were revealed");
				if (s.ReachableIds.Count == 0) break;
				Walk(sess, s.ReachableIds[step % s.ReachableIds.Count]);
				s.SetView(new SessionMapView(sess)); s.FinishPresentation();
				step++;
			}
			Assert.True(step >= 9, "the walk went the whole way to the boss");
			s.GetParent().QueueFree();
		}
	}

	static Vector2[] DrawnPath(MapScreen s, (Vector2 A, Vector2 B, RouteKind Kind) line)
	{
		var hit = s.RoutePaths.Values.FirstOrDefault(p => p[0].DistanceTo(line.A) < 0.5f && p[^1].DistanceTo(line.B) < 0.5f);
		Assert.True(hit != null, $"no centre line matches the drawn line {line.A} -> {line.B}: the stroke would be checked against nothing");
		return hit!;
	}

	[Test]
	public static void RoutesNeverCrossOnUnusualMapShapes()
	{
		var shapes = new (int rows, int per)[] { (2, 1), (3, 2), (4, 3), (6, 5), (9, 3), (12, 3), (5, 6), (3, 1) };
		foreach (var (rows, per) in shapes)
			AssertGeometry(StubMapView.Grid(rows, per), new Vector2(rows * 595f, 612f), $"grid {rows}x{per}");
		// sparse random graphs: every node has one to three routes forward, picked by a hash of the ids
		for (int seed = 1; seed <= 12; seed++)
		{
			var v = new StubMapView();
			int rows = 5 + seed % 4;
			var ids = new List<List<string>>();
			for (int r = 1; r <= rows; r++)
			{
				int cnt = 1 + (int)(MapLayout.Hash("c" + seed + r) % 4);
				ids.Add(Enumerable.Range(0, cnt).Select(i => $"n{r}-{i}").ToList());
				foreach (var id in ids[^1]) v.Add(id, r, "duel", id);
			}
			for (int r = 0; r + 1 < rows; r++)
				foreach (var a in ids[r])
				{
					uint h = MapLayout.Hash(a + seed);
					var tgt = ids[r + 1];
					v.Route(a, tgt[(int)(h % (uint)tgt.Count)]);
					if (tgt.Count > 1 && MapLayout.Unit(h, 1) > 0.45f) v.Route(a, tgt[((int)(h % (uint)tgt.Count) + 1) % tgt.Count]);
				}
			var dedup = new StubMapView();
			foreach (var n in v.Nodes) dedup.Add(n.Id, n.Row, n.Type, n.Yokai);
			foreach (var r in v.Routes.Select(r => (r.From, r.To)).Distinct()) dedup.Route(r.From, r.To);
			AssertGeometry(dedup, new Vector2(rows * 595f, 612f), $"random {seed}");
		}
		var cross = new StubMapView().Add("a1", 1, "duel").Add("a2", 1, "duel").Add("b1", 2, "duel").Add("b2", 2, "duel")
			.Route("a1", "b2").Route("a2", "b1").Route("a1", "b1");
		AssertGeometry(cross, new Vector2(1190f, 612f), "inverted pair plus a fork");
	}

	[Test]
	public static void SiblingRoutesJoinAndSplitOnASharedTrunk()
	{
		var v = new StubMapView().Add("a1", 1, "duel").Add("a2", 1, "duel").Add("b1", 2, "duel").Add("b2", 2, "duel").Route("a1", "b1").Route("a1", "b2").Route("a2", "b1").Route("a2", "b2");
		var board = new Vector2(1190f, 612f);
		var layout = MapLayout.Compute(v.Nodes, board);
		var paths = MapRouteGeometry.Compute(v.Nodes, v.Routes, layout, board.Y);
		var all = paths.Values.ToList();
		var common = all[0].Where(p => all.All(q => q.Contains(p))).ToList();
		Assert.True(common.Count >= 1, "all four routes pass one junction point (a join, then a split)");
		Assert.True(paths[("a1", "b1")].Where(p => paths[("a1", "b2")].Contains(p)).Count() > 5, "routes out of one node leave together");
		Assert.True(paths[("a1", "b1")].Where(p => paths[("a2", "b1")].Contains(p)).Count() > 5, "routes into one node arrive together");
	}

	[Test]
	public static void StrokesStillWindBetweenLevelNodes()
	{
		var v = new StubMapView().Add("a", 1, "duel").Add("b", 2, "duel").Route("a", "b");
		var board = new Vector2(1190f, 612f);
		var layout = MapLayout.Compute(v.Nodes, board);
		var p = MapRouteGeometry.Compute(v.Nodes, v.Routes, layout, board.Y)[("a", "b")];
		float y0 = p[0].Y, y1 = p[^1].Y;
		float swing = p.Max(q => System.Math.Abs(q.Y - (y0 + (y1 - y0) * (q.X - p[0].X) / (p[^1].X - p[0].X))));
		Assert.True(swing > 8f, $"a lone route winds off the straight line by {swing:F1} px");
		Assert.True(p.Zip(p.Skip(1)).All(q => q.Second.X >= q.First.X - 0.01f), "and never doubles back along x");
	}

	[Test]
	public static void FallbackCurveWindsEvenBetweenLevelNodes()
	{
		var pts = MapRoutes.Path(new Vector2(0, 200), new Vector2(400, 200), 48, out var normals);
		Assert.True(pts[0] == new Vector2(0, 200) && pts[^1].DistanceTo(new Vector2(400, 200)) < 0.01f, "the path starts and ends on its anchors");
		float swing = pts.Max(p => System.Math.Abs(p.Y - 200f));
		Assert.True(swing > 8f, $"a level pair of nodes is still joined by an S, swing {swing:F1} px");
		Assert.True(pts.Zip(pts.Skip(1)).All(p => p.Second.X >= p.First.X - 1f), "and it never doubles back along x");
		Assert.True(MapRoutes.Path(new Vector2(0, 200), new Vector2(400, 200), 48, out _).Zip(pts).All(p => p.First == p.Second), "deterministic");
	}

	[Test]
	public static void SketchesTheRealRoutesAheadAndDrawNoNode(Node host)
	{
		var sess = Start();
		var s = Make(host, new SessionMapView(sess));
		var all = sess.Map.Graph.Routes.Count;
		Assert.Equal(all, s.Routes.Sketches.Count, "at the start every route of the map is listed as ahead and sketched, boss included (the fade hides the far rows)");
		Assert.Equal("r1-apprentice", string.Join(",", s.Nodes.Select(n => n.NodeId)), "and still only the first node is a control");

		Walk(sess, "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant");
		s.SetView(new SessionMapView(sess));
		var view = new SessionMapView(sess);
		var state = view.Nodes.ToDictionary(n => n.Id, n => n.State);
		int ahead = view.Routes.Count(r => state[r.To] == MapNodeState.Locked && state[r.From] != MapNodeState.PassedOver);
		Assert.Equal(14, ahead, "(the rows 5-6, 6-7, 7-8 and 8-boss routes)");
		Assert.Equal(ahead, s.Routes.Sketches.Count, "only routes whose far end is not drawn are sketched: not the brushed ones, not those of rows Ryo left");
		var revealed = state.Where(kv => MapReveal.IsRevealed(kv.Value)).Select(kv => kv.Key).ToHashSet();
		Assert.True(s.Nodes.All(n => revealed.Contains(n.NodeId)), "no control for any node not revealed");
		foreach (var r in view.Routes.Where(r => state[r.To] == MapNodeState.Locked && state[r.From] != MapNodeState.PassedOver))
		{
			var c = s.SpriteRectOf(r.To).GetCenter();
			Assert.True(s.Routes.Sketches.Any(l => l.B.DistanceTo(c) < 0.5f), $"the sketch of {r.From} -> {r.To} runs through where {r.To} will stand");
		}
		Assert.True(s.Routes.Sketches.All(l => l.A.X < l.B.X), "sketches run forward too");
		var text = string.Join("\n", AllText(s)).ToLowerInvariant();
		foreach (var hidden in new[] { "tanuki", "kitsune", "dojo", "boss", "elder kappa" })
			Assert.True(!text.Contains(hidden), $"no text names '{hidden}' while its row is still ahead");
		s.GetParent().QueueFree();
	}

	static IEnumerable<string> AllText(Node n)
	{
		if (n is Godot.Label l) yield return l.Text;
		foreach (var c in n.GetChildren()) foreach (var t in AllText(c)) yield return t;
	}

	[Test]
	public static void NoSketchOnceTheBossIsTheChoice(Node host)
	{
		var sess = Start();
		while (sess.Map.Graph.Node(sess.Reachable()[0]).Type != "boss") Walk(sess, sess.Reachable()[0]);
		var s = Make(host, new SessionMapView(sess));
		Assert.Equal(0, s.Routes.Sketches.Count, "nothing is ahead of the boss");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void PaperIsTheTitleScrollAndHasNoMountNoSeamsNoHeadings(Node host)
	{
		Assert.True(GD.Load<Texture2D>(MapScroll.TexturePath) != null, "the title's reason scroll is the paper");
		Assert.Equal("res://assets/generated/ui/title-reason-scroll.png", MapScroll.TexturePath, "and it is that file");
		var s = Make(host, new SessionMapView(Start()));
		var paper = UiFind.Get<Control>(s, "Paper");
		Assert.True(paper is MapScroll && paper.Size == s.Scroll.Size, "the paper fills the scroll");
		Assert.True(s.FindChild("RowHeaders", true, false) == null, "no row headings");
		Assert.Equal(0, paper.GetChildCount(), "the paper is one drawing");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void StatusPanelHasNoRowCount(Node host)
	{
		var sess = Start();
		var s = Make(host, new SessionMapView(sess));
		Assert.True(s.StatusHealthText.StartsWith("RYO · ") && s.StatusCoinsText.EndsWith("coins"), "health and coins stay");
		Assert.True(s.FindChild("RowsText", true, false) == null, "the 'row N of 8 done' label is gone");
		Assert.True(UiFind.Get<ProgressBar>(s, "HealthBar").Visible, "with the health bar");
		var text = string.Join("\n", AllText(UiFind.Get<Control>(s, "Status")));
		Assert.True(!text.ToLowerInvariant().Contains("done") && !text.ToLowerInvariant().Contains("row"), "no row count: " + text);
		s.GetParent().QueueFree();
	}

	[Test]
	public static void HiddenNodeShapeNeverMovesRoutesSketchesOrScenery(Node host)
	{
		(string Type, string? Yokai)[][] variants =
		{
			new[] { ("duel", "Oni"), ("duel", "Kappa"), ("duel", "Kitsune") },
			new[] { ("elder", "Elder Oni"), ("merchant", null), ("boss", "The Tanuki") },
			new[] { ("dojo", null), ("elder", "Nine-Tails"), ("duel", "Zzz") },
		};
		string? p0 = null, l0 = null, m0 = null, r0 = null;
		foreach (var v in variants)
		{
			var view = new StubMapView();
			view.Add("a", 1, "duel", "Apprentice");
			for (int i = 0; i < 3; i++) view.Add("b" + i, 2, v[i].Type, v[i].Yokai, v[i].Type == "elder");
			for (int i = 0; i < 3; i++) view.Add("c" + i, 3, v[(i + 1) % 3].Type, v[(i + 1) % 3].Yokai, v[(i + 1) % 3].Type == "elder");
			for (int i = 0; i < 3; i++) { view.Route("a", "b" + i); view.Route("b" + i, "c" + i); }
			view.Route("b0", "c1"); view.Open("a");
			var s = Make(host, view);
			string p = string.Join(";", s.RoutePaths.OrderBy(k => k.Key.ToString()).Select(k => string.Join(",", k.Value)));
			string l = string.Join(";", s.Landmarks.Plan.Select(x => $"{x.Kind}{x.Rect}{x.Route}"));
			string m = string.Join(";", s.Routes.Sketches.Select(x => string.Join(",", x.Path)));
			string r = string.Join(";", Enumerable.Range(0, 7).Select(i => i < 1 ? "" : "").Concat(new[] { "b0", "b1", "b2", "c0", "c1", "c2" }.Select(id => s.RectOf(id).ToString())));
			if (p0 == null) { p0 = p; l0 = l; m0 = m; r0 = r; }
			else
			{
				Assert.True(p == p0, "the type or yokai of a hidden node moved a route centre line");
				Assert.True(l == l0, "... or the landmarks");
				Assert.True(m == m0, "... or a sketch");
				Assert.True(r == r0, "... or a node box");
			}
			s.GetParent().QueueFree();
		}
	}

	[Test]
	public static void LeftBehindScenerySwitchesOffWithItsRouteAndFadesLikeTheNodes(Node host)
	{
		var sess = Start();
		var s = Make(host, new SessionMapView(sess));
		Walk(sess, "r1-apprentice");
		s.SetView(new SessionMapView(sess)); s.FinishPresentation();
		var before = s.Landmarks.Shown.Count();
		Walk(sess, "r2-oni");
		s.Refresh();
		Assert.True(s.Landmarks.Shown.Count() <= before + 20, "scenery stays within the plan");
		if (s.Landmarks.IsFading) Assert.True(s.Presenting, "a fade in progress is part of the presentation");
		s.FinishPresentation();
		Assert.True(!s.Landmarks.IsFading && !s.LandmarksFront.IsFading, "skipping ends the fade");
		var left = sess.Map.Graph.Routes.Select((r, i) => (r, i)).Where(x => x.r.From == "r1-apprentice" && x.r.To != "r2-oni").ToList();
		Assert.True(left.Count == 2, "two routes of row 1 were left behind");
		s.GetParent().QueueFree();
	}

	/// <summary>
	/// Code Critic (YOK-238): leaving behind a route with scenery together with routes without any must start the fade, not clear it
	/// (a dangling <c>else</c> once wiped it whenever a scenery-less route came next). The fade keeps the scenery drawn until it ends or is skipped.
	/// </summary>
	[Test]
	public static void LeftBehindSceneryFadesEvenWhenOtherLeftRoutesHaveNone(Node host)
	{
		var l = new MapLandmarks();
		host.AddChild(l);
		var rock = new MapLandmark(MapLandmarkKind.Rocks, new Rect2(10, 10, 20, 20)) { Route = 0 };
		l.SetPlan(new[] { rock }, Vector2.Zero);
		l.SetRoutes(new[] { 0, 1, 2, 3 });
		l.SetRoutes(new int[0], animate: true);
		Assert.True(l.IsFading, "the left-behind route's scenery starts fading although routes 1-3 carry none");
		Assert.True(l.Shown.Contains(rock), "... and stays drawn while it fades");
		l.FinishFade();
		Assert.True(!l.IsFading && !l.Shown.Contains(rock), "skipping ends the fade and the scenery is gone");
		l.SetRoutes(new[] { 0 });
		l.SetRoutes(new int[0], animate: false);
		Assert.True(!l.IsFading && !l.Shown.Contains(rock), "without animation the scenery goes at once");
		l.QueueFree();
	}

	// ---- YOK-240: map v2 polish -----------------------------------------------------------------------------------------------------

	static StubMapView RowOfThree()
	{
		var v = new StubMapView();
		v.Add("a", 1, "duel", "Apprentice");
		v.Add("b0", 2, "merchant", null); v.Add("b1", 2, "duel", "Oni"); v.Add("b2", 2, "dojo", null);
		foreach (var b in new[] { "b0", "b1", "b2" }) v.Route("a", b);
		v.Open("a"); v.Select("a"); v.Complete();
		return v;
	}

	/// <summary>A stall, the dojo or the tea house is drawn larger than a mask, yet its box, sprite rect and everything the layout reads are the same as a mask's (AM19).</summary>
	[Test]
	public static void BuildingsDrawLargerThanMasksWithoutChangingTheBox(Node host)
	{
		foreach (var size in new[] { P1080, P720 })
		{
			var s = Make(host, RowOfThree(), size);
			var merchant = s.NodeFor("b0")!; var mask = s.NodeFor("b1")!; var dojo = s.NodeFor("b2")!;
			Assert.True(merchant.Size.IsEqualApprox(mask.Size) && dojo.Size.IsEqualApprox(mask.Size), $"{size}: one box for every type in a row: {merchant.Size} {mask.Size} {dojo.Size}");
			Assert.True(s.RectOf("b0").Size.IsEqualApprox(s.RectOf("b1").Size) && s.SpriteRectOf("b0").Size.IsEqualApprox(s.SpriteRectOf("b2").Size), "layout rect and sprite rect do not depend on the type");
			float ratio(MapNode n) => UiFind.Get<TextureRect>(n, "IconTexture").Size.Y / n.SpriteRect.Size.Y;
			Assert.True(ratio(merchant) >= 1.2f && ratio(dojo) >= 1.2f, $"{size}: the stall ({ratio(merchant):0.00}) and the dojo ({ratio(dojo):0.00}) draw at least 1.2x the sprite rect");
			Assert.True(ratio(mask) <= 1.01f, $"{size}: a mask stays inside its rect ({ratio(mask):0.00})");
			Assert.True(merchant.ArtRect.Encloses(merchant.SpriteRect), "the marks go round the larger art");
			s.GetParent().QueueFree();
		}
		// a hidden node draws nothing, so nothing of its type can show through its size
		var hidden = Make(host, RowOfThree());
		var v2 = new StubMapView();
		v2.Add("a", 1, "duel", "Apprentice"); v2.Add("b0", 2, "merchant", null); v2.Add("b1", 2, "duel", "Oni"); v2.Add("b2", 2, "dojo", null);
		foreach (var b in new[] { "b0", "b1", "b2" }) v2.Route("a", b);
		v2.Open("a");
		var s2 = Make(host, v2);
		Assert.True(s2.NodeFor("b0") == null && s2.NodeFor("b2") == null, "hidden nodes have no control to draw large");
		Assert.True(s2.RectOf("b0") == hidden.RectOf("b0") && s2.RectOf("b2") == hidden.RectOf("b2"), "and the same box whether drawn or not");
		hidden.GetParent().QueueFree(); s2.GetParent().QueueFree();
	}

	/// <summary>The boss's two extra tag lines sit inside its box at both sizes (they once hung below it).</summary>
	[Test]
	public static void BossTagSitsInsideItsBox(Node host)
	{
		var sess = Start();
		while (sess.Map.Graph.Node(sess.Reachable()[0]).Type != "boss")
			Walk(sess, sess.Reachable()[0]);
		foreach (var size in new[] { P1080, P720 })
		{
			var s = Make(host, new SessionMapView(sess), size);
			s.FinishPresentation();
			var n = s.NodeFor("boss-tanuki") ?? s.NodeFor(sess.Reachable()[0])!;
			Assert.True(n.TagText.Contains("copies your best move"), "the boss's tag has its two extra lines");
			var tag = n.Tag;
			Assert.True(tag.Position.Y + tag.Size.Y <= n.Size.Y + 0.5f, $"{size}: the tag's bottom {tag.Position.Y + tag.Size.Y} is inside the box ({n.Size.Y})");
			s.GetParent().QueueFree();
		}
	}

	/// <summary>Largest vertical distance of the path between x0 and x1 from the plain smootherstep curve joining its two ends there (what the route would be with no winding).</summary>
	static float SwingBetween(Vector2[] path, float x0, float x1)
	{
		var pts = path.Where(p => p.X >= x0 && p.X <= x1).ToArray();
		if (pts.Length < 8) return -1f;
		float ya = pts[0].Y, yb = pts[^1].Y, w = pts[^1].X - pts[0].X;
		float Smoother(float u) => u * u * u * (u * (6f * u - 15f) + 10f);
		return pts.Max(p => Mathf.Abs(p.Y - (ya + (yb - ya) * Smoother((p.X - pts[0].X) / w))));
	}

	/// <summary>
	/// The winding of a route is at least a meaningful share of its gap, measured on what <see cref="MapRouteGeometry.Compute"/> really builds: level pairs of nodes
	/// (where nothing but the meander bends the line) at both sizes, and every lone route (not part of a shared trunk) of the default map on several seeds.
	/// It fails if the meander stops using <see cref="MapRouteGeometry.Amplitude"/> (or its amplitude shrinks).
	/// </summary>
	[Test]
	public static void RealRoutesWindByAtLeastAFractionOfTheirGap(Node host)
	{
		const float Need = 0.7f * 0.07f;
		Assert.True(MapRouteGeometry.MinMeander >= 0.07f, "the pinned minimum share is not lowered");
		int checkedLevel = 0;
		foreach (var size in new[] { P1080, P720 })
		{
			float boardH = size.Y * 0.45f;
			foreach (float colW in new[] { 380f, 520f, 680f })
			{
				var nodes = new List<MapNodeView> { new("a", 1, "duel", "A", false, MapNodeState.Reachable), new("b", 2, "duel", "B", false, MapNodeState.Locked) };
				float s = boardH * MapLayout.SpriteShare;
				var box = MapNodeBox.Size(s, MapNodeShape.Standard);
				var rects = new Dictionary<string, Rect2> { ["a"] = new Rect2(colW * 0.5f - box.X / 2f, boardH / 2f - box.Y / 2f, box.X, box.Y), ["b"] = new Rect2(colW * 1.5f - box.X / 2f, boardH / 2f - box.Y / 2f, box.X, box.Y) };
				var sp = MapNodeBox.Sprite(box, s, MapNodeShape.Standard);
				var sprites = rects.ToDictionary(kv => kv.Key, kv => new Rect2(kv.Value.Position + sp.Position, sp.Size));
				var layout = new MapLayoutResult(rects, box) { Columns = new List<(int, float)> { (1, colW * 0.5f), (2, colW * 1.5f) }, ColumnWidth = colW, Sprites = sprites };
				var paths = MapRouteGeometry.Compute(nodes, new[] { new MapRouteView("a", "b") }, layout, boardH);
				float xo = rects["a"].End.X + 8f, xi = rects["b"].Position.X - 8f, gap = xi - xo;
				float swing = SwingBetween(paths[("a", "b")], xo, xi);
				Assert.True(swing >= Need * Mathf.Min(boardH, gap), $"{size} column {colW}: a level route swings {swing:0.0} px over a gap of {gap:0} (board {boardH:0}); need {Need * Mathf.Min(boardH, gap):0.0}");
				checkedLevel++;
			}
		}
		Assert.True(checkedLevel == 6, "all level pairs were measured");
		int real = 0;
		foreach (uint seed in new uint[] { 5, 6, 7, 8, 9, 10 })
			foreach (var size in new[] { P1080, P720 })
			{
				var sess = Start(seed);
				var s = Make(host, new SessionMapView(sess), size);
				var routes = sess.Map.Graph.Routes.ToList();
				foreach (var r in routes)
				{
					var mine = s.RoutePaths[(r.From, r.To)]; var mid = mine[mine.Length / 2];
					bool lone = !s.RoutePaths.Any(kv => kv.Key != (r.From, r.To) && kv.Value.Any(q => q.DistanceTo(mid) < 6f)); // its middle runs on no shared trunk
					var a = s.RectOf(r.From); var b = s.RectOf(r.To);
					float xo = a.End.X + 8f, xi = b.Position.X - 8f, gap = xi - xo;
					if (!lone || gap < 40f) continue;
					float swing = SwingBetween(s.RoutePaths[(r.From, r.To)], xo, xi);
					Assert.True(swing >= Need * Mathf.Min(s.Board.Size.Y, gap), $"seed {seed} {size}: {r.From}>{r.To} swings {swing:0.0} over a gap of {gap:0}");
					real++;
				}
				s.GetParent().QueueFree();
			}
		Assert.True(real >= 20, $"enough real routes were measured ({real})");
	}

	/// <summary>YOK-245: no mist or cloud node exists on the screen, at the start or at the boss.</summary>
	[Test]
	public static void MapScreenHasNoMistOrCloudNode(Node host)
	{
		var s = Make(host, new SessionMapView(Start()));
		Assert.True(s.FindChild("Mist", true, false) == null, "no Mist node");
		bool Cloudy(Node n) => n.Name.ToString().ToLowerInvariant().Contains("mist") || n.Name.ToString().ToLowerInvariant().Contains("cloud") || (n.GetScript().Obj is Script sc && sc.ResourcePath.ToLowerInvariant().Contains("mist"));
		bool any = false; void Walk(Node n) { if (Cloudy(n)) any = true; foreach (var c in n.GetChildren()) Walk(c); }
		Walk(s);
		Assert.True(!any, "no node or script named mist or cloud");
		s.GetParent().QueueFree();
	}

	/// <summary>YOK-245: the ink of a sketch, as the screen set its fade, falls smoothly to nothing on the board and never rises to the right; at the start and with Ryo late in the run.</summary>
	[Test]
	public static void SketchInkReachesZeroOnTheBoardAndNeverRises(Node host)
	{
		foreach (var size in new[] { P1080, P720 })
			foreach (int walked in new[] { 0, 6, 7 })
			{
				var sess = Start();
				for (int i = 0; i < walked; i++) Walk(sess, sess.Reachable()[0]);
				var s = Make(host, new SessionMapView(sess), size);
				float w = s.Board.Size.X, f = s.Routes.FadeFrom, span = s.Routes.FadeSpan;
				string tag = $"{size}, {walked} rows walked";
				Assert.True(f + span <= w - 19f, $"{tag}: the fade ends on the board ({f + span} of {w})");
				Assert.True(MapRoutes.SketchAlpha(f + span, f, span) <= 0.001f && MapRoutes.SketchAlpha(w, f, span) == 0f, $"{tag}: nothing is left at the fade's end or the board's");
				Assert.True(MapRoutes.SketchAlpha(f - 1f, f, span) >= MapRoutes.SketchMaxAlpha - 0.001f, $"{tag}: full ink before the fade");
				float last = 1f;
				for (float x = 0; x < w + 200f; x += 5f)
				{
					float a = MapRoutes.SketchAlpha(x, f, span);
					Assert.True(!float.IsNaN(a) && a <= last + 1e-6f, $"{tag}: ink rises or is NaN at x={x}");
					last = a;
				}
				s.GetParent().QueueFree();
			}
	}

	/// <summary>The scroll draws its roller columns 1.5x the texture's own width: the very slices <see cref="MapScroll"/> draws from.</summary>
	[Test]
	public static void ScrollRollersAreDrawnThickerThanTheTexture(Node host)
	{
		var tex = GD.Load<Texture2D>(MapScroll.TexturePath);
		var slices = MapScroll.Slices(new Vector2(4000, 640), new Vector2(tex.GetWidth(), tex.GetHeight()));
		var left = slices.Where(x => x.Dst.Position.X == 0f && x.Dst.Size.Y > 100f).Single();
		var right = slices.Where(x => x.Dst.End.X == 4000f && x.Dst.Size.Y > 100f).Single();
		Assert.True(left.Dst.Size.X >= 1.4f * left.Src.Size.X && right.Dst.Size.X >= 1.4f * right.Src.Size.X, $"drawn {left.Dst.Size.X} wide from {left.Src.Size.X} source px");
		Assert.True(left.Src.Size.X == 72f && left.Dst.Size.X == MapScroll.RollerWidth, "the roller is the texture's own 72 px roller column, drawn RollerWidth wide");
		Assert.True(Mathf.IsEqualApprox(slices.Sum(x => x.Dst.Size.X * x.Dst.Size.Y), 4000f * 640f), "the nine pieces tile the whole scroll");
	}

	/// <summary>
	/// YOK-240: a building drawn up to MaxArtScale (and its ring) stays inside the paper and clear of scenery, for every node of every row, whoever waits there:
	/// the layout reserves the room and the landmarks keep clear of the grown box, both by the node's size alone.
	/// </summary>
	[Test]
	public static void DrawnArtAndRingStayOnThePaperAndClearOfScenery(Node host)
	{
		var maxScale = Styles().Styles.Where(x => x != null).Max(x => x.SpriteScale);
		Assert.True(maxScale <= MapNodeBox.MaxArtScale, $"no type is drawn above the reserved MaxArtScale ({maxScale})");
		var views = new List<IMapView> { RowOfThree(), new SessionMapView(Start()) };
		var v3 = new StubMapView();
		v3.Add("a", 1, "duel", "Apprentice"); v3.Add("b0", 2, "dojo", null); v3.Add("b1", 2, "merchant", null); v3.Add("b2", 2, "duel", "Oni");
		foreach (var b in new[] { "b0", "b1", "b2" }) v3.Route("a", b);
		v3.Open("a"); v3.Select("a"); v3.Complete(); views.Add(v3);
		foreach (var view in views)
			foreach (var size in new[] { P1080, P720 })
			{
				var s = Make(host, view, size);
				var plan = s.Landmarks.Plan.ToList();
				foreach (var n in view.Nodes)
				{
					var box = s.RectOf(n.Id); float sh = s.SpriteRectOf(n.Id).Size.Y;
					var grown = MapNodeBox.Grown(box, sh);
					Assert.True(grown.Position.Y >= 0f && grown.End.Y <= s.Board.Size.Y, $"{size} {n.Id}: grown box {grown} stays on the board {s.Board.Size}");
					Assert.True(s.Board.Position.Y + grown.Position.Y >= MapScroll.TopInset + 4f, $"{size} {n.Id}: and below the paper's top edge");
					foreach (var lm in plan) Assert.True(!lm.Rect.Intersects(grown), $"{size} {n.Id}: a {lm.Kind} {lm.Rect} overlaps its grown box {grown}");
					if (s.NodeFor(n.Id) is { } node)
					{
						var ring = node.ArtRect.Grow(MapNodeBox.Pad(sh) * 0.72f);
						ring = new Rect2(ring.Position + box.Position, ring.Size);
						Assert.True(grown.Encloses(ring.Grow(-0.5f)), $"{size} {n.Id}: the drawn ring {ring} is inside the reserved {grown}");
					}
				}
				s.GetParent().QueueFree();
			}
	}
}
