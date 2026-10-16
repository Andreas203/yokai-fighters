using System.Linq;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// PLAYTEST (YOK-oni-kappa-playtest): Oni and Kappa nodes show their own bodies on the Kitsune stand-in kit.
/// Presentation only: the node's fighter (the sim's kit) stays the stand-in.
/// </summary>
public static class OniKappaBodyTests
{
	static ClipCatalog Cat => FightScene.Catalog;

	[Test]
	public static void ForNode_ShowsTheYokaiBody_KitStaysTheStandIn()
	{
		Assert.Equal(FighterAnimSet.Oni, FighterAnimSet.ForNode("kitsune", "oni"), "an Oni duel shows the Oni");
		Assert.Equal(FighterAnimSet.Oni, FighterAnimSet.ForNode("kitsune", "elder-oni"), "the Elder Oni shows the Oni");
		Assert.Equal(FighterAnimSet.Kappa, FighterAnimSet.ForNode("kitsune", "kappa"), "a Kappa duel shows the Kappa");
		Assert.Equal(FighterAnimSet.Kappa, FighterAnimSet.ForNode("kitsune", "elder-kappa"), "the Elder Kappa shows the Kappa");
		Assert.Equal(FighterAnimSet.Kitsune, FighterAnimSet.ForNode("kitsune", "kitsune"), "a Kitsune duel is unchanged");
		Assert.Equal(FighterAnimSet.Kitsune, FighterAnimSet.ForNode("kitsune", null), "a node with no yokai shows its fighter");
		Assert.Equal(FighterAnimSet.Ryo, FighterAnimSet.ForNode("tanuki", "tanuki"), "the Tanuki is unchanged");
		foreach (var id in new[] { "oni", "kappa" })
			Assert.Equal("kitsune", DevJump.FighterOf(new DevFightSetup(id)), $"{id}: the sim still fights the Kitsune kit (no sim change)");
	}

	[Test]
	public static void OniAndKappa_ModelsLoad_KappaSharesTheKitsuneClips()
	{
		foreach (var set in new[] { FighterAnimSet.Oni, FighterAnimSet.Kappa })
		{
			Assert.True(ResourceLoader.Exists(set.ModelPath), $"{set.Fighter}: rigged model {set.ModelPath}");
			var model = FighterModel.Create(set, Cat);
			Assert.True(model != null, $"{set.Fighter}: model scene sets up on the catalog");
			model!.Free();
		}
		Assert.True(ReferenceEquals(FighterAnimSet.Kappa.Clips, FighterAnimSet.Kitsune.Clips), "kappa: the Kitsune's shared clips");
	}

	/// <summary>The Oni plays his own club kit (oni-club-clips.md final summary) on the Kitsune stand-in moves.</summary>
	[Test]
	public static void Oni_PlaysHisOwnClubKit()
	{
		var oni = FighterAnimSet.Oni;
		foreach (var (k, clip) in new[]
		{
			(PoseKind.Idle, "oni-idle"), (PoseKind.WalkFwd, "oni-walk-fwd"), (PoseKind.WalkBack, "oni-walk-fwd"),
			(PoseKind.HitHigh, "oni-hit-high"), (PoseKind.HitLow, "oni-hit-low"), (PoseKind.BlockLow, "oni-block-low"),
			(PoseKind.BlockHigh, "ryo-block-high"), (PoseKind.Crouch, "ryo-crouch"), (PoseKind.Knockdown, "ryo-knockdown"),
			(PoseKind.GetUp, "ryo-get-up"),
		})
			Assert.Equal(clip, oni.ClipFor(k), $"oni {k}");
		Assert.True(oni.ReversedPoses.Contains(PoseKind.WalkBack), "oni walk back is the forward walk reversed");
		foreach (var (kit, clip) in new[]
		{
			("kitsune-light-punch", "ryo-light-punch"), ("kitsune-medium-punch", "oni-medium-punch"),
			("kitsune-medium-kick", "oni-medium-kick"), ("kitsune-heavy-punch", "oni-heavy-punch"),
			("kitsune-heavy-kick", "ryo-heavy-kick"), ("kitsune-throw", "ryo-generic-throw"),
		})
		{
			Assert.Equal(clip, oni.MoveClip(kit), $"oni plays {kit} as {clip}");
			Assert.True(Cat.Has(clip), $"{clip} is catalogued");
		}
		Assert.True(Cat["oni-medium-punch"]!.Retime != null && Cat["oni-heavy-punch"]!.Retime != null, "MP and HP carry their retime curves");
	}

	/// <summary>The Oni walking back (P2 walking right, away from P1) steps his forward walk backwards, frame by frame.</summary>
	[Test]
	public static void Oni_WalkBackPlaysTheForwardWalkReversed()
	{
		var m = FightScene.NewMatch();
		var p = new FighterPresenter(FighterAnimSet.Oni, Cat, FightScene.MoveClips);
		p.Observe(m, 1);
		int total = Cat["oni-walk-fwd"]!.FramesTotal;
		int checkedTicks = 0;
		for (int t = 1; t <= total + 5; t++)
		{
			int x0 = m.P2.X;
			m.Step(FighterInput.None, new FighterInput(InputBits.Right));
			p.Observe(m, 1);
			if (m.P2.X == x0) break;
			Assert.Equal(PoseKind.WalkBack, p.Sample.Kind, $"tick {t}: walking back");
			Assert.Equal("oni-walk-fwd", p.Sample.Clip, $"tick {t}: on the forward walk clip");
			Assert.Equal(total - (t - 1) % total, p.Sample.Frame, $"tick {t}: reversed loop frame");
			checkedTicks++;
		}
		Assert.True(checkedTicks > 10, $"walked back for a while ({checkedTicks} ticks)");
	}

	[Test]
	public static void DevJump_OniAndKappaArePickable()
	{
		var foes = DevJump.Opponents();
		foreach (var id in new[] { "oni", "kappa" })
			Assert.True(foes.First(o => o.Yokai == id).Built, $"{id}: pickable in the dev fight menu");
		Assert.True(!foes.First(o => o.Yokai == "human-guises").Built, "human guises still greyed");
	}
}
