using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-76: the run map model. The default map (<c>data/map/default-map.json</c>) loads through the same
/// <see cref="FileMapSource"/> the game will use and is held to R1; every possible walk of it is played to the
/// end. Rejections and other layouts use small maps written here, so they don't depend on the data file.
/// </summary>
public static class RunMapTests
{
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");

	static MapGraph Default() => FileMapSource.Default(ContentPaths.Data("map")).Load();

	// A three-row map: a -> b1 | b2 -> c. Edited per test.
	const string SmallNodes =
		"{\"id\":\"a\",\"row\":1,\"type\":\"duel\",\"yokai\":\"apprentice\"}," +
		"{\"id\":\"b1\",\"row\":2,\"type\":\"duel\",\"yokai\":\"oni\"}," +
		"{\"id\":\"b2\",\"row\":2,\"type\":\"elder\",\"yokai\":\"elder-oni\",\"elder\":true}," +
		"{\"id\":\"c\",\"row\":3,\"type\":\"boss\",\"yokai\":\"tanuki\"}";
	const string SmallRoutes =
		"{\"from\":\"a\",\"to\":\"b1\"},{\"from\":\"a\",\"to\":\"b2\"},{\"from\":\"b1\",\"to\":\"c\"},{\"from\":\"b2\",\"to\":\"c\"}";

	static string Json(string nodes = SmallNodes, string routes = SmallRoutes, string head = "\"kind\":\"run_map\",\"id\":\"small\",\"schema_version\":1") =>
		"{" + head + ",\"nodes\":[" + nodes + "],\"routes\":[" + routes + "]}";

	static void Do(MapResult r, string what) => Assert.True(r.Ok, $"{what}: {r.Error}");

	static void Step(RunMap map, string id)
	{
		Do(map.Select(id), $"select {id}");
		Do(map.Complete(id), $"complete {id}");
	}

	/// <summary>Every walk from a start node to an end node, as node id lists, in graph order.</summary>
	static List<List<string>> AllWalks(MapGraph g)
	{
		var walks = new List<List<string>>();
		void Go(List<string> path)
		{
			var next = g.Next(path[^1]);
			if (next.Count == 0) { walks.Add(new List<string>(path)); return; }
			foreach (string n in next) { path.Add(n); Go(path); path.RemoveAt(path.Count - 1); }
		}
		foreach (string s in g.StartNodes) Go(new List<string> { s });
		return walks;
	}

	[Test]
	public static void DefaultMapLoadsWithTheR1Shape()
	{
		var g = Default();
		Assert.Equal("default-map", g.Id, "id");
		Assert.Equal(0, MapRules.R1Problems(g).Count, "R1 problems: " + string.Join("; ", MapRules.R1Problems(g)));
		Assert.True(g.Rows.SequenceEqual(Enumerable.Range(1, 9)), "rows 1-8 and the boss row");
		Assert.True(g.StartNodes.SequenceEqual(new[] { "r1-apprentice" }), "one start: the apprentice");
		Assert.True(g.EndNodes.SequenceEqual(new[] { "boss-tanuki" }), "one end: the Tanuki after row 8");
		Assert.Equal(NodeTypeRegistry.Boss, g.Node("boss-tanuki").Type, "the end is a boss node");
		// Figure 6: rows 1 and 4 are single nodes, every other row is a fork.
		foreach (int row in Enumerable.Range(1, 8))
		{
			int n = g.NodesInRow(row).Count();
			if (row is 1 or 4) Assert.Equal(1, n, $"row {row} nodes");
			else Assert.True(n is 2 or 3, $"row {row} is a fork of 2-3 nodes, got {n}");
		}
		Assert.True(g.Nodes.Sum(n => g.Next(n.Id).Count) == g.Routes.Count && g.Nodes.Sum(n => g.Previous(n.Id).Count) == g.Routes.Count, "next/previous mirror the routes");
		foreach (var n in g.Nodes)
		{
			var type = NodeTypeRegistry.Default().Find(n.Type)!;
			if (type.Fight) Assert.True(!string.IsNullOrEmpty(n.Yokai), $"{n.Id} names its yokai (R2)");
			Assert.Equal(n.Row is 5 or 8, n.Elder, $"{n.Id} elder icon (Y3)");
			Assert.Equal(n.Row is 4 or 7, type.Placeholder, $"{n.Id} placeholder type");
		}
	}

	[Test]
	public static void ReachableSetsForEveryNode()
	{
		var g = Default();
		// The lines of the default map, written out: a change to the file's routes must be a change here too.
		var expected = new Dictionary<string, string[]>
		{
			["r1-apprentice"] = new[] { "r2-kitsune", "r2-oni", "r2-kappa" },
			["r2-kitsune"] = new[] { "r3-oni" },
			["r2-oni"] = new[] { "r3-oni", "r3-kappa" },
			["r2-kappa"] = new[] { "r3-kappa" },
			["r3-oni"] = new[] { "r4-merchant" },
			["r3-kappa"] = new[] { "r4-merchant" },
			["r4-merchant"] = new[] { "r5-elder-oni", "r5-nine-tails" },
			["r5-elder-oni"] = new[] { "r6-kitsune", "r6-kappa" },
			["r5-nine-tails"] = new[] { "r6-kitsune", "r6-kappa" },
			["r6-kitsune"] = new[] { "r7-merchant", "r7-dojo" },
			["r6-kappa"] = new[] { "r7-merchant", "r7-dojo" },
			["r7-merchant"] = new[] { "r8-elder-kappa", "r8-elder-oni" },
			["r7-dojo"] = new[] { "r8-elder-kappa", "r8-elder-oni" },
			["r8-elder-kappa"] = new[] { "boss-tanuki" },
			["r8-elder-oni"] = new[] { "boss-tanuki" },
			["boss-tanuki"] = Array.Empty<string>(),
		};
		Assert.Equal(expected.Count, g.Nodes.Count, "every node has an expectation");
		Assert.True(new RunMap(g).Reachable().SequenceEqual(g.StartNodes), "before the first pick: the start nodes");

		foreach (var node in g.Nodes)
		{
			// Any walk to the node: back along the first route in, then forwards.
			var path = new List<string> { node.Id };
			while (g.Previous(path[0]).Count > 0) path.Insert(0, g.Previous(path[0])[0]);
			var map = new RunMap(g);
			foreach (string id in path.Take(path.Count - 1)) Step(map, id);

			Assert.True(map.CanSelect(node.Id), $"{node.Id} is reachable along {string.Join(" > ", path)}");
			Do(map.Select(node.Id), $"select {node.Id}");
			Assert.Equal(0, map.Reachable().Count, $"{node.Id} entered: nothing else to pick (one per row, R1)");
			Assert.True(map.InNode, $"{node.Id} entered");
			Do(map.Complete(node.Id), $"complete {node.Id}");
			Assert.True(map.Reachable().SequenceEqual(expected[node.Id]),
				$"after {node.Id}: expected {string.Join(", ", expected[node.Id])}, got {string.Join(", ", map.Reachable())}");
			Assert.Equal(node.Id, map.Current, "current stays on the completed node");
			Assert.Equal(expected[node.Id].Length == 0, map.IsFinished, $"finished after {node.Id}");
			// One pick per row: nothing reachable shares a row with a walked node or with each other's next row.
			var rows = map.Reachable().Select(id => g.Node(id).Row).Distinct().ToArray();
			Assert.True(rows.Length <= 1 && rows.All(r => r > node.Row), $"after {node.Id} every option is in one later row");
		}
	}

	[Test]
	public static void InvalidSelectChangesNothing()
	{
		var map = new RunMap(Default());
		int entered = 0, completed = 0;
		map.NodeEntered += _ => entered++;
		map.NodeCompleted += _ => completed++;

		void Refused(MapResult r, string what)
		{
			Assert.True(!r.Ok && !string.IsNullOrEmpty(r.Error), $"{what} must fail with a reason");
		}
		void Unchanged(Func<MapResult> act, string what)
		{
			ulong hash = map.StateHash();
			string? current = map.Current;
			int visited = map.Visited.Count, e = entered, c = completed;
			Refused(act(), what);
			Assert.Equal(hash, map.StateHash(), $"{what}: hash");
			Assert.Equal(current, map.Current, $"{what}: current");
			Assert.Equal(visited, map.Visited.Count, $"{what}: visited");
			Assert.True(e == entered && c == completed, $"{what}: no event");
		}

		Assert.True(map.Current is null && map.Visited.Count == 0 && !map.IsFinished && !map.InNode, "fresh map");
		Unchanged(() => map.Select("r2-oni"), "row 2 before row 1");
		Unchanged(() => map.Select("boss-tanuki"), "the boss from the start");
		Unchanged(() => map.Select("no-such-node"), "unknown node");
		Unchanged(() => map.Complete("r1-apprentice"), "complete before select");

		Do(map.Select("r1-apprentice"), "select row 1");
		Unchanged(() => map.Select("r2-oni"), "select while a node is entered");
		Unchanged(() => map.Select("r1-apprentice"), "select the entered node again");
		Unchanged(() => map.Complete("r2-oni"), "complete another node");
		Do(map.Complete("r1-apprentice"), "complete row 1");
		Unchanged(() => map.Complete("r1-apprentice"), "complete twice");
		Unchanged(() => map.Select("r1-apprentice"), "go back to row 1");
		Unchanged(() => map.Select("r3-oni"), "skip row 2");

		Step(map, "r2-kitsune");
		Unchanged(() => map.Select("r3-kappa"), "no line from r2-kitsune to r3-kappa");
		Unchanged(() => map.Select("r2-oni"), "a second node in the same row");
		Assert.True(map.Visited.SequenceEqual(new[] { "r1-apprentice", "r2-kitsune" }), "visited in walk order");
		Assert.True(entered == 2 && completed == 2, "one event per real step");
	}

	[Test]
	public static void EveryWalkOfTheDefaultMapReachesTheBoss()
	{
		var g = Default();
		var walks = AllWalks(g);
		Assert.Equal(64, walks.Count, "walks through the default map (4 ways through rows 2-3, then four two-way forks)");
		var hashes = new HashSet<ulong>();
		var seen = new HashSet<string>();
		foreach (var walk in walks)
		{
			var map = new RunMap(g);
			var bossEntries = new List<string>(); // T1 hook: the lock point is entering the boss node
			map.NodeEntered += n => { if (n.Type == NodeTypeRegistry.Boss) bossEntries.Add(n.Id); };
			foreach (string id in walk)
			{
				Assert.True(!map.IsFinished, $"not finished before {id}");
				Do(map.Select(id), $"select {id}");
				if (id == walk[^1]) Assert.True(bossEntries.Count == 1 && !map.IsVisited(id), "boss entry is raised on select, before the fight is over");
				Do(map.Complete(id), $"complete {id}");
				seen.Add(id);
			}
			Assert.True(map.IsFinished, "finished at the end of the walk");
			Assert.Equal("boss-tanuki", map.Current, "ends on the Tanuki");
			Assert.Equal(1, bossEntries.Count, "the boss node is entered once");
			Assert.True(map.Visited.SequenceEqual(walk), "visited = the walk");
			Assert.True(walk.Select(id => g.Node(id).Row).SequenceEqual(Enumerable.Range(1, 9)), "one node per row, rows 1-8 then the boss (R1)");
			Assert.Equal(0, map.Reachable().Count, "nothing reachable after the end");
			Assert.True(!map.Select(g.StartNodes[0]).Ok, "no selecting after the end");
			Assert.True(hashes.Add(map.StateHash()), "each walk hashes differently");
		}
		Assert.Equal(g.Nodes.Count, seen.Count, "every node is on some walk");
	}

	[Test]
	public static void HashIsStableAndSeededWalksRepeat()
	{
		var g = Default();
		// The graph's fingerprint comes from the data, not from object identity: two loads agree.
		Assert.Equal(g.Fingerprint, Default().Fingerprint, "fingerprint across loads");
		Assert.Equal(new RunMap(g).StateHash(), new RunMap(Default()).StateHash(), "fresh hash across loads");

		(List<string> Path, List<ulong> Hashes) Walk(uint seed)
		{
			var rng = new SimRng(seed);
			var map = new RunMap(Default());
			var hashes = new List<ulong> { map.StateHash() };
			while (!map.IsFinished)
			{
				var options = map.Reachable();
				string pick = options[rng.Next(options.Count)];
				Do(map.Select(pick), $"select {pick}");
				hashes.Add(map.StateHash());
				Do(map.Complete(pick), $"complete {pick}");
				hashes.Add(map.StateHash());
			}
			return (map.Visited.ToList(), hashes);
		}

		var paths = new HashSet<string>();
		for (uint seed = 1; seed <= 40; seed++)
		{
			var a = Walk(seed);
			var b = Walk(seed);
			Assert.True(a.Path.SequenceEqual(b.Path), $"seed {seed}: same walk");
			Assert.True(a.Hashes.SequenceEqual(b.Hashes), $"seed {seed}: same hash after every step");
			Assert.Equal(a.Hashes.Count, a.Hashes.Distinct().Count(), $"seed {seed}: every step changes the hash");
			paths.Add(string.Join(">", a.Path));
		}
		Assert.True(paths.Count > 10, $"40 seeds take different routes, got {paths.Count}");
	}

	[Test]
	public static void RunStateOwnsTheMapAndHashesIt()
	{
		var pool = AbilityPool.LoadFixtures(RepoRoot);
		var g = Default();
		var plain = RunState.NewRun(pool);
		Assert.True(plain.Map is null, "a run without a map (the demo flow) has none");
		var run = RunState.NewRun(pool, g);
		var twin = RunState.NewRun(pool, Default());
		Assert.True(run.Map is not null && run.Map.Current is null, "new run starts before row 1");
		Assert.Equal(run.StateHash(), twin.StateHash(), "same run, same hash");
		Assert.True(run.StateHash() != plain.StateHash(), "the map is part of the run hash");

		var slots = run.Owned.Select(o => (o.Slot, o.Special.Data.Id, o.Special.Level)).ToArray();
		ulong before = run.StateHash();
		Do(run.Map!.Select("r1-apprentice"), "select");
		Assert.True(run.StateHash() != before, "entering a node changes the run hash");
		before = run.StateHash();
		Do(run.Map.Complete("r1-apprentice"), "complete");
		Assert.True(run.StateHash() != before, "completing a node changes the run hash");
		Assert.Equal(RunState.MaxHealth, run.Health, "the map never touches health (C1)");
		Assert.True(run.Owned.Select(o => (o.Slot, o.Special.Data.Id, o.Special.Level)).SequenceEqual(slots), "nor the slots");

		Step(twin.Map!, "r1-apprentice");
		Assert.Equal(run.StateHash(), twin.StateHash(), "same steps, same hash");
		run.AfterDuel(700);
		Assert.Equal(750, run.Health, "R3 still applies with a map attached");
		Assert.True(run.StateHash() != twin.StateHash(), "health is still hashed");
		run.SetMap(null);
		plain.AfterDuel(700);
		Assert.Equal(plain.StateHash(), run.StateHash(), "without a map the hash is the pre-map one");
	}

	[Test]
	public static void SnapshotIsPlainDataAndRestores()
	{
		var g = Default();
		var map = new RunMap(g);
		void RoundTrip(string when)
		{
			var s = map.Snapshot();
			var back = RunMap.Restore(Default(), new RunMapSnapshot(s.MapId, s.Visited.ToArray(), s.Current));
			Assert.Equal(map.StateHash(), back.StateHash(), $"{when}: hash");
			Assert.Equal(map.InNode, back.InNode, $"{when}: in node");
			Assert.True(map.Reachable().SequenceEqual(back.Reachable()), $"{when}: reachable");
		}
		RoundTrip("fresh");
		Do(map.Select("r1-apprentice"), "select");
		RoundTrip("in row 1");
		Do(map.Complete("r1-apprentice"), "complete");
		RoundTrip("after row 1");
		Step(map, "r2-oni");
		Do(map.Select("r3-kappa"), "select");
		RoundTrip("in row 3");

		void Bad(RunMapSnapshot s, string why)
		{
			try { RunMap.Restore(g, s); }
			catch (FormatException) { return; }
			throw new AssertFailed($"restored: {why}");
		}
		Bad(new RunMapSnapshot("other-map", Array.Empty<string>(), null), "another map's walk");
		Bad(new RunMapSnapshot(g.Id, new[] { "r1-apprentice", "r3-oni" }, "r3-oni"), "a walk that skips a row");
		Bad(new RunMapSnapshot(g.Id, new[] { "r1-apprentice" }, "r3-oni"), "a current node with no line to it");
		Bad(new RunMapSnapshot(g.Id, new[] { "r1-apprentice" }, null), "visited nodes but no current");
	}

	[Test]
	public static void LoaderRejectsBadMaps()
	{
		Assert.Equal(4, MapLoader.Parse(Json()).Nodes.Count, "the small map itself loads");

		void Bad(string json, string expect, string why)
		{
			try { MapLoader.Parse(json); }
			catch (FormatException e)
			{
				Assert.True(e.Message.Contains(expect), $"{why}: message should say '{expect}', got: {e.Message}");
				return;
			}
			throw new AssertFailed($"accepted: {why}");
		}
		const string D = "{\"id\":\"d\",\"row\":2,\"type\":\"duel\",\"yokai\":\"kappa\"}";

		Bad(Json(routes: SmallRoutes + ",{\"from\":\"b1\",\"to\":\"nowhere\"}"), "unknown node 'nowhere'", "route to an unknown node");
		Bad(Json(routes: SmallRoutes + ",{\"from\":\"ghost\",\"to\":\"c\"}"), "unknown node 'ghost'", "route from an unknown node");
		Bad(Json(routes: SmallRoutes + ",{\"from\":\"c\",\"to\":\"b1\"}"), "goes backwards", "route going backwards");
		Bad(Json(routes: SmallRoutes + ",{\"from\":\"b1\",\"to\":\"b2\"}"), "goes sideways", "route inside a row");
		Bad(Json(routes: SmallRoutes + ",{\"from\":\"a\",\"to\":\"c\"}"), "skips a row", "route skipping a row");
		Bad(Json(routes: SmallRoutes + ",{\"from\":\"a\",\"to\":\"b1\"}"), "listed twice", "the same route twice");
		Bad(Json(nodes: SmallNodes + "," + D, routes: SmallRoutes + ",{\"from\":\"d\",\"to\":\"c\"}"), "'d' is unreachable", "node no route leads to");
		Bad(Json(nodes: SmallNodes + "," + D, routes: SmallRoutes + ",{\"from\":\"a\",\"to\":\"d\"}"), "'d' is a dead end", "node with no way on");
		Bad(Json(nodes: SmallNodes + "," + D.Replace("\"d\"", "\"b1\"")), "'b1' is defined twice", "duplicate node id");
		Bad(Json(nodes: "", routes: ""), "no nodes", "no nodes, so no start and no end");
		Bad(Json(nodes: SmallNodes.Replace("\"type\":\"boss\"", "\"type\":\"shrine\"")), "type 'shrine' is not registered", "unregistered node type");
		Bad(Json(nodes: SmallNodes.Replace(",\"yokai\":\"oni\"", "")), "needs a yokai", "duel without a yokai (R2)");
		Bad(Json(nodes: SmallNodes.Replace(",\"elder\":true", "")), "elder = true", "elder node without the elder icon (Y3)");
		Bad(Json(head: "\"kind\":\"modifier\",\"id\":\"small\",\"schema_version\":1"), "kind must be 'run_map'", "wrong kind");
		Bad(Json(head: "\"kind\":\"run_map\",\"id\":\"small\",\"schema_version\":2"), "schema_version 2 is not supported", "a newer schema version");
		Bad(Json(head: "\"kind\":\"run_map\",\"id\":\"small\""), "schema_version is missing", "no schema version");
		Bad(Json(nodes: SmallNodes.Replace("\"row\":3", "\"row\":\"3\"")), "no integer row", "row that is not a number");

		// Every problem is reported at once, so a map is fixed in one pass.
		try { MapLoader.Parse(Json(nodes: SmallNodes + "," + D, routes: SmallRoutes + ",{\"from\":\"c\",\"to\":\"a\"}")); }
		catch (FormatException e)
		{
			Assert.True(e.Message.Contains("goes backwards") && e.Message.Contains("'d' is unreachable"), $"both problems listed: {e.Message}");
			return;
		}
		throw new AssertFailed("accepted a map with two problems");
	}

	/// <summary>A map built in code: stands in for the seeded generator that will implement IMapSource.</summary>
	sealed class CodeMapSource : IMapSource
	{
		readonly INodeTypeRegistry _types;
		public CodeMapSource(INodeTypeRegistry types) => _types = types;

		public MapGraph Load() => new("code-map", new[]
		{
			new MapNodeData("left", 10, "duel", "kitsune"),
			new MapNodeData("right", 10, "shrine", Label: "Shrine"),
			new MapNodeData("mid", 20, "shrine"),
			new MapNodeData("end-a", 35, "duel", "oni"),
			new MapNodeData("end-b", 35, "boss", "tanuki"),
		}, new[]
		{
			new MapRoute("left", "mid"), new MapRoute("right", "mid"), new MapRoute("mid", "end-a"), new MapRoute("mid", "end-b"),
		}, _types);
	}

	[Test]
	public static void NewNodeTypesAndLayoutsNeedNoMapChanges()
	{
		// An unregistered type fails; registering it is all it takes.
		try { new CodeMapSource(NodeTypeRegistry.Default()).Load(); throw new AssertFailed("loaded an unregistered type"); }
		catch (FormatException e) { Assert.True(e.Message.Contains("type 'shrine' is not registered"), e.Message); }

		var types = NodeTypeRegistry.Default().Register(new NodeType("shrine"));
		Assert.True(types.Types.SequenceEqual(new[] { "duel", "elder", "boss", "merchant", "dojo", "shrine" }), "registered types, in order");
		IMapSource source = new CodeMapSource(types);
		var g = source.Load();

		// Not R1's shape at all: two starts, two ends, rows 10/20/35. It loads and walks; only the R1 check objects.
		Assert.True(g.Rows.SequenceEqual(new[] { 10, 20, 35 }), "rows are data");
		Assert.True(g.StartNodes.SequenceEqual(new[] { "left", "right" }), "two starts");
		Assert.True(g.EndNodes.SequenceEqual(new[] { "end-a", "end-b" }), "two ends");
		Assert.True(MapRules.R1Problems(g).Count > 0, "and it is not an R1 map");
		Assert.Equal(4, AllWalks(g).Count, "walks");
		foreach (var walk in AllWalks(g))
		{
			var map = new RunMap(g);
			foreach (string id in walk) Step(map, id);
			Assert.True(map.IsFinished, $"finished: {string.Join(" > ", walk)}");
		}
		var m = new RunMap(g);
		Do(m.Select("right"), "start on the second start node");
		Assert.True(!m.Select("left").Ok, "the other start is gone once one is entered");

		// The payload is opaque to the map and reaches the node type's own code untouched.
		var withPayload = MapLoader.Parse(Json(nodes: SmallNodes.Replace("\"yokai\":\"oni\"}",
			"\"yokai\":\"oni\",\"label\":\"Bridge\",\"payload\":{\"scene\":\"bridge\",\"coins\":30,\"stock\":[\"a\",\"b\"]}}")));
		var b1 = withPayload.Node("b1");
		Assert.Equal("bridge", b1.PayloadString("scene"), "payload string");
		Assert.Equal(30, b1.PayloadInt("coins") ?? -1, "payload int");
		Assert.Equal(2, b1.Payload["stock"].GetArrayLength(), "payload keeps nested data");
		Assert.Equal("Bridge", b1.Label, "label");
		Assert.True(b1.PayloadString("missing") is null && withPayload.Node("a").Payload.Count == 0, "no payload = empty, never null");
		Assert.True(withPayload.Fingerprint != MapLoader.Parse(Json()).Fingerprint, "a payload edit changes the fingerprint");
	}

	[Test]
	public static void FileSourceNamesTheFileItRejects()
	{
		string tmp = Path.Combine(Path.GetTempPath(), "yok76-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tmp);
		try
		{
			string good = Path.Combine(tmp, FileMapSource.DefaultFile);
			File.WriteAllText(good, Json());
			Assert.Equal("small", FileMapSource.Default(tmp).Load().Id, "loads the folder's default file");

			string bad = Path.Combine(tmp, "bad.json");
			File.WriteAllText(bad, Json(routes: "{\"from\":\"a\",\"to\":\"b1\"},{\"from\":\"b1\",\"to\":\"c\"}"));
			try { new FileMapSource(bad).Load(); }
			catch (FormatException e)
			{
				Assert.True(e.Message.Contains("bad.json") && e.Message.Contains("'b2' is unreachable"), e.Message);
				return;
			}
			throw new AssertFailed("loaded a map with an unreachable node");
		}
		finally { Directory.Delete(tmp, true); }
	}
}
