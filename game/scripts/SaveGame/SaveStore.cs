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
	/// <summary>
	/// Not JSON or not an object: an empty save, and the file is kept as a backup on the next write. Or the file
	/// could not be opened at all (<see cref="SaveStore.WriteBlocked"/>): an empty save, and nothing is written over it.
	/// </summary>
	Corrupt,
	/// <summary>Readable current-version file with a section that could not be used: that section is empty, the rest kept. The file is kept as a backup on the next write.</summary>
	Partial,
	/// <summary>Written by an older build (or no version): converted, rewritten as the current version on the next save.</summary>
	OlderVersion,
	/// <summary>Written by a newer build, or with a version this build cannot read: the fields this build knows are kept. The file is kept as a backup on the next write.</summary>
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
/// { "version": 7,
///   "run": null | { "seed": 77, "map": "default-map", "map_file": "default-map.json", "map_fingerprint": "hex",
///                   "visited": ["r1"], "current": null, "health": 950, "coins": 25,
///                   "first_meeting": false, "copy_locked": false, "node_paid": true (only when set), "locked_copy": null | { "slot": "A", "special": "spirit-wave", "level": 2 },
///                   "slots": [ { "slot": "A", "special": "spirit-wave", "level": 2, "modifier": null, "levelled": 1, "borrowed": false, "modifier_borrowed": false } ] },
///   "unlocks": ["ability-id"],
///   "story": { "seen": ["intro"], "elders_met": ["elder-oni"] },
///   "carry_over": null | "foxfire",
///   "tanuki_wins": 0 }
/// </code>
/// Each section is read on its own (M4): a missing or broken one is empty and the others are kept. Inside the
/// run, <c>seed</c>, <c>map</c>, <c>visited</c>, <c>health</c> and <c>slots</c> are needed (without them there is
/// no run to continue, so the run alone is dropped); every other field, and any field a later build adds, has a
/// default when missing. Unknown fields are ignored. A format change adds its conversion in <see cref="Parse"/>,
/// keyed on the version.
/// </para>
/// <para>
/// The store never destroys a file it could not fully read: the first write after any load that lost something
/// (corrupt, partial, newer or unreadable version, an older version with problems) first copies the file to a
/// backup name that is not taken (<c>save.json.bak</c>, <c>.bak.1</c>, ...), and after a read the disk refused
/// nothing is written at all. Because a build cannot see fields a later build added to the same version, a ticket
/// that adds saved state raises <see cref="CurrentVersion"/>: an older build then loads that file as newer and
/// backs it up before writing.
/// </para>
/// </summary>
public sealed class SaveStore
{
	/// <summary>2 (YOK-87 after YOK-82/85): <c>run.coins</c>, <c>run.copy_locked</c>, <c>run.locked_copy</c>. Version 1 files load: no coins, the lock worked out or refused (<see cref="RunSave.Restore"/>).</summary>
	/// <remarks>3 (YOK-95): <c>run.slots[].borrowed</c> and <c>run.slots[].modifier_borrowed</c> (R6 borrowed ink; missing = false), so version 2 and 1 files load with nothing borrowed.</remarks>
	/// <remarks>4 (YOK-128): <c>run.node_paid</c> (R8: the entered node's win is already paid and healed; missing = false), so older files load unpaid, as before.</remarks>
	/// <remarks>5 (YOK-106, Y3/M4/M5): <c>story.elders_met</c> (elder ids Ryo has met, story progress) and <c>run.first_meeting</c> (the node saved at its entry is an elder met first on that entry); both missing = none, so version 4 and older files load with no elder met.</remarks>
	/// <remarks>6 (YOK-97, M3): top-level <c>carry_over</c> (the special id the next run carries at Lv 2, or null; missing = none), so older files load with nothing carried.</remarks>
	/// <remarks>7 (YOK-99): top-level <c>tanuki_wins</c> (Y5, whole number 0 or above; missing = 0), so older files load with no Tanuki win.</remarks>
	public const int CurrentVersion = 7;
	public const string FileName = "save.json";
	/// <summary>Suffix of the copy kept before a file this build could not fully read is first overwritten (then <c>.bak.1</c>, <c>.bak.2</c>, ...: a backup is never replaced).</summary>
	public const string BackupSuffix = ".bak";

	public string Path { get; }
	/// <summary>Why the last <see cref="Save"/> failed; null after a good one.</summary>
	public string? LastSaveError { get; private set; }
	private bool _backupFirst;
	private string? _readError;
	/// <summary>
	/// True after a <see cref="Load"/> the disk refused (no permission, a sharing violation): the file is probably
	/// intact, so <see cref="Save"/> writes nothing until a later load can read it.
	/// </summary>
	public bool WriteBlocked => _readError is not null;

	public SaveStore(string path) => Path = path ?? throw new ArgumentNullException(nameof(path));

	public SaveLoad Load()
	{
		string text;
		_readError = null;
		_backupFirst = false;
		try
		{
			if (!File.Exists(Path)) return new SaveLoad(SaveData.Empty, SaveLoadStatus.Missing, Array.Empty<string>());
			text = File.ReadAllText(Path);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
		{
			_readError = e.Message;
			return new SaveLoad(SaveData.Empty, SaveLoadStatus.Corrupt, new[] { "cannot read: " + e.Message });
		}
		SaveLoad load = Parse(text);
		_backupFirst = LostSomething(load);
		return load;
	}

	/// <summary>True when the load did not keep everything the file held, so writing over the file would destroy it.</summary>
	public static bool LostSomething(SaveLoad load) => load.Status switch
	{
		SaveLoadStatus.Loaded or SaveLoadStatus.Missing => false,
		SaveLoadStatus.OlderVersion => load.Problems.Count > 1, // the first line only names the version
		_ => true,
	};

	/// <summary>The file's text to save data (what <see cref="Load"/> does after reading).</summary>
	public static SaveLoad Parse(string text)
	{
		if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..]; // a byte-order mark some editors add
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
			string? unknownVersion = null; // present but not a whole number 0 or above: not a format this build wrote
			if (root.TryGetProperty("version", out JsonElement v))
			{
				if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) && n >= 0) version = n;
				else unknownVersion = v.GetRawText();
			}

			// Version 1 is the first format, so there is nothing to convert yet: an older or versionless file is read
			// with the same field names. A later format change adds its conversion here, keyed on `version`.
			var data = new SaveData
			{
				Run = Run(root, problems),
				Unlocks = Ids(root, "unlocks", problems, sort: false),
				Story = Story(root, problems),
				CarryOver = CarryOver(root, problems),
				TanukiWins = TanukiWins(root, problems),
			};
			SaveLoadStatus status =
				unknownVersion is not null ? SaveLoadStatus.NewerVersion :
				version < CurrentVersion ? SaveLoadStatus.OlderVersion :
				version > CurrentVersion ? SaveLoadStatus.NewerVersion :
				problems.Count > 0 ? SaveLoadStatus.Partial : SaveLoadStatus.Loaded;
			if (unknownVersion is not null) problems.Insert(0, $"version {unknownVersion} is not one this build can read, this build writes {CurrentVersion}");
			else if (version != CurrentVersion) problems.Insert(0, $"version {version}, this build writes {CurrentVersion}");
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
			if (!run.TryGetProperty("seed", out JsonElement s) || !WholeUInt(s, out seed)) why = "seed missing or not a whole number 0-4294967295";
			else if (Text(run, "map") is not { } map) why = "map missing";
			else if (!run.TryGetProperty("health", out JsonElement h) || !Whole(h, out health)) why = "health missing or not a whole number";
			else if (StringArray(run, "visited") is not { } visited) why = "visited missing or not a list of node ids";
			else if (Slots(run, out string? slotProblem) is not { } slots) why = slotProblem;
			// Optional (missing = default), but a value of the wrong kind is damage, not absence: the run is dropped.
			else if (OptionalInt(run, "coins", out int coins) is { } coinProblem) why = coinProblem;
			else if (OptionalBool(run, "copy_locked", out bool? copyLocked) is { } lockProblem) why = lockProblem;
			// Version 5 (YOK-106): missing (version 4 and older) = not a first meeting.
			else if (OptionalBool(run, "first_meeting", out bool? firstMeeting) is { } meetingProblem) why = meetingProblem;
			else if (OptionalCopy(run, out LockedCopySave? copy) is { } copyProblem) why = copyProblem;
			// Version 4 (YOK-128, R8): the entered node already paid; missing (version 3 and older) = not paid.
			else if (OptionalBool(run, "node_paid", out bool? nodePaid) is { } paidProblem) why = paidProblem;
			else if (OptionalBool(run, "off_route", out bool? offRoute) is { } routeProblem) why = routeProblem; // YOK-124, dev slot only
			else
				return new RunSave(seed, map, visited, Text(run, "current"), health, slots)
				{
					MapFile = Text(run, "map_file"),
					MapFingerprint = Text(run, "map_fingerprint"),
					Coins = coins,
					CopyLocked = copyLocked,
					LockedCopy = copy,
					FirstMeeting = firstMeeting ?? false,
					NodePaid = nodePaid ?? false,
					OffRoute = offRoute ?? false,
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
			if (!e.TryGetProperty("level", out JsonElement l) || !Whole(l, out int level)) return null;
			int levelled = e.TryGetProperty("levelled", out JsonElement o) && Whole(o, out int rank) ? rank : 0;
			// Version 3 (YOK-95, R6): borrowed ink; missing (version 2 and older) = not borrowed.
			if (OptionalBool(e, "borrowed", out bool? borrowed) is { } borrowedProblem) { problem = $"slots[{slots.Count}]: {borrowedProblem}"; return null; }
			if (OptionalBool(e, "modifier_borrowed", out bool? modBorrowed) is { } modProblem) { problem = $"slots[{slots.Count}]: {modProblem}"; return null; }
			slots.Add(new SlotSave(slot, special, level, Text(e, "modifier"), levelled) { Borrowed = borrowed ?? false, ModifierBorrowed = modBorrowed ?? false });
		}
		problem = null;
		return slots;
	}

	private static StoryProgress Story(JsonElement root, List<string> problems)
	{
		if (!root.TryGetProperty("story", out JsonElement story)) { problems.Add("story: missing, no story progress"); return StoryProgress.None; }
		if (story.ValueKind != JsonValueKind.Object) { problems.Add("story: not an object, no story progress"); return StoryProgress.None; }
		var seen = Ids(story, "seen", problems, sort: true, label: "story.seen");
		// Version 5 (YOK-106, Y3): elders met. Missing (version 4 and older) = none met, not a problem; a list of the
		// wrong kind is reported and taken as empty, without touching `seen` or any other section (M4).
		var met = story.TryGetProperty(StoryProgress.EldersMetField, out _)
			? Ids(story, StoryProgress.EldersMetField, problems, sort: true, label: "story." + StoryProgress.EldersMetField)
			: Array.Empty<string>();
		return new StoryProgress(seen, met);
	}

	/// <summary>Version 6 (YOK-97, M3): the carried special's id. Missing (older files) or null = none; anything but a non-empty string is reported and taken as none.</summary>
	private static string? CarryOver(JsonElement root, List<string> problems)
	{
		if (!root.TryGetProperty("carry_over", out JsonElement c) || c.ValueKind == JsonValueKind.Null) return null;
		string? id = c.ValueKind == JsonValueKind.String ? c.GetString() : null;
		if (string.IsNullOrWhiteSpace(id)) { problems.Add("carry_over: not a special id, nothing carried"); return null; }
		return id;
	}

	/// <summary>
	/// YOK-99, Y5: the Tanuki win count. Missing = 0 with no problem (like every field a later build adds: older
	/// files have none); anything but a whole number 0 or above is taken as 0 and reported, so the load is partial
	/// and the file is backed up before it is next written.
	/// </summary>
	private static int TanukiWins(JsonElement root, List<string> problems)
	{
		if (!root.TryGetProperty("tanuki_wins", out JsonElement w)) return 0;
		if (Whole(w, out int n) && n >= 0) return n; // like every whole number in the file, 1e2 reads as 100 (YOK-87)
		problems.Add($"tanuki_wins: {w.GetRawText()} is not a whole number 0 or above, taken as 0");
		return 0;
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

	/// <summary>
	/// A whole number in the run section, written any way JSON allows: <c>100</c>, <c>100.0</c> and <c>1e2</c> all
	/// read as 100 (designer, 2026-10-11). A fractional value or one out of range is not a whole number. The format
	/// <c>version</c> is not read this way: it stays strict so an unreadable version counts as newer.
	/// </summary>
	private static bool Whole(JsonElement e, out int value)
	{
		value = 0;
		if (e.ValueKind != JsonValueKind.Number) return false;
		if (e.TryGetInt32(out value)) return true;
		if (!e.TryGetDecimal(out decimal d) || d != decimal.Truncate(d) || d < int.MinValue || d > int.MaxValue) return false;
		value = (int)d;
		return true;
	}

	private static bool WholeUInt(JsonElement e, out uint value)
	{
		value = 0;
		if (e.ValueKind != JsonValueKind.Number) return false;
		if (e.TryGetUInt32(out value)) return true;
		if (!e.TryGetDecimal(out decimal d) || d != decimal.Truncate(d) || d < uint.MinValue || d > uint.MaxValue) return false;
		value = (uint)d;
		return true;
	}

	private static string? OptionalInt(JsonElement obj, string name, out int value)
	{
		value = 0;
		if (!obj.TryGetProperty(name, out JsonElement e)) return null;
		return Whole(e, out value) ? null : $"{name} is not a whole number";
	}

	private static string? OptionalBool(JsonElement obj, string name, out bool? value)
	{
		value = null;
		if (!obj.TryGetProperty(name, out JsonElement e)) return null;
		if (e.ValueKind is JsonValueKind.True or JsonValueKind.False) { value = e.GetBoolean(); return null; }
		return $"{name} is not true/false";
	}

	private static string? OptionalCopy(JsonElement run, out LockedCopySave? copy)
	{
		copy = null;
		if (!run.TryGetProperty("locked_copy", out JsonElement e) || e.ValueKind == JsonValueKind.Null) return null;
		if (e.ValueKind != JsonValueKind.Object || Text(e, "slot") is not { } slot || Text(e, "special") is not { } special
			|| !e.TryGetProperty("level", out JsonElement l) || !Whole(l, out int level))
			return "locked_copy needs slot, special and level";
		copy = new LockedCopySave(slot, special, level);
		return null;
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
				w.WriteNumber("coins", run.Coins);
				w.WriteBoolean("first_meeting", run.FirstMeeting);
				if (run.CopyLocked is { } locked) w.WriteBoolean("copy_locked", locked);
				if (run.NodePaid) w.WriteBoolean("node_paid", true); // only after a won node, before it is completed
				if (run.OffRoute) w.WriteBoolean("off_route", true); // YOK-124: only a dev walk off the routes has it
				if (run.LockedCopy is not { } copy) w.WriteNull("locked_copy");
				else
				{
					w.WriteStartObject("locked_copy");
					w.WriteString("slot", copy.Slot);
					w.WriteString("special", copy.Special);
					w.WriteNumber("level", copy.Level);
					w.WriteEndObject();
				}
				w.WriteStartArray("slots");
				foreach (SlotSave s in run.Slots)
				{
					w.WriteStartObject();
					w.WriteString("slot", s.Slot);
					w.WriteString("special", s.Special);
					w.WriteNumber("level", s.Level);
					if (s.Modifier is null) w.WriteNull("modifier"); else w.WriteString("modifier", s.Modifier);
					w.WriteNumber("levelled", s.Levelled);
					w.WriteBoolean("borrowed", s.Borrowed);
					w.WriteBoolean("modifier_borrowed", s.ModifierBorrowed);
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
			w.WriteStartArray(StoryProgress.EldersMetField);
			foreach (string elder in d.Story.EldersMet) w.WriteStringValue(elder);
			w.WriteEndArray();
			w.WriteEndObject();
			if (d.CarryOver is { } carried) w.WriteString("carry_over", carried);
			else w.WriteNull("carry_over");
			w.WriteNumber("tanuki_wins", Math.Max(0, d.TanukiWins)); // YOK-99, Y5
			w.WriteEndObject();
		}
		return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
	}

	/// <summary>
	/// Writes the file (temp file flushed to disk, then rename). False with <see cref="LastSaveError"/> when the
	/// disk refuses; never throws. The first write after a load that lost something first copies the file to a free
	/// backup name; if that copy cannot be made, nothing is written. While <see cref="WriteBlocked"/>, nothing is written.
	/// </summary>
	public bool Save(SaveData data)
	{
		if (_readError is not null)
		{
			LastSaveError = $"the existing file could not be read ({_readError}), so it is left untouched";
			return false;
		}
		string temp = Path + ".tmp";
		try
		{
			string? dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
			if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
			if (_backupFirst && File.Exists(Path)) File.Copy(Path, FreeBackupPath(), overwrite: false);
			_backupFirst = false;
			byte[] bytes = new UTF8Encoding(false).GetBytes(Serialize(data));
			using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				file.Write(bytes, 0, bytes.Length);
				file.Flush(flushToDisk: true); // on disk before the rename, so a power cut cannot leave an empty save
			}
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

	/// <summary>The first backup name not in use: an earlier backup is never replaced.</summary>
	private string FreeBackupPath()
	{
		string name = Path + BackupSuffix;
		for (int n = 1; File.Exists(name) || Directory.Exists(name); n++) name = Path + BackupSuffix + "." + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
		return name;
	}
}
