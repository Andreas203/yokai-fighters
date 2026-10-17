using System;
using System.IO;
using System.Text.Json;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-136: one <c>data/fighters/&lt;id&gt;.json</c> (kind <c>fighter</c>, <c>data/schema/fighter.schema.json</c>):
/// a fighter's body numbers that no move carries. <see cref="PushWidth"/> is in gameplay units; null = the
/// fight-wide <see cref="SimConfig.BodyWidth"/>. A fighter without a file gets <see cref="Default"/>.
/// </summary>
public sealed record FighterData(string Id, int? PushWidth)
{
	/// <summary>Screen-wall separation (E1, AM9): a wider push box could never fit between the walls.</summary>
	public const int MaxPushWidth = 1060;

	public static readonly FighterData Default = new("", null);

	public static FighterData Parse(string json, string fileId)
	{
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		string kind = root.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
		if (kind != "fighter") throw new FormatException($"{fileId}: kind must be \"fighter\"");
		string id = root.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
		if (id != fileId) throw new FormatException($"{fileId}: id \"{id}\" must equal the file name");
		int? push = null;
		if (root.TryGetProperty("push_width", out var p) && p.ValueKind != JsonValueKind.Null)
		{
			if (p.ValueKind != JsonValueKind.Number || !p.TryGetInt32(out int w) || w < 1 || w > MaxPushWidth)
				throw new FormatException($"{fileId}: push_width must be a whole number 1-{MaxPushWidth}");
			push = w;
		}
		return new FighterData(id, push);
	}

	/// <summary>The fighter's file in <paramref name="dir"/>, or <see cref="Default"/> when the folder or file is missing.</summary>
	public static FighterData Load(string dir, string fighter)
	{
		if (string.IsNullOrEmpty(dir)) return Default;
		string path = Path.Combine(dir, fighter + ".json");
		return File.Exists(path) ? Parse(File.ReadAllText(path), fighter) : Default;
	}

	/// <summary>Sets <see cref="Fighter.PushWidth"/> (centi-units; 0 = the config's).</summary>
	public void Apply(Fighter f) => f.PushWidth = PushWidth is int w ? w * SimConfig.Scale : 0;
}
