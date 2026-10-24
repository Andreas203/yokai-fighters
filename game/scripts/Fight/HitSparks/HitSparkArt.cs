using System;
using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-198: where a spark's image comes from. This is the seam the generated VFX textures drop into, with no code
/// change (AM1, AM5; generation waits on the designer's approval):
/// 1. a level preset's image for the outcome (<c>spark.textures.hit|block|counter</c>, a path relative to game/);
/// 2. else the default image for the outcome, <see cref="DefaultPath"/> = <c>game/vfx/hit_sparks/hit.png</c>,
///    <c>block.png</c>, <c>counter.png</c> (white or greyscale on alpha: the source colour tints it, V5);
/// 3. else a procedural placeholder (<see cref="Procedural"/>), drawn once per shape.
/// A named image that does not exist falls through to the next step (with one warning).
/// </summary>
public static class HitSparkArt
{
	public const string DefaultDir = "res://vfx/hit_sparks";
	public const int ProceduralSize = 64;

	private static readonly Dictionary<string, Texture2D?> Files = new();
	private static readonly Dictionary<HitSparkShape, ImageTexture> Shapes = new();

	public static string DefaultPath(HitSparkKind kind) => $"{DefaultDir}/{kind.ToString().ToLowerInvariant()}.png";

	/// <summary>The resource path the preset's image resolves to (it is relative to game/).</summary>
	public static string PresetPath(string relative) => "res://" + relative;

	/// <summary>
	/// The image for a look and outcome, and whether it is a file (a flipbook's columns apply only to a file).
	/// <paramref name="exists"/> and <paramref name="load"/> are injectable for tests.
	/// </summary>
	public static (Texture2D Texture, bool FromFile) For(HitSparkLook look, HitSparkKind kind,
		Func<string, bool>? exists = null, Func<string, Texture2D?>? load = null)
	{
		exists ??= p => ResourceLoader.Exists(p);
		load ??= p => ResourceLoader.Load<Texture2D>(p);
		if (look.Texture is { } rel && Cached(PresetPath(rel), exists, load, warn: true) is { } named) return (named, true);
		if (Cached(DefaultPath(kind), exists, load, warn: false) is { } def) return (def, true);
		return (Procedural(look.Shape), false);
	}

	/// <summary>Which source <see cref="For"/> would pick, as a path (or "procedural:&lt;shape&gt;"). Pure; for tests and logs.</summary>
	public static string Pick(HitSparkLook look, HitSparkKind kind, Func<string, bool> exists)
	{
		if (look.Texture is { } rel && exists(PresetPath(rel))) return PresetPath(rel);
		if (exists(DefaultPath(kind))) return DefaultPath(kind);
		return "procedural:" + look.Shape.ToString().ToLowerInvariant();
	}

	private static Texture2D? Cached(string path, Func<string, bool> exists, Func<string, Texture2D?> load, bool warn)
	{
		if (Files.TryGetValue(path, out var cached)) return cached;
		Texture2D? tex = exists(path) ? load(path) : null;
		if (tex is null && warn) GD.PushWarning($"HitSparkArt: preset image {path} not found; using the default spark image");
		Files[path] = tex;
		return tex;
	}

	/// <summary>Forget loaded images (tests; a hot-swapped texture).</summary>
	public static void ClearCache() => Files.Clear();

	/// <summary>PLACEHOLDER art: a white-on-alpha shape, the same pixels every run (no RNG).</summary>
	public static ImageTexture Procedural(HitSparkShape shape)
	{
		if (Shapes.TryGetValue(shape, out var t)) return t;
		var img = Image.CreateEmpty(ProceduralSize, ProceduralSize, false, Image.Format.Rgba8);
		float c = (ProceduralSize - 1) / 2f;
		for (int y = 0; y < ProceduralSize; y++)
			for (int x = 0; x < ProceduralSize; x++)
			{
				float dx = (x - c) / c, dy = (y - c) / c;
				float a = Mathf.Clamp(Alpha(shape, Mathf.Sqrt(dx * dx + dy * dy), Mathf.Atan2(dy, dx)), 0f, 1f);
				img.SetPixel(x, y, new Color(1f, 1f, 1f, a));
			}
		t = ImageTexture.CreateFromImage(img);
		Shapes[shape] = t;
		return t;
	}

	/// <summary>Opacity at radius <paramref name="r"/> (0 centre, 1 edge) and angle <paramref name="ang"/>.</summary>
	public static float Alpha(HitSparkShape shape, float r, float ang)
	{
		switch (shape)
		{
			case HitSparkShape.Ring: // a guard ring
				return 1f - Mathf.Abs(r - 0.7f) / 0.12f;
			case HitSparkShape.Star: // four long rays over a bright core: the counterhit
			{
				float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * ang)), 12f);
				return Mathf.Max(1f - r / 0.3f, ray * (1f - r));
			}
			default: // eight-ray ink burst
			{
				float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(4f * ang)), 6f);
				return Mathf.Max(1f - r / 0.4f, ray * (1f - r) * 1.2f);
			}
		}
	}
}
