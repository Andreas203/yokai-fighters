using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace YokaiFighters.Harness;

/// <summary>
/// YOK-170 (designer 2026-10-20; R5, Y4, P6): the test bot's skill levels, kind <c>harness_bot_levels</c>
/// (<c>data/harness/bot-levels.json</c>, <c>data/schema/harness-bot-levels.schema.json</c>). Each level names a bot id and
/// overrides fields of the base bot file (<c>data/harness/&lt;base&gt;.json</c>): any <c>fight</c> field (weights one by one)
/// and the run habits. An empty override plays the base bot exactly, so the newcomer level is today's bot. Not overridable:
/// <c>max_fight_ticks</c> and <c>menu_seconds_per_node</c>, the batch's measuring rules, so levels stay comparable.
/// Strict: an unknown key, a repeated level, a bad value (through <see cref="BotConfig.Parse"/>) is a <see cref="FormatException"/>.
/// </summary>
public sealed record BotLevels
{
	public const string Kind = "harness_bot_levels";
	public const string DefaultFile = "bot-levels.json";
	/// <summary>The level the harness plays when none is named: today's bot (YOK-26/154 comparisons hold).</summary>
	public const string DefaultLevel = "newcomer";

	static readonly string[] FightKeys =
	{
		"reaction_frames", "decide_every", "mistake_pct", "block_pct", "idle_guard_pct", "anti_air_pct", "anti_air_button",
		"poke_range", "throw_range", "preferred_min", "preferred_max", "walk_ticks", "block_ticks", "wait_ticks", "weights",
	};
	static readonly string[] WeightKeys = { "walk", "normal", "special", "throw", "jump", "wait" };
	static readonly string[] RunKeys = { "route", "reward_order", "merchant", "heal_below_pct", "dojo" };
	static readonly string[] LevelKeys = { "level", "bot_id", "status", "fight", "run", "notes" };
	// \z, not $: .NET's $ also matches before a final newline, so "mixed\n" would pass.
	static readonly Regex Name = new(@"^[a-z][a-z0-9-]*\z");

	public required string Id { get; init; }
	public required string Base { get; init; }
	/// <summary>In file order; each config carries its <see cref="BotConfig.Level"/>.</summary>
	public required IReadOnlyList<BotConfig> Levels { get; init; }

	public BotConfig this[string level] =>
		FindLevel(level) ?? throw new KeyNotFoundException($"{Id}: no level '{level}' (levels: {string.Join(", ", LevelNames())})");

	BotConfig? FindLevel(string level)
	{
		foreach (var b in Levels) if (b.Level == level) return b;
		return null;
	}

	public IEnumerable<string> LevelNames()
	{
		foreach (var b in Levels) yield return b.Level!;
	}

	/// <summary><c>data/harness/bot-levels.json</c>, with its base bot from the same folder.</summary>
	public static BotLevels LoadDefault(string dataDir) => LoadFile(Path.Combine(dataDir, "harness", DefaultFile));

	public static BotLevels LoadFile(string path)
	{
		string dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
		return Parse(File.ReadAllText(path), b => File.ReadAllText(Path.Combine(dir, b + ".json")));
	}

	/// <param name="baseJson">The base bot file's text by its id.</param>
	public static BotLevels Parse(string json, Func<string, string> baseJson)
	{
		// A value of the wrong JSON kind (e.g. "base": 5) throws InvalidOperationException from the casts below, bad JSON a
		// JsonException: both are a bad file, so a FormatException like every other check.
		try { return ParseChecked(json, baseJson); }
		catch (InvalidOperationException e) { throw new FormatException($"bot levels: a value of the wrong kind: {e.Message}", e); }
		catch (System.Text.Json.JsonException e) { throw new FormatException($"bot levels: not JSON: {e.Message}", e); }
		// A key written twice in one object: System.Text.Json's JsonObject throws ArgumentException ("same key").
		catch (ArgumentException e) { throw new FormatException($"bot levels: a key written twice: {e.Message}", e); }
	}

	static BotLevels ParseChecked(string json, Func<string, string> baseJson)
	{
		var o = JsonNode.Parse(json)?.AsObject() ?? throw new FormatException("empty bot levels file");
		if ((string?)o["kind"] != Kind) throw new FormatException($"kind is not '{Kind}'");
		string id = (string?)o["id"] ?? throw new FormatException("id is missing");
		string baseId = (string?)o["base"] ?? throw new FormatException($"{id}: base is missing");
		var levels = o["levels"] as JsonArray ?? throw new FormatException($"{id}: levels is missing");
		if (levels.Count == 0) throw new FormatException($"{id}: no levels");
		if (!Name.IsMatch(baseId)) throw new FormatException($"{id}: bad base '{baseId}'");
		string baseText;
		try { baseText = baseJson(baseId); }
		catch (IOException e) { throw new FormatException($"{id}: base '{baseId}' cannot be read: {e.Message}", e); }
		var baseBot = BotConfig.Parse(baseText); // the base file itself must be a good bot
		if (baseBot.Id != baseId) throw new FormatException($"{id}: base file's id is '{baseBot.Id}', not '{baseId}'");

		var built = new List<BotConfig>();
		var names = new HashSet<string>(StringComparer.Ordinal);
		var botIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var node in levels)
		{
			var lv = node as JsonObject ?? throw new FormatException($"{id}: a level is not an object");
			Only(lv, LevelKeys, $"{id}: level");
			string level = (string?)lv["level"] ?? throw new FormatException($"{id}: a level has no 'level' name");
			string where = $"{id}: level '{level}'";
			if (!Name.IsMatch(level) || level is "all" or "mixed") throw new FormatException($"{where}: bad level name");
			if (!names.Add(level)) throw new FormatException($"{where}: listed twice");
			string botId = (string?)lv["bot_id"] ?? throw new FormatException($"{where}: bot_id is missing");
			if (!Name.IsMatch(botId)) throw new FormatException($"{where}: bad bot_id '{botId}'");
			if (!botIds.Add(botId)) throw new FormatException($"{where}: bot_id '{botId}' is used twice");
			if (botId == id + "-mixed") throw new FormatException($"{where}: bot_id '{botId}' is the mixed batch's results name");
			if ((string?)lv["status"] is not ("final" or "proposed")) throw new FormatException($"{where}: status must be 'final' or 'proposed'");

			var merged = JsonNode.Parse(baseText)!.AsObject();
			merged["id"] = botId;
			if (lv["fight"] is { } f)
			{
				var fo = f as JsonObject ?? throw new FormatException($"{where}: fight is not an object");
				Only(fo, FightKeys, $"{where}: fight");
				var target = merged["fight"]!.AsObject();
				foreach (var (k, v) in fo)
				{
					if (k == "weights")
					{
						var wo = v as JsonObject ?? throw new FormatException($"{where}: fight.weights is not an object");
						Only(wo, WeightKeys, $"{where}: fight.weights");
						foreach (var (wk, wv) in wo) target["weights"]![wk] = Value(wv, $"{where}: fight.weights.{wk}");
					}
					else target[k] = Value(v, $"{where}: fight.{k}");
				}
			}
			if (lv["run"] is { } r)
			{
				var ro = r as JsonObject ?? throw new FormatException($"{where}: run is not an object");
				Only(ro, RunKeys, $"{where}: run");
				var target = merged["run"]!.AsObject();
				foreach (var (k, v) in ro) target[k] = Value(v, $"{where}: run.{k}");
			}
			BotConfig bot;
			try { bot = BotConfig.Parse(merged.ToJsonString()); }
			catch (Exception e) when (e is FormatException or InvalidOperationException) { throw new FormatException($"{where}: {e.Message}", e); }
			built.Add(bot with { Level = level });
		}
		return new BotLevels { Id = id, Base = baseId, Levels = built };
	}

	static void Only(JsonObject o, string[] allowed, string where)
	{
		foreach (var (k, _) in o)
			if (Array.IndexOf(allowed, k) < 0) throw new FormatException($"{where}: unknown or fixed key '{k}'");
	}

	static JsonNode Value(JsonNode? v, string where) => v?.DeepClone() ?? throw new FormatException($"{where} is null");
}
