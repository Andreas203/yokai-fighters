using System;
using System.Collections.Generic;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// One projectile as drawn on one tick (YOK-242): its sim serial and owner, the special and level it belongs to, its
/// look, the data hitbox (<see cref="Box"/>, world centi-units, exactly what the sim hits with) and the quad drawn over
/// it (<see cref="X0"/>..<see cref="Y1"/>), the direction it flies, its age in sim ticks and its flipbook frame.
/// </summary>
public readonly record struct ProjectileVisual(int Serial, int Owner, string? Special, int Level, VfxStyle Style,
	(int x0, int y0, int x1, int y1) Box, int X0, int Y0, int X1, int Y1, int Dir, int Age, int SheetFrame);

/// <summary>
/// One effect (YOK-242), spawned on sim tick <see cref="SpawnTick"/>. A <see cref="VfxAnchor.Point"/> effect sits at
/// (<see cref="X"/>, <see cref="Y"/>), its quad's centre in world centi-units; an <see cref="VfxAnchor.Owner"/> effect
/// is offset by them from its owner and ends when the owner stops playing use <see cref="MoveSerial"/> of the move.
/// W and H are the quad's size in centi-units.
/// </summary>
public readonly record struct VfxEffect(int SpawnTick, VfxMoment Moment, int Owner, int MoveSerial, VfxAnchor Anchor,
	int X, int Y, int W, int H, int Dir, int Frames, VfxStyle Style, string? Special = null);

/// <summary>An effect on one tick: its age, where its quad's centre is (world centi-units), opacity, life progress (percent) and flipbook frame.</summary>
public readonly record struct VfxEffectFrame(VfxEffect Effect, int Age, int CentreX, int CentreY, int AlphaPct, int LifePct, int SheetFrame);

/// <summary>
/// YOK-242: projectile visuals and special-move effects of one fight, presentation only (V8, A5). Fed by the sim's
/// events (<see cref="Match.Impact"/>, <see cref="Match.ProjectileChanged"/>, <see cref="Match.Throw"/>) and by
/// <see cref="Observe"/> once per sim tick (cast and release); read by <see cref="VfxLayer"/>. It never writes the
/// match and the sim never reads it, so <see cref="Match.StateHash"/> is unchanged. Timed by the match's tick and each
/// projectile's <see cref="Projectile.Age"/>: an effect spawned on tick t shows on ticks t .. t + Frames - 1, plays
/// through hitstop, freezes with the sim, and is the same at any render rate and in a replay. No RNG.
/// </summary>
public sealed class SpecialFx
{
	/// <summary>Effects kept at once; the oldest goes first (placeholder).</summary>
	public const int Capacity = 16;

	private readonly List<VfxEffect> _fx = new();
	private readonly int[] _castSerial = { -1, -1 };
	private readonly int[] _releaseSerial = { -1, -1 };
	private readonly HashSet<int> _seenProjectiles = new();
	private readonly Dictionary<(int, string), int> _projectileY = new();
	private Match? _match;
	private int _resets;
	private int _lastTick = -1;

	public LevelPresetLibrary Presets { get; set; }

	public SpecialFx(LevelPresetLibrary? presets = null) { Presets = presets ?? LevelPresetLibrary.Empty; }

	public int Count => _fx.Count;
	public IReadOnlyList<VfxEffect> All => _fx;
	/// <summary>The match whose events this listens to (<see cref="Attach"/>), or null.</summary>
	public Match? Attached { get; private set; }

	/// <summary>Listens to <paramref name="match"/>'s events (and stops listening to the previous one). Subscribing changes nothing in the sim.</summary>
	public void Attach(Match match)
	{
		Detach();
		Attached = match;
		match.Impact += OnImpact;
		match.ProjectileChanged += OnProjectile;
		match.Throw += OnThrow;
		Follow(match);
	}

	public void Detach()
	{
		if (Attached is { } m)
		{
			m.Impact -= OnImpact;
			m.ProjectileChanged -= OnProjectile;
			m.Throw -= OnThrow;
		}
		Attached = null;
	}

	public void Clear()
	{
		_fx.Clear();
		_castSerial[0] = _castSerial[1] = -1;
		_releaseSerial[0] = _releaseSerial[1] = -1;
		_seenProjectiles.Clear();
		_projectileY.Clear();
		_lastTick = -1;
		_match = null;
	}

	/// <summary>
	/// The hook every special shares, data or not: spawn an effect (captures and future callers use it directly).
	/// Look <see cref="VfxLook.None"/> or a projectile look spawns nothing.
	/// </summary>
	public void Spawn(in VfxEffect e)
	{
		if (e.Style.Look == VfxLook.None || VfxLooks.IsProjectile(e.Style.Look) || e.Frames <= 0) return;
		if (_fx.Count >= Capacity) _fx.RemoveAt(0);
		_fx.Add(e);
	}

	/// <summary>
	/// Call once after every sim tick: a special that started this tick casts, one that reached its first active frame
	/// (no projectile) releases, a projectile that appeared this tick releases at the hand. Reads the match only.
	/// </summary>
	public void Observe(Match match)
	{
		Follow(match);
		Sync(match.Tick);
		for (int i = 0; i < 2; i++)
		{
			Fighter f = match.Fighters[i];
			if (f.State != FighterState.Attack) continue;
			var move = f.ActiveBase ?? f.CurrentMove;
			var eq = f.ActiveSpecial != SpecialSlot.None ? f.Specials[(int)f.ActiveSpecial] : null;
			if (move is null) continue;
			if (_castSerial[i] != f.MoveSerial)
			{
				_castSerial[i] = f.MoveSerial;
				if (eq != null) Fire(match, VfxMoment.Cast, i, f.MoveSerial, move, eq, f.X, f.Y, f.Facing);
			}
			if (eq != null && move.Projectile is null && _releaseSerial[i] != f.MoveSerial && f.MoveFrame >= move.FirstActive)
			{
				_releaseSerial[i] = f.MoveSerial;
				Fire(match, VfxMoment.Release, i, f.MoveSerial, move, eq, f.X, f.Y, f.Facing);
			}
		}
		foreach (var p in match.Projectiles)
		{
			var (x0, y0, x1, y1) = p.WorldBox();
			_projectileY[(p.Owner, p.Move.Id)] = (y0 + y1) / 2;
			if (!_seenProjectiles.Add(p.Serial)) continue;
			if (Find(match, p.Owner, p.Move.Id) is not { } found) continue;
			var preset = Presets.For(found.Eq.Data.Id, found.Eq.Level);
			var style = SpecialVfxStyle.EffectStyle(VfxMoment.Release, p.Move, preset, found.Eq.Data.Source);
			int tail = p.Dir >= 0 ? x0 : x1; // the trailing edge on the spawn tick = where it left the hand
			AddPoint(match, VfxMoment.Release, p.Owner, p.Move, preset, style, tail, (y0 + y1) / 2, p.Dir, found.Eq.Data.Id);
		}
	}

	/// <summary>The effects of <paramref name="match"/> showing on its current tick (what the layer draws), oldest first.</summary>
	public List<VfxEffectFrame> Visible(Match match)
	{
		if (!ReferenceEquals(match, _match) || match.Resets != _resets || match.Tick < _lastTick) { Clear(); Follow(match); }
		_lastTick = match.Tick;
		int tick = match.Tick;
		var shown = new List<VfxEffectFrame>(_fx.Count);
		_fx.RemoveAll(e => tick - e.SpawnTick >= e.Frames || (e.Anchor == VfxAnchor.Owner && !StillPlaying(match, e)));
		foreach (var e in _fx)
		{
			int age = tick - e.SpawnTick;
			if (age < 0) continue;
			int cx = e.X, cy = e.Y;
			if (e.Anchor == VfxAnchor.Owner)
			{
				Fighter f = match.Fighters[e.Owner];
				cx = f.X + e.X * e.Dir;
				cy = f.Y + e.Y;
			}
			int fromEnd = e.Frames - age;
			int alpha = fromEnd > SpecialVfxStyle.FadeFrames ? 100 : 100 * fromEnd / (SpecialVfxStyle.FadeFrames + 1);
			int cols = Math.Max(1, e.Style.SheetColumns);
			shown.Add(new VfxEffectFrame(e, age, cx, cy, alpha, 100 * age / Math.Max(1, e.Frames), Math.Min(cols - 1, age * cols / Math.Max(1, e.Frames))));
		}
		return shown;
	}

	/// <summary>Every projectile in flight on the match's current tick, with its look and the quad over its data hitbox. Pure.</summary>
	public static List<ProjectileVisual> Projectiles(Match match, LevelPresetLibrary presets)
	{
		var list = new List<ProjectileVisual>(match.Projectiles.Count);
		foreach (var p in match.Projectiles)
		{
			var (source, special, level) = HitSparks.Identify(match, p.Owner, p.Move.Id);
			var style = SpecialVfxStyle.ProjectileStyle(presets.For(special, level), source);
			var box = p.WorldBox();
			var (x0, y0, x1, y1) = SpecialVfxStyle.ProjectileQuad(box, p.Dir, style);
			int cols = Math.Max(1, style.SheetColumns);
			list.Add(new ProjectileVisual(p.Serial, p.Owner, special, level, style, box, x0, y0, x1, y1, p.Dir, p.Age, p.Age / SpecialVfxStyle.SheetTicks % cols));
		}
		return list;
	}

	// --- event handlers (run inside Match.Step; they only append to the effect list) ---------------------------------

	private void OnImpact(Match m, ImpactEvent e)
	{
		Follow(m);
		Sync(m.Tick);
		if (Find(m, e.Attacker, e.MoveId) is not { } found || found.Move.IsGrab) return; // a grab's whirl comes from Throw
		var preset = Presets.For(found.Eq.Data.Id, found.Eq.Level);
		var style = SpecialVfxStyle.EffectStyle(VfxMoment.Impact, found.Move, preset, found.Eq.Data.Source);
		Fighter att = m.Fighters[e.Attacker];
		int dir = e.At.X != att.X ? Math.Sign(e.At.X - att.X) : att.Facing;
		AddPoint(m, VfxMoment.Impact, e.Attacker, found.Move, preset, style, e.At.X, e.At.Y, dir, found.Eq.Data.Id);
	}

	private void OnProjectile(Match m, ProjectileEvent e)
	{
		if (e.Kind is not (ProjectileEventKind.Clash or ProjectileEventKind.Absorbed)) return;
		Follow(m);
		Sync(m.Tick);
		if (Find(m, e.Owner, e.MoveId) is not { } found) return;
		var preset = Presets.For(found.Eq.Data.Id, found.Eq.Level);
		var style = SpecialVfxStyle.EffectStyle(VfxMoment.Impact, found.Move, preset, found.Eq.Data.Source);
		int y = _projectileY.TryGetValue((e.Owner, e.MoveId), out int py) ? py : m.Fighters[e.Owner].Y + 200 * SimConfig.Scale;
		AddPoint(m, VfxMoment.Impact, e.Owner, found.Move, preset, style, e.X, y, m.Fighters[e.Owner].Facing, found.Eq.Data.Id);
	}

	private void OnThrow(Match m, ThrowEvent e)
	{
		if (e.Outcome != ThrowOutcome.Grab) return;
		Follow(m);
		Sync(m.Tick);
		if (Find(m, e.Attacker, e.MoveId) is not { } found) return; // generic throws (C4) are not specials: no effect
		var preset = Presets.For(found.Eq.Data.Id, found.Eq.Level);
		var style = SpecialVfxStyle.EffectStyle(VfxMoment.Impact, found.Move, preset, found.Eq.Data.Source);
		Fighter a = m.Fighters[e.Attacker], d = m.Fighters[1 - e.Attacker];
		var shape = SpecialVfxStyle.ShapeOf(style.Look);
		AddPoint(m, VfxMoment.Impact, e.Attacker, found.Move, preset, style, (a.X + d.X) / 2, d.Y + shape.OffY * SimConfig.Scale, a.Facing, found.Eq.Data.Id);
	}

	// --- helpers ----------------------------------------------------------------------------------------------------

	/// <summary>A cast or a body release: placed from the owner by the look's offsets, or riding on the owner.</summary>
	private void Fire(Match m, VfxMoment moment, int owner, int serial, MoveData move, EquippedSpecial eq, int x, int y, int facing)
	{
		var preset = Presets.For(eq.Data.Id, eq.Level);
		var style = SpecialVfxStyle.EffectStyle(moment, move, preset, eq.Data.Source);
		if (style.Look == VfxLook.None) return;
		var shape = SpecialVfxStyle.ShapeOf(style.Look);
		int s = SimConfig.Scale;
		int w = shape.W * s * style.ScalePct / 100, h = shape.H * s * style.ScalePct / 100;
		int frames = SpecialVfxStyle.FramesOf(style.Look, move, SpecialVfxStyle.BlockFor(preset, moment));
		int offY = style.Look == VfxLook.Dust ? h / 2 : shape.OffY * s; // dust sits on the ground
		if (shape.Anchor == VfxAnchor.Owner)
			Spawn(new VfxEffect(m.Tick, moment, owner, serial, VfxAnchor.Owner, shape.OffX * s, offY, w, h, facing, frames, style, eq.Data.Id));
		else
			Spawn(new VfxEffect(m.Tick, moment, owner, serial, VfxAnchor.Point, x + shape.OffX * s * facing, (style.Look == VfxLook.Dust ? 0 : y) + offY, w, h, facing, frames, style, eq.Data.Id));
	}

	/// <summary>An effect at a known point (impact, projectile release); dust drops to the ground under it.</summary>
	private void AddPoint(Match m, VfxMoment moment, int owner, MoveData move, LevelPreset? preset, VfxStyle style, int x, int y, int dir, string special)
	{
		if (style.Look == VfxLook.None) return;
		var shape = SpecialVfxStyle.ShapeOf(style.Look);
		int s = SimConfig.Scale;
		int w = shape.W * s * style.ScalePct / 100, h = shape.H * s * style.ScalePct / 100;
		if (style.Look == VfxLook.Dust) y = h / 2;
		int frames = SpecialVfxStyle.FramesOf(style.Look, move, SpecialVfxStyle.BlockFor(preset, moment));
		if (shape.Anchor == VfxAnchor.Owner) frames = Math.Min(frames, shape.Frames > 0 ? shape.Frames : 20); // a moving look at a fixed point: brief
		Spawn(new VfxEffect(m.Tick, moment, owner, m.Fighters[owner].MoveSerial, VfxAnchor.Point, x, y, w, h, dir, frames, style, special));
	}

	private static bool StillPlaying(Match m, in VfxEffect e)
	{
		Fighter f = m.Fighters[e.Owner];
		return f.State == FighterState.Attack && f.MoveSerial == e.MoveSerial;
	}

	/// <summary>The equipped special a move id belongs to (base, EX or evolution), the attacker's kit first, then the other's (a reflect).</summary>
	public static (EquippedSpecial Eq, MoveData Move)? Find(Match m, int attacker, string moveId)
	{
		for (int k = 0; k < 2; k++)
		{
			Fighter f = m.Fighters[k == 0 ? attacker : 1 - attacker];
			foreach (var s in f.Specials)
			{
				if (s is null) continue;
				if (s.Move.Id == moveId) return (s, s.Move);
				if (s.ExMove?.Id == moveId) return (s, s.ExMove);
				if (s.Data.Id == moveId) return (s, s.Move);
			}
		}
		return null;
	}

	private void Follow(Match match)
	{
		if (ReferenceEquals(match, _match) && match.Resets == _resets) return;
		Clear();
		_match = match;
		_resets = match.Resets;
	}

	/// <summary>A tick earlier than the last one seen means the match was reset: its effects are gone.</summary>
	private void Sync(int tick)
	{
		if (tick < _lastTick)
		{
			_fx.Clear();
			_seenProjectiles.Clear();
			_castSerial[0] = _castSerial[1] = _releaseSerial[0] = _releaseSerial[1] = -1;
		}
		_lastTick = tick;
	}
}
