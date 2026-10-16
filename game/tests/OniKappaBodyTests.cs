using System.Linq;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;

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
	public static void OniAndKappa_LoadAndShareTheKitsuneClips()
	{
		foreach (var set in new[] { FighterAnimSet.Oni, FighterAnimSet.Kappa })
		{
			Assert.True(ResourceLoader.Exists(set.ModelPath), $"{set.Fighter}: rigged model {set.ModelPath}");
			Assert.True(ReferenceEquals(set.Clips, FighterAnimSet.Kitsune.Clips), $"{set.Fighter}: the Kitsune's shared clips");
			var model = FighterModel.Create(set, Cat);
			Assert.True(model != null, $"{set.Fighter}: model scene sets up on the catalog");
			model!.Free();
		}
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
