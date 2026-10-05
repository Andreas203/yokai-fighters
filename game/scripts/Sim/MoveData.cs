using System;
using System.Collections.Generic;

namespace YokaiFighters.Sim;

/// <summary>2D rectangle in gameplay units relative to the feet, x toward the opponent, y up (F3, F4).</summary>
public readonly record struct Box(int X, int Y, int W, int H);

/// <summary>A box live on move frames First..Last inclusive (1-based from the move's first frame).</summary>
public readonly record struct TimedBox(int First, int Last, Box Box)
{
	public bool Covers(int frame) => frame >= First && frame <= Last;
}

/// <summary>
/// One move's Lv 1 frame data, read from the YOK-12 schema (data/schema/frame-data.schema.json).
/// Every gameplay number here comes from data; the sim never special-cases a move (A5, A10).
/// Frames are 1-based: startup S, active A, recovery R; active frames are S+1..S+A (F1).
/// </summary>
public sealed record MoveData
{
	public required string Id { get; init; }
	public required int Startup { get; init; }
	/// <summary>0 when the body never hits ("active": null, projectile moves).</summary>
	public required int Active { get; init; }
	public required int Recovery { get; init; }
	public required int Damage { get; init; }
	public int Hitstun { get; init; }
	public int Blockstun { get; init; }
	/// <summary>Units the defender slides on hit / block; null falls back to SimConfig.</summary>
	public int? HitPushback { get; init; }
	public int? BlockPushback { get; init; }
	public bool Knockdown { get; init; }
	/// <summary>C3: must be blocked crouching.</summary>
	public bool Low { get; init; }
	public IReadOnlyList<TimedBox> Hitboxes { get; init; } = Array.Empty<TimedBox>();
	public IReadOnlyList<TimedBox> Hurtboxes { get; init; } = Array.Empty<TimedBox>();

	public int TotalFrames => Startup + Active + Recovery;
	public int FirstActive => Startup + 1;
	public int LastActive => Startup + Active;
	public bool IsActive(int frame) => Active > 0 && frame >= FirstActive && frame <= LastActive;

	/// <summary>Frame advantage if the first active frame connects (hitstun or blockstun minus the attacker's remaining frames).</summary>
	public int AdvantageOnHit => Hitstun - (Active - 1 + Recovery);
	public int AdvantageOnBlock => Blockstun - (Active - 1 + Recovery);
}
