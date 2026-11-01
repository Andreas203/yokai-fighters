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
	/// move's data omits them (e.g. <c>meter_gain.on_block</c> = C5's 3, <c>pushback</c> = E5's 40 / 50).
	/// YOK-103: a modifier's conditional effects become variants of the same move, never code per modifier:
	/// <see cref="MoveData.StartVariants"/> (a condition read when the special starts) and
	/// <see cref="MoveData.OnHitVariant"/> / <see cref="MoveData.OnBlockVariant"/> (the move from its connect on).
	/// </summary>
	public MoveData Build(int level, bool ex, ModifierData? modifier, SimConfig? defaults)
	{
		var fd = LevelFrameData(level, ex);
		if (modifier is null) return Parse(fd, ex);
		if (HasEx) modifier.ExCostFor(ExCost); // fails at load if the modifier's EX cost leaves int32
		var cfg = defaults ?? SimConfig.Default;
		string tag = $"{Id}+{modifier.Id}";

		MoveData Make(StartConditions held)
		{
			var b = (JsonObject)fd.DeepClone();
			foreach (var e in modifier.FrameEffects)
				if (e.When == ModifierWhen.Always || e.Start != StartConditions.None && held.HasFlag(e.Start))
					Effects.Apply(b, e.Effect, tag, cfg);
			MoveData? Outcome(ModifierWhen when)
			{
				if (!modifier.FrameEffects.Any(e => e.When == when)) return null;
				var o = (JsonObject)b.DeepClone();
				foreach (var e in modifier.FrameEffects.Where(e => e.When == when)) Effects.Apply(o, e.Effect, tag, cfg);
				return Parse(o, ex);
			}
			return Parse(b, ex) with { OnHitVariant = Outcome(ModifierWhen.OnHit), OnBlockVariant = Outcome(ModifierWhen.OnBlock) };
		}

		var baseMove = Make(StartConditions.None);
		var used = modifier.StartMask;
		if (used == StartConditions.None) return baseMove;
		var variants = new List<MoveVariant>();
		for (int s = 1; s <= (int)used; s++)
			if ((s & ~(int)used) == 0) variants.Add(new MoveVariant((StartConditions)s, Make((StartConditions)s)));
		return baseMove with { StartVariants = variants };
	}

	/// <summary>Lv 1 frame data with the level steps and the EX changes applied, before any modifier.</summary>
	JsonObject LevelFrameData(int level, bool ex)
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
		return fd;
	}

	MoveData Parse(JsonObject fd, bool ex)
	{
		var wrapper = new JsonObject { ["kind"] = "special", ["id"] = Id, ["frame_data"] = fd };
		return Legal(MoveLoader.Parse(wrapper.ToJsonString(), Id) with { Ex = ex });
	}

	/// <summary>
	/// YOK-103: a special as built (after level, EX and modifier effects) must still be a move the schema would
	/// accept as written: the minimums of frame-data.schema.json, which effects can step past (startup add -100).
	/// tools/validate_data.py runs the same list (built_move_errors).
	/// </summary>
	public static MoveData Legal(MoveData m)
	{
		void Min(int value, int min, string what)
		{
			if (value < min) throw new FormatException($"{m.Id}: {what} is {value} after its effects; it must be at least {min}");
		}
		Min(m.Startup, 1, "startup");
		Min(m.Active, 0, "active");
		Min(m.Recovery, 0, "recovery");
		Min(m.Damage, 0, "damage");
		Min(m.Hitstun, 0, "hitstun");
		Min(m.Blockstun, 0, "blockstun");
		Min(m.HitPushback ?? 0, 0, "pushback.on_hit");
		Min(m.BlockPushback ?? 0, 0, "pushback.on_block");
		Min(m.MeterOnHit ?? 0, 0, "meter_gain.on_hit");
		Min(m.MeterOnBlock ?? 0, 0, "meter_gain.on_block");
		if (m.Projectile is { } p)
		{
			Min(p.Speed, 0, "projectile.speed");
			Min(p.Lifetime, 0, "projectile.lifetime");
			Min(p.AbsorbsProjectiles, 0, "projectile.absorbs_projectiles");
		}
		return m;
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
			ExCost = ex?["meter_cost"] is { } cost ? (Effects.Whole(cost, out int c) ? c : throw new FormatException($"{id}: ex.meter_cost must be a whole number")) : 0,
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
/// The data-only change vocabulary (common.schema.json <c>effect</c> / <c>modifierEffect</c>: target, op add |
/// mul_pct | set) applied to a frame_data object. A target whose first segment isn't a frame_data field
/// resolves under <c>properties</c>, so <c>projectile.hits</c> means <c>properties.projectile.hits</c>.
/// YOK-103: the accepted targets, ops and value shapes are exactly <see cref="Vocabulary"/>, and the schemas list
/// the same set. A schema can't say whether an effect still leaves a legal move on the special it lands on, so
/// tools/validate_data.py also applies every modifier to every special it claims (its own copy of <see cref="Apply"/>
/// and <see cref="SpecialData.Legal"/>); the two sides are held together by one shared case list,
/// game/tests/fixtures/modifier-gate/cases.json, which both test suites run. The loader stays the last word:
/// ModifierEffectTests builds the real pool from data/. A condition (<c>when</c>) is a modifier's:
/// <see cref="ModifierData"/> sorts its effects by condition before they reach <see cref="Apply"/>.
/// </summary>
public static class Effects
{
	/// <summary>
	/// YOK-103: an optional string field: absent = null; present it must be a JSON string (null, a number or an object
	/// is a FormatException naming <paramref name="what"/>, never an InvalidOperationException).
	/// </summary>
	public static string? Text(JsonObject parent, string key, string id, string what)
	{
		if (!parent.ContainsKey(key)) return null;
		return parent[key] is JsonValue v && v.GetValueKind() == JsonValueKind.String
			? v.GetValue<string>()
			: throw new FormatException($"{id}: {what} must be a string");
	}

	static readonly HashSet<string> FrameDataFields = new()
	{
		"startup", "active", "recovery", "damage", "hitstun", "blockstun", "pushback", "strength",
		"meter_gain", "hitboxes", "hurtboxes", "cancel_windows", "properties",
	};

	/// <summary>A target's value: an integer, a flag, the pushback pair, an armour window, the throw-break phase.</summary>
	public enum ValueKind { Int, Flag, IntPair, Armour, ThrowBreak }

	/// <summary>
	/// YOK-103: every target the loader accepts (resolved path, see <see cref="Resolve"/>) and its value kind.
	/// Int: add / mul_pct / set with an integer. IntPair (pushback): add / mul_pct on on_hit and on_block alike.
	/// Flag: set true / false. Armour: set { frames: [first, last], hits? }. ThrowBreak: set "startup".
	/// <c>ex.meter_cost</c> is modifier-only (<see cref="ModifierData.ExCostFor"/>), not frame data.
	/// </summary>
	public static readonly IReadOnlyDictionary<string, ValueKind> Vocabulary = new Dictionary<string, ValueKind>
	{
		["startup"] = ValueKind.Int, ["active"] = ValueKind.Int, ["recovery"] = ValueKind.Int,
		["damage"] = ValueKind.Int, ["hitstun"] = ValueKind.Int, ["blockstun"] = ValueKind.Int,
		["pushback"] = ValueKind.IntPair, ["pushback.on_hit"] = ValueKind.Int, ["pushback.on_block"] = ValueKind.Int,
		["meter_gain.on_hit"] = ValueKind.Int, ["meter_gain.on_block"] = ValueKind.Int,
		["properties.projectile.speed"] = ValueKind.Int, ["properties.projectile.hits"] = ValueKind.Int,
		["properties.projectile.absorbs_projectiles"] = ValueKind.Int, ["properties.projectile.max_active"] = ValueKind.Int,
		["properties.projectile.lifetime"] = ValueKind.Int,
		["properties.projectile.passes_projectiles"] = ValueKind.Flag, ["properties.projectile.angle_up"] = ValueKind.Flag,
		["properties.knockdown"] = ValueKind.Flag, ["properties.launch"] = ValueKind.Flag,
		["properties.counterhit_immune"] = ValueKind.Flag, ["properties.projectile_interrupt_immune"] = ValueKind.Flag,
		["properties.armour"] = ValueKind.Armour, ["properties.throw_break"] = ValueKind.ThrowBreak,
		["properties.pull_on_hit"] = ValueKind.Int, ["properties.heal_pct_max_health"] = ValueKind.Int,
		["properties.aftershock_delay"] = ValueKind.Int, // YOK-136: Oni Quake Lv 3 Aftershock (A5)
	};

	/// <summary>The modifier-only target that changes the special's EX cost (Ninefold Spark), not its frame data.</summary>
	public const string ExMeterCost = "ex.meter_cost";

	/// <summary>Dotted target to its frame_data path: a first segment that isn't a frame_data field goes under properties.</summary>
	public static List<string> Resolve(string target)
	{
		var path = target.Split('.').ToList();
		if (!FrameDataFields.Contains(path[0])) path.Insert(0, "properties");
		return path;
	}

	/// <summary>
	/// Throws FormatException unless the target is in <see cref="Vocabulary"/> (or, with <paramref name="allowExCost"/>,
	/// is <c>ex.meter_cost</c>), the op suits it and the value has its shape.
	/// </summary>
	public static void Check(JsonNode effect, string id, bool allowExCost = false)
	{
		string target = Str(effect, "target", id);
		string op = Str(effect, "op", id);
		JsonNode value = effect["value"] ?? throw new FormatException($"{id}: effect without value");
		if (op is not ("add" or "mul_pct" or "set")) throw new FormatException($"{id}: unknown effect op '{op}'");
		ValueKind kind;
		if (target == ExMeterCost)
		{
			if (!allowExCost) throw new FormatException($"{id}: only a modifier changes {ExMeterCost}");
			kind = ValueKind.Int;
		}
		else if (!Vocabulary.TryGetValue(string.Join('.', Resolve(target)), out kind))
			throw new FormatException($"{id}: effect target '{target}' is not one the game reads");
		bool isInt = Whole(value, out _);
		bool ok = kind switch
		{
			ValueKind.Int => isInt,
			ValueKind.IntPair => isInt && op != "set",
			ValueKind.Flag => op == "set" && value is JsonValue fv && fv.GetValueKind() is JsonValueKind.True or JsonValueKind.False,
			ValueKind.Armour => op == "set" && ArmourWindow.TryRead(value, out _),
			ValueKind.ThrowBreak => op == "set" && value is JsonValue sv && sv.GetValueKind() == JsonValueKind.String
				&& sv.GetValue<string>() == ThrowBreakStartup,
			_ => false,
		};
		if (!ok) throw new FormatException($"{id}: effect '{op}' on {target} needs {Describe(kind)}");
	}

	/// <summary>
	/// A whole number written any way JSON allows: 2, 2.0 and 1e1 all count (the rule the save format uses, YOK-87,
	/// and what JSON Schema's "integer" accepts, so loader and validator agree). 1.5 or a value past int range doesn't.
	/// </summary>
	public static bool Whole(JsonNode? node, out int value)
	{
		value = 0;
		if (node is not JsonValue v || v.GetValueKind() != JsonValueKind.Number) return false;
		if (v.TryGetValue(out value)) return true;
		if (!v.TryGetValue(out decimal d) || d != decimal.Truncate(d) || d < int.MinValue || d > int.MaxValue) return false;
		value = (int)d;
		return true;
	}

	/// <summary>
	/// <see cref="Whole(JsonNode?, out int)"/> for frame data read by MoveLoader: 30, 30.0 and 3e1 are 30; a fraction or
	/// a number outside int32 is a FormatException (tools/validate_data.py <c>whole()</c> is the same rule).
	/// </summary>
	public static int WholeInt(this JsonElement e)
	{
		if (e.ValueKind == JsonValueKind.Number)
		{
			if (e.TryGetInt32(out int i)) return i;
			if (e.TryGetDecimal(out decimal d) && d == decimal.Truncate(d) && d >= int.MinValue && d <= int.MaxValue) return (int)d;
		}
		throw new FormatException($"{e.GetRawText()} is not a whole number between {int.MinValue} and {int.MaxValue}");
	}

	/// <summary>The result of add / mul_pct, or a FormatException when it leaves int32 (no silent wrap-around).</summary>
	public static int Combine(string op, int n, int v, string id, string target)
	{
		long r = op == "add" ? (long)n + v : MulPctLong(n, v);
		if (r < int.MinValue || r > int.MaxValue)
			throw new FormatException($"{id}: effect '{op}' on {target} gives {r}, outside the whole numbers the fight holds");
		return (int)r;
	}

	static long MulPctLong(int n, int pct)
	{
		long p = (long)n * pct;
		return p >= 0 ? (p + 50) / 100 : -((-p + 50) / 100);
	}

	/// <summary>The one throw-break phase the data can name (Slippery Skin, AM12).</summary>
	public const string ThrowBreakStartup = "startup";

	static string Str(JsonNode effect, string name, string id) =>
		effect[name] is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>()
			: throw new FormatException($"{id}: effect without {name}");

	static string Describe(ValueKind k) => k switch
	{
		ValueKind.Int => "an integer",
		ValueKind.IntPair => "add or mul_pct with an integer",
		ValueKind.Flag => "set with true or false",
		ValueKind.Armour => "set with { \"frames\": [first, last], \"hits\": n }",
		_ => $"set with \"{ThrowBreakStartup}\"",
	};

	public static int MulPct(int n, int pct)
	{
		// Runtime use (heal, projectile climb) must never throw mid-fight: clamp. Loading uses Combine, which refuses.
		return (int)Math.Clamp(MulPctLong(n, pct), int.MinValue, int.MaxValue);
	}

	/// <summary>
	/// YOK-47/103: the fight-wide value a modifier scales when the move's data omits it: C5 attacker meter
	/// gains, E5 pushback; a heal or pull the move never had starts at 0. Null = none (the effect is rejected).
	/// </summary>
	public static int? Default(string resolvedPath, SimConfig cfg) => resolvedPath switch
	{
		"meter_gain.on_hit" => cfg.MeterOnHit,
		"meter_gain.on_block" => cfg.MeterOnBlock,
		"pushback.on_hit" => cfg.HitPushback,
		"pushback.on_block" => cfg.BlockPushback,
		"properties.pull_on_hit" or "properties.heal_pct_max_health" => 0,
		_ => null,
	};

	/// <summary>
	/// Applies one effect (checked against <see cref="Vocabulary"/>; unconditional only). <paramref name="defaults"/>
	/// (modifiers) fills a missing value from <see cref="Default"/> before add / mul_pct.
	/// </summary>
	public static void Apply(JsonObject fd, JsonNode effect, string id, SimConfig? defaults = null)
	{
		Check(effect, id);
		string target = effect["target"]!.GetValue<string>();
		string op = effect["op"]!.GetValue<string>();
		JsonNode value = effect["value"]!;
		string when = effect["when"]?.GetValue<string>() ?? "always";
		if (when != "always")
			throw new FormatException($"{id}: conditional effect ({target} when {when}) is only for modifiers, not levels or EX");

		var path = Resolve(target);
		if (Vocabulary[string.Join('.', path)] == ValueKind.IntPair)
		{
			// pushback: the effect applies to on_hit and on_block alike (Club Momentum: +50% both ways).
			foreach (var side in new[] { "on_hit", "on_block" })
				Apply(fd, new JsonObject { ["target"] = $"{target}.{side}", ["op"] = op, ["value"] = value.DeepClone() }, id, defaults);
			return;
		}
		JsonObject node = fd;
		for (int i = 0; i < path.Count - 1; i++)
		{
			if (node[path[i]] is not JsonObject next)
			{
				if (op != "set" && defaults is null) throw new FormatException($"{id}: effect target {target} doesn't exist for '{op}'");
				next = new JsonObject();
				node[path[i]] = next;
			}
			node = next;
		}
		string leaf = path[^1];
		// A modifier that scales one side of meter_gain / pushback states both (YOK-47): the other keeps the fight default.
		if (defaults != null && path.Count == 2 && path[0] is "meter_gain" or "pushback")
			foreach (var side in new[] { "on_hit", "on_block" })
				node[side] ??= Default($"{path[0]}.{side}", defaults);
		if (op == "set")
		{
			// Written back in the plain form MoveLoader reads (2.0 -> 2; armour frames and hits as integers).
			node[leaf] = Vocabulary[string.Join('.', path)] switch
			{
				ValueKind.Int when Whole(value, out int w) => JsonValue.Create(w),
				ValueKind.Armour when ArmourWindow.TryRead(value, out var a) =>
					new JsonObject { ["frames"] = new JsonArray(a!.First, a.Last), ["hits"] = a.Hits },
				_ => value.DeepClone(),
			};
			return;
		}
		int n;
		if (Whole(node[leaf], out int c)) n = c;
		else if (defaults != null && Default(string.Join('.', path), defaults) is int d) n = d;
		else throw new FormatException($"{id}: effect '{op}' needs an integer at {target}");
		Whole(value, out int v);
		// Integers only (F3). mul_pct rounds to nearest, half away from zero (E20: Fox's Patience's
		// +25% of C5's 3 meter on block = 4; flooring would leave it at 3, no change).
		node[leaf] = Combine(op, n, v, id, target);
	}
}
