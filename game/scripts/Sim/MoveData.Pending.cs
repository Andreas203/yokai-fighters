using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-220: <c>properties.multi_hit</c> = { hits, interval }: the move's hitboxes connect <see cref="Hits"/> times,
/// <see cref="Interval"/> frames apart (Torrent Spin, Shell Spin Lv 3). Shape only: no fight code reads it yet.
/// </summary>
public sealed record MultiHit(int Hits, int Interval)
{
	/// <summary>Reads the JSON shape (exactly hits >= 2 and interval >= 1, whole numbers); false (no throw) otherwise.</summary>
	public static bool TryRead(JsonNode? node, out MultiHit? multiHit)
	{
		multiHit = null;
		if (node is not JsonObject o || o.Count != 2 || o.Any(kv => kv.Key is not ("hits" or "interval"))) return false;
		if (!Effects.Whole(o["hits"], out int hits) || !Effects.Whole(o["interval"], out int interval)) return false;
		if (hits < 2 || interval < 1) return false;
		multiHit = new MultiHit(hits, interval);
		return true;
	}
}

/// <summary>
/// YOK-220 partial: the frame-data properties the effect vocabulary names (<see cref="Effects.Vocabulary"/>) that no
/// fight code reads yet: <c>wall_bounce</c> (Crushing Charge), <c>throw</c> (Whirlpool Grab's <c>throw.range</c>),
/// <c>multi_hit</c> (Torrent Spin), <c>decoy</c> (Mirage Feint). (Torrent Spin's <c>forward_travel</c> left for
/// <see cref="ReadTravel"/>, YOK-221.) The loader checks their
/// frame-data.schema.json shapes, so a level, EX or modifier effect can't leave one the schema would refuse, and
/// otherwise ignores them: the move plays exactly as it would without them. Each behaviour ticket moves its property
/// out of here into the reader that uses it. tools/validate_data.py <c>built_move_errors</c> runs the same checks.
/// </summary>
public sealed partial record MoveData
{
	internal static MoveData CheckPendingProperties(MoveData move, JsonElement fd)
	{
		if (!fd.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object) return move;
		JsonNode? Node(string name) => props.TryGetProperty(name, out var v) ? JsonNode.Parse(v.GetRawText()) : null;
		bool Has(string name) => props.TryGetProperty(name, out _);
		FormatException Bad(string what) => new($"{move.Id}: properties.{what}");

		if (Has("wall_bounce") && props.GetProperty("wall_bounce").ValueKind is not (JsonValueKind.True or JsonValueKind.False))
			throw Bad("wall_bounce must be true or false");
		if (Has("multi_hit") && !MultiHit.TryRead(Node("multi_hit"), out _))
			throw Bad("multi_hit must be { \"hits\": n >= 2, \"interval\": frames >= 1 }");
		if (Has("decoy") && (!Effects.Whole(Node("decoy"), out int decoy) || decoy < 1))
			throw Bad("decoy must be a whole number of hits >= 1");
		if (Has("throw") && !ThrowShape(Node("throw")))
			throw Bad("throw needs a whole range >= 1 and breakable true or false (break_window, if given, >= 1)");
		return move;
	}

	/// <summary>frame-data.schema.json <c>properties.throw</c>: range and breakable required, break_window optional.</summary>
	static bool ThrowShape(JsonNode? node)
	{
		if (node is not JsonObject o || o.Any(kv => kv.Key is not ("range" or "breakable" or "break_window"))) return false;
		if (!Effects.Whole(o["range"], out int range) || range < 1) return false;
		if (o["breakable"] is not JsonValue b || b.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False)) return false;
		return !o.ContainsKey("break_window") || (Effects.Whole(o["break_window"], out int w) && w >= 1);
	}
}
