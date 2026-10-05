using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace YokaiFighters.Sim;

/// <summary>
/// Reads moves from content JSON (YOK-12 schema) at fight start. Pure C# (System.Text.Json, no Godot),
/// so the harness can load the same files headless. Accepts a content file whose Lv 1 frame data is at
/// <c>levels.1.frame_data</c> (kind "special"), or an object with a top-level <c>frame_data</c>.
/// Schema validation is tools/validate_data.py's job; this loader only fails loudly on what the sim
/// cannot run (missing numbers, a hitting move without hitstun/blockstun).
/// </summary>
public static class MoveLoader
{
	public static MoveData Parse(string json, string? fallbackId = null)
	{
		using var doc = JsonDocument.Parse(json);
		JsonElement root = doc.RootElement;
		string id = root.TryGetProperty("id", out var idEl) ? idEl.GetString()! : fallbackId ?? "unnamed";

		JsonElement fd;
		if (root.TryGetProperty("frame_data", out var direct)) fd = direct;
		else if (root.TryGetProperty("levels", out var levels) && levels.TryGetProperty("1", out var l1)
			&& l1.TryGetProperty("frame_data", out var nested)) fd = nested;
		else throw new FormatException($"{id}: no levels.1.frame_data or frame_data");

		int Req(string name) => fd.TryGetProperty(name, out var v)
			? v.GetInt32() : throw new FormatException($"{id}: frame_data.{name} is required");
		int? Opt(JsonElement obj, string name) =>
			obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

		JsonElement props = fd.TryGetProperty("properties", out var p) ? p : default;
		bool Flag(string name) => props.ValueKind == JsonValueKind.Object && props.TryGetProperty(name, out var v) && v.GetBoolean();
		JsonElement push = fd.TryGetProperty("pushback", out var pb) ? pb : default;

		var activeEl = fd.TryGetProperty("active", out var a) ? a : throw new FormatException($"{id}: frame_data.active is required");
		var move = new MoveData
		{
			Id = id,
			Startup = Req("startup"),
			Active = activeEl.ValueKind == JsonValueKind.Null ? 0 : activeEl.GetInt32(),
			Recovery = Req("recovery"),
			Damage = Req("damage"),
			Hitstun = Opt(fd, "hitstun") ?? 0,
			Blockstun = Opt(fd, "blockstun") ?? 0,
			HitPushback = Opt(push, "on_hit"),
			BlockPushback = Opt(push, "on_block"),
			Knockdown = Flag("knockdown"),
			Low = Flag("low"),
			Hitboxes = Boxes(fd, "hitboxes"),
			Hurtboxes = Boxes(fd, "hurtboxes"),
		};

		if (move.Hitboxes.Count > 0 && (Opt(fd, "hitstun") is null || Opt(fd, "blockstun") is null))
			throw new FormatException($"{id}: a move with hitboxes needs hitstun and blockstun");
		foreach (var h in move.Hitboxes)
			if (!move.IsActive(h.First) || !move.IsActive(h.Last))
				throw new FormatException($"{id}: hitbox frames {h.First}-{h.Last} outside the active window");
		return move;
	}

	public static MoveData LoadFile(string path) =>
		Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));

	/// <summary>Every *.json in the folder, sorted by file name so slot order is deterministic.</summary>
	public static MoveData[] LoadDirectory(string dir) =>
		Directory.Exists(dir)
			? Directory.GetFiles(dir, "*.json").OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal).Select(LoadFile).ToArray()
			: Array.Empty<MoveData>();

	private static TimedBox[] Boxes(JsonElement fd, string name)
	{
		if (!fd.TryGetProperty(name, out var arr)) return Array.Empty<TimedBox>();
		var list = new List<TimedBox>();
		foreach (var e in arr.EnumerateArray())
		{
			var fr = e.GetProperty("frames");
			var r = e.GetProperty("rect");
			list.Add(new TimedBox(fr[0].GetInt32(), fr[1].GetInt32(),
				new Box(r.GetProperty("x").GetInt32(), r.GetProperty("y").GetInt32(), r.GetProperty("w").GetInt32(), r.GetProperty("h").GetInt32())));
		}
		return list.ToArray();
	}
}
