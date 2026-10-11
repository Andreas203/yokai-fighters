using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace YokaiFighters.SaveGame;

public enum SaveLoadStatus
{
	/// <summary>Current-version file, every section valid.</summary>
	Loaded,
	/// <summary>No file: an empty save (a first launch).</summary>
	Missing,
	/// <summary>Not JSON, not an object, or unreadable: an empty save. The file is kept as a backup on the next write.</summary>
	Corrupt,
	/// <summary>Readable current-version file with a section that could not be used: that section is empty, the rest kept.</summary>
	Partial,
	/// <summary>Written by an older build (or no version): converted, rewritten as the current version on the next save.</summary>
	OlderVersion,
	/// <summary>Written by a newer build: the fields this build knows are kept. The file is kept as a backup on the next write.</summary>
	NewerVersion,
}

/// <summary>What <see cref="SaveStore.Load"/> found. <see cref="Data"/> is always usable.</summary>
public sealed record SaveLoad(SaveData Data, SaveLoadStatus Status, IReadOnlyList<string> Problems);

/// <summary>
/// YOK-87: reads and writes the save file. Same conventions as <c>Settings.SettingsStore</c>: pure C# (no Godot),
/// the path injected (tests use a temp file, the game the globalized <c>user://save.json</c>), loading never
/// throws, saving is temp file + rename. Nothing in <c>Sim/</c>, <c>Ai/</c> or <c>Replay/</c> refers to it, and
/// the file holds no time stamp: the same save data always writes the same bytes.
/// <para>
/// Format (version <see cref="CurrentVersion"/>), one JSON object with three independent sections:
/// <code>
/// { "version": 1,
///   "run": null | { "seed": 77, "map": "default-map", "map_file": "default-map.json", "map_fingerprint": "hex",
///                   "visited": ["r1"], "current": null, "health": 950,
///                   "slots": [ { "slot": "A", "special": "spirit-wave", "level": 2, "modifier": null, "levelled": 1 } ] },
///   "unlocks": ["ability-id"],
///   "story": { "seen": ["intro"] } }
/// </code>
/// Each section is read on its own (M4): a missing or broken one is empty and the others are kept. Inside the
/// run, <c>seed</c>, <c>map</c>, <c>visited</c>, <c>health</c> and <c>slots</c> are needed (without them there is
/// no run to continue, so the run alone is dropped); every other field, and any field a later build adds, has a
/// default when missing. Unknown fields are ignored. A format change adds its conversion in <see cref="Parse"/>,
/// keyed on the version.
/// </para>
/// </summary>
public sealed class SaveStore
{
	public const int CurrentVersion = 1;
	public const string FileName = "save.json";
	/// <summary>Suffix of the copy kept before a file this build could not fully read is first overwritten.</summary>
	public const string BackupSuffix = ".bak";

	public string Path { get; }
	/// <summary>Why the last <see cref="Save"/> failed; null after a good one.</summary>
	public string? LastSaveError { get; private set; }
	private bool _backupFirst;

	public SaveStore(string path) => Path = path ?? throw new ArgumentNullException(nameof(path));

	public SaveLoad Load()
	{
		string text;
		try
		{
			if (!File.Exists(Path)) return new SaveLoad(SaveData.Empty, SaveLoadStatus.Missing, Array.Empty<string>());
			text = File.ReadAllText(Path);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
		{
			return new SaveLoad(SaveData.Empty, SaveLoadStatus.Corrupt, new[] { "cannot read: " + e.Message });
		}
		SaveLoad load = Parse(text);
		// Writing over a file from a newer build, or one that would not parse, would destroy what this build could not read.
		_backupFirst = load.Status is SaveLoadStatus.Corrupt or SaveLoadStatus.NewerVersion;
		return load;
	}

	/// <summary>The file's text to save data (what <see cref="Load"/> does after reading).</summary>
	public static SaveLoad Parse(string text)
	{
		JsonDocument doc;
		try { doc = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }); }
		catch (JsonException e) { return new SaveLoad(SaveData.Empty, SaveLoadStatus.Corrupt, new[] { "not JSON: " + e.Message }); }
		using (doc)
		{
			JsonElement root = doc.RootElement;
			if (root.ValueKind != JsonValueKind.Object)
				return new SaveLoad(SaveData.Empty, SaveLoadStatus.Corrupt, new[] { "not a JSON object" });

			var problems = new List<string>();
			int version = 0; // a file without a version is older than version 1
			if (root.TryGetProperty("version", out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n)) version = n;

			// Version 1 is the first format, so there is nothing to convert yet: an older or versionless file is read
			// with the same field names. A later format change adds its conversion here, keyed on `version`.
			var data = new SaveData
			{
				Run = Run(root, problems),
				Unlocks = Ids(root, "unlocks", problems, sort: false),
				Story = Story(root, problems),
			};
			SaveLoadStatus status =
				version < CurrentVersion ? SaveLoadStatus.OlderVersion :
				version > CurrentVersion ? SaveLoadStatus.NewerVersion :
				problems.Count > 0 ? SaveLoadStatus.Partial : SaveLoadStatus.Loaded;
			if (version != CurrentVersion) problems.Insert(0, $"version {version}, this build writes {CurrentVersion}");
			return new SaveLoad(data, status, problems);
		}
	}

	// ---- sections ----

	private static RunSave? Run(JsonElement root, List<string> problems)
	{
		if (!root.TryGetProperty("run", out JsonElement run) || run.ValueKind == JsonValueKind.Null) return null; // no run in progress
		string? why = null;
		if (run.ValueKind != JsonValueKind.Object) why = "not an object";
		else
		{
			uint seed = 0;
			int health = 0;
			if (!run.TryGetProperty("seed", out JsonElement s) || s.ValueKind != JsonValueKind.Number || !s.TryGetUInt32(out seed)) why = "seed missing or not a whole number 0-4294967295";
			else if (Text(run, "map") is not { } map) why = "map missing";
			else if (!run.TryGetProperty("health", out JsonElement h) || h.ValueKind != JsonValueKind.Number || !h.TryGetInt32(out health)) why = "health missing or not a whole number";
			else if (StringArray(run, "visited") is not { } visited) why = "visited missing or not a list of node ids";
			else if (Slots(run, out string? slotProblem) is not { } slots) why = slotProblem;
			else
				return new RunSave(seed, map, visited, Text(run, "current"), health, slots)
				{
					MapFile = Text(run, "map_file"),
					MapFingerprint = Text(run, "map_fingerprint"),
				};
		}
		problems.Add($"run: {why}; the saved run is dropped, unlocks and story are kept");
		return null;
	}

	private static List<SlotSave>? Slots(JsonElement run, out string? problem)
	{
		problem = "slots missing or not a list";
		if (!run.TryGetProperty("slots", out JsonElement list) || list.ValueKind != JsonValueKind.Array) return null;
		var slots = new List<SlotSave>();
		foreach (JsonElement e in list.EnumerateArray())
		{
			problem = $"slots[{slots.Count}]: needs slot, special and level";
			if (e.ValueKind != JsonValueKind.Object || Text(e, "slot") is not { } slot || Text(e, "special") is not { } special) return null;
			if (!e.TryGetProperty("level", out JsonElement l) || l.ValueKind != JsonValueKind.Number || !l.TryGetInt32(out int level)) return null;
			int levelled = e.TryGetProperty("levelled", out JsonElement o) && o.ValueKind == JsonValueKind.Number && o.TryGetInt32(out int rank) ? rank : 0;
			slots.Add(new SlotSave(slot, special, level, Text(e, "modifier"), levelled));
		}
		problem = null;
		return slots;
	}

	private static StoryProgress Story(JsonElement root, List<string> problems)
	{
		if (!root.TryGetProperty("story", out JsonElement story)) { problems.Add("story: missing, no story progress"); return StoryProgress.None; }
		if (story.ValueKind != JsonValueKind.Object) { problems.Add("story: not an object, no story progress"); return StoryProgress.None; }
		return new StoryProgress(Ids(story, "seen", problems, sort: true, label: "story.seen"));
	}

	/// <summary>A list of ids: entries that are not non-empty strings are skipped (and reported), the rest kept in order, once each.</summary>
	private static IReadOnlyList<string> Ids(JsonElement parent, string name, List<string> problems, bool sort, string? label = null)
	{
		label ??= name;
		if (!parent.TryGetProperty(name, out JsonElement list)) { problems.Add($"{label}: missing, taken as empty"); return Array.Empty<string>(); }
		if (list.ValueKind != JsonValueKind.Array) { problems.Add($"{label}: not a list, taken as empty"); return Array.Empty<string>(); }
		var ids = new List<string>();
		int skipped = 0;
		foreach (JsonElement e in list.EnumerateArray())
		{
			string? id = e.ValueKind == JsonValueKind.String ? e.GetString() : null;
			if (string.IsNullOrWhiteSpace(id)) skipped++;
			else if (!ids.Contains(id, StringComparer.Ordinal)) ids.Add(id);
		}
		if (skipped > 0) problems.Add($"{label}: {skipped} entr{(skipped == 1 ? "y" : "ies")} skipped (not an id)");
		if (sort) ids.Sort(StringComparer.Ordinal);
		return ids;
	}

	private static string? Text(JsonElement obj, string name) =>
		obj.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(e.GetString()) ? e.GetString() : null;

	private static string[]? StringArray(JsonElement obj, string name)
	{
		if (!obj.TryGetProperty(name, out JsonElement list) || list.ValueKind != JsonValueKind.Array) return null;
		var items = new List<string>();
		foreach (JsonElement e in list.EnumerateArray())
		{
			if (e.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(e.GetString())) return null;
			items.Add(e.GetString()!);
		}
		return items.ToArray();
	}

	// ---- writing ----

	/// <summary>The file's text for this save (current version). The same data always gives the same text.</summary>
	public static string Serialize(SaveData d)
	{
		using var stream = new MemoryStream();
		using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
		{
			w.WriteStartObject();
			w.WriteNumber("version", CurrentVersion);
			if (d.Run is not { } run) w.WriteNull("run");
			else
			{
				w.WriteStartObject("run");
				w.WriteNumber("seed", run.Seed);
				w.WriteString("map", run.MapId);
				if (run.MapFile is not null) w.WriteString("map_file", run.MapFile);
				if (run.MapFingerprint is not null) w.WriteString("map_fingerprint", run.MapFingerprint);
				w.WriteStartArray("visited");
				foreach (string id in run.Visited) w.WriteStringValue(id);
				w.WriteEndArray();
				if (run.Current is null) w.WriteNull("current"); else w.WriteString("current", run.Current);
				w.WriteNumber("health", run.Health);
				w.WriteStartArray("slots");
				foreach (SlotSave s in run.Slots)
				{
					w.WriteStartObject();
					w.WriteString("slot", s.Slot);
					w.WriteString("special", s.Special);
					w.WriteNumber("level", s.Level);
					if (s.Modifier is null) w.WriteNull("modifier"); else w.WriteString("modifier", s.Modifier);
					w.WriteNumber("levelled", s.Levelled);
					w.WriteEndObject();
				}
				w.WriteEndArray();
				w.WriteEndObject();
			}
			w.WriteStartArray("unlocks");
			foreach (string id in d.Unlocks.Distinct(StringComparer.Ordinal)) w.WriteStringValue(id);
			w.WriteEndArray();
			w.WriteStartObject("story");
			w.WriteStartArray("seen");
			foreach (string flag in d.Story.Seen) w.WriteStringValue(flag);
			w.WriteEndArray();
			w.WriteEndObject();
			w.WriteEndObject();
		}
		return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
	}

	/// <summary>
	/// Writes the file (temp file, then rename). False with <see cref="LastSaveError"/> when the disk refuses; never
	/// throws. The first write after loading a corrupt or newer-version file first copies it to
	/// <c>save.json.bak</c>; if that copy cannot be made, nothing is written.
	/// </summary>
	public bool Save(SaveData data)
	{
		string temp = Path + ".tmp";
		try
		{
			string? dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
			if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
			if (_backupFirst && File.Exists(Path)) File.Copy(Path, Path + BackupSuffix, overwrite: true);
			_backupFirst = false;
			File.WriteAllText(temp, Serialize(data), new UTF8Encoding(false));
			File.Move(temp, Path, overwrite: true);
			LastSaveError = null;
			return true;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException or ArgumentException)
		{
			LastSaveError = e.Message;
			try { if (File.Exists(temp)) File.Delete(temp); } catch (Exception) { /* nothing more to do */ }
			return false;
		}
	}
}
