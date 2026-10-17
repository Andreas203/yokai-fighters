using System.Linq;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-131: Oni and Kappa nodes show their own bodies on the Kitsune stand-in kit.
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
		foreach (var id in new[] { "ryo", "kitsune", "tanuki" })
			Assert.True(!FighterAnimSet.HasBody(id), $"{id}: not a stand-in body");
		Assert.Equal("ryo-generic-throw", FighterAnimSet.Oni.FallbackThrowClip, "the Oni falls back to Ryo's throw");
		Assert.Equal("kitsune-throw", FighterAnimSet.Kappa.FallbackThrowClip, "the Kappa falls back to the Kitsune's throw");
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

	/// <summary>Reversed loop mapping: frame total + 1 - n, wrapping, always inside 1..total; forward unchanged.</summary>
	[Test]
	public static void LoopFrame_ReversedIsTotalPlusOneMinusN()
	{
		foreach (int total in new[] { 1, 2, 7, 33 })
			for (int n = -2 * total; n <= 3 * total; n++)
			{
				int fwd = FighterPresenter.LoopFrame(n, total, false);
				int rev = FighterPresenter.LoopFrame(n, total, true);
				Assert.Equal(((n - 1) % total + total) % total + 1, fwd, $"total {total} n {n}: forward loop as before");
				Assert.Equal(total + 1 - fwd, rev, $"total {total} n {n}: reversed = total + 1 - forward");
				Assert.True(rev >= 1 && rev <= total, $"total {total} n {n}: reversed frame {rev} in range");
			}
		Assert.Equal(33, FighterPresenter.LoopFrame(1, 33, true), "first step shows the last frame");
		Assert.Equal(1, FighterPresenter.LoopFrame(33, 33, true), "step total shows the first frame");
		Assert.Equal(33, FighterPresenter.LoopFrame(34, 33, true), "then wraps to the last frame again");
	}

	/// <summary>An Oni and a Kappa node show their bodies in the fight scene, and the sim runs exactly as with no body at all.</summary>
	[Test]
	public static void Scene_OniAndKappaNodes_ShowTheirBodies_SimUntouched(Node runner)
	{
		var pool = FightScene.LoadAbilityPool();
		foreach (var (yokai, elder) in new[] { ("oni", false), ("kappa", false), ("oni", true) })
		{
			var jump = DevJump.BuildFight(new DevFightSetup(yokai, Elder: elder, Seed: 11), pool);
			Assert.True(jump.Session.Enter(jump.NodeId).Ok, $"{yokai}: node entered");
			var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			scene.ExternalDrive = true;
			scene.Session = jump.Session;
			runner.AddChild(scene);
			try
			{
				Assert.Equal(yokai, scene.ModelOf(1)?.Fighter, $"{yokai}: P2 shows the {yokai} body");
				Assert.Equal("kitsune", FightScene.Opponent, $"{yokai}: P2 still fights the Kitsune stand-in kit");
				var bare = FightScene.NewMatch(jump.Session.Run, jump.Session);
				Assert.Equal(bare.StateHash(), scene.Match.StateHash(), $"{yokai}: same match at the start");
				var rng = new SimRng(5);
				for (int t = 0; t < 400; t++)
				{
					var p1 = new FighterInput((InputBits)rng.Next(1 << 10));
					var p2 = new FighterInput((InputBits)rng.Next(1 << 10));
					scene.Step(p1, p2);
					bare.Step(p1, p2);
					Assert.Equal(bare.StateHash(), scene.Match.StateHash(), $"{yokai} tick {t}: the body doesn't change the sim");
				}
			}
			finally { runner.RemoveChild(scene); scene.Free(); }
		}
	}
}
