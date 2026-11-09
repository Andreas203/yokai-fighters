using System;
using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-242: where a projectile's or effect's image comes from. The seam the VFX textures (YOK-243) drop into with no
/// code change (AM1, AM5; generation waits on the designer's approval):
/// 1. the preset block's <c>texture</c> (a path relative to game/);
/// 2. else the default image for the look, <see cref="DefaultPath"/> = <c>game/vfx/projectiles/&lt;look&gt;.png</c> or
///    <c>game/vfx/effects/&lt;look&gt;.png</c> (white or greyscale on alpha: the colour tints it unless <c>tint: false</c>);
/// 3. else the procedural shader (game/shaders/vfx.gdshader), drawn from the look, colours and sim tick.
/// A named image that does not exist falls through to the next step (with one warning).
/// </summary>
public static class VfxArt
{
	public const string ShaderPath = "res://shaders/vfx.gdshader";
	public const string ProjectileDir = "res://vfx/projectiles";
	public const string EffectDir = "res://vfx/effects";

	private static readonly Dictionary<string, Texture2D?> Files = new();

	public static string DefaultPath(VfxLook look) =>
		$"{(VfxLooks.IsProjectile(look) ? ProjectileDir : EffectDir)}/{VfxLooks.Name(look)}.png";

	/// <summary>Which source the style resolves to, as a path, or "procedural:&lt;look&gt;". Pure; for tests and logs.</summary>
	public static string Pick(VfxStyle style, Func<string, bool> exists)
	{
		if (style.Texture is { } rel && exists("res://" + rel)) return "res://" + rel;
		if (exists(DefaultPath(style.Look))) return DefaultPath(style.Look);
		return "procedural:" + VfxLooks.Name(style.Look);
	}

	/// <summary>The image for a style, or null for the procedural shader. <paramref name="exists"/>/<paramref name="load"/> are injectable for tests.</summary>
	public static Texture2D? For(VfxStyle style, Func<string, bool>? exists = null, Func<string, Texture2D?>? load = null)
	{
		exists ??= p => ResourceLoader.Exists(p);
		load ??= p => ResourceLoader.Load<Texture2D>(p);
		if (style.Texture is { } rel && Cached("res://" + rel, exists, load, warn: true) is { } named) return named;
		return Cached(DefaultPath(style.Look), exists, load, warn: false);
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
