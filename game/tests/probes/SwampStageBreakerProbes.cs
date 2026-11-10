using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-150 breaker probes for the night-swamp riverbank (8ba931e6): edge coverage, the water join, deleted assets, rebuilds, determinism, the other stages' lanterns.</summary>
public static class SwampStageBreakerProbes
{
	const string Riverbank = StageCatalog.RiverbankScene;

	static FightScene Load(Node runner, string stage)
	{
		string? was = FightScene.ForcedStage;
		FightScene.ForcedStage = stage;
		try
		{
			var s = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			s.ExternalDrive = true;
			runner.AddChild(s);
			return s;
		}
		finally { FightScene.ForcedStage = was; }
	}

	static ShaderMaterial Far(MeshInstance3D b) => (ShaderMaterial)b.MaterialOverride;

	/// <summary>Where a screen ray lands: "ok" (the painting within its art), a ground plane's name, or what is wrong.</summary>
	static string Land(FightScene scene, DynamicCamera cam, float nx, float ny)
	{
		var stage = scene.StageRoot;
		var backdrop = stage.GetNode<MeshInstance3D>("Backdrop");
		var size = ((QuadMesh)backdrop.Mesh).Size;
		var mat = Far(backdrop);
		var tex = mat.GetShaderParameter("tex").As<Texture2D>();
		Vector4 art = mat.GetShaderParameter("art_rect").AsVector4();
		var (o, d) = cam.ScreenRay(nx, ny);
		var planes = new[] { "Floor", "Water" }.Select(g => stage.GetNode<MeshInstance3D>(g)).ToList();
		float tb = (backdrop.Position.Z - o.Z) / d.Z;
		float? best = null; string what = "";
		if (tb > 0) { best = tb; what = "backdrop"; }
		foreach (var p in planes)
		{
			if (d.Y >= 0) continue;
			float t = (p.Position.Y - o.Y) / d.Y;
			if (t <= 0 || (best is not null && t >= best)) continue;
			float x = o.X + d.X * t, z = o.Z + d.Z * t; var sz = ((PlaneMesh)p.Mesh).Size;
			if (Mathf.Abs(x - p.Position.X) <= sz.X / 2 && Mathf.Abs(z - p.Position.Z) <= sz.Y / 2) { best = t; what = p.Name; }
		}
		if (best is null) return "sky/none";
		if (what != "backdrop") return what;
		float bx = o.X + d.X * tb, by = o.Y + d.Y * tb;
		if (Mathf.Abs(bx - backdrop.Position.X) > size.X / 2) return $"off-side x={bx:0.00}";
		if (by > backdrop.Position.Y + size.Y / 2) return $"off-top y={by:0.00}";
		if (by < backdrop.Position.Y - size.Y / 2) return $"off-bottom y={by:0.00}";
		float v = (backdrop.Position.Y + size.Y / 2 - by) / size.Y;
		float row = (art.Y + v * art.W) * tex.GetHeight();
		float limit = (art.Y + art.W) * tex.GetHeight();
		return row > limit + 0.01f ? $"below-art row={row:0}" : "ok";
	}

	static IEnumerable<(float X, float Y)> Perimeter()
	{
		for (int i = 0; i <= 16; i++) { float u = -1f + i / 8f; yield return (u, 1f); yield return (u, -1f); yield return (-1f, u); yield return (1f, u); }
	}

	static bool Fine(string r) => r == "ok" || r == "Floor" || r == "Water";

	// Rule: YOK-129 / designer 2026-11-09: every screen edge shows art or ground in every framing the camera can reach (whole perimeter, not only corners).
	[Test]
	public static void Edges_WholePerimeter_ShowArtOrWater_InEveryReachableFraming(Node runner)
	{
		var scene = Load(runner, Riverbank);
		try
		{
			var c = scene.Match.Config;
			var cam = new DynamicCamera(c);
			var oni = new DynamicCamera(c, CameraConfig.Default.ForBodies("ryo", "oni"));
			float apex = c.JumpY(c.JumpFrames / 2) / (float)SimConfig.Scale;
			var air = new DynamicCamera.Subject(new DynamicCamera.Body(0, apex, -240, 240), new DynamicCamera.Body(0, 0, -240, 240), 0, 0);
			var bad = new List<string>();
			foreach (var fc in new[] { cam, oni })
				for (int wi = 0; wi <= 6; wi++)
				{
					float w = Mathf.Lerp(fc.MinWidth, fc.MaxWidth * 1.01f, wi / 6f);
					foreach (float h in new[] { fc.Config.BaseHeightMeters, fc.Config.WideHeightMeters, Mathf.Max(fc.TargetHeight(air, w), fc.MinHeight(w, air)) })
						for (int ci = 0; ci <= 4; ci++)
						{
							float cx = Mathf.Lerp(-(fc.StageHalf - w / 2f), fc.StageHalf - w / 2f, ci / 4f);
							fc.Place(w, h, cx);
							foreach (var (nx, ny) in Perimeter())
							{
								string r = Land(scene, fc, nx, ny);
								if (!Fine(r)) bad.Add($"w {w:0} h {h:0.00} cx {cx:0} ({nx},{ny}): {r}");
							}
						}
				}
			Assert.True(bad.Count == 0, $"{bad.Count} edge samples wrong, first: {(bad.Count > 0 ? bad[0] : "")}");
		}
		finally { scene.QueueFree(); }
	}

	// Rule: YOK-129: the live camera over a real match (max separation at each corner, one or both jumping) never shows past the art.
	[Test]
	public static void Edges_LiveCamera_MaxSeparationCornersAndJumpApex(Node runner)
	{
		var scene = Load(runner, Riverbank);
		try
		{
			var c = scene.Match.Config;
			int corner = c.StageHalfWidth - c.BodyWidth / 2;
			var bad = new List<string>();
			foreach (int at in new[] { -corner, corner })
				foreach (bool both in new[] { false, true })
				{
					var m = new Match();
					m.P1.X = Math.Clamp(at - c.MaxSeparation / 2, -corner, corner - c.MaxSeparation); m.P2.X = m.P1.X + c.MaxSeparation;
					foreach (var cam in new[] { new DynamicCamera(c), new DynamicCamera(c, CameraConfig.Default.ForBodies("ryo", "kappa")) })
					{
						cam.Snap(DynamicCamera.SubjectOf(m));
						for (int t = 0; t < c.JumpFrames * 2; t++)
						{
							m.Step(new FighterInput(InputBits.Up), both ? new FighterInput(InputBits.Up) : FighterInput.None);
							cam.Step(DynamicCamera.SubjectOf(m));
							foreach (var (nx, ny) in Perimeter())
							{
								string r = Land(scene, cam, nx, ny);
								if (!Fine(r)) bad.Add($"at {at} both {both} tick {t} ({nx},{ny}): {r}");
							}
						}
					}
				}
			Assert.True(bad.Count == 0, $"{bad.Count} live-camera edge samples wrong, first: {(bad.Count > 0 ? bad[0] : "")}");
		}
		finally { scene.QueueFree(); }
	}

	// Rule: the painting meets the 3D water with no gap (designer 2026-11-09 swamp retake).
	[Test]
	public static void Join_NoGapBetweenWaterFarEdgeAndPainting(Node runner)
	{
		var scene = Load(runner, Riverbank);
		try
		{
			var st = scene.StageRoot;
			var b = st.GetNode<MeshInstance3D>("Backdrop"); var w = st.GetNode<MeshInstance3D>("Water");
			var bs = ((QuadMesh)b.Mesh).Size; var ws = ((PlaneMesh)w.Mesh).Size;
			Assert.True(w.Position.Z - ws.Y / 2 <= b.Position.Z + 1e-3f, $"water far edge z {w.Position.Z - ws.Y / 2} reaches the painting at z {b.Position.Z}");
			Assert.True(b.Position.Y - bs.Y / 2 <= w.Position.Y + 1e-3f, "quad bottom is not above the water plane");
			Assert.True(w.Position.X - ws.X / 2 <= b.Position.X - bs.X / 2 && w.Position.X + ws.X / 2 >= b.Position.X + bs.X / 2, "water spans the quad's width");
			Assert.True(w.Position.Z - ws.Y / 2 >= b.Position.Z - 5f, "water does not run far behind the painting");
			var fl = st.GetNode<MeshInstance3D>("Floor"); var fs = ((PlaneMesh)fl.Mesh).Size;
			Assert.True(w.Position.Z + ws.Y / 2 >= fl.Position.Z - fs.Y / 2, "no gap between the water's near edge and the bank floor's far edge");
			var wm = (ShaderMaterial)((PlaneMesh)w.Mesh).Material;
			Assert.True(Mathf.IsEqualApprox(wm.GetShaderParameter("far_z").AsSingle(), b.Position.Z), "far_z is the painting's plane");
			Assert.True(wm.GetShaderParameter("near_z").AsSingle() > wm.GetShaderParameter("far_z").AsSingle(), "near_z in front of far_z");
		}
		finally { scene.QueueFree(); }
	}

	// Rule: the art_rect stays inside the 1672x941 image and the shader's water row is inside the art.
	[Test]
	public static void Painting_ArtRectInsideTheImage_AndWaterVInsideArt(Node runner)
	{
		var scene = Load(runner, Riverbank);
		try
		{
			var m = Far(scene.StageRoot.GetNode<MeshInstance3D>("Backdrop"));
			var tex = m.GetShaderParameter("tex").As<Texture2D>();
			Vector4 a = m.GetShaderParameter("art_rect").AsVector4();
			Assert.Equal(1672, tex.GetWidth(), "width"); Assert.Equal(941, tex.GetHeight(), "height");
			Assert.True(a.X >= 0 && a.Y >= 0 && a.X + a.Z <= 1f + 1e-6f && a.Y + a.W <= 1f + 1e-6f, "art_rect inside the image");
			float wv = m.GetShaderParameter("water_v").AsSingle();
			Assert.True(wv > 0.8f && wv < a.Y + a.W, "water_v inside the art, near row 776");
		}
		finally { scene.QueueFree(); }
	}

	// Rule: deleted assets: nothing outside tests/docs names them, and every ext_resource of every scene and material exists.
	[Test]
	public static void DeletedLanternAssets_NothingLoadsThem_EveryResourceExists()
	{
		foreach (string p in new[] { "res://assets/generated/stage/riverbank/floating-lantern.glb", "res://assets/generated/stage/riverbank/floating-lantern-ref.png", "res://assets/generated/stage/riverbank/floating-lantern_persimmon.png", "res://materials/stage/floating_lantern.tres", "res://scenes/stage_props/floating_lantern.tscn" })
			Assert.True(!ResourceLoader.Exists(p) && !FileAccess.FileExists(p), $"{p} is deleted");
		var bad = new List<string>();
		var rx = new System.Text.RegularExpressions.Regex("\\[ext_resource[^\\]]*path=\"(res://[^\"]+)\"");
		void Scan(string dir)
		{
			foreach (var f in DirAccess.GetFilesAt(dir))
			{
				if (!(f.EndsWith(".tscn") || f.EndsWith(".tres") || f.EndsWith(".gdshader") || f.EndsWith(".cs"))) continue;
				string path = dir.TrimEnd('/') + "/" + f;
				string text = FileAccess.GetFileAsString(path);
				if (text.Contains("floating_lantern") || text.Contains("floating-lantern")) bad.Add($"{path} names a deleted lantern asset");
				if (f.EndsWith(".tscn") || f.EndsWith(".tres"))
					foreach (System.Text.RegularExpressions.Match m in rx.Matches(text))
						if (!ResourceLoader.Exists(m.Groups[1].Value)) bad.Add($"{path}: missing {m.Groups[1].Value}");
			}
			foreach (var d in DirAccess.GetDirectoriesAt(dir)) Scan(dir.TrimEnd('/') + "/" + d);
		}
		Scan("res://scenes"); Scan("res://materials"); Scan("res://shaders"); Scan("res://scripts");
		Assert.True(bad.Count == 0, string.Join("; ", bad.Take(5)));
	}

	// Rule: loading the swamp shows the swamp (not the grove fallback), every catalog asset present, a few ticks run.
	[Test]
	public static void Riverbank_LoadsAndSteps_EveryCatalogAssetPresent(Node runner)
	{
		Assert.True(StageCatalog.Riverbank.Present(), "assets present");
		var scene = Load(runner, Riverbank);
		try
		{
			Assert.Equal("riverbank", scene.StageKind, "riverbank, not the grove fallback");
			Assert.Equal(Riverbank, scene.StageRoot.SceneFilePath, "scene");
			for (int i = 0; i < 5; i++) scene.Step(FighterInput.None, FighterInput.None);
			foreach (var mi in scene.StageRoot.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>())
				Assert.True(mi.Mesh is not null, $"{mi.GetPath()} has a mesh");
		}
		finally { scene.QueueFree(); }
	}

	// Rule: YOK-150 Code Critic: a self-walked run builds the stage again per node; a grove node then a Kappa node ends on one clean swamp.
	[Test]
	public static void Rebuild_KappaNodeAfterGroveNode_IsTheCleanSwamp(Node runner)
	{
		string was = FightScene.OwnMapFile;
		FightScene.OwnMapFile = ProjectSettings.GlobalizePath("res://tests/fixtures/maps/kitsune-then-kappa.json");
		var s = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		s.ExternalDrive = true; s.FixedRunSeed = 48;
		try
		{
			runner.AddChild(s);
			Assert.Equal(BambooGroveStage.Kind, s.StageKind, "the Kitsune's grove first");
			s.Match.P2.Health = 1;
			for (int t = 0; t < 900 && s.Match.Phase != MatchPhase.Over; t++)
				s.Step(new FighterInput(t % 2 == 0 ? InputBits.DebugStrike : InputBits.None), FighterInput.None);
			s.BindingCard.Continue(); s.Rewards.Confirm();
			if (s.Rewards.Step == YokaiFighters.Ui.RewardStep.Target && s.Stage == DemoStage.Reward) s.Rewards.Confirm();
			Assert.Equal("fx-kappa", s.Node.NodeId, "on the Kappa node");
			Assert.Equal("riverbank", s.StageKind, "the swamp");
			Assert.Equal(1, s.GetNode(FightScene.StageContainerName).GetChildCount(), "one stage");
			var st = s.StageRoot;
			Assert.Equal(0, st.FindChildren("*", "OmniLight3D", true, false).Count, "no lantern lights");
			Assert.True(!st.HasNode("Props/Floats"), "no floats");
			s.Step(FighterInput.None, FighterInput.None);
		}
		finally { FightScene.OwnMapFile = was; s.QueueFree(); }
	}

	// Rule: loading the grove then the swamp then the swamp again leaves identical stages (no leaked state between builds).
	[Test]
	public static void Rebuild_GroveThenRiverbankThenRiverbank_Identical(Node runner)
	{
		string Dump(FightScene s) => string.Join("|", s.StageRoot.FindChildren("*", "Node3D", true, false).Cast<Node3D>().Select(n => $"{s.StageRoot.GetPathTo(n)}:{n.Transform}"));
		var g = Load(runner, FightScene.GroveScene); g.QueueFree();
		var a = Load(runner, Riverbank); var b = Load(runner, Riverbank);
		try { Assert.Equal(Dump(a), Dump(b), "same node tree and transforms"); Assert.Equal("riverbank", b.StageKind, "kind"); }
		finally { a.QueueFree(); b.QueueFree(); }
	}

	// Rule: determinism: two loads give the same node tree, transforms and shader parameters.
	[Test]
	public static void Determinism_TwoLoads_SameMaterialsAndTree(Node runner)
	{
		string Params(ShaderMaterial sm) => string.Join(",", sm.Shader.GetShaderUniformList().Select(x => x.AsString()).Select(p => $"{p}={sm.GetShaderParameter(p)}"));
		string Dump(FightScene s)
		{
			var sb = new System.Text.StringBuilder();
			foreach (var n in s.StageRoot.FindChildren("*", "", true, false))
			{
				sb.Append(n.GetClass()).Append(':').Append(s.StageRoot.GetPathTo(n)).Append(';');
				if (n is Node3D n3) sb.Append(n3.GlobalTransform).Append(';');
				if (n is MeshInstance3D mi)
				{
					if (mi.MaterialOverride is ShaderMaterial sm) sb.Append(Params(sm));
					if (mi.Mesh is PlaneMesh pm && pm.Material is ShaderMaterial wm) sb.Append(Params(wm));
				}
			}
			return sb.ToString();
		}
		var a = Load(runner, Riverbank); string da = Dump(a); a.QueueFree();
		var b = Load(runner, Riverbank); string db = Dump(b); b.QueueFree();
		Assert.True(da.Length > 200, "something was dumped");
		Assert.Equal(da, db, "identical");
	}

	// Rule: the stage is presentation only: ticks move nothing in the swamp and the sim hash equals the grove's.
	[Test]
	public static void Swamp_StageDoesNotMoveWithTicks_SimHashSameAsGrove(Node runner)
	{
		var a = Load(runner, Riverbank); var g = Load(runner, FightScene.GroveScene);
		try
		{
			string Xf(FightScene s) => string.Join("|", s.StageRoot.FindChildren("*", "Node3D", true, false).Cast<Node3D>().Select(n => n.Transform.ToString()));
			string before = Xf(a);
			for (int i = 0; i < 120; i++) { a.Step(FighterInput.None, FighterInput.None); g.Step(FighterInput.None, FighterInput.None); }
			Assert.Equal(before, Xf(a), "nothing in the swamp moves");
			Assert.Equal(g.Match.StateHash(), a.Match.StateHash(), "the stage does not touch the sim");
		}
		finally { a.QueueFree(); g.QueueFree(); }
	}

	// Rule: the other stages keep their lanterns and lights within StageRules.MaxLanternLights, shadowless.
	[Test]
	public static void OtherStages_KeepLanternsAndLights_WithinBudget(Node runner)
	{
		foreach (var scene in new[] { StageCatalog.ShrineGateScene, StageCatalog.TeaHouseScene, FightScene.GroveScene })
		{
			var s = Load(runner, scene);
			try
			{
				var lights = s.StageRoot.FindChildren("*", "OmniLight3D", true, false).Cast<OmniLight3D>().ToList();
				Assert.True(lights.Count >= 2 && lights.Count <= StageRules.MaxLanternLights, $"{scene}: {lights.Count} lights, budget {StageRules.MaxLanternLights}");
				Assert.True(lights.All(l => !l.ShadowEnabled && l.LightEnergy > 0f), $"{scene}: shadowless and lit");
				var cat = StageCatalog.For(scene);
				if (cat is not null) Assert.True(cat.Present(), $"{scene}: assets present");
			}
			finally { s.QueueFree(); }
		}
	}
}
