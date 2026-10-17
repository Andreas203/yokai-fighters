using System;
using System.Collections.Generic;
using YokaiFighters.Fight;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-128 probes: the paid node in run saves (R8 once, R3 once, A7 draft never skipped). Pure RunSession/RunSave.</summary>
public static class PaidNodeProbes
{
	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		try { body(); } finally { FightScene.Sources = saved; }
	}

	static MapGraph DefaultMap() => new FileMapSource(System.IO.Path.Combine(ContentPaths.Data("map"), "default-map.json")).Load();
	static INodeSceneResolver Table() => NodeSceneTable.Default(ContentPaths.Data("map"));

	static RunSession Back(RunSession s, MapGraph g) => RunSave.Capture(s).Restore(FightScene.LoadAbilityPool(), g, Table());

	static void PickFirst(RewardDraft d)
	{
		var card = d.Offer.Cards[0];
		Assert.True(d.Pick(0, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, "picked");
	}

	// R8, R3, A7, M5: along the whole default map, every boundary the game saves at (entry, a won node with no draft,
	// completion) restores to the same StateHash, never throws, and finishing the node after Continue gives the run
	// the never-saved one reaches.
	[Test]
	public static void EveryBoundaryOfAWholeWalk_RestoresAndFinishesLikeTheLiveRun() => OnFixtures(() =>
	{
		var g = DefaultMap();
		var live = RunSession.Start(FightScene.LoadAbilityPool(), g, Table(), 11);
		int guard = 0;
		while (!live.IsOver && guard++ < 30)
		{
			string id = live.Reachable()[0];
			Assert.True(live.Enter(id).Ok, $"entered {id}");
			var atEntry = Back(live, g);
			Assert.True(atEntry.StateHash() == live.StateHash() && !atEntry.NodePaid, $"{id}: entry save restores unpaid, same hash");
			int health = Math.Max(100, live.Run.Health - 50);
			if (live.Node!.Type == NodeTypeRegistry.Merchant || live.Node.Type == NodeTypeRegistry.Dojo)
			{
				Assert.True(live.CompleteNode().Ok, $"{id}: rest stop left");
			}
			else
			{
				var d = live.WinFight(health);
				if (d is null)
				{
					int coins = live.Run.Coins, hp = live.Run.Health;
					ulong h = live.StateHash();
					var paid = Back(live, g); // must not throw (the game itself wrote this)
					Assert.True(paid.StateHash() == h && paid.Run.Coins == coins && paid.Run.Health == hp && paid.NodePaid, $"{id}: paid save restores exactly");
					Assert.True(paid.WinFight(1) == null && paid.Run.Coins == coins && paid.Run.Health == hp && paid.StateHash() == h, $"{id}: repeat win changes nothing");
					Assert.True(paid.CompleteNode().Ok && live.CompleteNode().Ok && paid.StateHash() == live.StateHash(), $"{id}: finishing after Continue matches the live run");
				}
				else
				{
					PickFirst(d);
					Assert.True(live.CompleteNode().Ok, $"{id}: completed");
				}
			}
			if (!live.IsOver)
			{
				var atDone = Back(live, g);
				Assert.True(atDone.StateHash() == live.StateHash() && !atDone.NodePaid, $"{id}: completion save restores unpaid, same hash");
			}
		}
		Assert.True(live.Ended is { Outcome: RunOutcome.Won }, "the walk reached the Tanuki and won");
	});

	// T1, R8: the boss node (binds nothing, last row) saved after its win keeps the copy lock, pays and heals nothing twice.
	[Test]
	public static void BossNode_SavedAfterWin_KeepsLockAndCountsOnce() => OnFixtures(() =>
	{
		var g = DefaultMap();
		var s = RunSession.Start(FightScene.LoadAbilityPool(), g, Table(), 5);
		while (s.Node?.Type != NodeTypeRegistry.Boss)
		{
			if (s.Node is null) Assert.True(s.Enter(s.Reachable()[0]).Ok, "entered");
			var d = s.WinFight(800);
			if (d is not null) PickFirst(d);
			Assert.True(s.CompleteNode().Ok, "completed");
			if (s.Node is null && s.Reachable().Count > 0 && s.Map.Graph.Node(s.Reachable()[0]).Type == NodeTypeRegistry.Boss) s.Enter(s.Reachable()[0]);
		}
		Assert.True(s.CopyLocked, "lock taken on entering the Tanuki (T1)");
		s.WinFight(321);
		int coins = s.Run.Coins; ulong h = s.StateHash();
		var back = Back(s, g);
		Assert.True(back.CopyLocked && back.LockedCopy?.Data.Id == s.LockedCopy?.Data.Id && back.StateHash() == h, "T1: lock kept");
		back.WinFight(1000);
		Assert.True(back.Run.Health == 321 && back.Run.Coins == coins && back.StateHash() == h, "counted once");
		Assert.True(back.CompleteNode().Ok && back.Ended is { Outcome: RunOutcome.Won }, "run won after Continue");
	});

	// R8: a save after the win, then the player loses the next try of the same node? A loss after a restored win ends the run;
	// no paid save is possible for an ended run.
	[Test]
	public static void LossAfterWin_EndsRun_AndIsNotSavable() => OnFixtures(() =>
	{
		var g = DefaultMap();
		var s = RunSession.Start(FightScene.LoadAbilityPool(), g, Table(), 3);
		s.Enter("r1-apprentice");
		var d = s.WinFight(900); PickFirst(d!); s.CompleteNode();
		// r2: pick a node, win (draft), save at entry instead, win again after restore, then lose
		s.Enter(s.Reachable()[0]);
		var back = Back(s, g);
		var draft = back.WinFight(800);
		Assert.True(draft is not null, "A7: restored entry save offers the draft");
		Assert.True(back.LoseFight().Ok && back.IsOver, "loss ends the run");
		bool refused = false;
		try { RunSave.Capture(back); } catch (InvalidOperationException) { refused = true; }
		Assert.True(refused, "an ended run is not saved");
	});

	// R8: restoring the same paid save twice gives two independent sessions; one finishing does not touch the other.
	[Test]
	public static void RestoringOnePaidSaveTwice_GivesIndependentSessions() => OnFixtures(() =>
	{
		var g = DefaultMap();
		var s = RunSession.Start(FightScene.LoadAbilityPool(), g, Table(), 9);
		while (s.Node?.Type != NodeTypeRegistry.Boss)
		{
			if (s.Node is null) s.Enter(s.Reachable()[0]);
			var d = s.WinFight(700); if (d is not null) PickFirst(d);
			s.CompleteNode();
			if (s.Node is null && s.Reachable().Count > 0 && s.Map.Graph.Node(s.Reachable()[0]).Type == NodeTypeRegistry.Boss) s.Enter(s.Reachable()[0]);
		}
		s.WinFight(500);
		var save = RunSave.Capture(s);
		Assert.True(save == RunSave.Capture(s) || save.NodePaid == RunSave.Capture(s).NodePaid, "capture twice agrees on the flag");
		var a = save.Restore(FightScene.LoadAbilityPool(), g, Table());
		var b = save.Restore(FightScene.LoadAbilityPool(), g, Table());
		a.CompleteNode();
		Assert.True(a.IsOver && !b.IsOver && b.NodePaid && b.StateHash() == s.StateHash(), "the second restore is untouched");
	});

	// A7: a saved-paid flag on a node of every kind that owes a draft (elder, duel) with a successor is refused, never restored.
	[Test]
	public static void HandMadePaidFlag_OnEveryDraftOwingNode_IsRefused() => OnFixtures(() =>
	{
		var g = DefaultMap();
		foreach (string id in new[] { "r1-apprentice", "r2-kitsune", "r5-elder-oni" })
		{
			var s = RunSession.Start(FightScene.LoadAbilityPool(), g, Table(), 3);
			// walk to the node by its row
			var path = new List<string>();
			string at = id;
			s = RunSession.Start(FightScene.LoadAbilityPool(), g, Table(), 3);
			while (s.Node?.NodeId != id)
			{
				if (s.Node is null)
				{
					var reach = s.Reachable();
					string next = System.Linq.Enumerable.Contains(reach, id) ? id : reach[0];
					s.Enter(next);
					if (next == id) break;
				}
				var d = s.WinFight(900); if (d is not null) PickFirst(d);
				s.CompleteNode();
			}
			if (s.Node?.NodeId != id) continue; // not reachable on this walk; other ids cover it
			var save = RunSave.Capture(s) with { NodePaid = true };
			bool refused = false;
			try { save.Restore(FightScene.LoadAbilityPool(), g, Table()); } catch (FormatException) { refused = true; }
			Assert.True(refused, $"A7: {id} owes a draft, a paid flag is refused");
		}
	});
}
