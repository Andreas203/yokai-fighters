using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-78 adapter: a <see cref="RunMap"/> (pure C#, YOK-76) as the map screen's <see cref="IMapView"/>. No rules here: reachability,
/// visited and the walk all come from the RunMap, read fresh on every <see cref="Nodes"/> call. <see cref="Select"/> enters the node
/// (<c>RunMap.Select</c>); the run flow owns <c>Complete</c> and what the node does.
/// State: the node Ryo is on (entered, or just completed) is Current; other completed nodes Visited; <c>Reachable()</c> Reachable; the rest Locked.
/// </summary>
public sealed class RunMapView : IMapView
{
	readonly RunMap _map;
	readonly IReadOnlyList<MapRouteView> _routes;

	public RunMapView(RunMap map)
	{
		_map = map;
		_routes = map.Graph.Routes.Select(r => new MapRouteView(r.From, r.To)).ToList();
	}

	public IReadOnlyList<MapRouteView> Routes => _routes;

	public IReadOnlyList<MapNodeView> Nodes
	{
		get
		{
			var reachable = _map.Reachable();
			return _map.Graph.Nodes.Select(n => new MapNodeView(n.Id, n.Row, n.Type, YokaiName(n), n.Elder,
				n.Id == _map.Current ? MapNodeState.Current
				: _map.IsVisited(n.Id) ? MapNodeState.Visited
				: reachable.Contains(n.Id) ? MapNodeState.Reachable
				: MapNodeState.Locked)).ToList();
		}
	}

	public bool Select(string nodeId) => _map.Select(nodeId).Ok;

	static string? YokaiName(MapNodeData n) =>
		n.Yokai is null ? null : n.Label ?? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(n.Yokai.Replace('-', ' '));
}
