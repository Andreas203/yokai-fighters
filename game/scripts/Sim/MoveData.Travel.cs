using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-221: <c>properties.forward_travel</c> (frame-data.schema.json: "Units moved forward over the move"), e.g.
/// Iron-Club Charge. The distance is spread over the move's active frames in whole centi-units: frame k of n moves
/// floor(D·k/n) − floor(D·(k−1)/n), so the steps add up to exactly D with no drift (F3). Match.StepTravel moves the
/// fighter and stops it at the opponent's push box; ResolvePositions keeps the walls (E1). 0 = none.
/// </summary>
public sealed partial record MoveData
{
	/// <summary>Gameplay units moved toward <see cref="Fighter.Facing"/> over the active frames (0 = none).</summary>
	public int ForwardTravel { get; init; }

	/// <summary>
	/// Centi-units this move travels forward on <paramref name="frame"/> (1-based): its share of
	/// <see cref="ForwardTravel"/> on an active frame, else 0.
	/// </summary>
	public long TravelAt(int frame)
	{
		int k = frame - Startup;
		if (ForwardTravel <= 0 || Active <= 0 || k < 1 || k > Active) return 0;
		long total = (long)ForwardTravel * SimConfig.Scale;
		return Share(total, k, Active) - Share(total, k - 1, Active);
	}

	/// <summary>floor(total·k/n) without overflowing a long: total ≤ int.MaxValue·Scale, k ≤ n ≤ int.MaxValue.</summary>
	private static long Share(long total, int k, int n) => total / n * k + total % n * k / n;

	internal static MoveData ReadTravel(MoveData move, JsonElement fd)
	{
		if (!fd.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object
			|| !props.TryGetProperty("forward_travel", out var v))
			return move;
		// An explicit null is refused like any other non-number (the schema's integer; YOK-220's shape check did this
		// before forward_travel left MoveData.Pending).
		// The one whole-number rule (Effects.Whole): 300, 300.0 and 3e2 are all 300 units.
		int travel = v.ValueKind == JsonValueKind.Number && Effects.Whole(JsonNode.Parse(v.GetRawText()), out int d) ? d
			: throw new FormatException($"{move.Id}: properties.forward_travel must be a whole number of units");
		if (travel < 0) throw new FormatException($"{move.Id}: properties.forward_travel must be >= 0");
		if (travel > 0 && move.Active <= 0)
			throw new FormatException($"{move.Id}: properties.forward_travel needs active frames to travel over");
		return move with { ForwardTravel = travel };
	}
}
