using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-39: the stage and camera rig are editor scenes instanced in fight.tscn (YOK-143: the fighters are spawned at runtime at its markers), the
/// scene files carry the settings determinism depends on, and the code still drives them from the sim.
/// </summary>
public static class EditableSceneTests
{
	static FightScene Load(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		return scene;
	}

	static string Instance(SceneState state, string path)
	{
		for (int i = 0; i < state.GetNodeCount(); i++)
		{
			string p = state.GetNodePath(i).ToString();
			if (p == "./" + path || p == path) return state.GetNodeInstance(i)?.ResourcePath ?? "";
		}
		return "(missing)";
	}

	static int IndexOf(SceneState state, string path)
	{
		for (int i = 0; i < state.GetNodeCount(); i++)
		{
			string p = state.GetNodePath(i).ToString();
			if (p == "./" + path || p == path) return i;
		}
		return -1;
	}

	[Test]
	public static void FightScene_InstancesStageAndCameraRig_FightersIsAnEmptyContainerWithSpawnMarkers()
	{
		var state = GD.Load<PackedScene>("res://scenes/fight.tscn").GetState();
		Assert.Equal(FightScene.GroveScene, Instance(state, "Stage"), "Stage is an instanced child of fight.tscn");
		Assert.Equal("res://scenes/camera_rig.tscn", Instance(state, "CameraRig"), "CameraRig is an instanced child");
		// YOK-143: no hard-instanced bodies; the scene is a template with a container and two markers.
		Assert.Equal("", Instance(state, FightScene.FightersName), "Fighters is a plain container");
		for (int i = 0; i < state.GetNodeCount(); i++)
		{
			string p = state.GetNodePath(i).ToString();
			Assert.True(!p.StartsWith("./Fighters/") && !p.StartsWith("Fighters/"), $"fight.tscn holds no body under Fighters ({p})");
		}
		foreach (var (name, i) in new[] { (FightScene.P1SpawnName, 0), (FightScene.P2SpawnName, 1) })
		{
			int n = IndexOf(state, name);
			Assert.True(n >= 0, $"{name} exists");
			Assert.Equal("Marker3D", state.GetNodeType(n).ToString(), $"{name} is a Marker3D");
			Transform3D t = Transform3D.Identity;
			for (int k = 0; k < state.GetNodePropertyCount(n); k++)
				if (state.GetNodePropertyName(n, k) == "transform") t = state.GetNodePropertyValue(n, k).AsTransform3D();
			Assert.True(t.Origin.IsEqualApprox(FightScene.DefaultSpawn[i]) && t.Basis.IsEqualApprox(Basis.Identity), $"{name} at {t.Origin}, unrotated (same placement as before YOK-143)");
		}
	}

	[Test]
	public static void FightScene_InstancesBothBodiesAtTheirMarkers(Node runner)
	{
		// YOK-143: at load (before any render moves them to the sim's positions) each body sits on its marker.
		var scene = Load(runner);
		try
		{
			var fighters = scene.GetNode<Node3D>(FightScene.FightersName);
			Assert.Equal(2, fighters.GetChildCount(), "two bodies under Fighters");
			foreach (var (slot, i, path) in new[] { ("P1", 0, FighterAnimSet.Ryo.ScenePath), ("P2", 1, FighterAnimSet.Kitsune.ScenePath) })
			{
				var body = fighters.GetNodeOrNull<FighterModel>(slot);
				Assert.True(body != null && ReferenceEquals(body, scene.ModelOf(i)), $"Fighters/{slot} is the model of player {i}");
				Assert.Equal(path, body!.SceneFilePath, $"{slot} body scene");
				Assert.Equal(i, body.GetIndex(), $"{slot} is child {i} of Fighters");
				Assert.True(body.Transform.IsEqualApprox(scene.SpawnOf(i)), $"{slot} at {body.Transform.Origin}, marker {scene.SpawnOf(i).Origin}");
			}
		}
		finally { scene.QueueFree(); }
	}

	[Test]
	public static void CameraRig_FovFromSceneMatchesF4_AndFollowsTheSim(Node runner)
	{
		var scene = Load(runner);
		Assert.True(Math.Abs(scene.Camera.Fov - FightScene.CameraFovDegrees) < 1e-4, $"camera_rig.tscn FOV {scene.Camera.Fov} = {FightScene.CameraFovDegrees} (F4)");
		Assert.True(scene.Camera.Current, "the rig's camera is current");
		for (int t = 0; t < 400; t++) scene.Step(new FighterInput(InputBits.Left), new FighterInput(InputBits.Left));
		// YOK-129: the rig carries the dynamic camera's transform (position and pitch).
		var (x, y, z, pitch) = scene.Cam.Transform;
		var want = new Vector3(x, y, z);
		Assert.True(scene.CameraRig.Position.IsEqualApprox(want), $"rig at {scene.CameraRig.Position}, dynamic camera {want}");
		Assert.True(Math.Abs(scene.CameraRig.Rotation.X - pitch) < 1e-5f, $"rig pitch {scene.CameraRig.Rotation.X} = {pitch}");
		Assert.True(scene.Cam.CenterX < 0, "walked left: the camera followed");
		Assert.True(scene.Cam.InFrame(DynamicCamera.SubjectOf(scene.Match)), "both models in the dynamic view");
		scene.QueueFree();
	}

	[Test]
	public static void DynamicCameraProjection_MatchesTheRenderedCamera_DownAndUpPitch(Node runner)
	{
		// YOK-129: the pure camera maths (Cam.ScreenYPx, used by every in-frame and HUD-line check) must agree with
		// Godot's Camera3D within 1 px, for a feet point and a top point, looking down (close) and up (widest).
		var scene = Load(runner);
		try
		{
			float hPx = scene.GetViewport().GetVisibleRect().Size.Y;
			foreach (var (name, p1, p2, wantDown) in new[] { ("close", InputBits.Right, InputBits.Left, true), ("widest", InputBits.Left, InputBits.Right, false) })
			{
				for (int t = 0; t < 400; t++) scene.Step(new FighterInput(p1), new FighterInput(p2));
				for (int t = 0; t < 120; t++) scene.Step(FighterInput.None, FighterInput.None);
				var cam = scene.Cam;
				Assert.True(wantDown ? cam.PitchDegrees > 0f : cam.PitchDegrees < 0f, $"{name}: pitch {cam.PitchDegrees} deg ({(wantDown ? "down" : "up")})");
				foreach (var f in scene.Match.Fighters)
					foreach (float h in new[] { 0f, cam.Config.ModelTopMeters })
					{
						float x = f.X / (float)SimConfig.Scale;
						float godot = scene.Camera.UnprojectPosition(new Vector3(x / DynamicCamera.UnitsPerMeter, h, 0f)).Y * 1080f / hPx;
						float pure = cam.ScreenYPx(x, h);
						Assert.True(Math.Abs(godot - pure) <= 1f, $"{name}: x {x}, h {h}: pure {pure:0.00} px vs rendered {godot:0.00} px");
					}
			}
		}
		finally { scene.QueueFree(); }
	}

	[Test]
	public static void FighterScenes_AreManualAndKitsuneHasNinePlacedTails()
	{
		foreach (var set in new[] { FighterAnimSet.Ryo, FighterAnimSet.Kitsune, FighterAnimSet.Oni, FighterAnimSet.Kappa })
		{
			var model = GD.Load<PackedScene>(set.ScenePath).Instantiate<FighterModel>();
			Assert.Equal(set.Fighter, model.Fighter, $"{set.ScenePath}: Fighter id");
			var player = model.FindChild(FighterModel.PlayerName, true, false) as AnimationPlayer;
			Assert.True(player != null, $"{set.ScenePath}: has the Animator");
			Assert.True(player!.CallbackModeProcess == AnimationMixer.AnimationCallbackModeProcess.Manual, $"{set.ScenePath}: Animator is Manual in the scene file (F3)");
			Assert.True(model.FindChild("Skeleton3D", true, false) is Skeleton3D, $"{set.ScenePath}: rigged model instanced");
			Assert.True(model.GetChildren().Any(c => c is ToonLook), $"{set.ScenePath}: toon look");
			var tails = model.FindChild(FighterModel.TailsName, true, false);
			if (set == FighterAnimSet.Kitsune)
			{
				Assert.True(tails is BoneAttachment3D { BoneName: "Hips" }, "the Kitsune's tails ride the hips");
				Assert.Equal(FighterModel.TailCount, tails!.GetChildCount(), "nine placed tails");
				foreach (Node t in tails.GetChildren())
					Assert.True(t.FindChild("*", true, false) != null && ((Node3D)t).Rotation.IsZeroApprox(), $"{t.Name}: the pivot carries only the sway (fan pose on its child)");
			}
			else Assert.True(tails is null, $"{set.Fighter} has no tails");
			model.Free();
		}
	}

	[Test]
	public static void KitsuneTails_FollowTheHips(Node runner)
	{
		var scene = Load(runner);
		var kit = scene.ModelOf(1)!;
		for (int t = 0; t < 30; t++) scene.Step(new FighterInput(InputBits.Right), new FighterInput(InputBits.Down));
		var tails = (BoneAttachment3D)kit.FindChild(FighterModel.TailsName, true, false)!;
		tails.OnSkeletonUpdate(); // what the skeleton's end-of-frame update does (no frames run in this test)
		var skel = kit.Skeleton;
		Vector3 hips = (skel.GlobalTransform * skel.GetBoneGlobalPose(skel.FindBone("Hips"))).Origin;
		Assert.True(tails.GlobalPosition.DistanceTo(hips) < 1e-3f, $"tails at {tails.GlobalPosition}, hips at {hips}");
		scene.QueueFree();
	}
}
