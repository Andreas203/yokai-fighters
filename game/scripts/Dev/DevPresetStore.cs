using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using YokaiFighters.Sim;

namespace YokaiFighters.Dev;

/// <summary>
/// YOK-126: named run-state builds for the dev menu, in a dev-only file next to the dev save slot
/// (<see cref="PathFor"/>: <c>run-presets.dev.json</c> in the folder of <c>save.dev.json</c>). Path injected; pure C#.
/// It never opens any other file, so the player's save is never read or written. A missing file = no presets; an
/// unreadable one = no presets plus <see cref="Problem"/> (left as it is until the next save replaces it).
/// <code>{ "version": 1, "presets": [ { "name", "seed", "coins", "health", "depth",
///   "slots": [ { "slot": "A", "special", "level", "modifier", "levelled_order", "borrowed", "modifier_borrowed" } ] } ] }</code>
/// </summary>
public sealed class DevPresetStore
{
	public const string FileName = "run-presets.dev.json";
	public const int Version = 1;
	/// <summary>A preset file larger than this is not read (a handful of builds is a few KB).</summary>
	public const long MaxBytes = 1024 * 1024;

	public string Path { get; }
	/// <summary>Why the last <see cref="Load"/> found no usable presets in an existing file (null = fine).</summary>
	public string? Problem { get; private set; }

	public DevPresetStore(string path)
	{
		if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("no preset path", nameof(path));
		Path = path;
	}

	/// <summary>The preset file for a player save path: in the dev slot's folder (<see cref="DevMode.SavePathFor"/>), never the save itself.</summary>
	public static string PathFor(string playerSavePath) =>
		System.IO.Path.Combine(System.IO.Path.GetDirectoryName(DevMode.SavePathFor(playerSavePath)) ?? "", FileName);

	public IReadOnlyList<DevPreset> Load()
	{
		Problem = null;
		if (!File.Exists(Path)) return Array.Empty<DevPreset>();
		try
		{
			long size = new FileInfo(Path).Length;
			if (size > MaxBytes) { Problem = $"{FileName} is {size} bytes, over the {MaxBytes}-byte cap: not read."; return Array.Empty<DevPreset>(); }
			return Parse(File.ReadAllText(Path));
		}
		catch (Exception e) when (e is JsonException or FormatException or IOException or InvalidOperationException or UnauthorizedAccessException)
		{
			Problem = $"{FileName} can't be read: {e.Message}";
			return Array.Empty<DevPreset>();
		}
	}

	/// <summary>Adds or replaces (by name, case-sensitive) <paramref name="preset"/> and writes the file.</summary>
	public void Save(DevPreset preset)
	{
		if (string.IsNullOrWhiteSpace(preset.Name)) throw new ArgumentException("a preset needs a name", nameof(preset));
		var list = Load().Where(p => p.Name != preset.Name).Append(preset with { BuiltIn = false }).ToList();
		Write(list);
	}

	/// <summary>Removes the preset named <paramref name="name"/>; false when there was none.</summary>
	public bool Delete(string name)
	{
		var list = Load().ToList();
		if (list.RemoveAll(p => p.Name == name) == 0) return false;
		Write(list);
		return true;
	}

	void Write(IReadOnlyList<DevPreset> list)
	{
		string? dir = System.IO.Path.GetDirectoryName(Path);
		if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
		string tmp = Path + ".tmp";
		File.WriteAllText(tmp, Serialize(list));
		File.Move(tmp, Path, overwrite: true);
	}

	public static string Serialize(IEnumerable<DevPreset> presets)
	{
		var arr = new JsonArray();
		foreach (var p in presets)
		{
			var slots = new JsonArray();
			foreach (var s in p.Build.Slots)
				slots.Add(new JsonObject
				{
					["slot"] = s.Slot.ToString(), ["special"] = s.Special, ["level"] = s.Level, ["modifier"] = s.Modifier,
					["levelled_order"] = s.LevelledOrder, ["borrowed"] = s.Borrowed, ["modifier_borrowed"] = s.ModifierBorrowed,
				});
			arr.Add(new JsonObject
			{
				["name"] = p.Name, ["seed"] = p.Build.Seed, ["coins"] = p.Build.Coins, ["health"] = p.Build.Health,
				["depth"] = p.Build.Depth, ["slots"] = slots,
			});
		}
		return new JsonObject { ["version"] = Version, ["presets"] = arr }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
	}

	public static IReadOnlyList<DevPreset> Parse(string text)
	{
		var root = JsonNode.Parse(text) as JsonObject ?? throw new FormatException("not a JSON object");
		if (root["version"]?.GetValue<int>() != Version) throw new FormatException($"version is not {Version}");
		var list = new List<DevPreset>();
		foreach (var n in root["presets"] as JsonArray ?? throw new FormatException("no presets list"))
		{
			if (n is not JsonObject o) throw new FormatException("a preset is not an object");
			string name = o["name"]?.GetValue<string>() ?? throw new FormatException("a preset has no name");
			var slots = new List<DevSlotEdit>();
			foreach (var sn in o["slots"] as JsonArray ?? throw new FormatException($"{name}: no slots list"))
			{
				if (sn is not JsonObject so) throw new FormatException($"{name}: a slot is not an object");
				if (!Enum.TryParse(so["slot"]?.GetValue<string>(), out SpecialSlot slot) || slot is < SpecialSlot.A or > SpecialSlot.D)
					throw new FormatException($"{name}: '{so["slot"]}' is not a slot A-D");
				slots.Add(new DevSlotEdit(slot, so["special"]?.GetValue<string>() ?? throw new FormatException($"{name}: slot {slot} has no special"),
					so["level"]?.GetValue<int>() ?? 1, so["modifier"]?.GetValue<string>(), so["levelled_order"]?.GetValue<int>() ?? 0,
					so["borrowed"]?.GetValue<bool>() ?? false, so["modifier_borrowed"]?.GetValue<bool>() ?? false));
			}
			list.Add(new DevPreset(name, new DevRunBuild(slots, o["coins"]?.GetValue<int>() ?? 0, o["health"]?.GetValue<int>() ?? RunState.MaxHealth,
				o["depth"]?.GetValue<int>() ?? 0, o["seed"]?.GetValue<uint>() ?? 0)));
		}
		return list;
	}
}
