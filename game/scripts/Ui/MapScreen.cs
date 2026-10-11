using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Ui;

/// <summary>
/// Map screen (YOK-78, GDD fig. 6; R1 one pick per row, R2 the yokai at each node, Y3 elder marker). Deliberately plain: it reads an
/// <see cref="IMapView"/>, lays the nodes out from graph data (<see cref="MapLayout"/>), draws the routes, and raises
/// <see cref="NodeChosen"/> once when a reachable node is confirmed. No map rules and no node types in code; looks are
/// <c>ui/map_node_styles.tres</c>. Layout is <c>scenes/ui/map_screen.tscn</c>, nodes are <c>map_node.tscn</c>.
///
/// Flow: instantiate the scene, add it to the tree, <see cref="SetView"/> (before or after entering the tree), connect <see cref="NodeChosen"/>.
/// After the node has run and the map moved on, call <see cref="Refresh"/> (same view) or <see cref="SetView"/> (new view) to show the new
/// state and accept a pick again.
/// Input: arrows / d-pad / stick (ui_* actions) step through the reachable nodes, Enter / Space / pad A confirm, mouse hover moves the
/// cursor and click confirms. Only reachable nodes are ever on the cursor or clickable.
/// </summary>
public partial class MapScreen : Control
{
	[Signal] public delegate void NodeChosenEventHandler(string nodeId);

	[Export] public MapNodeStyles? Styles { get; set; }
	[Export] public PackedScene? NodeScene { get; set; }

	public IMapView? View { get; private set; }
	public string? CursorId { get; private set; }
	/// <summary>True after a pick went through, until <see cref="Refresh"/> / <see cref="SetView"/>: Confirm does nothing meanwhile.</summary>
	public bool Locked { get; private set; }
	public IReadOnlyList<string> ReachableIds { get; private set; } = new List<string>();
	public Control Board => _board ??= UiFind.Get<Control>(this, "Board");
	public MapRoutes Routes => _routes ??= UiFind.Get<MapRoutes>(this, "Routes");

	readonly Dictionary<string, MapNode> _nodes = new();
	Control? _board; MapRoutes? _routes;
	MapLayoutResult _layout = new(new Dictionary<string, Rect2>(), Vector2.Zero);
	bool _ready;

	public override void _Ready()
	{
		MenuInput.EnsurePadConfirm();
		Styles ??= GD.Load<MapNodeStyles>("res://ui/map_node_styles.tres");
		NodeScene ??= GD.Load<PackedScene>("res://scenes/ui/map_node.tscn");
		UiFind.Get<KeyChipFooter>(this, "Hint").SetHints(new KeyHint("choose", "Choose"), new KeyHint("confirm", "Go"));
		Board.Resized += () => { if (View != null) Rebuild(); };
		_ready = true;
		if (View != null) Rebuild();
	}

	/// <summary>Show this map. Safe before or after the screen enters the tree. Clears the pick lock.</summary>
	public void SetView(IMapView view)
	{
		View = view; CursorId = null; Locked = false;
		if (_ready) Rebuild();
	}

	/// <summary>Re-read node states from the view (after the run moved on) and accept a pick again.</summary>
	public void Refresh() { Locked = false; Update(); }

	public MapNode? NodeFor(string id) => _nodes.GetValueOrDefault(id);
	public IReadOnlyCollection<MapNode> Nodes => _nodes.Values;
	public Rect2 RectOf(string id) => _layout.Rects[id];

	void Rebuild()
	{
		foreach (var n in _nodes.Values) { Board.RemoveChild(n); n.QueueFree(); }
		_nodes.Clear();
		var nodes = View!.Nodes;
		_layout = MapLayout.Compute(nodes, Board.Size);
		foreach (var v in nodes)
		{
			var n = NodeScene!.Instantiate<MapNode>();
			Board.AddChild(n);
			n.Chosen += OnNodeChosen; n.Hovered += OnNodeHovered;
			_nodes[v.Id] = n;
		}
		Update();
	}

	void Update()
	{
		if (View == null || !_ready) return;
		var nodes = View.Nodes;
		ReachableIds = nodes.Where(n => n.State == MapNodeState.Reachable).Select(n => n.Id).ToList();
		if (CursorId == null || !ReachableIds.Contains(CursorId)) CursorId = ReachableIds.FirstOrDefault();
		foreach (var v in nodes)
		{
			if (!_nodes.TryGetValue(v.Id, out var n)) continue;
			var r = _layout.Rects[v.Id];
			n.Position = r.Position;
			n.Bind(v, Styles!.Look(v.Type), r.Size, v.Id == CursorId && !Locked);
		}
		var state = nodes.ToDictionary(n => n.Id, n => n.State);
		bool Done(string id) => state[id] is MapNodeState.Visited or MapNodeState.Current;
		Routes.SetLines(View.Routes.Where(r => _layout.Rects.ContainsKey(r.From) && _layout.Rects.ContainsKey(r.To)).Select(r =>
		{
			var a = _layout.Rects[r.From]; var b = _layout.Rects[r.To];
			var kind = Done(r.From) && Done(r.To) ? RouteKind.Walked : Done(r.From) && state[r.To] == MapNodeState.Reachable ? RouteKind.Open : RouteKind.Idle;
			return (new Vector2(a.End.X, a.GetCenter().Y), new Vector2(b.Position.X, b.GetCenter().Y), kind);
		}));
	}

	/// <summary>Step the cursor through the reachable nodes (wraps). Does nothing when none are reachable or a pick already went through.</summary>
	public void Move(int dir)
	{
		if (Locked || ReachableIds.Count == 0) return;
		int i = CursorId == null ? 0 : ReachableIds.ToList().IndexOf(CursorId);
		CursorId = ReachableIds[((i + dir) % ReachableIds.Count + ReachableIds.Count) % ReachableIds.Count];
		Update();
	}

	/// <summary>Pick the cursor node. Returns true when the view accepted it (then <see cref="NodeChosen"/> fires, once).</summary>
	public bool Confirm()
	{
		if (Locked || View == null || CursorId == null || !ReachableIds.Contains(CursorId)) return false;
		string id = CursorId;
		if (!View.Select(id)) return false;
		Locked = true;
		Update();
		EmitSignal(SignalName.NodeChosen, id);
		return true;
	}

	void OnNodeHovered(MapNode n)
	{
		if (Locked || n.State != MapNodeState.Reachable || CursorId == n.NodeId) return;
		CursorId = n.NodeId; Update();
	}

	void OnNodeChosen(MapNode n)
	{
		if (Locked || n.State != MapNodeState.Reachable) return;
		CursorId = n.NodeId;
		Confirm();
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (!IsVisibleInTree() || View == null) return;
		if (e.IsActionPressed("ui_up") || e.IsActionPressed("ui_left")) Move(-1);
		else if (e.IsActionPressed("ui_down") || e.IsActionPressed("ui_right")) Move(1);
		else if (e.IsActionPressed("ui_accept")) Confirm();
		else return;
		GetViewport().SetInputAsHandled();
	}
}
