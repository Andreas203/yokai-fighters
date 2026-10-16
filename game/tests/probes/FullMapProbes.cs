using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Fight;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-124 probes: random free-travel / select / complete / save-restore sequences on the pure sim.</summary>
public static class FullMapProbes
{
	static string MapDir => ContentPaths.Data("map");
	static MapGraph Map() => FileMapSource.Default(MapDir).Load();
	static RunSession New(uint seed) => RunSession.Start(FightScene.LoadAbilityPool(), Map(), NodeSceneTable.Default(MapDir), seed);

	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		try { body(); } finally { FightScene.Sources = saved; FightScene.Run = savedRun; }
	}

	static void Clear(RunSession s)
	{
		if (s.WinFight(800) is { } d) { var c = d.Offer.Cards[0]; Assert.True(d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null).Ok, "pick"); }
		Assert.True(s.CompleteNode().Ok, "complete");
	}

	// rules: R1 (routes), R3 (heal once per binding node), R6 (merchant once), R8 (coins once), T1 (lock on boss entry), AM3 (save mid-node)
	[Test]
	public static void RandomFreeTravelWalks_NeverReenter_NeverStick_LockOnBoss_SaveRestoreAgrees() => OnFixtures(() =>
	{
		var pool = FightScene.LoadAbilityPool();
		for (uint seed = 1; seed <= 150; seed++)
		{
			var rng = new Random((int)seed);
			var s = New(seed);
			var graph = s.Map.Graph;
			var all = graph.Nodes.Select(n => n.Id).ToArray();
			var entered = new HashSet<string>();
			string log = $"seed {seed}: ";
			for (int op = 0; op < 80 && !s.IsOver; op++)
			{
				int k = rng.Next(5);
				if (k == 0) { s.Map.FreeTravel = !s.Map.FreeTravel; log += "T "; }
				else if (k == 1 && s.Node is null)
				{
					string id = all[rng.Next(all.Length)];
					var reach = s.Reachable();
					ulong h = s.StateHash(); string? cur = s.Map.Current; int coins = s.Run.Coins;
					var r = s.Enter(id);
					log += "E:" + id + " ";
					Assert.True(r.Ok == reach.Contains(id), log + "enter ok iff reachable");
					if (!r.Ok) Assert.True(h == s.StateHash() && cur == s.Map.Current && coins == s.Run.Coins, log + "refused changes nothing");
					else
					{
						Assert.True(entered.Add(id), log + "node re-entered: " + id);
						if (graph.Node(id).Type == "boss") Assert.True(s.CopyLocked, log + "T1 lock on boss entry");
					}
				}
				else if (k == 2 && s.Node is not null)
				{
					int before = s.Run.Coins; int cost = s.Node.Coins;
					var id = s.Node.NodeId;
					if (s.Merchant is not null || s.Dojo is not null) { Assert.True(s.CompleteNode().Ok, log + "leave"); }
					else
					{
						s.WinFight(800);
						Assert.True(s.Run.Coins == before + cost, log + $"R8 coins once ({before}+{cost} got {s.Run.Coins})");
						if (s.Draft is { } d) { var c = d.Offer.Cards[0]; d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null); }
						Assert.True(s.CompleteNode().Ok, log + "complete");
					}
					log += "C ";
				}
				else if (k == 3 && s.Draft is null)
				{
					var save = RunSave.Capture(s);
					var back = save;
					log += "S ";
					if (save.OffRoute)
					{
						bool threw = false;
						try { back!.Restore(pool, graph, NodeSceneTable.Default(MapDir)); } catch (FormatException) { threw = true; }
						Assert.True(threw, log + "player slot refuses off_route");
					}
					bool wasFree = s.Map.FreeTravel;
					ulong h = s.StateHash();
					var again = back!.Restore(pool, graph, NodeSceneTable.Default(MapDir), allowOffRoute: true);
					Assert.True(again.StateHash() == h && again.Map.Current == s.Map.Current && again.Map.InNode == s.Map.InNode && again.Map.OffRoute == s.Map.OffRoute, log + "dev restore agrees");
					Assert.True(again.CopyLocked == s.CopyLocked, log + "lock agrees");
					Assert.True(again.Reachable().Count > 0 || again.Node is not null, log + "restored not stuck");
					if (!again.Map.OffRoute) again.Map.FreeTravel = wasFree;
					again.Map.FreeTravel = wasFree;
					s = again;
				}
				if (!s.IsOver && s.Node is null) Assert.True(s.Reachable().Count > 0, log + "stuck: nothing reachable");
				if (!s.IsOver && s.Node is null) Assert.True(s.Reachable().All(id => !s.Map.IsVisited(id)), log + "walked node reachable");
			}
		}
	});

	[Test]
	public static void RouteFollowingRun_WithFreeTravelToggling_HasMainsHashAndSaveBytes() => OnFixtures(() =>
	{
		for (uint seed = 1; seed <= 40; seed++)
		{
			var a = New(seed); var b = New(seed);
			var rng = new Random((int)seed);
			while (!a.IsOver)
			{
				b.Map.FreeTravel = rng.Next(2) == 0;
				string pick = a.Reachable()[^1];
				Assert.True(a.Enter(pick).Ok && b.Enter(pick).Ok, "enter");
				b.Map.FreeTravel = rng.Next(2) == 0;
				string ta = System.Text.Json.JsonSerializer.Serialize(RunSave.Capture(a));
				string tb = System.Text.Json.JsonSerializer.Serialize(RunSave.Capture(b));
				Assert.True(ta == tb && a.StateHash() == b.StateHash(), $"seed {seed}: bytes/hash differ at {pick}");
				Clear(a); Clear(b);
			}
			Assert.True(b.IsOver && a.StateHash() == b.StateHash(), "end");
		}
	});

	[Test]
	public static void SameFreeTravelChoicesTwice_SameHash_DifferentOrder_DifferentHash() => OnFixtures(() =>
	{
		RunSession Walk(string[] ids) { var s = New(7); s.Map.FreeTravel = true; foreach (var id in ids) { Assert.True(s.Enter(id).Ok, id); Clear(s); } return s; }
		var one = new[] { "r4-merchant", "r2-oni" }; var two = new[] { "r2-oni", "r4-merchant" };
		Assert.True(Walk(one).StateHash() == Walk(one).StateHash(), "deterministic");
		Assert.True(Walk(one).StateHash() != Walk(two).StateHash(), "order is state");
	});
}
