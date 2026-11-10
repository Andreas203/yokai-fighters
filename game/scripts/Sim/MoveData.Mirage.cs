using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-224: <c>properties.teleport</c> = { to, frame }. On move frame <see cref="Frame"/> the fighter lands just
/// behind the opponent (<see cref="Match"/>.Mirage). <c>to</c> has one value, <c>behind_opponent</c> (frame-data.schema.json).
/// </summary>
public sealed record Teleport(int Frame);

/// <summary>
/// YOK-224 partial: Fox Mirage's teleport and Mirage Feint's decoy (rules.md specials table; A5: the evolution is
/// data only, <c>properties.decoy</c> set by its Lv 3 changes). Neither is a throw escape or an invulnerability (E12):
/// the move keeps its own hurtboxes and its recovery is punishable.
/// </summary>
public sealed partial record MoveData
{
	/// <summary>Null = the move doesn't teleport.</summary>
	public Teleport? Teleport { get; init; }

	/// <summary>Hits the decoy left at the teleport's origin absorbs; 0 = none. Only a teleport leaves one.</summary>
	public int Decoy { get; init; }

	internal static MoveData ReadMirage(MoveData move, JsonElement fd)
	{
		if (!fd.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object) return move;
		FormatException Bad(string what) => new($"{move.Id}: properties.{what}");

		if (props.TryGetProperty("teleport", out var t))
		{
			if (JsonNode.Parse(t.GetRawText()) is not JsonObject o || o.Count != 2
				|| o["to"] is not JsonValue to || to.GetValueKind() != JsonValueKind.String || to.GetValue<string>() != "behind_opponent"
				|| !Effects.Whole(o["frame"], out int frame) || frame < 1)
				throw Bad("teleport must be { \"to\": \"behind_opponent\", \"frame\": n >= 1 }");
			if (frame > move.TotalFrames)
				throw Bad($"teleport.frame {frame} is past the move's last frame ({move.TotalFrames})");
			move = move with { Teleport = new Teleport(frame) };
		}

		if (props.TryGetProperty("decoy", out var d))
		{
			if (!Effects.Whole(JsonNode.Parse(d.GetRawText()), out int hits) || hits < 1)
				throw Bad("decoy must be a whole number of hits >= 1");
			move = move with { Decoy = hits };
		}
		return move;
	}
}
