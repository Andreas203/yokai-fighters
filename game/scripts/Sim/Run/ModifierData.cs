using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-47: one kind "modifier" file (data/schema/modifier.schema.json). A modifier is data only (A10): its
/// effects use the same vocabulary as Lv 2 / Lv 3 / EX (<see cref="Effects"/>) and are applied on top of the
/// special it is attached to (<see cref="SpecialData.Build(int,bool,ModifierData,SimConfig)"/>), so
/// Will-o'-wisp (projectile.speed x1.3) and Fox's Patience (meter_gain.on_block x1.25) need no code of their own.
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
	public required JsonArray Effects { get; init; }
	public string CardPlain { get; init; } = "";
	public string CardFrames { get; init; } = "";

	/// <summary>True when the special has every required property (applies_to).</summary>
	public bool AppliesTo(SpecialData special) => Requires.All(special.HasProperty);
}

public static class ModifierLoader
{
	public static ModifierData Parse(string json, string? fallbackId = null)
	{
		var root = JsonNode.Parse(json)!.AsObject();
		string id = root["id"]?.GetValue<string>() ?? fallbackId ?? "unnamed";
		if (root["kind"]?.GetValue<string>() != "modifier") throw new FormatException($"{id}: not a kind \"modifier\" file");
		var effects = root["effects"]?.DeepClone() as JsonArray;
		if (effects is null || effects.Count == 0) throw new FormatException($"{id}: effects must list at least one effect");
		return new ModifierData
		{
			Id = id,
			Name = root["name"]?.GetValue<string>() ?? id,
			Source = root["source"]?.GetValue<string>() ?? "",
			Rarity = root["rarity"]?.GetValue<string>() ?? "common",
			Locked = root["locked"]?.GetValue<bool>() ?? false,
			Requires = root["applies_to"]?["requires"]?.AsArray().Select(n => n!.GetValue<string>()).ToArray() ?? Array.Empty<string>(),
			Effects = effects,
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
