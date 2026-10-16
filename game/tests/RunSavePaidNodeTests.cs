using System;
using System.IO;
using YokaiFighters.Fight;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-128: a run captured after a won node that offers no draft (a non-binding fight, the last node) and
/// restored keeps the node paid (<see cref="RunSession.NodePaid"/>), so finishing it after Continue pays its coins
/// once (R8) and heals once (R3). Pure: sessions and saves in memory, no scene, no file store.
/// </summary>
public static class RunSavePaidNodeTests
{
	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		try { body(); } finally { FightScene.Sources = saved; }
	}

	static MapGraph Map(string nodes, string routes)
	{
		string path = Path.Combine(Path.GetTempPath(), $"yok128-{Guid.NewGuid():N}.json");
		File.WriteAllText(path, $$"""{"kind":"run_map","id":"yok128","schema_version":1,"name":"t","status":"final","rules":[],"nodes":[{{nodes}}],"routes":[{{routes}}]}""");
		try { return new FileMapSource(path).Load(); } finally { File.Delete(path); }
	}

	/// <summary>Row 1 "a" → row 2 "b", both duels.</summary>
	static MapGraph TwoRows() => Map(
		"""{"id":"a","row":1,"type":"duel","yokai":"kitsune","label":"A"},{"id":"b","row":2,"type":"duel","yokai":"kitsune","label":"B"}""",
		"""{"from":"a","to":"b"}""");

	/// <summary>One duel, the last node: it binds (R3) and pays 25 (R8) but offers no draft (A7).</summary>
	static MapGraph Single() => Map("""{"id":"only","row":1,"type":"duel","yokai":"kitsune","label":"K"}""", "");

	/// <summary>A duel that pays 25 coins and does not bind: a won node with no draft that is not the last.</summary>
	static INodeSceneResolver NonBinding() => NodeSceneTable.Parse("""
	{ "kind": "node_scenes", "id": "t", "schema_version": 1,
	  "default_scene": "res://scenes/fight.tscn", "fallback_fighter": "kitsune",
	  "tiers": { "yokai": { "reaction_frames": 18 } },
	  "types": { "duel": { "tier": "yokai", "binds": false, "coins": 25 } },
	  "rows": {} }
	""");

	static INodeSceneResolver Binding() => NodeSceneTable.Default(ContentPaths.Data("map"));

	static RunSession Back(RunSession s, MapGraph g, INodeSceneResolver scenes) =>
		RunSave.Capture(s).Restore(FightScene.LoadAbilityPool(), g, scenes);

	[Test]
	public static void NonBindingNode_SavedAfterWin_FinishingPaysOnce() => OnFixtures(() =>
	{
		var g = TwoRows();
		var scenes = NonBinding();
		var s = RunSession.Start(FightScene.LoadAbilityPool(), g, scenes, 3);
		Assert.True(s.Enter("a").Ok, "entered a");
		Assert.True(s.WinFight(600) == null && s.Run.Coins == 25 && s.Run.Health == 600 && s.NodePaid, "setup: no draft, paid 25, no heal");

		var back = Back(s, g, scenes);
		Assert.True(back.Node?.NodeId == "a" && back.Draft == null && back.NodePaid && back.CoinsWon == 25, "Continue is at node a, already paid");
		Assert.True(back.StateHash() == s.StateHash() && back.Run.Coins == 25 && back.Run.Health == 600, "the save restores exactly");

		Assert.True(back.WinFight(400) == null, "the win reported again after Continue");
		Assert.True(back.Run.Coins == 25 && back.Run.Health == 600, $"R8: the node paid once (coins {back.Run.Coins}, health {back.Run.Health})");
		Assert.True(back.CompleteNode().Ok && !back.NodePaid && back.CoinsWon == 0, "finished; the next node starts unpaid");
		Assert.True(s.CompleteNode().Ok && back.StateHash() == s.StateHash(), "same run as the one never saved");
		Assert.True(back.Enter("b").Ok && !back.NodePaid && back.WinFight(600) == null && back.Run.Coins == 50, "the next node pays its own 25");
	});

	[Test]
	public static void LastBindingNode_SavedAfterWin_HealsAndPaysOnce() => OnFixtures(() =>
	{
		var g = Single();
		var scenes = Binding();
		var s = RunSession.Start(FightScene.LoadAbilityPool(), g, scenes, 3);
		s.Run.SetHealth(400);
		Assert.True(s.Enter("only").Ok, "entered");
		Assert.True(s.WinFight(300) == null && s.Run.Coins == 25 && s.Run.Health == 350, "setup: R3 heal, R8 25, no draft on the last node");

		var back = Back(s, g, scenes);
		Assert.True(back.Run.Health == 350 && back.Run.Coins == 25 && back.NodePaid, "restored after the win");
		Assert.True(back.WinFight(350) == null && back.Run.Health == 350 && back.Run.Coins == 25, $"R3 once, R8 once (health {back.Run.Health}, coins {back.Run.Coins})");
		Assert.True(back.CompleteNode().Ok && back.Ended is { Outcome: RunOutcome.Won }, "the run is won");
	});

	[Test]
	public static void LiveSession_SecondWinReport_ChangesNothing() => OnFixtures(() =>
	{
		var s = RunSession.Start(FightScene.LoadAbilityPool(), Single(), Binding(), 3);
		s.Run.SetHealth(400);
		s.Enter("only");
		s.WinFight(300);
		ulong h = s.StateHash();
		Assert.True(s.WinFight(900) == null && s.StateHash() == h && s.Run.Health == 350 && s.Run.Coins == 25, "R3, R8: counted once");
	});

	[Test]
	public static void SaveBoundaries_AreNeverPaid() => OnFixtures(() =>
	{
		var g = TwoRows();
		var scenes = NonBinding();
		var s = RunSession.Start(FightScene.LoadAbilityPool(), g, scenes, 3);
		Assert.True(!RunSave.Capture(s).NodePaid, "run start");
		RunSave? entered = null, completed = null;
		s.NodeEntered += _ => entered = RunSave.Capture(s);
		s.NodeCompleted += _ => completed = RunSave.Capture(s);
		s.Enter("a");
		ulong entry = s.StateHash();
		s.WinFight(600);
		Assert.True(s.CompleteNode().Ok, "completed a");
		Assert.True(!entered!.NodePaid && !completed!.NodePaid, "entry and completion saves are unpaid");
		var atEntry = entered.Restore(FightScene.LoadAbilityPool(), g, scenes);
		Assert.True(atEntry.StateHash() == entry && atEntry.Run.Coins == 0 && !atEntry.NodePaid, "M5: the entry save restarts the node unpaid");
		Assert.True(atEntry.WinFight(600) == null && atEntry.Run.Coins == 25, "and its win pays once");
		var atEnd = completed.Restore(FightScene.LoadAbilityPool(), g, scenes);
		Assert.True(atEnd.StateHash() == s.StateHash() && atEnd.Run.Coins == 25, "the completed node's pay is kept");
	});

	[Test]
	public static void PaidFlag_WithNoNodeEntered_IsRefused() => OnFixtures(() =>
	{
		var g = TwoRows();
		var scenes = NonBinding();
		var s = RunSession.Start(FightScene.LoadAbilityPool(), g, scenes, 3);
		var bad = RunSave.Capture(s) with { NodePaid = true };
		bool refused = false;
		try { bad.Restore(FightScene.LoadAbilityPool(), g, scenes); } catch (FormatException) { refused = true; }
		Assert.True(refused, "R8: a paid node needs an entered node");
	});

	[Test]
	public static void DraftNode_StillRefusedWhileTheOfferIsOpen() => OnFixtures(() =>
	{
		var g = TwoRows();
		var s = RunSession.Start(FightScene.LoadAbilityPool(), g, Binding(), 3);
		s.Enter("a");
		var draft = s.WinFight(600);
		Assert.True(draft is not null, "a binding node with a next row offers a draft (A7)");
		bool refused = false;
		try { RunSave.Capture(s); } catch (InvalidOperationException) { refused = true; }
		Assert.True(refused, "an open offer is not saved");
		var card = draft!.Offer.Cards[0];
		Assert.True(draft.Pick(0, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, "picked");
		refused = false;
		try { RunSave.Capture(s); } catch (InvalidOperationException) { refused = true; }
		Assert.True(refused, "a picked offer before completion is still not saved");
	});
}
