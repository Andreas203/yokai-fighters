using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-131. Presentation only: no rule ID applies beyond determinism (sim untouched).</summary>
public static class OniKappaBodyProbes
{
	static ClipCatalog Cat => FightScene.Catalog;

	// Case variants and unknown ids are not bodies; they fall back to the fighter's set.
	[Test]
	public static void ForNode_CaseVariantsAndUnknownIds_FallBackToFighter()
	{
		foreach (var id in new[] { "Oni", "ONI", "kappa ", "", "elder", "oni-elder", "tengu" })
		{
			Assert.True(!FighterAnimSet.HasBody(id), $"'{id}' is not a body");
			Assert.Equal(FighterAnimSet.Kitsune, FighterAnimSet.ForNode("kitsune", id), $"'{id}' keeps the fighter's set");
		}
		Assert.Equal(FighterAnimSet.Oni, FighterAnimSet.ForNode("tanuki", "oni"), "body follows the yokai, not the fighter");
	}

	// Every clip an Oni or Kappa can be asked for exists in the catalog (a missing one would T-pose).
	[Test]
	public static void EveryPose_AndMoveOverride_HasACataloguedClip()
	{
		foreach (var set in new[] { FighterAnimSet.Oni, FighterAnimSet.Kappa })
		{
			foreach (PoseKind k in System.Enum.GetValues<PoseKind>())
				Assert.True(Cat.Has(set.ClipFor(k)), $"{set.Fighter} {k}: clip {set.ClipFor(k)}");
			foreach (var raw in FightScene.MoveClips.Values.Distinct())
				Assert.True(Cat.Has(set.MoveClip(raw)), $"{set.Fighter} move clip {raw} -> {set.MoveClip(raw)}");
			Assert.True(Cat.Has(set.FallbackThrowClip), $"{set.Fighter} fallback throw");
		}
	}

	// LoopFrame stays in range for extreme n and the minimal clip.
	[Test]
	public static void LoopFrame_ExtremeInputs_StayInRange()
	{
		foreach (int total in new[] { 1, 2, 1000 })
			foreach (int n in new[] { 0, -1, int.MinValue + 1, int.MaxValue, 1_000_000 })
				foreach (bool rev in new[] { false, true })
				{
					int f = FighterPresenter.LoopFrame(n, total, rev);
					Assert.True(f >= 1 && f <= total, $"total {total} n {n} rev {rev}: {f}");
				}
		Assert.Equal(1, FighterPresenter.LoopFrame(5, 1, true), "single-frame clip");
	}

	// Random play: Oni/Kappa presenter frames always inside the clip, and the same inputs sample identically twice.
	[Test]
	public static void Presenters_RandomPlay_FramesInRange_AndDeterministic()
	{
		foreach (var set in new[] { FighterAnimSet.Oni, FighterAnimSet.Kappa })
		{
			var runs = new List<List<(string?, int)>>();
			for (int rep = 0; rep < 2; rep++)
			{
				var m = FightScene.NewMatch();
				var p = new FighterPresenter(set, Cat, FightScene.MoveClips);
				p.Observe(m, 1);
				var rng = new SimRng(21);
				var log = new List<(string?, int)>();
				for (int t = 0; t < 1500; t++)
				{
					m.Step(new FighterInput((InputBits)rng.Next(1 << 10)), new FighterInput((InputBits)rng.Next(1 << 10)));
					p.Observe(m, 1);
					var s = p.Sample;
					int total = Cat[s.Clip]?.FramesTotal ?? 1;
					Assert.True(s.Frame >= 1 && s.Frame <= total, $"{set.Fighter} tick {t}: {s.Clip} frame {s.Frame}/{total}");
					log.Add((s.Clip, s.Frame));
				}
				runs.Add(log);
			}
			Assert.True(runs[0].SequenceEqual(runs[1]), $"{set.Fighter}: same inputs sample the same frames");
		}
	}

	// The presenter must not influence the sim hash.
	[Test]
	public static void Observing_DoesNotChangeStateHash()
	{
		var a = FightScene.NewMatch();
		var b = FightScene.NewMatch();
		var p = new FighterPresenter(FighterAnimSet.Oni, Cat, FightScene.MoveClips);
		var rng = new SimRng(9);
		for (int t = 0; t < 600; t++)
		{
			var i1 = new FighterInput((InputBits)rng.Next(1 << 10));
			var i2 = new FighterInput((InputBits)rng.Next(1 << 10));
			a.Step(i1, i2); b.Step(i1, i2);
			p.Observe(a, 1);
			Assert.Equal(b.StateHash(), a.StateHash(), $"tick {t}");
		}
	}
}
