using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-146 (skeleton Option A): Ryo fights on the Tripo rig (metre bones under an unscaled Armature) with his stopgap
/// clips from <c>clips/ryo-tripo/</c>; the Kitsune, Oni and Kappa stay on the Meshy skeleton and its clips. A Meshy clip
/// never plays on the Tripo Ryo, nor the reverse (the spine bones mean different things on the two skeletons).
/// </summary>
public static class TripoRyoTests
{
	static ClipCatalog Cat => FightScene.Catalog;
	const string TripoRoot = "res://assets/generated/clips/ryo-tripo/";

	[Test]
	public static void Switch_PicksTheTripoRyo_AndTheTanukiFollows()
	{
		Assert.True(FighterAnimSet.TripoRyo, "this branch fights the Tripo Ryo");
		Assert.True(ReferenceEquals(FighterAnimSet.RyoTripo, FighterAnimSet.Ryo), "Ryo = the Tripo set");
		Assert.True(ReferenceEquals(FighterAnimSet.RyoTripo, FighterAnimSet.For("ryo")), "P1's body");
		Assert.True(ReferenceEquals(FighterAnimSet.RyoTripo, FighterAnimSet.ForNode("tanuki", "tanuki")), "T0: the Tanuki fights with Ryo's body");
		Assert.True(ReferenceEquals(FighterAnimSet.RyoTripo, FighterAnimSet.For("tanuki")), "T0: the Tanuki kit too");
		Assert.Equal(TripoRoot, FighterAnimSet.RyoTripo.ClipRoot, "Ryo's clip root");
		Assert.True(FighterAnimSet.RyoTripo.ModelPath.EndsWith("ryo-tripo-rigged.glb"), "Tripo model");
		try
		{
			FighterAnimSet.TripoRyo = false;
			Assert.True(ReferenceEquals(FighterAnimSet.RyoMeshy, FighterAnimSet.Ryo), "flipped: the Meshy Ryo");
			Assert.True(ReferenceEquals(FighterAnimSet.RyoMeshy, FighterAnimSet.ForNode("tanuki", "tanuki")), "flipped: the Tanuki follows");
			Assert.True(FighterAnimSet.RyoMeshy.ClipRoot is null, "the Meshy Ryo plays the catalog's files");
		}
		finally { FighterAnimSet.TripoRyo = true; }
		foreach (var set in new[] { FighterAnimSet.Kitsune, FighterAnimSet.Oni, FighterAnimSet.Kappa })
			Assert.True(set.ClipRoot is null, $"{set.Fighter} stays on the Meshy skeleton");
	}

	[Test]
	public static void TripoRyo_Loads_WithItsSkeleton_InMetres(Node runner)
	{
		var model = FighterModel.Create(FighterAnimSet.RyoTripo, Cat);
		Assert.True(model != null, "Tripo Ryo loads");
		Assert.True(model!.GetNodeOrNull<Skeleton3D>("Mirror/Yaw/Model/Armature/Skeleton3D") is { } sk && ReferenceEquals(sk, model.Skeleton), "Model/Armature/Skeleton3D found");
		var armature = model.GetNode<Node3D>("Mirror/Yaw/Model/Armature");
		Assert.True(armature.Scale.IsEqualApprox(Vector3.One), $"Armature unscaled ({armature.Scale})");
		var skel = model.Skeleton;
		Assert.Equal(25, skel.GetBoneCount(), "25 bones (skeleton.md)");
		foreach (var bone in new[] { "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head", "RightHand", "LeftToeBase", "RightToeBase" })
			Assert.True(skel.FindBone(bone) >= 0, $"{bone} on the rig");
		float hipY = skel.GetBoneRest(skel.FindBone("Hips")).Origin.Y;
		Assert.True(hipY > 0.9f && hipY < 1.1f, $"Hips rest {hipY} is about 1 m (metre bones)");
		model.Free();
	}

	[Test]
	public static void EveryRyoClip_ResolvesOnTheTripoSkeleton_AtHipRatioOne(Node runner)
	{
		var set = FighterAnimSet.RyoTripo;
		var model = FighterModel.Create(set, Cat)!;
		var ryoClips = set.Clips.Values.Concat(set.ClipOverrides.Values).Append(set.FallbackThrowClip)
			.Concat(FightScene.MoveClips.Values.Where(c => c.StartsWith("ryo-")))
			.Concat(Cat.Clips.Keys.Where(c => c.StartsWith("ryo-"))).Distinct().ToList();
		Assert.True(ryoClips.Count >= 24, $"Ryo's clips ({ryoClips.Count})");
		foreach (string id in ryoClips)
		{
			var info = Cat[id];
			Assert.True(info != null, $"{id} catalogued");
			string? path = set.ClipPath(info!);
			Assert.True(path != null && path.StartsWith(TripoRoot), $"{id}: plays from clips/ryo-tripo/ ({path})");
			Assert.True(model.Has(id), $"{id} in the Tripo Ryo's library");
			float k = model.HipScaleFor(info!);
			float raw = model.Skeleton.GetBoneRest(model.Skeleton.FindBone("Hips")).Origin.Y / FighterModel.ClipHipRestY(path!);
			Assert.Equal(1f, k, $"{id}: hip scale {k} is 1");
			Assert.True(Math.Abs(raw - 1f) < 0.01f, $"{id}: raw hip ratio {raw:0.0000} is about 1 (no cm clip on the metre rig)");
		}
		model.Free();
	}

	[Test]
	public static void TripoRyo_NeverPlaysAMeshyClip(Node runner)
	{
		var set = FighterAnimSet.RyoTripo;
		var model = FighterModel.Create(set, Cat)!;
		var lib = model.Player.GetAnimationLibrary(FighterModel.LibraryName);
		foreach (var name in lib.GetAnimationList())
		{
			string? path = set.ClipPath(Cat[name.ToString()]!);
			Assert.True(path != null && path.StartsWith(TripoRoot), $"{name}: only Tripo files ({path})");
		}
		// A clip id whose Meshy file has no Tripo twin is left out (the Kitsune's and the Oni's own takes); an id that
		// names a Ryo file (e.g. oni-block-low on ryo-crouch.glb) plays that file's Tripo twin, checked above.
		int left = 0;
		foreach (var (id, info) in Cat.Clips)
		{
			string twin = TripoRoot + info.ResPath[(info.ResPath.LastIndexOf('/') + 1)..];
			if (ResourceLoader.Exists(twin)) continue;
			left++;
			Assert.True(!model.Has(id), $"{id} ({info.ResPath}) has no Tripo twin: not on the Tripo Ryo");
		}
		Assert.True(left >= 10, $"the Kitsune's and the Oni's takes are left out ({left})");
		model.Free();
	}

	[Test]
	public static void KitsuneAndOni_StillLoadTheMeshyClips(Node runner)
	{
		foreach (var set in new[] { FighterAnimSet.Kitsune, FighterAnimSet.Oni, FighterAnimSet.Kappa })
		{
			var model = FighterModel.Create(set, Cat);
			Assert.True(model != null, $"{set.Fighter} loads");
			foreach (string id in new[] { "ryo-dash-back", "ryo-crouch", "ryo-light-punch", "ryo-knockdown", "ryo-get-up" })
			{
				var info = Cat[id]!;
				Assert.Equal(info.ResPath, set.ClipPath(info), $"{set.Fighter} plays {id} from the catalog's Meshy file");
				Assert.True(!(info.ResPath.Contains("/ryo-tripo/")), $"{id}: the catalog names the Meshy file");
				Assert.True(model!.Has(id), $"{set.Fighter} has {id}");
			}
			model!.Free();
		}
	}

	[Test]
	public static void DevMenu_FlipsTheRyoBody()
	{
		var page = YokaiFighters.Dev.DevMenu.ToolsPage();
		var box = page.GetNodeOrNull<CheckButton>("TripoRyo");
		Assert.True(box != null && box.ButtonPressed, "the Tools page shows the Tripo Ryo switch, on");
		try
		{
			box!.ButtonPressed = false;
			Assert.True(!FighterAnimSet.TripoRyo && ReferenceEquals(FighterAnimSet.RyoMeshy, FighterAnimSet.Ryo), "off: the Meshy Ryo");
			box.ButtonPressed = true;
			Assert.True(FighterAnimSet.TripoRyo, "on again: the Tripo Ryo");
		}
		finally { FighterAnimSet.TripoRyo = true; page.Free(); }
	}

	[Test]
	public static void Presenter_FallsBack_WhenTheBodyLacksTheMoveClip()
	{
		var set = FighterAnimSet.RyoTripo;
		Assert.True(!(set.CanPlay(Cat["kitsune-light-punch"])), "no Tripo kitsune-light-punch");
		Assert.True(set.CanPlay(Cat["ryo-foxfire-cast"]), "Ryo's own foxfire cast is on Tripo");
		Assert.True(set.CanPlay(Cat[set.FallbackThrowClip]), "the fallback throw clip is on Tripo");
		Assert.True(set.CanPlay(Cat[set.ClipFor(PoseKind.Attack)]), "the fallback attack clip is on Tripo");
		Assert.True(!(set.CanPlay(null)), "no clip");
	}
}
