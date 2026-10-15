using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-78 adapter: a <see cref="RunMap"/> (pure C#, YOK-76) as the map screen's <see cref="IMapView"/>. No rules here: reachability,
/// visited and the walk all come from the RunMap, read fresh on every <see cref="Nodes"/> call. <see cref="Select"/> enters the node
/// (<c>RunMap.Select</c>); the run flow owns <c>Complete</c> and what the node does.
/// State: the node Ryo is on (entered, or just completed) is Current; other completed nodes Visited; <c>Reachable()</c> Reachable;
/// an unchosen node in a row Ryo has left (row at or below the Current node's) PassedOver (YOK-108); the rest Locked.
/// YOK-108: with a <see cref="RunState"/> the view also gives the status panel's numbers and the T6 copy target (read-only,
/// <see cref="CopyRule.Target"/>); with an <see cref="INodeSceneResolver"/> it words each node's reward line from the same
/// table the run flow uses (<c>NodeContext.Coins</c>, <c>Binds</c>), so no reward number is typed here.
/// </summary>
public class RunMapView : IMapView
{
	readonly RunMap _map;
	readonly RunState? _run;
	readonly INodeSceneResolver? _scenes;
	readonly IReadOnlyList<MapRouteView> _routes;
	readonly HashSet<int> _playRows;

	public RunMapView(RunMap map, RunState? run = null, INodeSceneResolver? scenes = null)
	{
		_map = map; _run = run; _scenes = scenes;
		_routes = map.Graph.Routes.Select(r => new MapRouteView(r.From, r.To)).ToList();
		_playRows = map.Graph.Nodes.Where(n => n.Type != NodeTypeRegistry.Boss).Select(n => n.Row).ToHashSet();
	}

	public IReadOnlyList<MapRouteView> Routes => _routes;

	public IReadOnlyList<MapNodeView> Nodes
	{
		get
		{
			var reachable = _map.Reachable();
			int leftRow = _map.CurrentNode?.Row ?? int.MinValue;
			return _map.Graph.Nodes.Select(n => new MapNodeView(n.Id, n.Row, n.Type, YokaiName(n), n.Elder,
				n.Id == _map.Current ? MapNodeState.Current
				: _map.IsVisited(n.Id) ? MapNodeState.Visited
				: reachable.Contains(n.Id) ? MapNodeState.Reachable
				: n.Row <= leftRow ? MapNodeState.PassedOver
				: MapNodeState.Locked)).ToList();
		}
	}

	public MapStatusView? Status => _run is null ? null
		: new(_run.Health, RunState.MaxHealth, _run.Coins, _map.Visited.Count(id => _playRows.Contains(_map.Graph.Node(id).Row)), _playRows.Count) { LastPlayRow = _playRows.Max() };

	public virtual CopyTarget? CopyTarget => _run is null ? null : CopyRule.Target(_run);
	public virtual bool CopyLocked => false;

	public MapNodeDetail? Detail(string nodeId) => _map.Graph.Has(nodeId) ? new MapNodeDetail(RewardLine(_map.Graph.Node(nodeId))) : null;

	public bool Select(string nodeId) => _map.Select(nodeId).Ok;

	string? RewardLine(MapNodeData n)
	{
		switch (n.Type)
		{
			case NodeTypeRegistry.Merchant: return $"Heal {Merchant.HealPercent}%, or shop with your coins.";
			case NodeTypeRegistry.Dojo: return $"Rest {Dojo.RestHealPercent}%, or take a training trial.";
			case NodeTypeRegistry.Boss: return "The last duel of the run.";
		}
		if (_scenes is null) return null;
		NodeContext ctx;
		try { ctx = _scenes.Resolve(n).Context; }
		catch (FormatException) { return null; }
		string pay = ctx.Coins > 0 ? $"Pays {ctx.Coins} coins." : "Pays no coins.";
		if (!ctx.Binds) return pay;
		return $"{pay} Bind it and draft an ability{(n.Elder ? " (a rare can appear)" : "")}.";
	}

	static string? YokaiName(MapNodeData n) =>
		n.Yokai is null ? null : n.Label ?? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(n.Yokai.Replace('-', ' '));
}
