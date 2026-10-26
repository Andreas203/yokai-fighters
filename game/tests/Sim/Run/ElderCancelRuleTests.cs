using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Fight;
using YokaiFighters.Harness;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-186: elders offer their cancel rule (X1, X4, A9; GDD 4 "whose offers always include a cancel rule") on the real
/// data (data/cancels/, data/map/), Ryo holds a taken rule for the rest of the run (X2, M5) and the row-8 elder's rare
/// reward (R8) is never empty (PROVISIONAL 50-coin default). No fight is simulated: the session is told the result.
/// </summary>
public static class ElderCancelRuleTests
{
	static string MapDir => ContentPaths.Data("map");
	static NodeSceneTable Table() => NodeSceneTable.Default(MapDir);
	static MapGraph DefaultMap() => FileMapSource.Default(MapDir).Load();
	static AbilityPool Pool() => FightScene.LoadAbilityPool();

	static RunSession NewSession(AbilityPool? pool = null, uint seed = 5) =>
		RunSession.Start(pool ?? Pool(), DefaultMap(), Table(), seed);

	static NodeContext Ctx(string nodeId) => Table().Resolve(DefaultMap().Node(nodeId)).Context;

	static RewardDraft ElderDraft(RunState run, AbilityPool pool, string nodeId, uint seed)
	{
		var ctx = Ctx(nodeId);
		return RewardDraft.Build(run, pool, ctx.DraftYokai, seed, ctx.Elder, elderId: ctx.Elder ? ctx.BindingName : null);
	}

	static IEnumerable<CardView> Cancels(RewardDraft d) => d.Offer.Cards.Where(c => c.Kind == CardKind.CancelRule);

	static string Cards(RewardDraft d) => string.Join("|", d.Offer.Cards.Select(c => $"{c.Kind}:{c.AbilityId}"));

	/// <summary>Enters each node in turn; wins it; picks the first card of <paramref name="pickKind"/> (else card 0); completes it. Stops entered on the last.</summary>
	static void Walk(RunSession s, IEnumerable<string> nodes, CardKind? pickKind = null, bool completeLast = true)
	{
		var list = nodes.ToList();
		for (int i = 0; i < list.Count; i++)
		{
			Assert.True(s.Enter(list[i]).Ok, $"enter {list[i]}");
			if (i == list.Count - 1 && !completeLast) return;
			if (s.Node!.Type is not (NodeTypeRegistry.Merchant or NodeTypeRegistry.Dojo)) Pick(s.WinFight(600), pickKind);
			Assert.True(s.CompleteNode().Ok, $"complete {list[i]}");
		}
	}

	static void Pick(RewardDraft? d, CardKind? kind)
	{
		if (d is null) return;
		var card = d.Offer.Cards.FirstOrDefault(c => c.Kind == kind) ?? d.Offer.Cards.FirstOrDefault(c => c.Kind != CardKind.CancelRule) ?? d.Offer.Cards[0];
		Assert.True(d.Pick(card.Index, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, $"pick {card.AbilityId}");
	}

	static readonly string[] ToRow4 = { "r1-apprentice", "r2-oni", "r3-oni", "r4-merchant" };

	// ---- the offer ----

	[Test]
	public static void ElderDraft_HoldsItsOwnCancelRule_RareWithCardText_AndTheUpgradeStays()
	{
		var pool = Pool();
		Assert.True(pool.CancelRules.Count == 3, $"the pool reads data/cancels/ ({pool.CancelRules.Count})");
		var expected = new Dictionary<string, string> { ["r5-elder-oni"] = "oni-chain", ["r5-nine-tails"] = "fox-step", ["r8-elder-oni"] = "oni-chain" };
		foreach (var (node, ruleId) in expected)
			for (uint seed = 1; seed <= 24; seed++)
			{
				var d = ElderDraft(RunState.NewRun(pool), pool, node, seed);
				var c = Cancels(d).Single();
				var data = pool.CancelRule(ruleId)!;
				Assert.Equal(ruleId, c.AbilityId, $"{node} seed {seed}: its own rule (offered_by = {Ctx(node).BindingName}, X4)");
				Assert.True(c.Rare && c.Badge == "CANCEL" && c.Targets.Count == 0, $"{node}: rare, no target (A9)");
				Assert.True(c.Name == data.Name && c.Plain == data.CardPlain && c.Frames == data.CardFrames, $"{node}: A12 text from the card block");
				Assert.True(d.Offer.Cards.Count == 3, $"{node} seed {seed}: three cards ({Cards(d)}, A7)");
				Assert.True(d.Offer.Cards.Any(x => x.Kind == CardKind.Upgrade), $"{node} seed {seed}: A7's upgrade stays ({Cards(d)})");
				Assert.True(d.Offer.Cards.Select((x, i) => x.Index == i).All(b => b), "card indexes are their places");
			}
	}

	[Test]
	public static void NonElders_NeverOfferACancelRule_NorDoesAnElderFlagWithoutItsId()
	{
		var pool = Pool();
		var graph = DefaultMap();
		foreach (var n in graph.Nodes.Where(n => !n.Elder && n.Yokai is not null))
		{
			var ctx = Table().Resolve(n).Context;
			for (uint seed = 1; seed <= 16; seed++)
				Assert.True(!Cancels(RewardDraft.Build(RunState.NewRun(pool), pool, ctx.DraftYokai, seed, ctx.Elder)).Any(), $"{n.Id}: X1, only elders");
		}
		foreach (string yokai in new[] { "oni", "kitsune", "kappa" })
			Assert.True(!Cancels(RewardDraft.Build(RunState.NewRun(pool), pool, yokai, 3, elder: true)).Any(), $"{yokai}: rares on, but no elder named, no rule");
		// A whole walk through duels: no duel's draft holds one.
		var s = NewSession(pool);
		foreach (string id in new[] { "r1-apprentice", "r2-kappa", "r3-kappa" })
		{
			Assert.True(s.Enter(id).Ok, id);
			var d = s.WinFight(600)!;
			Assert.True(!Cancels(d).Any(), $"{id}: a duel offers no cancel rule (X1)");
			Pick(d, null);
			Assert.True(s.CompleteNode().Ok, id);
		}
	}

	[Test]
	public static void Undertow_StartsLocked_AndIsOfferedOnceTheSaveUnlocksIt()
	{
		var pool = Pool();
		var undertow = pool.CancelRule("undertow")!;
		Assert.True(undertow.Locked && undertow.OfferedBy == "elder-kappa", "X4 ◆: Undertow starts locked, from Elder Kappa");
		for (uint seed = 1; seed <= 16; seed++)
			Assert.True(!Cancels(ElderDraft(RunState.NewRun(pool), pool, "r8-elder-kappa", seed)).Any(), $"seed {seed}: locked, not offered (A11, M1)");
		// The same unlock path as locked specials: the M1 order names it, the save's unlocks make it available.
		var order = Unlocks.FromData(pool, CancelRuleLoader.LockedIds(ContentPaths.Data("cancels")), out _);
		Assert.True(order.Order.Contains("undertow"), "Undertow is in the M1 unlock order");
		var open = pool.WithUnlocks(new[] { "undertow" });
		Assert.True(open.IsAvailable(open.CancelRule("undertow")!) && !pool.IsAvailable(undertow), "available only in the unlocked pool");
		for (uint seed = 1; seed <= 16; seed++)
			Assert.Equal("undertow", Cancels(ElderDraft(RunState.NewRun(open), open, "r8-elder-kappa", seed)).Single().AbilityId, $"seed {seed}: unlocked, offered");
	}

	[Test]
	public static void HeldRule_OrTwoHeld_IsNotOffered_AndTheDrawIsTheOneWithoutRules()
	{
		var pool = Pool().WithUnlocks(new[] { "undertow" });
		for (uint seed = 1; seed <= 16; seed++)
		{
			var holds = RunState.NewRun(pool);
			holds.HoldCancelRule(pool.CancelRule("oni-chain")!);
			var d = ElderDraft(holds, pool, "r5-elder-oni", seed);
			Assert.True(!Cancels(d).Any(), $"seed {seed}: already held, not offered again");
			// With no rule available the draw is the pre-YOK-186 draw for that seed (E23: same seed, same cards).
			Assert.Equal(Cards(RewardDraft.Build(holds, pool, "oni", seed, elder: true)), Cards(d), $"seed {seed}: as before");

			var two = RunState.NewRun(pool);
			two.HoldCancelRule(pool.CancelRule("fox-step")!);
			two.HoldCancelRule(pool.CancelRule("undertow")!);
			Assert.True(!Cancels(ElderDraft(two, pool, "r5-elder-oni", seed)).Any(), $"seed {seed}: two held (X2), none offered");

			var one = RunState.NewRun(pool);
			one.HoldCancelRule(pool.CancelRule("fox-step")!);
			Assert.Equal("oni-chain", Cancels(ElderDraft(one, pool, "r5-elder-oni", seed)).Single().AbilityId, $"seed {seed}: one other held, still offered");
		}
	}

	[Test]
	public static void Draft_IsTheSameForTheSameSeed_AndElderOffersVaryByCard()
	{
		var pool = Pool();
		var seen = new HashSet<string>();
		for (uint seed = 1; seed <= 32; seed++)
		{
			string a = Cards(ElderDraft(RunState.NewRun(pool), pool, "r5-elder-oni", seed));
			Assert.Equal(a, Cards(ElderDraft(RunState.NewRun(pool), pool, "r5-elder-oni", seed)), $"seed {seed}: same seed, same offer");
			seen.Add(a);
		}
		Assert.True(seen.Count > 1, $"the seed still draws the other cards ({seen.Count} offers)");
		// Two sessions with one seed walk to the same elder offer (E23).
		string Offer(uint seed)
		{
			var s = NewSession(seed: seed);
			Walk(s, ToRow4.Append("r5-elder-oni"), completeLast: false);
			return Cards(s.WinFight(600)!);
		}
		Assert.Equal(Offer(11), Offer(11), "same run seed, same elder offer");
	}

	// ---- taking it ----

	[Test]
	public static void Pick_HoldsTheRule_RefusesATarget_AndAThirdRule()
	{
		var pool = Pool().WithUnlocks(new[] { "undertow" });
		var run = RunState.NewRun(pool);
		var d = ElderDraft(run, pool, "r5-elder-oni", 4);
		var c = Cancels(d).Single();
		ulong before = run.StateHash();
		Assert.True(!d.Pick(c.Index, SpecialSlot.A).Ok && !d.Picked && run.CancelRules.Count == 0 && run.StateHash() == before, "a target is refused and changes nothing");
		var res = d.Pick(c.Index);
		Assert.True(res.Ok && d.Picked && res.Slot == SpecialSlot.None && res.Card == c, "picked with no target");
		Assert.True(run.CancelRules.Single().Id == "oni-chain" && run.StateHash() != before, "held; part of the run hash");

		// X2: the offer was built when Ryo held fewer than two; a pick that would make three is refused.
		var full = RunState.NewRun(pool);
		var late = ElderDraft(full, pool, "r5-elder-oni", 4);
		full.HoldCancelRule(pool.CancelRule("fox-step")!);
		full.HoldCancelRule(pool.CancelRule("undertow")!);
		Assert.True(!late.Pick(Cancels(late).Single().Index).Ok && full.CancelRules.Count == 2 && !late.Picked, "a third rule is refused (X2)");
		Assert.True(!full.CanHoldCancelRule(pool.CancelRule("oni-chain")!), "X2");
	}

	[Test]
	public static void TakenRule_IsOnRyoInEveryLaterFight_AndSurvivesContinue()
	{
		var pool = Pool();
		var graph = DefaultMap();
		var s = NewSession(pool, 21);
		Walk(s, ToRow4.Append("r5-elder-oni"), CardKind.CancelRule);
		Assert.True(s.Run.CancelRules.Select(c => c.Id).SequenceEqual(new[] { "oni-chain" }), "X1/X2: Oni Chain held after the row-5 elder");

		Assert.True(s.Enter("r6-kappa").Ok, "next node");
		Fighter Ryo(RunSession x) => NodeFight.NewMatch(FightScene.Sources, x.Run, x, x.Node!.Fighter, ControlScheme.Kihon).P1;
		var held = Ryo(s).CancelRules;
		Assert.True(held.Count == 1 && held[0] == pool.CancelRule("oni-chain")!.Rule, "the next fight's Ryo holds the rule");

		// M5: Continue into this node (the node-entered save) restarts it holding the rule.
		var save = RunSave.Capture(s, "default-map.json");
		Assert.True(save.CancelRules.SequenceEqual(new[] { "oni-chain" }), "the save names it");
		var back = save.Restore(Pool(), graph, Table());
		Assert.Equal(s.StateHash(), back.StateHash(), "same session hash after restore");
		Assert.True(back.Run.CancelRules.Single().Id == "oni-chain" && Ryo(back).CancelRules.Single().Id == "oni-chain", "held after Continue, and in its fight");

		// Later fights keep it, and a row-8 elder's draft cannot offer it again.
		Pick(s.WinFight(600), null);
		Assert.True(s.CompleteNode().Ok, "r6 done");
		Walk(s, new[] { "r7-dojo", "r8-elder-oni" }, completeLast: false);
		Assert.True(Ryo(s).CancelRules.Single().Id == "oni-chain", "still held two fights later");
		Assert.True(!Cancels(s.WinFight(600)!).Any(), "the row-8 Elder Oni does not offer a rule Ryo holds");

		// A new run starts with none (X1).
		Assert.True(NewSession(pool).Run.CancelRules.Count == 0, "X1: a new run holds no cancel rule");
	}

	[Test]
	public static void Restore_RefusesUnknownRepeatedOrThirdRule()
	{
		var pool = Pool();
		var s = NewSession(pool, 3);
		Walk(s, new[] { "r1-apprentice" });
		var save = RunSave.Capture(s);
		Assert.True(save.CancelRules.Count == 0, "none held, none saved");
		void Refused(string[] ids, string what)
		{
			try { (save with { CancelRules = ids }).Restore(pool, DefaultMap(), Table()); }
			catch (FormatException) { return; }
			throw new AssertFailed($"{what}: expected FormatException");
		}
		Refused(new[] { "no-such-rule" }, "an id the data lacks");
		Refused(new[] { "fox-step", "fox-step" }, "one rule twice");
		Refused(new[] { "fox-step", "oni-chain", "undertow" }, "three rules (X2)");
		var ok = (save with { CancelRules = new[] { "fox-step", "oni-chain" } }).Restore(pool, DefaultMap(), Table());
		Assert.True(ok.Run.CancelRules.Select(c => c.Id).SequenceEqual(new[] { "fox-step", "oni-chain" }), "two, in the saved order");
	}

	// ---- R8: the row-8 elder ----

	[Test]
	public static void Row8Elder_PaysItsRareCard_ElseTheProvisional50Coins()
	{
		var pool = Pool();
		// Oni Chain not taken at row 5 (the Nine-Tails' Fox Step taken instead): the row-8 Elder Oni pays its rare rule, no coins.
		var s = NewSession(pool, 8);
		Walk(s, ToRow4.Concat(new[] { "r5-nine-tails", "r6-kitsune", "r7-merchant", "r8-elder-oni" }), CardKind.CancelRule, completeLast: false);
		int coins = s.Run.Coins;
		var d = s.WinFight(600)!;
		Assert.True(Cancels(d).Single().AbilityId == "oni-chain" && d.Offer.Cards.Any(c => c.Rare), "R8: a rare in the offer");
		Assert.True(s.Run.Coins == coins && s.CoinsWon == 0, "R8: no coins when the rare is there");

		// Oni Chain already held: no rare at the row-8 Elder Oni, so it pays the row-5 elder's 50 (PROVISIONAL).
		var t = NewSession(pool, 8);
		Walk(t, ToRow4.Concat(new[] { "r5-elder-oni", "r6-kitsune", "r7-merchant", "r8-elder-oni" }), CardKind.CancelRule, completeLast: false);
		coins = t.Run.Coins;
		var e = t.WinFight(600)!;
		Assert.True(!e.Offer.Cards.Any(c => c.Rare), "no rare available");
		Assert.True(t.Run.Coins == coins + 50 && t.CoinsWon == 50, $"PROVISIONAL: 50 coins instead ({t.Run.Coins - coins})");
		Assert.Equal(50, t.R8FallbackCoins(), "the row-5 elder's amount, from the node-scene table");

		// Elder Kappa: Undertow locked = no rare = 50; unlocked = the rule, no coins.
		int Paid(AbilityPool p)
		{
			var k = NewSession(p, 8);
			Walk(k, ToRow4.Concat(new[] { "r5-elder-oni", "r6-kappa", "r7-dojo", "r8-elder-kappa" }), CardKind.Upgrade, completeLast: false);
			int before = k.Run.Coins;
			var kd = k.WinFight(600)!;
			Assert.Equal(kd.Offer.Cards.Any(c => c.Rare) ? 0 : 50, k.Run.Coins - before, "paid = 0 with a rare, else 50");
			return k.Run.Coins - before;
		}
		Assert.Equal(50, Paid(pool), "Undertow locked: 50 coins (PROVISIONAL)");
		Assert.Equal(0, Paid(pool.WithUnlocks(new[] { "undertow" })), "Undertow unlocked: the rare is the reward");

		// The row-5 elder still pays its 50 whatever its offer holds (R8).
		var r5 = NewSession(pool, 8);
		Walk(r5, ToRow4.Append("r5-elder-oni"), completeLast: false);
		coins = r5.Run.Coins;
		Assert.True(Cancels(r5.WinFight(600)!).Any() && r5.Run.Coins == coins + 50, "row 5: rule offered and 50 paid");
	}

	[Test]
	public static void MapRewardLine_AgreesWithWhatTheRow8ElderPays()
	{
		var pool = Pool();
		// The map's line for each node, as the player reads it before choosing (Ui.RunMapView, the session's map and table).
		string Line(RunSession s, string id) => new Ui.RunMapView(s.Map, s.Run, s.Scenes).Detail(id)!.Reward!;
		const string R8Line = "Pays a rare ability, or 50 coins if its offer has none.";

		// Case 1: Oni Chain taken at the row-5 Elder Oni, then the row-8 Elder Oni: no rare left, 50 paid.
		var a = NewSession(pool, 8);
		Walk(a, ToRow4.Concat(new[] { "r5-elder-oni", "r6-kitsune", "r7-merchant" }), CardKind.CancelRule);
		Assert.True(Line(a, "r8-elder-oni").StartsWith(R8Line), "map: " + Line(a, "r8-elder-oni"));
		Assert.True(a.Enter("r8-elder-oni").Ok, "enter");
		int before = a.Run.Coins;
		Assert.True(!a.WinFight(600)!.Offer.Cards.Any(c => c.Rare) && a.Run.Coins - before == 50, "no rare in the offer, so the 50 the line names");

		// Case 2: the row-8 Elder Kappa on a save that has not unlocked Undertow: no rare, 50 paid.
		var b = NewSession(pool, 8);
		Walk(b, ToRow4.Concat(new[] { "r5-elder-oni", "r6-kappa", "r7-dojo" }), CardKind.Upgrade);
		Assert.True(Line(b, "r8-elder-kappa").StartsWith(R8Line), "map: " + Line(b, "r8-elder-kappa"));
		Assert.True(b.Enter("r8-elder-kappa").Ok, "enter");
		before = b.Run.Coins;
		Assert.True(!b.WinFight(600)!.Offer.Cards.Any(c => c.Rare) && b.Run.Coins - before == 50, "no rare in the offer, so the 50 the line names");

		// With the rare there (Oni Chain not taken), the same line and no coins: the rare is the reward.
		var rare = NewSession(pool, 8);
		Walk(rare, ToRow4.Concat(new[] { "r5-elder-oni", "r6-kitsune", "r7-merchant" }), CardKind.Upgrade);
		Assert.True(rare.Enter("r8-elder-oni").Ok, "enter");
		before = rare.Run.Coins;
		Assert.True(rare.WinFight(600)!.Offer.Cards.Any(x => x.Rare) && rare.Run.Coins == before, "a rare in the offer, no coins");

		// Elders that pay coins and duels keep their lines.
		Assert.True(Line(rare, "r5-nine-tails").StartsWith("Pays 50 coins.") && Line(rare, "r2-oni").StartsWith("Pays 25 coins."), "other lines unchanged");
	}

	/// <summary>The real node table, except that the nodes in <see cref="FailFor"/> can't be placed (as a missing tier).</summary>
	sealed class FlakyScenes : INodeSceneResolver
	{
		readonly NodeSceneTable _table = Table();
		public HashSet<string> FailFor { get; } = new();
		public NodeScene Resolve(MapNodeData node) =>
			FailFor.Contains(node.Id) ? throw new FormatException($"{node.Id}: no tier (test)") : _table.Resolve(node);
	}

	[Test]
	public static void R8Fallback_SkipsAnElderTheTableCannotPlace_MapAndWinFightStillWork()
	{
		var pool = Pool();
		var scenes = new FlakyScenes();
		var s = RunSession.Start(pool, DefaultMap(), scenes, 8); // the session places every node at start, so fail only after
		Walk(s, ToRow4.Concat(new[] { "r5-elder-oni", "r6-kappa", "r7-dojo" }), CardKind.Upgrade);
		Assert.True(s.Enter("r8-elder-kappa").Ok, "enter the row-8 Elder Kappa");

		// Another elder (one that pays coins) can't be placed: the map still shows every other line, and that elder none.
		scenes.FailFor.Add("r5-elder-oni");
		var map = new Ui.RunMapView(s.Map, s.Run, scenes);
		Assert.True(map.Detail("r8-elder-oni")!.Reward!.StartsWith("Pays a rare ability, or 50 coins"), "the other row-5 elder still pays 50: " + map.Detail("r8-elder-oni")!.Reward);
		Assert.True(map.Detail("r5-elder-oni")!.Reward is null, "the elder that can't be placed shows no reward line, as before");
		Assert.Equal(50, RunSession.R8FallbackCoins(s.Map.Graph, scenes), "skipped, not thrown");

		// Every elder that pays coins can't be placed: the fallback is 0, and WinFight still pays once and offers its draft.
		scenes.FailFor.Add("r5-nine-tails");
		Assert.True(new Ui.RunMapView(s.Map, s.Run, scenes).Detail("r8-elder-kappa")!.Reward!.StartsWith("Pays no coins."), "no amount to name");
		int coins = s.Run.Coins;
		var d = s.WinFight(600);
		Assert.True(d is not null && s.NodePaid && s.Run.Coins == coins && s.CoinsWon == 0 && s.Run.Health == 650, "won and paid as a 0-coin node, nothing half-applied");
	}

	[Test]
	public static void DevTools_KeepAndShowCancelRules()
	{
		var pool = Pool();
		var s = NewSession(pool, 4);
		Walk(s, ToRow4.Append("r5-elder-oni"), CardKind.CancelRule);
		// The dev run editor keeps the held rules through Capture and Check (they were dropped before).
		var build = Dev.DevRunEditor.Capture(s);
		Assert.True(build.CancelRules.SequenceEqual(new[] { "oni-chain" }), "captured");
		var check = Dev.DevRunEditor.For(s).Check(build);
		Assert.True(check.Refusal is null && check.Session!.Run.CancelRules.Single().Id == "oni-chain", "rebuilt holding it: " + check.Refusal);
		Assert.True(Dev.DevRunEditor.For(s).Check(build with { CancelRules = new[] { "no-such-rule" } }).Refusal is { } why && why.Contains("no-such-rule"), "an unknown id is refused");
		Assert.True(Dev.DevRunEditor.For(s).Check(build with { CancelRules = new[] { "fox-step", "oni-chain", "undertow" } }).Refusal is not null, "a third rule is refused (X2)");
		// The dev reward preview of an elder's spread shows its cancel card; a duel's never does.
		var (elder, _) = Dev.DevJump.BuildDraft(pool, "oni", 3, rares: true, new Dev.DefaultDevRunState());
		Assert.Equal("oni-chain", Cancels(elder).Single().AbilityId, "the elder preview offers Oni Chain");
		var (duel, _) = Dev.DevJump.BuildDraft(pool, "oni", 3, rares: false, new Dev.DefaultDevRunState());
		Assert.True(!Cancels(duel).Any(), "a duel preview offers none");
	}

	// ---- the reward screen's adapter ----

	[Test]
	public static void RewardScreenSource_ShowsTheRuleAsARareCard_PlainFirst_AndPicksWithNoTarget()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		var draft = ElderDraft(run, pool, "r5-nine-tails", 6);
		var source = new Ui.DraftRewardSource(draft, run, story: new Ui.StubRewardSource());
		int i = Cancels(draft).Single().Index;
		var view = source.Offer.Cards[i];
		var data = pool.CancelRule("fox-step")!;
		Assert.True(view.Kind == Ui.CardKind.CancelRule && view.Rare && view.Tag.StartsWith("RARE"), $"rare cancel-rule card ({view.Kind}, {view.Tag})");
		Assert.True(view.Plain == data.CardPlain && view.Frames == data.CardFrames && view.TargetPrompt is null, "A12: plain text, frames behind the toggle, no target step");
		Assert.True(source.Offer.Cards.Where((c, k) => k != i).All(c => !c.Rare), "the common cards are not marked rare");
		source.Pick(i, null);
		Assert.True(source.LastResult is { Ok: true } && run.CancelRules.Single().Id == "fox-step", "picked through the screen's adapter");
	}

	// ---- the whole-run bot ----

	[Test]
	public static void Bot_PicksFromAnElderOffer_ByItsOwnOrder()
	{
		var bot = BotConfig.LoadDefault(ContentPaths.DataDir);
		var pool = Pool();
		for (uint seed = 1; seed <= 8; seed++)
		{
			var run = RunState.NewRun(pool);
			var d = ElderDraft(run, pool, "r5-elder-oni", seed);
			var (card, target) = new RunBot(bot.Run, seed).ChooseCard(d.Offer);
			Assert.True(d.Pick(card, target).Ok, $"seed {seed}: the bot's pick is accepted");
			Assert.Equal(bot.Run.RewardOrder[0], d.Offer.Cards[card].Kind, "the newcomer's order (upgrade first) decides; tuning untouched");
		}
		// An offer whose only card is the rule (nothing to level, no modifier fits): the bot's fallback takes it.
		var only = RunState.NewRun(pool);
		while (only.Owned.Any(o => only.CanLevelUp(o.Slot))) only.LevelUp(only.Owned.First(o => only.CanLevelUp(o.Slot)).Slot);
		foreach (var m in pool.Modifiers) foreach (var (slot, _) in only.Owned.ToList()) if (only.CanAttach(slot, m)) only.Attach(slot, m);
		var lone = ElderDraft(only, pool, "r5-elder-oni", 2);
		if (lone.Offer.Cards.Count == 1)
		{
			var (card, target) = new RunBot(bot.Run, 2).ChooseCard(lone.Offer);
			Assert.True(lone.Pick(card, target).Ok && only.CancelRules.Count == 1, "the bot takes a lone rule");
		}
		else Assert.True(Cancels(lone).Count() == 1, $"the offer still holds the rule ({Cards(lone)})");
	}
}
