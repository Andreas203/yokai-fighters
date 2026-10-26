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

	// Round 2: the light budget and the depth guard.
	static OmniLight3D ExtraLight(Node parent, string name)
	{
		var l = new OmniLight3D { Name = name };
		parent.AddChild(l);
		l.Owner = parent.Owner ?? parent;
		return l;
	}

	// Cap of 6 (BambooGroveStage.MaxLanternLights): extra lights elsewhere in the stage shrink the line's budget; odd remainders round down.
	[Test]
	public static void Budget_ExtraStageLights_CapHoldsAfterRebuild(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		line.PerSide = 8; line.LitPerSide = 8;
		for (int extra = 1; extra <= 8; extra++)
		{
			ExtraLight(scene.StageRoot, "Extra" + extra);
			line.Rebuild();
			int total = Lights(scene.StageRoot);
			Assert.True(total <= Mathf.Max(BambooGroveStage.MaxLanternLights, extra + 2), $"{extra} extra lights: {total} in the stage; the line must add none past the cap");
			Assert.True(Lights(line) == 2 * line.LightBudgetPerSide() || Lights(line) == 0, $"{extra} extra: line lights {Lights(line)} vs budget {line.LightBudgetPerSide()}");
		}
		Assert.Equal(0, Lights(line), "stage already over the cap: the line lights nothing");
		scene.QueueFree();
	}

	[Test]
	public static void Budget_OddRemainder_NeverExceedsCap(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		line.LitPerSide = 8;
		ExtraLight(scene.StageRoot, "Extra1"); // 3 others -> 3 left -> 1 per row, 2 lit, 5 total
		line.Rebuild();
		Assert.True(Lights(scene.StageRoot) <= 6, $"odd remainder: {Lights(scene.StageRoot)} lights");
		scene.QueueFree();
	}

	// A line added in code has no Owner: LightBudgetPerSide then counts only itself, ignoring the stage's front-lantern lights.
	[Test]
	public static void Budget_LineWithNoOwnerInsideTheStage_StillHoldsStageCap(Node runner)
	{
		var scene = Load(runner);
		var extra = new LanternLine { Name = "CodeLine", PerSide = 4, LitPerSide = 8 };
		scene.StageRoot.GetNode("Props").AddChild(extra);
		Assert.True(Lights(scene.StageRoot) <= BambooGroveStage.MaxLanternLights, $"runtime-added line: {Lights(scene.StageRoot)} lights in the stage (cap {BambooGroveStage.MaxLanternLights}); budget must count the stage, not Owner ?? this");
		scene.QueueFree();
	}

	// Lights added elsewhere after the build: the next Rebuild (and a parameter change) must re-fit the budget.
	[Test]
	public static void Budget_ParameterChangeAfterLightAdded_ReFits(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		ExtraLight(scene.StageRoot, "Extra1");
		ExtraLight(scene.StageRoot, "Extra2");
		line.LitPerSide = 2; // same value as shipped: setter still rebuilds
		Assert.True(Lights(scene.StageRoot) <= BambooGroveStage.MaxLanternLights, $"{Lights(scene.StageRoot)} lights after extra lights and a rebuild");
		scene.QueueFree();
	}

	// Depth guard: skipped lanterns keep the other names and lit flags intact, nothing leaks.
	[Test]
	public static void Guard_NearPointInFrontOfMaxZ_SkipsOnlyThoseAndLeaksNothing(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		line.NearLeft = new Vector3(-1.7f, 0, -5f); // in front of LanternMaxZ; its row recedes into range
		var names = Built(line).Select(l => l.Name.ToString()).ToList();
		Assert.Equal(names.Count, names.Distinct().Count(), "names unique");
		foreach (var l in Built(line))
			Assert.True((line.GlobalTransform * l.Position).Z <= BambooGroveStage.LanternMaxZ, $"{l.Name} survived the guard in front of LanternMaxZ");
		Assert.True(Lights(scene.StageRoot) <= BambooGroveStage.MaxLanternLights, "cap holds");
		Assert.True(Built(line).Count < 2 * line.PerSide, "at least the nearest left lantern is skipped");
		scene.QueueFree();
	}

	// The guard measures the line's parent-relative position only; a moved Props node shifts the real depth.
	[Test]
	public static void Guard_ParentMoved_UsesWorldDepth(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		var props = (Node3D)line.GetParent();
		props.Position = new Vector3(0, 0, 6f); // lanterns at local z -14 now at world z -8
		line.Rebuild();
		foreach (var l in Built(line))
		{
			float z = (line.GlobalTransform * l.Position).Z;
			Assert.True(z <= BambooGroveStage.LanternMaxZ, $"{l.Name} at world z {z}: guard must test world depth (LanternMaxZ {BambooGroveStage.LanternMaxZ})");
		}
		scene.QueueFree();
	}

	[Test]
	public static void Guard_NaNParameters_BuildNothingNonFinite(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		line.Recede = float.NaN;
		foreach (var l in Built(line))
			Assert.True(float.IsFinite(l.Position.Z) && float.IsFinite(l.Scale.X), $"{l.Name} non-finite with Recede NaN");
		scene.QueueFree();
	}

	// Round 3: StageScope fallbacks, two lines, NearScale extremes, Recede clamp.
	[Test]
	public static void Scope_TwoLinesInTheStage_AlternatingRebuilds_HoldCap(Node runner)
	{
		var scene = Load(runner);
		var a = Line(scene);
		var b = new LanternLine { Name = "SecondLine", PerSide = 8, LitPerSide = 8 };
		scene.StageRoot.GetNode("Props").AddChild(b);
		for (int i = 0; i < 3; i++)
		{
			a.LitPerSide = 8; Assert.True(Lights(scene.StageRoot) <= BambooGroveStage.MaxLanternLights, $"round {i} after A: {Lights(scene.StageRoot)}");
			b.LitPerSide = 8; Assert.True(Lights(scene.StageRoot) <= BambooGroveStage.MaxLanternLights, $"round {i} after B: {Lights(scene.StageRoot)}");
		}
		scene.QueueFree();
	}

	[Test]
	public static void Scope_NestedStageInsideAnotherStage_CapHoldsForTheInnerStage(Node runner)
	{
		var outer = new FightStage { Name = "Outer" };
		runner.AddChild(outer);
		var inner = GD.Load<PackedScene>("res://scenes/stage/bamboo_grove.tscn").Instantiate<FightStage>();
		outer.AddChild(inner);
		var line = inner.GetNode<LanternLine>("Props/PathLanterns");
		line.PerSide = 8; line.LitPerSide = 8;
		Assert.True(Lights(inner) <= BambooGroveStage.MaxLanternLights, $"inner stage has {Lights(inner)} lights");
		Assert.True(ReferenceEquals(line.StageScope(), inner), "nearest FightStage is the scope");
		outer.QueueFree();
	}

	[Test]
	public static void Scope_LineOnItsOwnOutsideAnyStage_BuildsAndHoldsItsOwnBudget(Node runner)
	{
		var line = new LanternLine { PerSide = 8, LitPerSide = 8 };
		runner.AddChild(line);
		Assert.True(Lights(line) <= BambooGroveStage.MaxLanternLights, $"stand-alone line lights {Lights(line)}");
		line.QueueFree();
	}

	[Test]
	public static void Scope_StageScopeOutOfTree_DoesNotThrow()
	{
		var holder = new Node3D();
		var line = new LanternLine();
		holder.AddChild(line);
		Assert.True(ReferenceEquals(line.StageScope(), holder), "out of tree: topmost ancestor");
		Assert.True(line.LightBudgetPerSide() >= 0, "budget non-negative");
		holder.Free();
	}

	[Test]
	public static void NearScale_TinyAndHuge_BasisInvertibleAndFinite(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		foreach (float s in new[] { 1e-12f, 1e-20f, 1e20f })
		{
			line.NearScale = s;
			foreach (var l in Built(line))
				Assert.True(float.IsFinite(l.Scale.X) && l.Scale.X > 0f && Mathf.Abs(l.Basis.Determinant()) > 0f, $"NearScale {s}: {l.Name} scale {l.Scale.X} det {l.Basis.Determinant()} (singular basis)");
		}
		scene.QueueFree();
	}

	[Test]
	public static void Recede_OutOfRange_EqualsClampedPlacements()
	{
		var v = new Vector3(0, 0, -16);
		var nl = new Vector3(-2, 0, -13); var nr = new Vector3(2, 0, -13);
		Assert.True(LanternLine.Placements(nl, nr, v, 4, 5f, 0.6f, 2).SequenceEqual(LanternLine.Placements(nl, nr, v, 4, LanternLine.MaxRecede, 0.6f, 2)), "Recede 5 == MaxRecede");
		Assert.True(LanternLine.Placements(nl, nr, v, 4, -3f, 0.6f, 2).SequenceEqual(LanternLine.Placements(nl, nr, v, 4, LanternLine.MinRecede, 0.6f, 2)), "Recede -3 == MinRecede");
	}

	[Test]
	public static void Guard_SkippedLanterns_LitCountStillWithinBudgetAndNamesKeepIndex(Node runner)
	{
		var scene = Load(runner);
		var line = Line(scene);
		line.NearLeft = new Vector3(-1.7f, 0, -4f);
		line.NearRight = new Vector3(2.3f, 0, -4f);
		foreach (var l in Built(line))
			Assert.True(l.Name.ToString().StartsWith(LanternLine.Prefix) && !l.Name.ToString().Contains('@'), $"{l.Name} name");
		Assert.True(Lights(scene.StageRoot) <= BambooGroveStage.MaxLanternLights, "cap");
		line.NearLeft = new Vector3(-1.7f, 0, -30f); // behind the backdrop: world z beyond it
		float back = scene.StageRoot.GetNode<MeshInstance3D>("Backdrop").Position.Z;
		foreach (var l in Built(line).Where(n => n.Name.ToString().StartsWith(LanternLine.Prefix + "L")))
			Assert.True((line.GlobalTransform * l.Position).Z > back, $"{l.Name} built behind the backdrop plane z {back}");
		scene.QueueFree();
	}
}
