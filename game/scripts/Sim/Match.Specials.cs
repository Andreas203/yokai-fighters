using System;
using System.Collections.Generic;

namespace YokaiFighters.Sim;

/// <summary>A special started (or upgraded to EX in its first frames).</summary>
public readonly record struct SpecialEvent(int Fighter, SpecialSlot Slot, string MoveId, int Level, bool Ex, bool Precision);

public enum ProjectileEventKind { Spawn, Hit, Blocked, Clash, Absorbed, OffScreen, Expired }

public readonly record struct ProjectileEvent(int Owner, string MoveId, ProjectileEventKind Kind, int X);

/// <summary>
/// YOK-21: a projectile in flight. Lives on the 2D plane in world centi-units like a fighter; its hit
/// carries the firing move's numbers (damage incl. Kata precision/EX, stun, pushback, strength, meter).
/// </summary>
public sealed class Projectile
{
	public int Owner;
	public int Serial;
	public required MoveData Move;
	public required ProjectileData Data;
	public int X, Y, Dir;
	/// <summary>World frames since it spawned (0 on the spawn tick).</summary>
	public int Age;
	public int HitsLeft;
	public int AbsorbsLeft;

	/// <summary>Hitbox in world centi-units, mirrored by the direction it flies.</summary>
	public (int x0, int y0, int x1, int y1) WorldBox()
	{
		int s = SimConfig.Scale;
		Box b = Data.Box;
		int x0 = Dir > 0 ? X + b.X * s : X - (b.X + b.W) * s;
		int y0 = Y + b.Y * s;
		return (x0, y0, x0 + b.W * s, y0 + b.H * s);
	}
}

/// <summary>
/// YOK-21: special slots A-D (A1), EX (C5), Kata precision (K1), projectiles and the cancel-rule hook (X1-X4).
/// Hooks in Match.WorldStep: a parsed Special command or a direct <see cref="FighterInput.Special"/> request →
/// <see cref="TryStartSpecial"/>; while a special plays, <see cref="TryExUpgrade"/> then <see cref="TryCancel"/>;
/// <see cref="StepProjectiles"/> after positions, <see cref="ResolveProjectiles"/> after body hits.
/// Slot resolution lives here, not in the parsers, so Kata and Kihon share it (K3).
/// </summary>
public sealed partial class Match
{
	public List<Projectile> Projectiles { get; } = new();
	private int _projectileSerial;

	public event Action<Match, SpecialEvent>? SpecialStarted;
	public event Action<Match, ProjectileEvent>? ProjectileChanged;

	public const InputBits Punches = InputBits.LightPunch | InputBits.MediumPunch | InputBits.HeavyPunch;
	public const InputBits Kicks = InputBits.LightKick | InputBits.MediumKick | InputBits.HeavyKick;

	/// <summary>
	/// EX input (designer proposal, SimConfig.ExPressWindow): Kata, two punches or two kicks; Kihon (YOK-23,
	/// designer proposal), Special + any one punch or kick. The Special bit only reaches here from the Kihon parser.
	/// </summary>
	public static bool ExPair(InputBits pressed) =>
		(pressed & InputBits.Special) != 0
			? (pressed & InputBits.Attacks) != 0
			: System.Numerics.BitOperations.PopCount((uint)(pressed & Punches)) >= 2
			  || System.Numerics.BitOperations.PopCount((uint)(pressed & Kicks)) >= 2;

	public int ProjectileCount(int owner)
	{
		int n = 0;
		foreach (var p in Projectiles) if (p.Owner == owner) n++;
		return n;
	}

	/// <summary>
	/// Starts the special in <paramref name="slot"/>. False (nothing happens) when the slot is empty or the
	/// move's projectile is already on screen as often as its max_active allows. EX without the meter
	/// falls back to the plain version and spends nothing.
	/// </summary>
	public bool TryStartSpecial(int i, SpecialSlot slot, bool ex, bool precision, InputBits buttons)
	{
		Fighter f = Fighters[i];
		EquippedSpecial? eq = slot == SpecialSlot.None ? null : f.Specials[(int)slot];
		if (eq is null) return false;
		if (eq.Move.Projectile is { } pd && ProjectileCount(i) >= pd.MaxActive) return false;
		MoveData m = eq.Move;
		bool isEx = ex && eq.ExMove != null && TrySpendMeter(i, eq.Data.ExCost);
		if (isEx) m = eq.ExMove!;

		f.State = FighterState.Attack;
		f.ResetSpecialMoveState();
		f.ActiveMove = precision ? WithPrecision(m) : m;
		f.MoveSlot = -1;
		f.MoveFrame = 1;
		f.MoveConnected = false;
		f.Guarding = false;
		f.Crouching = false;
		f.ActiveSpecial = slot;
		f.ActiveEx = isEx;
		f.ActivePrecision = precision;
		f.SpecialButtons = buttons & (InputBits.Attacks | InputBits.Special); // Special: Kihon (YOK-23)
		SpecialStarted?.Invoke(this, new SpecialEvent(i, slot, m.Id, eq.Level, isEx, precision));
		return true;
	}

	/// <summary>K1: a Kata motion input deals +PrecisionBonusPct damage (projectile hits included).</summary>
	private MoveData WithPrecision(MoveData m) => m with { Damage = m.Damage * (100 + Config.PrecisionBonusPct) / 100 };

	/// <summary>
	/// EX leniency: a second punch (or kick) pressed while the special is still within its first
	/// ExPressWindow frames turns it into the EX version in place (same frame), if the meter allows.
	/// </summary>
	private bool TryExUpgrade(int i)
	{
		Fighter f = Fighters[i];
		if (f.ActiveSpecial == SpecialSlot.None || f.ActiveEx || f.SpecialButtons == InputBits.None
			|| f.MoveFrame > Config.ExPressWindow) return false;
		EquippedSpecial? eq = f.Specials[(int)f.ActiveSpecial];
		if (eq?.ExMove is null) return false;
		InputBits now = f.Input.Buffer.PressedAt(0) & InputBits.Attacks;
		if ((now & ~f.SpecialButtons) == 0 || !ExPair(f.SpecialButtons | now)) return false;
		if (!TrySpendMeter(i, eq.Data.ExCost)) return false;
		f.ActiveMove = f.ActivePrecision ? WithPrecision(eq.ExMove) : eq.ExMove;
		f.ActiveEx = true;
		f.SpecialButtons |= now;
		f.Input.TryConsume(out _); // the second press re-parsed as the same special; it's used up here
		SpecialStarted?.Invoke(this, new SpecialEvent(i, f.ActiveSpecial, eq.ExMove.Id, eq.Level, true, f.ActivePrecision));
		return true;
	}

	/// <summary>
	/// X1-X4 framework hook: a held cancel rule lets the current move, inside one of its data cancel windows
	/// and on a listed outcome, cancel into a dash or a special, each target once per combo (X3). Ryo holds
	/// no rules at run start (X1), so nothing cancels by default.
	/// </summary>
	public bool TryCancel(int i)
	{
		Fighter f = Fighters[i];
		MoveData? m = f.CurrentMove;
		if (m is null || f.CancelRules.Count == 0) return false;
		bool whiff = !f.MoveConnected, block = f.MoveConnected && f.MoveBlocked, hit = f.MoveConnected && !f.MoveBlocked;
		bool inWindow = false;
		foreach (var w in m.CancelWindows)
			if (w.Covers(f.MoveFrame) && (whiff && w.OnWhiff || block && w.OnBlock || hit && w.OnHit)) inWindow = true;
		if (!inWindow) return false;

		foreach (var rule in f.CancelRules)
		{
			if (!(whiff && rule.OnWhiff || block && rule.OnBlock || hit && rule.OnHit)) continue;
			bool from = rule.From switch
			{
				CancelFrom.Specials => f.ActiveSpecial != SpecialSlot.None,
				CancelFrom.HeavyNormals => m.IsNormal && m.Button is InputBits.HeavyPunch or InputBits.HeavyKick,
				CancelFrom.Normals => m.IsNormal,
				CancelFrom.SuccessfulThrows => m.IsThrow && f.MoveConnected,
				_ => false,
			};
			if (!from) continue;
			if (rule.Into == CancelInto.Dash)
			{
				if (f.Input.Dash == 0 || (f.CancelUsed & DashCancelBit) != 0) continue;
				f.CancelUsed |= DashCancelBit;
				StartDash(f, f.Input.Dash * f.Facing);
				return true;
			}
			InputCommand cmd = f.Input.Pending;
			if (cmd.Kind != CommandKind.Special || (f.CancelUsed & (1 << (int)cmd.Slot)) != 0) continue;
			if (!TryStartSpecial(i, cmd.Slot, ExPair(cmd.Pressed), cmd.Precision, cmd.Pressed)) continue;
			f.Input.TryConsume(out _);
			f.CancelUsed |= 1 << (int)cmd.Slot;
			return true;
		}
		return false;
	}

	private const int DashCancelBit = 1 << 5;

	/// <summary>Travel, lifetime/off-screen despawn, then spawns on the firing move's spawn frame.</summary>
	private void StepProjectiles()
	{
		int cx = FightCamera.CenterX(this), half = Config.ViewWidth / 2;
		for (int k = 0; k < Projectiles.Count; k++)
		{
			var p = Projectiles[k];
			p.Age++;
			p.X += p.Dir * p.Data.Speed * SimConfig.Scale;
			var (x0, _, x1, _) = p.WorldBox();
			ProjectileEventKind? gone = p.Data.Lifetime > 0 && p.Age >= p.Data.Lifetime ? ProjectileEventKind.Expired
				: x1 <= cx - half || x0 >= cx + half ? ProjectileEventKind.OffScreen : null;
			if (gone is { } g)
			{
				Projectiles.RemoveAt(k--);
				ProjectileChanged?.Invoke(this, new ProjectileEvent(p.Owner, p.Move.Id, g, p.X));
			}
		}
		for (int i = 0; i < 2; i++)
		{
			Fighter f = Fighters[i];
			if (f.CurrentMove is not { Projectile: { } pd } m || f.MoveFrame != pd.SpawnFrame) continue;
			if (ProjectileCount(i) >= pd.MaxActive) continue;
			var p = new Projectile
			{
				Owner = i, Serial = ++_projectileSerial, Move = m, Data = pd,
				X = f.X, Y = f.Y, Dir = f.Facing, HitsLeft = pd.Hits, AbsorbsLeft = pd.AbsorbsProjectiles,
			};
			Projectiles.Add(p);
			ProjectileChanged?.Invoke(this, new ProjectileEvent(i, m.Id, ProjectileEventKind.Spawn, p.X));
		}
	}

	/// <summary>Projectile vs projectile (clash, absorb, pass), then projectile vs fighter.</summary>
	private void ResolveProjectiles()
	{
		if (Projectiles.Count == 0) return;
		foreach (var a in Projectiles)
			foreach (var b in Projectiles)
				if (a.Owner == 0 && b.Owner == 1 && a.HitsLeft > 0 && b.HitsLeft > 0 && Overlaps(a.WorldBox(), b.WorldBox()))
					Clash(a, b);

		bool any = false;
		foreach (var p in Projectiles)
		{
			if (p.HitsLeft <= 0) continue;
			Fighter def = Fighters[1 - p.Owner];
			if (def.State == FighterState.Knockdown || def.KnockedOut || def.Invulnerable) continue;
			if (def.CurrentMove?.InvulnAt(def.MoveFrame, InvulnAgainst.Projectile) == true) continue;
			if (!HurtOverlaps(def, p.WorldBox())) continue;
			bool blocked = ApplyHit(p.Owner, p.Move, p.Age, def.CurrentMove != null, p);
			p.HitsLeft--;
			any = true;
			ProjectileChanged?.Invoke(this, new ProjectileEvent(p.Owner, p.Move.Id,
				blocked ? ProjectileEventKind.Blocked : ProjectileEventKind.Hit, p.X));
		}
		Projectiles.RemoveAll(p => p.HitsLeft <= 0);
		if (any) ResolvePositions(stackalloc int[2]);
	}

	/// <summary>
	/// Two projectiles meet: one that passes projectiles ignores the other (both fly on); one that can still
	/// absorb eats the other whole; otherwise each loses one hit (equal hits cancel out).
	/// </summary>
	private void Clash(Projectile a, Projectile b)
	{
		if (a.Data.PassesProjectiles || b.Data.PassesProjectiles) return;
		bool aAbs = a.AbsorbsLeft > 0, bAbs = b.AbsorbsLeft > 0;
		if (aAbs != bAbs)
		{
			var (eater, eaten) = aAbs ? (a, b) : (b, a);
			eater.AbsorbsLeft--;
			eaten.HitsLeft = 0;
			ProjectileChanged?.Invoke(this, new ProjectileEvent(eaten.Owner, eaten.Move.Id, ProjectileEventKind.Absorbed, eaten.X));
			return;
		}
		if (aAbs) { a.AbsorbsLeft--; b.AbsorbsLeft--; }
		a.HitsLeft--;
		b.HitsLeft--;
		ProjectileChanged?.Invoke(this, new ProjectileEvent(a.Owner, a.Move.Id, ProjectileEventKind.Clash, a.X));
		ProjectileChanged?.Invoke(this, new ProjectileEvent(b.Owner, b.Move.Id, ProjectileEventKind.Clash, b.X));
	}

	private ulong HashProjectiles(ulong h)
	{
		h = Fnv.Mix(h, Projectiles.Count);
		h = Fnv.Mix(h, _projectileSerial);
		foreach (var p in Projectiles)
		{
			h = Fnv.Mix(h, p.Owner | (p.Dir > 0 ? 2 : 0));
			h = Fnv.Mix(h, p.Serial);
			h = Fnv.Mix(h, p.X);
			h = Fnv.Mix(h, p.Y);
			h = Fnv.Mix(h, p.Age);
			h = Fnv.Mix(h, p.HitsLeft);
			h = Fnv.Mix(h, p.AbsorbsLeft);
			h = Fnv.Mix(h, p.Move.Damage);
		}
		return h;
	}
}
