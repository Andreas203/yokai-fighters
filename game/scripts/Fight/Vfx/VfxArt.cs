using System;
using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-242: where a projectile's or effect's image comes from. The seam the VFX textures (YOK-243) drop into with no
/// code change (AM1, AM5; generation waits on the designer's approval):
/// 1. the preset block's <c>texture</c> (a path relative to game/);
/// 2. else the default image for the look, <see cref="DefaultPath"/>: the generated texture slot under
///    <c>game/assets/generated/vfx/</c> named in assets/specs/vfx/finish-vfx-textures.md (white on luminance alpha: the
///    preset colour tints it unless <c>tint: false</c>); a look with no slot has no default image;
/// 3. else the procedural shader (game/shaders/vfx.gdshader), drawn from the look, colours and sim tick.
/// A named image that does not exist falls through to the next step (with one warning).
/// </summary>
public static class VfxArt
{
	public const string ShaderPath = "res://shaders/vfx.gdshader";
	public const string ProjectileDir = "res://assets/generated/vfx/projectiles";
	public const string EffectDir = "res://assets/generated/vfx/effects";

	private static readonly Dictionary<string, Texture2D?> Files = new();

	/// <summary>
	/// The YOK-243 texture slot of each look (file name in <see cref="ProjectileDir"/> or <see cref="EffectDir"/>).
	/// Looks not listed (orb, ink splash, flame burst) are procedural only.
	/// </summary>
	public static readonly IReadOnlyDictionary<VfxLook, string> Slots = new Dictionary<VfxLook, string>
	{
		[VfxLook.InkWave] = "proj-ink-wave.png",
		[VfxLook.FoxFlame] = "proj-fox-flame.png",
		[VfxLook.Seal] = "fx-seal-burst.png",
		[VfxLook.Afterimage] = "fx-afterimage.png",
		[VfxLook.Trail] = "fx-speed-trail.png",
		[VfxLook.Dust] = "fx-ground-wave.png",
		[VfxLook.Grab] = "fx-water-swirl.png",
		[VfxLook.Ring] = "fx-spin-ring.png",
	};

	/// <summary>The look's default image path (res://), or null when the look has no texture slot.</summary>
	public static string? DefaultPath(VfxLook look) =>
		Slots.TryGetValue(look, out var file) ? $"{(VfxLooks.IsProjectile(look) ? ProjectileDir : EffectDir)}/{file}" : null;

	/// <summary>Which source the style resolves to, as a path, or "procedural:&lt;look&gt;". Pure; for tests and logs.</summary>
	public static string Pick(VfxStyle style, Func<string, bool> exists)
	{
		if (style.Texture is { } rel && exists("res://" + rel)) return "res://" + rel;
		if (DefaultPath(style.Look) is { } def && exists(def)) return def;
		return "procedural:" + VfxLooks.Name(style.Look);
	}

	/// <summary>The image for a style, or null for the procedural shader. <paramref name="exists"/>/<paramref name="load"/> are injectable for tests.</summary>
	public static Texture2D? For(VfxStyle style, Func<string, bool>? exists = null, Func<string, Texture2D?>? load = null)
	{
		exists ??= p => ResourceLoader.Exists(p);
		load ??= p => ResourceLoader.Load<Texture2D>(p);
		if (style.Texture is { } rel && Cached("res://" + rel, exists, load, warn: true) is { } named) return named;
		return DefaultPath(style.Look) is { } def ? Cached(def, exists, load, warn: false) : null;
	}

	private static Texture2D? Cached(string path, Func<string, bool> exists, Func<string, Texture2D?> load, bool warn)
	{
		if (Files.TryGetValue(path, out var cached)) return cached;
		Texture2D? tex = exists(path) ? load(path) : null;
		if (tex is null && warn) GD.PushWarning($"VfxArt: preset image {path} not found; using the default for the look");
		Files[path] = tex;
		return tex;
	}

	/// <summary>Forget loaded images (tests; a hot-swapped texture).</summary>
	public static void ClearCache() => Files.Clear();
}
