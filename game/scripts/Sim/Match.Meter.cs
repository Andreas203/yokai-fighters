using System;

namespace YokaiFighters.Sim;

/// <summary>
/// A move connected, as presentation needs it (YOK-20): its V2 strength, the hitstop it started and
/// whether it shakes the screen (V3: heavy hits and EX only, never lights).
/// </summary>
public readonly record struct ImpactEvent(int Attacker, string MoveId, HitStrength Strength, int Hitstop, bool Blocked, bool Counter, bool Shake);

/// <summary>YOK-20: meter (C5), burst (C6) and hitstop (V2), hooked into Match's step order.</summary>
public sealed partial class Match
{
	/// <summary>
	/// V2: ticks left in which both fighters are frozen. A connect on tick h freezes ticks h+1..h+N,
	/// both sides alike, so frame advantage is unchanged. Inputs still feed the readers (buffering)
	/// and a burst may fire.
	/// </summary>
	public int HitstopLeft { get; private set; }

	/// <summary>Raised for every connect, after the Hit event's state changes and before damage applies.</summary>
	public event Action<Match, ImpactEvent>? Impact;
	/// <summary>C6: fighter index that burst.</summary>
	public event Action<Match, int>? BurstFired;

	public const InputBits BurstButtons = InputBits.LightPunch | InputBits.MediumPunch | InputBits.HeavyPunch;

	/// <summary>EX hook for YOK-21: spend meter (an EX special costs SimConfig.ExCost = 1 bar, C5).</summary>
	public bool TrySpendMeter(int fighterIndex, int cost) =>
		Phase == MatchPhase.Fighting && Fighters[fighterIndex].TrySpendMeter(cost);

	public bool TrySpendEx(int fighterIndex) => TrySpendMeter(fighterIndex, Config.ExCost);

	private void ResetMeterState()
	{
		HitstopLeft = 0;
		P1.ResetMeter();
		P2.ResetMeter();
	}

	/// <summary>A hitstop tick: the world does not advance. False when there is no hitstop.</summary>
	private bool HitstopStep(FighterInput in1, FighterInput in2)
	{
		if (HitstopLeft == 0) return false;
		HitstopLeft--;
		P1.Input.Update(in1, P1.Facing);
		P2.Input.Update(in2, P2.Facing);
		P1.PrevBits = in1.Bits;
		P2.PrevBits = in2.Bits;
		bool b0 = TryBurst(0), b1 = TryBurst(1);
		if (b0 || b1) HitstopLeft = 0; // the burst breaks the freeze
		return true;
	}

	/// <summary>C5 meter and V2 hitstop for one connect. Called from ApplyHit.</summary>
	private void OnConnect(int attIndex, MoveData m, bool blocked, bool counter)
	{
		Fighter att = Fighters[attIndex], def = Fighters[1 - attIndex];
		int max = Config.MeterMax;
		if (blocked) att.GainMeter(m.MeterOnBlock ?? Config.MeterOnBlock, max);
		else
		{
			att.GainMeter(m.MeterOnHit ?? Config.MeterOnHit, max);
			def.GainMeter(Config.MeterOnTaken, max);
		}

		int stop = Config.Hitstop(m.Strength, counter);
		HitstopLeft = Math.Max(HitstopLeft, stop); // a trade freezes for the longer of the two
		bool shake = m.Ex || (!blocked && m.Strength == HitStrength.Heavy);
		Impact?.Invoke(this, new ImpactEvent(attIndex, m.Id, m.Strength, stop, blocked, counter, shake));
	}

	/// <summary>All three punches pressed within BurstPressWindow ticks, the last one this tick, all still held.</summary>
	public bool BurstPressed(Fighter f)
	{
		var buf = f.Input.Buffer;
		if ((buf.At(0) & BurstButtons) != BurstButtons) return false;
		if ((buf.PressedAt(0) & BurstButtons) == 0) return false;
		InputBits seen = InputBits.None;
		for (int age = 0; age < Config.BurstPressWindow; age++) seen |= buf.PressedAt(age);
		return (seen & BurstButtons) == BurstButtons;
	}

	/// <summary>
	/// C6: once per fight, from hitstun: 20 invulnerable frames (the burst tick plus BurstFrames-1),
	/// all current meter spent, the opponent thrown back (proposed distance).
	/// </summary>
	private bool TryBurst(int i)
	{
		Fighter f = Fighters[i], o = Fighters[1 - i];
		if (Phase != MatchPhase.Fighting || f.BurstUsed || f.KnockedOut || f.State != FighterState.Hitstun) return false;
		if (!BurstPressed(f)) return false;

		f.BurstUsed = true;
		f.Meter = 0;
		SetState(f, FighterState.Burst, Config.BurstFrames - 1);
		f.JumpDir = 0;
		f.Guarding = false;
		f.Crouching = false;

		int dir = o.X != f.X ? Math.Sign(o.X - f.X) : f.Facing;
		int lo = -Config.StageHalfWidth + Config.BodyWidth / 2, hi = Config.StageHalfWidth - Config.BodyWidth / 2;
		o.X = Math.Clamp(o.X + dir * Config.BurstPushback * SimConfig.Scale, lo, hi);
		BurstFired?.Invoke(this, i);
		return true;
	}

	private ulong HashMeterState(ulong h)
	{
		h = Fnv.Mix(h, HitstopLeft);
		h = P1.HashMeter(h);
		return P2.HashMeter(h);
	}
}
