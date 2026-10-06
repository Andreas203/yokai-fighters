using System;

namespace YokaiFighters.Sim;

/// <summary>YOK-20: a fighter's meter (C5) and once-per-fight burst (C6).</summary>
public sealed partial class Fighter
{
	/// <summary>Meter points, 0..SimConfig.MeterMax.</summary>
	public int Meter;
	/// <summary>C6: the burst has been spent this fight.</summary>
	public bool BurstUsed;

	/// <summary>No hurtbox at all (burst invulnerability, or getting up after a knockdown). Knockdown is handled separately.</summary>
	public bool Invulnerable => State is FighterState.Burst or FighterState.WakeUp;

	public void GainMeter(int points, int max) => Meter = Math.Clamp(Meter + points, 0, max);

	/// <summary>Spend meter if there is enough (EX specials: 1 bar). False and no change otherwise.</summary>
	public bool TrySpendMeter(int cost)
	{
		if (cost < 0 || Meter < cost) return false;
		Meter -= cost;
		return true;
	}

	internal void ResetMeter()
	{
		Meter = 0;
		BurstUsed = false;
	}

	internal ulong HashMeter(ulong h)
	{
		h = Fnv.Mix(h, Meter);
		return Fnv.Mix(h, BurstUsed ? 1 : 0);
	}
}
