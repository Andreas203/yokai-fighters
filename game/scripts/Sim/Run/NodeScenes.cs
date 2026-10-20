using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-77: everything a node's scene needs to run it, as plain data. Built by an <see cref="INodeSceneResolver"/>
/// from the map node and the node-scene table; the scene never reads the map or the table itself.
/// </summary>
/// <param name="Yokai">Who the map says waits there (R2); null for a node without one (merchant, dojo).</param>
/// <param name="Fighter">The kit that actually fights: <paramref name="Yokai"/>'s own, or the table's stand-in.</param>
/// <param name="Profile">Opponent behaviour profile id; null = the fighter's first profile.</param>
/// <param name="Tier">The Y4 row (apprentice, yokai, elder, tanuki) and its reaction delay in frames.</param>
/// <param name="Binds">Winning binds the yokai: R3 heal, A7 draft and the binding card.</param>
/// <param name="StandIn">The fighter is not the node's own yokai (no kit yet, or a node without a yokai).</param>
/// <param name="Notes">What the resolver substituted, for the log.</param>
public sealed record NodeContext(
	string NodeId, string Type, int Row, string? Yokai, bool Elder,
	string Fighter, string? Profile, string Tier, int ReactionFrames, bool Binds, bool StandIn,
	IReadOnlyDictionary<string, JsonElement> Payload, IReadOnlyList<string> Notes)
{
	/// <summary>YOK-85, R8: coins a won fight on this node pays (0 = none). Not positional, so older callers still build.</summary>
	public int Coins { get; init; }
	/// <summary>YOK-94: how the opponent's kit is built, from the opponent's <c>kit</c> in the table; null = the fighter's own files (<c>Kit.Load</c>); <see cref="TanukiKit.KitId"/> = Ryo's kit + the locked copy (T0).</summary>
	public string? Kit { get; init; }
	/// <summary>YOK-144: the stage scene (<c>res://scenes/stage/*.tscn</c>) the fight shows, from the table's <c>stage</c>; null = the fight scene's default (the bamboo grove). Presentation only: the sim never reads it.</summary>
	public string? Stage { get; init; }
	/// <summary>YOK-156, Y4: percent of its move damage this node's opponent deals, from the tier's <c>damage_dealt_pct</c> (the apprentice: 50); 100 when the tier names none.</summary>
	public int DamageDealtPct { get; init; } = SimConfig.FullDamagePct;
	/// <summary>YOK-174: the yokai whose own move files and behaviour profiles the opponent fights with
	/// (<see cref="YokaiFighters.Sim.Kit.LoadAs"/> on <see cref="Fighter"/>'s kit); the table's <c>opponents[yokai].plays_as</c>,
	/// else the opponent's own id when it has no fighter (the Oni, the Kappa), else <see cref="Fighter"/>. Null (older
	/// callers) = <see cref="Fighter"/>.</summary>
	public string? PlaysAs { get; init; }
	/// <summary>YOK-174: <see cref="PlaysAs"/> with its default.</summary>
	public string MovesAndProfilesOf => PlaysAs ?? Fighter;
}

/// <summary>The scene a node runs in (a <c>res://</c> path) and what it is handed.</summary>
public sealed record NodeScene(string Scene, NodeContext Context);

/// <summary>
/// YOK-77: node → scene + <see cref="NodeContext"/>. The run flow asks this for every node it enters and knows
/// nothing about node types; a generated or scripted resolver can replace the data table.
/// Throws <see cref="FormatException"/> for a node it cannot place.
/// </summary>
public interface INodeSceneResolver
{
	NodeScene Resolve(MapNodeData node);
}

/// <summary>
/// YOK-77: the data resolver, kind <c>node_scenes</c> (<c>data/map/node-scenes.json</c>,
/// <c>data/schema/node-scenes.schema.json</c>). Each setting is looked up from the most specific place to the least:
/// <list type="bullet">
/// <item>scene: the node's <c>payload.scene</c> → <c>nodes[id]</c> → <c>types[type]</c> → <c>default_scene</c>;</item>
/// <item>opponent: the node's yokai → <c>nodes[id].opponent</c> → <c>types[type].opponent</c> (the stand-in for a
/// node with no yokai); its kit is <c>opponents[yokai].fighter</c>, else <c>fallback_fighter</c> with a note;</item>
/// <item>tier (Y4): <c>nodes[id]</c> → <c>opponents[yokai]</c> → <c>types[type]</c>; frames from <c>tiers</c>;</item>
/// <item>plays as (YOK-174, whose move files and profiles): <c>opponents[yokai].plays_as</c> → the yokai when it has no fighter → the fighter;</item>
/// <item>profile: <c>nodes[id]</c> → <c>opponents[yokai]</c>; binds: <c>nodes[id]</c> → <c>types[type]</c> → false;</item>
/// <item>coins (R8): <c>nodes[id]</c> → <c>rows[row].types[type]</c> → <c>types[type]</c> → 0;</item>
/// <item>stage (YOK-144, presentation only): <c>nodes[id]</c> → <c>types[type]</c> → <c>default_stage</c> → null (the fight scene's own default).</item>
/// </list>
/// So a new scene for a type or one node, a yokai getting its own kit, or a different stand-in is a data edit.
/// </summary>
public sealed class NodeSceneTable : INodeSceneResolver
{
	public const string DefaultFile = "node-scenes.json";
	public const string Kind = "node_scenes";
	public const int SchemaVersion = 1;

	sealed record Entry(string? Scene, string? Opponent, string? Fighter, string? Tier, string? Profile, bool? Binds, int? Coins = null, string? Kit = null, string? Stage = null, string? PlaysAs = null);

	/// <summary>YOK-144: where a stage scene may live.</summary>
	public const string StagePrefix = "res://scenes/stage/";

	/// <summary>
	/// YOK-144: a stage path, the same as <c>STAGE_PATTERN</c> in <c>tools/validate_data.py</c>. Segments are ASCII letters,
	/// digits, '_' and '-' only, so '..', '//' and '\' cannot escape the stage folder. It ends in <c>\z</c>, not <c>$</c>
	/// (which also matches before a final newline). The schema's <c>$defs/stage</c> has to use <c>$</c>; this and the
	/// validator are the strict checks.
	/// </summary>
	public const string StagePattern = @"^res://scenes/stage/(?:[A-Za-z0-9_-]+/)*[A-Za-z0-9_-]+\.tscn\z";

	readonly Dictionary<string, Entry> _types, _nodes, _opponents;
	readonly Dictionary<string, int> _tiers;
	readonly Dictionary<string, int> _damagePct = new(); // YOK-156, Y4: tier → damage_dealt_pct
	readonly Dictionary<(int Row, string Type), int> _rowCoins = new(); // R8: what a row pays instead of its type's amount

	public string DefaultScene { get; }
	public string FallbackFighter { get; }
	/// <summary>YOK-144: <c>default_stage</c>, the stage for a node whose id and type name none; null = the fight scene's default.</summary>
	public string? DefaultStage { get; private init; }

	NodeSceneTable(string defaultScene, string fallbackFighter, Dictionary<string, int> tiers,
		Dictionary<string, Entry> types, Dictionary<string, Entry> nodes, Dictionary<string, Entry> opponents)
	{
		DefaultScene = defaultScene;
		FallbackFighter = fallbackFighter;
		_tiers = tiers;
		_types = types;
		_nodes = nodes;
		_opponents = opponents;
	}

	/// <summary>The table in a map folder (<c>ContentPaths.Data("map")</c> in the game).</summary>
	public static NodeSceneTable Default(string mapDir) => LoadFile(Path.Combine(mapDir, DefaultFile));

	public static NodeSceneTable LoadFile(string path)
	{
		try { return Parse(File.ReadAllText(path)); }
		catch (FormatException e) { throw new FormatException($"{path}: {e.Message}", e); }
		catch (JsonException e) { throw new FormatException($"{path}: {e.Message}", e); }
	}

	public static NodeSceneTable Parse(string json)
	{
		if (JsonNode.Parse(json) is not JsonObject o) throw new FormatException("a node-scene table must be a JSON object");
		string kind = Text(o, "kind", "the table") ?? "";
		if (kind != Kind) throw new FormatException($"kind must be '{Kind}', got '{kind}'");
		if (o["schema_version"] is not JsonValue v || !v.TryGetValue(out int version) || version != SchemaVersion)
			throw new FormatException($"node scenes: schema_version must be {SchemaVersion}");
		string scene = Text(o, "default_scene", "the table") ?? throw new FormatException("node scenes: default_scene is missing");
		string fallback = Text(o, "fallback_fighter", "the table") ?? throw new FormatException("node scenes: fallback_fighter is missing");

		var tiers = new Dictionary<string, int>();
		var damagePct = new Dictionary<string, int>(); // YOK-156: tiers that name a damage_dealt_pct
		foreach (var (name, node) in Section(o, "tiers"))
		{
			if (node is not JsonObject t || t["reaction_frames"] is not JsonValue f || !f.TryGetValue(out int frames) || frames < 1)
				throw new FormatException($"node scenes: tier '{name}' needs reaction_frames of 1 or more (Y4)");
			tiers[name] = frames;
			if (t.TryGetPropertyValue("damage_dealt_pct", out var d)) // present, even as null, must be a valid pct
			{
				if (d is not JsonValue dv || !dv.TryGetValue(out int pct) || pct < 1 || pct > SimConfig.FullDamagePct)
					throw new FormatException($"node scenes: tier '{name}' damage_dealt_pct must be a whole number from 1 to 100 (Y4)");
				damagePct[name] = pct;
			}
		}
		var table = new NodeSceneTable(scene, fallback, tiers, Entries(o, "types"), Entries(o, "nodes"), Entries(o, "opponents"))
		{
			DefaultStage = StagePath(Text(o, "default_stage", "the table"), "default_stage"),
		};
		foreach (var (name, pct) in damagePct) table._damagePct[name] = pct;
		foreach (var (where, e) in table._types.Select(p => ($"type '{p.Key}'", p.Value))
			.Concat(table._nodes.Select(p => ($"node '{p.Key}'", p.Value)))
			.Concat(table._opponents.Select(p => ($"opponent '{p.Key}'", p.Value))))
			if (e.Tier is { } tier && !tiers.ContainsKey(tier))
				throw new FormatException($"node scenes: {where} names tier '{tier}', which 'tiers' does not define");
		foreach (var (name, node) in Section(o, "rows"))
		{
			if (!int.TryParse(name, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int row) || row < 1)
				throw new FormatException($"node scenes: rows.{name}: a row is a whole number of 1 or more");
			if (node is not JsonObject r) throw new FormatException($"node scenes: rows.{name} must be an object");
			foreach (var (type, entry) in Section(r, "types"))
			{
				if (entry is not JsonObject e) throw new FormatException($"node scenes: rows.{name}.types.{type} must be an object");
				if (Coins(e, $"rows.{name}.types.{type}") is { } coins) table._rowCoins[(row, type)] = coins;
			}
		}
		return table;
	}

	public NodeScene Resolve(MapNodeData node)
	{
		Entry? byNode = _nodes.GetValueOrDefault(node.Id), byType = _types.GetValueOrDefault(node.Type);
		var notes = new List<string>();

		string? yokai = node.Yokai ?? byNode?.Opponent ?? byType?.Opponent;
		bool standIn = node.Yokai is null;
		if (node.Yokai is null && yokai is not null) notes.Add($"node '{node.Id}' ({node.Type}) has no yokai: '{yokai}' stands in (node-scenes.json)");
		Entry? foe = yokai is null ? null : _opponents.GetValueOrDefault(yokai);
		string fighter = foe?.Fighter ?? FallbackFighter;
		if (foe?.Fighter is null)
		{
			standIn = true;
			notes.Add(yokai is null
				? $"node '{node.Id}' ({node.Type}) names no opponent: fallback fighter '{fighter}'"
				: $"node '{node.Id}': no fighter for '{yokai}' yet: fallback fighter '{fighter}' (node-scenes.json 'opponents')");
		}

		Entry? own = node.Yokai is null ? null : _opponents.GetValueOrDefault(node.Yokai);
		string tier = byNode?.Tier ?? own?.Tier ?? foe?.Tier ?? byType?.Tier
			?? throw new FormatException($"node '{node.Id}': no Y4 tier for type '{node.Type}' in the node-scene table");
		string scene = node.PayloadString("scene") ?? byNode?.Scene ?? byType?.Scene ?? DefaultScene;

		var ctx = new NodeContext(node.Id, node.Type, node.Row, node.Yokai, node.Elder, fighter,
			byNode?.Profile ?? foe?.Profile, tier, _tiers[tier], byNode?.Binds ?? byType?.Binds ?? false, standIn,
			node.Payload, notes)
		{
			Kit = foe?.Kit,
			PlaysAs = foe?.PlaysAs ?? (foe?.Fighter is null && yokai is not null ? yokai : fighter), // YOK-174
			DamageDealtPct = _damagePct.TryGetValue(tier, out int pct) ? pct : SimConfig.FullDamagePct,
			Stage = byNode?.Stage ?? byType?.Stage ?? DefaultStage,
			Coins = byNode?.Coins ?? (_rowCoins.TryGetValue((node.Row, node.Type), out int rowCoins) ? rowCoins : byType?.Coins ?? 0),
		};
		return new NodeScene(scene, ctx);
	}

	static Dictionary<string, Entry> Entries(JsonObject root, string section)
	{
		var map = new Dictionary<string, Entry>();
		foreach (var (name, node) in Section(root, section))
		{
			if (node is not JsonObject e) throw new FormatException($"node scenes: {section}.{name} must be an object");
			string where = $"{section}.{name}";
			bool? binds = null;
			if (e["binds"] is { } b)
			{
				if (b is not JsonValue bv || !bv.TryGetValue(out bool flag)) throw new FormatException($"node scenes: {where}.binds must be true or false");
				binds = flag;
			}
			map[name] = new Entry(Text(e, "scene", where), Text(e, "opponent", where), Text(e, "fighter", where),
				Text(e, "tier", where), Text(e, "profile", where), binds, Coins(e, where), Text(e, "kit", where),
				StagePath(Text(e, "stage", where), where + ".stage"), Text(e, "plays_as", where));
			if (map[name].PlaysAs is not null && (section != "opponents" || map[name].Fighter is not null)) // YOK-174
				throw new FormatException($"node scenes: {where}.plays_as is only for an opponent with no 'fighter' of its own");
			if (map[name].Stage is not null && section == "opponents")
				throw new FormatException($"node scenes: {where}.stage: a stage belongs to a node or a node type, not an opponent");
			if (map[name].Kit is not null && (section != "opponents" || map[name].Fighter is null))
				throw new FormatException($"node scenes: {where}.kit is only for an opponent that names its 'fighter'");
			if (map[name].Kit is { } kit && kit != TanukiKit.KitId) throw new FormatException($"node scenes: {where}.kit '{kit}' is not a kit this build knows ('{TanukiKit.KitId}')");
		}
		return map;
	}

	/// <summary>YOK-144: a stage value is a scene under <see cref="StagePrefix"/>; null passes through.</summary>
	static string? StagePath(string? stage, string where)
	{
		if (stage is null) return null;
		if (!System.Text.RegularExpressions.Regex.IsMatch(stage, StagePattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant))
			throw new FormatException($"node scenes: {where} must be a scene under {StagePrefix} (got '{stage}')");
		return stage;
	}

	/// <summary>R8: an entry's <c>coins</c>, a whole number of 0 or more; null when it names none.</summary>
	static int? Coins(JsonObject e, string where)
	{
		if (e["coins"] is not { } c) return null;
		if (c is JsonValue v && v.GetValueKind() == JsonValueKind.Number && v.TryGetValue(out int coins) && coins >= 0) return coins;
		throw new FormatException($"node scenes: {where}.coins must be a whole number of 0 or more (R8)");
	}

	static IEnumerable<KeyValuePair<string, JsonNode?>> Section(JsonObject root, string name) => root[name] switch
	{
		null => Enumerable.Empty<KeyValuePair<string, JsonNode?>>(),
		JsonObject s => s,
		_ => throw new FormatException($"node scenes: '{name}' must be an object"),
	};

	static string? Text(JsonObject o, string name, string where)
	{
		if (o[name] is not { } n) return null;
		if (n is JsonValue v && v.TryGetValue(out string? s) && !string.IsNullOrEmpty(s)) return s;
		throw new FormatException($"node scenes: {where}: {name} must be a non-empty string");
	}
}
