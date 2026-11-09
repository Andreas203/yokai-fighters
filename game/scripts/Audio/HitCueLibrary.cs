using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Audio;

/// <summary>
/// YOK-93 (V6): what a hit must be for a cue rule to match. A null field matches anything; all null = the default.
/// <see cref="Move"/> matches the hit's move id or its special's id (so a special's id covers its EX and evolution).
/// </summary>
public sealed record HitCueWhen(string? Move = null, string? Source = null, int? Level = null, HitStrength? Strength = null,
	HitSparkKind? Outcome = null)
{
	public static readonly HitCueWhen Any = new();

	/// <summary>Specific to general: a move (2) beats a source (1) beats a default (0).</summary>
	public int Tier => Move != null ? 2 : Source != null ? 1 : 0;

	/// <summary>Within a tier, the rule naming more of level / strength / outcome wins.</summary>
	public int Narrowing => (Level != null ? 1 : 0) + (Strength != null ? 1 : 0) + (Outcome != null ? 1 : 0);

	public bool Matches(in HitCue c) =>
		(Move == null || Move == c.MoveId || Move == c.Special)
		&& (Source == null || Source == c.Source)
		&& (Level == null || Level == c.Level)
		&& (Strength == null || Strength == c.Strength)
		&& (Outcome == null || Outcome == c.Kind);

	/// <summary>One string per distinct case: the library refuses two rules with the same one.</summary>
	public string Key => $"move={Move}|source={Source}|level={Level}|strength={Strength}|outcome={Outcome}";
}

/// <summary>
/// One rule of a cue table: its case, the thuds and optional stings (res://sound/... paths), the volume in dB and the
/// ticks after the impact tick it starts. <see cref="Table"/> and <see cref="Index"/> say where it was written.
/// </summary>
public sealed record HitCueRule(HitCueWhen When, IReadOnlyList<string> Thud, IReadOnlyList<string> Sting, int VolumeDb,
	int OffsetTicks, string Table = "", int Index = 0);

/// <summary>One kind <c>sound_cue</c> file (data/schema/sound-cue.schema.json).</summary>
public sealed record HitCueTable(string Id, IReadOnlyList<HitCueRule> Cues, string Status = "final");

/// <summary>What one hit plays: a thud, maybe a sting, at a volume, starting on sim tick <see cref="StartTick"/>.</summary>
public readonly record struct HitSound(string Thud, string? Sting, int VolumeDb, int StartTick);

/// <summary>
/// YOK-93 (V6): every hit-sound cue table, read from data/sound/ (absent or empty = <see cref="Empty"/>: the scene
/// keeps its placeholder hit pack). Pure C#, presentation only: nothing in the sim, AI, replays or harness reads it.
/// <see cref="Resolve"/> picks the most specific matching rule (<see cref="HitCueWhen.Tier"/>, then
/// <see cref="HitCueWhen.Narrowing"/>, then the first: files in name order, rules in array order) and a sample from
/// its lists by the hit's tick and attacker, never by an RNG, so a replay plays the same sounds. Every hit sounds (V6):
/// a non-empty set must have a default (<see cref="CoversEveryHit"/>). Throws <see cref="FormatException"/> naming the
/// file and field on anything the schema refuses, so bad data fails at load and the scene keeps its placeholder pack.
/// </summary>
public sealed class HitCueLibrary
{
	public const string Kind = "sound_cue";
	public const int MaxOffsetTicks = 30, MinVolumeDb = -40, MaxVolumeDb = 12;
	public static readonly HitCueLibrary Empty = new(Array.Empty<HitCueTable>());

	// \z, not $: .NET's $ also matches before a trailing newline ("a\n" would pass).
	static readonly Regex IdPattern = new(@"\A[a-z0-9]+(-[a-z0-9]+)*\z", RegexOptions.CultureInvariant);
	static readonly Regex SamplePattern = new(@"\Ares://sound/([A-Za-z0-9_-]+/)*[A-Za-z0-9_-]+\.(wav|ogg|mp3)\z", RegexOptions.CultureInvariant);
	static readonly HashSet<string> TopFields = new() { "kind", "id", "status", "rules", "cues", "notes" };
	static readonly HashSet<string> CueFields = new() { "when", "thud", "sting", "volume_db", "offset_ticks", "notes" };
	static readonly HashSet<string> WhenFields = new() { "move", "source", "level", "strength", "outcome" };

	public IReadOnlyList<HitCueTable> Tables { get; }

	/// <summary>Every rule, in precedence order for ties: tables as given, rules in array order.</summary>
	public IReadOnlyList<HitCueRule> Rules { get; }

	/// <summary>At least one rule: the scene plays these instead of the placeholder hit pack.</summary>
	public bool HasCues => Rules.Count > 0;

	public HitCueLibrary(IEnumerable<HitCueTable> tables)
	{
		Tables = tables.ToList();
		Rules = Tables.SelectMany(t => t.Cues).ToList();
		var seen = new Dictionary<string, HitCueRule>();
		foreach (var r in Rules)
			if (!seen.TryAdd(r.When.Key, r))
			{
				var first = seen[r.When.Key];
				throw new FormatException($"sound cues '{first.Table}' cues.{first.Index} and '{r.Table}' cues.{r.Index} are the same case ({r.When.Key}); one rule per case (V6)");
			}
		if (Rules.Count > 0 && !CoversEveryHit(Rules))
			throw new FormatException("sound cues have no default: some hits would be silent. Add a rule with no when (or rules naming only strength / outcome that cover every hit) (V6)");
	}

	/// <summary>
	/// V6 "every hit": the rules naming no move, source or level match every strength and outcome, so
	/// <see cref="Resolve"/> never returns null for any hit. tools/validate_data.py runs the same check.
	/// </summary>
	public static bool CoversEveryHit(IEnumerable<HitCueRule> rules)
	{
		var general = rules.Where(r => r.When.Move == null && r.When.Source == null && r.When.Level == null).ToList();
		foreach (var s in Strengths.Values)
			foreach (var o in Outcomes.Values)
				if (!general.Any(r => (r.When.Strength ?? s) == s && (r.When.Outcome ?? o) == o)) return false;
		return true;
	}

	/// <summary>The rule that plays for this hit; null only for <see cref="Empty"/> (a loaded set covers every hit).</summary>
	public HitCueRule? RuleFor(in HitCue cue)
	{
		HitCueRule? best = null;
		int bestScore = -1;
		foreach (var r in Rules)
		{
			if (!r.When.Matches(cue)) continue;
			int score = r.When.Tier * 4 + r.When.Narrowing;
			if (score > bestScore) { best = r; bestScore = score; }
		}
		return best;
	}

	/// <summary>The sounds this hit plays, or null.</summary>
	public HitSound? Resolve(in HitCue cue)
	{
		var r = RuleFor(cue);
		if (r is null) return null;
		string thud = r.Thud[Pick(r.Thud.Count, cue.Tick, cue.Attacker, 0)];
		string? sting = r.Sting.Count > 0 ? r.Sting[Pick(r.Sting.Count, cue.Tick, cue.Attacker, 1)] : null;
		return new HitSound(thud, sting, r.VolumeDb, cue.Tick + r.OffsetTicks);
	}

	/// <summary>
	/// A deterministic index in 0..count-1 from the hit's tick and attacker; <paramref name="salt"/> separates the thud
	/// from the sting. FNV-1a over the bytes, then the murmur3 finaliser: without it the low bits of FNV depend only on
	/// the low bits of the input bytes, so with two samples the salt flipped parity and thud i always met sting 1 - i.
	/// </summary>
	public static int Pick(int count, int tick, int attacker, int salt)
	{
		if (count <= 1) return 0;
		unchecked
		{
			uint h = 2166136261;
			foreach (int v in stackalloc int[] { tick, attacker, salt })
				for (int b = 0; b < 4; b++) { h ^= (byte)(v >> (8 * b)); h *= 16777619; }
			h ^= h >> 16; h *= 0x85EBCA6B; h ^= h >> 13; h *= 0xC2B2AE35; h ^= h >> 16;
			return (int)(h % (uint)count);
		}
	}

	public static HitCueLibrary LoadDirectory(string dir)
	{
		if (!Directory.Exists(dir)) return Empty;
		var tables = new List<HitCueTable>();
		foreach (string f in Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
		{
			var t = LoadFile(f);
			string stem = Path.GetFileNameWithoutExtension(f);
			if (t.Id != stem) throw new FormatException($"{f}: id: '{t.Id}' must equal the file name '{stem}'");
			tables.Add(t);
		}
		try { return new HitCueLibrary(tables); }
		catch (FormatException e) { throw new FormatException($"{dir}: {e.Message}"); }
	}

	public static HitCueTable LoadFile(string path)
	{
		try { return Parse(File.ReadAllText(path)); }
		catch (FormatException e) { throw new FormatException($"{path}: {e.Message}"); }
		catch (JsonException e) { throw new FormatException($"{path}: not JSON: {e.Message}"); }
	}

	public static HitCueTable Parse(string json)
	{
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		if (root.ValueKind != JsonValueKind.Object) throw new FormatException("(root): top level must be a JSON object");
		Unknown(root, TopFields, "");
		if (Str(root, "kind", "kind") != Kind) throw new FormatException($"kind: must be '{Kind}'");
		string id = Id(root, "id", "id") ?? throw new FormatException("id: required");
		string status = "final";
		if (root.TryGetProperty("status", out var st))
		{
			status = st.ValueKind == JsonValueKind.String ? st.GetString()! : "";
			if (status is not ("final" or "proposed")) throw new FormatException("status: must be 'final' or 'proposed'");
		}
		if (!root.TryGetProperty("cues", out var cues) || cues.ValueKind != JsonValueKind.Array || cues.GetArrayLength() == 0)
			throw new FormatException("cues: required, a non-empty array");
		var rules = new List<HitCueRule>();
		var seen = new HashSet<string>();
		int i = 0;
		foreach (var c in cues.EnumerateArray())
		{
			string at = $"cues.{i}";
			if (c.ValueKind != JsonValueKind.Object) throw new FormatException($"{at}: must be an object");
			Unknown(c, CueFields, at + ".");
			var when = HitCueWhen.Any;
			if (c.TryGetProperty("when", out var w))
			{
				if (w.ValueKind != JsonValueKind.Object) throw new FormatException($"{at}.when: must be an object");
				Unknown(w, WhenFields, at + ".when.");
				when = new HitCueWhen(Id(w, "move", $"{at}.when.move"), Enum(w, "source", $"{at}.when.source", Sources),
					Int(w, "level", $"{at}.when.level", 1, 3), Enum(w, "strength", $"{at}.when.strength", Strengths),
					Enum(w, "outcome", $"{at}.when.outcome", Outcomes));
			}
			if (!seen.Add(when.Key)) throw new FormatException($"{at}.when: same case as an earlier rule ({when.Key}); one rule per case (V6)");
			var thud = Samples(c, "thud", at) ?? throw new FormatException($"{at}.thud: required");
			var sting = Samples(c, "sting", at) ?? (IReadOnlyList<string>)Array.Empty<string>();
			rules.Add(new HitCueRule(when, thud, sting, Int(c, "volume_db", $"{at}.volume_db", MinVolumeDb, MaxVolumeDb) ?? 0,
				Int(c, "offset_ticks", $"{at}.offset_ticks", 0, MaxOffsetTicks) ?? 0, id, i));
			i++;
		}
		return new HitCueTable(id, rules, status);
	}

	/// <summary>common.schema.json yokai: a move's own source (HitSparks.Identify reads it from the move's file).</summary>
	static readonly Dictionary<string, string> Sources = new() { ["ryo"] = "ryo", ["kitsune"] = "kitsune", ["oni"] = "oni", ["kappa"] = "kappa" };
	static readonly Dictionary<string, HitStrength> Strengths = new()
	{ ["light"] = HitStrength.Light, ["medium"] = HitStrength.Medium, ["heavy"] = HitStrength.Heavy };
	static readonly Dictionary<string, HitSparkKind> Outcomes = new()
	{ ["hit"] = HitSparkKind.Hit, ["block"] = HitSparkKind.Block, ["counter"] = HitSparkKind.Counter };

	static void Unknown(JsonElement obj, HashSet<string> allowed, string prefix)
	{
		foreach (var p in obj.EnumerateObject())
			if (!allowed.Contains(p.Name)) throw new FormatException($"{prefix}{p.Name}: not a sound cue field");
	}

	static string? Str(JsonElement obj, string name, string field)
	{
		if (!obj.TryGetProperty(name, out var v)) return null;
		if (v.ValueKind != JsonValueKind.String) throw new FormatException($"{field}: must be a string");
		return v.GetString();
	}

	static string? Id(JsonElement obj, string name, string field)
	{
		string? s = Str(obj, name, field);
		if (s != null && !IdPattern.IsMatch(s)) throw new FormatException($"{field}: '{s}' is not a lowercase kebab-case id");
		return s;
	}

	static int? Int(JsonElement obj, string name, string field, int lo, int hi)
	{
		if (!obj.TryGetProperty(name, out var v)) return null;
		if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out int n)) throw new FormatException($"{field}: must be a whole number");
		if (n < lo || n > hi) throw new FormatException($"{field}: {n} is outside {lo}-{hi}");
		return n;
	}

	static T? Enum<T>(JsonElement obj, string name, string field, Dictionary<string, T> values) where T : struct =>
		Str(obj, name, field) is { } s ? Lookup(s, field, values) : null;

	static string? Enum(JsonElement obj, string name, string field, Dictionary<string, string> values) =>
		Str(obj, name, field) is { } s ? Lookup(s, field, values) : null;

	static T Lookup<T>(string s, string field, Dictionary<string, T> values) =>
		values.TryGetValue(s, out var v) ? v : throw new FormatException($"{field}: '{s}' must be one of {string.Join(", ", values.Keys)}");

	static IReadOnlyList<string>? Samples(JsonElement obj, string name, string at)
	{
		if (!obj.TryGetProperty(name, out var v)) return null;
		if (v.ValueKind != JsonValueKind.Array || v.GetArrayLength() == 0) throw new FormatException($"{at}.{name}: must be a non-empty array");
		var list = new List<string>();
		int j = 0;
		foreach (var e in v.EnumerateArray())
		{
			string field = $"{at}.{name}.{j++}";
			if (e.ValueKind != JsonValueKind.String) throw new FormatException($"{field}: must be a string");
			string s = e.GetString()!;
			if (!SamplePattern.IsMatch(s)) throw new FormatException($"{field}: '{s}' is not a res://sound/ .wav/.ogg/.mp3 path");
			if (list.Contains(s)) throw new FormatException($"{field}: '{s}' is listed twice");
			list.Add(s);
		}
		return list;
	}
}
