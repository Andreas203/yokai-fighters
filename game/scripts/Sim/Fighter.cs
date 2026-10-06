namespace YokaiFighters.Sim;

/// <summary>
/// One fighter's simulation state. Plain integers so the whole match can be hashed and replayed.
/// X/Y are centi-units on the 2D gameplay plane (F4): X along the stage, Y up from the floor.
/// </summary>
public sealed partial class Fighter
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

	// --- Move state machine (YOK-16) -----------------------------------------------------
	/// <summary>This fighter's moves, by slot (loaded from data at fight start).</summary>
	public MoveData[] Moves = System.Array.Empty<MoveData>();
	public FighterState State;
	/// <summary>Slot of the move in progress (State == Attack), else -1.</summary>
	public int MoveSlot = -1;
	/// <summary>1-based frame of the move in progress; the frame the move starts on is frame 1.</summary>
	public int MoveFrame;
	/// <summary>The current move already connected (one hit per move).</summary>
	public bool MoveConnected;
	/// <summary>Stun frames still to play after this one (hitstun, blockstun, knockdown).</summary>
	public int StunLeft;
	/// <summary>Holding back this frame while able to block (C3).</summary>
	public bool Guarding;
	public bool Crouching;

	// --- Movement and input (YOK-18) ---------------------------------------------------------
	/// <summary>This fighter's input layer (YOK-17): raw history, scheme parser, command buffer.</summary>
	public InputReader Input = new();
	/// <summary>0 = grounded; 1..JumpFrames = frame of the jump arc (C2). Y follows the arc.</summary>
	public int AirFrame;
	/// <summary>Absolute X drift sign of the jump (-1, 0, +1); a hit in the air stops it.</summary>
	public int JumpDir;
	/// <summary>E11 (YOK-55): this jump's air normal is spent (or the fighter is falling from a hit); cleared on jump and landing.</summary>
	public bool AirAttackUsed;
	/// <summary>1-based dash frame (State == Dash) and absolute dash direction.</summary>
	public int DashFrame;
	public int DashDir;

	public bool Airborne => AirFrame > 0;
	public MoveData? CurrentMove => State == FighterState.Attack ? ActiveMove : null;
	/// <summary>Free to start a move, jump, dash or walk this frame (only ever on the ground).</summary>
	public bool Actionable => State == FighterState.Idle && !KnockedOut;

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
		State = FighterState.Idle;
		MoveSlot = -1;
		MoveFrame = 0;
		MoveConnected = false;
		StunLeft = 0;
		Guarding = false;
		Crouching = false;
		Input.Reset();
		AirFrame = 0;
		JumpDir = 0;
		AirAttackUsed = false;
		DashFrame = 0;
		DashDir = 0;
		ResetSpecialState(); // YOK-21 (equipped specials and held cancel rules are run state and stay)
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
		h = Fnv.Mix(h, (int)State);
		h = Fnv.Mix(h, MoveSlot);
		h = Fnv.Mix(h, MoveFrame);
		h = Fnv.Mix(h, MoveConnected ? 1 : 0);
		h = Fnv.Mix(h, StunLeft);
		h = Fnv.Mix(h, (Guarding ? 1 : 0) | (Crouching ? 2 : 0));
		h = Fnv.Mix(h, AirFrame);
		h = Fnv.Mix(h, JumpDir);
		h = Fnv.Mix(h, AirAttackUsed ? 1 : 0);
		h = Fnv.Mix(h, DashFrame);
		h = Fnv.Mix(h, DashDir);
		h = Fnv.Mix(h, (int)Input.Pending.Kind);
		return HashSpecialState(h);
	}
}

public enum FighterState
{
	Idle,
	Attack,
	Hitstun,
	Blockstun,
	/// <summary>On the floor after a knockdown move; no hurtbox.</summary>
	Knockdown,
	/// <summary>C2: dashing forward or back for DashFrames frames.</summary>
	Dash,
	/// <summary>C2: in the air (jump arc, or falling after an air hit's stun ended); lands into Idle.</summary>
	Jump,
	/// <summary>C4 (YOK-19): grabbed. StunLeft = break-window frames still open; then the throw lands.</summary>
	Thrown,
	/// <summary>C6 (YOK-20): bursting out of hitstun; invulnerable, no control, for BurstFrames frames.</summary>
	Burst,
	/// <summary>E11 (YOK-55): touched down during an air normal; standing, no control or guard, throwable, StunLeft frames.</summary>
	Landing,
}

/// <summary>FNV-1a over ints, for state hashes in determinism and replay checks.</summary>
public static class Fnv
{
	public const ulong Offset = 14695981039346656037UL;
	private const ulong Prime = 1099511628211UL;

	/// <summary>YOK-47: mixes a string's UTF-16 code units (ids in run state), stable across runs.</summary>
	public static ulong MixString(ulong h, string s)
	{
		h = Mix(h, s.Length);
		foreach (char c in s) h = Mix(h, c);
		return h;
	}

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
