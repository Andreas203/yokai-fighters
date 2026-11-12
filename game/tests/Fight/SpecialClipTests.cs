using System;
using System.IO;
using System.Linq;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-226: every special in data/moves plays a real clip, never the jab fallback or a T-pose, on every body that can
/// hold it: Ryo's (any special can be drafted, and the Tanuki copies on his body, T0), on both Ryo rigs, and its source
/// yokai's. Data-driven, so a special added later needs its clip here before this goes green.
/// </summary>
public static class SpecialClipTests
{
	static ClipCatalog Cat => FightScene.Catalog;

	[Test]
	public static void EverySpecial_HasAPlayableClip_OnRyoAndOnItsSourceYokai()
	{
		var specials = SpecialLoader.LoadDirectory(ContentPaths.Data("moves"));
		Assert.True(specials.Length >= 8, $"data/moves has the specials ({specials.Length})");
		var fallback = FighterAnimSet.Ryo.ClipFor(PoseKind.Attack);
		bool tripo = FighterAnimSet.TripoRyo;
		try
		{
			foreach (var sp in specials)
			{
				Assert.True(FightScene.MoveClips.TryGetValue(sp.Id, out var clip), $"{sp.Id}: names a clip");
				Assert.True(Cat.Has(clip), $"{sp.Id}: clip {clip} is catalogued (data/clips)");
				foreach (bool useTripo in new[] { true, false })
				{
					FighterAnimSet.TripoRyo = useTripo;
					var ryo = FighterAnimSet.Ryo;
					string mine = ryo.MoveClip(clip);
					Assert.True(ryo.CanPlay(Cat[mine]), $"{sp.Id}: Ryo ({(useTripo ? "Tripo" : "Meshy")}) can't play {mine}");
					Assert.True(mine != fallback, $"{sp.Id}: Ryo falls back to the jab");
					// Same timing as the special's own clip (F2: its frame data came from that clip), through its own retime curve.
					Assert.Equal(Cat[clip]!.FramesTotal, Cat[mine]!.FramesTotal, $"{sp.Id}: Ryo's {mine} has the frames of {clip}");
					Assert.True(Math.Abs(Cat[mine]!.ComputedFrames - Cat[mine]!.FramesTotal) <= 1, $"{sp.Id}: {mine}'s trim and retime give its frames");
				}
				FighterAnimSet.TripoRyo = tripo;
				if (sp.Source == "ryo") continue;
				var body = FighterAnimSet.For(sp.Source);
				Assert.Equal(sp.Source, body.Fighter, $"{sp.Id}: its source has a body");
				Assert.True(body.CanPlay(Cat[body.MoveClip(clip)]), $"{sp.Id}: the {sp.Source} can't play {body.MoveClip(clip)}");
			}
		}
		finally { FighterAnimSet.TripoRyo = tripo; }
	}
}
