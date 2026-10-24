using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-198 (V8, A5): one kind <c>level_preset</c> file (data/schema/level-preset.schema.json): how one special looks
/// at one level. Presentation only; the sim never reads it. Colours are 0xRRGGBB.
/// </summary>
public sealed record LevelPreset(string Id, string Special, int Level, uint Colour, SparkPreset Spark, string Status = "final");

/// <summary>The preset's hit spark. Null fields keep the default spark's value (<see cref="HitSparkStyle"/>).</summary>
public sealed record SparkPreset(uint? Colour = null, int? ScalePct = null, int? Frames = null,
	string? HitTexture = null, string? BlockTexture = null, string? CounterTexture = null, int SheetColumns = 1, bool Tint = true)
{
	public static readonly SparkPreset None = new();

	/// <summary>The image this preset names for an outcome (relative to game/), or null.</summary>
	public string? TextureFor(HitSparkKind kind) => kind switch
	{
		HitSparkKind.Block => BlockTexture,
		HitSparkKind.Counter => CounterTexture,
		_ => HitTexture,
	};
}

/// <summary>
/// Every level preset, indexed by (special, level). <see cref="LoadDirectory"/> reads data/presets/ (an absent or
/// empty folder = <see cref="Empty"/>: every hit gets the default spark). Throws <see cref="FormatException"/>
/// naming the file and field on anything the schema refuses, so bad data fails at load, never mid-fight.
/// </summary>
public sealed class LevelPresetLibrary
{
	public const string Kind = "level_preset";
	public static readonly LevelPresetLibrary Empty = new(Array.Empty<LevelPreset>());

	// \z, not $: .NET's $ also matches before a trailing newline ("a\n" would pass).
	static readonly Regex IdPattern = new(@"\A[a-z0-9]+(-[a-z0-9]+)*\z", RegexOptions.CultureInvariant);
	static readonly Regex HexPattern = new(@"\A#[0-9A-Fa-f]{6}\z", RegexOptions.CultureInvariant);
	static readonly Regex TexturePattern = new(@"\A[a-z0-9_-]+(/[a-z0-9_-]+)*\.(png|webp)\z", RegexOptions.CultureInvariant);
	static readonly HashSet<string> TopFields = new() { "kind", "id", "special", "level", "status", "rules", "colour", "spark", "notes" };
	static readonly HashSet<string> SparkFields = new() { "colour", "scale_pct", "frames", "textures", "sheet_columns", "tint" };
	static readonly HashSet<string> TextureFields = new() { "hit", "block", "counter" };

	private readonly Dictionary<(string, int), LevelPreset> _byLevel = new();

	public IReadOnlyList<LevelPreset> All { get; }

	public LevelPresetLibrary(IEnumerable<LevelPreset> presets)
	{
		var list = presets.ToList();
		foreach (var p in list)
			if (p.Level is < 1 or > 3)
				throw new FormatException($"level preset '{p.Id}': level {p.Level} is outside 1-3 (A3)");
			else if (!_byLevel.TryAdd((p.Special, p.Level), p))
				throw new FormatException($"level presets '{_byLevel[(p.Special, p.Level)].Id}' and '{p.Id}' both dress '{p.Special}' Lv {p.Level}");
		All = list;
	}

	/// <summary>The preset for this special at this level, or null (the default spark).</summary>
	public LevelPreset? For(string? special, int level) =>
		special != null && _byLevel.TryGetValue((special, level), out var p) ? p : null;

	public static LevelPresetLibrary LoadDirectory(string dir)
	{
		if (!Directory.Exists(dir)) return Empty;
		var files = Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal);
		var presets = new List<LevelPreset>();
		foreach (string f in files)
		{
			var p = LoadFile(f);
			string stem = Path.GetFileNameWithoutExtension(f);
			if (p.Id != stem) throw new FormatException($"{f}: id: '{p.Id}' must equal the file name '{stem}'");
			presets.Add(p);
		}
		try { return new LevelPresetLibrary(presets); }
		catch (FormatException e) { throw new FormatException($"{dir}: {e.Message}"); }
	}

	public static LevelPreset LoadFile(string path)
	{
		try { return Parse(File.ReadAllText(path)); }
		catch (FormatException e) { throw new FormatException($"{path}: {e.Message}"); }
		catch (JsonException e) { throw new FormatException($"{path}: not JSON: {e.Message}"); }
	}

	public static LevelPreset Parse(string json)
	{
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		if (root.ValueKind != JsonValueKind.Object) throw new FormatException("(root): top level must be a JSON object");
		Unknown(root, TopFields, "");
		if (Str(root, "kind", "kind") != Kind) throw new FormatException($"kind: must be '{Kind}'");
		string id = Id(root, "id");
		string special = Id(root, "special");
		int level = Int(root, "level", "level", 1, 3) ?? throw new FormatException("level: required (1-3)");
		uint colour = Hex(root, "colour", "colour") ?? throw new FormatException("colour: required (#RRGGBB)");
		string status = "final";
		if (root.TryGetProperty("status", out var st))
		{
			status = st.ValueKind == JsonValueKind.String ? st.GetString()! : "";
			if (status is not ("final" or "proposed")) throw new FormatException("status: must be 'final' or 'proposed'");
		}
		var spark = SparkPreset.None;
		if (root.TryGetProperty("spark", out var s))
		{
			if (s.ValueKind != JsonValueKind.Object) throw new FormatException("spark: must be an object");
			Unknown(s, SparkFields, "spark.");
			string? hit = null, block = null, counter = null;
			if (s.TryGetProperty("textures", out var t))
			{
				if (t.ValueKind != JsonValueKind.Object) throw new FormatException("spark.textures: must be an object");
				Unknown(t, TextureFields, "spark.textures.");
				hit = Texture(t, "hit");
				block = Texture(t, "block");
				counter = Texture(t, "counter");
			}
			bool tint = true;
			if (s.TryGetProperty("tint", out var ti))
			{
				if (ti.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new FormatException("spark.tint: must be true or false");
				tint = ti.GetBoolean();
			}
			spark = new SparkPreset(Hex(s, "colour", "spark.colour"), Int(s, "scale_pct", "spark.scale_pct", 25, 400),
				Int(s, "frames", "spark.frames", 2, 60), hit, block, counter, Int(s, "sheet_columns", "spark.sheet_columns", 1, 16) ?? 1, tint);
		}
		return new LevelPreset(id, special, level, colour, spark, status);
	}

	static void Unknown(JsonElement obj, HashSet<string> allowed, string prefix)
	{
		foreach (var p in obj.EnumerateObject())
			if (!allowed.Contains(p.Name)) throw new FormatException($"{prefix}{p.Name}: not a level preset field");
	}

	static string? Str(JsonElement obj, string name, string field)
	{
		if (!obj.TryGetProperty(name, out var v)) return null;
		if (v.ValueKind != JsonValueKind.String) throw new FormatException($"{field}: must be a string");
		return v.GetString();
	}

	static string Id(JsonElement obj, string name)
	{
		string? s = Str(obj, name, name) ?? throw new FormatException($"{name}: required");
		if (!IdPattern.IsMatch(s)) throw new FormatException($"{name}: '{s}' is not a lowercase kebab-case id");
		return s;
	}

	static int? Int(JsonElement obj, string name, string field, int lo, int hi)
	{
		if (!obj.TryGetProperty(name, out var v)) return null;
		if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out int n)) throw new FormatException($"{field}: must be a whole number");
		if (n < lo || n > hi) throw new FormatException($"{field}: {n} is outside {lo}-{hi}");
		return n;
	}

	static uint? Hex(JsonElement obj, string name, string field)
	{
		string? s = Str(obj, name, field);
		if (s is null) return null;
		if (!HexPattern.IsMatch(s)) throw new FormatException($"{field}: '{s}' is not a #RRGGBB colour");
		return Convert.ToUInt32(s.Substring(1), 16);
	}

	static string? Texture(JsonElement obj, string name)
	{
		string? s = Str(obj, name, "spark.textures." + name);
		if (s is null) return null;
		if (!TexturePattern.IsMatch(s)) throw new FormatException($"spark.textures.{name}: '{s}' is not a .png/.webp path relative to game/");
		return s;
	}
}
