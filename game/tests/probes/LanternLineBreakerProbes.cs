using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-208 test-breaker probes: the lit lantern line against the BambooGroveStage placement rules (LanternMaxZ, clear band, F4, backdrop).</summary>
public static class LanternLineBreakerProbes
{
	static FightScene Load(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		return scene;
	}

	static LanternLine Line(FightScene s) => s.StageRoot.GetNode<LanternLine>("Props/PathLanterns");
	static int Lights(Node n) => n.FindChildren("*", "OmniLight3D", true, false).Count;
	static System.Collections.Generic.List<Node3D> Built(LanternLine l) => l.GetChildren().Cast<Node3D>().ToList();

	// Stage rule (YOK-208 / StageTests "a handful of lantern lights, <= 6"): the stage keeps at most 6 lights.
	// 2 front lanterns + 2 rows x LitPerSide. Shipped LitPerSide 2 = 6; any larger value exceeds the cap with no clamp.
	[Test]
	public static void LightCap_LitPerSideAtItsExportMax_StillSixLightsAtMost(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		line.PerSide = 8; line.LitPerSide = 8;
		int total = Lights(scene.StageRoot);
		Assert.True(total <= 6, $"LitPerSide 8 gives {total} lights in the stage; cap is 6 -> clamp lit count in Placements/Rebuild");
		scene.QueueFree();
	}

	[Test]
	public static void LightCap_LitPerSideThree_ExceedsCap(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		line.LitPerSide = 3;
		int total = Lights(scene.StageRoot);
		Assert.True(total <= 6, $"LitPerSide 3 gives {total} lights; cap is 6");
		scene.QueueFree();
	}

	[Test]
	public static void LightCap_ShippedValues_AtMostSix(Node runner)
	{
		var scene = Load(runner);
		Assert.True(Lights(scene.StageRoot) <= 6, "shipped parameters hold the cap");
		scene.QueueFree();
	}

	[Test]
	public static void Placements_LitPerSideBeyondPerSideOrNegative_LitCountSane()
	{
		var v = new Vector3(0, 0, -16);
		var a = LanternLine.Placements(new(-2, 0, -13), new(2, 0, -13), v, 2, 0.6f, 0.6f, 9);
		Assert.Equal(4, a.Count, "2 per side");
		Assert.Equal(4, a.Count(p => p.Lit), "all lit, none extra");
		var b = LanternLine.Placements(new(-2, 0, -13), new(2, 0, -13), v, 3, 0.6f, 0.6f, -1);
		Assert.Equal(0, b.Count(p => p.Lit), "negative LitPerSide lights none");
		var c = LanternLine.Placements(new(-2, 0, -13), new(2, 0, -13), v, -3, 0.6f, 0.6f, 2);
		Assert.Equal(0, c.Count, "negative PerSide builds nothing");
	}

	[Test]
	public static void Placements_Deterministic_AndNamesUnique()
	{
		var v = new Vector3(0.3f, 0, -16);
		var a = LanternLine.Placements(new(-2, 0, -13), new(2, 0, -13), v, 8, 0.7f, 0.6f, 2);
		var b = LanternLine.Placements(new(-2, 0, -13), new(2, 0, -13), v, 8, 0.7f, 0.6f, 2);
		Assert.True(a.SequenceEqual(b), "same inputs, same placements");
		Assert.Equal(16, a.Select(p => p.Name).Distinct().Count(), "names unique");
	}

	// LanternMaxZ: Recede > 1 (outside the export hint, but settable from code or a hand-edited .tscn) walks lanterns toward the camera.
	[Test]
	public static void StageRule_RecedeAboveOne_LanternsStayDeep(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		line.Recede = 1.5f;
		foreach (var l in Built(line))
		{
			float z = (line.GlobalTransform * l.Position).Z;
			Assert.True(z <= BambooGroveStage.LanternMaxZ, $"{l.Name} at z {z} with Recede 1.5 is nearer than LanternMaxZ {BambooGroveStage.LanternMaxZ}");
		}
		scene.QueueFree();
	}

	[Test]
	public static void StageRule_ShippedLanternsBehindMaxZ_AndAheadOfBackdrop(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		float back = scene.StageRoot.GetNode<MeshInstance3D>("Backdrop").Position.Z;
		foreach (var l in Built(line))
		{
			float z = (line.GlobalTransform * l.Position).Z;
			Assert.True(z <= BambooGroveStage.LanternMaxZ && z > back, $"{l.Name} z {z}");
		}
		scene.QueueFree();
	}

	[Test]
	public static void StageRule_RecedeOneAndZero_Finite(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		foreach (float r in new[] { 0f, 1f })
		{
			line.Recede = r;
			foreach (var l in Built(line))
				Assert.True(float.IsFinite(l.Position.X) && float.IsFinite(l.Position.Z) && l.Scale.X >= 0f, $"Recede {r}: {l.Name} finite");
		}
		scene.QueueFree();
	}

	[Test]
	public static void StageRule_VanishUOutsideRange_LanternsStayDeep(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		foreach (float u in new[] { -1f, 2f })
		{
			line.VanishU = u;
			foreach (var l in Built(line))
			{
				float z = (line.GlobalTransform * l.Position).Z;
				Assert.True(z <= BambooGroveStage.LanternMaxZ, $"VanishU {u}: {l.Name} z {z}");
			}
		}
		scene.QueueFree();
	}

	[Test]
	public static void Backdrop_MissingWrongOrNotAQuad_FallsBackWithoutThrowing(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		line.Backdrop = "../../Nope";
		Assert.Equal(new Vector3(0, 0, -16), line.VanishPoint(), "missing backdrop: fallback point");
		Assert.Equal(2 * line.PerSide, line.GetChildCount(), "still builds");
		line.Backdrop = "../../Floor"; // PlaneMesh, not a QuadMesh
		Assert.Equal(new Vector3(0, 0, -16), line.VanishPoint(), "non-quad mesh: fallback point");
		line.Backdrop = "";
		Assert.Equal(new Vector3(0, 0, -16), line.VanishPoint(), "empty path: fallback");
		scene.QueueFree();
	}

	[Test]
	public static void Backdrop_EdgeOnOrFlatQuad_VanishPointFinite(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		var bd = scene.StageRoot.GetNode<MeshInstance3D>("Backdrop");
		bd.Transform = new Transform3D(new Basis(Vector3.Right, Mathf.Pi / 2f), bd.Position);
		var v = line.VanishPoint();
		Assert.True(float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z), $"flat quad: {v}");
		bd.Transform = new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 2f), bd.Position);
		v = line.VanishPoint();
		Assert.True(float.IsFinite(v.X) && float.IsFinite(v.Z), $"edge-on quad: {v}");
		line.Rebuild();
		foreach (var l in Built(line)) Assert.True(float.IsFinite(l.Position.X) && float.IsFinite(l.Position.Z), $"{l.Name} finite");
		scene.QueueFree();
	}

	[Test]
	public static void Backdrop_VanishPointOnTheFloor(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		var v = line.GlobalTransform * line.VanishPoint();
		Assert.True(Mathf.Abs(v.Y) < 1e-3f, $"vanish point at world y {v.Y}, floor is y 0");
		scene.QueueFree();
	}

	[Test]
	public static void Rebuild_ManyChangesInTree_NoLeakNoDoubleBuild(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		int lightsBefore = Lights(scene.StageRoot), kids = line.GetChildCount();
		for (int i = 0; i < 20; i++) { line.PerSide = 4; line.Recede = 0.65f; line.Rebuild(); }
		Assert.Equal(kids, line.GetChildCount(), "same child count");
		Assert.Equal(lightsBefore, Lights(scene.StageRoot), "same light count");
		Assert.Equal(kids, line.GetChildren().Select(c => c.Name.ToString()).Distinct().Count(), "names unique");
		Assert.True(line.GetChildren().All(c => c.Name.ToString().StartsWith(LanternLine.Prefix) && !c.Name.ToString().Contains('@')), "names exact");
		scene.QueueFree();
	}

	[Test]
	public static void Rebuild_OutOfTreeThenEnter_BuildsOnce(Node runner)
	{
		var line = new LanternLine();
		line.PerSide = 3; line.LitPerSide = 1;
		Assert.Equal(0, line.GetChildCount(), "setters outside the tree build nothing");
		runner.AddChild(line);
		Assert.Equal(6, line.GetChildCount(), "built once on entering");
		Assert.Equal(2, Lights(line), "one lit per side");
		runner.RemoveChild(line);
		runner.AddChild(line);
		Assert.Equal(6, line.GetChildCount(), "re-entering rebuilds, not stacks");
		Assert.True(line.GetChildren().All(c => c.Owner == null), "built children have no Owner (never saved)");
		line.QueueFree();
	}

	[Test]
	public static void Rebuild_ShrinkingPerSide_LeavesNoStaleLanterns(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		line.PerSide = 8; line.PerSide = 1;
		Assert.Equal(2, line.GetChildCount(), "one per side");
		line.PerSide = 0;
		Assert.Equal(0, line.GetChildCount(), "none");
		scene.QueueFree();
	}

	[Test]
	public static void Saving_PackedStage_HoldsNoBuiltLanterns(Node runner)
	{
		var scene = Load(runner);
		var stage = scene.StageRoot;
		var line = Line(scene);
		Assert.True(line.GetChildren().All(c => c.Owner == null), "no built child is owned");
		var copy0 = stage.Owner;
		foreach (var c in stage.GetChildren()) c.Owner = stage; // what the editor does
		var packed = new PackedScene();
		packed.Pack(stage);
		var copy = packed.Instantiate();
		Assert.Equal(0, copy.GetNode("Props/PathLanterns").GetChildCount(), "packed copy has no built lanterns");
		copy.Free();
		scene.QueueFree();
	}

	[Test]
	public static void Toon_RestyleAfterChange_NoStackedOutline_FillStaysFlat(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		line.PerSide = 3; line.Recede = 0.7f; line.PerSide = 4;
		foreach (var mi in line.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>())
		{
			var m = mi.GetActiveMaterial(0) as BaseMaterial3D;
			if (mi.IsInGroup(ToonLook.FlatFillGroup))
				Assert.True(m is { ShadingMode: BaseMaterial3D.ShadingModeEnum.Unshaded, NextPass: null }, $"{mi.Name}: still flat after rebuild");
			else
				Assert.True(m?.NextPass != null && (m.NextPass as BaseMaterial3D)?.NextPass == null, $"{mi.Name}: toon with exactly one outline");
		}
		scene.QueueFree();
	}

	[Test]
	public static void Toon_NoFlatFillMeshOutsideTheStage(Node runner)
	{
		var scene = Load(runner);
		var stageRoot = scene.StageRoot;
		foreach (var mi in scene.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>())
			if (mi.IsInGroup(ToonLook.FlatFillGroup))
				Assert.True(stageRoot.IsAncestorOf(mi), $"{mi.GetPath()}: flat_fill only on stage lanterns, not fighters");
		scene.QueueFree();
	}

	[Test]
	public static void Sim_StateHashIndependentOfLanternLine(Node runner)
	{
		var a = Load(runner);
		var b = Load(runner);
		Line(b).PerSide = 0;
		for (int i = 0; i < 30; i++)
		{
			a.Step(FighterInput.None, FighterInput.None);
			b.Step(FighterInput.None, FighterInput.None);
		}
		Assert.Equal(a.Match.StateHash(), b.Match.StateHash(), "StateHash with and without the lantern line");
		a.QueueFree(); b.QueueFree();
	}
}
