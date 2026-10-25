using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-208 test-breaker probes: the near-left lantern (Props/Lantern0) against bamboo and the path row through the real fight camera.</summary>
public static class LanternFrontBreakerProbes
{
	static FightScene Load(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		return scene;
	}

	static FighterInput In(InputBits b) => new(b);
	static void Walk(FightScene s, InputBits p1, InputBits p2, int n) { for (int t = 0; t < n; t++) s.Step(In(p1), In(p2)); }
	static void Corner(FightScene s, int side)
	{
		var toward = side < 0 ? InputBits.Left : InputBits.Right;
		for (int t = 0, lastX = int.MinValue; t < 1500 && s.Match.Fighters[side < 0 ? 0 : 1].X != lastX; t++)
		{
			lastX = s.Match.Fighters[side < 0 ? 0 : 1].X;
			s.Step(In(toward), In(toward));
		}
		for (int t = 0; t < 30; t++) s.Step(In(toward), In(toward));
	}

	static readonly (string Name, System.Action<FightScene> Pose, bool Settle)[] Framings =
	{
		("close", s => Walk(s, InputBits.Right, InputBits.Left, 40), true),
		("mid", s => Walk(s, InputBits.Left, InputBits.Right, 11), true),
		("max-separation", s => Walk(s, InputBits.Left, InputBits.Right, 400), true),
		("left-corner", s => Corner(s, -1), true),
		("right-corner", s => Corner(s, 1), true),
		("jump-in", s => { Walk(s, InputBits.Right, InputBits.None, 14); Walk(s, InputBits.None, InputBits.None, 60); s.Step(In(InputBits.Up | InputBits.Right), FighterInput.None); Walk(s, InputBits.None, InputBits.None, 22); }, false),
	};

	static IEnumerable<MeshInstance3D> Meshes(Node n) => n.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>();

	static IEnumerable<Vector3> Verts(MeshInstance3D mi)
	{
		for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
			foreach (Vector3 v in (Vector3[])mi.Mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex]) yield return mi.GlobalTransform * v;
	}

	static IEnumerable<(Vector3 A, Vector3 B, Vector3 C)> Tris(MeshInstance3D mi)
	{
		for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
		{
			var arr = mi.Mesh.SurfaceGetArrays(s);
			var v = (Vector3[])arr[(int)Mesh.ArrayType.Vertex];
			int[]? idx = arr[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil ? null : (int[])arr[(int)Mesh.ArrayType.Index];
			int n = idx?.Length ?? v.Length;
			for (int i = 0; i + 2 < n; i += 3)
			{
				Vector3 a = v[idx?[i] ?? i], b = v[idx?[i + 1] ?? i + 1], c = v[idx?[i + 2] ?? i + 2];
				yield return (mi.GlobalTransform * a, mi.GlobalTransform * b, mi.GlobalTransform * c);
			}
		}
	}

	/// <summary>Moller-Trumbore on the segment from to (two-sided); true if it crosses the triangle before 'to'.</summary>
	static bool Hit(Vector3 from, Vector3 to, Vector3 a, Vector3 b, Vector3 c)
	{
		Vector3 d = to - from, e1 = b - a, e2 = c - a, p = d.Cross(e2);
		float det = e1.Dot(p);
		if (Mathf.Abs(det) < 1e-9f) return false;
		float inv = 1f / det; Vector3 t = from - a;
		float u = t.Dot(p) * inv; if (u < 0 || u > 1) return false;
		Vector3 q = t.Cross(e1);
		float v = d.Dot(q) * inv; if (v < 0 || u + v > 1) return false;
		float s = e2.Dot(q) * inv;
		return s > 0.001f && s < 0.98f;
	}

	/// <summary>Fraction of the target's vertices whose sight line from the camera crosses a triangle of the occluders.</summary>
	static float Covered(Vector3 cam, IEnumerable<MeshInstance3D> target, List<MeshInstance3D> occluders)
	{
		var tris = occluders.SelectMany(Tris).ToList();
		var pts = target.SelectMany(Verts).ToList();
		int hidden = 0;
		foreach (var p in pts)
			if (tris.Any(t => Hit(cam, p, t.A, t.B, t.C))) hidden++;
		return pts.Count == 0 ? 0 : hidden / (float)pts.Count;
	}

	// YOK-208 designer feedback 2026-10-25: the near-left lantern is not hidden by bamboo. At every real fight framing,
	// no bamboo triangle may sit on the sight line to the lantern's mesh.
	[Test]
	public static void Lantern0_NotCoveredByAnyBambooAtAnyFraming(Node runner)
	{
		foreach (var (name, pose, settle) in Framings)
		{
			var scene = Load(runner);
			pose(scene);
			if (settle) Walk(scene, InputBits.None, InputBits.None, 90);
			Vector3 cam = scene.Camera.GlobalPosition;
			var props = scene.StageRoot.GetNode("Props");
			var bamboo = props.GetChildren().Where(c => c.Name.ToString().StartsWith("Bamboo")).SelectMany(Meshes).ToList();
			float f = Covered(cam, Meshes(props.GetNode("Lantern0")), bamboo);
			scene.QueueFree();
			Assert.True(f == 0f, $"{name}: {f:P0} of Lantern0's vertices are behind bamboo from the camera at {cam}");
		}
	}

	// Same designer feedback: off-centre cameras across the stage width (the camera x ranges over the stage).
	[Test]
	public static void Lantern0_NotCoveredByBamboo3AcrossCameraXSweep(Node runner)
	{
		var scene = Load(runner);
		Walk(scene, InputBits.Left, InputBits.Right, 400);
		Walk(scene, InputBits.None, InputBits.None, 90);
		Vector3 cam0 = scene.Camera.GlobalPosition;
		var props = scene.StageRoot.GetNode("Props");
		var b3 = Meshes(props.GetNode("Bamboo3")).ToList();
		var lantern = Meshes(props.GetNode("Lantern0")).ToList();
		string worst = ""; float worstF = 0;
		for (float x = -12f; x <= 12f; x += 1f)
			foreach (float zOff in new[] { 0f, -3f, -6f })
			{
				float f = Covered(new Vector3(x, cam0.Y, cam0.Z + zOff), lantern, b3);
				if (f > worstF) { worstF = f; worst = $"cam ({x}, {cam0.Y:0.0}, {cam0.Z + zOff:0.0})"; }
			}
		scene.QueueFree();
		Assert.True(worstF == 0f, $"Bamboo3 covers {worstF:P0} of Lantern0 from {worst}");
	}

	// BambooGroveStage.LanternMaxZ (-10): "lanterns stay at or behind" it. Checked on the full mesh extent of every lantern, not the origin.
	[Test]
	public static void EveryLanternFullExtentAtOrBehindLanternMaxZ(Node runner)
	{
		var scene = Load(runner);
		var props = scene.StageRoot.GetNode("Props");
		var all = new List<Node3D>();
		foreach (var n in props.GetChildren().Cast<Node3D>())
		{
			if (n is LanternLine l) all.AddRange(l.GetChildren().Cast<Node3D>());
			else if (n.Name.ToString().StartsWith("Lantern")) all.Add(n);
		}
		string bad = "";
		foreach (var n in all)
		{
			Aabb b = Meshes(n).Select(mi => mi.GlobalTransform * mi.Mesh.GetAabb()).Aggregate((x, y) => x.Merge(y));
			if (n.GlobalPosition.Z > BambooGroveStage.LanternMaxZ) bad += $"{n.Name} origin z {n.GlobalPosition.Z:0.00}; ";
			else if (b.End.Z > BambooGroveStage.LanternMaxZ + 1e-3f) bad += $"{n.Name} front z {b.End.Z:0.00}; ";
		}
		scene.QueueFree();
		Assert.True(bad == "", $"lanterns past LanternMaxZ {BambooGroveStage.LanternMaxZ}: {bad}");
	}

	// "A path of lanterns": Lantern0 must not hide the head of the left row. Rule silent on a limit; 25 % chosen. See Designer attention.
	[Test]
	public static void Lantern0_DoesNotCoverMuchOfPathLanternL0(Node runner)
	{
		string msg = ""; float worst = 0;
		foreach (var (name, pose, settle) in Framings)
		{
			var scene = Load(runner);
			pose(scene);
			if (settle) Walk(scene, InputBits.None, InputBits.None, 90);
			var props = scene.StageRoot.GetNode("Props");
			float f = Covered(scene.Camera.GlobalPosition, Meshes(props.GetNode("PathLanterns/" + LanternLine.Prefix + "L0")), Meshes(props.GetNode("Lantern0")).ToList());
			if (f > worst) { worst = f; msg = name; }
			scene.QueueFree();
		}
		Assert.True(worst <= 0.25f, $"Lantern0 covers {worst:P0} of PathLanternL0 at framing {msg}");
	}
}
