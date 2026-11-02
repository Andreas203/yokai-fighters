using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Story;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-216, S3: <see cref="FightScene.LoseCardFor"/> on the save's first loss shows <c>first-loss</c> (the Tanuki
/// names the move it would have stolen); every later loss the later-loss card (<see cref="StoryLibrary.LaterLossCard"/>,
/// YOK-212). The save side (the flag set at the run end, kept across a reload, no hash change) is in
/// <c>SaveTests.FirstLoss_*</c>: only SaveTests may boot GameFlow on a file (SaveProbes.Isolation_*).
/// </summary>
public static class FirstLossTests
{
	static NodeContext At(string yokai, int row) =>
		NodeSceneTable.Default(ContentPaths.Data("map")).Resolve(new MapNodeData("n", row, "duel", yokai, false)).Context;

	[Test]
	public static void LoseCardFor_GatesOnTheFirstLoss()
	{
		var lib = FightScene.LoadStory();
		Assert.True(lib.Has(StoryLibrary.FirstLossId), "data/story has first-loss");
		var node = At("oni", 3);
		var (_, first) = FightScene.LoseCardFor(lib, node, node.Fighter, firstLoss: true, move: "Foxfire");
		Assert.Equal(lib[StoryLibrary.FirstLossId].FillText("Oni", "Foxfire"), first, "S3: first loss = first-loss, {move} = the copy target");
		var (_, later) = FightScene.LoseCardFor(lib, node, node.Fighter);
		Assert.Equal(lib.LaterLossCard("oni", 3).FillText(yokai: "Oni"), later, "later loss = the YOK-212 later-loss card");
		Assert.True(first != later, "the two differ");
		var (_, noMove) = FightScene.LoseCardFor(lib, node, node.Fighter, firstLoss: true);
		Assert.Equal(lib[StoryLibrary.FirstLossId].FillText("Oni", FightScene.FallbackMove), noMove, "no copy target: the fallback word, never '{move}'");
		Assert.True(!noMove.Contains('{'), "filled");
	}

	[Test]
	public static void LoseCardFor_NoFirstLossCard_FallsBackToTheLaterLossCard()
	{
		var lib = FightScene.LoadStory();
		var without = new StoryLibrary(System.Linq.Enumerable.Where(lib.Cards, c => c.Id != StoryLibrary.FirstLossId));
		var node = At("kitsune", 2);
		Assert.Equal(FightScene.LoseCardFor(without, node, node.Fighter).Text, FightScene.LoseCardFor(without, node, node.Fighter, true, "Foxfire").Text,
			"a library without first-loss shows the later-loss card, never throws");
	}
}
