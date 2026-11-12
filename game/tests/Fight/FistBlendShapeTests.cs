using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>
/// E15 (AM26): the Tripo Ryo's hand is the open-hand mesh with a scripted <c>fist</c> blend shape. Clip data
/// (<c>hand</c>: "fist" default, "open") sets its weight every presented tick, blended over the clip blend (F3:
/// from the sample alone). Bodies without the shape (Kitsune, Oni, Kappa, the Meshy Ryo) are untouched.
/// </summary>
public static class FistBlendShapeTests
{
	static ClipCatalog Cat => FightScene.Catalog;
	const string Jab = "ryo-light-punch", Palm = "ryo-foxfire-cast";

	static PoseSample S(string clip, int frame = 1, string? from = null, int fromFrame = 1, float w = 1f) =>
		new(PoseKind.Attack, clip, frame, from, fromFrame, w);

	[Test]
	public static void Catalog_FoxfireOpensTheHand_EveryOtherClipIsAFist()
	{
		Assert.Equal(ClipHand.Open, Cat[Palm]!.Hand, "foxfire cast: open palm (E15)");
		Assert.Equal(0f, Cat[Palm]!.FistWeight, "open = weight 0");
		Assert.Equal(ClipHand.Fist, Cat[Jab]!.Hand, "jab: no hand field = fist");
		Assert.Equal(1f, Cat[Jab]!.FistWeight, "fist = weight 1");
		var open = Cat.Clips.Values.Where(c => c.Hand == ClipHand.Open).Select(c => c.Id).ToList();
		Assert.Equal(Palm, string.Join(",", open), "only the foxfire cast opens the hand");
	}

	[Test]
	public static void Loader_ReadsHand_MissingIsFist_BadValueIsAProblem()
	{
		string dir = Path.Combine(Path.GetTempPath(), "yf-clip-hand-" + System.Environment.ProcessId);
		string clips = Path.Combine(dir, "clips"), game = Path.Combine(dir, "game");
		Directory.CreateDirectory(clips);
		Directory.CreateDirectory(Path.Combine(game, "assets/generated/clips/ryo"));
		File.WriteAllText(Path.Combine(game, "assets/generated/clips/ryo/a.glb"), "");
		void Clip(string id, string extra) => File.WriteAllText(Path.Combine(clips, id + ".json"),
			$"{{\"kind\":\"clip\",\"id\":\"{id}\",\"frames_total\":10,\"trim_start\":0,\"trim_end\":9,\"file\":\"assets/generated/clips/ryo/a.glb\"{extra}}}");
		try
		{
			Clip("ryo-none", "");
			Clip("ryo-fist", ",\"hand\":\"fist\"");
			Clip("ryo-open", ",\"hand\":\"open\"");
			Clip("ryo-caps", ",\"hand\":\"Open\"");
			Clip("ryo-num", ",\"hand\":0");
			var cat = ClipCatalog.Load(clips, game);
			Assert.Equal(ClipHand.Fist, cat["ryo-none"]!.Hand, "missing field = fist");
			Assert.Equal(ClipHand.Fist, cat["ryo-fist"]!.Hand, "\"fist\"");
			Assert.Equal(ClipHand.Open, cat["ryo-open"]!.Hand, "\"open\"");
			foreach (string id in new[] { "ryo-caps", "ryo-num" })
			{
				Assert.True(cat.Has(id) && cat[id]!.Hand == ClipHand.Fist, $"{id}: kept, read as a fist");
				Assert.True(cat.Problems.Exists(p => p.StartsWith(id + ": hand")), $"{id}: listed as a problem");
			}
			Assert.Equal(2, cat.Problems.Count, "only the two bad hands are problems: " + string.Join("; ", cat.Problems));
			Assert.Equal(ClipHand.Fist, ClipCatalog.Load(clips, game, notesOnly: true)["ryo-open"]?.Hand ?? ClipHand.Fist, "notesOnly ignores the field");
		}
		finally { Directory.Delete(dir, true); }
	}

	[Test]
	public static void FistWeight_FromTheSample_BlendsOverTheClipBlend()
	{
		Assert.Equal(1f, FighterModel.FistWeight(S(Jab, 3), Cat), "fist clip: 1");
		Assert.Equal(0f, FighterModel.FistWeight(S(Palm, 3), Cat), "open-hand clip: 0");
		Assert.Equal(1f, FighterModel.FistWeight(S("no-such-clip"), Cat), "uncatalogued clip: fist");
		Assert.Equal(1f, FighterModel.FistWeight(new PoseSample(PoseKind.Idle, null, 1, null, 1, 1f), Cat), "no clip: fist");
		const int n = 6; // a blend length; the presenter's weight on blend tick k is (k + 1) / n
		for (int k = 0; k < n - 1; k++)
		{
			float w = (k + 1f) / n;
			float open = FighterModel.FistWeight(S(Palm, k + 1, Jab, 9, w), Cat);
			Assert.Equal(1f - w, open, $"blend tick {k}: fist into palm");
			Assert.Equal(open, FighterModel.FistWeight(S(Palm, k + 1, Jab, 9, w), Cat), $"blend tick {k}: same sample, same weight (F3)");
			Assert.Equal(w, FighterModel.FistWeight(S(Jab, k + 1, Palm, 40, w), Cat), $"blend tick {k}: palm back to fist");
			Assert.Equal(1f, FighterModel.FistWeight(S(Jab, k + 1, "ryo-idle-guard", 5, w), Cat), $"blend tick {k}: fist to fist stays closed");
		}
		Assert.Equal(0f, FighterModel.FistWeight(S(Palm, n, Jab, 9, 1f), Cat), "weight 1 is not a blend: the target hand");
	}

	[Test]
	public static void TripoRyo_HasTheFist_ClosedOnLoad_AndAppliesTheClipHand(Node runner)
	{
		var model = FighterModel.Create(FighterAnimSet.RyoTripo, Cat)!;
		Assert.True(model.HasFist, "Tripo Ryo has a fist blend shape");
		var mesh = model.GetNodeOrNull<MeshInstance3D>("Mirror/Yaw/Model/Armature/Skeleton3D/ryo");
		Assert.True(mesh != null && mesh.FindBlendShapeByName(FighterModel.FistShape) >= 0, "on Armature/Skeleton3D/ryo");
		Assert.Equal(1f, model.FistValue, "closed on load (Godot drops the glTF default weight)");
		model.Apply(S(Palm, 16), Cat, 1, 0);
		Assert.Equal(0f, model.FistValue, "foxfire cast: open palm");
		model.Apply(S(Jab, 4), Cat, 1, 1);
		Assert.Equal(1f, model.FistValue, "jab: fist");
		float[] run1 = Steps(model), run2 = Steps(model);
		for (int k = 0; k < run1.Length; k++)
		{
			Assert.True(Math.Abs(1f - (k + 1f) / 5f - run1[k]) < 1e-6f, $"blend tick {k}: weight {run1[k]} opens in step with the pose blend");
			Assert.Equal(run1[k], run2[k], $"blend tick {k}: replay gives the same weight");
		}
		model.Free();

		static float[] Steps(FighterModel m)
		{
			var w = new float[4];
			for (int k = 0; k < 4; k++)
			{
				m.Apply(S(Palm, k + 1, Jab, 9, (k + 1f) / 5f), Cat, 1, 10 + k);
				w[k] = m.FistValue;
			}
			return w;
		}
	}

	[Test]
	public static void OtherBodies_HaveNoFist_AndAreUntouched(Node runner)
	{
		foreach (var set in new[] { FighterAnimSet.Kitsune, FighterAnimSet.Oni, FighterAnimSet.Kappa, FighterAnimSet.RyoMeshy })
		{
			var model = FighterModel.Create(set, Cat);
			if (model is null) { Assert.True(set == FighterAnimSet.RyoMeshy, $"{set.Fighter} loads"); continue; }
			Assert.True(!model.HasFist && float.IsNaN(model.FistValue), $"{set.Fighter} ({set.ModelPath}): no fist blend shape");
			model.Apply(S(Palm, 16), Cat, 1, 0); // must not throw or add a shape
			Assert.True(!model.HasFist, $"{set.Fighter}: still none after an open-hand clip");
			model.Free();
		}
	}
}
