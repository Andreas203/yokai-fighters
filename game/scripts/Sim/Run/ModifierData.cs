using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-103: a modifier effect's <c>when</c> (common.schema.json <c>modifierCondition</c>). Always applies at build;
/// OnHit / OnBlock apply from the move's connect on (<see cref="MoveData.Outcome"/>); the rest are read once,
/// when the special starts (<see cref="StartConditions"/>).
/// </summary>
public enum ModifierWhen { Always, OnHit, OnBlock, FirstUseEachRound, RyoHealthBelow30Pct, HoldingUp, ChargeReleased }

/// <summary>YOK-103: start-of-special conditions, as bits: which <see cref="MoveData.StartVariants"/> entry plays.</summary>
[Flags]
public enum StartConditions
{
	None = 0,
	/// <summary>The special's first start this round (Illusion Cloak).</summary>
	FirstUseEachRound = 1,
	/// <summary>The owner's health is below 30% of max (Rage Ember; only Ryo carries modifiers, T4).</summary>
	OwnerHealthBelow30Pct = 2,
	/// <summary>Up (7/8/9) held on the tick the special starts (Trickster's Angle).</summary>
	HoldingUp = 4,
	/// <summary>YOK-171 (AM11): the special started on the release of a charge hold inside the modifier's window.</summary>
	ChargeReleased = 8,
}

/// <summary>One parsed modifier effect: the common effect without its <c>when</c>, and that condition.</summary>
public sealed record ModifierEffect(JsonObject Effect, ModifierWhen When)
{
	public StartConditions Start => When switch
	{
		ModifierWhen.FirstUseEachRound => StartConditions.FirstUseEachRound,
		ModifierWhen.RyoHealthBelow30Pct => StartConditions.OwnerHealthBelow30Pct,
		ModifierWhen.HoldingUp => StartConditions.HoldingUp,
		ModifierWhen.ChargeReleased => StartConditions.ChargeReleased,
		_ => StartConditions.None,
	};
	public string Target => Effect["target"]!.GetValue<string>();
}

/// <summary>
/// YOK-47: one kind "modifier" file (data/schema/modifier.schema.json). A modifier is data only (A10): its
/// effects use the same vocabulary as Lv 2 / Lv 3 / EX (<see cref="Effects"/>) and are applied on top of the
/// special it is attached to (<see cref="SpecialData.Build(int,bool,ModifierData,SimConfig)"/>), so no
/// modifier has code of its own. YOK-103: an effect may carry a <c>when</c> (<see cref="ModifierWhen"/>), and
/// <c>ex.meter_cost</c> changes the special's EX cost (<see cref="ExCostFor"/>).
/// </summary>
public sealed class ModifierData
{
	public required string Id { get; init; }
	public string Name { get; init; } = "";
	public string Source { get; init; } = "";
	public string Rarity { get; init; } = "common";
	public bool Locked { get; init; }
	/// <summary><c>applies_to.requires</c>: frame-data properties a special must have (empty = any special).</summary>
	public string[] Requires { get; init; } = Array.Empty<string>();
	/// <summary>YOK-171: <c>applies_to.excludes</c>: frame-data properties a special must not have (Sumo Stance: no projectile).</summary>
	public string[] Excludes { get; init; } = Array.Empty<string>();
	/// <summary>YOK-171 (AM11): the <c>charge</c> object (hold before the special starts), or null.</summary>
	public ChargeSpec? Charge { get; init; }
	/// <summary>The file's effects as written (card text, fingerprints).</summary>
	public required JsonArray Effects { get; init; }
	/// <summary>Effects on the move's frame data (everything but <c>ex.meter_cost</c>), each with its condition.</summary>
	public IReadOnlyList<ModifierEffect> FrameEffects { get; init; } = Array.Empty<ModifierEffect>();
	/// <summary><c>ex.meter_cost</c> effects (always unconditional).</summary>
	public IReadOnlyList<JsonObject> ExCostEffects { get; init; } = Array.Empty<JsonObject>();
	public string CardPlain { get; init; } = "";
	public string CardFrames { get; init; } = "";

	/// <summary>Every start condition one of the effects reads.</summary>
	public StartConditions StartMask => FrameEffects.Aggregate(Charge is null ? StartConditions.None : StartConditions.ChargeReleased, (m, e) => m | e.Start);

	/// <summary>True when the special has every required property and none of the excluded ones (applies_to).</summary>
	public bool AppliesTo(SpecialData special) => Requires.All(special.HasProperty) && !Excludes.Any(special.HasProperty);

	/// <summary>The special's EX cost with this modifier (Ninefold Spark: set 50); never below 0.</summary>
	public int ExCostFor(int baseCost)
	{
		int cost = baseCost;
		foreach (var e in ExCostEffects)
		{
			global::YokaiFighters.Sim.Effects.Whole(e["value"], out int v); // checked at Parse
			string op = e["op"]!.GetValue<string>();
			cost = op == "set" ? v : global::YokaiFighters.Sim.Effects.Combine(op, cost, v, Id, global::YokaiFighters.Sim.Effects.ExMeterCost);
		}
		return Math.Max(0, cost);
	}
}

public static class ModifierLoader
{
	/// <summary>The <c>when</c> values the loader accepts (the schema's <c>modifierCondition</c> enum).</summary>
	public static readonly IReadOnlyDictionary<string, ModifierWhen> Conditions = new Dictionary<string, ModifierWhen>
	{
		["always"] = ModifierWhen.Always,
		["on_hit"] = ModifierWhen.OnHit,
		["on_block"] = ModifierWhen.OnBlock,
		["first_use_each_round"] = ModifierWhen.FirstUseEachRound,
		["ryo_health_below_30pct"] = ModifierWhen.RyoHealthBelow30Pct,
		["holding_up"] = ModifierWhen.HoldingUp,
		["charge_released"] = ModifierWhen.ChargeReleased,
	};

	/// <summary>
	/// YOK-171 (AM11): the modifier-only target that turns on the charge dash. Its one shape is
	/// <c>{ "target": "properties.forward_travel", "op": "set", "value": "charge_dash", "when": "charge_released" }</c>;
	/// it changes no frame data (frame data's own <c>forward_travel</c> is a distance nothing reads): the move carries the
	/// <see cref="ChargeSpec"/> (<see cref="MoveData.Charge"/>) and a release inside its window dashes at <see cref="ChargeSpec.SpeedFor"/>.
	/// </summary>
	public const string ForwardTravel = "properties.forward_travel";
	public const string ChargeDashValue = "charge_dash";

	public static ModifierData Parse(string json, string? fallbackId = null)
	{
		var root = JsonNode.Parse(json)!.AsObject();
		string id = root["id"]?.GetValue<string>() ?? fallbackId ?? "unnamed";
		if (root["kind"]?.GetValue<string>() != "modifier") throw new FormatException($"{id}: not a kind \"modifier\" file");
		var effects = root["effects"]?.DeepClone() as JsonArray;
		if (effects is null || effects.Count == 0) throw new FormatException($"{id}: effects must list at least one effect");
		var frame = new List<ModifierEffect>();
		var exCost = new List<JsonObject>();
		ChargeSpec? charge = root.ContainsKey("charge") ? ChargeSpec.Parse(root["charge"], id) : null;
		bool chargeDash = false, chargeRead = false;
		foreach (var node in effects)
		{
			if (node is not JsonObject e) throw new FormatException($"{id}: an effect must be an object");
			string w = Effects.Text(e, "when", id, "an effect's when") ?? "always";
			if (!Conditions.TryGetValue(w, out var when)) throw new FormatException($"{id}: condition '{w}' is not one the fight reads");
			if (when == ModifierWhen.ChargeReleased) chargeRead = true;
			if (e["target"] is JsonValue tv && tv.GetValueKind() == System.Text.Json.JsonValueKind.String
				&& tv.GetValue<string>() == ForwardTravel) // written in full, as the schema's effectTarget lists it
			{
				// YOK-171: the charge dash switch, not a frame-data change (see ForwardTravel).
				bool shape = e["op"] is JsonValue ov && ov.GetValueKind() == System.Text.Json.JsonValueKind.String && ov.GetValue<string>() == "set"
					&& e["value"] is JsonValue vv && vv.GetValueKind() == System.Text.Json.JsonValueKind.String && vv.GetValue<string>() == ChargeDashValue
					&& when == ModifierWhen.ChargeReleased && e.Count == 4;
				if (!shape) throw new FormatException($"{id}: {ForwardTravel} takes only set \"{ChargeDashValue}\" when charge_released");
				chargeDash = true;
				continue;
			}
			Effects.Check(e, id, allowExCost: true);
			var bare = (JsonObject)e.DeepClone();
			bare.Remove("when");
			if (bare["target"]!.GetValue<string>() == Effects.ExMeterCost)
			{
				if (when != ModifierWhen.Always) throw new FormatException($"{id}: {Effects.ExMeterCost} can't be conditional");
				exCost.Add(bare);
			}
			else frame.Add(new ModifierEffect(bare, when));
		}
		foreach (var key in new[] { "excludes" }) // YOK-171; requires stays as it was (probes use an unmatched one on purpose)
			if (root["applies_to"]?[key] is JsonArray props && props.Any(p => p is not JsonValue v || v.GetValueKind() != System.Text.Json.JsonValueKind.String
				|| v.GetValue<string>() is not ("projectile" or "throw" or "armour" or "low" or "anti_air")))
				throw new FormatException($"{id}: applies_to.{key} lists a property the schema doesn't");
		if (chargeRead && charge is null) throw new FormatException($"{id}: charge_released needs a charge object");
		if (charge != null && !chargeDash) throw new FormatException($"{id}: a charge object needs the {ForwardTravel} charge_dash effect");
		return new ModifierData
		{
			Id = id,
			Name = root["name"]?.GetValue<string>() ?? id,
			Source = root["source"]?.GetValue<string>() ?? "",
			Rarity = root["rarity"]?.GetValue<string>() ?? "common",
			Locked = root["locked"]?.GetValue<bool>() ?? false,
			Requires = root["applies_to"]?["requires"]?.AsArray().Select(n => n!.GetValue<string>()).ToArray() ?? Array.Empty<string>(),
			Excludes = root["applies_to"]?["excludes"]?.AsArray().Select(n => n!.GetValue<string>()).ToArray() ?? Array.Empty<string>(),
			Charge = charge,
			Effects = effects,
			FrameEffects = frame,
			ExCostEffects = exCost,
			CardPlain = root["card"]?["plain"]?.GetValue<string>() ?? "",
			CardFrames = root["card"]?["frames"]?.GetValue<string>() ?? "",
		};
	}

	public static ModifierData LoadFile(string path) =>
		Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));

	/// <summary>Every kind "modifier" *.json in the folder, sorted by file name; other kinds are skipped.</summary>
	public static ModifierData[] LoadDirectory(string dir) =>
		Directory.Exists(dir)
			? Directory.GetFiles(dir, "*.json").OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
				.Where(f => MoveLoader.KindOf(f) == "modifier").Select(LoadFile).ToArray()
			: Array.Empty<ModifierData>();
}
