using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-78: map screen against the stub view (every node and route, reachable-only selection, one signal per confirm, any size) and the RunMap adapter.</summary>
public static class MapScreenTests
{
	static MapScreen Make(Node host, IMapView view, bool viewFirst = false)
	{
		var s = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		var stage = new Control { Size = new Vector2(1920, 1080) }; // the screen's real canvas, so its anchors resolve to 1920x1080
		host.AddChild(stage);
		if (viewFirst) s.SetView(view);
		stage.AddChild(s);
		if (!viewFirst) s.SetView(view);
		return s;
	}

	static void Press(MapScreen s, string action) => s._UnhandledInput(new InputEventAction { Action = action, Pressed = true });

	static StubMapView Fork()
	{
		var v = new StubMapView()
			.Add("a", 1, "duel", "Kitsune").Add("b", 2, "duel", "Oni").Add("c", 2, "elder", "Elder Kappa", true).Add("d", 2, "shrine", null).Add("e", 3, "boss", "Tanuki")
			.Route("a", "b").Route("a", "c").Route("a", "d").Route("b", "e").Route("c", "e").Route("d", "e");
		return v.Open("a");
	}

	[Test]
	public static void StubRendersEveryNodeAndRoute(Node host)
	{
		var v = Fork();
		var s = Make(host, v);
		Assert.Equal(5, s.Nodes.Count, "a node per stub node");
		Assert.Equal(6, s.Routes.Lines.Count, "a line per stub route");
		Assert.Equal("Duel", s.NodeFor("a")!.TypeText, "type label from the styles resource");
		Assert.Equal("Kitsune", s.NodeFor("a")!.YokaiText, "the yokai that waits (R2)");
		Assert.True(s.NodeFor("c")!.ElderShown && !s.NodeFor("b")!.ElderShown, "elder marker only on the elder node (Y3)");
		Assert.Equal("Shrine", s.NodeFor("d")!.TypeText, "unknown type still renders, with its id as label");
		Assert.Equal("", s.NodeFor("d")!.YokaiText, "no yokai, no name");
		Assert.True(s.NodeFor("a")!.State == MapNodeState.Reachable && s.NodeFor("e")!.State == MapNodeState.Locked, "states shown");
		foreach (var (a, b, _) in s.Routes.Lines) Assert.True(a.X < b.X, "routes run forward");
		var r = s.RectOf("a");
		var line = s.Routes.Lines.First();
		Assert.True(Mathf.IsEqualApprox(line.A.X, r.End.X) && Mathf.IsEqualApprox(line.A.Y, r.GetCenter().Y), "route starts on its node's edge");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void ViewCanBeHandedInBeforeTheScreenEntersTheTree(Node host)
	{
		var s = Make(host, Fork(), viewFirst: true);
		Assert.Equal(5, s.Nodes.Count, "nodes built when the view came first");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void SelectionOnlyAmongReachableAndConfirmFiresOnce(Node host)
	{
		var v = Fork();
		var s = Make(host, v);
		var fired = new List<string>();
		s.NodeChosen += id => fired.Add(id);
		Assert.True(s.NodeFor("b")!.Disabled && !s.NodeFor("a")!.Disabled, "locked node button disabled");
		s.NodeFor("e")!.EmitSignal(BaseButton.SignalName.Pressed); // even a forced press on a locked node is refused
		Assert.Equal(0, fired.Count, "locked node cannot be chosen");
		Press(s, "ui_accept"); Press(s, "ui_accept");
		Assert.Equal("a", string.Join(",", fired), "confirm fires once, then locks");
		Assert.True(s.Locked && s.NodeFor("a")!.State == MapNodeState.Current, "screen re-read the state after the pick");
		Assert.Equal(1, v.Selected.Count, "the view saw one Select");
		v.Complete();
		s.Refresh();
		Assert.Equal("b,c,d", string.Join(",", s.ReachableIds), "next row opens");
		Press(s, "ui_down"); Press(s, "ui_down");
		Assert.Equal("d", s.CursorId, "cursor steps through reachable nodes only");
		Press(s, "ui_down");
		Assert.Equal("b", s.CursorId, "cursor wraps");
		s.NodeFor("c")!.EmitSignal(BaseButton.SignalName.Pressed); // mouse click on a reachable node
		Assert.Equal("a,c", string.Join(",", fired), "click picks that node");
		Assert.True(!s.Confirm(), "locked after a pick");
		s.NodeFor("c")!.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Equal(2, fired.Count, "a second click is ignored");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void ViewRefusalRaisesNothing(Node host)
	{
		var v = Fork();
		var s = Make(host, v);
		int n = 0; s.NodeChosen += _ => n++;
		v.Select("a"); // view moved on without the screen: the cursor node is no longer selectable
		Assert.True(!s.Confirm(), "view refused");
		Assert.Equal(0, n, "no signal on refusal");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void ThreeAndTwelveRowsLayOutInsideTheBoard(Node host)
	{
		foreach (var (rows, per) in new[] { (3, 2), (12, 3), (12, 5) })
		{
			var s = Make(host, StubMapView.Grid(rows, per));
			var rects = s.Nodes.Select(n => s.RectOf(n.NodeId)).ToList();
			var board = new Rect2(Vector2.Zero, s.Board.Size);
			Assert.True(board.Size.X > 1000, "board has a real size");
			Assert.True(rects.All(r => board.Encloses(r)), $"{rows}x{per}: every node inside the board");
			for (int i = 0; i < rects.Count; i++)
				for (int j = i + 1; j < rects.Count; j++)
					Assert.True(!rects[i].Intersects(rects[j]), $"{rows}x{per}: nodes {i} and {j} overlap");
			Assert.Equal(s.View!.Nodes.Count, s.Nodes.Count, $"{rows}x{per}: all nodes shown");
			Assert.Equal(s.View.Routes.Count, s.Routes.Lines.Count, $"{rows}x{per}: all routes drawn");
			Assert.Equal(rows, rects.Select(r => (int)r.Position.X).Distinct().Count(), $"{rows}x{per}: one column per row");
			s.GetParent().QueueFree();
		}
	}

	[Test]
	public static void LayoutIsDrivenByRowValuesNotCounts()
	{
		var nodes = new List<MapNodeView> { new("x", 10, "duel", null, false, MapNodeState.Locked), new("y", 40, "duel", null, false, MapNodeState.Locked), new("z", 40, "duel", null, false, MapNodeState.Locked) };
		var l = MapLayout.Compute(nodes, new Vector2(1000, 600));
		Assert.True(l.Rects["x"].Position.X < l.Rects["y"].Position.X && Mathf.IsEqualApprox(l.Rects["y"].Position.X, l.Rects["z"].Position.X), "sparse row values still make adjacent columns");
		Assert.True(l.Rects["y"].Position.Y < l.Rects["z"].Position.Y, "order within the row is the view's order");
		Assert.Equal(0, MapLayout.Compute(new List<MapNodeView>(), new Vector2(100, 100)).Rects.Count, "empty map is empty");
	}

	[Test]
	public static void RunMapAdapterShowsTheDefaultMap(Node host)
	{
		var graph = FileMapSource.Default(ContentPaths.Data("map")).Load();
		var run = new RunMap(graph);
		var view = new RunMapView(run);
		Assert.Equal(graph.Nodes.Count, view.Nodes.Count, "a node per graph node");
		Assert.Equal(graph.Routes.Count, view.Routes.Count, "a route per graph route");
		Assert.True(view.Nodes.Where(n => n.State == MapNodeState.Reachable).Select(n => n.Id).SequenceEqual(graph.StartNodes), "start nodes reachable first");
		Assert.True(view.Nodes.Where(n => n.State != MapNodeState.Reachable).All(n => n.State == MapNodeState.Locked), "everything else locked");
		Assert.True(!view.Select("boss-tanuki") && run.Current is null, "unreachable node refused, run untouched");

		var s = Make(host, view);
		Assert.Equal(graph.Nodes.Count, s.Nodes.Count, "screen shows the whole default map");
		Assert.True(s.Nodes.All(n => n.Size.X > 0), "nodes sized");
		Assert.Equal("Apprentice", s.NodeFor("r1-apprentice")!.YokaiText, "yokai name from the map data");
		Assert.True(s.NodeFor("r5-elder-oni")!.ElderShown, "elder flag from the map data");
		string? chosen = null; s.NodeChosen += id => chosen = id;
		Press(s, "ui_accept");
		Assert.Equal("r1-apprentice", chosen, "pick reaches the signal");
		Assert.Equal("r1-apprentice", run.Current, "RunMap entered the node");
		Assert.True(view.Nodes.All(n => n.State != MapNodeState.Reachable) && view.Nodes.First(n => n.Id == chosen).State == MapNodeState.Current, "nothing reachable while in the node; it is Current");
		run.Complete(chosen!);
		s.Refresh();
		Assert.Equal("r2-kitsune,r2-oni,r2-kappa", string.Join(",", s.ReachableIds), "routes out of the completed node open");
		Assert.Equal(MapNodeState.Current, view.Nodes.First(n => n.Id == chosen).State, "last node stays Current");
		Press(s, "ui_down"); Press(s, "ui_accept");
		Assert.Equal("r2-oni", chosen, "second pick");
		Assert.True(view.Nodes.First(n => n.Id == "r1-apprentice").State == MapNodeState.Visited, "earlier node Visited");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void NodeStylesAreALookupWithAFallback()
	{
		var st = GD.Load<MapNodeStyles>("res://ui/map_node_styles.tres");
		Assert.Equal("Elder duel", st.Look("elder").Label, "known type");
		Assert.Equal("Merchant", st.Look("merchant").Label, "placeholder type has a look too");
		Assert.Equal("Rest stop", st.Look("rest-stop").Label, "unknown type falls back to its id");
	}
}
