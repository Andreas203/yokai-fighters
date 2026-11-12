using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for the E15 fist blend shape (AM26). Expected values: E15, F3, C1.</summary>
public static class FistBlendShapeBreakerProbes
{
	static ClipCatalog Cat => FightScene.Catalog;

	// E15: a model made after another one opened its hand starts closed again (no leak through shared mesh).
	[Test]
	public static void NewModel_StartsClosed_AfterAnotherOpenedItsHand(Node runner)
	{
		var a = FighterModel.Create(FighterAnimSet.RyoTripo, Cat)!;
		a.Apply(new PoseSample(PoseKind.Attack, "ryo-foxfire-cast", 16, null, 1, 1f), Cat, 1, 0);
		Assert.Equal(0f, a.FistValue, "a is open");
		var b = FighterModel.Create(FighterAnimSet.RyoTripo, Cat)!;
		Assert.Equal(1f, b.FistValue, "b closed on load");
		Assert.Equal(0f, a.FistValue, "a unchanged by b");
		a.Free(); b.Free();
	}

	// E15: the hand returns to a fist after the cast when the pose is idle (a sample with no clip at all).
	[Test]
	public static void IdleSampleWithNoClip_ClosesTheHand(Node runner)
	{
		var m = FighterModel.Create(FighterAnimSet.RyoTripo, Cat)!;
		m.Apply(new PoseSample(PoseKind.Attack, "ryo-foxfire-cast", 16, null, 1, 1f), Cat, 1, 0);
		m.Apply(new PoseSample(PoseKind.Idle, null, 1, null, 1, 1f), Cat, 1, 1);
		Assert.Equal(1f, m.FistValue, "no clip = fist");
		m.Free();
	}

	// E15: Kitsune/Oni/Kappa bodies must not gain the shape, and the Meshy Ryo must load.
	[Test]
	public static void MeshyRyo_Loads_AndHasNoFist(Node runner)
	{
		var m = FighterModel.Create(FighterAnimSet.RyoMeshy, Cat);
		Assert.True(m != null, "Meshy Ryo model loads");
		Assert.True(!m!.HasFist, "Meshy Ryo has no fist shape");
		m.Free();
	}

	// E15: rig default (glTF weight 1) is what a fresh model shows even when the first sample is an open clip
	// arriving mid-blend from an uncatalogued source (treated as a fist): weight must be w*0 + (1-w)*1.
	[Test]
	public static void UncataloguedSource_BlendsFromFist()
	{
		var s = new PoseSample(PoseKind.Attack, "ryo-foxfire-cast", 3, "no-such-clip", 1, 0.25f);
		Assert.Equal(0.75f, FighterModel.FistWeight(s, Cat), "from a fist toward open");
	}

	// Loader: " open" and "OPEN" and null are not the enum values: problems, fist.
	[Test]
	public static void Loader_RejectsNullAndPaddedHand()
	{
		string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yf-hand-probe-" + System.Environment.ProcessId);
		string clips = System.IO.Path.Combine(dir, "clips"), game = System.IO.Path.Combine(dir, "game");
		System.IO.Directory.CreateDirectory(clips);
		System.IO.Directory.CreateDirectory(System.IO.Path.Combine(game, "assets/generated/clips/ryo"));
		System.IO.File.WriteAllText(System.IO.Path.Combine(game, "assets/generated/clips/ryo/a.glb"), "");
		void Clip(string id, string h) => System.IO.File.WriteAllText(System.IO.Path.Combine(clips, id + ".json"),
			$"{{\"kind\":\"clip\",\"id\":\"{id}\",\"frames_total\":10,\"trim_start\":0,\"trim_end\":9,\"file\":\"assets/generated/clips/ryo/a.glb\",\"hand\":{h}}}");
		try
		{
			Clip("ryo-pad", "\" open\""); Clip("ryo-null", "null"); Clip("ryo-arr", "[\"open\"]");
			var cat = ClipCatalog.Load(clips, game);
			foreach (string id in new[] { "ryo-pad", "ryo-null", "ryo-arr" })
			{
				Assert.Equal(ClipHand.Fist, cat[id]!.Hand, $"{id}: fist");
				Assert.True(cat.Problems.Exists(p => p.StartsWith(id + ": hand")), $"{id}: problem listed");
			}
		}
		finally { System.IO.Directory.Delete(dir, true); }
	}
}
