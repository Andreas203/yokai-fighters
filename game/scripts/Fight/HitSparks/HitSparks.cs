using System;
using System.Collections.Generic;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>YOK-198: which outcome a spark shows. Each has its own look (shape and image).</summary>
public enum HitSparkKind : byte { Hit, Block, Counter }

/// <summary>Placeholder procedural shapes, used while no image exists for an outcome (<see cref="HitSparkArt"/>).</summary>
public enum HitSparkShape : byte { Burst, Ring, Star }

/// <summary>
/// How one spark looks: colour 0xRRGGBB, size in percent of <see cref="HitSparkStyle.BaseSizeMeters"/>, life in sim
/// ticks, the placeholder shape, the preset's image (relative to game/, null = the default image for the outcome),
/// its flipbook columns and whether the colour tints it.
/// </summary>
public sealed record HitSparkLook(uint Colour, int SizePct, int Frames, HitSparkShape Shape, string? Texture = null, int SheetColumns = 1, bool Tint = true);

/// <summary>
/// One spark: spawned on the sim tick of the impact at the contact point (world centi-units, <see cref="ImpactEvent.At"/>).
/// <paramref name="Dir"/> = +1 when the blow travels toward +X (images may be drawn pointing that way).
/// </summary>
public readonly record struct HitSpark(int SpawnTick, int Attacker, HitSparkKind Kind, int X, int Y, int Dir, int RotationDeg, HitSparkLook Look);

/// <summary>A spark on one tick: its age, size and opacity in percent, and the flipbook frame.</summary>
public readonly record struct HitSparkFrame(HitSpark Spark, int Age, int ScalePct, int AlphaPct, int SheetFrame);

/// <summary>
/// The frame-timed sound hook (V6, YOK-198): one cue per impact on the impact tick, with everything a cue table
/// needs to pick the thud and the yokai sting. Nothing listens yet: the animator's cues (data/sound/, after the
/// sound purchase, YOK-54) attach here; the scene's placeholder hit-pack sound is separate and unchanged.
/// </summary>
public readonly record struct HitCue(int Tick, int Attacker, string MoveId, string Source, string? Special, int Level,
	HitStrength Strength, HitSparkKind Kind, int X, int Y);

/// <summary>Receives <see cref="HitCue"/>s. Presentation only.</summary>
public interface IHitCueSink
{
	void Play(in HitCue cue);
}

/// <summary>The default sink: does nothing.</summary>
public sealed class SilentHitCues : IHitCueSink
{
	public static readonly SilentHitCues Instance = new();
	public void Play(in HitCue cue) { }
}

/// <summary>
/// YOK-198 default spark looks (V5) and how a level preset (V8) changes them. Pure C#. Every number here is a
/// PLACEHOLDER for the procedural art, kept until the generated textures and the designer's presets land.
/// </summary>
public static class HitSparkStyle
{
	// V5 colours by source, from the palette in assets/specs/style.md.
	public const uint Ink = 0x1D1B21;        // Ryo's own ink black
	public const uint Foxfire = 0xF2A23A;    // foxfire orange (Kitsune)
	public const uint KappaTeal = 0x3E7F86;  // kappa teal = water teal (style.md, confirmed 2026-10-20)
	public const uint OniRed = 0xB5332B;     // PROVISIONAL: shrine red until the designer names "oni red" (not in style.md)
	public const uint Neutral = 0xF1E8D4;    // rice paper: a move with no known source (test fixtures)

	/// <summary>On-screen width of a 100% spark at the Z = 0 plane (placeholder).</summary>
	public const float BaseSizeMeters = 0.35f;
	/// <summary>Ticks a spark fades over after the hitstop it rides on (placeholder).</summary>
	public const int FadeFrames = 6;
	/// <summary>Ticks a spark grows in over, from <see cref="PopStartPct"/> to full size (placeholder).</summary>
	public const int PopFrames = 2;
	public const int PopStartPct = 60;
	/// <summary>A block spark is this much of the hit spark's size (placeholder: the blow did no damage).</summary>
	public const int BlockSizePct = 70;

	/// <summary>V5: the spark colour of a move's source (its file's <c>source</c>, or an elder's yokai).</summary>
	public static uint SourceColour(string? source) => source switch
	{
		"ryo" or "tanuki" => Ink, // the Tanuki fights as Ryo, with Ryo's ink (assets/specs/tanuki.md)
		"kitsune" or "nine-tailed-kitsune" => Foxfire,
		"oni" or "elder-oni" => OniRed,
		"kappa" or "elder-kappa" => KappaTeal,
		_ => Neutral,
	};

	public static HitSparkKind KindOf(in ImpactEvent e) => e.Blocked ? HitSparkKind.Block : e.Counter ? HitSparkKind.Counter : HitSparkKind.Hit;

	public static HitSparkShape ShapeOf(HitSparkKind k) => k switch
	{
		HitSparkKind.Block => HitSparkShape.Ring,
		HitSparkKind.Counter => HitSparkShape.Star,
		_ => HitSparkShape.Burst,
	};

	/// <summary>
	/// V2 hitstop the hit carries in the default config (6 / 9 / 12, counterhit +4), whether or not the fight has
	/// hitstop switched off: the spark's size follows the hit's weight.
	/// </summary>
	public static int Weight(HitStrength s, bool counter)
	{
		var c = SimConfig.Default;
		return (s == HitStrength.Light ? c.HitstopLight : s == HitStrength.Medium ? c.HitstopMedium : c.HitstopHeavy)
			+ (counter ? c.CounterHitstop : 0);
	}

	/// <summary>The look of one impact: the default spark for its source and weight, then the preset's changes.</summary>
	public static HitSparkLook Look(HitSparkKind kind, HitStrength strength, string? source, LevelPreset? preset)
	{
		int weight = Weight(strength, kind == HitSparkKind.Counter);
		int size = 100 * weight / SimConfig.Default.HitstopLight; // light 100, medium 150, heavy 200, counter more
		if (kind == HitSparkKind.Block) size = size * BlockSizePct / 100;
		int frames = weight + FadeFrames;
		uint colour = SourceColour(source);
		if (preset is null) return new HitSparkLook(colour, size, frames, ShapeOf(kind));
		var s = preset.Spark;
		return new HitSparkLook(s.Colour ?? preset.Colour, size * (s.ScalePct ?? 100) / 100, s.Frames ?? frames, ShapeOf(kind),
			s.TextureFor(kind), s.SheetColumns, s.Tint);
	}

	/// <summary>Size, opacity and flipbook frame <paramref name="age"/> ticks after the spawn (0 = the impact tick).</summary>
	public static HitSparkFrame FrameOf(HitSpark s, int age)
	{
		int life = s.Look.Frames;
		int scale = age >= PopFrames ? 100 : PopStartPct + (100 - PopStartPct) * age / PopFrames;
		int fade = Math.Min(FadeFrames, life);
		int fromEnd = life - age; // 1 on the last tick shown
		int alpha = fromEnd > fade ? 100 : 100 * fromEnd / (fade + 1);
		int cols = Math.Max(1, s.Look.SheetColumns);
		int frame = Math.Min(cols - 1, age * cols / life);
		return new HitSparkFrame(s, age, scale, alpha, frame);
	}
}

/// <summary>
/// YOK-198: the hit sparks of one fight, presentation only (V5, V8). Fed by the sim's <see cref="Match.Impact"/>
/// and read by the renderer; the sim never reads it. Timed by the match's own tick: a spark spawned on tick t shows
/// on ticks t .. t + Frames - 1, so it plays through hitstop, freezes with the sim (pause, frame step) and is
/// exactly the same at any render rate. No RNG: the rotation comes from the tick and the attacker.
/// </summary>
public sealed class HitSparks
{
	/// <summary>Sparks kept at once; the oldest goes first (placeholder).</summary>
	public const int Capacity = 12;

	private readonly List<HitSpark> _sparks = new();
	private int _lastTick = -1;
	/// <summary>The match the sparks belong to, and its <see cref="Match.Resets"/> when they were spawned.</summary>
	private Match? _match;
	private int _resets;

	public LevelPresetLibrary Presets { get; set; }

	public HitSparks(LevelPresetLibrary? presets = null) { Presets = presets ?? LevelPresetLibrary.Empty; }

	public int Count => _sparks.Count;
	public IReadOnlyList<HitSpark> All => _sparks;

	public void Clear()
	{
		_sparks.Clear();
		_lastTick = -1;
		_match = null;
	}

	/// <summary>
	/// One impact: resolve the move's source and level, spawn its spark at <see cref="ImpactEvent.At"/> and return
	/// the sound cue for the same tick. Call from the <see cref="Match.Impact"/> handler (the match's Tick is the
	/// tick being stepped).
	/// </summary>
	public HitCue OnImpact(Match match, in ImpactEvent e)
	{
		Follow(match);
		Sync(match.Tick);
		var (source, special, level) = Identify(match, e.Attacker, e.MoveId);
		var kind = HitSparkStyle.KindOf(e);
		var look = HitSparkStyle.Look(kind, e.Strength, source, Presets.For(special, level));
		Fighter att = match.Fighters[e.Attacker];
		int dir = e.At.X != att.X ? Math.Sign(e.At.X - att.X) : att.Facing;
		int rot = (match.Tick * 47 + e.Attacker * 90) % 360;
		if (_sparks.Count >= Capacity) _sparks.RemoveAt(0);
		_sparks.Add(new HitSpark(match.Tick, e.Attacker, kind, e.At.X, e.At.Y, dir, rot, look));
		return new HitCue(match.Tick, e.Attacker, e.MoveId, source, special, level, e.Strength, kind, e.At.X, e.At.Y);
	}

	/// <summary>The sparks of <paramref name="match"/> showing on its current tick (what the scene draws).</summary>
	public List<HitSparkFrame> Visible(Match match)
	{
		Follow(match);
		return Visible(match.Tick);
	}

	/// <summary>
	/// The sparks showing on <paramref name="tick"/> of the match they came from, oldest first; spent ones are dropped.
	/// Sparks are frames of that match only: if it was reset since, or is not on <paramref name="tick"/> (another
	/// match's tick was asked for), they are gone. Callers holding the match use <see cref="Visible(Match)"/>.
	/// </summary>
	public List<HitSparkFrame> Visible(int tick)
	{
		if (_match != null && (_match.Resets != _resets || _match.Tick != tick)) Clear();
		Sync(tick);
		var shown = new List<HitSparkFrame>(_sparks.Count);
		_sparks.RemoveAll(s => tick - s.SpawnTick >= s.Look.Frames);
		foreach (var s in _sparks) shown.Add(HitSparkStyle.FrameOf(s, tick - s.SpawnTick));
		return shown;
	}

	/// <summary>Another match, or the same one reset since (YOK-198 probes): the old sparks are gone.</summary>
	private void Follow(Match match)
	{
		if (ReferenceEquals(match, _match) && match.Resets == _resets) return;
		Clear();
		_match = match;
		_resets = match.Resets;
	}

	/// <summary>A tick earlier than the last one seen means the match was reset: its sparks are gone.</summary>
	private void Sync(int tick)
	{
		if (tick < _lastTick) _sparks.Clear();
		_lastTick = tick;
	}

	/// <summary>
	/// The move's source (V5), and when it is an equipped special its id and level (V8). The attacker's kit first,
	/// then the other fighter's (an elder's reflect lands the victim's own move). Unknown = ("", null, 1).
	/// </summary>
	public static (string Source, string? Special, int Level) Identify(Match match, int attacker, string moveId)
	{
		for (int k = 0; k < 2; k++)
		{
			Fighter f = match.Fighters[k == 0 ? attacker : 1 - attacker];
			foreach (var s in f.Specials)
				if (s != null && (s.Data.Id == moveId || s.Move.Id == moveId || s.ExMove?.Id == moveId))
					return (s.Data.Source, s.Data.Id, s.Level);
		}
		for (int k = 0; k < 2; k++)
		{
			Fighter f = match.Fighters[k == 0 ? attacker : 1 - attacker];
			foreach (var m in f.Moves)
				if (m.Id == moveId) return (m.Source, null, 1);
		}
		return ("", null, 1);
	}
}
