using System;
using System.Linq;
using YokaiFighters.Fight;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-175: <see cref="RunSession.LeaveNode"/>, sim only. Provisional defaults the designer confirms at review:
/// Ryo may leave a rest stop without choosing; the stop is then spent (the node is done, the next row is picked, R1);
/// leaving is a node boundary that saves like completing the node (M5). No heal, no coins, no shop (R6, R7).
/// </summary>
public static class LeaveNodeTests
{
	static string MapDir => ContentPaths.Data("map");

	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		try { body(); }
		finally { FightScene.Sources = saved; FightScene.Run = savedRun; }
	}

	static RunSession NewSession(uint seed = 5) => RunSession.Start(
		FightScene.LoadAbilityPool(), FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir), seed);

	static bool Leads(RunSession s, string from, string to) => from == to || s.Map.Graph.Next(from).Any(n => Leads(s, n, to));

	/// <summary>A run standing at <paramref name="nodeId"/> with 400 health and some coins; fights won, earlier stops passed.</summary>
	static RunSession AtNode(string nodeId, uint seed = 5)
	{
		var s = NewSession(seed);
		for (; ; )
		{
			string pick = s.Reachable().First(id => Leads(s, id, nodeId));
			Assert.True(s.Enter(pick).Ok, "entered " + pick);
			if (pick == nodeId) break;
			if (s.Node!.Type is "merchant" or "dojo") { Assert.True(s.CompleteNode().Ok, "a stop passed"); continue; }
			if (s.WinFight(400) is { } d) { var c = d.Offer.Cards[0]; Assert.True(d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null).Ok, "picked"); }
			Assert.True(s.CompleteNode().Ok, "completed " + pick);
		}
		s.Run.SetHealth(400);
		return s;
	}

	static string Slots(RunSession s) => string.Join("|", s.Run.Owned.Select(o => $"{o.Slot}:{o.Special.Data.Id}:{o.Special.Level}:{o.Special.Modifier?.Id}"));

	[Test]
	public static void Merchant_LeaveBeforeAChoice_SpendsTheStop_NoHealNoCoins_NextRowIsPicked() => OnFixtures(() =>
	{
		foreach (string stop in new[] { "r4-merchant", "r7-dojo" })
		{
			var s = AtNode(stop);
			int health = s.Run.Health, coins = s.Run.Coins; string slots = Slots(s);
			var completed = new System.Collections.Generic.List<string>();
			s.NodeCompleted += n => completed.Add(n.NodeId);
			Assert.True(s.LeaveNode().Ok, $"{stop}: left with no choice");
			Assert.True(s.Node == null && !s.Map.InNode && s.Merchant == null && s.Dojo == null, $"{stop}: no node entered after leaving");
			Assert.Equal(stop, string.Join(",", completed), $"{stop}: NodeCompleted once, the save point (M5)");
			Assert.True(s.Map.Visited.Last() == stop, $"{stop}: the stop counts as done (default 2)");
			Assert.Equal(string.Join(",", s.Map.Graph.Next(stop)), string.Join(",", s.Reachable()), $"{stop}: Ryo picks from the next row");
			Assert.True(s.Run.Health == health && s.Run.Coins == coins && Slots(s) == slots, $"{stop}: health, coins and slots as entered");
			Assert.True(!s.Enter(stop).Ok, $"{stop}: cannot re-enter a spent stop (R1)");
		}
	});

	[Test]
	public static void Leave_RefusedAfterAChoice_ChangesNothing() => OnFixtures(() =>
	{
		var heal = AtNode("r4-merchant");
		Assert.True(heal.Merchant!.TakeHeal().Ok, "heal");
		var shop = AtNode("r4-merchant");
		Assert.True(shop.Merchant!.Choose(MerchantChoice.Shop).Ok, "shop");
		var rest = AtNode("r7-dojo");
		Assert.True(rest.Dojo!.Rest().Ok, "rest");
		foreach (var (name, s) in new[] { ("heal", heal), ("shop", shop), ("rest", rest) })
		{
			ulong h = s.StateHash(); int health = s.Run.Health, coins = s.Run.Coins; string node = s.Node!.NodeId;
			int fired = 0; s.NodeCompleted += _ => fired++;
			Assert.True(!s.LeaveNode().Ok, $"{name}: refused once the choice is made");
			Assert.True(s.StateHash() == h && s.Node?.NodeId == node && s.Run.Health == health && s.Run.Coins == coins && fired == 0, $"{name}: nothing changed");
			Assert.True(s.CompleteNode().Ok, $"{name}: Continue still completes it");
		}
	});

	[Test]
	public static void Leave_RefusedOnAFightNode_WithNoNode_AndWhenOver() => OnFixtures(() =>
	{
		var s = NewSession();
		ulong h0 = s.StateHash();
		Assert.True(!s.LeaveNode().Ok && s.StateHash() == h0, "no node entered: refused");
		Assert.True(s.Enter(s.Reachable()[0]).Ok, "a duel entered");
		ulong h1 = s.StateHash();
		Assert.True(!s.LeaveNode().Ok && s.Node != null && s.StateHash() == h1, "a fight node cannot be left (R1, M5)");
		Assert.True(s.LoseFight().Ok && !s.LeaveNode().Ok, "the run is over: refused");
	});

	[Test]
	public static void Leave_HashIsCompletingTheStopWithNoChoice_Deterministic() => OnFixtures(() =>
	{
		foreach (string stop in new[] { "r4-merchant", "r7-dojo" })
		{
			var a = AtNode(stop, 9); var b = AtNode(stop, 9);
			Assert.Equal(a.StateHash(), b.StateHash(), $"{stop}: twins");
			Assert.True(a.LeaveNode().Ok && b.CompleteNode().Ok, $"{stop}: one leaves, one completes with no choice");
			Assert.Equal(b.StateHash(), a.StateHash(), $"{stop}: leaving changes the hash only as the node-state change does");
			Assert.Equal(string.Join(",", b.Map.Visited), string.Join(",", a.Map.Visited), $"{stop}: same walk");
		}
	});

	[Test]
	public static void Leave_SavesAtTheBoundary_ContinueResumesAtTheNextRowChoice_M5() => OnFixtures(() =>
	{
		foreach (string stop in new[] { "r4-merchant", "r7-dojo" })
		{
			var s = AtNode(stop);
			RunSave? saved = null;
			s.NodeCompleted += _ => saved = RunSave.Capture(s); // the boot flow's save point on a completed node
			int health = s.Run.Health, coins = s.Run.Coins; string slots = Slots(s);
			Assert.True(s.LeaveNode().Ok && saved != null, $"{stop}: left, saved");
			var pool = FightScene.LoadAbilityPool();
			// the run as saved (the file round trip is RestStopLeaveTests' boot-flow test, on a temp save file)
			var back = saved!.Restore(pool, FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir));
			Assert.True(!back.Map.InNode && back.Node == null, $"{stop}: Continue lands on the map, not in the stop");
			Assert.Equal(string.Join(",", s.Map.Graph.Next(stop)), string.Join(",", back.Reachable()), $"{stop}: at the next-row choice");
			Assert.True(back.Run.Health == health && back.Run.Coins == coins && Slots(back) == slots, $"{stop}: health, coins and slots as entered");
			Assert.Equal(s.StateHash(), back.StateHash(), $"{stop}: the restored run is the left run");
		}
	});
}
