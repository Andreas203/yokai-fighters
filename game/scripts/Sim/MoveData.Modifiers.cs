using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-103: <c>properties.armour</c> = { frames: [first, last], hits? }. Inside the window the move takes a hit
/// without being interrupted (damage still lands), up to <see cref="Hits"/> hits per use of the move.
/// Hits default to 1: "shrug off a hit" (Oni's Hide card); how many hits armour takes is an open designer question.
/// </summary>
public sealed record ArmourWindow(int First, int Last, int Hits = 1)
{
	public bool Covers(int frame) => frame >= First && frame <= Last;

	/// <summary>Reads the JSON shape; false (no throw) when it isn't one.</summary>
	public static bool TryRead(JsonNode? node, out ArmourWindow? window)
	{
		window = null;
		if (node is not JsonObject o || o["frames"] is not JsonArray fr || fr.Count != 2) return false;
		if (o.Any(kv => kv.Key is not ("frames" or "hits"))) return false;
		if (fr[0] is not JsonValue a || !a.TryGetValue(out int first) || fr[1] is not JsonValue b || !b.TryGetValue(out int last)) return false;
		int hits = 1;
		if (o["hits"] is { } h && (h is not JsonValue hv || !hv.TryGetValue(out hits))) return false;
		if (first < 1 || last < first || hits < 1) return false;
		window = new ArmourWindow(first, last, hits);
		return true;
	}
}

/// <summary>YOK-103: the move as built with these start conditions held (see <see cref="MoveData.StartVariants"/>).</summary>
public sealed record MoveVariant(StartConditions Conditions, MoveData Move);

/// <summary>
/// YOK-103 partial: the move properties modifiers add (A10, data only) and the conditional variants a modifier's
/// <c>when</c> builds (<see cref="SpecialData.Build(int,bool,ModifierData,SimConfig)"/>). The fight reads them
/// generically in Match (ApplyHit, Throwable, StepProjectiles, TryStartSpecial); nothing is keyed on a modifier id.
/// </summary>
public sealed partial record MoveData
{
	/// <summary><c>properties.counterhit_immune</c>: a hit during this move is never a counterhit (C7).</summary>
	public bool CounterhitImmune { get; init; }
	/// <summary><c>properties.projectile_interrupt_immune</c>: a projectile hit deals damage but doesn't interrupt the move.</summary>
	public bool ProjectileInterruptImmune { get; init; }
	/// <summary><c>properties.armour</c>, or null.</summary>
	public ArmourWindow? Armour { get; init; }
	/// <summary><c>properties.throw_invuln: "startup"</c>: not throwable during the move's startup frames.</summary>
	public bool ThrowInvulnStartup { get; init; }
	/// <summary><c>properties.pull_on_hit</c>: on hit the defender is pulled this many steps toward the attacker instead of pushed.</summary>
	public int PullOnHit { get; init; }
	/// <summary><c>properties.heal_pct_max_health</c>: on hit the attacker heals this % of its max health (rounded to nearest).</summary>
	public int HealPctMaxHealth { get; init; }
	/// <summary><c>properties.projectile.angle_up</c>: the projectile climbs as it flies.</summary>
	public bool ProjectileAngleUp { get; init; }

	/// <summary>This move from its connect on when it hits (a modifier's <c>on_hit</c> effects), or null = unchanged.</summary>
	public MoveData? OnHitVariant { get; init; }
	/// <summary>This move from its connect on when it is blocked (<c>on_block</c>), or null = unchanged.</summary>
	public MoveData? OnBlockVariant { get; init; }
	/// <summary>Builds of this move for each set of start conditions a modifier reads; empty = no conditions.</summary>
	public IReadOnlyList<MoveVariant> StartVariants { get; init; } = Array.Empty<MoveVariant>();

	/// <summary>The move as it plays from a connect on: the on_hit / on_block variant, else itself.</summary>
	public MoveData Outcome(bool blocked) => (blocked ? OnBlockVariant : OnHitVariant) ?? this;

	/// <summary>The build for the start conditions held now (conditions no effect reads are ignored).</summary>
	public MoveData ForStart(StartConditions held)
	{
		MoveData best = this;
		int bestBits = 0;
		foreach (var v in StartVariants)
		{
			if ((v.Conditions & ~held) != 0) continue;
			int bits = System.Numerics.BitOperations.PopCount((uint)v.Conditions);
			if (bits > bestBits) { best = v.Move; bestBits = bits; }
		}
		return best;
	}

	/// <summary>The start conditions this move's variants read (bits worth computing when it starts).</summary>
	public StartConditions StartMask => StartVariants.Aggregate(StartConditions.None, (m, v) => m | v.Conditions);

	/// <summary>Applies <paramref name="f"/> to the move and its outcome variants (Kata precision keeps holding after a connect).</summary>
	public MoveData WithOutcomes(Func<MoveData, MoveData> f) =>
		f(this) with { OnHitVariant = OnHitVariant is { } h ? f(h) : null, OnBlockVariant = OnBlockVariant is { } b ? f(b) : null };

	/// <summary>Reads the YOK-103 modifier properties from a frame_data block (MoveLoader); rejects shapes the fight can't run.</summary>
	internal static MoveData ReadModifierProperties(MoveData move, JsonElement fd)
	{
		if (!fd.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object) return move;
		bool Flag(JsonElement o, string n) => o.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.True;
		int NonNegative(string n)
		{
			if (!props.TryGetProperty(n, out var v)) return 0;
			if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out int i) || i < 0)
				throw new FormatException($"{move.Id}: properties.{n} must be a whole number >= 0");
			return i;
		}
		ArmourWindow? armour = null;
		if (props.TryGetProperty("armour", out var a))
		{
			if (!ArmourWindow.TryRead(JsonNode.Parse(a.GetRawText()), out armour) || armour!.Last > move.TotalFrames)
				throw new FormatException($"{move.Id}: properties.armour needs frames [first, last] inside the move (1-{move.TotalFrames})");
		}
		bool throwInvuln = false;
		if (props.TryGetProperty("throw_invuln", out var ti))
		{
			if (ti.ValueKind != JsonValueKind.String || ti.GetString() != Effects.ThrowInvulnStartup)
				throw new FormatException($"{move.Id}: properties.throw_invuln must be \"{Effects.ThrowInvulnStartup}\"");
			throwInvuln = true;
		}
		bool angleUp = props.TryGetProperty("projectile", out var p) && p.ValueKind == JsonValueKind.Object && Flag(p, "angle_up");
		return move with
		{
			CounterhitImmune = Flag(props, "counterhit_immune"),
			ProjectileInterruptImmune = Flag(props, "projectile_interrupt_immune"),
			Armour = armour,
			ThrowInvulnStartup = throwInvuln,
			PullOnHit = NonNegative("pull_on_hit"),
			HealPctMaxHealth = NonNegative("heal_pct_max_health"),
			ProjectileAngleUp = angleUp,
		};
	}
}
