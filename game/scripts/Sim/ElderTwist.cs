using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YokaiFighters.Sim;

/// <summary>YOK-90: the closed list of sim rules an elder twist can be (data/schema/elder-twist.schema.json).</summary>
public enum TwistEffect : byte
{
	/// <summary>The opponent's special, started as the <see cref="ElderTwist.Repeat"/>-th use in a row or later, is reflected.</summary>
	ReflectRepeatedSpecial = 1,
	/// <summary>The twist holder's specials take <see cref="ElderTwist.ArmourHits"/> hits each without being interrupted.</summary>
	SpecialArmour = 2,
	/// <summary>The opponent's throw-break press does nothing against the twist holder's throws (C4).</summary>
	UnbreakableThrows = 3,
}

/// <summary>
/// YOK-90 (P5, Y2, Y3): one elder's twist, from <c>data/twists/&lt;id&gt;.json</c> (kind <c>elder_twist</c>). It belongs
/// to the elder a map node names, so it holds for whatever fighter stands in for that elder; the match applies it
/// through <see cref="Fighter.Twist"/> (the fighter that holds it is the elder). Numbers come from data only.
/// </summary>
/// <param name="Elder">The yokai id elder map nodes name (e.g. <c>elder-oni</c>).</param>
/// <param name="Repeat">ReflectRepeatedSpecial: from which use in a row (2 = the second); else 0.</param>
/// <param name="ArmourHits">SpecialArmour: hits per special; else 0.</param>
/// <param name="LabelKey">The HUD reminder's text key (text: habit-writer).</param>
/// <param name="Icon">Map-node icon id, or null.</param>
/// <param name="IntroCard">Story-card id of the intro card (Y3), or null until written.</param>
/// <param name="CalloutCard">Story-card id of the first-meeting callout (Y3), or null until written.</param>
public sealed record ElderTwist(
	string Id, string Elder, TwistEffect Effect, int Repeat, int ArmourHits,
	string LabelKey, string? Icon, string? IntroCard, string? CalloutCard);

/// <summary>YOK-90: reads <c>elder_twist</c> files; throws <see cref="FormatException"/> naming the file and field.</summary>
public static class ElderTwistLoader
{
	public const string Kind = "elder_twist";

	public static ElderTwist LoadFile(string path)
	{
		try { return Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path)); }
		catch (FormatException e) { throw new FormatException($"{path}: {e.Message}", e); }
		catch (JsonException e) { throw new FormatException($"{path}: {e.Message}", e); }
	}

	/// <param name="fileId">The file name without extension, which the id must equal; null = not checked.</param>
	public static ElderTwist Parse(string json, string? fileId = null)
	{
		if (JsonNode.Parse(json) is not JsonObject o) throw new FormatException("an elder twist must be a JSON object");
		if (Text(o, "kind") != Kind) throw new FormatException($"kind must be '{Kind}'");
		string id = Text(o, "id") ?? throw new FormatException("id is missing");
		if (fileId != null && id != fileId) throw new FormatException($"id '{id}' must equal the file name '{fileId}'");
		string elder = Text(o, "elder") ?? throw new FormatException("elder is missing");
		string label = Text(o, "label_key") ?? throw new FormatException("label_key is missing");
		if (o["effect"] is not JsonObject e) throw new FormatException("effect must be an object");
		string type = Text(e, "type") ?? throw new FormatException("effect.type is missing");
		int repeat = 0, hits = 0;
		TwistEffect effect;
		switch (type)
		{
			case "reflect_repeated_special":
				effect = TwistEffect.ReflectRepeatedSpecial;
				repeat = Int(e, "repeat", 2);
				break;
			case "special_armour":
				effect = TwistEffect.SpecialArmour;
				hits = Int(e, "hits", 1);
				break;
			case "unbreakable_throws":
				effect = TwistEffect.UnbreakableThrows;
				break;
			default: throw new FormatException($"effect.type '{type}' is not one the fight knows (elder-twist.schema.json)");
		}
		foreach (var (name, _) in e)
			if (name != "type" && name != (effect == TwistEffect.ReflectRepeatedSpecial ? "repeat" : effect == TwistEffect.SpecialArmour ? "hits" : ""))
				throw new FormatException($"effect.{name} is not a setting of '{type}'");
		return new ElderTwist(id, elder, effect, repeat, hits, label, Text(o, "icon"), Text(o, "intro_card"), Text(o, "callout_card"));
	}

	static string? Text(JsonObject o, string name)
	{
		if (o[name] is not { } n) return null;
		if (n is JsonValue v && v.TryGetValue(out string? s) && !string.IsNullOrEmpty(s)) return s;
		throw new FormatException($"{name} must be a non-empty string");
	}

	static int Int(JsonObject o, string name, int min)
	{
		if (o[name] is JsonValue v && v.GetValueKind() == JsonValueKind.Number && v.TryGetValue(out int i) && i >= min && i <= 9) return i;
		throw new FormatException($"effect.{name} must be a whole number from {min} to 9");
	}
}

/// <summary>
/// YOK-90: every elder's twist, by elder. Which fights get one comes from the map: only a node with
/// <see cref="MapNodeData.Elder"/> set, and the twist is the one whose <see cref="ElderTwist.Elder"/> is the node's
/// yokai (no yokai id in code). Pure; the game loads <c>data/twists/</c> (<c>FightScene.Twists</c>).
/// </summary>
public sealed class ElderTwists
{
	public const string Folder = "twists";
	readonly Dictionary<string, ElderTwist> _byElder;

	public static readonly ElderTwists None = new(Array.Empty<ElderTwist>());

	public IReadOnlyCollection<ElderTwist> All => _byElder.Values;

	public ElderTwists(IEnumerable<ElderTwist> twists)
	{
		_byElder = new Dictionary<string, ElderTwist>(StringComparer.Ordinal);
		foreach (var t in twists)
			if (!_byElder.TryAdd(t.Elder, t))
				throw new FormatException($"elder '{t.Elder}' has two twists ('{_byElder[t.Elder].Id}', '{t.Id}'); Y2 gives each elder one");
	}

	/// <summary>Every <c>*.json</c> in <paramref name="dir"/> (sorted); a missing folder = no twists.</summary>
	public static ElderTwists LoadDir(string dir)
	{
		if (!Directory.Exists(dir)) return None;
		var files = Directory.GetFiles(dir, "*.json");
		Array.Sort(files, StringComparer.Ordinal);
		return new ElderTwists(files.Select(ElderTwistLoader.LoadFile));
	}

	/// <summary>The twist an elder node shows and fights with; null for a node that is not an elder or has none.</summary>
	public ElderTwist? ForNode(MapNodeData? node) => node is null ? null : For(node.Yokai, node.Elder);

	/// <summary>Same, from a resolved node (the fight's <see cref="NodeContext"/>): the node's yokai, not the stand-in fighter.</summary>
	public ElderTwist? ForNode(NodeContext? node) => node is null ? null : For(node.Yokai, node.Elder);

	/// <summary>For a map view that carries the node's yokai and elder flag.</summary>
	public ElderTwist? For(string? yokai, bool elder) =>
		elder && yokai != null && _byElder.TryGetValue(yokai, out var t) ? t : null;

	public ElderTwist? ById(string id) => _byElder.Values.FirstOrDefault(t => t.Id == id);

	/// <summary>
	/// Puts the node's twist on the elder's fighter (P2, the opponent) of a match about to start; clears it otherwise,
	/// so a match is never left with an earlier node's twist. Returns the twist applied.
	/// </summary>
	public ElderTwist? Apply(Match m, NodeContext? node, int elderIndex = 1)
	{
		var t = ForNode(node);
		m.Fighters[elderIndex].Twist = t;
		m.Fighters[1 - elderIndex].Twist = null;
		return t;
	}
}
