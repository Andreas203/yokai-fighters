using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-150: the placement rules every fight stage keeps (factored out of <see cref="BambooGroveStage"/>, YOK-39/208, whose
/// constants alias these). Props are placed by hand in each stage scene; <c>StageTests</c> checks them against these rules.
/// </summary>
public static class StageRules
{
	/// <summary>No prop's front (centre z + footprint radius) comes nearer than this: everything stays behind the Z = 0 plane (F4).</summary>
	public const float PropMaxZ = -2f;
	/// <summary>Ink outline thickness of stage props, in world metres (fighters use <see cref="ToonLook.DefaultOutlineMetres"/>).</summary>
	public const float PropOutlineMetres = 0.02f;

	/// <summary>
	/// The clear band: behind the fighting area (|x| &lt; <see cref="ClearBandHalfX"/>, the stage plus a margin) no prop's
	/// footprint comes nearer than <see cref="ClearBandBackZ"/>, so nothing sits right behind a fighter and reads as attached
	/// to them. Lanterns (small, fighter-sized) stay at or behind <see cref="LanternMaxZ"/> everywhere.
	/// </summary>
	public const float ClearBandHalfX = 10.5f, ClearBandBackZ = -8f, LanternMaxZ = -10f;

	/// <summary>A stage's lantern light budget, all OmniLight3D together (shadowless, a handful at most).</summary>
	public const int MaxLanternLights = 6;

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

	/// <summary>Every file in <paramref name="files"/> exists under <paramref name="dir"/> (a stage's asset check; else the fight falls back).</summary>
	public static bool FilesPresent(string dir, params string[] files)
	{
		foreach (string f in files) if (!ResourceLoader.Exists(dir + f)) return false;
		return true;
	}
}
