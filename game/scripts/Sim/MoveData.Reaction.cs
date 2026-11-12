using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YokaiFighters.Sim;

/// <summary>
/// <c>properties.multi_hit</c> = { hits, interval } (YOK-220 shape, YOK-226 behaviour): the move's body can connect
/// up to <see cref="Hits"/> times on one opponent in one use, each connect at least <see cref="Interval"/> move frames
/// after the one before (Torrent Spin, Shell Spin Lv 3, A5). See <see cref="Match"/>.Connects.
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
/// YOK-226 (the YOK-222 and YOK-223 behaviours): hit-reaction properties of the Lv 3 evolutions (rules.md specials
/// table, A5: data only, never keyed on a move id).
/// <c>launch</c> (Heaven Seal): an unblocked, unarmoured hit sends the defender up the jump arc, juggleable, into a
/// knockdown on landing. <c>wall_bounce</c> (Crushing Charge): an unblocked, unarmoured hit whose push meets the
/// defender's stage corner bounces them off it the same way, drifting back toward the attacker. Once launched, a
/// fighter can't be launched or bounced again before landing (no loops, X3 spirit). <c>multi_hit</c> (Torrent Spin):
/// see <see cref="MultiHit"/>. tools/validate_data.py <c>built_move_errors</c> runs the same shape checks.
/// </summary>
public sealed partial record MoveData
{
	/// <summary>Heaven Seal: a hit launches (<see cref="Match"/>.Launch).</summary>
	public bool Launch { get; init; }
	/// <summary>Crushing Charge: a hit that pushes the defender into its corner bounces them off it.</summary>
	public bool WallBounce { get; init; }
	/// <summary>Null = the move connects once per use.</summary>
	public MultiHit? MultiHit { get; init; }

	internal static MoveData ReadReaction(MoveData move, JsonElement fd)
	{
		if (!fd.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object) return move;
		FormatException Bad(string what) => new($"{move.Id}: properties.{what}");
		bool FlagOf(string name) => props.TryGetProperty(name, out var v)
			&& (v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : throw Bad($"{name} must be true or false"));

		bool launch = FlagOf("launch"), wallBounce = FlagOf("wall_bounce");
		MultiHit? multiHit = null;
		if (props.TryGetProperty("multi_hit", out var mh) && !MultiHit.TryRead(JsonNode.Parse(mh.GetRawText()), out multiHit))
			throw Bad("multi_hit must be { \"hits\": n >= 2, \"interval\": frames >= 1 }");
		return move with { Launch = launch, WallBounce = wallBounce, MultiHit = multiHit };
	}
}
