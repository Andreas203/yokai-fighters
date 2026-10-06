using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-21: one kind "special" content file (data/schema/special.schema.json) as the sim needs it: the
/// slot its inputs name (K1/K2), the Lv 1 frame data, the Lv 2 tuning step, the Lv 3 evolution changes
/// and the EX version (C5). <see cref="Build"/> turns (level, EX) into the MoveData the fight runs by
/// applying the data's effects to the Lv 1 frame data, so levelling up (A3, A4) or EX never needs code.
/// Effects stack in order: Lv 2 tuning (level >= 2), Lv 3 evolution (level 3), then EX changes.
/// </summary>
public sealed class SpecialData
{
	public required string Id { get; init; }
	public string Name { get; init; } = "";
	public required SpecialSlot Slot { get; init; }
	/// <summary>EX meter cost from <c>ex.meter_cost</c>; 0 = this special has no EX version.</summary>
	public int ExCost { get; init; }
	public bool HasEx => ExCost > 0;

	// YOK-47 draft metadata (special.schema.json): who offers it (A8), rarity (A9), locked (A11), starter (A1), card (A12).
	public string Source { get; init; } = "ryo";
	public string Rarity { get; init; } = "common";
	public bool Locked { get; init; }
	public bool Starter { get; init; }
	public string CardPlain { get; init; } = "";
	public string CardFrames { get; init; } = "";
	/// <summary>Lv 3 evolution name and card text, for the Lv 2 -> 3 upgrade card.</summary>
	public string EvolutionName { get; init; } = "";
	public string EvolutionPlain { get; init; } = "";
	public string EvolutionFrames { get; init; } = "";
	/// <summary>Highest level the file defines (1-3).</summary>
	public int MaxLevel => Level3Changes != null && Level2Tuning != null ? 3 : Level2Tuning != null ? 2 : 1;
	/// <summary>The Lv 2 tuning effect (A4) as data, or null.</summary>
	public JsonNode? Level2Step => Level2Tuning;

	/// <summary>
	/// Modifier <c>applies_to.requires</c>: true when the Lv 1 frame data has the property (a truthy
	/// top-level field such as <c>low</c>, or any non-false entry under <c>properties</c>).
	/// </summary>
	public bool HasProperty(string name)
	{
		var fd = JsonNode.Parse(Level1Json)!.AsObject();
		if (fd[name] is JsonValue v && v.TryGetValue(out bool b) && b) return true;
		var p = fd["properties"]?[name];
		return p is not null && !(p is JsonValue pv && pv.TryGetValue(out bool pb) && !pb);
	}

	public required string Level1Json { get; init; }
	internal JsonNode? Level2Tuning { get; init; }
	internal JsonArray? Level3Changes { get; init; }
	internal JsonArray? ExChanges { get; init; }

	/// <summary>The move for this level (1-3), optionally EX. Throws FormatException on data the sim can't run.</summary>
	public MoveData Build(int level, bool ex) => Build(level, ex, null, null);

	/// <summary>
	/// YOK-47: as <see cref="Build(int,bool)"/>, then the attached modifier's effects (A2, A10) on top of the
	/// level and EX changes. <paramref name="defaults"/> fills fight-wide values a modifier scales when the
	/// move's data omits them (e.g. <c>meter_gain.on_block</c> = C5's 3).
	/// </summary>
	public MoveData Build(int level, bool ex, ModifierData? modifier, SimConfig? defaults)
	{
		if (level < 1 || level > 3) throw new ArgumentOutOfRangeException(nameof(level), "levels are 1-3 (A3)");
		if (ex && !HasEx) throw new InvalidOperationException($"{Id} has no EX version");
		var fd = JsonNode.Parse(Level1Json)!.AsObject();
		if (level >= 2)
		{
			if (Level2Tuning is null) throw new FormatException($"{Id}: levels.2.tuning is missing");
			Effects.Apply(fd, Level2Tuning, Id);
		}
		if (level >= 3)
		{
			if (Level3Changes is null) throw new FormatException($"{Id}: levels.3.evolution.changes is missing");
			foreach (var e in Level3Changes) Effects.Apply(fd, e!, Id);
		}
		if (ex)
			foreach (var e in ExChanges!) Effects.Apply(fd, e!, Id);
		if (modifier != null)
		{
			if (modifier.Effects.Any(e => e?["target"]?.GetValue<string>()?.StartsWith("meter_gain") == true))
				Effects.FillDefaults(fd, defaults ?? SimConfig.Default);
			foreach (var e in modifier.Effects) Effects.Apply(fd, e!, $"{Id}+{modifier.Id}");
		}
		var wrapper = new JsonObject { ["kind"] = "special", ["id"] = Id, ["frame_data"] = fd };
		return MoveLoader.Parse(wrapper.ToJsonString(), Id) with { Ex = ex };
	}
}

/// <summary>Reads kind "special" files (pure C#, no Godot) for slots A-D.</summary>
public static class SpecialLoader
{
	public static SpecialData Parse(string json, string? fallbackId = null)
	{
		var root = JsonNode.Parse(json)!.AsObject();
		string id = root["id"]?.GetValue<string>() ?? fallbackId ?? "unnamed";
		if (root["kind"]?.GetValue<string>() != "special") throw new FormatException($"{id}: not a kind \"special\" file");
		string? slotName = root["input"]?["kata"]?["slot"]?.GetValue<string>();
		SpecialSlot slot = slotName switch
		{
			"A" => SpecialSlot.A,
			"B" => SpecialSlot.B,
			"C" => SpecialSlot.C,
			"D" => SpecialSlot.D,
			_ => throw new FormatException($"{id}: input.kata.slot must be A-D (K1)"),
		};
		var levels = root["levels"] ?? throw new FormatException($"{id}: levels is required");
		var fd = levels["1"]?["frame_data"] ?? throw new FormatException($"{id}: levels.1.frame_data is required");
		var ex = root["ex"];
		var data = new SpecialData
		{
			Id = id,
			Name = root["name"]?.GetValue<string>() ?? id,
			Slot = slot,
			ExCost = ex?["meter_cost"]?.GetValue<int>() ?? 0,
			Level1Json = fd.ToJsonString(),
			Level2Tuning = levels["2"]?["tuning"]?.DeepClone(),
			Level3Changes = levels["3"]?["evolution"]?["changes"]?.DeepClone().AsArray(),
			ExChanges = ex?["changes"]?.DeepClone().AsArray(),
			Source = root["source"]?.GetValue<string>() ?? "ryo",
			Rarity = root["rarity"]?.GetValue<string>() ?? "common",
			Locked = root["locked"]?.GetValue<bool>() ?? false,
			Starter = root["starter"]?.GetValue<bool>() ?? false,
			CardPlain = root["card"]?["plain"]?.GetValue<string>() ?? "",
			CardFrames = root["card"]?["frames"]?.GetValue<string>() ?? "",
			EvolutionName = levels["3"]?["evolution"]?["name"]?.GetValue<string>() ?? "",
			EvolutionPlain = levels["3"]?["evolution"]?["card"]?["plain"]?.GetValue<string>() ?? "",
			EvolutionFrames = levels["3"]?["evolution"]?["card"]?["frames"]?.GetValue<string>() ?? "",
		};
		// Fail at load, not mid-fight (or at a level-up): build every level the file has, plain and EX.
		for (int level = 1; level <= data.MaxLevel; level++)
		{
			data.Build(level, false);
			if (data.HasEx) data.Build(level, true);
		}
		return data;
	}

	public static SpecialData LoadFile(string path) =>
		Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));

	/// <summary>Every kind "special" *.json in the folder, sorted by file name; other kinds are skipped.</summary>
	public static SpecialData[] LoadDirectory(string dir) =>
		Directory.Exists(dir)
			? Directory.GetFiles(dir, "*.json").OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
				.Where(f => MoveLoader.KindOf(f) == "special").Select(LoadFile).ToArray()
			: Array.Empty<SpecialData>();
}

/// <summary>
/// The data-only change vocabulary (common.schema.json effect: target, op add | mul_pct | set) applied to
/// a frame_data object. A target whose first segment isn't a frame_data field resolves under
/// <c>properties</c>, so <c>projectile.hits</c> means <c>properties.projectile.hits</c>.
/// Conditional effects (<c>when</c> other than always) belong to modifiers and aren't applied here.
/// </summary>
public static class Effects
{
	static readonly HashSet<string> FrameDataFields = new()
	{
		"startup", "active", "recovery", "damage", "hitstun", "blockstun", "pushback", "strength",
		"meter_gain", "hitboxes", "hurtboxes", "cancel_windows", "properties",
	};

	public static int MulPct(int n, int pct)
	{
		long p = (long)n * pct;
		return (int)(p >= 0 ? (p + 50) / 100 : -((-p + 50) / 100));
	}

	/// <summary>
	/// YOK-47: writes fight-wide defaults into a frame_data object where it omits them, so a modifier can
	/// scale a value the move never stated: <c>meter_gain.on_hit</c>/<c>on_block</c> (C5 attacker gains).
	/// </summary>
	public static void FillDefaults(JsonObject fd, SimConfig cfg)
	{
		if (fd["meter_gain"] is not JsonObject mg) fd["meter_gain"] = mg = new JsonObject();
		mg["on_hit"] ??= cfg.MeterOnHit;
		mg["on_block"] ??= cfg.MeterOnBlock;
	}

	public static void Apply(JsonObject fd, JsonNode effect, string id)
	{
		string target = effect["target"]?.GetValue<string>() ?? throw new FormatException($"{id}: effect without target");
		string op = effect["op"]?.GetValue<string>() ?? throw new FormatException($"{id}: effect without op");
		JsonNode value = effect["value"] ?? throw new FormatException($"{id}: effect without value");
		string when = effect["when"]?.GetValue<string>() ?? "always";
		// YOK-47: a condition that only restates its target (meter_gain.on_block when on_block) is unconditional.
		bool redundant = target.StartsWith("meter_gain.") && target["meter_gain.".Length..] == when;
		if (when != "always" && !redundant)
			throw new FormatException($"{id}: conditional effect ({target} when {when}) is not supported on levels or EX yet");

		var path = target.Split('.').ToList();
		if (!FrameDataFields.Contains(path[0])) path.Insert(0, "properties");
		JsonObject node = fd;
		for (int i = 0; i < path.Count - 1; i++)
		{
			if (node[path[i]] is not JsonObject next)
			{
				if (op != "set") throw new FormatException($"{id}: effect target {target} doesn't exist for '{op}'");
				next = new JsonObject();
				node[path[i]] = next;
			}
			node = next;
		}
		string leaf = path[^1];
		switch (op)
		{
			case "set":
				node[leaf] = value.DeepClone();
				break;
			case "add":
			case "mul_pct":
				if (node[leaf] is not JsonValue cur || !cur.TryGetValue(out int n))
					throw new FormatException($"{id}: effect '{op}' needs an integer at {target}");
				int v = value.GetValue<int>();
				// Integers only (F3). mul_pct rounds to nearest, half away from zero (YOK-47: Fox's Patience's
				// +25% of C5's 3 meter on block = 4; flooring would leave it at 3, no change).
				node[leaf] = op == "add" ? n + v : MulPct(n, v);
				break;
			default:
				throw new FormatException($"{id}: unknown effect op '{op}'");
		}
	}
}
