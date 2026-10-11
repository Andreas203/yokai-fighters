using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-76: where a run's map comes from. <see cref="FileMapSource"/> reads a data file today; a seeded generator
/// implements this later (seed in its constructor). <see cref="RunMap"/> only ever sees the <see cref="MapGraph"/>.
/// </summary>
public interface IMapSource
{
	MapGraph Load();
}

/// <summary>A kind <c>run_map</c> file on disk (<c>data/map/&lt;id&gt;.json</c>).</summary>
public sealed class FileMapSource : IMapSource
{
	public const string DefaultFile = "default-map.json";

	public string Path { get; }
	public INodeTypeRegistry Types { get; }

	public FileMapSource(string path, INodeTypeRegistry? types = null)
	{
		Path = path;
		Types = types ?? NodeTypeRegistry.Default();
	}

	/// <summary>The default map in a map folder (<c>ContentPaths.Data("map")</c> in the game).</summary>
	public static FileMapSource Default(string mapDir, INodeTypeRegistry? types = null) =>
		new(System.IO.Path.Combine(mapDir, DefaultFile), types);

	public MapGraph Load() => MapLoader.LoadFile(Path, Types);
}

/// <summary>
/// YOK-76: System.Text.Json (no Godot) reader of kind <c>run_map</c> (<c>data/schema/run-map.schema.json</c>).
/// Throws <see cref="FormatException"/> on a file the game can't run; the graph checks are <see cref="MapGraph"/>'s.
/// </summary>
public static class MapLoader
{
	public const int SchemaVersion = 1;

	public static MapGraph LoadFile(string path, INodeTypeRegistry? types = null)
	{
		try { return Parse(File.ReadAllText(path), types); }
		catch (FormatException e) { throw new FormatException($"{path}: {e.Message}", e); }
		catch (JsonException e) { throw new FormatException($"{path}: {e.Message}", e); }
	}

	public static MapGraph Parse(string json, INodeTypeRegistry? types = null)
	{
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		if (root.ValueKind != JsonValueKind.Object) throw new FormatException("a run map must be a JSON object");
		string kind = Str(root, "kind", "the map") ?? "";
		if (kind != "run_map") throw new FormatException($"kind must be 'run_map', got '{kind}'");
		string id = Str(root, "id", "the map") ?? throw new FormatException("the map has no id");
		if (!root.TryGetProperty("schema_version", out var ver) || ver.ValueKind != JsonValueKind.Number || !ver.TryGetInt32(out int version))
			throw new FormatException($"run map '{id}': schema_version is missing");
		if (version != SchemaVersion)
			throw new FormatException($"run map '{id}': schema_version {version} is not supported (this build reads {SchemaVersion})");

		var nodes = new List<MapNodeData>();
		foreach (var n in Array(root, "nodes", id))
		{
			string nodeId = Str(n, "id", "a node") ?? throw new FormatException($"run map '{id}': a node has no id");
			string where = $"node '{nodeId}'";
			if (!n.TryGetProperty("row", out var row) || row.ValueKind != JsonValueKind.Number || !row.TryGetInt32(out int rowValue))
				throw new FormatException($"run map '{id}': {where} has no integer row");
			string type = Str(n, "type", where) ?? throw new FormatException($"run map '{id}': {where} has no type");
			bool elder = false;
			if (n.TryGetProperty("elder", out var e))
			{
				if (e.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
					throw new FormatException($"run map '{id}': {where}: elder must be true or false");
				elder = e.GetBoolean();
			}
			Dictionary<string, JsonElement>? payload = null;
			if (n.TryGetProperty("payload", out var p))
			{
				if (p.ValueKind != JsonValueKind.Object) throw new FormatException($"run map '{id}': {where}: payload must be an object");
				payload = new Dictionary<string, JsonElement>();
				foreach (var field in p.EnumerateObject()) payload[field.Name] = field.Value.Clone(); // outlives the document
			}
			nodes.Add(new MapNodeData(nodeId, rowValue, type, Str(n, "yokai", where), elder, Str(n, "label", where), payload));
		}

		var routes = new List<MapRoute>();
		foreach (var r in Array(root, "routes", id))
			routes.Add(new MapRoute(
				Str(r, "from", "a route") ?? throw new FormatException($"run map '{id}': a route has no 'from'"),
				Str(r, "to", "a route") ?? throw new FormatException($"run map '{id}': a route has no 'to'")));

		return new MapGraph(id, nodes, routes, types ?? NodeTypeRegistry.Default(), Str(root, "name", "the map"));
	}

	static string? Str(JsonElement obj, string name, string where)
	{
		if (obj.ValueKind != JsonValueKind.Object) throw new FormatException($"{where} must be a JSON object");
		if (!obj.TryGetProperty(name, out var v)) return null;
		if (v.ValueKind != JsonValueKind.String) throw new FormatException($"{where}: {name} must be a string");
		return v.GetString();
	}

	static JsonElement.ArrayEnumerator Array(JsonElement root, string name, string id)
	{
		if (!root.TryGetProperty(name, out var a) || a.ValueKind != JsonValueKind.Array)
			throw new FormatException($"run map '{id}': '{name}' must be a list");
		return a.EnumerateArray();
	}
}
