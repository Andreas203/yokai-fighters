using System;
using System.IO;
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
		var path = root["path"] ?? throw new FormatException($"{id}: path is required");
		CancelFrom from = path["from"]?.GetValue<string>() switch
		{
			"specials" => CancelFrom.Specials,
			"heavy_normals" => CancelFrom.HeavyNormals,
			"normals" => CancelFrom.Normals,
			"successful_throws" => CancelFrom.SuccessfulThrows,
			var o => throw new FormatException($"{id}: path.from '{o}' is unknown"),
		};
		CancelInto into = path["into"]?.GetValue<string>() switch
		{
			"dash" => CancelInto.Dash,
			"specials" => CancelInto.Specials,
			"one_special" => CancelInto.OneSpecial,
			var o => throw new FormatException($"{id}: path.into '{o}' is unknown"),
		};
		bool hit = false, block = false, whiff = false;
		foreach (var on in path["on"]?.AsArray() ?? new JsonArray())
			switch (on?.GetValue<string>())
			{
				case "hit": hit = true; break;
				case "block": block = true; break;
				case "whiff": whiff = true; break;
			}
		return new CancelRule(id, from, into, hit, block, whiff);
	}

	public static CancelRule LoadFile(string path) =>
		Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
}
