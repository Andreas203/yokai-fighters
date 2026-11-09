using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-238 breaker probes: map v2. Rules R2 [AM18, AM19], Y3, T6 [AM20], the Tanuki never drawn, determinism, bounds.</summary>
public static class MapV2BreakerProbes
{
	static readonly Vector2 P1080 = new(1920, 1080), P720 = new(1280, 720);

	static MapScreen Make(Node host, IMapView view, Vector2? size = null)
	{
		var s = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		var stage = new Control { Size = size ?? P1080 };
		host.AddChild(stage);
		stage.AddChild(s);
		s.SetView(view);
		s.FinishPresentation();
		return s;
	}

	static IEnumerable<Node> Descendants(Node n)
	{
		foreach (var c in n.GetChildren()) { yield return c; foreach (var d in Descendants(c)) yield return d; }
	}

	static IEnumerable<string> Texts(Node root) => Descendants(root).SelectMany(n =>
	{
		var l = new List<string>();
		if (n is Label lb) l.Add(lb.Text);
		if (n is RichTextLabel rt) l.Add(rt.Text);
		if (n is Button b) l.Add(b.Text);
		if (n is Control c) { l.Add(c.TooltipText); l.Add(c.Name); l.Add(c.Get("accessibility_name").AsString()); l.Add(c.Get("accessibility_description").AsString()); }
		return l;
	});

	// Hidden rows carry secrets; one node per row after the first, opened at row 1.
	static StubMapView SecretMap()
	{
		var v = new StubMapView();
		v.Add("a1", 1, "duel", "Apprentice");
		v.Add("b1", 2, "duel", "ZZQSECRETNAME", false);
		v.Add("b2", 2, "zzqsecrettype", "ZZQSECRETYOKAI", true, "zzqsecretseal");
		v.Add("c1", 3, "merchant");
		v.Add("boss", 4, "boss", "The Tanuki");
		v.Route("a1", "b1").Route("a1", "b2").Route("b1", "c1").Route("b2", "c1").Route("c1", "boss");
		v.Open("a1");
		v.Legend.Add(new MapTwistLegendEntry("zzqsecretseal", "ZZQSECRETLEGEND"));
		return v;
	}

	// R2 [AM18, AM19]: nothing of a hidden node leaks into any text, name, tooltip or accessibility string.
	[Test]
	public static void HiddenNodesLeakNoText(Node host)
	{
		var v = SecretMap();
		var s = Make(host, v);
		Assert.True(s.NodeFor("b1") == null && s.NodeFor("b2") == null && s.NodeFor("boss") == null, "hidden nodes have no control");
		var all = string.Join(" | ", Texts(s)).ToLowerInvariant();
		foreach (var secret in new[] { "zzqsecretname", "zzqsecrettype", "zzqsecretyokai", "tanuki" })
			Assert.True(!all.Contains(secret), "the hidden node text " + secret + " appears on the map: " + all);
		s.GetParent().QueueFree();
	}

	// R2: the cursor and Move() never land on a hidden node along a whole walk; only revealed nodes have controls.
	[Test]
	public static void CursorNeverReachesHiddenNodeAlongAWalk(Node host)
	{
		var v = StubMapView.Grid(6, 3);
		var s = Make(host, v);
		for (int step = 0; step < 5; step++)
		{
			var vis = v.Nodes.Where(n => MapReveal.IsRevealed(n.State)).Select(n => n.Id).ToHashSet();
			Assert.True(s.ShownIds.All(vis.Contains), "step " + step + ": only revealed nodes have controls");
			for (int k = 0; k < 7; k++) { s.Move(1); Assert.True(s.CursorId == null || vis.Contains(s.CursorId), "cursor on hidden node " + s.CursorId); }
			var pick = v.Nodes.First(n => n.State == MapNodeState.Reachable).Id;
			v.Select(pick); v.Complete(); s.Refresh(); s.FinishPresentation();
		}
		s.GetParent().QueueFree();
	}

	// AM19: mist lines = exactly the routes whose far end is hidden; none once the boss is the next choice.
	[Test]
	public static void MistLinesOnlyForHiddenFarEndsAndNoneAtTheBoss(Node host)
	{
		var v = StubMapView.Grid(4, 2); // rows: 1 / 2 / 2 / boss
		var s = Make(host, v);
		for (int i = 0; i < 3; i++)
		{
			int expected = v.Routes.Count(r => v.Nodes.First(n => n.Id == r.To).State == MapNodeState.Locked);
			Assert.Equal(expected, s.Routes.MistLines.Count, "step " + i + ": mist lines vs routes into hidden nodes");
			var next = v.Nodes.FirstOrDefault(n => n.State == MapNodeState.Reachable);
			if (next == null) break;
			v.Select(next.Id); v.Complete(); s.Refresh(); s.FinishPresentation();
		}
		s.GetParent().QueueFree();
	}

	// Y3: every drawn elder shows its twist seal, whatever state it is in.
	[Test]
	public static void EveryDrawnElderShowsItsSeal(Node host)
	{
		var v = new StubMapView();
		v.Add("a", 1, "elder", "Elder Oni", true, "t-one").Add("b", 1, "elder", "Elder Kappa", true, "t-two").Add("c", 1, "duel", "Oni");
		v.Open("a", "b", "c");
		var s = Make(host, v);
		Assert.True(s.NodeFor("a")!.ElderShown && s.NodeFor("b")!.ElderShown, "reachable elders show the seal");
		Assert.True(!s.NodeFor("c")!.ElderShown, "a plain duel has no seal");
		v.Select("a"); s.Refresh(); s.FinishPresentation();
		Assert.True(s.NodeFor("a")!.ElderShown, "the current elder keeps its seal");
		s.GetParent().QueueFree();
	}

	// Tanuki never drawn: no mask lookup resolves for any spelling of the Tanuki.
	[Test]
	public static void NoMaskForAnyTanukiSpelling()
	{
		var st = GD.Load<MapNodeStyles>("res://ui/map_node_styles.tres");
		foreach (var id in new string?[] { "tanuki", "Tanuki", "TANUKI", "the-tanuki", "The Tanuki", "THE TANUKI", " tanuki ", "boss-tanuki", "", null })
		{
			Assert.True(st.MaskFor(id, null) == null && st.MaskFor(null, id) == null, "a mask resolves for '" + id + "'");
			var look = st.Look(new MapNodeView("x", 9, "duel", id, false, MapNodeState.Reachable) { YokaiId = id });
			Assert.True(look.Sprite != null && !look.Sprite.ResourcePath.ToLowerInvariant().Contains("tanuki"), "a Tanuki-like sprite for a duel node named '" + id + "'");
		}
		var boss = st.Look(new MapNodeView("boss-tanuki", 9, "boss", "The Tanuki", false, MapNodeState.Reachable) { YokaiId = "tanuki" });
		Assert.True(boss.Sprite != null && boss.Sprite.ResourcePath.EndsWith("node-tanuki.png"), "the boss is the tea house");
	}

	static void CheckGeometry(MapLayoutResult l, Vector2 board, string where)
	{
		var ids = l.Rects.Keys.ToList();
		foreach (var id in ids)
		{
			var r = l.Rects[id];
			Assert.True(r.Position.X >= -0.01f && r.Position.Y >= -0.01f && r.End.X <= board.X + 0.5f && r.End.Y <= board.Y + 0.5f, where + ": " + id + " off the paper " + r + " in " + board);
			Assert.True(r.Encloses(l.Sprites[id]), where + ": " + id + " sprite outside its box");
		}
		for (int i = 0; i < ids.Count; i++)
			for (int j = i + 1; j < ids.Count; j++)
				Assert.True(!l.Rects[ids[i]].Intersects(l.Rects[ids[j]]), where + ": " + ids[i] + " overlaps " + ids[j] + " " + l.Rects[ids[i]] + " " + l.Rects[ids[j]]);
	}

	static List<MapNodeView> Nodes(params int[] perRow)
	{
		var l = new List<MapNodeView>();
		for (int r = 0; r < perRow.Length; r++)
			for (int i = 0; i < perRow[r]; i++)
				l.Add(new MapNodeView("n" + r + "-" + i, r + 1, i % 2 == 0 ? "duel" : "merchant", "Y", false, MapNodeState.Reachable));
		return l;
	}

	// Bounds and overlaps on unusual maps at several board sizes (nothing off the paper, no overlaps).
	[Test]
	public static void LayoutHoldsOnUnusualMaps()
	{
		var maps = new Dictionary<string, List<MapNodeView>>
		{
			["one row, one node"] = Nodes(1), ["one row, 7 nodes"] = Nodes(7), ["one row, 12 nodes"] = Nodes(12),
			["1/5/1"] = Nodes(1, 5, 1), ["3x8"] = Nodes(3, 3, 3, 3, 3, 3, 3, 3), ["wide 8,1,8"] = Nodes(8, 1, 8),
		};
		foreach (var board in new[] { new Vector2(2600, 700), new Vector2(1700, 480) })
			foreach (var (name, nodes) in maps)
				CheckGeometry(MapLayout.Compute(nodes, board), board, name + " @" + board);
	}

	// Determinism: a State change only must not move anything; same input twice is identical.
	[Test]
	public static void LayoutIgnoresStateAndIsRepeatable()
	{
		var a = Nodes(1, 3, 3, 2, 1);
		var b = a.Select(n => n with { State = MapNodeState.Locked }).ToList();
		var board = new Vector2(2000, 560);
		var l1 = MapLayout.Compute(a, board); var l2 = MapLayout.Compute(b, board); var l3 = MapLayout.Compute(a, board);
		foreach (var n in a)
		{
			Assert.True(l1.Rects[n.Id] == l2.Rects[n.Id], n.Id + " moved when its state changed");
			Assert.True(l1.Rects[n.Id] == l3.Rects[n.Id], n.Id + " moved between identical calls");
		}
		var lm1 = MapLandmarkPlan.Compute(board, l1.Rects.Values); var lm2 = MapLandmarkPlan.Compute(board, l3.Rects.Values);
		Assert.True(lm1.Count == lm2.Count && lm1.Zip(lm2).All(p => p.First == p.Second), "landmarks differ between identical calls");
	}

	// Landmarks never sit on a node box.
	[Test]
	public static void LandmarksNeverOnANodeBox()
	{
		foreach (var perRow in new[] { new[] { 1, 3, 3, 1 }, new[] { 6, 6, 6 }, new[] { 12 } })
		{
			var board = new Vector2(1900, 560);
			var l = MapLayout.Compute(Nodes(perRow), board);
			foreach (var lm in MapLandmarkPlan.Compute(board, l.Rects.Values))
				foreach (var kv in l.Rects)
					Assert.True(!lm.Rect.Intersects(kv.Value), lm.Kind + " " + lm.Rect + " sits on " + kv.Key + " " + kv.Value);
		}
	}

	// Screen level: layout does not move when a node is revealed by walking; tags stay on the board; both resolutions.
	[Test]
	public static void ScreenLayoutStableWhileWalkingAt1080And720(Node host)
	{
		foreach (var size in new[] { P1080, P720 })
		{
			var v = StubMapView.Grid(6, 3);
			var s = Make(host, v, size);
			var before = v.Nodes.ToDictionary(n => n.Id, n => s.RectOf(n.Id));
			for (int i = 0; i < 5; i++)
			{
				v.Select(v.Nodes.First(n => n.State == MapNodeState.Reachable).Id); v.Complete(); s.Refresh(); s.FinishPresentation();
				foreach (var n in v.Nodes) Assert.True(s.RectOf(n.Id) == before[n.Id], size + " step " + i + ": " + n.Id + " moved");
			}
			foreach (var n in s.Nodes)
			{
				var tag = new Rect2(n.Position + n.Tag.Position, n.Tag.Size);
				Assert.True(tag.End.Y <= s.Board.Size.Y + 1f && tag.End.X <= s.Board.Size.X + 24f, size + ": tag of " + n.NodeId + " off the board " + tag + " in " + s.Board.Size);
			}
			s.GetParent().QueueFree();
		}
	}

	// T6 [AM20]: the map view carries no copy target at all, so nothing about the copy on the map.
	[Test]
	public static void LockedCopyTargetLeavesNoCopyTextOnTheMap(Node host)
	{
		var v = StubMapView.Grid(4, 2);
		var s = Make(host, v);
		var all = string.Join(" | ", Texts(s)).ToUpperInvariant();
		Assert.True(!all.Contains("COPY TARGET") && !all.Contains("LOCKED COPY") && !all.Contains("COPYING"), "copy text on the map: " + all);
		s.GetParent().QueueFree();
	}

	// AM19: with a real run walked to the row before the boss, the boss is the next choice and no mist line remains (rows left are PassedOver).
	[Test]
	public static void RealRunNoMistOnceBossIsNextChoice(Node host)
	{
		string md = ContentPaths.Data("map");
		var sess = RunSession.Start(YokaiFighters.Fight.FightScene.LoadAbilityPool(), FileMapSource.Default(md).Load(), NodeSceneTable.Default(md), 5);
		var view = new SessionMapView(sess);
		var s = Make(host, view);
		int guard = 0;
		while (!view.Nodes.Any(n => n.Type == "boss" && n.State == MapNodeState.Reachable) && guard++ < 20)
		{
			var pick = view.Nodes.First(n => n.State == MapNodeState.Reachable);
			sess.Enter(pick.Id);
			if (sess.Node!.Type is not ("merchant" or "dojo"))
			{
				var d = sess.WinFight(840);
				if (d != null) { var c = d.Offer.Cards[0]; d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null); }
			}
			sess.CompleteNode();
			s.SetView(new SessionMapView(sess)); s.FinishPresentation();
			view = new SessionMapView(sess);
		}
		Assert.True(view.Nodes.Any(n => n.Type == "boss" && n.State == MapNodeState.Reachable), "setup: boss reachable");
		Assert.Equal(0, s.Routes.MistLines.Count, "the boss is the next choice: no mist lines");
		s.GetParent().QueueFree();
	}
}
