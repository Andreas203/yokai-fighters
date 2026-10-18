using System.Text.Json;
using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace YokaiFighters.Sim;

/// <summary>Reads kind "cancel_rule" files (data/cancels/, cancel-rule.schema.json) into <see cref="CancelRule"/> (YOK-21).</summary>
public static class CancelRuleLoader
{
	public static CancelRule Parse(string json, string? fallbackId = null)
	{
		var root = JsonNode.Parse(json)!.AsObject();
		string id = root["id"]?.GetValue<string>() ?? fallbackId ?? "unnamed";
		if (root["kind"]?.GetValue<string>() != "cancel_rule") throw new FormatException($"{id}: not a kind \"cancel_rule\" file");
		var path = root["path"] as JsonObject ?? throw new FormatException($"{id}: path is required and must be an object");
		CancelFrom from = Effects.Text(path, "from", id, "path.from") switch
		{
			"specials" => CancelFrom.Specials,
			"heavy_normals" => CancelFrom.HeavyNormals,
			"normals" => CancelFrom.Normals,
			"successful_throws" => CancelFrom.SuccessfulThrows,
			var o => throw new FormatException($"{id}: path.from '{o}' is unknown"),
		};
		CancelInto into = Effects.Text(path, "into", id, "path.into") switch
		{
			"dash" => CancelInto.Dash,
			"specials" => CancelInto.Specials,
			"one_special" => CancelInto.OneSpecial,
			var o => throw new FormatException($"{id}: path.into '{o}' is unknown"),
		};
		bool hit = false, block = false, whiff = false;
		if (path.ContainsKey("on") && path["on"] is not JsonArray) throw new FormatException($"{id}: path.on must be a list");
		foreach (var on in path["on"] as JsonArray ?? new JsonArray())
			switch (on is JsonValue ov && ov.GetValueKind() == JsonValueKind.String ? ov.GetValue<string>() : throw new FormatException($"{id}: path.on entries must be strings"))
			{
				case "hit": hit = true; break;
				case "block": block = true; break;
				case "whiff": whiff = true; break;
				case var o: throw new FormatException($"{id}: path.on '{o}' is unknown"); // YOK-103: as the schema
			}
		if (!hit && !block && !whiff) throw new FormatException($"{id}: path.on must list hit, block or whiff");
		// X3 is not optional: the fight always limits each target to once per combo (Fighter.CancelUsed).
		if (root["once_per_combo"] is not JsonValue once || !once.TryGetValue(out bool opc) || !opc)
			throw new FormatException($"{id}: once_per_combo must be true (X3)");
		return new CancelRule(id, from, into, hit, block, whiff);
	}

	/// <summary>Every kind "cancel_rule" *.json in the folder, sorted by file name; other kinds are skipped.</summary>
	public static CancelRule[] LoadDirectory(string dir) =>
		Directory.Exists(dir)
			? Directory.GetFiles(dir, "*.json").OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
				.Where(f => MoveLoader.KindOf(f) == "cancel_rule").Select(LoadFile).ToArray()
			: Array.Empty<CancelRule>();

	/// <summary>
	/// YOK-86 (A11, M1): ids of the cancel rules that start locked (<c>locked: true</c>), sorted by file name like
	/// <see cref="LoadDirectory"/>; each file is parsed in full, so a broken rule fails here as it would there.
	/// </summary>
	public static string[] LockedIds(string dir) =>
		Directory.Exists(dir)
			? Directory.GetFiles(dir, "*.json").OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
				.Where(f => MoveLoader.KindOf(f) == "cancel_rule")
				.Select(f => (Text: File.ReadAllText(f), Name: Path.GetFileNameWithoutExtension(f)))
				.Select(x => (Rule: Parse(x.Text, x.Name), Root: JsonNode.Parse(x.Text)!.AsObject())) // one read; Parse validates
				.Where(x => x.Root["locked"] is JsonValue v && v.TryGetValue(out bool l) && l)
				.Select(x => x.Rule.Id).ToArray()
			: Array.Empty<string>();

	public static CancelRule LoadFile(string path) =>
		Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
}
