using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

/// <summary>
/// Where a node stands for the player right now (YOK-78). <see cref="PassedOver"/> (YOK-108): a row Ryo has already
/// left, so this node was not chosen and can never be entered (R1); it is a kind of not-reachable, never selectable.
/// </summary>
public enum MapNodeState { Visited, Current, Reachable, Locked, PassedOver }

/// <summary>The run's numbers for the map's status panel (C1 health, R8 coins, R1 rows done). RowsTotal excludes the boss's row.</summary>
public sealed record MapStatusView(int Health, int MaxHealth, int Coins, int RowsDone, int RowsTotal)
{
	/// <summary>The highest play-row value (the boss's row is past it). RowsTotal is a count, so it is not this when row values skip. 0 = use RowsTotal.</summary>
	public int LastPlayRow { get; init; }
	public int PlayRowLimit => LastPlayRow > 0 ? LastPlayRow : RowsTotal;
}

/// <summary>
/// What the detail panel adds to a node's own data: the reward line, the elder twist line when the twist data exists (Y3, YOK-90),
/// and the habit line (YOK-109: the habit of the profile this node's yokai fights with, from the profile data; null = none).
/// </summary>
public sealed record MapNodeDetail(string? Reward, string? Twist = null, string? Habit = null);

/// <summary>YOK-109: one legend row for an elder twist: its icon id (data/twists <c>icon</c>) and its label text (story data <c>-label</c>).</summary>
public sealed record MapTwistLegendEntry(string Icon, string Text);

/// <summary>
/// One node as the map screen shows it. <see cref="Type"/> is an open string (a lookup key into the map node styles resource,
/// never a switch in code); <see cref="Yokai"/> is the display name of whoever waits there (R2), null for a stop with no yokai;
/// <see cref="Elder"/> is Y3's marker.
/// </summary>
public sealed record MapNodeView(string Id, int Row, string Type, string? Yokai, bool Elder, MapNodeState State)
{
	/// <summary>YOK-139 (Y3): the icon id of this elder node's twist (data/twists/*.json <c>icon</c>); null = no twist data for it.</summary>
	public string? TwistIcon { get; init; }
}

/// <summary>A route between two node ids; goes from the lower row to the higher.</summary>
public sealed record MapRouteView(string From, string To);

/// <summary>
/// What the map screen needs from the run (YOK-78): read-only nodes with their current state, the routes, and one action.
/// The screen holds no map rules; it never decides what is reachable beyond reading <see cref="MapNodeState.Reachable"/>.
/// Node order is the order within a row (top to bottom); row values only need to ascend, not be 1..n.
/// <see cref="Nodes"/> must reflect the latest state each time it is read.
/// </summary>
public interface IMapView
{
	IReadOnlyList<MapNodeView> Nodes { get; }
	IReadOnlyList<MapRouteView> Routes { get; }
	/// <summary>YOK-108: health, coins and rows done; null = no run behind this view (the panel hides).</summary>
	MapStatusView? Status => null;
	/// <summary>YOK-108, T6: what the Tanuki would copy right now (read-only, <c>CopyRule.Target</c>); null = no slot holds a special (T2).</summary>
	CopyTarget? CopyTarget => null;
	/// <summary>True once the copy is locked (T1): the chip then shows <see cref="CopyTarget"/> as the locked copy.</summary>
	bool CopyLocked => false;
	/// <summary>
	/// YOK-119: who this view belongs to (the run). The screen forgets what the copy chip last showed when it changes, so a new run
	/// starts with no pulse or toast. Null = unknown (the chip then carries over between views, as the tests' stubs do).
	/// </summary>
	object? RunKey => null;
	/// <summary>YOK-108: the reward line (and twist line) for one node; null = nothing to add.</summary>
	MapNodeDetail? Detail(string nodeId) => null;
	/// <summary>YOK-109: the elder twists on this map, one per icon, with their label text; empty = no legend rows.</summary>
	IReadOnlyList<MapTwistLegendEntry> TwistLegend => System.Array.Empty<MapTwistLegendEntry>();
	/// <summary>Try to enter the node. False = refused (not reachable, or the view says no); nothing changes then.</summary>
	bool Select(string nodeId);
}

/// <summary>
/// TEST FIXTURE: an in-memory map with no run behind it. <see cref="Grid"/> builds <c>rows</c> rows of <c>perRow</c> nodes with every
/// node linked to every node of the next row. <see cref="Select"/> makes a reachable node Current and locks the rest;
/// <see cref="Complete"/> then opens the routes out of it, like a RunMap would.
/// </summary>
public sealed class StubMapView : IMapView
{
	readonly List<(string Id, int Row, string Type, string? Yokai, bool Elder)> _defs = new();
	readonly Dictionary<string, MapNodeState> _state = new();
	readonly List<MapRouteView> _routes = new();

	public List<string> Selected { get; } = new();
	public MapStatusView? Status { get; set; }
	public CopyTarget? CopyTarget { get; set; }
	public bool CopyLocked { get; set; }
	public Dictionary<string, MapNodeDetail> Details { get; } = new();
	public List<MapTwistLegendEntry> Legend { get; } = new();
	public IReadOnlyList<MapTwistLegendEntry> TwistLegend => Legend;
	public MapNodeDetail? Detail(string nodeId) => Details.GetValueOrDefault(nodeId);
	public string? Current { get; private set; }

	public IReadOnlyList<MapRouteView> Routes => _routes;
	public IReadOnlyList<MapNodeView> Nodes => _defs.Select(d => new MapNodeView(d.Id, d.Row, d.Type, d.Yokai, d.Elder, _state[d.Id]) { TwistIcon = TwistIcons.GetValueOrDefault(d.Id) }).ToList();

	public Dictionary<string, string> TwistIcons { get; } = new();

	public StubMapView Add(string id, int row, string type, string? yokai = null, bool elder = false, string? twistIcon = null)
	{
		_defs.Add((id, row, type, yokai, elder));
		if (twistIcon != null) TwistIcons[id] = twistIcon;
		_state[id] = MapNodeState.Locked;
		return this;
	}

	public StubMapView Route(string from, string to) { _routes.Add(new MapRouteView(from, to)); return this; }

	public StubMapView Open(params string[] ids) { foreach (var id in ids) _state[id] = MapNodeState.Reachable; return this; }

	public static StubMapView Grid(int rows, int perRow, string[]? types = null)
	{
		var s = new StubMapView();
		types ??= new[] { "duel", "elder", "merchant" };
		for (int r = 1; r <= rows; r++)
			for (int i = 0; i < (r == 1 || r == rows ? 1 : perRow); i++)
				s.Add($"r{r}-{i}", r, types[(r + i) % types.Length], types[(r + i) % types.Length] == "merchant" ? null : $"Yokai {r}.{i}", types[(r + i) % types.Length] == "elder");
		for (int r = 1; r < rows; r++)
			foreach (var a in s._defs.Where(d => d.Row == r))
				foreach (var b in s._defs.Where(d => d.Row == r + 1))
					s.Route(a.Id, b.Id);
		s.Open(s._defs.Where(d => d.Row == 1).Select(d => d.Id).ToArray());
		return s;
	}

	public bool Select(string nodeId)
	{
		if (!_state.TryGetValue(nodeId, out var st) || st != MapNodeState.Reachable) return false;
		foreach (var k in _state.Keys.ToList())
		{
			if (_state[k] == MapNodeState.Current) _state[k] = MapNodeState.Visited;
			else if (_state[k] == MapNodeState.Reachable) _state[k] = MapNodeState.Locked;
		}
		_state[nodeId] = MapNodeState.Current;
		Current = nodeId;
		Selected.Add(nodeId);
		return true;
	}

	/// <summary>The current node is over: the nodes its routes lead to become reachable.</summary>
	public void Complete()
	{
		if (Current is null) return;
		foreach (var r in _routes.Where(r => r.From == Current)) _state[r.To] = MapNodeState.Reachable;
	}
}
