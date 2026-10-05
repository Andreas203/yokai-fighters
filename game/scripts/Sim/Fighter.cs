namespace YokaiFighters.Sim;

/// <summary>
/// One fighter's simulation state. Plain integers so the whole match can be hashed and replayed.
/// X/Y are centi-units on the 2D gameplay plane (F4): X along the stage, Y up from the floor.
/// </summary>
public sealed class Fighter
{
	public int X;
	public int Y;
	/// <summary>+1 faces right (toward +X), -1 faces left.</summary>
	public int Facing;
	public int Health;
	public int MaxHealth;
	public bool KnockedOut;
	/// <summary>Last tick's input, for press (edge) detection.</summary>
	public InputBits PrevBits;
	/// <summary>Damage queued this world frame; applied simultaneously at the end of the frame.</summary>
	internal int PendingDamage;

	public bool Pressed(FighterInput input, InputBits b) => input.Has(b) && (PrevBits & b) == 0;

	public void Reset(int x, int facing, int maxHealth)
	{
		X = x;
		Y = 0;
		Facing = facing;
		MaxHealth = maxHealth;
		Health = maxHealth;
		KnockedOut = false;
		PrevBits = InputBits.None;
		PendingDamage = 0;
	}

	public ulong Hash(ulong h)
	{
		h = Fnv.Mix(h, X);
		h = Fnv.Mix(h, Y);
		h = Fnv.Mix(h, Facing);
		h = Fnv.Mix(h, Health);
		h = Fnv.Mix(h, MaxHealth);
		h = Fnv.Mix(h, KnockedOut ? 1 : 0);
		h = Fnv.Mix(h, (int)PrevBits);
		return h;
	}
}

/// <summary>FNV-1a over ints, for state hashes in determinism and replay checks.</summary>
public static class Fnv
{
	public const ulong Offset = 14695981039346656037UL;
	private const ulong Prime = 1099511628211UL;

	public static ulong Mix(ulong h, int v)
	{
		unchecked
		{
			uint u = (uint)v;
			for (int i = 0; i < 4; i++)
			{
				h ^= (byte)(u >> (8 * i));
				h *= Prime;
			}
		}
		return h;
	}
}

/// <summary>Seeded xorshift32. The only RNG gameplay may use (F3: seeded RNG only).</summary>
public sealed class SimRng
{
	private uint _s;
	public SimRng(uint seed) => _s = seed == 0 ? 0x9E3779B9u : seed;
	public uint NextUInt()
	{
		_s ^= _s << 13;
		_s ^= _s >> 17;
		_s ^= _s << 5;
		return _s;
	}
	/// <summary>Uniform-ish integer in [0, max).</summary>
	public int Next(int max) => (int)(NextUInt() % (uint)max);
}
