using System;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>When an effect fires (YOK-242): the special starts, it releases (projectile spawn, else first active frame), or it connects.</summary>
public enum VfxMoment : byte { Cast, Release, Impact }

/// <summary>Where an effect stays: a fixed point on the fight plane, or riding on its owner while the same use of the move plays.</summary>
public enum VfxAnchor : byte { Point, Owner }

/// <summary>
/// YOK-242: the resolved look of one projectile or effect: shape, colours 0xRRGGBB (fill and core), size in percent of
/// the default, the image (relative to game/, null = the default image for the look, then the procedural shader), its
/// flipbook columns and whether the colour tints it.
/// </summary>
public sealed record VfxStyle(VfxLook Look, uint Colour, uint Core, int ScalePct = 100, string? Texture = null, int SheetColumns = 1, bool Tint = true);

/// <summary>Default size and placement of an effect look, in sim units (200 per metre, F4); offsets are along the owner's facing.</summary>
public readonly record struct VfxShape(int W, int H, int OffX, int OffY, VfxAnchor Anchor, int Frames);

/// <summary>
/// YOK-242 defaults for projectile and special-move effects, and how a level preset (V8, A5) changes them. Pure C#.
/// Every size and duration here is a PLACEHOLDER for the procedural art, kept until the VFX textures (YOK-243) and the
/// designer's presets land.
/// </summary>
public static class SpecialVfxStyle
{
	/// <summary>Core colour when no preset names one: rice paper, the ofuda colour (style.md).</summary>
	public const uint DefaultCore = 0xF1E8D4;
	/// <summary>Ticks per flipbook frame of a looping projectile image (15 images a second at 60 ticks, F3).</summary>
	public const int SheetTicks = 4;
	/// <summary>Ticks an effect fades out over at the end of its life.</summary>
	public const int FadeFrames = 6;
	/// <summary>The hand splash on a projectile's release is this much of the look's size, so it never hides the projectile.</summary>
	public const int ReleaseSplashPct = 70;

	/// <summary>
	/// How far the drawn projectile reaches past its data hitbox, in percent of the box (width, height): the box keeps
	/// the leading edge and the vertical centre, and the look's trail runs back behind it.
	/// </summary>
	public static (int WidthPct, int HeightPct) Extent(VfxLook look) => look switch
	{
		VfxLook.InkWave => (200, 150),
		VfxLook.FoxFlame => (220, 130),
		_ => (120, 120),
	};

	/// <summary>Default size, placement and life of an effect look (sim units, ticks; 0 frames = as long as the move plays).</summary>
	public static VfxShape ShapeOf(VfxLook look) => look switch
	{
		VfxLook.Seal => new(150, 150, 60, 200, VfxAnchor.Point, 12), // gone before a 13-frame startup releases
		VfxLook.InkSplash => new(140, 140, 90, 200, VfxAnchor.Point, 16),
		VfxLook.FlameBurst => new(140, 140, 90, 220, VfxAnchor.Point, 16),
		VfxLook.Dust => new(400, 140, 110, 70, VfxAnchor.Point, 26),
		VfxLook.Trail => new(290, 280, -175, 150, VfxAnchor.Owner, 0),
		VfxLook.Ring => new(330, 230, 0, 160, VfxAnchor.Owner, 0),
		VfxLook.Afterimage => new(130, 340, 0, 170, VfxAnchor.Point, 20),
		VfxLook.Grab => new(230, 230, 100, 190, VfxAnchor.Point, 24),
		_ => new(140, 140, 60, 200, VfxAnchor.Point, 16),
	};

	/// <summary>The splash a projectile look makes when it is released or connects.</summary>
	public static VfxLook SplashOf(VfxLook projectile) => projectile == VfxLook.FoxFlame ? VfxLook.FlameBurst : VfxLook.InkSplash;

	/// <summary>
	/// The hook for every special, with or without a preset: the effect its generic move properties call for (A10:
	/// nothing keyed on an id). Teleport → afterimage left behind; a charge or forward travel → speed trail; projectile
	/// invulnerability (a spin) → ring; hits low → ground dust; a command grab → grab whirl; a projectile → its splash.
	/// Anything else gets a talisman seal on the cast and an ink splash on the hit. A preset's look wins over this.
	/// </summary>
	public static VfxLook DefaultLook(VfxMoment moment, MoveData move, VfxLook projectile = VfxLook.Orb)
	{
		bool spin = false;
		foreach (var w in move.Invuln) if ((w.Against & InvulnAgainst.Projectile) != 0) spin = true;
		switch (moment)
		{
			case VfxMoment.Cast:
				if (move.Teleport != null) return VfxLook.Afterimage;
				if (move.Charge != null || move.ForwardTravel > 0) return VfxLook.Trail;
				if (spin) return VfxLook.Ring;
				return VfxLook.Seal;
			case VfxMoment.Release:
				if (move.Projectile != null) return SplashOf(projectile);
				if (move.Low && !move.IsGrab) return VfxLook.Dust;
				return VfxLook.None;
			default:
				if (move.IsGrab) return VfxLook.Grab;
				if (move.Projectile != null) return SplashOf(projectile);
				if (move.Low) return VfxLook.Dust;
				return VfxLook.InkSplash;
		}
	}

	/// <summary>The fill colour of a special: its preset's (V8), else its source's (V5).</summary>
	public static uint BaseColour(LevelPreset? preset, string? source) => preset?.Colour ?? HitSparkStyle.SourceColour(source);

	/// <summary>A projectile's look: the preset's <c>projectile</c> block over the default orb in the special's colour.</summary>
	public static VfxStyle ProjectileStyle(LevelPreset? preset, string? source)
	{
		var b = preset?.Projectile;
		var look = b?.Look is { } l && VfxLooks.IsProjectile(l) ? l : VfxLook.Orb;
		return new VfxStyle(look, b?.Colour ?? BaseColour(preset, source), b?.Core ?? DefaultCore, b?.ScalePct ?? 100, b?.Texture, b?.SheetColumns ?? 1, b?.Tint ?? true);
	}

	/// <summary>The preset's block for a moment.</summary>
	public static VfxPreset? BlockFor(LevelPreset? preset, VfxMoment moment) => moment switch
	{
		VfxMoment.Cast => preset?.Cast,
		VfxMoment.Release => preset?.Release,
		_ => preset?.Impact,
	};

	/// <summary>An effect's look for a moment: the preset's block, else <see cref="DefaultLook"/>. Look None = no effect.</summary>
	public static VfxStyle EffectStyle(VfxMoment moment, MoveData move, LevelPreset? preset, string? source)
	{
		var b = BlockFor(preset, moment);
		var proj = ProjectileStyle(preset, source).Look;
		var look = b?.Look is { } l && !VfxLooks.IsProjectile(l) ? l : DefaultLook(moment, move, proj);
		int scale = b?.ScalePct ?? (moment == VfxMoment.Release && move.Projectile != null ? ReleaseSplashPct : 100);
		return new VfxStyle(look, b?.Colour ?? BaseColour(preset, source), b?.Core ?? DefaultCore, scale, b?.Texture, b?.SheetColumns ?? 1, b?.Tint ?? true);
	}

	/// <summary>Life of an effect in ticks: the preset's frames, else the look's; a move-long look lasts the move; an afterimage holds until the teleport.</summary>
	public static int FramesOf(VfxLook look, MoveData move, VfxPreset? block)
	{
		if (block?.Frames is { } n) return n;
		var shape = ShapeOf(look);
		if (look == VfxLook.Afterimage && move.Teleport is { } tp) return tp.Frame + FadeFrames * 2;
		return shape.Frames > 0 ? shape.Frames : Math.Max(1, move.TotalFrames);
	}

	/// <summary>
	/// The quad a projectile is drawn on, in world centi-units: the data hitbox (<see cref="Projectile.WorldBox"/>)
	/// grown by the look's <see cref="Extent"/> and the preset's scale, keeping the box's leading edge and vertical centre.
	/// </summary>
	public static (int x0, int y0, int x1, int y1) ProjectileQuad((int x0, int y0, int x1, int y1) box, int dir, VfxStyle style)
	{
		var (wp, hp) = Extent(style.Look);
		long w = (long)(box.x1 - box.x0) * wp * style.ScalePct / 10000;
		long h = (long)(box.y1 - box.y0) * hp * style.ScalePct / 10000;
		int cy = (box.y0 + box.y1) / 2;
		int y0 = cy - (int)(h / 2), y1 = y0 + (int)h;
		return dir >= 0 ? (box.x1 - (int)w, y0, box.x1, y1) : (box.x0, y0, box.x0 + (int)w, y1);
	}

	/// <summary>A dark fill gets a rice-paper outline instead of an ink one, so an ink-black move still reads at dusk (V1).</summary>
	public static uint OutlineFor(uint colour)
	{
		int r = (int)((colour >> 16) & 0xFF), g = (int)((colour >> 8) & 0xFF), b = (int)(colour & 0xFF);
		int luma = (299 * r + 587 * g + 114 * b) / 1000;
		return luma < 64 ? DefaultCore : HitSparkStyle.Ink;
	}
}
