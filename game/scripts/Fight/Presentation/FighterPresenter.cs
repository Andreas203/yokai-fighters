using System;
using System.Collections.Generic;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>A pose to show: clip + exact 1-based frame, optionally blended from another clip's frame.</summary>
public readonly record struct PoseSample(PoseKind Kind, string? Clip, int Frame, string? FromClip, int FromFrame, float Weight)
{
	public bool Blending => FromClip != null && Weight < 1f;
}

/// <summary>
/// YOK-53: maps one fighter's sim state to a clip and an exact frame (F3: presentation is stepped by sim frames,
/// never by the AnimationPlayer). Pure C#, read-only on the sim. <see cref="Observe"/> runs after every sim tick
/// (FightScene.StepSim), so segment starts, loops and blends count world frames: hitstop freezes them, the V4 KO
/// slow-down halves them, frame-step advances them by exactly one, and the render rate never matters.
/// </summary>
public sealed class FighterPresenter
{
	public const int DefaultBlend = 4;   // clip notes: 4-6 tick blend-outs
	public const int CrouchBlend = 6;    // crouch down/up is a data blend (clip notes)
	public const int GetUpBlend = 6;     // get-up end → the next pose (clip notes: a 6-tick blend is enough)
	public const int HitBlend = 2;       // a hit reaction should read at once
	/// <summary>
	/// Wake-up roll: the knockdown clip ends face up, the get-up clip starts face down (~150° apart, clip notes).
	/// The sim's WakeUp state (SimConfig.WakeUpFrames) plays the get-up clip's last WakeUpFrames frames; over the
	/// first WakeRollBlend of them the pose blends from the knockdown's final frame, so the body turns over onto
	/// its front while it starts to rise instead of popping. Designer proposal alongside WakeUpFrames.
	/// </summary>
	public const int WakeRollBlend = 10;

	private readonly FighterAnimSet _set;
	private readonly ClipCatalog _cat;
	private readonly IReadOnlyDictionary<string, string> _moveClips;

	private PoseKind _kind;
	private string? _clip;
	private int _segStart;      // world frame the segment began
	private int _frameBase = 1; // clip frame shown on the segment's first world frame
	private bool _loop;
	private string? _fromClip;
	private int _fromFrame;
	private int _blendLen;
	private int _lastWorld = int.MinValue;
	private int _prevX;
	private int _prevStun;
	private int _prevMoveFrame;
	private string? _prevMoveId;
	private int _prevAir;
	private bool _lowHit;

	public FighterPresenter(FighterAnimSet set, ClipCatalog catalog, IReadOnlyDictionary<string, string> moveClips)
	{
		_set = set;
		_cat = catalog;
		_moveClips = moveClips;
	}

	public FighterAnimSet Set => _set;

	/// <summary>The pose for the current sim state (call after Observe).</summary>
	public PoseSample Sample { get; private set; }

	/// <summary>Forget history (fight reset); the next Observe starts fresh with no blend.</summary>
	public void Reset() { _lastWorld = int.MinValue; _clip = null; _fromClip = null; }

	public void Observe(Match m, int i)
	{
		Fighter f = m.Fighters[i];
		Fighter opp = m.Fighters[1 - i];
		int world = m.WorldFrame;
		bool fresh = _lastWorld == int.MinValue || world < _lastWorld;
		if (!fresh && world == _lastWorld)
		{
			// Hitstop or KO slow-down hold: nothing moves, so the pose holds (no dx-based walk flicker).
			return;
		}

		var (kind, clip, frameBase, loop, restart) = Classify(m, f, opp, fresh);
		if (fresh || kind != _kind || clip != _clip || restart)
		{
			if (!fresh && _clip != null && Sample.Clip != null)
			{
				_fromClip = Sample.Clip;
				_fromFrame = Sample.Frame;
				_blendLen = BlendInto(_kind, kind, f);
			}
			else _fromClip = null;
			_kind = kind; _clip = clip; _segStart = world; _frameBase = frameBase; _loop = loop;
		}

		int elapsed = world - _segStart;
		int frame = FrameFor(f, elapsed);
		float weight = 1f;
		if (_fromClip != null && _blendLen > 0 && elapsed < _blendLen - 1) weight = (elapsed + 1f) / _blendLen;
		else _fromClip = null;
		Sample = new PoseSample(_kind, _clip, frame, _fromClip, _fromFrame, weight);

		_lastWorld = world;
		_prevX = f.X;
		_prevStun = f.StunLeft;
		_prevMoveFrame = f.State == FighterState.Attack ? f.MoveFrame : 0;
		_prevMoveId = f.CurrentMove?.Id;
		_prevAir = f.AirFrame;
	}

	private int FrameFor(Fighter f, int elapsed)
	{
		var info = _cat[_clip];
		int total = info?.FramesTotal ?? 1;
		switch (_kind)
		{
			case PoseKind.Attack:
				return Math.Clamp(f.MoveFrame, 1, total);
			case PoseKind.Jump:
			case PoseKind.JumpBack:
				return Math.Clamp(f.AirFrame + _set.JumpFrameOffset, 1, total);
			case PoseKind.GetUp:
				// Wake-up frame k of N has StunLeft N-k: the clip's last N frames, ending on its last frame.
				return Math.Clamp(total - f.StunLeft, 1, total);
		}
		int n = _frameBase + elapsed;
		return _loop ? ((n - 1) % total + total) % total + 1 : Math.Clamp(n, 1, total);
	}

	private (PoseKind, string?, int, bool, bool) Classify(Match m, Fighter f, Fighter opp, bool fresh)
	{
		string? C(PoseKind k) => _set.ClipFor(k);
		if (f.KnockedOut) return (PoseKind.Ko, C(PoseKind.Ko), 1, false, false);
		switch (f.State)
		{
			case FighterState.Attack:
			{
				var move = f.CurrentMove;
				string? clip = null;
				if (move != null && _moveClips.TryGetValue(move.Id, out var c)) clip = _set.MoveClip(c);
				if (!_cat.Has(clip)) clip = move != null && move.IsThrow ? FallbackThrow() : C(PoseKind.Attack);
				bool restart = f.MoveFrame < _prevMoveFrame || move?.Id != _prevMoveId;
				return (PoseKind.Attack, clip, 1, false, restart);
			}
			case FighterState.Hitstun:
			case FighterState.Thrown:
			{
				bool restart = f.StunLeft > _prevStun && !fresh; // re-hit
				if (_kind is not (PoseKind.HitHigh or PoseKind.HitLow) || restart)
					_lowHit = f.Crouching || (opp.CurrentMove?.Low ?? false);
				var k = f.State == FighterState.Thrown ? PoseKind.Thrown : _lowHit ? PoseKind.HitLow : PoseKind.HitHigh;
				return (k, C(k), 1, false, restart);
			}
			case FighterState.Blockstun:
			{
				var k = f.Crouching ? PoseKind.BlockLow : PoseKind.BlockHigh;
				return (k, C(k), 1, false, f.StunLeft > _prevStun && !fresh);
			}
			case FighterState.Knockdown:
				return (PoseKind.Knockdown, C(PoseKind.Knockdown), 1, false, false);
			case FighterState.WakeUp:
				return _cat.Has(C(PoseKind.GetUp))
					? (PoseKind.GetUp, C(PoseKind.GetUp), 1, false, false)
					: (PoseKind.Knockdown, C(PoseKind.Knockdown), 1, false, false); // no get-up clip: stay down until actionable
			case FighterState.Dash:
			{
				var k = f.DashDir == f.Facing ? PoseKind.Dash : PoseKind.DashBack;
				return (k, C(k), 1, false, false);
			}
			case FighterState.Jump:
			{
				var k = f.JumpDir != 0 && f.JumpDir != f.Facing && _cat.Has(C(PoseKind.JumpBack)) ? PoseKind.JumpBack : PoseKind.Jump;
				return (k, C(k), 1, false, f.AirFrame < _prevAir);
			}
			case FighterState.Landing:
				return (PoseKind.Landing, C(PoseKind.Landing), _set.LandingFrame, false, false);
			case FighterState.Burst:
				return (PoseKind.Burst, C(PoseKind.Burst), _set.BurstFrame, false, false);
		}
		// Idle: crouch, walk or stand. Walk only when the fighter took its own walk step this frame and
		// moved that way (YOK-39): being pushed by the opponent's push box slides the guard stance instead
		// of playing a walk cycle with no input, and holding back against a corner doesn't walk in place.
		if (f.Crouching) return (PoseKind.Crouch, C(PoseKind.Crouch), 1, true, false);
		int dx = fresh ? 0 : f.X - _prevX;
		if (f.WalkDir != 0 && Math.Sign(dx) == f.WalkDir)
		{
			var k = f.WalkDir == f.Facing ? PoseKind.WalkFwd : PoseKind.WalkBack;
			return (k, C(k), 1, true, false);
		}
		// The idle loop runs on the world frame so it never restarts between moves.
		int idleTotal = _cat[C(PoseKind.Idle)]?.FramesTotal ?? 1;
		return (PoseKind.Idle, C(PoseKind.Idle), m.WorldFrame % idleTotal + 1, true, false);
	}

	private string? FallbackThrow() => _set.Fighter == "kitsune" ? "kitsune-throw" : "ryo-generic-throw";

	private int BlendInto(PoseKind from, PoseKind to, Fighter f)
	{
		if (to is PoseKind.HitHigh or PoseKind.HitLow or PoseKind.Thrown or PoseKind.BlockHigh or PoseKind.BlockLow) return HitBlend;
		if (to == PoseKind.GetUp && from == PoseKind.Knockdown) return WakeRollBlend;
		if (to == PoseKind.GetUp || from == PoseKind.GetUp) return GetUpBlend;
		if (to == PoseKind.Crouch || from == PoseKind.Crouch) return CrouchBlend;
		if (to == PoseKind.Attack) return Math.Min(DefaultBlend, Math.Max(1, f.CurrentMove?.Startup ?? DefaultBlend));
		return DefaultBlend;
	}
}
