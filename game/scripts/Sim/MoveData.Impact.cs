using System;
using System.Text.Json;

namespace YokaiFighters.Sim;

/// <summary>V2 hit strength: picks the hitstop length (and V3 screen shake for heavy).</summary>
public enum HitStrength
{
	Light,
	Medium,
	Heavy,
}

/// <summary>YOK-20: how hard a move lands (V2) and what meter it pays (C5).</summary>
public sealed partial record MoveData
{
	/// <summary>
	/// V2 strength from <c>frame_data.strength</c>. When the data omits it, a normal takes it from its
	/// button (LP/LK light, MP/MK medium, HP/HK heavy), a throw is heavy (E12: hitstop and shake as a
	/// heavy hit) and any other move is medium.
	/// </summary>
	public HitStrength Strength { get; init; } = HitStrength.Medium;
	/// <summary>True for an EX version (YOK-21 sets it): EX hits shake the screen like heavies (V3).</summary>
	public bool Ex { get; init; }
	/// <summary><c>frame_data.meter_gain</c> overrides of the C5 attacker gains; null = SimConfig default.</summary>
	public int? MeterOnHit { get; init; }
	public int? MeterOnBlock { get; init; }

	public static HitStrength StrengthOfButton(InputBits button) => button switch
	{
		InputBits.LightPunch or InputBits.LightKick => HitStrength.Light,
		InputBits.HeavyPunch or InputBits.HeavyKick => HitStrength.Heavy,
		_ => HitStrength.Medium,
	};

	/// <summary>Reads strength and meter_gain from a frame_data block (MoveLoader).</summary>
	public static MoveData ReadImpact(MoveData move, JsonElement fd)
	{
		HitStrength strength = move.IsNormal ? StrengthOfButton(move.Button)
			: move.IsThrow ? HitStrength.Heavy : HitStrength.Medium;
		if (fd.TryGetProperty("strength", out var s))
			strength = s.GetString() switch
			{
				"light" => HitStrength.Light,
				"medium" => HitStrength.Medium,
				"heavy" => HitStrength.Heavy,
				var other => throw new FormatException($"{move.Id}: frame_data.strength '{other}' is not light, medium or heavy"),
			};
		int? onHit = null, onBlock = null;
		if (fd.TryGetProperty("meter_gain", out var mg) && mg.ValueKind == JsonValueKind.Object)
		{
			if (mg.TryGetProperty("on_hit", out var h)) onHit = h.GetInt32();
			if (mg.TryGetProperty("on_block", out var b)) onBlock = b.GetInt32();
		}
		return move with { Strength = strength, MeterOnHit = onHit, MeterOnBlock = onBlock };
	}
}
