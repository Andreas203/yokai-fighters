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
public sealed record MapStatusView(int Health, int MaxHealth, int Coins, int RowsDone, int RowsTotal);

/// <summary>What the detail panel adds to a node's own data: the reward line, and the elder twist line when the twist data exists (Y3, YOK-90).</summary>
public sealed record MapNodeDetail(string? Reward, string? Twist = null);

/// <summary>
/// One node as the map screen shows it. <see cref="Type"/> is an open string (a lookup key into the map node styles resource,
/// never a switch in code); <see cref="Yokai"/> is the display name of whoever waits there (R2), null for a stop with no yokai;
/// <see cref="Elder"/> is Y3's marker.
/// </summary>
public sealed record MapNodeView(string Id, int Row, string Type, string? Yokai, bool Elder, MapNodeState State);

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
	/// <summary>YOK-108: the reward line (and twist line) for one node; null = nothing to add.</summary>
	MapNodeDetail? Detail(string nodeId) => null;
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
	public MapNodeDetail? Detail(string nodeId) => Details.GetValueOrDefault(nodeId);
	public string? Current { get; private set; }

	public IReadOnlyList<MapRouteView> Routes => _routes;
	public IReadOnlyList<MapNodeView> Nodes => _defs.Select(d => new MapNodeView(d.Id, d.Row, d.Type, d.Yokai, d.Elder, _state[d.Id])).ToList();

	public StubMapView Add(string id, int row, string type, string? yokai = null, bool elder = false)
	{
		_defs.Add((id, row, type, yokai, elder));
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
