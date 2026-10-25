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

	/// <summary>YOK-108, T6: the copy-target chip. Same control, icon and glow as the HUD talisman (<see cref="CopyTalisman"/>), laid out as a strip.</summary>
	public CopyTalisman CopyChip => UiFind.Get<CopyTalisman>(this, "CopyChip");
	/// <summary>The node whose detail panel shows: the cursor, else the node Ryo is on.</summary>
	public string? FocusId => CursorId ?? View?.Nodes.FirstOrDefault(n => n.State == MapNodeState.Current)?.Id;
	/// <summary>Row header texts left to right ("ROW 5 · ELDER", "AFTER ROW 8").</summary>
	public IReadOnlyList<string> HeaderTexts => _headers.Select(l => l.Text).ToList();
	public string StatusHealthText => UiFind.Get<Label>(this, "HealthText").Text;
	public string StatusCoinsText => UiFind.Get<Label>(this, "CoinsText").Text;
	public string StatusRowsText => UiFind.Get<Label>(this, "RowsText").Text;
	public string DetailNameText => UiFind.Get<Label>(this, "DetailName").Text;
	public string DetailRewardText => UiFind.Get<Label>(this, "DetailReward").Text;
	/// <summary>The twist line's slot (Y3, YOK-90): hidden while the view gives no twist text.</summary>
	public Label DetailTwist => UiFind.Get<Label>(this, "DetailTwist");
	/// <summary>YOK-109: the habit line ("Habit to punish: ..."), hidden while the view gives no habit for the focused node.</summary>
	public Label DetailHabit => UiFind.Get<Label>(this, "DetailHabit");
	/// <summary>YOK-109: the big twist seal beside the focus panel, shown for a focused elder that has a twist.</summary>
	public TwistSeal DetailSeal => UiFind.Get<TwistSeal>(this, "DetailSeal");
	/// <summary>YOK-109: the legend's twist rows: (icon id, label text), in order.</summary>
	public IReadOnlyList<(string Icon, string Text)> LegendTwists => UiFind.Get<Control>(this, "LegendTwists").GetChildren()
		.OfType<HBoxContainer>().Select(r => (r.GetNode<TwistSeal>("Seal").Icon, r.GetNode<Label>("Text").Text)).ToList();
	public string LegendTypesText => UiFind.Get<Label>(this, "LegendTypes").Text;
	public int LegendStateCount => UiFind.Get<Control>(this, "LegendStates").GetChildCount();

	static readonly Color ElderHeader = new(0.96f, 0.5f, 0.42f);
	readonly Dictionary<string, MapNode> _nodes = new();
	readonly List<Label> _headers = new();
	bool _legendBuilt;
	object? _runKey;
	Control? _board; MapRoutes? _routes;
	MapLayoutResult _layout = new(new Dictionary<string, Rect2>(), Vector2.Zero);
	bool _ready;

	public override void _Ready()
	{
		MenuInput.EnsurePadConfirm();
		Styles ??= GD.Load<MapNodeStyles>("res://ui/map_node_styles.tres");
		NodeScene ??= GD.Load<PackedScene>("res://scenes/ui/map_node.tscn");
		// Routes in a row stack top to bottom, so up/down (and left/right) all step the cursor: the chip says "arrows", not LEFT / RIGHT.
		// YOK-109: both devices on each chip, as in the concept. No Esc / Start chip: the map has no pause binding (only fights pause).
		UiFind.Get<KeyChipFooter>(this, "Hint").SetHints(new KeyHint("route_any", "Choose route"), new KeyHint("confirm_any", "Go"));
		Board.Resized += () => { if (View != null) Rebuild(); };
		_ready = true;
		if (View != null) Rebuild();
	}

	/// <summary>Show this map. Safe before or after the screen enters the tree. Clears the pick lock.</summary>
	public void SetView(IMapView view)
	{
		bool newRun = view.RunKey is not null && !ReferenceEquals(view.RunKey, _runKey);
		View = view; CursorId = null; Locked = false; _runKey = view.RunKey ?? _runKey;
		if (_ready && newRun) CopyChip.Reset(); // a new run's first chip is a first show: no "levelled" / "changed" against the last run
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
		// YOK-109: a card that says more than its name (the boss's "waits at the end / copies your best move") is a little larger, about its centre.
		var rects = _layout.Rects.ToDictionary(k => k.Key, k => k.Value);
		foreach (var v in nodes)
			if (Styles!.Look(v.Type).Tagline != "") { var r = rects[v.Id]; var big = r.Size * new Vector2(1.2f, 1.15f); rects[v.Id] = new Rect2(r.GetCenter() - big / 2, big); }
		_layout = _layout with { Rects = rects };
		foreach (var v in nodes)
		{
			var n = NodeScene!.Instantiate<MapNode>();
			Board.AddChild(n);
			n.Chosen += OnNodeChosen; n.Hovered += OnNodeHovered;
			_nodes[v.Id] = n;
		}
		BuildHeaders(nodes);
		BuildLegend();
		Update();
	}

	/// <summary>Header over a row's column: "ROW 3", "ROW 5 · ELDER" (R1, Y3), "AFTER ROW 8" for the row past the last play row.</summary>
	public static string HeaderText(int row, bool elder, int? lastPlayRow) =>
		lastPlayRow is { } n && row > n ? $"AFTER ROW {n}" : elder ? $"ROW {row} · ELDER" : $"ROW {row}";

	void BuildHeaders(IReadOnlyList<MapNodeView> nodes)
	{
		var holder = UiFind.Get<Control>(this, "RowHeaders");
		foreach (var l in _headers) { holder.RemoveChild(l); l.QueueFree(); }
		_headers.Clear();
		int? playRows = View!.Status?.PlayRowLimit;
		foreach (var (row, x) in _layout.Columns)
		{
			bool elder = nodes.Any(n => n.Row == row && n.Elder);
			var l = new Label
			{
				Text = HeaderText(row, elder, playRows), Name = "Header" + row, MouseFilter = MouseFilterEnum.Ignore,
				HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
				Position = new Vector2(x - _layout.ColumnWidth / 2, 0), Size = new Vector2(_layout.ColumnWidth, holder.Size.Y),
				ThemeTypeVariation = "PaperLabel",
			};
			l.AddThemeFontSizeOverride("font_size", 20);
			l.AddThemeColorOverride("font_color", elder ? ElderHeader : MapNodePalette.Paper);
			holder.AddChild(l);
			_headers.Add(l);
		}
	}

	/// <summary>YOK-109: one row per elder twist on this map: its seal and its label text (from the view; story data, never typed here).</summary>
	void BuildTwistLegend()
	{
		var holder = UiFind.Get<Control>(this, "LegendTwists");
		foreach (var c in holder.GetChildren()) { holder.RemoveChild(c); c.QueueFree(); }
		foreach (var e in View!.TwistLegend)
		{
			var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Name = "Twist_" + e.Icon };
			row.AddThemeConstantOverride("separation", 10);
			row.AddChild(new TwistSeal { Name = "Seal", Icon = e.Icon, Fallback = Styles!.TwistLook(e.Icon)?.Icon ?? "E", CustomMinimumSize = new Vector2(30, 30), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
			var t = new Label { Name = "Text", Text = e.Text, MouseFilter = MouseFilterEnum.Ignore, ThemeTypeVariation = "PaperLabel", ClipText = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			t.AddThemeFontSizeOverride("font_size", 18);
			row.AddChild(t);
			holder.AddChild(row);
		}
	}

	void BuildLegend()
	{
		BuildTwistLegend();
		if (_legendBuilt) return;
		_legendBuilt = true;
		var states = UiFind.Get<Control>(this, "LegendStates");
		foreach (var (label, state, cursor) in new[] {
			("cleared", MapNodeState.Visited, false), ("here", MapNodeState.Current, false), ("reachable", MapNodeState.Reachable, false),
			("selected", MapNodeState.Reachable, true), ("later", MapNodeState.Locked, false), ("passed over", MapNodeState.PassedOver, false) })
		{
			var item = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Name = "Legend_" + label.Replace(' ', '_') };
			item.AddThemeConstantOverride("separation", 8);
			var sw = new Panel { CustomMinimumSize = new Vector2(24, 24), MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
			var box = new StyleBoxFlat { BgColor = MapNodePalette.Fill(state), BorderColor = state == MapNodeState.PassedOver ? MapNodePalette.PassedOverLine : state == MapNodeState.Locked ? new Color(0.62f, 0.63f, 0.7f, 0.6f) : MapNodePalette.Border(state, cursor, MapNodePalette.Ink) };
			box.SetBorderWidthAll(cursor ? 5 : 3); box.SetCornerRadiusAll(4);
			sw.AddThemeStyleboxOverride("panel", box);
			item.AddChild(sw);
			var t = new Label { Text = label, MouseFilter = MouseFilterEnum.Ignore, ThemeTypeVariation = "PaperLabel" };
			t.AddThemeFontSizeOverride("font_size", 20);
			item.AddChild(t);
			states.AddChild(item);
		}
		UiFind.Get<Label>(this, "LegendTypes").Text = string.Join(" · ", Styles!.Styles.Where(s => s != null).Select(s => $"{s.Icon} {s.Label.ToLowerInvariant()}"));
	}

	/// <summary>Chip, status panel and detail panel read from the view (read-only; nothing here writes the run).</summary>
	void BindPanels()
	{
		CopyChip.Apply(View!.CopyTarget, View.CopyLocked);
		var status = View.Status;
		var panel = UiFind.Get<Control>(this, "Status");
		panel.Visible = status != null;
		if (status != null)
		{
			UiFind.Get<Label>(this, "HealthText").Text = $"RYO · {status.Health} / {status.MaxHealth}";
			UiFind.Get<Label>(this, "CoinsText").Text = $"{status.Coins} coins";
			UiFind.Get<Label>(this, "RowsText").Text = $"row {status.RowsDone} of {status.RowsTotal} done";
			var bar = UiFind.Get<ProgressBar>(this, "HealthBar");
			bar.MaxValue = status.MaxHealth; bar.Value = status.Health;
		}
		var focus = FocusId == null ? null : View.Nodes.FirstOrDefault(n => n.Id == FocusId);
		UiFind.Get<Control>(this, "Detail").Visible = focus != null;
		if (focus == null) return;
		var look = Styles!.Look(focus.Type);
		var detail = View.Detail(focus.Id);
		int? playRows = status?.PlayRowLimit;
		string where = playRows is { } n && focus.Row > n ? $"after row {n}" : $"row {focus.Row}";
		UiFind.Get<Label>(this, "DetailName").Text = $"{(focus.Yokai ?? look.Label).ToUpperInvariant()} · {look.Label} · {where}";
		var twist = UiFind.Get<Label>(this, "DetailTwist");
		twist.Text = detail?.Twist ?? ""; twist.Visible = !string.IsNullOrEmpty(detail?.Twist);
		var habit = UiFind.Get<Label>(this, "DetailHabit");
		habit.Text = string.IsNullOrEmpty(detail?.Habit) ? "" : "Habit to punish: " + detail!.Habit; habit.Visible = habit.Text != "";
		var seal = UiFind.Get<TwistSeal>(this, "DetailSeal");
		var twistLook = focus.Elder ? Styles.TwistLook(focus.TwistIcon) : null;
		seal.Visible = twistLook != null && twist.Visible; // a seal only where there is a twist to name (Y3)
		if (seal.Visible) { seal.Icon = focus.TwistIcon!; seal.Fallback = twistLook!.Value.Icon; }
		UiFind.Get<Label>(this, "DetailReward").Text = detail?.Reward ?? "";
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
			n.Bind(v, Styles!.Look(v.Type), r.Size, v.Id == CursorId && !Locked, Styles.TwistLook(v.TwistIcon));
		}
		BindPanels();
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
