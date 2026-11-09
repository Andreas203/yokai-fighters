using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Dev;

/// <summary>
/// YOK-124: the dev menu's Map page. Toggles "Full map unlocked" (every node selectable on the map screen), "Reveal every
/// node" (the list below shows each node's type and elder twist) and the meta-unlocks hook (no-op until YOK-86), then
/// lists the run's nodes with a Travel button each (enabled for the nodes selectable now). Travel is the map screen's
/// own path (<see cref="DevMap.Travel"/>), so the node's real rules apply. Mid-fight every control is greyed with the reason.
/// Keyboard: the page's controls are a vertical ring; Right from the Map section button enters it, Left goes back.
/// </summary>
public partial class DevMapPage : VBoxContainer
{
	readonly DevContext _ctx;
	readonly VBoxContainer _nodes;
	readonly Label _note;

	public CheckButton FullMapToggle { get; }
	public CheckButton RevealToggle { get; }
	public CheckButton MetaUnlocksToggle { get; }
	/// <summary>The last refusal or result shown under the toggles.</summary>
	public string NoteText => _note.Text;

	public DevMapPage(DevContext ctx)
	{
		_ctx = ctx;
		AddThemeConstantOverride("separation", 10);
		AddChild(DevMenu.PageLabel("Map", 30));

		FullMapToggle = Toggle("FullMapToggle", "Full map unlocked: every node selectable", DevMap.FullMapUnlocked);
		FullMapToggle.Toggled += on => Changed(DevMap.SetFullMap(_ctx, on, out string? why), why, FullMapToggle, DevMap.FullMapUnlocked,
			on ? "Every node not yet walked is selectable on the map." : "The routes apply again from the node Ryo is on.");
		RevealToggle = Toggle("RevealToggle", "Reveal every node type and elder twist", DevMap.RevealAll);
		RevealToggle.Toggled += on => Changed(DevMap.SetReveal(_ctx, on, out string? why), why, RevealToggle, DevMap.RevealAll, null);
		MetaUnlocksToggle = Toggle("MetaUnlocksToggle", "Unlock all 12 meta-unlocks (YOK-86)", false);
		MetaUnlocksToggle.Disabled = true; // DevMap.UnlockAllMetaUnlocks is the hook; nothing to unlock yet
		MetaUnlocksToggle.TooltipText = DevMap.MetaUnlocksPending;

		_note = DevMenu.PageLabel(_ctx.RunChangeRefusal ?? DevMap.MetaUnlocksPending, 20, "Note");
		_note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		AddChild(_note);

		var scroll = new ScrollContainer { Name = "NodeScroll", CustomMinimumSize = new Vector2(0, 420), SizeFlagsVertical = SizeFlags.ExpandFill };
		_nodes = new VBoxContainer { Name = "Nodes", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_nodes.AddThemeConstantOverride("separation", 4);
		scroll.AddChild(_nodes);
		AddChild(scroll);

		bool frozen = _ctx.RunChangeRefusal != null;
		FullMapToggle.Disabled = RevealToggle.Disabled = frozen;
		Rebuild();
		TreeEntered += WireFocus;
	}

	CheckButton Toggle(string name, string text, bool on)
	{
		var b = new CheckButton { Name = name, Text = text, ButtonPressed = on, FocusMode = FocusModeEnum.All };
		b.AddThemeFontSizeOverride("font_size", 22);
		AddChild(b);
		return b;
	}

	void Changed(bool ok, string? why, CheckButton toggle, bool state, string? done)
	{
		if (!ok) toggle.SetPressedNoSignal(state); // refused: the toggle shows what is in force
		_note.Text = ok ? done ?? _note.Text : why ?? "";
		Rebuild();
	}

	/// <summary>The travel button of <paramref name="nodeId"/>; null when the page lists no such node.</summary>
	public Button? TravelButton(string nodeId) => _nodes.FindChild("Travel_" + nodeId, true, false) as Button;

	/// <summary>The listed text of <paramref name="nodeId"/>'s row (row, name, and with Reveal on its type and twist).</summary>
	public string? NodeText(string nodeId) => (_nodes.FindChild("Info_" + nodeId, true, false) as Label)?.Text;

	/// <summary>Re-reads the run: which nodes are listed and which can be travelled to now.</summary>
	public void Rebuild()
	{
		foreach (Node c in _nodes.GetChildren()) { _nodes.RemoveChild(c); c.QueueFree(); }
		if (_ctx.Flow?.Session is not { } session)
		{
			_nodes.AddChild(DevMenu.PageLabel(DevMap.NoRunReason, 20, "NoRun"));
			WireFocus();
			return;
		}
		foreach (var n in session.Map.Graph.Nodes)
		{
			var row = new HBoxContainer { Name = "Row_" + n.Id };
			row.AddThemeConstantOverride("separation", 12);
			string? refusal = DevMap.TravelRefusal(_ctx, n.Id);
			var travel = new Button { Name = "Travel_" + n.Id, Text = "Travel", Disabled = refusal != null, CustomMinimumSize = new Vector2(110, 36),
				TooltipText = refusal ?? "", FocusMode = refusal == null ? FocusModeEnum.All : FocusModeEnum.None };
			travel.AddThemeFontSizeOverride("font_size", 18);
			string id = n.Id;
			travel.Pressed += () =>
			{
				if (DevMap.Travel(_ctx, id, out string? why)) DevShell.Instance?.Menu.Close(); // the node's scene is up
				else { _note.Text = why ?? ""; Rebuild(); }
			};
			row.AddChild(travel);
			var info = DevMenu.PageLabel(Describe(session, n), 18, "Info_" + n.Id);
			info.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			row.AddChild(info);
			_nodes.AddChild(row);
		}
		WireFocus();
	}

	static string Describe(RunSession session, MapNodeData n)
	{
		string state = n.Id == session.Map.Current ? (session.Map.InNode ? " · HERE" : " · here, cleared")
			: session.Map.IsVisited(n.Id) ? " · cleared" : "";
		string name = n.Label ?? n.Yokai ?? n.Id;
		if (!DevMap.RevealAll) return $"Row {n.Row} · {name}{state}";
		// Y3: the elder's twist, worded by the same story card the fight's strip shows (nothing when the data has none).
		string twist = n.Elder && FightScene.Twists.ForNode(n) is { } t && TwistCopy.Label(t, TwistCopy.Story, n.Yokai) is { Placeholder: false } label ? $" · ELDER TWIST: {label.Text}" : "";
		return $"Row {n.Row} · {name} · {n.Type}{(n.Yokai is { } y ? $" ({y})" : "")}{twist}{state}";
	}

	/// <summary>A vertical ring over the enabled controls; Left returns to the Map section button, whose Right enters the page.</summary>
	void WireFocus()
	{
		var ring = new List<Control>();
		foreach (var t in new[] { FullMapToggle, RevealToggle }) if (!t.Disabled) ring.Add(t);
		ring.AddRange(_nodes.GetChildren().OfType<HBoxContainer>().Select(r => r.GetChildOrNull<Button>(0)).Where(b => b is { Disabled: false })!);
		if (ring.Count >= 2) MenuInput.WrapVertical(ring.ToArray());
		if (!IsInsideTree() || ring.Count == 0) return;
		Node? up = GetParent();
		while (up != null && up is not DevMenu) up = up.GetParent();
		if (up is DevMenu menu && menu.FindChild("Section_" + DevMenu.MapId, true, false) is Button section)
		{
			section.FocusNeighborRight = section.GetPathTo(ring[0]);
			foreach (var c in ring) c.FocusNeighborLeft = c.GetPathTo(section);
		}
	}
}
