using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-225: command grabs. A special whose frame data carries <c>properties.throw</c> (River Grab, C4, R7) is a
/// generic throw fired by its special input instead of LP+LK: it resolves through MatchThrows exactly as the E12
/// throw does (grab, break window, break press, clash, Slippery Skin, unbreakable twist, burst invulnerability,
/// knockdown, heavy hitstop, meter). Read from data only, never keyed on a move id (A10).
///
/// Geometry (proposal, open designer question): the move's hitboxes place the grab (their near edge x, y and h, and
/// their frames) and <c>range</c> is how far the grab reaches in front of the thrower's origin, so each grab box spans
/// x .. range. The hitbox's own width is not used. Whirlpool Grab's <c>throw.range</c> mul_pct 150 is then exactly
/// +50% reach. The hitboxes never strike: they become the move's <see cref="Throwboxes"/>.
/// </summary>
public sealed partial record MoveData
{
	/// <summary>C4: the break window when <c>properties.throw.break_window</c> is omitted.</summary>
	public const int DefaultBreakWindow = 7;
	/// <summary>E12: the throw input (LP+LK) is what breaks a command grab, as it breaks any throw.</summary>
	public const InputBits ThrowInputButtons = InputBits.LightPunch | InputBits.LightKick;

	/// <summary>A special that is a throw (<c>properties.throw</c>); <see cref="IsThrow"/> stays the LP+LK throw only.</summary>
	public bool IsCommandGrab { get; init; }
	/// <summary>Resolves through the throw pipeline (C4): the LP+LK throw or a command grab. Counts as a throw for
	/// trials (R7) and for <c>successful_throws</c> cancels (X4, Undertow).</summary>
	public bool IsGrab => IsThrow || IsCommandGrab;
	/// <summary><c>properties.throw.breakable</c>: false = the defender's break press does nothing (a clash still breaks it).</summary>
	public bool Breakable { get; init; } = true;
	/// <summary>A command grab's reach in units from the thrower's origin (<c>properties.throw.range</c>); 0 for any other move.</summary>
	public int GrabRange { get; init; }
	/// <summary>The press that breaks this grab: the throw's own buttons, or the E12 throw input for a command grab.</summary>
	public InputBits BreakButtons => IsThrow ? ThrowButtons : ThrowInputButtons;

	/// <summary>MoveLoader: turns a special with <c>properties.throw</c> into a command grab; any other move is returned as is.</summary>
	internal static MoveData ReadCommandGrab(MoveData move, JsonElement fd)
	{
		if (!fd.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object
			|| !props.TryGetProperty("throw", out var el)) return move;
		FormatException Bad(string what) => new($"{move.Id}: properties.throw {what}");
		if (move.IsThrow || move.IsNormal) throw Bad("is for a special (a command grab); a throw or a normal can't carry it");
		var node = JsonNode.Parse(el.GetRawText());
		if (!ThrowShape(node))
			throw Bad("needs a whole range >= 1 and breakable true or false (break_window, if given, >= 1)");
		var o = node!.AsObject();
		Effects.Whole(o["range"], out int range);
		int window = o.ContainsKey("break_window") && Effects.Whole(o["break_window"], out int w) ? w : DefaultBreakWindow;
		bool breakable = o["breakable"]!.GetValue<bool>();
		if (move.Hitboxes.Count == 0) throw Bad("needs hitboxes: they place the grab (C4)");
		var boxes = move.Hitboxes.Select(h => h.Box.X < range
			? h with { Box = h.Box with { W = range - h.Box.X } }
			: throw Bad($"range {range} must reach past the hitbox's near edge (x {h.Box.X})")).ToArray();
		if (move.Recovery <= window) // E12: the grab clip must still be playing when the throw lands
			throw Bad($"break window ({window}) must be shorter than the recovery ({move.Recovery}) (E12)");
		return move with
		{
			IsCommandGrab = true, Throwboxes = boxes, Hitboxes = Array.Empty<TimedBox>(),
			BreakWindow = window, Breakable = breakable, GrabRange = range,
		};
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
