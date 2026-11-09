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
/// real routes ahead are sketched in mist with no node control anywhere past the reveal (AM19); the paper is the title scroll.
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
	public static void LandmarksAreDeterministicAndNeverOnANodeOrItsTag(Node host)
	{
		foreach (var size in new[] { P1080, P720 })
		{
			var s = Make(host, new SessionMapView(Start()), size);
			var plan = s.Landmarks.Plan;
			Assert.True(plan.Count >= 8, $"{size}: the paper is dressed with scenery ({plan.Count})");
			var kinds = plan.Select(l => l.Kind).ToHashSet();
			Assert.True(kinds.Contains(MapLandmarkKind.PineTall) && kinds.Contains(MapLandmarkKind.PinesWide) && kinds.Contains(MapLandmarkKind.Rocks) && kinds.Contains(MapLandmarkKind.Bridge) && kinds.Contains(MapLandmarkKind.Torii), $"{size}: pines, rocks, a footbridge and a torii all appear: " + string.Join(",", plan.Select(l => l.Kind)));
			var again = MapLandmarkPlan.Compute(s.Board.Size, s.View!.Nodes.Select(n => s.RectOf(n.Id)), MapNodeBox.LineHeight(s.Board.Size.Y * MapLayout.SpriteShare));
			Assert.True(again.Select(l => (l.Kind, l.Rect)).SequenceEqual(plan.Select(l => (l.Kind, l.Rect))), $"{size}: the same board gives the same scenery");
			var board = new Rect2(Vector2.Zero, s.Board.Size);
			foreach (var l in plan)
			{
				Assert.True(board.Encloses(l.Rect), $"{size}: {l.Kind} at {l.Rect} stays on the board");
				foreach (var n in s.View.Nodes)
					Assert.True(!l.Rect.Intersects(s.RectOf(n.Id)), $"{size}: {l.Kind} at {l.Rect} overlaps node {n.Id} at {s.RectOf(n.Id)}");
			}
			s.GetParent().QueueFree();
		}
	}

	// ---- routes and mist -----------------------------------------------------------------------------------------------------------

	[Test]
	public static void StrokesWindEvenBetweenNodesAtTheSameHeight()
	{
		var pts = MapRoutes.Path(new Vector2(0, 200), new Vector2(400, 200), 48, out var normals);
		Assert.True(pts[0] == new Vector2(0, 200) && pts[^1].DistanceTo(new Vector2(400, 200)) < 0.01f, "the path starts and ends on its anchors");
		float swing = pts.Max(p => System.Math.Abs(p.Y - 200f));
		Assert.True(swing > 8f, $"a level pair of nodes is still joined by an S, swing {swing:F1} px");
		Assert.True(pts.Zip(pts.Skip(1)).All(p => p.Second.X >= p.First.X - 1f), "and it never doubles back along x");
		Assert.True(MapRoutes.Path(new Vector2(0, 200), new Vector2(400, 200), 48, out _).Zip(pts).All(p => p.First == p.Second), "deterministic");
	}

	[Test]
	public static void MistSketchesTheRealRoutesAheadAndDrawsNoNode(Node host)
	{
		var sess = Start();
		var s = Make(host, new SessionMapView(sess));
		var all = sess.Map.Graph.Routes.Count;
		Assert.Equal(all, s.Routes.MistLines.Count, "at the start every route of the map is ahead and is sketched, boss included");
		Assert.True(s.Mist.Active && s.Mist.Visible, "with mist");
		Assert.Equal(0, s.Mist.GetChildCount(), "the mist is only a drawing: no control in it");
		Assert.Equal("r1-apprentice", string.Join(",", s.Nodes.Select(n => n.NodeId)), "and still only the first node is a control");
		Assert.True(s.Mist.From >= s.RectOf("r1-apprentice").End.X - 1f, "the mist starts at the last drawn node");

		Walk(sess, "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant");
		s.SetView(new SessionMapView(sess));
		var view = new SessionMapView(sess);
		var state = view.Nodes.ToDictionary(n => n.Id, n => n.State);
		int ahead = view.Routes.Count(r => state[r.To] == MapNodeState.Locked && state[r.From] != MapNodeState.PassedOver);
		Assert.Equal(14, ahead, "(the rows 5-6, 6-7, 7-8 and 8-boss routes)");
		Assert.Equal(ahead, s.Routes.MistLines.Count, "only routes whose far end is not drawn are sketched: not the brushed ones, not those of rows Ryo left");
		var revealed = state.Where(kv => MapReveal.IsRevealed(kv.Value)).Select(kv => kv.Key).ToHashSet();
		Assert.True(s.Nodes.All(n => revealed.Contains(n.NodeId)), "no control for any node not revealed");
		foreach (var r in view.Routes.Where(r => state[r.To] == MapNodeState.Locked && state[r.From] != MapNodeState.PassedOver))
		{
			var c = s.SpriteRectOf(r.To).GetCenter();
			Assert.True(s.Routes.MistLines.Any(l => l.B.DistanceTo(c) < 0.5f), $"the sketch of {r.From} -> {r.To} runs through where {r.To} will stand");
		}
		Assert.True(s.Routes.MistLines.All(l => l.A.X < l.B.X), "sketches run forward too");
		var text = string.Join("\n", AllText(s)).ToLowerInvariant();
		foreach (var hidden in new[] { "tanuki", "kitsune", "dojo", "boss", "elder kappa" })
			Assert.True(!text.Contains(hidden), $"no text names '{hidden}' while its row is in the mist");
		s.GetParent().QueueFree();
	}

	static IEnumerable<string> AllText(Node n)
	{
		if (n is Godot.Label l) yield return l.Text;
		foreach (var c in n.GetChildren()) foreach (var t in AllText(c)) yield return t;
	}

	[Test]
	public static void NoMistOnceTheBossIsTheChoice(Node host)
	{
		var sess = Start();
		while (sess.Map.Graph.Node(sess.Reachable()[0]).Type != "boss") Walk(sess, sess.Reachable()[0]);
		var s = Make(host, new SessionMapView(sess));
		Assert.Equal(0, s.Routes.MistLines.Count, "nothing is ahead of the boss");
		Assert.True(!s.Mist.Active, "so no mist");
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
}
