using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-124 dev mode, full map: free travel on the run map (every unwalked node selectable, one at a time, routes back
/// when off), the node's real rules on arrival (T1 copy lock, the elder fight, the merchant's shop, the dojo), the
/// off-route walk in the dev slot only (M5), and the dev menu's Map page on the boot flow (travel, reveal, refused
/// mid-fight). Every test puts <see cref="DevMode"/> and <see cref="DevMap"/> back.
/// </summary>
public static class DevMapTests
{
	static string MapDir => ContentPaths.Data("map");
	static NodeSceneTable Table() => NodeSceneTable.Default(MapDir);
	static MapGraph DefaultMap() => FileMapSource.Default(MapDir).Load();
	static DevShell Shell => DevShell.Instance ?? throw new AssertFailed("the DevShell autoload is missing");
	static void Press(Button b) => b.EmitSignal(BaseButton.SignalName.Pressed);

	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		try { body(); }
		finally { FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe; }
	}

	static RunSession NewSession(uint seed = 124) => RunSession.Start(FightScene.LoadAbilityPool(), DefaultMap(), Table(), seed);

	/// <summary>Wins the entered node, picks its first card when it offers one, and completes it.</summary>
	static void Clear(RunSession s, int health = 800)
	{
		if (s.WinFight(health) is { } draft)
		{
			var card = draft.Offer.Cards[0];
			Assert.True(draft.Pick(0, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, "picked");
		}
		Assert.True(s.CompleteNode().Ok, "node completed");
	}

	// ---- free travel on the map (sim, no scene) ----

	[Test]
	public static void FreeTravel_EveryUnwalkedNodeSelectable_OneAtATime_OffRestoresTheRoutes() => OnFixtures(() =>
	{
		var s = NewSession();
		var graph = s.Map.Graph;
		Assert.True(s.Reachable().SequenceEqual(graph.StartNodes), "off: the start nodes only (R1)");
		Assert.True(!s.Enter("r5-elder-oni").Ok && s.Map.Current == null, "off: a jump is refused and changes nothing");
		ulong onRouteHash = s.StateHash();

		s.Map.FreeTravel = true;
		Assert.True(s.Reachable().SequenceEqual(graph.Nodes.Select(n => n.Id)), "on: every node is selectable");
		Assert.Equal(onRouteHash, s.StateHash(), "the toggle alone is not run state");
		Assert.True(s.Enter("r5-elder-oni").Ok && s.Map.OffRoute, "on: row 5 entered from the start, the walk is off-route");
		Assert.True(s.Reachable().Count == 0 && !s.Enter("r2-oni").Ok, "still one node at a time: nothing selectable inside a node");
		Clear(s);
		Assert.True(!s.Reachable().Contains("r5-elder-oni") && s.Reachable().Count == graph.Nodes.Count - 1, "every node but the walked one");
		Assert.True(s.Enter("r2-oni").Ok, "backwards too: row 2 after row 5");
		Clear(s);

		s.Map.FreeTravel = false;
		Assert.True(s.Reachable().SequenceEqual(graph.Next("r2-oni")), "off again: the routes out of the node Ryo is on");
		Assert.True(!s.Enter("boss-tanuki").Ok && s.Map.Current == "r2-oni", "off: the boss is refused again, nothing changes");
		Assert.True(s.Enter(graph.Next("r2-oni")[0]).Ok, "a route step works as before");

		var plain = NewSession();
		plain.Map.FreeTravel = true;
		Assert.True(plain.Enter("r1-apprentice").Ok && !plain.Map.OffRoute, "free travel along a route is not off-route");
		Assert.True(s.StateHash() != plain.StateHash(), "an off-route walk hashes as its own state");
	});

	[Test]
	public static void ToggleOffAfterAnOffRouteWalk_NeverOffersAWalkedNode_AndNeverSticks() => OnFixtures(() =>
	{
		// Routes back into walked nodes: r2-oni → r3-oni after r3-oni was walked first.
		var s = NewSession();
		var graph = s.Map.Graph;
		Assert.True(graph.HasRoute("r2-oni", "r3-oni") && graph.Next("r2-oni").Count > 1, "the default map has the routes this test needs");
		s.Map.FreeTravel = true;
		foreach (string id in new[] { "r1-apprentice", "r3-oni", "r2-oni" }) { Assert.True(s.Enter(id).Ok, "walked " + id); Clear(s); }
		s.Map.FreeTravel = false;
		var open = s.Reachable();
		Assert.True(open.Count > 0 && !open.Contains("r3-oni") && open.SequenceEqual(graph.Next("r2-oni").Where(id => !s.Map.IsVisited(id))), "the routes minus walked nodes: " + string.Join(", ", open));
		int coins = s.Run.Coins;
		Assert.True(!s.Enter("r3-oni").Ok && s.Map.Current == "r2-oni" && s.Run.Coins == coins, "a walked node is refused, nothing paid again (R8)");

		// The critic's repro: r1 → r4-merchant → r3-oni, toggle off: the only route out leads back to the merchant.
		var d = NewSession();
		d.Map.FreeTravel = true;
		foreach (string id in new[] { "r1-apprentice", "r4-merchant", "r3-oni" }) { Assert.True(d.Enter(id).Ok, "walked " + id); Clear(d); }
		d.Map.FreeTravel = false;
		Assert.True(graph.Next("r3-oni").All(d.Map.IsVisited), "every route out of r3-oni is walked");
		var fallback = d.Reachable();
		Assert.True(!fallback.Contains("r4-merchant") && fallback.SequenceEqual(graph.Nodes.Select(n => n.Id).Where(id => !d.Map.IsVisited(id))), "dead end: every unwalked node opens: " + string.Join(", ", fallback));
		Assert.True(!d.Enter("r4-merchant").Ok && d.Merchant == null, "R6: the merchant is not re-run");
		Assert.True(d.Enter("r5-nine-tails").Ok, "the run goes on");

		// A walk on the routes is unchanged by any of this: same reachable lists and hash as before free travel existed.
		var a = NewSession();
		var b = NewSession();
		b.Map.FreeTravel = true;
		b.Map.FreeTravel = false;
		foreach (var walk in new[] { a, b })
			while (!walk.IsOver)
			{
				var r = walk.Reachable();
				Assert.True(walk.Map.Current is null ? r.SequenceEqual(graph.StartNodes) : r.SequenceEqual(graph.Next(walk.Map.Current)), "routes unchanged");
				Assert.True(walk.Enter(r[^1]).Ok, "route step");
				Clear(walk);
			}
		Assert.True(!b.Map.OffRoute && a.StateHash() == b.StateHash(), "same hash");
	});

	[Test]
	public static void JumpToTheBoss_LocksTheCopy_AsTheRunStandsOnEntry() => OnFixtures(() =>
	{
		var s = NewSession();
		s.Map.FreeTravel = true;
		CopyTarget? seen = null;
		bool lockedWhenRaised = false;
		s.NodeEntered += n => { lockedWhenRaised = s.CopyLocked; seen = s.LockedCopy; };
		var expected = CopyRule.Target(s.Run);
		Assert.True(!s.CopyLocked && s.Enter("boss-tanuki").Ok, "the boss entered straight from the start");
		Assert.True(s.CopyLocked && lockedWhenRaised, "T1: locked on entering, before NodeEntered");
		Assert.True(s.LockedCopy is { } c && expected is { } e && c.Data.Id == e.Data.Id && c.Level == e.Level && c.Slot == e.Slot,
			$"T1: the copy is CopyRule.Target of the run on entry ({s.LockedCopy?.Data.Id})");
		Assert.True(ReferenceEquals(seen, s.LockedCopy), "listeners read the lock");
		Assert.True(new SessionHudRunView(s).InTanukiFight, "the HUD sees the Tanuki fight");
		Assert.True(s.WinFight(500) is null && s.CompleteNode().Ok && s.Ended is { Won: true, RowReached: 9 }, "winning the boss wins the run");
	});

	[Test]
	public static void JumpToAnElder_RunsTheElderNode_WithItsTwistAndRowRules() => OnFixtures(() =>
	{
		var s = NewSession();
		s.Map.FreeTravel = true;
		Assert.True(s.Enter("r8-elder-kappa").Ok, "row 8 elder entered from the start");
		var node = s.Node!;
		var direct = Table().Resolve(s.Map.Graph.Node("r8-elder-kappa")).Context;
		Assert.True(node.Elder && node.Type == NodeTypeRegistry.ElderDuel, "Y3: an elder node");
		Assert.True(new SessionHudRunView(s).ElderFight, "the fight's HUD shows the elder twist strip");
		Assert.True(node.Tier == direct.Tier && node.ReactionFrames == direct.ReactionFrames && node.Tier == "elder", $"Y4: the elder tier ({node.Tier}, {node.ReactionFrames})");
		Assert.True(s.WinFight(600) is { } draft && draft.Offer.Cards.Count > 0, "the elder binds and offers its draft");
		Assert.Equal(0, s.CoinsWon, "R8: the row-8 elder pays no coins, as in a walked run");
		Assert.True(!s.CopyLocked, "T1: no lock before the boss");
	});

	[Test]
	public static void JumpToTheMerchant_OpensItsShop_AndToTheDojo_ItsRest() => OnFixtures(() =>
	{
		var s = NewSession();
		s.Map.FreeTravel = true;
		Assert.True(s.Enter("r7-merchant").Ok && s.Merchant is { Open: true } m, "the merchant stop is open");
		Assert.True(!s.Merchant!.Shop.Open && s.Merchant.Choose(MerchantChoice.Shop).Ok && s.Merchant.Shop.Open, "R6: choosing the shop opens it");
		Assert.True(!s.Merchant.CanHeal, "R6: shop or heal, not both");
		Assert.True(s.CompleteNode().Ok && s.Merchant == null, "left the stop");
		Assert.True(s.Enter("r7-dojo").Ok && s.Dojo is { CanRest: true } && s.Dojo.Rest().Ok, "R7: the dojo's rest");
	});

	// (the off-route walk in the save file: SaveTests.OffRouteWalk_ComesBackOnlyInADevSession, the only test files that may name the store)

	// ---- the dev menu's Map page on the boot flow ----

	static void WithBoot(Node runner, Action<GameFlow> body)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-devmap-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		MenuSettings.Reset();
		DevMode.Configure(new DevModeState(true, true));
		DevMap.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = 124;
		flow.SaveFile = Path.Combine(dir, "progress.json");
		flow.ConfigureFight = f => { f.ExternalDrive = true; f.PauseEnabled = true; f.HeldBitsSource = () => InputBits.None; };
		var stage = new SubViewport { Size = new Vector2I(1920, 1080), Name = "DevMapStage" };
		runner.AddChild(stage);
		try
		{
			stage.AddChild(flow);
			Press(flow.Title.StartButton);
			Press(flow.ControlSelect.Start);
			Assert.Equal(Screen.Map, flow.Router.Current, "on the map");
			body(flow);
		}
		finally
		{
			Shell.Menu.Close();
			runner.RemoveChild(stage);
			stage.QueueFree();
			DevMap.Reset();
			DevMode.ResetToLaunch();
			MenuSettings.Reset();
			FightScene.Sources = saved;
			FightScene.Run = savedRun;
			FightScene.Opponent = savedFoe;
			try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
		}
	}

	static DevMapPage OpenMapPage(GameFlow flow)
	{
		Assert.True(flow.OpenDevMenu() && Shell.Menu.ShowSection(DevMenu.MapId), "the dev menu's Map page opens on the map");
		return Shell.Menu.Page as DevMapPage ?? throw new AssertFailed("the Map section is the DevMapPage");
	}

	static int Selectable(GameFlow flow) => flow.Map.View!.Nodes.Count(n => n.State == MapNodeState.Reachable);

	[Test]
	public static void MapPage_FullMap_MakesEveryNodeSelectableOnTheMapScreen_AndTravelLocksTheBossCopy(Node runner) => WithBoot(runner, flow =>
	{
		var session = flow.Session!;
		var page = OpenMapPage(flow);
		Assert.Equal(1, Selectable(flow), "toggle off: the map screen offers row 1 only");
		Assert.True(page.TravelButton("boss-tanuki") is { Disabled: true } && page.TravelButton("r1-apprentice") is { Disabled: false }, "off: travel follows the routes");
		page.FullMapToggle.ButtonPressed = true;
		Assert.True(DevMap.FullMapUnlocked && session.Map.FreeTravel, "on: the run's map is opened");
		Assert.Equal(session.Map.Graph.Nodes.Count, Selectable(flow), "on: every node is selectable on the map screen itself");
		var boss = page.TravelButton("boss-tanuki")!;
		Assert.True(!boss.Disabled, "the boss can be travelled to");
		Press(boss);
		Assert.True(flow.Router.Current == Screen.Fight && flow.Fight is { } f && f.Session == session && session.Node?.NodeId == "boss-tanuki", "the boss fight runs through the normal node flow");
		Assert.True(session.CopyLocked && session.LockedCopy != null, "T1: the copy locked on entry");
		Assert.True(!Shell.Menu.IsOpen, "the menu closed on travel");
		var saved = flow.Saved.Run!;
		Assert.True(saved.Current == "boss-tanuki" && saved.OffRoute && saved.CopyLocked == true, "M5: saved on entry, in the dev slot");
		Assert.True(flow.Saves!.Path.EndsWith(".dev.json") && File.ReadAllText(flow.Saves.Path).Contains("\"off_route\""), "written to the dev slot: " + flow.Saves.Path);
	});

	[Test]
	public static void MapPage_TravelToTheMerchant_OpensTheStop_AndToAnElder_ItsFight(Node runner) => WithBoot(runner, flow =>
	{
		var session = flow.Session!;
		var page = OpenMapPage(flow);
		page.FullMapToggle.ButtonPressed = true;
		Press(page.TravelButton("r4-merchant")!);
		Assert.True(flow.Router.Current == Screen.Fight && flow.NodeScene is RestStopScreen && session.Merchant is { Open: true }, "the merchant's rest stop runs");
		Assert.True(session.Merchant!.Choose(MerchantChoice.Shop).Ok && session.Merchant.Shop.Open, "R6: its shop opens");
		Assert.True(session.CompleteNode().Ok && flow.Router.Current == Screen.Map, "left the stop, back on the map");
		Assert.True(session.Map.FreeTravel && Selectable(flow) == session.Map.Graph.Nodes.Count - 1, "the toggle holds on the way back to the map");

		page = OpenMapPage(flow);
		Assert.True(page.FullMapToggle.ButtonPressed, "the page shows the toggle as set");
		Press(page.TravelButton("r5-nine-tails")!);
		Assert.True(flow.Router.Current == Screen.Fight && flow.Fight != null && session.Node is { Elder: true, Tier: "elder" }, "the elder fight runs");
		Assert.True(new SessionHudRunView(session).ElderFight, "Y3: its twist strip shows in the fight");
	});

	[Test]
	public static void MapPage_ToggleOff_RestoresTheRoutes_RevealListsTypesAndTwists_MetaUnlocksNoOp(Node runner) => WithBoot(runner, flow =>
	{
		var session = flow.Session!;
		var page = OpenMapPage(flow);
		page.FullMapToggle.ButtonPressed = true;
		Press(page.TravelButton("r3-kappa")!);
		session.WinFight(900);
		if (session.Draft is { } d) d.Pick(0, d.Offer.Cards[0].Targets.Count > 0 ? d.Offer.Cards[0].Targets[0].Slot : null);
		Assert.True(session.CompleteNode().Ok && flow.Router.Current == Screen.Map, "back on the map after row 3");

		page = OpenMapPage(flow);
		page.FullMapToggle.ButtonPressed = false;
		Assert.True(!DevMap.FullMapUnlocked && !session.Map.FreeTravel, "off");
		var open = flow.Map.View!.Nodes.Where(n => n.State == MapNodeState.Reachable).Select(n => n.Id).ToArray();
		Assert.True(open.SequenceEqual(session.Map.Graph.Next("r3-kappa")), "the map screen offers the routes out of row 3 again: " + string.Join(", ", open));
		Assert.True(page.TravelButton("boss-tanuki") is { Disabled: true } && page.TravelButton("r4-merchant") is { Disabled: false }, "travel follows the routes");
		Assert.True(!flow.ChooseNode("boss-tanuki").Ok && flow.Router.Current == Screen.Map, "the map flow refuses a jump with the toggle off");

		Assert.True(page.NodeText("r5-elder-oni") is { } hidden && !hidden.Contains("ELDER TWIST"), "reveal off: name and row only");
		page.RevealToggle.ButtonPressed = true;
		Assert.True(page.NodeText("r5-elder-oni") is { } shown && shown.Contains("ELDER TWIST") && shown.Contains("elder"), "reveal on: type and twist: " + page.NodeText("r5-elder-oni"));
		Assert.True(page.NodeText("r7-dojo")!.Contains("dojo") && page.NodeText("r4-merchant")!.Contains("merchant"), "every node's type");

		Assert.True(page.MetaUnlocksToggle.Disabled, "meta-unlocks greyed until YOK-86");
		Assert.True(!DevMap.UnlockAllMetaUnlocks(Shell.Menu.Context!, out string? why) && why == DevMap.MetaUnlocksPending, "the hook is a no-op with its reason");
	});

	[Test]
	public static void MapPage_MidFight_RefusesEveryChange_AndTheFightIsUntouched(Node runner) => WithBoot(runner, flow =>
	{
		Assert.True(flow.ChooseNode("r1-apprentice").Ok && flow.Fight is { } fight0, "in the row 1 fight");
		var fight = flow.Fight!;
		ulong before = fight.Match.StateHash();
		int tick = fight.Match.Tick;
		Assert.True(fight.TryPause() && fight.OpenDevMenu() && Shell.Menu.ShowSection(DevMenu.MapId), "the Map page opens over the paused fight");
		var page = (DevMapPage)Shell.Menu.Page!;
		var ctx = Shell.Menu.Context!;
		Assert.True(page.FullMapToggle.Disabled && page.RevealToggle.Disabled, "toggles greyed mid-fight");
		Assert.True(page.NoteText == DevContext.MidFightReason, "the page says why: " + page.NoteText);
		Assert.True(!DevMap.SetFullMap(ctx, true, out string? why) && why == DevContext.MidFightReason && !DevMap.FullMapUnlocked && !flow.Session!.Map.FreeTravel, "full map refused mid-fight, nothing changed");
		Assert.True(!DevMap.Travel(ctx, "boss-tanuki", out why) && why == DevContext.MidFightReason && flow.Session!.Node!.NodeId == "r1-apprentice" && !flow.Session.CopyLocked, "travel refused mid-fight");
		Assert.True(page.TravelButton("r2-oni") is { Disabled: true }, "every travel button greyed");
		Shell.Menu.Close();
		fight.PauseMenu!.Resume();
		Assert.True(fight.Match.Tick == tick && fight.Match.StateHash() == before, "the fight is untouched");
	});
}
