using System;
using System.Collections.Generic;
using System.Linq;

namespace YokaiFighters.Sim;

/// <summary>Outcome of a map step. A failed step changes nothing; <see cref="Error"/> says why.</summary>
public sealed record MapResult(bool Ok, string? Error = null)
{
	public static readonly MapResult Done = new(true);
	public static MapResult Fail(string why) => new(false, why);
}

/// <summary>
/// The whole of a <see cref="RunMap"/>'s progress as plain data (ids only), for a later save / Continue:
/// <see cref="RunMap.Snapshot"/> writes it, <see cref="RunMap.Restore"/> checks it against the graph.
/// </summary>
public sealed record RunMapSnapshot(string MapId, IReadOnlyList<string> Visited, string? Current)
{
	/// <summary>YOK-124: the walk took a step off the routes (<see cref="RunMap.FreeTravel"/>); restored only where the caller allows it.</summary>
	public bool OffRoute { get; init; }
}

/// <summary>
/// YOK-76: where Ryo is on the run map (pure C#, deterministic, no RNG). State is node ids only.
/// A step is <see cref="Select"/> (enter a reachable node: the fight or stop on it begins) then
/// <see cref="Complete"/> (it is over: the routes out of it open). While a node is entered nothing is reachable,
/// which is R1's one pick per row. Node types never appear here: a new type is a registry entry.
/// </summary>
public sealed class RunMap
{
	readonly List<string> _visited = new();

	public MapGraph Graph { get; }

	/// <summary>The node last entered (null before the first pick). Stays set after it is completed.</summary>
	public string? Current { get; private set; }

	/// <summary>Completed nodes, in the order they were walked.</summary>
	public IReadOnlyList<string> Visited => _visited;

	/// <summary>True between <see cref="Select"/> and <see cref="Complete"/>: Ryo is on <see cref="Current"/>.</summary>
	public bool InNode => Current is not null && !IsVisited(Current);

	/// <summary>The last completed node has no route out: the run's map is done.</summary>
	public bool IsFinished => Current is not null && !InNode && Graph.Next(Current).Count == 0;

	public MapNodeData? CurrentNode => Current is null ? null : Graph.Node(Current);

	/// <summary>Raised by <see cref="Select"/> once the node is entered. Not the place to read the Tanuki's copy (T1): the lock is taken by <c>RunSession.Enter</c> after this is raised; read <c>RunSession.LockedCopy</c> from <c>RunSession.NodeEntered</c>.</summary>
	public event Action<MapNodeData>? NodeEntered;

	/// <summary>Raised by <see cref="Complete"/> once the node is marked visited.</summary>
	public event Action<MapNodeData>? NodeCompleted;

	/// <summary>
	/// YOK-124 (a development tool: the dev menu's "Full map unlocked"): every node not yet walked can be picked,
	/// whatever the routes say. Still one node at a time: nothing is reachable while a node is entered or once the
	/// map is finished, and a visited node is never picked again. Off (the default) = the routes, R1. Switching it
	/// off leaves the walk as it is: the routes out of the last completed node open, as always. Nothing in the
	/// game sets it but the dev menu.
	/// </summary>
	public bool FreeTravel { get; set; }

	/// <summary>YOK-124: true once any step was taken off the routes (sticky; saved in the snapshot, mixed into the hash).</summary>
	public bool OffRoute { get; private set; }

	public RunMap(MapGraph graph) => Graph = graph;

	public bool IsVisited(string nodeId) => _visited.Contains(nodeId);

	/// <summary>
	/// Node ids the player may pick now, in the graph's order: the start nodes before the first pick, nothing
	/// while a node is entered, then the routes out of the completed node (none once finished).
	/// </summary>
	public IReadOnlyList<string> Reachable()
	{
		if (FreeTravel)
			return InNode || IsFinished ? Array.Empty<string>() : Graph.Nodes.Where(n => !IsVisited(n.Id)).Select(n => n.Id).ToArray();
		if (Current is null) return Graph.StartNodes;
		return InNode ? Array.Empty<string>() : Graph.Next(Current);
	}

	public bool CanSelect(string nodeId) => Reachable().Contains(nodeId);

	/// <summary>Enters a reachable node. Anything else fails and changes nothing.</summary>
	public MapResult Select(string nodeId)
	{
		if (!Graph.Has(nodeId)) return MapResult.Fail($"'{nodeId}' is not a node of map '{Graph.Id}'");
		if (InNode) return MapResult.Fail($"'{Current}' is not completed yet");
		if (IsFinished) return MapResult.Fail("the map is finished");
		if (!CanSelect(nodeId)) return MapResult.Fail($"'{nodeId}' is not reachable from {(Current is null ? "the start" : $"'{Current}'")}");
		if (!OnRoute(nodeId)) OffRoute = true; // only free travel gets here off the routes
		Current = nodeId;
		NodeEntered?.Invoke(Graph.Node(nodeId));
		return MapResult.Done;
	}

	bool OnRoute(string nodeId) => Current is null ? Graph.StartNodes.Contains(nodeId) : Graph.HasRoute(Current, nodeId);

	/// <summary>Marks the entered node visited and opens its routes. Fails unless <paramref name="nodeId"/> is the entered node.</summary>
	public MapResult Complete(string nodeId)
	{
		if (!InNode) return MapResult.Fail("no node is entered");
		if (nodeId != Current) return MapResult.Fail($"'{nodeId}' is not the entered node ('{Current}')");
		_visited.Add(nodeId);
		NodeCompleted?.Invoke(Graph.Node(nodeId));
		return MapResult.Done;
	}

	public RunMapSnapshot Snapshot() => new(Graph.Id, _visited.ToArray(), Current) { OffRoute = OffRoute };

	/// <summary>
	/// Rebuilds progress on <paramref name="graph"/>; throws <see cref="FormatException"/> when the snapshot is not a walk of it.
	/// YOK-124: a walk marked <see cref="RunMapSnapshot.OffRoute"/> is replayed with free travel only when
	/// <paramref name="allowOffRoute"/> (the dev slot); otherwise it must follow the routes like any other. The
	/// restored map has <see cref="FreeTravel"/> off either way.
	/// </summary>
	public static RunMap Restore(MapGraph graph, RunMapSnapshot snapshot, bool allowOffRoute = false)
	{
		if (snapshot.MapId != graph.Id)
			throw new FormatException($"the saved walk is of map '{snapshot.MapId}', not '{graph.Id}'");
		var map = new RunMap(graph) { FreeTravel = snapshot.OffRoute && allowOffRoute };
		foreach (string id in snapshot.Visited)
		{
			var step = map.Select(id);
			if (step.Ok) step = map.Complete(id);
			if (!step.Ok) throw new FormatException($"the saved walk of map '{graph.Id}' is not valid: {step.Error}");
		}
		string? last = snapshot.Visited.Count > 0 ? snapshot.Visited[^1] : null;
		if (snapshot.Current != last)
		{
			var step = snapshot.Current is null ? MapResult.Fail("no current node after visited nodes") : map.Select(snapshot.Current);
			if (!step.Ok) throw new FormatException($"the saved walk of map '{graph.Id}' is not valid: {step.Error}");
		}
		map.FreeTravel = false;
		return map;
	}

	public ulong StateHash()
	{
		ulong h = Fnv.Mix(Fnv.Offset, unchecked((int)Graph.Fingerprint));
		h = Fnv.Mix(h, unchecked((int)(Graph.Fingerprint >> 32)));
		h = Fnv.Mix(h, _visited.Count);
		foreach (string id in _visited) h = Fnv.MixString(h, id);
		h = Fnv.MixString(h, Current ?? "");
		if (OffRoute) h = Fnv.MixString(h, "off-route"); // YOK-124: a walk on the routes keeps its old hash
		return h;
	}
}

/// <summary>
/// YOK-76: R1's layout as a check on a map, kept out of <see cref="MapGraph"/> and <see cref="RunMap"/> so maps of
/// another shape still load. The default map must pass it (tests, and <c>tools/validate_data.py</c> for any map
/// that lists R1 in its <c>rules</c>).
/// </summary>
public static class MapRules
{
	/// <summary>Empty = the map has R1's shape: rows 1-8 of the listed types, then one boss node.</summary>
	public static IReadOnlyList<string> R1Problems(MapGraph map)
	{
		var problems = new List<string>();
		if (!map.Rows.SequenceEqual(Enumerable.Range(1, 9)))
			problems.Add($"rows must be 1-8 plus the boss in row 9, got {string.Join(", ", map.Rows)}");
		foreach (var n in map.Nodes)
		{
			string[] allowed = n.Row switch
			{
				5 or 8 => new[] { NodeTypeRegistry.ElderDuel },
				1 or 2 or 3 or 6 => new[] { NodeTypeRegistry.Duel },
				4 => new[] { NodeTypeRegistry.Merchant },
				7 => new[] { NodeTypeRegistry.Merchant, NodeTypeRegistry.Dojo },
				9 => new[] { NodeTypeRegistry.Boss },
				_ => Array.Empty<string>(),
			};
			if (allowed.Length > 0 && !allowed.Contains(n.Type))
				problems.Add($"node '{n.Id}': row {n.Row} must be {string.Join(" or ", allowed)}, got '{n.Type}'");
			if (n.Row == 1 && n.Yokai != "apprentice")
				problems.Add($"node '{n.Id}': row 1 is the apprentice, got '{n.Yokai}'");
		}
		if (map.NodesInRow(9).Count() != 1 || map.NodesInRow(9).Any(n => n.Yokai != "tanuki"))
			problems.Add("row 9 must be exactly one boss node with yokai 'tanuki'");
		return problems;
	}
}
