using System;
using System.Collections.Generic;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-242: the procedural looks a level preset can name (<c>projectile.look</c>, <c>cast.look</c>, <c>release.look</c>,
/// <c>impact.look</c>). The first three dress a projectile in flight; the rest are one-shot or move-long effects.
/// The number is the shader's <c>shape</c> uniform (game/shaders/vfx.gdshader); <see cref="None"/> turns an effect off.
/// No look is keyed on a move id (A5, A10): data names the look, or <see cref="SpecialVfxStyle.DefaultLook"/> picks one
/// from the move's generic properties.
/// </summary>
public enum VfxLook : byte
{
	Orb = 0, InkWave = 1, FoxFlame = 2,
	Seal = 3, InkSplash = 4, FlameBurst = 5, Dust = 6, Trail = 7, Ring = 8, Afterimage = 9, Grab = 10,
	None = 255,
}

/// <summary>YOK-242: the data names of <see cref="VfxLook"/> (snake_case, as in data/schema/level-preset.schema.json).</summary>
public static class VfxLooks
{
	static readonly Dictionary<string, VfxLook> ByName = new(StringComparer.Ordinal)
	{
		["orb"] = VfxLook.Orb, ["ink_wave"] = VfxLook.InkWave, ["fox_flame"] = VfxLook.FoxFlame,
		["seal"] = VfxLook.Seal, ["ink_splash"] = VfxLook.InkSplash, ["flame_burst"] = VfxLook.FlameBurst,
		["dust"] = VfxLook.Dust, ["trail"] = VfxLook.Trail, ["ring"] = VfxLook.Ring,
		["afterimage"] = VfxLook.Afterimage, ["grab"] = VfxLook.Grab, ["none"] = VfxLook.None,
	};

	/// <summary>Every name, in the schema's order.</summary>
	public static IEnumerable<string> Names => ByName.Keys;

	public static bool TryParse(string name, out VfxLook look) => ByName.TryGetValue(name, out look);

	public static string Name(VfxLook look)
	{
		foreach (var kv in ByName) if (kv.Value == look) return kv.Key;
		return look.ToString();
	}

	/// <summary>A look that dresses a projectile in flight (the <c>projectile</c> block); the others are effects.</summary>
	public static bool IsProjectile(VfxLook look) => look is VfxLook.Orb or VfxLook.InkWave or VfxLook.FoxFlame;
}
