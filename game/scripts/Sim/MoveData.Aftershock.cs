using System;
using System.Text.Json;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-136: <c>properties.aftershock_delay</c> (Oni Quake Lv 3 "Aftershock", rules.md specials table; A5: an
/// evolution is data only). N frames after the move's first active frame a second quake replays the move's
/// hitboxes where the first one struck (see <see cref="Match.Aftershocks"/>). 0 = none.
/// </summary>
public sealed partial record MoveData
{
	public int AftershockDelay { get; init; }

	internal static MoveData ReadAftershock(MoveData move, JsonElement fd)
	{
		if (!fd.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object
			|| !props.TryGetProperty("aftershock_delay", out var v) || v.ValueKind == JsonValueKind.Null)
			return move;
		int delay = v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int d) ? d
			: throw new FormatException($"{move.Id}: properties.aftershock_delay must be a whole number of frames");
		if (delay < 1) throw new FormatException($"{move.Id}: properties.aftershock_delay must be >= 1");
		if (move.Hitboxes.Count == 0 || move.IsThrow)
			throw new FormatException($"{move.Id}: properties.aftershock_delay needs the move's own hitboxes to replay");
		return move with { AftershockDelay = delay };
	}
}
