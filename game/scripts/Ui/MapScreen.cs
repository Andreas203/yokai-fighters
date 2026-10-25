using System;
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
///
/// YOK-109 (designer, 2025-10-25): the map is a horizontal paper scroll (<see cref="MapScroll"/>) much wider than the screen, panned inside a
/// clipping viewport: mouse wheel, click-drag on the paper, pad right stick, Q / E and LB / RB (a page at a time). Pan is eased and clamped to the
/// paper; moving the cursor to a node off screen centres it. A node is drawn only once it is time to pick it or it is on Ryo's walked path
/// (<see cref="MapReveal"/>): later rows, the Tanuki included, are blank paper with no name, icon, twist seal or row header. Not-taken nodes
/// of rows Ryo has left disappear (fade out, nothing left behind). A newly shown node inks in and its route paints out from Ryo: presentation
/// only, ended at once by any key or click (<see cref="FinishPresentation"/>); nothing in the flow waits for it.
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
	/// <summary>The node holder inside the scroll: as wide as every row's column together, so its rects are the layout of the whole map.</summary>
	public Control Board => _board ??= UiFind.Get<Control>(this, "Board");
	public MapRoutes Routes => _routes ??= UiFind.Get<MapRoutes>(this, "Routes");
	/// <summary>YOK-109: the clipping viewport the scroll slides in.</summary>
	public Control ScrollView => _view ??= UiFind.Get<Control>(this, "ScrollView");
	/// <summary>YOK-109: the paper strip itself (many screens wide); its x is minus <see cref="ScrollOffset"/>.</summary>
	public Control Scroll => _scroll ??= UiFind.Get<Control>(this, "Scroll");

	/// <summary>YOK-108, T6: the copy-target chip. Same control, icon and glow as the HUD talisman (<see cref="CopyTalisman"/>), laid out as a strip.</summary>
	public CopyTalisman CopyChip => UiFind.Get<CopyTalisman>(this, "CopyChip");
	/// <summary>The node whose detail panel shows: the cursor, else the node Ryo is on.</summary>
	public string? FocusId => CursorId ?? View?.Nodes.FirstOrDefault(n => n.State == MapNodeState.Current)?.Id;
	/// <summary>Row header texts left to right ("ROW 5 · ELDER", "AFTER ROW 8"): only the rows that are drawn.</summary>
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
	/// <summary>YOK-109: the seal beside the focus strip, shown for a focused elder that has a twist.</summary>
	public TwistSeal DetailSeal => UiFind.Get<TwistSeal>(this, "DetailSeal");
	/// <summary>YOK-109: the legend's twist rows: (icon id, label text), in order. Only twists on nodes that are drawn.</summary>
	public IReadOnlyList<(string Icon, string Text)> LegendTwists => UiFind.Get<Control>(this, "LegendTwists").GetChildren()
		.OfType<HBoxContainer>().Select(r => (r.GetNode<TwistSeal>("Seal").Icon, r.GetNode<Label>("Text").Text)).ToList();
	public string LegendTypesText => UiFind.Get<Label>(this, "LegendTypes").Text;
	public int LegendStateCount => UiFind.Get<Control>(this, "LegendStates").GetChildCount();

	/// <summary>YOK-109: where the scroll is now (px from the paper's left end; eased toward <see cref="ScrollTarget"/>).</summary>
	public float ScrollOffset { get; private set; }
	/// <summary>YOK-109: where the scroll is heading, always inside 0..<see cref="MaxScroll"/>.</summary>
	public float ScrollTarget { get; private set; }
	/// <summary>YOK-109: the furthest the paper can pan: its width minus the viewport's.</summary>
	public float MaxScroll => Mathf.Max(0f, Scroll.Size.X - ScrollView.Size.X);
	/// <summary>YOK-109: ids of the nodes drawn right now (cleared, here, choosable); everything else is blank paper.</summary>
	public IReadOnlyCollection<string> ShownIds => _nodes.Keys;
	/// <summary>True while Ryo's choice is on a pan or an ink animation the screen would skip on a key press (tests and captures use <see cref="FinishPresentation"/>).</summary>
	public bool Presenting => Routes.IsPainting || _tweens.Any(t => t.IsValid() && t.IsRunning());

	/// <summary>Pixels per wheel notch and per second of full right-stick push.</summary>
	public const float WheelStep = 200f, StickSpeed = 1500f, StickDeadzone = 0.25f;

	static readonly Color ElderHeader = MapNodePalette.ElderRed;
	readonly Dictionary<string, MapNode> _nodes = new();
	readonly List<Label> _headers = new();
	readonly List<Tween> _tweens = new();
	HashSet<string>? _shownIds;                      // ids drawn at the end of the last Update; null = nothing shown yet for this run
	Dictionary<(string, string), RouteKind>? _routeKinds;
	bool _legendBuilt, _layoutValid, _needsCentre, _snapNext, _dragging;
	string? _twistKey;
	object? _runKey;
	Control? _board, _view, _scroll; MapRoutes? _routes;
	MapLayoutResult _layout = new(new Dictionary<string, Rect2>(), Vector2.Zero);
	bool _ready;

	public override void _Ready()
	{
		MenuInput.EnsurePadConfirm();
		Styles ??= GD.Load<MapNodeStyles>("res://ui/map_node_styles.tres");
		NodeScene ??= GD.Load<PackedScene>("res://scenes/ui/map_node.tscn");
		// Routes in a row stack top to bottom, so up/down (and left/right) all step the cursor: the chip says "arrows", not LEFT / RIGHT.
		// YOK-109: both devices on each chip, as in the concept. No Esc / Start chip: the map has no pause binding (only fights pause).
		// The pan chips name wheel / right stick and the page keys; click-drag on the paper needs no chip.
		UiFind.Get<KeyChipFooter>(this, "Hint").SetHints(new KeyHint("route_any", "Choose route"), new KeyHint("confirm_any", "Go"),
			new KeyHint("pan_any", "Pan scroll"), new KeyHint("page_any", "Page"));
		ScrollView.Resized += () => { if (View != null) Relayout(); };
		ScrollView.GuiInput += OnViewGui;
		UiFind.Get<MapEdgeShade>(this, "ShadeLeft").Left = true;
		_ready = true;
		if (View != null) Rebuild();
	}

	/// <summary>Show this map. Safe before or after the screen enters the tree. Clears the pick lock.</summary>
	public void SetView(IMapView view)
	{
		bool newRun = view.RunKey is not null && !ReferenceEquals(view.RunKey, _runKey);
		bool fresh = View == null || newRun; // a new run's first show: no "already seen" nodes or strokes to compare with, and the scroll jumps to the choice
		View = view; CursorId = null; Locked = false; _runKey = view.RunKey ?? _runKey;
		if (fresh) { _shownIds = null; _routeKinds = null; }
		_needsCentre = true; _snapNext = fresh;
		if (_ready && newRun) CopyChip.Reset(); // a new run's first chip is a first show: no "levelled" / "changed" against the last run
		if (_ready) Rebuild();
	}

	/// <summary>Re-read node states from the view (after the run moved on) and accept a pick again. The scroll slides to the new choice.</summary>
	public void Refresh() { Locked = false; _needsCentre = true; _snapNext = false; Update(); }

	/// <summary>The node's button, or null when it is not drawn (a hidden node has no control at all, so nothing of it can be read or pressed).</summary>
	public MapNode? NodeFor(string id) => _nodes.GetValueOrDefault(id);
	/// <summary>The nodes drawn right now.</summary>
	public IReadOnlyCollection<MapNode> Nodes => _nodes.Values;
	/// <summary>Where the node sits on the board, drawn or not (the layout covers the whole map).</summary>
	public Rect2 RectOf(string id) => _layout.Rects[id];
	/// <summary>YOK-109: the node's rect in the viewport's own coordinates at the current <see cref="ScrollOffset"/> (on screen when it lies inside the viewport's size).</summary>
	public Rect2 ViewRectOf(string id) { var r = _layout.Rects[id]; return new Rect2(r.Position + Board.Position - new Vector2(ScrollOffset, 0), r.Size); }

	// ---- scroll -------------------------------------------------------------------------------------------------------------------

	/// <summary>Pan by <paramref name="dx"/> px from where the scroll is heading (eased, clamped to the paper).</summary>
	public void PanBy(float dx) => PanTo(ScrollTarget + dx);
	/// <summary>Pan to <paramref name="x"/> (px from the paper's left end), clamped to the paper; the view eases there.</summary>
	public void PanTo(float x) => ScrollTarget = Mathf.Clamp(x, 0f, MaxScroll);
	/// <summary>Finish the pan at once (no easing).</summary>
	public void SnapScroll() { ScrollOffset = ScrollTarget; ApplyScroll(); }

	void ApplyScroll()
	{
		ScrollOffset = Mathf.Clamp(ScrollOffset, 0f, MaxScroll);
		Scroll.Position = new Vector2(-Mathf.Round(ScrollOffset), 0);
		UiFind.Get<MapEdgeShade>(this, "ShadeLeft").More = ScrollOffset > 4f;
		UiFind.Get<MapEdgeShade>(this, "ShadeRight").More = ScrollOffset < MaxScroll - 4f;
	}

	/// <summary>Dragging the paper follows the pointer exactly, no easing.</summary>
	void DragBy(float dx) { ScrollTarget = Mathf.Clamp(ScrollTarget - dx, 0f, MaxScroll); ScrollOffset = ScrollTarget; ApplyScroll(); }

	/// <summary>Pan so the whole choice is in view: halfway between Ryo's node and the nodes he can pick (so the stroke between shows), else on whichever exists, else the left end.</summary>
	void CentreOnChoice()
	{
		float Cx(string id) => Board.Position.X + _layout.Rects[id].GetCenter().X;
		var here = View!.Nodes.FirstOrDefault(n => n.State == MapNodeState.Current);
		float? cx = ReachableIds.Count > 0 ? (here != null ? (Cx(here.Id) + Cx(ReachableIds[0])) / 2f : Cx(ReachableIds[0])) : here != null ? Cx(here.Id) : null;
		PanTo((cx ?? 0f) - ScrollView.Size.X / 2f);
	}

	/// <summary>Moving the cursor to a node near or past the viewport's edge centres it; a node well inside leaves the scroll alone.</summary>
	void EnsureVisible(string id)
	{
		if (!_layoutValid || !_layout.Rects.TryGetValue(id, out var r)) return;
		float x0 = Board.Position.X + r.Position.X, x1 = Board.Position.X + r.End.X, vw = ScrollView.Size.X, margin = Mathf.Min(160f, vw * 0.12f);
		if (x0 - ScrollTarget < margin || x1 - ScrollTarget > vw - margin) PanTo((x0 + x1) / 2f - vw / 2f);
	}

	public override void _Process(double delta)
	{
		if (!_ready || !IsVisibleInTree() || View == null) return;
		float stick = 0f; // pad right stick: the strongest push on any pad
		foreach (int dev in Input.GetConnectedJoypads()) { float v = Input.GetJoyAxis(dev, JoyAxis.RightX); if (Mathf.Abs(v) > Mathf.Abs(stick)) stick = v; }
		if (Mathf.Abs(stick) > StickDeadzone) DragBy(-Mathf.Sign(stick) * (Mathf.Abs(stick) - StickDeadzone) / (1f - StickDeadzone) * StickSpeed * (float)delta);
		if (Mathf.Abs(ScrollTarget - ScrollOffset) < 0.5f) { if (ScrollOffset != ScrollTarget) { ScrollOffset = ScrollTarget; ApplyScroll(); } return; }
		ScrollOffset = Mathf.Lerp(ScrollOffset, ScrollTarget, 1f - Mathf.Exp(-10f * (float)delta)); // eased, never snapping
		ApplyScroll();
	}

	void OnViewGui(InputEvent e)
	{
		if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } b) _dragging = b.Pressed;
		else if (e is InputEventMouseMotion m && _dragging) DragBy(m.Relative.X);
	}

	// ---- layout and build ---------------------------------------------------------------------------------------------------------

	void Rebuild()
	{
		foreach (var n in _nodes.Values) { Board.RemoveChild(n); n.QueueFree(); }
		_nodes.Clear();
		_twistKey = null;
		Relayout();
	}

	void Relayout()
	{
		_layoutValid = ComputeLayout();
		if (_layoutValid) { PanTo(ScrollTarget); Update(); }
	}

	/// <summary>Sizes the paper and the board from the viewport and the view's rows, and lays every node out (shown or not) on it.</summary>
	bool ComputeLayout()
	{
		var vs = ScrollView.Size;
		if (View == null || !_ready || vs.X < 100f || vs.Y < 60f) return false;
		var nodes = View.Nodes;
		float h = vs.Y;
		float mount = Mathf.Clamp(h * 0.045f, 18f, 36f), band = Mathf.Clamp(h * 0.07f, 30f, 52f);
		float colW = Mathf.Clamp(h * 0.62f, 300f, 520f); // roomy columns: a screen shows about four rows, the rest waits off to the right
		int cols = Math.Max(1, nodes.Select(n => n.Row).Distinct().Count());
		float boardTop = mount + band + 8f, boardH = Mathf.Max(60f, h - mount - 14f - boardTop);
		float pad = MapScroll.PaperLeft + 90f;
		float width = Mathf.Max(vs.X, 2f * pad + cols * colW), boardX = (width - cols * colW) / 2f;
		Scroll.Size = new Vector2(width, h);
		UiFind.Get<Control>(this, "Paper").Size = new Vector2(width, h);
		Board.Position = new Vector2(boardX, boardTop); Board.Size = new Vector2(cols * colW, boardH);
		var holder = UiFind.Get<Control>(this, "RowHeaders");
		holder.Position = new Vector2(boardX, mount + 6f); holder.Size = new Vector2(cols * colW, band);
		_layout = MapLayout.Compute(nodes, Board.Size);
		// YOK-109: a card that says more than its name (the boss's "waits at the end / copies your best move") is a little larger, about its centre.
		var rects = _layout.Rects.ToDictionary(k => k.Key, k => k.Value);
		foreach (var v in nodes)
			if (Styles!.Look(v.Type).Tagline != "") { var r = rects[v.Id]; var big = r.Size * new Vector2(1.2f, 1.15f); rects[v.Id] = new Rect2(r.GetCenter() - big / 2, big); }
		_layout = _layout with { Rects = rects };
		return true;
	}

	/// <summary>Header over a row's column: "ROW 3", "ROW 5 · ELDER" (R1, Y3), "AFTER ROW 8" for the row past the last play row.</summary>
	public static string HeaderText(int row, bool elder, int? lastPlayRow) =>
		lastPlayRow is { } n && row > n ? $"AFTER ROW {n}" : elder ? $"ROW {row} · ELDER" : $"ROW {row}";

	/// <summary>Headers only for rows that have a drawn node (a hidden row gives away neither its number's type nor its elder mark). Rows with a new node ink in.</summary>
	void BuildHeaders(IReadOnlyList<MapNodeView> shown, HashSet<int> newRows)
	{
		var holder = UiFind.Get<Control>(this, "RowHeaders");
		foreach (var l in _headers) { holder.RemoveChild(l); l.QueueFree(); }
		_headers.Clear();
		int? playRows = View!.Status?.PlayRowLimit;
		foreach (var (row, x) in _layout.Columns)
		{
			var inRow = shown.Where(n => n.Row == row).ToList();
			if (inRow.Count == 0) continue;
			bool elder = inRow.Any(n => n.Elder);
			var l = new Label
			{
				Text = HeaderText(row, elder, playRows), Name = "Header" + row, MouseFilter = MouseFilterEnum.Ignore,
				HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
				Position = new Vector2(x - _layout.ColumnWidth / 2, 0), Size = new Vector2(_layout.ColumnWidth, holder.Size.Y),
				ThemeTypeVariation = "PaperLabel",
			};
			l.AddThemeFontSizeOverride("font_size", 22);
			l.AddThemeColorOverride("font_color", elder ? ElderHeader : MapNodePalette.Ink);
			holder.AddChild(l);
			_headers.Add(l);
			if (newRows.Contains(row)) InkIn(l);
		}
	}

	/// <summary>YOK-109: one row per elder twist among the drawn nodes: its seal and its label text (from the view; story data, never typed here).</summary>
	void BuildTwistLegend(IReadOnlyList<MapNodeView> shown)
	{
		var icons = shown.Where(n => n.Elder && n.TwistIcon != null).Select(n => n.TwistIcon!).ToHashSet();
		var entries = View!.TwistLegend.Where(e => icons.Contains(e.Icon)).ToList();
		string key = string.Join("|", entries.Select(e => e.Icon + "=" + e.Text));
		if (key == _twistKey) return;
		_twistKey = key;
		var holder = UiFind.Get<Control>(this, "LegendTwists");
		foreach (var c in holder.GetChildren()) { holder.RemoveChild(c); c.QueueFree(); }
		foreach (var e in entries)
		{
			var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Name = "Twist_" + e.Icon };
			row.AddThemeConstantOverride("separation", 8);
			row.AddChild(new TwistSeal { Name = "Seal", Icon = e.Icon, Fallback = Styles!.TwistLook(e.Icon)?.Icon ?? "E", CustomMinimumSize = new Vector2(24, 24), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
			var t = new Label { Name = "Text", Text = e.Text, MouseFilter = MouseFilterEnum.Ignore, ThemeTypeVariation = "PaperLabel" };
			t.AddThemeFontSizeOverride("font_size", 17);
			row.AddChild(t);
			holder.AddChild(row);
		}
	}

	/// <summary>The legend names only what is on the paper: the four states a drawn node can be in, the types and twists of the drawn nodes.</summary>
	void BuildLegend(IReadOnlyList<MapNodeView> shown)
	{
		BuildTwistLegend(shown);
		if (!_legendBuilt)
		{
			_legendBuilt = true;
			var states = UiFind.Get<Control>(this, "LegendStates");
			foreach (var (label, state, cursor) in new[] {
				("cleared", MapNodeState.Visited, false), ("here", MapNodeState.Current, false), ("reachable", MapNodeState.Reachable, false), ("selected", MapNodeState.Reachable, true) })
			{
				var item = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Name = "Legend_" + label.Replace(' ', '_') };
				item.AddThemeConstantOverride("separation", 6);
				var sw = new Panel { CustomMinimumSize = new Vector2(20, 20), MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
				var box = new StyleBoxFlat { BgColor = MapNodePalette.Fill(state), BorderColor = MapNodePalette.Border(state, cursor, MapNodePalette.Ink) };
				box.SetBorderWidthAll(cursor ? 5 : 3); box.SetCornerRadiusAll(4);
				sw.AddThemeStyleboxOverride("panel", box);
				item.AddChild(sw);
				var t = new Label { Text = label, MouseFilter = MouseFilterEnum.Ignore, ThemeTypeVariation = "PaperLabel" };
				t.AddThemeFontSizeOverride("font_size", 17);
				item.AddChild(t);
				states.AddChild(item);
			}
		}
		var types = shown.Select(n => n.Type).ToHashSet();
		UiFind.Get<Label>(this, "LegendTypes").Text = string.Join(" · ", Styles!.Styles.Where(s => s != null && types.Contains(s.Type)).Select(s => $"{s.Icon} {s.Label.ToLowerInvariant()}"));
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

	// ---- presentation (ink in, fade out, paint) -----------------------------------------------------------------------------------

	/// <summary>A newly shown control appears like ink spreading in: a short fade with a slight swell. Skippable; nothing waits for it.</summary>
	void InkIn(Control c)
	{
		c.Modulate = new Color(1, 1, 1, 0);
		c.PivotOffset = c.Size / 2f; c.Scale = new Vector2(0.94f, 0.94f);
		var t = c.CreateTween().SetParallel();
		t.TweenProperty(c, "modulate:a", 1f, 0.5f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		t.TweenProperty(c, "scale", Vector2.One, 0.5f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		_tweens.RemoveAll(x => !x.IsValid());
		_tweens.Add(t);
	}

	/// <summary>A node that is no longer drawn (its row was left without it) fades away and is freed; it is out of the screen's tables at once and cannot be pressed.</summary>
	void RemoveNode(string id, bool animate)
	{
		if (!_nodes.Remove(id, out var n)) return;
		n.Disabled = true; n.MouseFilter = MouseFilterEnum.Ignore;
		if (!animate) { Board.RemoveChild(n); n.QueueFree(); return; }
		var t = n.CreateTween();
		t.TweenProperty(n, "modulate:a", 0f, 0.25f);
		t.TweenCallback(Callable.From(n.QueueFree));
	}

	/// <summary>Ends every ink and paint animation at once (a key press does this; tests and captures call it so they never read a half-drawn frame).</summary>
	public void FinishPresentation()
	{
		Routes.FinishPainting();
		foreach (var t in _tweens) if (t.IsValid()) t.Kill();
		_tweens.Clear();
		foreach (var n in _nodes.Values) { n.Modulate = Colors.White; n.Scale = Vector2.One; }
		foreach (var l in _headers) { l.Modulate = Colors.White; l.Scale = Vector2.One; }
	}

	// ---- state ------------------------------------------------------------------------------------------------------------------

	void Update()
	{
		if (View == null || !_ready || !_layoutValid) return;
		var all = View.Nodes;
		var shown = all.Where(n => MapReveal.IsRevealed(n.State)).ToList();
		var shownIds = shown.Select(n => n.Id).ToHashSet();
		bool fresh = _shownIds == null;
		var before = _shownIds ?? new HashSet<string>();
		ReachableIds = shown.Where(n => n.State == MapNodeState.Reachable).Select(n => n.Id).ToList();
		if (CursorId == null || !ReachableIds.Contains(CursorId)) CursorId = ReachableIds.FirstOrDefault();
		foreach (var id in _nodes.Keys.Where(k => !shownIds.Contains(k)).ToList()) RemoveNode(id, !fresh);
		var newRows = new HashSet<int>();
		foreach (var v in shown)
		{
			var r = _layout.Rects[v.Id];
			if (!_nodes.TryGetValue(v.Id, out var n))
			{
				n = NodeScene!.Instantiate<MapNode>();
				Board.AddChild(n);
				n.Chosen += OnNodeChosen; n.Hovered += OnNodeHovered;
				_nodes[v.Id] = n;
				n.Bind(v, Styles!.Look(v.Type), r.Size, false, Styles.TwistLook(v.TwistIcon));
				// the choice inks in even on a run's first show; cleared nodes already on the walk just are there
				if (!before.Contains(v.Id) && (!fresh || v.State == MapNodeState.Reachable)) { InkIn(n); newRows.Add(v.Row); }
			}
			n.Position = r.Position;
			n.Bind(v, Styles!.Look(v.Type), r.Size, v.Id == CursorId && !Locked, Styles.TwistLook(v.TwistIcon));
		}
		_shownIds = shownIds;
		BuildHeaders(shown, newRows);
		BuildLegend(shown);
		BindPanels();

		var state = all.ToDictionary(n => n.Id, n => n.State);
		bool Done(string id) => state[id] is MapNodeState.Visited or MapNodeState.Current;
		var lines = new List<(Vector2 A, Vector2 B, RouteKind Kind)>();
		var kinds = new Dictionary<(string, string), RouteKind>();
		var paint = new List<(Vector2 A, Vector2 B)>();
		foreach (var r in View.Routes.Where(r => shownIds.Contains(r.From) && shownIds.Contains(r.To))) // a stroke needs both ends on the paper
		{
			var a = _layout.Rects[r.From]; var b = _layout.Rects[r.To];
			var kind = Done(r.From) && Done(r.To) ? RouteKind.Walked : Done(r.From) && state[r.To] == MapNodeState.Reachable ? RouteKind.Open : RouteKind.Idle;
			var line = (new Vector2(a.End.X, a.GetCenter().Y), new Vector2(b.Position.X, b.GetCenter().Y), kind);
			lines.Add(line);
			kinds[(r.From, r.To)] = kind;
			// a stroke paints in when it is new or has just become the walked one (a pick); on a run's first show only the open choice does
			bool changed = _routeKinds == null ? kind == RouteKind.Open : !_routeKinds.TryGetValue((r.From, r.To), out var was) || was != kind;
			if (changed && kind != RouteKind.Idle) paint.Add((line.Item1, line.Item2));
		}
		_routeKinds = kinds;
		Routes.SetLines(lines);
		Routes.Paint(paint);

		if (_needsCentre)
		{
			_needsCentre = false;
			CentreOnChoice();
			if (_snapNext) SnapScroll();
			_snapNext = false;
		}
		ApplyScroll();
	}

	/// <summary>Step the cursor through the reachable nodes (wraps). Does nothing when none are reachable or a pick already went through.</summary>
	public void Move(int dir)
	{
		if (Locked || ReachableIds.Count == 0) return;
		int i = CursorId == null ? 0 : ReachableIds.ToList().IndexOf(CursorId);
		CursorId = ReachableIds[((i + dir) % ReachableIds.Count + ReachableIds.Count) % ReachableIds.Count];
		Update();
		EnsureVisible(CursorId);
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

	/// <summary>Any key, click or pad button ends the ink and paint animation at once; the wheel pans the scroll wherever the pointer is (the map has nothing else to scroll).</summary>
	public override void _Input(InputEvent e)
	{
		if (!IsVisibleInTree() || View == null) return;
		if (e is InputEventKey { Pressed: true } or InputEventMouseButton { Pressed: true } or InputEventJoypadButton { Pressed: true }) FinishPresentation();
		if (e is InputEventMouseButton { Pressed: true } w)
		{
			float dir = w.ButtonIndex switch { MouseButton.WheelUp or MouseButton.WheelLeft => -1f, MouseButton.WheelDown or MouseButton.WheelRight => 1f, _ => 0f };
			if (dir != 0f) { PanBy(dir * WheelStep * Mathf.Max(1f, w.Factor)); GetViewport().SetInputAsHandled(); }
		}
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (!IsVisibleInTree() || View == null) return;
		int page = MenuInput.Tab(e); // Q / E, LB / RB: a page of scroll at a time
		if (page != 0) { PanBy(page * ScrollView.Size.X * 0.6f); GetViewport().SetInputAsHandled(); return; }
		if (e.IsActionPressed("ui_up") || e.IsActionPressed("ui_left")) Move(-1);
		else if (e.IsActionPressed("ui_down") || e.IsActionPressed("ui_right")) Move(1);
		else if (e.IsActionPressed("ui_accept")) Confirm();
		else return;
		GetViewport().SetInputAsHandled();
	}
}
