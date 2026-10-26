using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>YOK-208 test-breaker probes round 4: the NearScale clamp and the backdrop guard in LanternLine.Rebuild.</summary>
public static class LanternBackdropGuardBreakerProbes
{
	static FightScene Load(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		return scene;
	}

	static LanternLine Line(FightScene s) => s.StageRoot.GetNode<LanternLine>("Props/PathLanterns");
	static MeshInstance3D Backdrop(FightScene s) => s.StageRoot.GetNode<MeshInstance3D>("Backdrop");
	static List<Node3D> Built(LanternLine l) => l.GetChildren().Cast<Node3D>().ToList();
	static IEnumerable<MeshInstance3D> Meshes(Node n) => n.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>();
	static IEnumerable<Vector3> Verts(MeshInstance3D mi)
	{
		for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
			foreach (Vector3 v in (Vector3[])mi.Mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex]) yield return mi.GlobalTransform * v;
	}

	/// <summary>Stage rule (StageTests "poke through the backdrop"): no built lantern vertex is on or behind the backdrop plane (camera side = +Z).</summary>
	static string BehindBackdrop(FightScene s)
	{
		var bd = Backdrop(s);
		Transform3D q = bd.GlobalTransform;
		Vector3 n = q.Basis.Column0.Cross(q.Basis.Column1);
		if (n.LengthSquared() < 1e-12f) return "";
		n = n.Normalized();
		if (n.Z < 0) n = -n;
		string bad = "";
		foreach (var l in Built(Line(s)))
		{
			float min = Meshes(l).SelectMany(Verts).Min(v => n.Dot(v - q.Origin));
			if (!(min > 0f)) bad += $"{l.Name} reaches {min:0.000} m behind the plane; ";
		}
		return bad;
	}

	static string Describe(FightScene s) => string.Join(",", Built(Line(s)).Select(l => l.Name.ToString()));

	// NearScale clamp [0.05, 5] (LanternLine.MinNearScale / MaxNearScale).
	[Test]
	public static void NearScaleClamp_Boundaries()
	{
		var v = new Vector3(0, 0, -16);
		float S(float ns) => LanternLine.Placements(new(-2, 0, -13), new(2, 0, -13), v, 1, 0.6f, ns, 1)[0].Scale;
		Assert.Equal(0.05f, S(0.05f), "at the minimum");
		Assert.Equal(0.05f, S(0.049f), "just below the minimum clamps up");
		Assert.Equal(0.05f, S(1e-20f), "tiny clamps up");
		Assert.Equal(0.051f, S(0.051f), "just above is kept");
		Assert.Equal(5f, S(5f), "at the maximum");
		Assert.Equal(5f, S(5.001f), "just above the maximum clamps down");
		Assert.Equal(5f, S(1e30f), "huge clamps down");
		Assert.Equal(0, LanternLine.Placements(new(-2, 0, -13), new(2, 0, -13), v, 1, 0.6f, 0f, 1).Count, "zero builds nothing");
		Assert.Equal(0, LanternLine.Placements(new(-2, 0, -13), new(2, 0, -13), v, 1, 0.6f, -1f, 1).Count, "negative builds nothing");
		Assert.Equal(0, LanternLine.Placements(new(-2, 0, -13), new(2, 0, -13), v, 1, 0.6f, float.PositiveInfinity, 1).Count, "infinity builds nothing");
		var deep = LanternLine.Placements(new(-2, 0, -13), new(2, 0, -13), v, 8, 0.2f, 0.05f, 2);
		Assert.True(deep.All(p => p.Scale > 0f), "deep lanterns of a minimum-scale row keep a positive scale (singular basis)");
	}

	// Stage rule: lanterns at the maximum NearScale still keep the front limit and stay in front of the backdrop.
	[Test]
	public static void NearScaleMax_LanternsKeepFrontLimitAndBackdrop(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		line.NearScale = 5f;
		string bad = "";
		foreach (var l in Built(line))
		{
			Aabb own = l.GetChildren().Select(BambooGroveStage.LocalBounds).Aggregate((a, b) => a.Merge(b));
			var p = (line.GlobalTransform * l.Transform).Origin;
			float r = Mathf.Max(own.Size.X, own.Size.Z) * 0.5f * l.Scale.Y;
			if (p.Z + r > BambooGroveStage.FrontLimit(p.X, true) + 1e-3f) bad += $"{l.Name} front {p.Z + r:0.00} past limit; ";
			if (Mathf.Abs(p.X) < BambooGroveStage.ClearBandHalfX && p.Z + r > BambooGroveStage.ClearBandBackZ + 1e-3f) bad += $"{l.Name} in clear band; ";
		}
		bad += BehindBackdrop(scene);
		scene.QueueFree();
		Assert.True(bad == "", "NearScale 5: " + bad);
	}

	[Test]
	public static void Backdrop_Mirrored_NoLanternBehindIt(Node runner)
	{
		foreach (var sc in new[] { new Vector3(-1, 1, 1), new Vector3(1, 1, -1), new Vector3(-1, 1, -1), new Vector3(-1, -1, 1) })
		{
			var scene = Load(runner);
			var bd = Backdrop(scene);
			bd.Transform = new Transform3D(bd.Transform.Basis.Scaled(sc), bd.Position);
			Line(scene).Rebuild();
			string bad = BehindBackdrop(scene);
			scene.QueueFree();
			Assert.True(bad == "", $"backdrop scale {sc}: {bad}");
		}
	}

	[Test]
	public static void Backdrop_YawedPast90_NoLanternBehindIt(Node runner)
	{
		foreach (float deg in new[] { 20f, 60f, 100f, 135f, 179f, 180f, 300f })
		{
			var scene = Load(runner);
			var bd = Backdrop(scene);
			bd.Transform = new Transform3D(bd.Transform.Basis.Rotated(Vector3.Up, Mathf.DegToRad(deg)), bd.Position);
			Line(scene).Rebuild();
			string bad = BehindBackdrop(scene);
			scene.QueueFree();
			Assert.True(bad == "", $"backdrop yaw {deg}: {bad}");
		}
	}

	// A leaning backdrop: the guard measures the horizontal footprint only, a lantern's top can still cross a plane that leans toward the camera.
	[Test]
	public static void Backdrop_LeaningTowardCamera_NoLanternVertexBehindIt(Node runner)
	{
		foreach (float deg in new[] { 10f, 25f, -25f })
		{
			var scene = Load(runner);
			var bd = Backdrop(scene);
			bd.Transform = new Transform3D(bd.Transform.Basis.Rotated(Vector3.Right, Mathf.DegToRad(deg)), bd.Position);
			Line(scene).Rebuild();
			string bad = BehindBackdrop(scene);
			scene.QueueFree();
			Assert.True(bad == "", $"backdrop pitch {deg}: {bad}");
		}
	}

	[Test]
	public static void Backdrop_DegenerateScale_NoThrowNothingBehind(Node runner)
	{
		foreach (var sc in new[] { new Vector3(0, 1, 1), new Vector3(1, 0, 1), new Vector3(1e-9f, 1e-9f, 1), new Vector3(1, 1, 0), new Vector3(float.NaN, 1, 1) })
		{
			var scene = Load(runner);
			var bd = Backdrop(scene);
			bd.Transform = new Transform3D(bd.Transform.Basis.Scaled(sc), bd.Position);
			Line(scene).Rebuild();
			foreach (var l in Built(Line(scene)))
				Assert.True(float.IsFinite(l.Position.X) && float.IsFinite(l.Position.Z) && float.IsFinite(l.Scale.X), $"scale {sc}: {l.Name} finite");
			string bad = BehindBackdrop(scene);
			scene.QueueFree();
			Assert.True(bad == "", $"backdrop scale {sc}: {bad}");
		}
	}

	// Backdrop pulled forward: lanterns it would swallow are skipped, others keep their names and the cap holds.
	[Test]
	public static void Backdrop_PulledForward_SkipsKeepNamesAndLitFlags(Node runner)
	{
		var scene = Load(runner);
		var bd = Backdrop(scene);
		var line = Line(scene);
		var full = Built(line).Select(l => l.Name.ToString()).ToList();
		foreach (float z in new[] { -14f, -13.9f, -13.5f, -12.5f, -11f, -9f, 5f })
		{
			bd.Position = new Vector3(bd.Position.X, bd.Position.Y, z);
			line.Rebuild();
			var names = Built(line).Select(l => l.Name.ToString()).ToList();
			Assert.True(names.All(n => full.Contains(n)), $"backdrop z {z}: built names {string.Join(",", names)} are all from the full set (no @ suffix, no rename)");
			Assert.Equal(names.Count, names.Distinct().Count(), $"z {z}: names unique");
			Assert.True(BehindBackdrop(scene) == "", $"backdrop z {z}: {BehindBackdrop(scene)}");
			int lights = scene.StageRoot.FindChildren("*", "OmniLight3D", true, false).Count;
			Assert.True(lights <= BambooGroveStage.MaxLanternLights, $"z {z}: {lights} lights");
			// lit flag follows the lantern's index in its row, not its position among the survivors
			foreach (var l in Built(line))
			{
				int idx = int.Parse(l.Name.ToString()[^1..]);
				bool lit = l.GetNodeOrNull("Pivot/Light") != null;
				Assert.True(lit == (idx < line.LitPerSide), $"z {z}: {l.Name} lit={lit}");
			}
		}
		scene.QueueFree();
	}

	// The line itself mirrored/scaled/turned: footprint radius uses the unsigned stretch.
	[Test]
	public static void LineTransformed_NoLanternBehindBackdropOrPastMaxZ(Node runner)
	{
		var cases = new (string Name, Basis B, Vector3 Off)[]
		{
			("mirror x", Basis.FromScale(new Vector3(-1, 1, 1)), Vector3.Zero),
			("mirror z", Basis.FromScale(new Vector3(1, 1, -1)), new Vector3(0, 0, -30)),
			("scale 1.5", Basis.FromScale(new Vector3(1.5f, 1.5f, 1.5f)), new Vector3(0, 0, 5)),
			("scale z 0.3", Basis.FromScale(new Vector3(1, 1, 0.3f)), new Vector3(0, 0, -8)),
			("yaw 180", new Basis(Vector3.Up, Mathf.Pi), new Vector3(0, 0, -30)),
		};
		foreach (var (name, b, off) in cases)
		{
			var scene = Load(runner);
			var line = Line(scene);
			var props = (Node3D)line.GetParent();
			props.Transform = new Transform3D(b, off);
			line.Rebuild();
			string bad = BehindBackdrop(scene);
			foreach (var l in Built(line))
				if ((line.GlobalTransform * l.Position).Z > BambooGroveStage.LanternMaxZ) bad += $"{l.Name} past LanternMaxZ; ";
			scene.QueueFree();
			Assert.True(bad == "", $"Props {name}: {bad}");
		}
	}

	// No backdrop node: the fallback plane (0,0,-16) normal +Z still stops lanterns, with the line in a moved parent.
	[Test]
	public static void NoBackdrop_FallbackPlaneStillGuards(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		line.Backdrop = "../../Nope";
		line.NearLeft = new Vector3(-1.7f, 0, -15.9f);
		line.NearScale = 2f;
		var names = Built(line).Select(l => l.Name.ToString()).ToList();
		scene.QueueFree();
		Assert.True(!names.Contains("PathLanternL0"), $"a scale-2 lantern at z -15.9 touches the fallback plane z -16 yet was built: {string.Join(",", names)}");
	}
}
