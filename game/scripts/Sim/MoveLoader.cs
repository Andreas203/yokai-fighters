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
		var (button, dirMask) = NormalInput(root, id);
		InputBits throwButtons = ThrowInput(root, id);
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
			Button = button,
			DirectionMask = dirMask,
			ThrowButtons = throwButtons,
			Throwboxes = Boxes(fd, "throwboxes"),
			BreakWindow = Opt(fd, "break_window") ?? 0,
			BreakPushback = Opt(push, "on_break"),
		};
		if (move.IsThrow)
		{
			if (move.BreakWindow < 1) throw new FormatException($"{id}: a throw needs frame_data.break_window >= 1");
			if (move.Throwboxes.Count == 0) throw new FormatException($"{id}: a throw needs throwboxes");
			if (move.Hitboxes.Count > 0) throw new FormatException($"{id}: a throw has throwboxes, not hitboxes (C4)");
			// The grab clip must still be playing when the throw lands (BreakWindow frames after the grab).
			if (move.Recovery <= move.BreakWindow)
				throw new FormatException($"{id}: a throw's recovery ({move.Recovery}) must outlast its break window ({move.BreakWindow})");
		}
		foreach (var t in move.Throwboxes)
			if (!move.IsActive(t.First) || !move.IsActive(t.Last))
				throw new FormatException($"{id}: throwbox frames {t.First}-{t.Last} outside the active window");

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

	/// <summary>Kind "throw" (data/schema/throw.schema.json): the two input.buttons, ORed. None for other kinds.</summary>
	private static InputBits ThrowInput(JsonElement root, string id)
	{
		bool isThrow = root.TryGetProperty("kind", out var k) && k.GetString() == "throw";
		if (!isThrow) return InputBits.None;
		if (!root.TryGetProperty("input", out var input) || !input.TryGetProperty("buttons", out var bs))
			throw new FormatException($"{id}: a throw needs input.buttons");
		InputBits mask = InputBits.None;
		int n = 0;
		foreach (var b in bs.EnumerateArray()) { mask |= ButtonBit(b.GetString(), id); n++; }
		if (n != 2 || System.Numerics.BitOperations.PopCount((uint)mask) != 2)
			throw new FormatException($"{id}: a throw needs exactly two different buttons");
		return mask;
	}

	private static InputBits ButtonBit(string? name, string id) => name switch
	{
		"LP" => InputBits.LightPunch,
		"MP" => InputBits.MediumPunch,
		"HP" => InputBits.HeavyPunch,
		"LK" => InputBits.LightKick,
		"MK" => InputBits.MediumKick,
		"HK" => InputBits.HeavyKick,
		var other => throw new FormatException($"{id}: unknown button '{other}'"),
	};

	/// <summary>Kind "normal" (data/schema/normal.schema.json): input.button and optional input.directions.</summary>
	private static (InputBits button, int dirMask) NormalInput(JsonElement root, string id)
	{
		bool normal = root.TryGetProperty("kind", out var k) && k.GetString() == "normal";
		if (!normal) return (InputBits.None, 0);
		if (!root.TryGetProperty("input", out var input) || !input.TryGetProperty("button", out var b))
			throw new FormatException($"{id}: a normal needs input.button");
		InputBits button = b.GetString() switch
		{
			"LP" => InputBits.LightPunch,
			"MP" => InputBits.MediumPunch,
			"HP" => InputBits.HeavyPunch,
			"LK" => InputBits.LightKick,
			"MK" => InputBits.MediumKick,
			"HK" => InputBits.HeavyKick,
			var other => throw new FormatException($"{id}: unknown button '{other}'"),
		};
		int mask = 0;
		if (input.TryGetProperty("directions", out var dirs))
			foreach (var d in dirs.EnumerateArray())
			{
				int n = d.GetInt32();
				if (n < 1 || n > 9) throw new FormatException($"{id}: direction {n} is not a numpad direction");
				mask |= 1 << n;
			}
		return (button, mask);
	}

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
