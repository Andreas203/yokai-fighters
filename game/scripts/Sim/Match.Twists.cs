using System;

namespace YokaiFighters.Sim;

/// <summary>YOK-90: an elder twist changed what happened. <paramref name="Holder"/> = the elder's fighter index.</summary>
public readonly record struct TwistEvent(int Holder, TwistEffect Effect, string MoveId);

/// <summary>
/// YOK-90 (P5, Y2): elder twists in the fight, read from <see cref="Fighter.Twist"/> (the elder's fighter holds it;
/// data/twists/). Integer-only and deterministic. Hooks: <see cref="TryStartSpecial"/> counts uses in a row;
/// <see cref="ResolveHits"/> and <see cref="ResolveProjectiles"/> reflect; <see cref="ApplyHit"/> asks
/// <see cref="TwistArmourTakes"/>; <see cref="ResolveThrowBreaks"/> asks <see cref="Unbreakable"/>.
/// <list type="bullet">
/// <item>Reflect (Nine-Tails): the opponent's specials this fight, in the order started; a special is "in a row"
/// with the one started just before it when it is the same special (same data id; EX and plain alike, the EX upgrade
/// is not a new use). Normals, throws, jumps, dashes and bursts do not break the row; a different special starts a
/// new one. From the twist's <c>repeat</c>-th use on, when that special would connect with the elder (hit or block):
/// a projectile turns around and becomes hers; a strike lands on its user instead, with its own numbers.</item>
/// <item>Armour (Elder Oni): each of the elder's specials takes up to <c>hits</c> unblocked hits from frame 1 to its
/// last active frame; the damage lands, the special is not interrupted (YOK-103's armour reading: hitstun,
/// knockdown and the drift stop are skipped, pushback is not).</item>
/// <item>Unbreakable throws (Elder Kappa): the opponent's break press in the C4 window does nothing; a same-frame
/// throw clash (E12) still clashes.</item>
/// </list>
/// </summary>
public sealed partial class Match
{
	/// <summary>An elder twist reflected a special, armoured a hit or refused a throw break.</summary>
	public event Action<Match, TwistEvent>? TwistTriggered;

	private readonly string?[] _lastSpecial = new string?[2];
	private readonly int[] _inRow = new int[2];

	private void ResetTwistState()
	{
		_lastSpecial[0] = _lastSpecial[1] = null;
		_inRow[0] = _inRow[1] = 0;
	}

	/// <summary>Fighter <paramref name="i"/> just started <paramref name="specialId"/>: counted only against a reflect twist.</summary>
	private void CountSpecialUse(int i, string specialId)
	{
		if (Fighters[1 - i].Twist?.Effect != TwistEffect.ReflectRepeatedSpecial) return;
		if (_lastSpecial[i] == specialId) _inRow[i]++;
		else { _lastSpecial[i] = specialId; _inRow[i] = 1; }
		Fighters[i].ActiveRepeat = _inRow[i];
	}

	/// <summary>Does the fighter at <paramref name="holder"/> reflect a special that is use <paramref name="repeat"/> in a row?</summary>
	private bool Reflects(int holder, int repeat) =>
		repeat > 0 && Fighters[holder].Twist is { Effect: TwistEffect.ReflectRepeatedSpecial } t && repeat >= t.Repeat;

	/// <summary>Reflect, strike: the move is spent and lands on its user, as if the elder had hit with it.</summary>
	private void ReflectStrike(int attIndex, MoveData m, int frame)
	{
		Fighters[attIndex].MoveConnected = true;
		TwistTriggered?.Invoke(this, new TwistEvent(1 - attIndex, TwistEffect.ReflectRepeatedSpecial, m.Id));
		ApplyHit(1 - attIndex, m, frame, defenderWasAttacking: false, projectile: null, reflected: true);
	}

	/// <summary>Reflect, projectile: it turns around and is the elder's from now on (it can hit or be blocked by its firer).</summary>
	private void ReflectProjectile(Projectile p)
	{
		p.Owner = 1 - p.Owner;
		p.Dir = -p.Dir;
		p.Repeat = 0;
		TwistTriggered?.Invoke(this, new TwistEvent(p.Owner, TwistEffect.ReflectRepeatedSpecial, p.Move.Id));
		ProjectileChanged?.Invoke(this, new ProjectileEvent(p.Owner, p.Move.Id, ProjectileEventKind.Reflected, p.X));
	}

	/// <summary>Armour: does the defender's special take this unblocked hit without being interrupted? Counts the hit.</summary>
	private bool TwistArmourTakes(int defIndex)
	{
		Fighter def = Fighters[defIndex];
		if (def.Twist is not { Effect: TwistEffect.SpecialArmour } t) return false;
		if (def.State != FighterState.Attack || def.ActiveSpecial == SpecialSlot.None || def.ActiveMove is not { } m) return false;
		if (def.MoveFrame > m.LastActive || def.TwistArmourTaken >= t.ArmourHits) return false;
		def.TwistArmourTaken++;
		TwistTriggered?.Invoke(this, new TwistEvent(defIndex, TwistEffect.SpecialArmour, m.Id));
		return true;
	}

	/// <summary>Unbreakable throws: the thrower holds the twist.</summary>
	private bool Unbreakable(int throwerIndex) => Fighters[throwerIndex].Twist?.Effect == TwistEffect.UnbreakableThrows;

	private ulong HashTwistState(ulong h)
	{
		if (P1.Twist is null && P2.Twist is null) return h; // other fights hash as before
		h = Fnv.MixString(h, _lastSpecial[0] ?? "");
		h = Fnv.MixString(h, _lastSpecial[1] ?? "");
		h = Fnv.Mix(h, _inRow[0]);
		return Fnv.Mix(h, _inRow[1]);
	}
}
