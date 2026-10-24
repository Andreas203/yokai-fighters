using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using YokaiFighters.Sim;

namespace YokaiFighters.Harness;

/// <summary>YOK-26: how the newcomer bot fights (P6: every reaction is to on-screen state). Units are gameplay units, times ticks.</summary>
public sealed record BotFightConfig
{
	public required int ReactionFrames { get; init; }
	public required int DecideEvery { get; init; }
	public required int MistakePct { get; init; }
	public required int BlockPct { get; init; }
	/// <summary>YOK-155: percent of wait and in-range walk plans held as a standing guard (back); 0 = none (the YOK-26 bot).</summary>
	public int IdleGuardPct { get; init; }
	public required int AntiAirPct { get; init; }
	public required InputBits AntiAirButton { get; init; }
	public required int PokeRange { get; init; }
	public required int ThrowRange { get; init; }
	public required int PreferredMin { get; init; }
	public required int PreferredMax { get; init; }
	public required int WalkTicks { get; init; }
	public required int BlockTicks { get; init; }
	public required int WaitTicks { get; init; }
	public required int WalkWeight { get; init; }
	public required int NormalWeight { get; init; }
	public required int SpecialWeight { get; init; }
	public required int ThrowWeight { get; init; }
	public required int JumpWeight { get; init; }
	public required int WaitWeight { get; init; }
	/// <summary>
	/// YOK-179: a safety cap only. There is no round timer (designer 2026-10-21): fights go to KO, and a fight with no KO
	/// by then is a stall (<see cref="FightRecord.Stalled"/>), never a loss. Not overridable per level (YOK-170).
	/// </summary>
	public required int MaxFightTicks { get; init; }
}

public enum RouteRule { PreferTypesElseRandom, Random }
/// <summary>R6 at the merchant: the free heal, the shop (YOK-95), the heal only when hurt (else the shop), or nothing.</summary>
public enum MerchantPlan { Heal, Skip, Shop, HealIfHurtElseShop }
public enum DojoPlan { Rest, Skip }

/// <summary>YOK-26: how the bot plays the run between fights.</summary>
public sealed record BotRunConfig
{
	public required RouteRule Route { get; init; }
	public required IReadOnlyList<string> PreferTypes { get; init; }
	public required IReadOnlyList<CardKind> RewardOrder { get; init; }
	public required MerchantPlan Merchant { get; init; }
	public required DojoPlan Dojo { get; init; }
	/// <summary><see cref="MerchantPlan.HealIfHurtElseShop"/> only: heal when health is below this percent of max, else shop. Proposed.</summary>
	public int HealBelowPct { get; init; } = 100;
	/// <summary>R9 estimate only: seconds a newcomer spends per node outside the fight (map, cards, stops).</summary>
	public required int MenuSecondsPerNode { get; init; }
}

/// <summary>
/// YOK-26: kind <c>harness_bot</c> (<c>data/harness/*.json</c>, <c>data/schema/harness-bot.schema.json</c>): the
/// player-side bot's skill and run habits as data, so the Sparring Partner tunes them without code.
/// </summary>
public sealed record BotConfig
{
	public const string Kind = "harness_bot";
	public const string DefaultId = "newcomer-kihon-bot";

	public required string Id { get; init; }
	public required ControlScheme Scheme { get; init; }
	public required BotFightConfig Fight { get; init; }
	public required BotRunConfig Run { get; init; }
	/// <summary>YOK-170: the skill level this config was built as (<see cref="BotLevels"/>); null for a bot file played on its own.</summary>
	public string? Level { get; init; }

	public static readonly IReadOnlyDictionary<string, InputBits> Buttons = new Dictionary<string, InputBits>
	{
		["light_punch"] = InputBits.LightPunch, ["medium_punch"] = InputBits.MediumPunch, ["heavy_punch"] = InputBits.HeavyPunch,
		["light_kick"] = InputBits.LightKick, ["medium_kick"] = InputBits.MediumKick, ["heavy_kick"] = InputBits.HeavyKick,
	};

	public static BotConfig LoadFile(string path) => Parse(File.ReadAllText(path));

	/// <summary>The default bot from a data folder (<c>data/harness/newcomer-kihon-bot.json</c>).</summary>
	public static BotConfig LoadDefault(string dataDir) => LoadFile(Path.Combine(dataDir, "harness", DefaultId + ".json"));

	/// <summary>Strict: every bad file is a <see cref="FormatException"/>, including bad JSON, a value of the wrong JSON kind
	/// (InvalidOperationException from the casts) and a key written twice (ArgumentException from JsonObject).</summary>
	public static BotConfig Parse(string json)
	{
		try { return ParseChecked(json); }
		catch (InvalidOperationException e) { throw new FormatException($"bot config: a value of the wrong kind: {e.Message}", e); }
		catch (System.Text.Json.JsonException e) { throw new FormatException($"bot config: not JSON: {e.Message}", e); }
		catch (ArgumentException e) { throw new FormatException($"bot config: a key written twice: {e.Message}", e); }
	}

	static BotConfig ParseChecked(string json)
	{
		var o = JsonNode.Parse(json)?.AsObject() ?? throw new FormatException("empty bot config");
		if ((string?)o["kind"] != Kind) throw new FormatException($"kind is not '{Kind}'");
		string id = (string?)o["id"] ?? throw new FormatException("id is missing");
		var f = o["fight"] ?? throw new FormatException($"{id}: fight is missing");
		var w = f["weights"] ?? throw new FormatException($"{id}: fight.weights is missing");
		var r = o["run"] ?? throw new FormatException($"{id}: run is missing");
		int I(JsonNode? parent, string name, int min, int max)
		{
			int v = parent?[name]?.GetValue<int>() ?? throw new FormatException($"{id}: {name} is missing");
			return v < min || v > max ? throw new FormatException($"{id}: {name} {v} outside {min}..{max}") : v;
		}
		string S(JsonNode? parent, string name) => (string?)parent?[name] ?? throw new FormatException($"{id}: {name} is missing");

		var fight = new BotFightConfig
		{
			ReactionFrames = I(f, "reaction_frames", 1, 120),
			DecideEvery = I(f, "decide_every", 1, 600),
			MistakePct = I(f, "mistake_pct", 0, 100),
			BlockPct = I(f, "block_pct", 0, 100),
			IdleGuardPct = f["idle_guard_pct"] is null ? 0 : I(f, "idle_guard_pct", 0, 100),
			AntiAirPct = I(f, "anti_air_pct", 0, 100),
			AntiAirButton = Buttons.TryGetValue(S(f, "anti_air_button"), out var aa) ? aa : throw new FormatException($"{id}: anti_air_button"),
			PokeRange = I(f, "poke_range", 0, 4000),
			ThrowRange = I(f, "throw_range", 0, 4000),
			PreferredMin = I(f, "preferred_min", 0, 4000),
			PreferredMax = I(f, "preferred_max", 0, 4000),
			WalkTicks = I(f, "walk_ticks", 1, 600),
			BlockTicks = I(f, "block_ticks", 1, 600),
			WaitTicks = I(f, "wait_ticks", 1, 600),
			WalkWeight = I(w, "walk", 0, 1000),
			NormalWeight = I(w, "normal", 0, 1000),
			SpecialWeight = I(w, "special", 0, 1000),
			ThrowWeight = I(w, "throw", 0, 1000),
			JumpWeight = I(w, "jump", 0, 1000),
			WaitWeight = I(w, "wait", 0, 1000),
			MaxFightTicks = I(f, "max_fight_ticks", 600, 216000),
		};
		if (fight.PreferredMin > fight.PreferredMax) throw new FormatException($"{id}: preferred_min is above preferred_max");
		if (fight.WalkWeight + fight.NormalWeight + fight.SpecialWeight + fight.ThrowWeight + fight.JumpWeight + fight.WaitWeight == 0)
			throw new FormatException($"{id}: every weight is 0");

		var route = r["route"] ?? throw new FormatException($"{id}: run.route is missing");
		var prefer = new List<string>();
		foreach (var t in route["prefer_types"]?.AsArray() ?? new JsonArray()) prefer.Add((string)t!);
		var order = new List<CardKind>();
		foreach (var k in r["reward_order"]?.AsArray() ?? throw new FormatException($"{id}: run.reward_order is missing"))
		{
			var kind = (string?)k switch
			{
				"upgrade" => CardKind.Upgrade, "new" => CardKind.NewSpecial, "modifier" => CardKind.Modifier,
				var x => throw new FormatException($"{id}: reward_order '{x}'"),
			};
			if (order.Contains(kind)) throw new FormatException($"{id}: reward_order lists '{k}' twice");
			order.Add(kind);
		}
		var run = new BotRunConfig
		{
			Route = S(route, "rule") switch
			{
				"prefer_types_else_random" => RouteRule.PreferTypesElseRandom, "random" => RouteRule.Random,
				var x => throw new FormatException($"{id}: route rule '{x}'"),
			},
			PreferTypes = prefer,
			RewardOrder = order,
			Merchant = S(r, "merchant") switch
			{
				"heal" => MerchantPlan.Heal, "skip" => MerchantPlan.Skip, "shop" => MerchantPlan.Shop,
				"heal_if_hurt_else_shop" => MerchantPlan.HealIfHurtElseShop,
				var x => throw new FormatException($"{id}: merchant '{x}'"),
			},
			HealBelowPct = r?["heal_below_pct"] is null ? 100 : I(r, "heal_below_pct", 0, 100),
			Dojo = S(r, "dojo") switch { "rest" => DojoPlan.Rest, "skip" => DojoPlan.Skip, var x => throw new FormatException($"{id}: dojo '{x}'") },
			MenuSecondsPerNode = I(r, "menu_seconds_per_node", 0, 600),
		};
		return new BotConfig
		{
			Id = id,
			Scheme = S(o, "scheme") == "kihon" ? ControlScheme.Kihon : throw new FormatException($"{id}: scheme must be 'kihon' (the bot presses Kihon specials, K2)"),
			Fight = fight,
			Run = run,
		};
	}
}
