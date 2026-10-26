using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Harness;
using YokaiFighters.Replay;
using YokaiFighters.Settings;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;
using YokaiFighters.Ui;
using SimKind = YokaiFighters.Sim.CardKind;

namespace YokaiFighters.Tests;

/// <summary>
/// Test Breaker probes for YOK-186 (elders offer their cancel rule). Expected values come from X1-X4, A7-A12, R6, R8, M1, M3, M5, T0-T2,
/// E23 in docs/design/rules.md; the row-8 coin fallback is the PROVISIONAL R8 reading the task names, so a probe that pins it says so.
/// </summary>
public static class ElderCancelRuleBreakerProbes
{
	static string MapDir => ContentPaths.Data("map");
	static MapGraph Graph() => FileMapSource.Default(MapDir).Load();
	static NodeSceneTable Table() => NodeSceneTable.Default(MapDir);
	static AbilityPool Pool() => FightScene.LoadAbilityPool();
	static AbilityPool Unlocked(AbilityPool p) => p.WithUnlocks(new[] { "undertow" });
	static RunSession Start(AbilityPool pool, uint seed = 5) => RunSession.Start(pool, Graph(), Table(), seed);

	static string Cards(RewardDraft d) => string.Join("|", d.Offer.Cards.Select(c => $"{c.Kind}:{c.AbilityId}"));
	static bool IsStop(RunSession s) => s.Node!.Type is NodeTypeRegistry.Merchant or NodeTypeRegistry.Dojo;
	static string[] Held(RunSession s) => s.Run.CancelRules.Select(c => c.Id).ToArray();

	/// <summary>Takes the first card of <paramref name="kind"/> (card 0 when there is none), with its first target when it has one.</summary>
	static void PickKind(RewardDraft d, SimKind? kind)
	{
		var card = d.Offer.Cards.FirstOrDefault(c => c.Kind == kind) ?? d.Offer.Cards[0];
		Assert.True(d.Pick(card.Index, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, $"pick {card.AbilityId}");
	}

	/// <summary>Enters each node, wins it (health 600), takes <paramref name="kind"/> and completes it.</summary>
	static void Walk(RunSession s, SimKind? kind, params string[] nodes)
	{
		foreach (string id in nodes)
		{
			Assert.True(s.Enter(id).Ok, $"enter {id}");
			if (!IsStop(s) && s.WinFight(600) is { } d) PickKind(d, kind);
			Assert.True(s.CompleteNode().Ok, $"complete {id}");
		}
	}

	static readonly string[] ToRow4 = { "r1-apprentice", "r2-oni", "r3-oni", "r4-merchant" };

	// ---- X1, M3: nothing leaks into the next run ----

	// X1: Ryo starts every run with no cancels. M3: only a special is carried over, never a rule.
	[Test]
	public static void NewRunAfterARunThatHeldTwoRules_HoldsNone_InTheFirstFight_AndCarryOverNeverNamesARule()
	{
		var pool = Unlocked(Pool());
		var cancelIds = pool.CancelRules.Select(c => c.Id).ToHashSet();
		var s = Start(pool, 9);
		Walk(s, SimKind.CancelRule, ToRow4.Concat(new[] { "r5-nine-tails", "r6-kitsune", "r7-merchant", "r8-elder-oni" }).ToArray());
		Assert.True(Held(s).SequenceEqual(new[] { "fox-step", "oni-chain" }), "setup: both rules held: " + string.Join(",", Held(s)));
		Walk(s, null, "boss-tanuki");
		Assert.True(s.Ended is { Won: true }, "setup: the run is won");
		string? carried = CarryOver.FromRunEnd(s.Run, s.Ended!);
		Assert.True(carried is not null && pool.Special(carried) is not null && !cancelIds.Contains(carried), $"M3 carries a special, not a rule ({carried})");
		Assert.Equal(0, CarryOver.NewRun(pool, carried, Graph(), out _).CancelRules.Count, "X1: the carried-over run holds no rule");

		// A fresh session (same seed, as a restart would give) and its first fight.
		var t = Start(pool, 9);
		Assert.Equal(0, t.Run.CancelRules.Count, "X1: a new run holds none");
		Assert.True(t.Enter("r1-apprentice").Ok, "enter");
		var m = NodeFight.NewMatch(FightScene.Sources, t.Run, t, t.Node!.Fighter, ControlScheme.Kihon);
		Assert.Equal(0, m.P1.CancelRules.Count, "X1: Ryo's first fight holds none");
		Assert.Equal(CarryOver.NewRun(pool, null, Graph(), out _).StateHash(), RunState.NewRun(pool, Graph()).StateHash(), "a plain new run hashes as ever");

		// A run lost at the row-8 elder while holding a rule: the next one is clean too.
		var lost = Start(pool, 4);
		Walk(lost, SimKind.CancelRule, ToRow4.Concat(new[] { "r5-elder-oni", "r6-kappa", "r7-dojo" }).ToArray());
		Assert.True(lost.Enter("r8-elder-kappa").Ok && lost.LoseFight().Ok && lost.Ended is { Won: false }, "setup: lost at row 8");
		var after = CarryOver.NewRun(pool, CarryOver.FromRunEnd(lost.Run, lost.Ended!), Graph(), out _);
		Assert.Equal(0, after.CancelRules.Count, "X1: after a loss the next run holds none");
	}

	// ---- X1/X2/T0/T2: the rule stays on Ryo, never on the Tanuki ----

	// X2: held for the run; T0: the Tanuki fights with Ryo's normals, starters and one copied special, so it holds no rule; T2: the copy is a special.
	[Test]
	public static void HeldRule_IsOnRyoInEveryLaterFightIncludingTheTanuki_TheTanukiHoldsNone_CopyIsASpecial()
	{
		var pool = Unlocked(Pool());
		var cancelIds = pool.CancelRules.Select(c => c.Id).ToHashSet();
		var s = Start(pool, 12);
		Walk(s, null, ToRow4);
		Assert.Equal(0, s.Run.CancelRules.Count, "X1: none before the first elder");
		Walk(s, SimKind.CancelRule, "r5-elder-oni");
		Assert.True(Held(s).SequenceEqual(new[] { "oni-chain" }), "Oni Chain taken at row 5");
		foreach (string id in new[] { "r6-kappa", "r7-dojo", "r8-elder-kappa", "boss-tanuki" })
		{
			Assert.True(s.Enter(id).Ok, "enter " + id);
			if (!IsStop(s))
			{
				var m = NodeFight.NewMatch(FightScene.Sources, s.Run, s, s.Node!.Fighter, ControlScheme.Kihon);
				Assert.True(m.P1.CancelRules.Select(r => r.Id).Where(x => x == "oni-chain").Count() == 1 && m.P1.CancelRules.Count == s.Run.CancelRules.Count, $"{id}: Ryo holds exactly what the run holds");
				Assert.Equal(0, m.P2.CancelRules.Count, $"{id}: the opponent holds no rule (T0 for the Tanuki)");
				if (id == "boss-tanuki")
				{
					Assert.True(s.LockedCopy is { } copy && pool.Special(copy.Data.Id) is not null && !cancelIds.Contains(copy.Data.Id), "T2: the copy target is a special");
					Assert.True(m.P2.Specials.Skip(1).Where(x => x is not null).All(x => !cancelIds.Contains(x!.Data.Id)), "T0: no rule among the Tanuki's specials");
				}
			}
			if (id != "boss-tanuki") { if (!IsStop(s) && s.WinFight(600) is { } d) PickKind(d, SimKind.Upgrade); Assert.True(s.CompleteNode().Ok, "complete " + id); }
		}
		// T2 by itself: the copy target does not move with a rule held.
		var run = RunState.NewRun(pool);
		run.LevelUp(SpecialSlot.B);
		var before = CopyRule.Target(run)!;
		run.HoldCancelRule(pool.CancelRule("fox-step")!); run.HoldCancelRule(pool.CancelRule("oni-chain")!);
		var now = CopyRule.Target(run)!;
		Assert.True(now.Slot == before.Slot && now.Data.Id == before.Data.Id && now.Level == before.Level, "T2: same target holding two rules");
	}

	// ---- R6: the shop ----

	// R6: never sells rares or cancel rules. The pool has cancel rules (all unlocked) and the run holds none, so a shelf that listed one could.
	[Test]
	public static void Shop_NeverListsOrSellsACancelRule_AndAnAttemptChangesNothing()
	{
		var pool = Pool().WithUnlocks(new[] { "undertow" }.Concat(Pool().Specials.Where(x => x.Locked).Select(x => x.Id)).Concat(Pool().Modifiers.Where(x => x.Locked).Select(x => x.Id)));
		var cancelIds = pool.CancelRules.Select(c => c.Id).ToHashSet();
		for (uint seed = 1; seed <= 6; seed++)
		{
			var s = Start(pool, seed);
			Walk(s, SimKind.Upgrade, "r1-apprentice", "r2-oni", "r3-oni");
			Assert.True(s.Enter("r4-merchant").Ok && s.Merchant!.Choose(MerchantChoice.Shop).Ok, "shop open");
			var shop = s.Merchant!.Shop;
			Assert.True(shop.Shelf.Concat<object>(shop.ModifierShelf).Count() > 0, "setup: the shelf has something");
			Assert.True(shop.Shelf.All(x => !cancelIds.Contains(x.Id) && x.Rarity == "common") && shop.ModifierShelf.All(x => !cancelIds.Contains(x.Id) && x.Rarity == "common"), $"seed {seed}: no rule, nothing rare on the shelf");
			s.Run.AddCoins(500);
			ulong h = s.StateHash(); int coins = s.Run.Coins;
			foreach (string id in cancelIds)
			{
				Assert.True(!shop.Buy(id).Ok && !shop.BuyModifier(id, SpecialSlot.A).Ok && shop.WhyNotBuy(id) is not null, $"{id}: not for sale");
				Assert.True(s.StateHash() == h && s.Run.Coins == coins && s.Run.CancelRules.Count == 0, $"{id}: a refused buy changes nothing");
			}
		}
	}

	// ---- X2: the cap, in whatever order ----

	// X2/E23/R8: Fox Step and Oni Chain held, Elder Kappa with Undertow unlocked: no cancel card, the draw without a rule, and (PROVISIONAL R8) the fallback coins at row 8.
	[Test]
	public static void TwoRulesHeld_ThenElderKappaWithUndertowUnlocked_OffersNoRule_TheOldDraw_AndTheRow8Fallback()
	{
		var pool = Unlocked(Pool());
		foreach (var order in new[] { new[] { "fox-step", "oni-chain" }, new[] { "oni-chain", "fox-step" } })
			for (uint seed = 1; seed <= 12; seed++)
			{
				var s = Start(pool, seed);
				s.Run.RestoreCancelRules(order.Select(id => pool.CancelRule(id)!));
				s.Map.FreeTravel = true;
				Assert.True(s.Enter("r8-elder-kappa").Ok, "enter");
				uint drawSeed = s.Seed + (uint)s.Step;
				int coins = s.Run.Coins;
				var expected = Cards(RewardDraft.Build(s.Run, pool, "kappa", drawSeed, elder: true)); // the draw with no elder id = no rule
				var d = s.WinFight(600)!;
				Assert.True(!d.Offer.Cards.Any(c => c.Kind == SimKind.CancelRule), $"seed {seed}: X2, no third rule offered ({Cards(d)})");
				Assert.Equal(expected, Cards(d), $"seed {seed}: the draw without a rule (E23)");
				Assert.Equal(50, s.Run.Coins - coins, $"seed {seed}: no rare in the offer, so the provisional row-8 coins");
				Assert.True(s.Run.CancelRules.Select(c => c.Id).SequenceEqual(order), "still the two, as held");
				PickKind(d, SimKind.Upgrade);
				Assert.True(s.Run.CancelRules.Count == 2, "no rule from a pick either");
			}
	}

	// X2 in a session, in both orders: take one rule, build an offer for a third, take the second rule (free travel in the same row), then the stale
	// offer is refused and changes nothing, and a fresh one holds no rule.
	[Test]
	public static void RulesTakenInEitherOrder_StopAtTwo_AStaleOfferForAThirdIsRefusedAndChangesNothing()
	{
		var pool = Unlocked(Pool());
		foreach (var rowFive in new[] { new[] { "r5-elder-oni", "r5-nine-tails" }, new[] { "r5-nine-tails", "r5-elder-oni" } })
		{
			var s = Start(pool, 6);
			Walk(s, null, ToRow4);
			s.Map.FreeTravel = true;
			Walk(s, SimKind.CancelRule, rowFive[0]);
			var stale = RewardDraft.Build(s.Run, pool, "kappa", 3, elder: true, elderId: "elder-kappa"); // built while Ryo holds one: Undertow is on offer
			int at = stale.Offer.Cards.ToList().FindIndex(c => c.Kind == SimKind.CancelRule);
			Assert.True(at >= 0, "setup: Undertow offered to a run holding one rule");
			Walk(s, SimKind.CancelRule, rowFive[1]);
			Assert.True(Held(s).OrderBy(x => x).SequenceEqual(new[] { "fox-step", "oni-chain" }), "both held: " + string.Join(",", Held(s)));
			ulong h = s.StateHash(); var order = Held(s);
			var res = stale.Pick(at);
			Assert.True(!res.Ok && !stale.Picked && Held(s).SequenceEqual(order) && s.StateHash() == h, "X2: the third rule is refused and nothing changes");
			Assert.True(!RewardDraft.Build(s.Run, pool, "kappa", 3, elder: true, elderId: "elder-kappa").Offer.Cards.Any(c => c.Kind == SimKind.CancelRule), "a new offer holds no third rule");
		}
	}

	// ---- E23: determinism ----

	// E23: same seed and choices give the same offers and StateHash at every step; the other pick gives another.
	[Test]
	public static void SameSeedAndPicks_SameOffersAndHashes_AnotherPickAtTheElder_AnotherHash()
	{
		List<string> Run(uint seed, SimKind? atElder, string elderNode = "r5-elder-oni")
		{
			var s = Start(Pool(), seed);
			var log = new List<string>();
			foreach (string id in ToRow4.Append(elderNode).Concat(new[] { "r6-kappa", "r7-dojo", "r8-elder-oni" }))
			{
				Assert.True(s.Enter(id).Ok, id);
				log.Add($"{id} in {s.StateHash()}");
				if (!IsStop(s) && s.WinFight(600) is { } d)
				{
					log.Add($"{id} offer {Cards(d)} coins {s.Run.Coins}");
					PickKind(d, id.StartsWith("r5-") || id.StartsWith("r8-") ? atElder : SimKind.Upgrade);
				}
				Assert.True(s.CompleteNode().Ok, id);
				log.Add($"{id} out {s.StateHash()}");
			}
			return log;
		}
		for (uint seed = 1; seed <= 6; seed++)
		{
			var a = Run(seed, SimKind.CancelRule);
			Assert.True(a.SequenceEqual(Run(seed, SimKind.CancelRule)), $"seed {seed}: the same walk twice is the same offers and hashes");
			var other = Run(seed, SimKind.Upgrade);
			int at = a.FindIndex(x => x.StartsWith("r5-elder-oni out"));
			Assert.True(a[at] != other[at], $"seed {seed}: taking the rule or the upgrade at the elder is a different run hash");
		}
		// Which rule is held is run state: the same run holding Fox Step, Oni Chain, Undertow, two of them, or none, is five different hashes.
		var pool = Unlocked(Pool());
		ulong HashHolding(params string[] ids) { var r = RunState.NewRun(pool); foreach (string id in ids) r.HoldCancelRule(pool.CancelRule(id)!); return r.StateHash(); }
		var hashes = new[] { HashHolding(), HashHolding("fox-step"), HashHolding("oni-chain"), HashHolding("undertow"), HashHolding("fox-step", "oni-chain") };
		Assert.Equal(hashes.Length, hashes.Distinct().Count(), "none, each rule alone and a pair all hash apart");
	}

	// A7/E23: the third card beside the rule and the upgrade is a seeded pick between a new special and a modifier, so neither kind is shut out where the
	// yokai has both (the Nine-Tailed Kitsune: Foxfire and modifiers); where it has only one (Oni: modifiers only in data today) that one is the third card.
	[Test]
	public static void ElderOffer_IsRuleUpgradeAndExactlyOneOfNewOrModifier_BothKindsOccurWhereBothExist()
	{
		var pool = Pool();
		int withNew = 0, withMod = 0;
		foreach (string node in new[] { "r5-elder-oni", "r5-nine-tails", "r8-elder-oni" })
		{
			var ctx = Table().Resolve(Graph().Node(node)).Context;
			bool bothExist = pool.Specials.Any(x => x.Source == ctx.DraftYokai && !x.Starter && x.Rarity == "common" && pool.IsAvailable(x)) && pool.Modifiers.Any(x => x.Source == ctx.DraftYokai && pool.IsAvailable(x));
			for (uint seed = 1; seed <= 128; seed++)
			{
				var d = RewardDraft.Build(RunState.NewRun(pool), pool, ctx.DraftYokai, seed, ctx.Elder, elderId: ctx.BindingName);
				var kinds = d.Offer.Cards.Select(c => c.Kind).ToList();
				Assert.True(kinds.Count == 3 && kinds[0] == SimKind.CancelRule && kinds.Count(k => k == SimKind.Upgrade) == 1 && kinds.Count(k => k is SimKind.NewSpecial or SimKind.Modifier) == 1, $"{node} seed {seed}: {Cards(d)}");
				if (!bothExist) continue;
				if (kinds.Contains(SimKind.NewSpecial)) withNew++; else withMod++;
			}
		}
		Assert.True(withNew + withMod > 0, "setup: the Nine-Tailed Kitsune has a new special and a modifier");
		Assert.True(withNew * 5 >= (withNew + withMod) && withMod * 5 >= (withNew + withMod), $"both third-card kinds appear (new {withNew}, modifier {withMod})");
	}

	// A7: three cards "from the yokai beaten", plus the rule. An elder whose yokai has no new special or modifier left still offers the rule and the upgrade.
	[Test]
	public static void OfferOfOnlyTheRuleAndTheUpgrade_ThenOnlyTheRule_PickAndBotAndScreenHandleThem()
	{
		var full = Pool();
		var pool = new AbilityPool(full.Specials.Where(s => s.Source != "oni"), full.Modifiers.Where(m => m.Source != "oni"), full.Notes, full.CancelRules);
		var bot = BotConfig.LoadDefault(ContentPaths.DataDir).Run;
		for (uint seed = 1; seed <= 8; seed++)
		{
			var run = RunState.NewRun(pool);
			var d = RewardDraft.Build(run, pool, "oni", seed, elder: true, elderId: "elder-oni");
			Assert.Equal("CancelRule:oni-chain|Upgrade", string.Join("|", d.Offer.Cards.Select(c => c.Kind == SimKind.CancelRule ? $"{c.Kind}:{c.AbilityId}" : c.Kind.ToString())), $"seed {seed}: the rule and the upgrade");
			// The bot's order puts the upgrade first.
			var (card, target) = new RunBot(bot, seed).ChooseCard(d.Offer);
			Assert.Equal(SimKind.Upgrade, d.Offer.Cards[card].Kind, "the newcomer's order");
			Assert.True(d.Pick(card, target).Ok, "accepted");
			// An order with neither of the draft's kinds falls back to card 0, the rule: no target, accepted.
			var run2 = RunState.NewRun(pool);
			var d2 = RewardDraft.Build(run2, pool, "oni", seed, elder: true, elderId: "elder-oni");
			var (c2, t2) = new RunBot(bot with { RewardOrder = new[] { SimKind.Modifier } }, seed).ChooseCard(d2.Offer);
			Assert.True(d2.Offer.Cards[c2].Kind == SimKind.CancelRule && t2 is null && d2.Pick(c2, t2).Ok && run2.CancelRules.Single().Id == "oni-chain", "fallback takes the rule with no target");
		}
		// Nothing left to level: the offer is the rule alone.
		var maxed = RunState.NewRun(pool);
		while (maxed.Owned.Any(o => maxed.CanLevelUp(o.Slot))) maxed.LevelUp(maxed.Owned.First(o => maxed.CanLevelUp(o.Slot)).Slot);
		var lone = RewardDraft.Build(maxed, pool, "oni", 2, elder: true, elderId: "elder-oni");
		Assert.Equal("CancelRule:oni-chain", Cards(lone), "only the rule");
		Assert.True(lone.Pick(0).Ok && maxed.CancelRules.Count == 1, "and it can be taken");
	}

	// ---- R8: the fallback ----

	/// <summary>The table with an edit: <paramref name="edit"/> receives node-scenes.json as a JSON object.</summary>
	static NodeSceneTable EditedTable(Action<JsonObject> edit)
	{
		var root = JsonNode.Parse(File.ReadAllText(Path.Combine(MapDir, "node-scenes.json")))!.AsObject();
		edit(root);
		return NodeSceneTable.Parse(root.ToJsonString());
	}

	// PROVISIONAL R8: only an elder that pays no coins gets the fallback; a duel that pays 0 by the table, the row-5 elder with no rare, and a stop pay what the table says.
	[Test]
	public static void Fallback_NeverFiresForANonElder_OrForTheRow5Elder_WhateverIsInItsOffer()
	{
		var pool = Pool();
		// A duel the table makes pay nothing (a data edit): still nothing.
		var zeroDuel = EditedTable(r => r["nodes"] = new JsonObject { ["r2-oni"] = new JsonObject { ["coins"] = 0 } });
		var s = RunSession.Start(pool, Graph(), zeroDuel, 5);
		Walk(s, null, "r1-apprentice");
		int coins = s.Run.Coins;
		Assert.True(s.Enter("r2-oni").Ok && s.WinFight(600) is not null && s.CoinsWon == 0 && s.Run.Coins == coins, "a non-elder that pays 0 gets no fallback");
		// The row-5 elder whose own rule is already held: no rare in its offer, and it still pays its 50, not 100.
		var t = Start(pool, 5);
		Walk(t, null, ToRow4);
		t.Run.HoldCancelRule(pool.CancelRule("oni-chain")!);
		int before = t.Run.Coins;
		Assert.True(t.Enter("r5-elder-oni").Ok, "enter");
		var d = t.WinFight(600)!;
		Assert.True(!d.Offer.Cards.Any(c => c.Rare), "setup: no rare in the offer");
		Assert.True(t.Run.Coins - before == 50 && t.CoinsWon == 50, $"row 5 pays its 50 and no fallback on top ({t.Run.Coins - before})");
		// A merchant pays nothing and gets no fallback either (a stop is not an elder).
		var u = Start(Unlocked(pool), 5);
		Walk(u, null, ToRow4);
		Assert.Equal(75, u.Run.Coins, "the merchant at row 4 paid nothing");
	}

	// PROVISIONAL R8: the amount is the most any elder on the map pays, so a map whose row-5 elder pays 80 has an 80-coin fallback, and its line says 80.
	[Test]
	public static void Fallback_IsTheMostAnyElderPays_LineAndPayoutAgreeOnAChangedTable()
	{
		var pool = Pool();
		var table = EditedTable(r => r["nodes"] = new JsonObject { ["r5-nine-tails"] = new JsonObject { ["coins"] = 80 } });
		Assert.Equal(80, RunSession.R8FallbackCoins(Graph(), table), "the most any elder pays");
		var s = RunSession.Start(pool, Graph(), table, 5);
		Walk(s, SimKind.Upgrade, ToRow4.Concat(new[] { "r5-elder-oni", "r6-kappa", "r7-dojo" }).ToArray());
		string line = new RunMapView(s.Map, s.Run, s.Scenes).Detail("r8-elder-kappa")!.Reward!;
		Assert.True(line.StartsWith("Pays a rare ability, or 80 coins if its offer has none."), "the line names 80: " + line);
		Assert.True(s.Enter("r8-elder-kappa").Ok, "enter");
		int before = s.Run.Coins;
		Assert.True(s.WinFight(600) is { } d && !d.Offer.Cards.Any(c => c.Rare) && s.Run.Coins - before == 80, $"paid 80 ({s.Run.Coins - before})");
		// A map with no elder paying anything: no amount to name, nothing paid.
		var none = EditedTable(r => r["types"]!["elder"]!["coins"] = 0);
		Assert.Equal(0, RunSession.R8FallbackCoins(Graph(), none), "no elder pays coins: 0");
	}

	// R8 (PROVISIONAL) + X4 + A11: over many seeds and routes, with and without Undertow unlocked, and taking the rule or not at row 5, the map's line
	// is the R8 line at both row-8 elders and the coins paid are 0 exactly when a rare is on offer: Oni Chain not held (at Elder Oni), Undertow unlocked (at Elder Kappa).
	[Test]
	public static void RowEightLineAndPayout_AgreeAcrossSeedsRoutesAndUnlocks()
	{
		const string R8Line = "Pays a rare ability, or 50 coins if its offer has none.";
		int rows8 = 0;
		foreach (bool undertow in new[] { false, true })
			foreach (SimKind? takeAtFive in new SimKind?[] { SimKind.CancelRule, SimKind.Upgrade })
				for (uint seed = 1; seed <= 10; seed++)
					for (int variant = 0; variant < 8; variant++)
					{
						var pool = undertow ? Unlocked(Pool()) : Pool();
						var s = Start(pool, seed);
						string[] route = { "r1-apprentice", variant % 2 == 0 ? "r2-oni" : "r2-kappa", "r3-kappa", "r4-merchant", (variant & 2) == 0 ? "r5-elder-oni" : "r5-nine-tails", (variant & 4) == 0 ? "r6-kitsune" : "r6-kappa", "r7-dojo" };
						foreach (string id in route) Walk(s, id.StartsWith("r5-") ? takeAtFive : SimKind.Upgrade, id);
						string eight = variant % 3 == 0 ? "r8-elder-kappa" : "r8-elder-oni";
						string line = new RunMapView(s.Map, s.Run, s.Scenes).Detail(eight)!.Reward!;
						Assert.True(line.StartsWith(R8Line), $"{eight} line: {line}");
						bool rareOnOffer = eight == "r8-elder-kappa" ? undertow : !s.Run.HoldsCancelRule("oni-chain");
						Assert.True(s.Enter(eight).Ok, "enter " + eight);
						int before = s.Run.Coins;
						var d = s.WinFight(600)!;
						Assert.Equal(rareOnOffer, d.Offer.Cards.Any(c => c.Rare), $"seed {seed} {string.Join(">", route)} {eight}: a rare on offer ({Cards(d)})");
						Assert.Equal(rareOnOffer ? 0 : 50, s.Run.Coins - before, $"seed {seed} {eight}: paid, held {string.Join(",", Held(s))}");
						rows8++;
					}
		Assert.Equal(2 * 2 * 10 * 8, rows8, "every combination played");
	}

	// ---- M5, v8: saving ----

	/// <summary>The session as a save captures it at a node boundary and restores it (the record, no file: the file is the boot-flow probes').</summary>
	static RunSession Roundtrip(RunSession s, AbilityPool pool) => RunSave.Capture(s, "default-map.json").Restore(pool, Graph(), Table());

	// M5/X2: a run holding 0, 1 and 2 rules is captured and restored to the same session hash and rules, in order; the field is written only when one is held.
	[Test]
	public static void SaveCapture_HoldingZeroOneAndTwoRules_RestoresTheSameSession()
	{
		var pool = Unlocked(Pool());
		var s = Start(pool, 3);
		Walk(s, null, ToRow4);
		void Check(string[] want, string what)
		{
			var save = RunSave.Capture(s, "default-map.json");
			Assert.True(save.CancelRules.SequenceEqual(want), $"{what}: captured {string.Join(",", save.CancelRules)}");
			var restored = save.Restore(pool, Graph(), Table());
			Assert.True(restored.Run.CancelRules.Select(c => c.Id).SequenceEqual(want), $"{what}: restored {string.Join(",", Held(restored))}");
			Assert.Equal(s.StateHash(), restored.StateHash(), $"{what}: same hash");
		}
		Check(Array.Empty<string>(), "none");
		Walk(s, SimKind.CancelRule, "r5-elder-oni");
		Check(new[] { "oni-chain" }, "one");
		Walk(s, null, "r6-kappa", "r7-dojo");
		Walk(s, SimKind.CancelRule, "r8-elder-kappa");
		Check(new[] { "oni-chain", "undertow" }, "two");
	}

	// M5: a file that reads fine but names a rule this build cannot hold is refused at Restore, with a reason, leaving the pool alone.
	[Test]
	public static void ReadableButUnrestorableLists_AreRefused_WithAReason_AndTheNextRestoreStillWorks()
	{
		var pool = Pool();
		var s = Start(pool, 8);
		Walk(s, SimKind.CancelRule, ToRow4.Append("r5-elder-oni").ToArray());
		var save = RunSave.Capture(s, "default-map.json");
		foreach (var ids in new[] { new[] { "Oni-Chain" }, new[] { "oni-chain", "oni-chain" }, new[] { "oni-chain", "fox-step", "undertow" }, new[] { "oni-chain", "" }, new[] { "silver-bell" } })
		{
			try { (save with { CancelRules = ids }).Restore(pool, Graph(), Table()); }
			catch (FormatException e) { Assert.True(e.Message.Length > 0, "a reason"); continue; }
			throw new AssertFailed($"{string.Join(",", ids)}: expected FormatException");
		}
		Assert.Equal(s.StateHash(), save.Restore(pool, Graph(), Table()).StateHash(), "the good save still restores to the same hash");
		// Refused capture while an offer is open: nothing changes.
		Assert.True(s.Enter(s.Reachable()[0]).Ok, "enter r6");
		var d = s.WinFight(600)!;
		ulong h = s.StateHash();
		bool threw = false;
		try { RunSave.Capture(s, "default-map.json"); } catch (InvalidOperationException) { threw = true; }
		Assert.True(threw && s.StateHash() == h && Held(s).SequenceEqual(new[] { "oni-chain" }) && !d.Picked, "an open offer refuses a save and changes nothing");
	}

	// M5 + E23: Continue into an elder's node restarts it as it was entered: the same offer for the same seed, the same coins, the same hash after the pick.
	[Test]
	public static void ContinueAtAnElder_OffersTheSameCardsAndPaysTheSameAsTheLiveRun()
	{
		var cases = new (string[] Route, SimKind At5, string Elder, bool Undertow)[]
		{
			(new[] { "r5-elder-oni", "r6-kappa", "r7-dojo" }, SimKind.CancelRule, "r8-elder-kappa", false), // locked: 50 coins, no rule
			(new[] { "r5-elder-oni", "r6-kappa", "r7-dojo" }, SimKind.CancelRule, "r8-elder-kappa", true),  // unlocked: Undertow, no coins
			(new[] { "r5-elder-oni", "r6-kitsune", "r7-merchant" }, SimKind.CancelRule, "r8-elder-oni", false), // Oni Chain held: none offered, 50 coins
			(new[] { "r5-nine-tails", "r6-kitsune", "r7-merchant" }, SimKind.CancelRule, "r8-elder-oni", false), // Fox Step held: Oni Chain offered, no coins
			(new[] { "r5-elder-oni", "r6-kitsune", "r7-merchant" }, SimKind.Upgrade, "r8-elder-oni", false), // Oni Chain declined: offered again, no coins
		};
		foreach (var (route, at5, elder, undertow) in cases)
			for (uint seed = 1; seed <= 4; seed++)
			{
				var pool = undertow ? Unlocked(Pool()) : Pool();
				var live = Start(pool, seed);
				Walk(live, null, ToRow4);
				foreach (string id in route) Walk(live, id.StartsWith("r5-") ? at5 : SimKind.Upgrade, id);
				Assert.True(live.Enter(elder).Ok, "enter " + elder);
				var back = Roundtrip(live, pool);
				Assert.Equal(live.StateHash(), back.StateHash(), $"{elder} seed {seed}: restored at the node's entry");
				var a = live.WinFight(600)!; var b = back.WinFight(600)!;
				string what = $"{elder} seed {seed} after {string.Join(">", route)}";
				Assert.Equal(Cards(a), Cards(b), what + ": the same offer");
				Assert.True(live.Run.Coins == back.Run.Coins && live.CoinsWon == back.CoinsWon, what + $": the same pay ({live.CoinsWon} / {back.CoinsWon})");
				PickKind(a, SimKind.CancelRule); PickKind(b, SimKind.CancelRule);
				Assert.True(live.CompleteNode().Ok && back.CompleteNode().Ok, "complete");
				Assert.Equal(live.StateHash(), back.StateHash(), what + ": the same run after the pick");
			}
	}

	// F3/E23 at the ends of the seed range: no overflow, no refusal, and the same walk twice is the same.
	[Test]
	public static void ExtremeSeeds_WalkThroughBothElders_Deterministically()
	{
		foreach (uint seed in new[] { 0u, 1u, uint.MaxValue, uint.MaxValue - 4, 0x80000000u })
		{
			string Run()
			{
				var s = Start(Unlocked(Pool()), seed);
				var log = new List<string>();
				foreach (string id in ToRow4.Concat(new[] { "r5-nine-tails", "r6-kappa", "r7-merchant", "r8-elder-oni" }))
				{
					Assert.True(s.Enter(id).Ok, $"seed {seed}: enter {id}");
					if (!IsStop(s) && s.WinFight(600) is { } d) { log.Add(Cards(d)); PickKind(d, SimKind.CancelRule); }
					Assert.True(s.CompleteNode().Ok, id);
					log.Add(s.StateHash().ToString());
				}
				return string.Join(";", log) + ";" + string.Join(",", Held(s));
			}
			Assert.Equal(Run(), Run(), $"seed {seed}: the same walk twice");
		}
	}

	// ---- the reward screen ----

	static (RewardScreen Screen, RunState Run, RewardDraft Draft, List<(int, string?)> Picks) Present(Node host, AbilityPool pool, string node = "r5-elder-oni", uint seed = 4, bool story = true)
	{
		var run = RunState.NewRun(pool);
		var ctx = Table().Resolve(Graph().Node(node)).Context;
		var draft = RewardDraft.Build(run, pool, ctx.DraftYokai, seed, ctx.Elder, elderId: ctx.BindingName);
		var screen = GD.Load<PackedScene>("res://scenes/ui/reward_screen.tscn").Instantiate<RewardScreen>();
		host.AddChild(screen);
		var picks = new List<(int, string?)>();
		screen.Picked += (c, t) => picks.Add((c, t));
		screen.Present(new DraftRewardSource(draft, run, story: new StubRewardSource()));
		return (screen, run, draft, picks);
	}

	// A12: plain text first, frame data behind the toggle; no target step for a rule (X1); keyboard, pad and mouse take it.
	[Test]
	public static void RewardScreen_TakesTheRuleByKeyboardPadAndClick_WithNoTargetStep_AndTheFramesToggleShowsItsPath(Node host)
	{
		var pool = Pool();
		var data = pool.CancelRule("oni-chain")!;
		foreach (string how in new[] { "key", "pad", "click" })
		{
			var (screen, run, draft, picks) = Present(host, pool);
			int at = draft.Offer.Cards.ToList().FindIndex(c => c.Kind == SimKind.CancelRule);
			Assert.Equal(0, at, "the rule leads the offer");
			var card = screen.GetNode<RewardCard>("Cards/Card1");
			Assert.Equal(data.CardPlain, UiFind.Get<Label>(card, "Plain").Text, "plain text first");
			Assert.True(!UiFind.Get<Control>(card, "FrameBox").Visible, "frame data hidden to begin with");
			screen.ToggleFrameData();
			Assert.True(screen.FrameDataVisible && UiFind.Get<Control>(card, "FrameBox").Visible && UiFind.Get<Label>(card, "FrameText").Text == data.CardFrames, "the toggle shows the cancel path");
			screen.ToggleFrameData();
			Assert.True(!screen.FrameDataVisible, "and hides it");
			switch (how)
			{
				case "key": screen._UnhandledInput(new InputEventKey { Keycode = Key.Enter, Pressed = true }); break;
				case "pad": screen._UnhandledInput(new InputEventJoypadButton { ButtonIndex = JoyButton.A, Pressed = true }); break;
				default: card.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true }); break;
			}
			Assert.True(screen.Step == RewardStep.Cards && !screen.Showing, $"{how}: no target step, the screen closes");
			Assert.True(picks.SequenceEqual(new[] { (0, (string?)null) }), $"{how}: picked card 0 with no target");
			Assert.True(run.CancelRules.Select(c => c.Id).SequenceEqual(new[] { "oni-chain" }) && draft.Picked, $"{how}: Ryo holds it");
			screen.Free();
		}
		// The modifier card still asks for a target, and Back returns to the cards with the rule still takeable.
		var (s2, run2, d2, p2) = Present(host, pool, "r5-elder-oni", 1);
		int mod = d2.Offer.Cards.ToList().FindIndex(c => c.Kind == SimKind.Modifier);
		if (mod >= 0)
		{
			s2.Move(mod); s2.Confirm();
			Assert.True(s2.Step == RewardStep.Target && p2.Count == 0, "a modifier card opens the target step");
			s2.Back();
			Assert.True(s2.Step == RewardStep.Cards && s2.Showing, "Back");
			s2.Move(-mod); s2.Confirm();
			Assert.True(p2.Count == 1 && run2.CancelRules.Count == 1, "then the rule is taken");
		}
		s2.Free();
	}

	// A12 + the 720p floor: every cancel card, and a rare new-special card, fits the reserved areas of the card like every other card.
	[Test]
	public static void RewardScreen_CancelAndRareCardCopyFitsTheReservedAreas(Node host)
	{
		var pool = Unlocked(Pool());
		var (screen, _, _, _) = Present(host, pool);
		var card = screen.GetNode<RewardCard>("Cards/Card1");
		void Fits(Ui.CardView view)
		{
			card.Bind(view); card.SetState(true, false);
			foreach (var (area, max) in new[] { ("Name", 89f), ("Source", 44f), ("Tag", 48f), ("Plain", 152f) })
			{
				var label = UiFind.Get<Label>(card, area);
				float height = label.GetLineCount() * label.GetLineHeight() + (label.GetLineCount() - 1) * label.GetThemeConstant("line_spacing");
				Assert.True(height <= max, $"{view.Id} {area} needs {height}px, reserved {max} ('{label.Text}')");
				Assert.True(label.GetThemeFontSize("font_size") * 2f / 3 >= 17, $"{view.Id} {area}: 720p minimum text");
			}
			card.SetState(true, true);
			var frames = UiFind.Get<Label>(card, "FrameText");
			float fh = frames.GetLineCount() * frames.GetLineHeight() + (frames.GetLineCount() - 1) * frames.GetThemeConstant("line_spacing");
			Assert.True(fh <= 164, $"{view.Id}: frame text needs {fh}px ('{frames.Text}')");
		}
		foreach (var (node, rule) in new[] { ("r5-nine-tails", "fox-step"), ("r5-elder-oni", "oni-chain"), ("r8-elder-kappa", "undertow") })
		{
			var (s, _, d, _) = Present(host, pool, node);
			var view = new DraftRewardSource(d, RunState.NewRun(pool), story: new StubRewardSource()).Offer.Cards.Single(c => c.Kind == Ui.CardKind.CancelRule);
			Assert.Equal(rule, view.Id, node);
			Fits(view);
			s.Free();
		}
		// A rare new special: the tag grows to "RARE - NEW - Lv 1".
		Fits(new Ui.CardView(Ui.CardKind.NewMove, "fox-mirage", "Fox Mirage", "Kitsune - special", "Teleport behind your opponent.", "Startup 18, recovery 10", "RARE - NEW - Lv 1", Rare: true));
		screen.Free();
	}

	// A12 + A7: an offer of two cards, and of one, moves with the cursor and takes the card under it; a card that was a rule leaves nothing behind when rebound.
	[Test]
	public static void RewardScreen_TwoCardAndOneCardOffers_CursorWraps_AndARuleCardRebindLeavesNoGold(Node host)
	{
		var full = Pool();
		var pool = new AbilityPool(full.Specials.Where(x => x.Source != "oni"), full.Modifiers.Where(x => x.Source != "oni"), full.Notes, full.CancelRules);
		var (two, run2, d2, p2) = Present(host, pool, "r5-elder-oni", 3);
		Assert.Equal(2, d2.Offer.Cards.Count, "setup: the rule and the upgrade");
		two.Move(1); Assert.Equal(1, two.CardCursor, "to the upgrade");
		two.Move(1); Assert.Equal(0, two.CardCursor, "wraps to the rule");
		two.Move(-1); Assert.Equal(1, two.CardCursor, "and back");
		two.Confirm();
		Assert.True(p2.Count == 1 && p2[0].Item1 == 1 && run2.CancelRules.Count == 0 && run2.Owned.Any(o => o.Special.Level == 2), "the upgrade under the cursor was taken, not the rule");
		two.Free();

		var maxed = RunState.NewRun(pool);
		while (maxed.Owned.Any(o => maxed.CanLevelUp(o.Slot))) maxed.LevelUp(maxed.Owned.First(o => maxed.CanLevelUp(o.Slot)).Slot);
		var lone = RewardDraft.Build(maxed, pool, "oni", 2, elder: true, elderId: "elder-oni");
		var screen = GD.Load<PackedScene>("res://scenes/ui/reward_screen.tscn").Instantiate<RewardScreen>();
		host.AddChild(screen);
		screen.Present(new DraftRewardSource(lone, maxed, story: new StubRewardSource()));
		screen.Move(1); screen.Move(-1);
		Assert.Equal(0, screen.CardCursor, "one card: the cursor stays on it");
		screen.Confirm();
		Assert.True(!screen.Showing && maxed.CancelRules.Count == 1, "the lone rule is taken");
		screen.Free();

		// Rebind: a rare rule card, then an ordinary new-special card in the same node.
		var (s3, _, _, _) = Present(host, Pool());
		var card = s3.GetNode<RewardCard>("Cards/Card1");
		var crest = UiFind.Get<SealStamp>(card, "Crest");
		card.Bind(new Ui.CardView(Ui.CardKind.CancelRule, "oni-chain", "Oni Chain", "Oni - cancel rule", "Cancel a heavy normal into a special.", "Heavy punch/kick", "RARE - CANCEL RULE", Rare: true));
		Assert.True(crest.Fill == RewardCard.RareGold, "setup: gold");
		card.Bind(new Ui.CardView(Ui.CardKind.NewMove, "foxfire", "Foxfire", "Kitsune - special", "A slow fireball.", "15/4/30", "NEW - Lv 1"));
		Assert.True(crest.Fill == Color.FromHtml("B5332B") && UiFind.Get<TextureRect>(card, "Illustration").Texture == AbilityArt.ForId("foxfire"), "an ordinary card is not left gold or on the diamond");
		s3.Free();
	}

	// A9 + R8 (PROVISIONAL): a rare second special counts as the elder's rare, so an elder that offers one pays no coins even with its rule held; the same special
	// is never offered by a duel. No yokai has a second special in data today, so this is a TEST FIXTURE special (as the capture tool's).
	[Test]
	public static void RareSecondSpecial_IsTheRowEightRare_AndOnlyAnElderOffersIt()
	{
		var node = JsonNode.Parse(File.ReadAllText(Path.Combine(ContentPaths.FixturesDir, "rewards", "test-foxfire.json")))!.AsObject();
		node["id"] = "test-oni-rare"; node["name"] = "Test Rare Special"; node["source"] = "oni"; node["rarity"] = "rare";
		node["input"]!["kata"]!["slot"] = "D"; node["input"]!["kihon"]!["slot"] = "D";
		var rare = SpecialLoader.Parse(node.ToJsonString(), "test-oni-rare");
		var full = Pool();
		var pool = new AbilityPool(full.Specials.Append(rare), full.Modifiers, full.Notes, full.CancelRules);
		for (uint seed = 1; seed <= 20; seed++)
		{
			// A duel (not an elder) of the Oni never offers it (A9).
			Assert.True(!RewardDraft.Build(RunState.NewRun(pool), pool, "oni", seed).Offer.Cards.Any(c => c.AbilityId == "test-oni-rare" || c.Rare), $"seed {seed}: a duel offers no rare");
			// A row-8 Elder Oni with Oni Chain already held: the rare special is the rare, so no fallback coins.
			var s = Start(pool, seed);
			Walk(s, SimKind.Upgrade, ToRow4);
			s.Run.HoldCancelRule(pool.CancelRule("oni-chain")!);
			s.Map.FreeTravel = true;
			Assert.True(s.Enter("r8-elder-oni").Ok, "enter");
			int before = s.Run.Coins;
			var d = s.WinFight(600)!;
			var cards = d.Offer.Cards;
			Assert.True(!cards.Any(c => c.Kind == SimKind.CancelRule), "Oni Chain is held");
			Assert.True(cards.Any(c => c.AbilityId == "test-oni-rare" && c.Rare && c.Badge == "NEW"), $"seed {seed}: the elder offers its rare special ({Cards(d)})");
			Assert.Equal(0, s.Run.Coins - before, $"seed {seed}: a rare on offer, so no coins");
		}
	}

	static void SortAll(Node n)
	{
		foreach (var c in n.GetChildren()) SortAll(c);
		if (n is Container ct) ct.Notification((int)Container.NotificationSortChildren);
	}

	// Layout: the map's detail panel (a 920 x 168 box in the scene) must still hold an elder's name, twist line and the longer row-8 reward line without growing.
	[Test]
	public static void MapDetailPanel_HoldsTheRowEightElderLine_WithoutGrowingPastItsBox(Node host)
	{
		var sess = Start(Pool(), 5);
		Walk(sess, SimKind.Upgrade, ToRow4.Concat(new[] { "r5-elder-oni", "r6-kappa", "r7-dojo" }).ToArray());
		var screen = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		var stage = new Control { Size = new Vector2(1920, 1080) };
		host.AddChild(stage); stage.AddChild(screen);
		screen.SetView(new SessionMapView(sess));
		var detail = UiFind.Get<Control>(screen, "Detail");
		foreach (string id in new[] { "r8-elder-kappa", "r8-elder-oni" })
		{
			screen.SetView(new SessionMapView(sess)); // rebind
			// Move the cursor to the node (ui_left / ui_right between the two row-8 nodes).
			for (int i = 0; i < 3 && screen.CursorId != id; i++) screen._UnhandledInput(new InputEventAction { Action = "ui_down", Pressed = true });
			Assert.Equal(id, screen.CursorId, "cursor on the node");
			detail.Size = new Vector2(920, 168); // the scene's box
			for (int pass = 0; pass < 3; pass++) { SortAll(screen); detail.Size = new Vector2(920, 168); }
			// A label's cached minimum height is stale until a frame has run; measure what each shows at the width it was given (as the card-copy test does).
			float Height(Label l) => l.Visible ? l.GetLineCount() * l.GetLineHeight() + (l.GetLineCount() - 1) * l.GetThemeConstant("line_spacing") : 0f;
			var lines = new[] { "DetailName", "DetailTwist", "DetailReward" }.Select(nm => UiFind.Get<Label>(screen, nm)).Where(l => l.Visible).ToArray();
			float need = lines.Sum(Height) + 4 * (lines.Length - 1) + detail.GetThemeStylebox("panel").GetMinimumSize().Y;
			string text = $"{screen.DetailNameText} / {UiFind.Get<Label>(screen, "DetailTwist").Text} / {screen.DetailRewardText}";
			Assert.True(need <= 168.5f, $"{id}: the detail panel needs {need}px, its box is 168 ({text})");
		}
		stage.QueueFree();
	}

	// A9, A12: an offer with the rule and a rare second special: both wear the rare tag, the cursor reaches the special, and taking it equips it with no target step and holds no rule.
	[Test]
	public static void RewardScreen_RuleAndRareSpecialInOneOffer_BothMarkedRare_TheSpecialIsTakenByKeyboardNotTheRule(Node host)
	{
		var node = JsonNode.Parse(File.ReadAllText(Path.Combine(ContentPaths.FixturesDir, "rewards", "test-foxfire.json")))!.AsObject();
		node["id"] = "test-oni-rare"; node["name"] = "Test Rare Special"; node["source"] = "oni"; node["rarity"] = "rare";
		node["input"]!["kata"]!["slot"] = "D"; node["input"]!["kihon"]!["slot"] = "D";
		var rare = SpecialLoader.Parse(node.ToJsonString(), "test-oni-rare");
		var full = Pool();
		var pool = new AbilityPool(full.Specials.Append(rare), full.Modifiers, full.Notes, full.CancelRules);
		uint seed = 0;
		for (uint x = 1; x < 64 && seed == 0; x++)
		{
			var d = RewardDraft.Build(RunState.NewRun(pool), pool, "oni", x, elder: true, elderId: "elder-oni");
			if (d.Offer.Cards.Any(c => c.Kind == SimKind.CancelRule) && d.Offer.Cards.Any(c => c.Kind == SimKind.NewSpecial && c.Rare)) seed = x;
		}
		Assert.True(seed != 0, "setup: an Oni elder offer with both");
		var (screen, run, draft, picks) = Present(host, pool, "r5-elder-oni", seed);
		var views = draft.Offer.Cards.Select(c => c.Kind).ToList();
		int at = views.IndexOf(SimKind.NewSpecial);
		var card = screen.GetNode<RewardCard>($"Cards/Card{at + 1}");
		screen.Move(at);
		Assert.Equal(at, screen.CardCursor, "the cursor reaches the special");
		Assert.True(UiFind.Get<Label>(card, "Tag").Text == "RARE - NEW - Lv 1" && UiFind.Get<Label>(screen.GetNode<RewardCard>("Cards/Card1"), "Tag").Text.StartsWith("RARE"), "both cards are tagged rare");
		Assert.True(UiFind.Get<SealStamp>(card, "Crest").Fill == RewardCard.RareGold && UiFind.Get<SealStamp>(screen.GetNode<RewardCard>("Cards/Card1"), "Crest").Fill == RewardCard.RareGold, "both wear the rare colour");
		screen._UnhandledInput(new InputEventKey { Keycode = Key.Enter, Pressed = true });
		Assert.True(screen.Step == RewardStep.Cards && !screen.Showing && picks.Count == 1 && picks[0].Item1 == at && picks[0].Item2 is null, "taken, no target step");
		Assert.True(run[SpecialSlot.D]?.Data.Id == "test-oni-rare" && run.CancelRules.Count == 0, "the special is in slot D and no rule is held");
		screen.Free();
	}

	// ---- the boot flow: quit, Continue, restart ----

	static void WithBoot(Node runner, string saveFile, Action<GameFlow> body)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { }; flow.FixedRunSeed = 186; flow.SaveFile = saveFile;
		flow.ConfigureFight = f => f.ExternalDrive = true;
		try { runner.AddChild(flow); body(flow); }
		finally
		{
			runner.RemoveChild(flow); flow.QueueFree(); MenuSettings.Reset();
			FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe;
		}
	}

	static void Press(Button b) => b.EmitSignal(BaseButton.SignalName.Pressed);

	// X1, X2, M5, M3: through the real flow, a rule taken at the row-5 elder is on the saved run, comes back through Continue into the next fight on Ryo,
	// and a restart starts a new run that holds none (and writes none).
	[Test]
	public static void BootFlow_RuleTakenAtTheElder_SurvivesQuitAndContinue_ARestartHoldsNone(Node runner)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-186-probe-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		string path = Path.Combine(dir, "probe-slot.json");
		try
		{
			WithBoot(runner, path, flow =>
			{
				Press(flow.Title.StartButton); Press(flow.ControlSelect.Start);
				var s = flow.Session!;
				Assert.Equal(0, s.Run.CancelRules.Count, "X1: a started run holds none");
				foreach (string id in ToRow4.Append("r5-elder-oni"))
				{
					Assert.True(flow.ChooseNode(id).Ok, "enter " + id);
					if (!IsStop(s) && s.WinFight(700) is { } d) PickKind(d, SimKind.CancelRule);
					Assert.True(s.CompleteNode().Ok, "complete " + id);
				}
				Assert.True(Held(s).SequenceEqual(new[] { "oni-chain" }), "Oni Chain taken at the elder");
				Assert.True(flow.ChooseNode("r6-kappa").Ok, "the next node is entered (saved at its entry)");
				Assert.True(flow.Fight!.Match.P1.CancelRules.Select(c => c.Id).SequenceEqual(new[] { "oni-chain" }), "Ryo holds it in the fight");
				flow.ReturnToTitle();
			});
			Assert.True(File.ReadAllText(path).Contains("\"cancel_rules\""), "the file names the held rule");
			WithBoot(runner, path, flow =>
			{
				Assert.True(!flow.Title.ContinueButton.Disabled, "Continue is offered: " + flow.Title.DisabledReason(TitleEntry.Continue));
				Press(flow.Title.ContinueButton);
				var s = flow.Session!;
				Assert.True(s.Node?.NodeId == "r6-kappa" && Held(s).SequenceEqual(new[] { "oni-chain" }), "back in the node, holding the rule");
				Assert.True(flow.Fight!.Match.P1.CancelRules.Select(c => c.Id).SequenceEqual(new[] { "oni-chain" }), "and Ryo holds it in the restarted fight");
				Assert.True(flow.RestartRun(), "restart from the fight");
				var fresh = flow.Session!;
				Assert.True(!ReferenceEquals(fresh, s) && fresh.Run.CancelRules.Count == 0 && fresh.Map.Visited.Count == 0, "a new run holds no rule (X1)");
				Assert.True(flow.Saved.Run is { CancelRules.Count: 0 } && !File.ReadAllText(path).Contains("cancel_rules"), "and the file holds none");
				Assert.True(flow.ChooseNode("r1-apprentice").Ok && flow.Fight!.Match.P1.CancelRules.Count == 0, "its first fight: Ryo holds none");
			});
		}
		finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
	}

	static string TempSlot(out string dir)
	{
		dir = Path.Combine(Path.GetTempPath(), "yokai-186-probe-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		return Path.Combine(dir, "probe-slot.json");
	}

	/// <summary>Plays a run in the boot flow along <paramref name="route"/> (taking a rule wherever one is offered), then back to the title with the file left as the flow wrote it.</summary>
	static (ulong Hash, string[] Held) PlayRunTo(Node runner, string path, string[] route, bool undertow)
	{
		ulong hash = 0; string[] held = Array.Empty<string>();
		WithBoot(runner, path, flow =>
		{
			if (undertow) flow.Write(flow.Saved.WithUnlock("undertow"));
			Press(flow.Title.StartButton); Press(flow.ControlSelect.Start);
			var s = flow.Session!;
			foreach (string id in route)
			{
				Assert.True(flow.ChooseNode(id).Ok, "enter " + id);
				if (!IsStop(s) && s.WinFight(700) is { } d) PickKind(d, SimKind.CancelRule);
				Assert.True(s.CompleteNode().Ok, "complete " + id);
			}
			hash = s.StateHash(); held = Held(s);
			flow.ReturnToTitle();
		});
		return (hash, held);
	}

	static readonly string[] ToRow5 = ToRow4.Append("r5-elder-oni").ToArray();
	static readonly string[] ToRow8 = ToRow5.Concat(new[] { "r6-kappa", "r7-dojo", "r8-elder-kappa" }).ToArray();

	// M5, X2: the file the flow writes at a node boundary names the rules held (and only when one is), and Continue gives back the same session hash and rules.
	[Test]
	public static void BootFlow_SaveFile_HoldingZeroOneAndTwoRules_ContinuesToTheSameSession(Node runner)
	{
		foreach (var (route, want) in new[] { (ToRow4, Array.Empty<string>()), (ToRow5, new[] { "oni-chain" }), (ToRow8, new[] { "oni-chain", "undertow" }) })
		{
			string path = TempSlot(out string dir);
			try
			{
				var (hash, held) = PlayRunTo(runner, path, route, undertow: true);
				Assert.True(held.SequenceEqual(want), $"{string.Join(",", want)}: setup, held {string.Join(",", held)}");
				string text = File.ReadAllText(path);
				Assert.True(text.Contains("\"version\": 8"), "written as version 8");
				Assert.Equal(want.Length > 0, text.Contains("\"cancel_rules\""), $"{want.Length} held: the field is there only when one is");
				foreach (string id in want) Assert.True(text.Contains($"\"{id}\""), $"the file names {id}");
				WithBoot(runner, path, flow =>
				{
					Assert.True(!flow.Title.ContinueButton.Disabled, "Continue lit: " + flow.Title.DisabledReason(TitleEntry.Continue));
					Press(flow.Title.ContinueButton);
					var s = flow.Session!;
					Assert.True(Held(s).SequenceEqual(want), $"continued holding {string.Join(",", Held(s))}");
					Assert.Equal(hash, s.StateHash(), "the same session hash after Continue");
				});
			}
			finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
		}
	}

	// M5: a version 7 file (no cancel_rules) continues and holds none: the run at row 5's entry and the run with its rule edited out, both marked version 7.
	[Test]
	public static void BootFlow_VersionSevenFile_Continues_HoldingNoRule(Node runner)
	{
		string path = TempSlot(out string dir);
		try
		{
			var (hash0, _) = PlayRunTo(runner, path, ToRow4, undertow: false);
			string text = File.ReadAllText(path);
			Assert.True(text.Contains("\"version\": 8") && !text.Contains("cancel_rules"), "setup: a v8 file with no rule differs from v7 only by its number");
			File.WriteAllText(path, text.Replace("\"version\": 8", "\"version\": 7"));
			WithBoot(runner, path, flow =>
			{
				Assert.True(!flow.Title.ContinueButton.Disabled, "the older file's run is offered: " + flow.Title.DisabledReason(TitleEntry.Continue));
				Press(flow.Title.ContinueButton);
				Assert.True(flow.Session!.Run.CancelRules.Count == 0 && flow.Session.StateHash() == hash0, "holds none, the same run");
			});
		}
		finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
	}

	// M5/M4: a cancel_rules field that is not a list of ids drops the run (Continue greys) and keeps unlocks; a list the build cannot hold greys Continue with a reason;
	// an empty list is a run holding none.
	[Test]
	public static void BootFlow_CorruptCancelRulesField_GreysContinue_KeepsUnlocks_OrContinuesHoldingNone(Node runner)
	{
		string path = TempSlot(out string dir);
		try
		{
			PlayRunTo(runner, path, ToRow5, undertow: true);
			string good = File.ReadAllText(path);
			var field = new System.Text.RegularExpressions.Regex("\"cancel_rules\": \\[[^\\]]*\\]");
			Assert.True(field.IsMatch(good), "setup: the field is in the file");
			void Boot(string bad, Action<GameFlow> check)
			{
				File.WriteAllText(path, field.Replace(good, "\"cancel_rules\": " + bad));
				WithBoot(runner, path, flow =>
				{
					Assert.True(flow.Saved.Unlocks.Contains("undertow"), $"{bad}: unlocks kept");
					check(flow);
				});
			}
			foreach (string bad in new[] { "\"oni-chain\"", "{ \"a\": 1 }", "7", "[ 1 ]", "[ \"\" ]", "[ null ]", "[ \"oni-chain\", 2 ]" })
				Boot(bad, flow => Assert.True(flow.Title.ContinueButton.Disabled && flow.Saved.Run is null, $"{bad}: not a list of ids, the run is dropped and Continue is grey"));
			foreach (string bad in new[] { "[ \"oni-chain\", \"oni-chain\" ]", "[ \"oni-chain\", \"fox-step\", \"undertow\" ]", "[ \"silver-bell\" ]", "[ \"Oni-Chain\" ]" })
				Boot(bad, flow =>
				{
					Assert.True(flow.Title.ContinueButton.Disabled && flow.Title.DisabledReason(TitleEntry.Continue).Contains("cannot be continued"), $"{bad}: Continue is grey with a reason ({flow.Title.DisabledReason(TitleEntry.Continue)})");
					Assert.True(!flow.ContinueRun() && flow.Session is null, $"{bad}: a forced Continue is refused");
				});
			Boot("[]", flow =>
			{
				Assert.True(!flow.Title.ContinueButton.Disabled, "an empty list: " + flow.Title.DisabledReason(TitleEntry.Continue));
				Press(flow.Title.ContinueButton);
				Assert.True(flow.Session!.Run.CancelRules.Count == 0, "holds none");
			});
		}
		finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
	}

	// ---- the bot and the whole-run harness ----

	// The whole-run harness walks the real elders (cancel cards in their offers) with stubbed fights: no refusal, same bytes twice, the newcomer bot
	// takes by its own order, a bot that ranks the rule first takes it, and Ryo holds it in the real fights that follow.
	[Test]
	public static void WholeRunHarness_WithCancelCardsInTheOffers_FinishesEveryRun_DeterministicallyAndHoldsATakenRule()
	{
		var content = HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
		var bot = BotConfig.LoadDefault(ContentPaths.DataDir);
		FightRecord Stub(RunSession s) => new() { Profile = "stub", Won = true, Ticks = 600, HealthLeft = Math.Max(1, s.Run.Health - 10), DamageTaken = 10, Bot = new BotStats() };
		var takesRules = bot with { Run = bot.Run with { RewardOrder = new[] { SimKind.CancelRule, SimKind.Upgrade, SimKind.NewSpecial, SimKind.Modifier } } };
		int elders = 0, taken = 0;
		for (uint seed = 1; seed <= 24; seed++)
		{
			var a = RunHarness.PlayRun(content, bot, seed, (_, _, s, _) => Stub(s));
			var b = RunHarness.PlayRun(content, bot, seed, (_, _, s, _) => Stub(s));
			Assert.Equal(a.SessionHash, b.SessionHash, $"seed {seed}: the same run twice");
			Assert.True(a.Won, $"seed {seed}: stub fights are won, so the run reaches the end");
			foreach (var n in a.Nodes.Where(n => n.Elder && n.Card is not null))
			{
				elders++;
				bool locked = n.Id == "r8-elder-kappa"; // Undertow starts locked (X4, A11): a first run's Elder Kappa has no rule to offer
				Assert.Equal(!locked, n.Card!.Offered.Any(o => o.StartsWith("CANCEL ")), $"seed {seed} {n.Id}: the offer holds its rule unless it is Undertow ({string.Join(",", n.Card.Offered)})");
				Assert.True(n.Card.Badge != "CANCEL", $"seed {seed} {n.Id}: the newcomer bot takes by its own order, not the rule");
				if (n.Row == 8) Assert.Equal(locked ? 50 : 0, n.CoinsWon, $"seed {seed} {n.Id}: R8 coins (PROVISIONAL fallback only with no rare)");
			}
			var t = RunHarness.PlayRun(content, takesRules, seed, (_, _, s, _) => Stub(s));
			Assert.True(t.Won, $"seed {seed}: a bot that takes rules still finishes");
			taken += t.Nodes.Count(n => n.Card?.Badge == "CANCEL");
		}
		Assert.True(elders >= 48 && taken >= 24, $"elders met {elders}; rules taken by the rule-first bot {taken}");
		// Real fights: Ryo holds, in each one after the first elder, what the run holds.
		var heldAt = new List<(string Node, int Run, int Ryo)>();
		FightRecord Real(HarnessContent c, BotConfig b, RunSession s, uint seed)
		{
			if (s.Node!.Row < 5) return Stub(s); // a newcomer bot rarely lives to row 5; the early rows are stubbed so the real fights are the elder's and after
			var m = RunHarness.NewNodeMatch(c, b, s);
			heldAt.Add((s.Node!.NodeId, s.Run.CancelRules.Count, m.P1.CancelRules.Count));
			return RunHarness.FightNode(c, b, s, seed);
		}
		for (uint seed = 1; seed <= 6; seed++) RunHarness.PlayRun(content, takesRules, seed, Real);
		Assert.True(heldAt.All(h => h.Run == h.Ryo), "every real fight: Ryo holds what the run holds: " + string.Join(", ", heldAt.Select(h => $"{h.Node}:{h.Run}/{h.Ryo}")));
		Assert.True(heldAt.Any(h => h.Ryo > 0), "a rule taken at an elder is held in a later real fight (no bot run in 12 reached one: " + heldAt.Count + " fights)");
	}

	// ---- replay ----

	// F3, X2: a fight recorded with the rule held replays with it held (the header carries it) and every tick's hash matches.
	[Test]
	public static void Replay_OfAFightWithAHeldRule_CarriesTheRule()
	{
		var pool = Pool();
		var s = Start(pool, 7);
		Walk(s, SimKind.CancelRule, ToRow4.Append("r5-elder-oni").ToArray());
		Assert.True(s.Enter("r6-kappa").Ok, "enter");
		var m = NodeFight.NewMatch(FightScene.Sources, s.Run, s, s.Node!.Fighter, ControlScheme.Kihon);
		var rec = ReplayRecorder.Begin(m, null, 1, s.Run);
		for (int i = 0; i < 180; i++)
		{
			var p1 = new FighterInput(i % 5 == 0 ? InputBits.Right : i % 7 == 0 ? InputBits.HeavyPunch : InputBits.None);
			m.Step(p1, FighterInput.None);
			rec.Record(p1, FighterInput.None, m.StateHash());
		}
		var done = rec.Finish(m);
		Assert.True(done.Header.Fighters[0].CancelRules.Select(c => c.Id).SequenceEqual(new[] { "oni-chain" }) && done.Header.Fighters[1].CancelRules.Count == 0, "the recording names Ryo's rule");
		var cat = ReplayCatalog.FromRepo(Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + ".."));
		var report = Replayer.Run(done, cat);
		Assert.True(report.Ok, "replays: " + report.Error);
		var rebuilt = Replayer.Build(done.Header, cat);
		Assert.True(rebuilt.P1.CancelRules.Select(c => c.Id).SequenceEqual(new[] { "oni-chain" }) && rebuilt.P1.CancelRules[0] == m.P1.CancelRules[0], "the replayed Ryo holds the same rule");
	}
}
