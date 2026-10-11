using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-76: one node of the run map as loaded. <see cref="Type"/> is an open id resolved through an
/// <see cref="INodeTypeRegistry"/>; <see cref="Row"/> is where the node sits (data, never index math);
/// <see cref="Yokai"/> is who waits there (R2); <see cref="Elder"/> shows the twist icon (Y3).
/// <see cref="Payload"/> is opaque to the map: the node type's own systems read it.
/// </summary>
public sealed record MapNodeData(string Id, int Row, string Type, string? Yokai = null, bool Elder = false,
	string? Label = null, IReadOnlyDictionary<string, JsonElement>? Payload = null)
{
	static readonly IReadOnlyDictionary<string, JsonElement> NoPayload = new Dictionary<string, JsonElement>();

	/// <summary>Never null: a node without a payload has an empty one.</summary>
	public IReadOnlyDictionary<string, JsonElement> Payload { get; init; } = Payload ?? NoPayload;

	public string? PayloadString(string key) =>
		Payload.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

	public int? PayloadInt(string key) =>
		Payload.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) ? n : null;
}

/// <summary>A line on the map: after <see cref="From"/> the player may pick <see cref="To"/>.</summary>
public sealed record MapRoute(string From, string To);

/// <summary>
/// YOK-76: the run map as an immutable graph (pure C#, no Godot): nodes and routes by id. Built by an
/// <see cref="IMapSource"/>; <see cref="RunMap"/> walks it. The constructor is the loader validation: it throws
/// one <see cref="FormatException"/> listing every problem (duplicate id, unregistered type, unknown node in a
/// route, route not going to the next row, no nodes, unreachable node, dead end).
/// Routes only go forward, so a map with nodes always has a start and an end.
/// Rows only matter here, to keep routes forward: starts are the nodes of the lowest row, ends the nodes with no
/// route out. R1's row layout is a separate check (<see cref="MapRules.R1Problems"/>), so other layouts load.
/// </summary>
public sealed class MapGraph
{
	readonly Dictionary<string, MapNodeData> _byId = new();
	readonly Dictionary<string, List<string>> _next = new();
	readonly Dictionary<string, List<string>> _previous = new();
	static readonly IReadOnlyList<string> None = Array.Empty<string>();

	public string Id { get; }
	public string? Name { get; }
	/// <summary>Nodes in source order (the order every list below keeps, so walks are deterministic).</summary>
	public IReadOnlyList<MapNodeData> Nodes { get; }
	public IReadOnlyList<MapRoute> Routes { get; }
	/// <summary>Where a run may begin: the nodes of the lowest row.</summary>
	public IReadOnlyList<string> StartNodes { get; }
	/// <summary>Nodes with no route out: completing one finishes the map.</summary>
	public IReadOnlyList<string> EndNodes { get; }
	/// <summary>The row values in use, ascending (for laying the map out; they need not be 1..n).</summary>
	public IReadOnlyList<int> Rows { get; }
	/// <summary>Changes when any node, route or payload changes (mixed into <see cref="RunMap.StateHash"/>).</summary>
	public ulong Fingerprint { get; }

	public MapGraph(string id, IEnumerable<MapNodeData> nodes, IEnumerable<MapRoute> routes, INodeTypeRegistry types, string? name = null)
	{
		Id = id;
		Name = name;
		Nodes = nodes.ToArray();
		Routes = routes.ToArray();
		var problems = new List<string>();
		if (string.IsNullOrEmpty(id)) problems.Add("the map has no id");
		if (Nodes.Count == 0) problems.Add("the map has no nodes, so no start and no end");

		foreach (var n in Nodes)
		{
			if (string.IsNullOrEmpty(n.Id)) { problems.Add("a node has no id"); continue; }
			if (!_byId.TryAdd(n.Id, n)) { problems.Add($"node id '{n.Id}' is defined twice"); continue; }
			_next[n.Id] = new List<string>();
			_previous[n.Id] = new List<string>();
			var type = types.Find(n.Type);
			if (type is null)
				problems.Add($"node '{n.Id}': type '{n.Type}' is not registered (registered: {string.Join(", ", types.Types)})");
			else if (type.Check(n) is { } why)
				problems.Add($"node '{n.Id}': {why}");
		}

		foreach (var r in Routes)
		{
			bool known = true;
			foreach (string end in new[] { r.From, r.To })
				if (!_byId.ContainsKey(end)) { problems.Add($"route {r.From} -> {r.To}: unknown node '{end}'"); known = false; }
			if (!known) continue;
			int from = _byId[r.From].Row, to = _byId[r.To].Row;
			if (to <= from) problems.Add($"route {r.From} -> {r.To} goes {(to == from ? "sideways" : "backwards")} (row {from} to row {to})");
			else if (NextRowAfter(from) != to) problems.Add($"route {r.From} -> {r.To} skips a row (row {from} to row {to})");
			else if (_next[r.From].Contains(r.To)) problems.Add($"route {r.From} -> {r.To} is listed twice");
			else { _next[r.From].Add(r.To); _previous[r.To].Add(r.From); }
		}

		Rows = _byId.Values.Select(n => n.Row).Distinct().OrderBy(x => x).ToArray();
		StartNodes = Rows.Count == 0 ? None : Nodes.Where(n => n.Row == Rows[0] && Owns(n)).Select(n => n.Id).ToArray();
		EndNodes = Nodes.Where(n => Owns(n) && _next[n.Id].Count == 0).Select(n => n.Id).ToArray();

		// Every node must be on some walk from a start to the last row.
		var reached = new HashSet<string>(StartNodes);
		var queue = new Queue<string>(StartNodes);
		while (queue.Count > 0)
			foreach (string to in _next[queue.Dequeue()])
				if (reached.Add(to)) queue.Enqueue(to);
		foreach (var n in Nodes)
			if (Owns(n) && !reached.Contains(n.Id))
				problems.Add($"node '{n.Id}' is unreachable: no route leads to it from a start node");
		foreach (string end in EndNodes)
			if (_byId[end].Row != Rows[^1])
				problems.Add($"node '{end}' is a dead end: no route out before the last row ({Rows[^1]})");

		if (problems.Count > 0)
			throw new FormatException($"run map '{id}': " + string.Join("; ", problems));
		Fingerprint = ComputeFingerprint();
	}

	/// <summary>False for a node dropped at load (no id, or a second node under a taken id).</summary>
	bool Owns(MapNodeData n) => !string.IsNullOrEmpty(n.Id) && _byId.TryGetValue(n.Id, out var first) && ReferenceEquals(first, n);

	int? NextRowAfter(int row)
	{
		int? best = null;
		foreach (var n in _byId.Values)
			if (n.Row > row && (best is null || n.Row < best)) best = n.Row;
		return best;
	}

	public bool Has(string nodeId) => _byId.ContainsKey(nodeId);

	public MapNodeData? Find(string nodeId) => _byId.GetValueOrDefault(nodeId);

	public MapNodeData Node(string nodeId) =>
		_byId.TryGetValue(nodeId, out var n) ? n : throw new KeyNotFoundException($"run map '{Id}' has no node '{nodeId}'");

	/// <summary>Node ids the routes out of <paramref name="nodeId"/> lead to, in route order (empty for an end or unknown node).</summary>
	public IReadOnlyList<string> Next(string nodeId) => _next.TryGetValue(nodeId, out var l) ? l : None;

	/// <summary>Node ids with a route into <paramref name="nodeId"/>.</summary>
	public IReadOnlyList<string> Previous(string nodeId) => _previous.TryGetValue(nodeId, out var l) ? l : None;

	public bool HasRoute(string from, string to) => _next.TryGetValue(from, out var l) && l.Contains(to);

	public IEnumerable<MapNodeData> NodesInRow(int row) => Nodes.Where(n => n.Row == row);

	ulong ComputeFingerprint()
	{
		ulong h = Fnv.MixString(Fnv.Offset, Id);
		foreach (var n in Nodes)
		{
			h = Fnv.MixString(h, n.Id);
			h = Fnv.Mix(h, n.Row);
			h = Fnv.MixString(h, n.Type);
			h = Fnv.MixString(h, n.Yokai ?? "");
			h = Fnv.Mix(h, n.Elder ? 1 : 0);
			foreach (var kv in n.Payload.OrderBy(kv => kv.Key, StringComparer.Ordinal))
			{
				h = Fnv.MixString(h, kv.Key);
				h = Fnv.MixString(h, kv.Value.GetRawText());
			}
		}
		foreach (var r in Routes)
		{
			h = Fnv.MixString(h, r.From);
			h = Fnv.MixString(h, r.To);
		}
		return h;
	}
}
