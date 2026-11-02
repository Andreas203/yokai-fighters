using System.Linq;
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

	[Test]
	public static void CopyMoveOf_TheLockedCopyBeatsTheLiveTarget()
	{
		Assert.True(FightScene.CopyMoveOf(null) is null, "no run: no move");
		string md = ContentPaths.Data("map");
		var s = RunSession.Start(FightScene.LoadAbilityPool(), FileMapSource.Default(md).Load(), NodeSceneTable.Default(md), 5);
		var target = CopyRule.Target(s.Run);
		Assert.True(target is not null && !s.CopyLocked, "a fresh run has a live target and no lock");
		Assert.Equal(target!.Data.Name, FightScene.CopyMoveOf(s), "unlocked: the live target (CopyRule.Target, T2)");
		// Walk to the Tanuki's node and enter it (T1: the lock point), then restore with a copy other than the live target.
		while (s.Map.Graph.Node(s.Reachable()[0]).Type != NodeTypeRegistry.Boss)
		{
			s.Enter(s.Reachable()[0]);
			if (s.Node!.Type is not ("merchant" or "dojo") && s.WinFight(840) is { } d)
			{
				var c = d.Offer.Cards[0];
				d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null);
			}
			s.CompleteNode();
		}
		Assert.True(s.Enter(s.Reachable()[0]).Ok && s.CopyLocked, "entered the Tanuki's node: locked");
		var live = CopyRule.Target(s.Run)!;
		Assert.Equal(s.LockedCopy!.Data.Name, FightScene.CopyMoveOf(s), "locked: the locked copy");
		var (slot, owned) = s.Run.Owned.First(o => o.Special.Data.Name != live.Data.Name); // another special Ryo holds (T2: a copy sits in its slot)
		var other = owned.Data;
		var locked = RunSession.Restore(s.Run, s.Pool, s.Scenes, s.Seed, true, new CopyTarget(slot, other, 1));
		Assert.Equal(other.Name, FightScene.CopyMoveOf(locked), "locked: the locked copy, though the live target is another (T1)");
		var lockedOnNothing = RunSession.Restore(s.Run, s.Pool, s.Scenes, s.Seed, true, null);
		Assert.True(FightScene.CopyMoveOf(lockedOnNothing) is null, "locked on nothing: no move, never the live target (T1)");
	}
}
