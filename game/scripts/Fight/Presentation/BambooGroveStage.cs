using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-39: the bamboo grove at dusk (V9) is the scene <c>res://scenes/stage/bamboo_grove.tscn</c>, edited in Godot
/// (spec <c>assets/specs/bamboo-grove-dusk.md</c>). Layers back to front: cropped backdrop quad, ten bamboo clusters,
/// two stone lanterns, the tiled ground strip; dusk = warm low key light + cool fill + indigo depth fog. The props are
/// placed by hand (the layout baked from the old seeded builder); this class holds the placement rules StageTests
/// checks against, and the asset check for the placeholder fallback.
/// </summary>
public static class BambooGroveStage
{
	public const string Kind = "bamboo-grove";
	public const string DefaultDir = "res://assets/generated/stage/bamboo-grove/";

	/// <summary>Art inside the generated PNGs' grey letterbox border (asset-smith acceptance, in pixels); the scene crops by UV.</summary>
	public static readonly Rect2 BackdropArt = new(110, 75, 1156, 618), BackdropImage = new(0, 0, 1376, 768);
	public static readonly Rect2 GroundArt = new(103, 173, 1137, 422), GroundImage = new(0, 0, 1344, 768);

	/// <summary>No prop's front (centre z + footprint radius) comes nearer than this: everything stays behind the Z = 0 plane (F4).</summary>
	public const float PropMaxZ = -2f;
	public const float PropOutlineMetres = 0.02f;

	/// <summary>
	/// YOK-39 clear band: behind the fighting area (|x| &lt; <see cref="ClearBandHalfX"/>, the stage plus a margin) no
	/// prop's footprint comes nearer than <see cref="ClearBandBackZ"/>, so nothing sits right behind a fighter and reads
	/// as attached to them. Lanterns (small, fighter-sized) stay at or behind <see cref="LanternMaxZ"/> everywhere.
	/// Lanterns sit by parallax so that in the standard framings each shows in a gap between the fighters: at z -12 a
	/// lantern appears at about (x - camera x) x 0.5.
	/// </summary>
	public const float ClearBandHalfX = 10.5f, ClearBandBackZ = -8f, LanternMaxZ = -10f;

	/// <summary>The grove's backdrop and ground textures exist under <paramref name="dir"/> (else the fight uses the placeholder).</summary>
	public static bool AssetsPresent(string dir) =>
		ResourceLoader.Exists(dir + "backdrop.png") && ResourceLoader.Exists(dir + "ground-strip.png");

	/// <summary>Limit for a prop's front at <paramref name="x"/>: the gameplay plane, the lantern depth, the clear band.</summary>
	public static float FrontLimit(float x, bool lantern)
	{
		float limit = lantern ? LanternMaxZ : PropMaxZ;
		return Mathf.Abs(x) < ClearBandHalfX ? Mathf.Min(limit, ClearBandBackZ) : limit;
	}

	/// <summary>Bounds of every mesh under <paramref name="n"/> in its parent's space (works before entering the tree).</summary>
	public static Aabb LocalBounds(Node n)
	{
		Aabb? acc = null;
		Walk(n, Transform3D.Identity, ref acc);
		return acc ?? new Aabb();
	}

	private static void Walk(Node n, Transform3D xf, ref Aabb? acc)
	{
		if (n is Node3D n3) xf = xf * n3.Transform;
		if (n is MeshInstance3D mi && mi.Mesh != null)
		{
			Aabb b = xf * mi.Mesh.GetAabb();
			acc = acc is null ? b : acc.Value.Merge(b);
		}
		foreach (Node ch in n.GetChildren()) Walk(ch, xf, ref acc);
	}
}
