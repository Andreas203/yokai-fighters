using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-39: the stage, camera rig and fighters are editor scenes instanced in fight.tscn (not built in code), the
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

	[Test]
	public static void FightScene_InstancesStageCameraRigAndBothFighterSlots()
	{
		var state = GD.Load<PackedScene>("res://scenes/fight.tscn").GetState();
		string Instance(string path)
		{
			for (int i = 0; i < state.GetNodeCount(); i++)
			{
				string p = state.GetNodePath(i).ToString();
				if (p == "./" + path || p == path) return state.GetNodeInstance(i)?.ResourcePath ?? "";
			}
			return "(missing)";
		}
		Assert.Equal(FightScene.GroveScene, Instance("Stage"), "Stage is an instanced child of fight.tscn");
		Assert.Equal("res://scenes/camera_rig.tscn", Instance("CameraRig"), "CameraRig is an instanced child");
		Assert.Equal(FighterAnimSet.Ryo.ScenePath, Instance("Fighters/P1"), "P1 slot shows Ryo in the editor");
		Assert.Equal(FighterAnimSet.Kitsune.ScenePath, Instance("Fighters/P2"), "P2 slot shows the Kitsune in the editor");
	}

	[Test]
	public static void CameraRig_FovFromSceneMatchesF4_AndFollowsTheSim(Node runner)
	{
		var scene = Load(runner);
		Assert.True(Math.Abs(scene.Camera.Fov - FightScene.CameraFovDegrees) < 1e-4, $"camera_rig.tscn FOV {scene.Camera.Fov} = {FightScene.CameraFovDegrees} (F4)");
		Assert.True(scene.Camera.Current, "the rig's camera is current");
		for (int t = 0; t < 400; t++) scene.Step(new FighterInput(InputBits.Left), new FighterInput(InputBits.Left));
		var c = scene.Match.Config;
		var want = new Vector3(FightScene.ToMeters(FightCamera.CenterX(scene.Match)), FightScene.CameraHeight, FightScene.CameraDistance(c));
		Assert.True(scene.CameraRig.Position.IsEqualApprox(want), $"rig at {scene.CameraRig.Position}, sim camera {want}");
		Assert.True(FightCamera.CenterX(scene.Match) < 0, "walked left: the camera followed");
		scene.QueueFree();
	}

	[Test]
	public static void FighterScenes_AreManualAndKitsuneHasNinePlacedTails()
	{
		foreach (var set in new[] { FighterAnimSet.Ryo, FighterAnimSet.Kitsune })
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
			else Assert.True(tails is null, "Ryo has no tails");
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
