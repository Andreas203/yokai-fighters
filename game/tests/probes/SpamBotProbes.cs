using System;
using System.IO;
using System.Linq;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-253 probes. Expectations come from data/schema/harness-bot.schema.json and harness/results/whole-run/README.md (spam-bot fields) and T2.</summary>
public static class SpamBotProbes
{
	static HarnessContent? _content;
	static HarnessContent Content => _content ??= HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static BotConfig Bot => BotConfig.LoadDefault(ContentPaths.DataDir);
	static CardTarget T(SpecialSlot s, string id = "x") => new(s, id, id, 1, "", "");
	static CardView C(int i, CardKind k, string id, params CardTarget[] t) => new(i, k, k.ToString(), id, id, "", "", "oni", t);

	// Contract 2 (schema special_slot_only: "never replaces its special with a NEW card"; the designer's rule for prefer_special).
	// Round 2: the first version offered a NEW card alone; A7 ("one is always an upgrade to an owned move") and the compulsory pick
	// make that offer unreachable while any owned special can level up, so the offer here is A7-valid: the NEW card for the filled
	// spam slot beside an upgrade (and a modifier). Whatever the reward order, the bot never lets the NEW card replace slot A.
	[Test]
	public static void NewCardForFilledSpamSlot_BesideAnUpgrade_IsNeverPicked_InAnyOrder()
	{
		var run = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5).Run; // A, B filled
		var offer = new RewardOffer("oni", new[]
		{
			C(0, CardKind.NewSpecial, "foxfire", T(SpecialSlot.A, "foxfire")),
			C(1, CardKind.Upgrade, "up", T(SpecialSlot.A), T(SpecialSlot.B)),
			C(2, CardKind.Modifier, "mod", T(SpecialSlot.A), T(SpecialSlot.B)),
		});
		var orders = new[]
		{
			new[] { CardKind.NewSpecial, CardKind.Upgrade, CardKind.Modifier }, new[] { CardKind.NewSpecial, CardKind.Modifier, CardKind.Upgrade },
			new[] { CardKind.Upgrade, CardKind.NewSpecial, CardKind.Modifier }, new[] { CardKind.Modifier, CardKind.NewSpecial, CardKind.Upgrade },
		};
		foreach (var order in orders)
			foreach (bool prefer in new[] { false, true })
			{
				var cfg = Bot.Run with { RewardOrder = order, PreferSpecial = prefer ? "iron-club-charge" : null };
				var pick = new RunBot(cfg, 1, SpecialSlot.A).ChooseCard(offer, run);
				Assert.True(pick.Card != 0, $"NEW card picked over spam slot A (order {string.Join(">", order)}, prefer {prefer}): {pick}");
				Assert.True(pick.Target == SpecialSlot.A, $"the spammed special is the one invested in: {pick}");
			}
	}

	// Round 2: a NEW card for the filled spam slot beside a NEW card for another (empty) slot: the other one may be taken, never slot A.
	[Test]
	public static void TwoNewCards_OneForFilledSpamSlot_NeverTheSpamOne()
	{
		var run = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5).Run; // C empty
		var offer = new RewardOffer("oni", new[]
		{
			C(0, CardKind.NewSpecial, "foxfire", T(SpecialSlot.A, "foxfire")),
			C(1, CardKind.NewSpecial, "other", T(SpecialSlot.C, "other")),
		});
		var cfg = Bot.Run with { RewardOrder = new[] { CardKind.NewSpecial, CardKind.Upgrade, CardKind.Modifier } };
		var pick = new RunBot(cfg, 1, SpecialSlot.A).ChooseCard(offer, run);
		Assert.True(!(pick.Card == 0 || pick.Target == SpecialSlot.A), $"NEW card put on spam slot A: {pick}");
	}

	// Round 2, prefer_special's own card is the one allowed replacement: it lands on the filled spam slot.
	[Test]
	public static void PreferredCard_MayReplaceFilledSpamSlot()
	{
		var run = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5).Run;
		var offer = new RewardOffer("oni", new[]
		{
			C(0, CardKind.Upgrade, "up", T(SpecialSlot.A)),
			C(1, CardKind.NewSpecial, "iron-club-charge", T(SpecialSlot.A, "iron-club-charge")),
		});
		var pick = new RunBot(Bot.Run with { PreferSpecial = "iron-club-charge" }, 1, SpecialSlot.A).ChooseCard(offer, run);
		Assert.Equal((1, (SpecialSlot?)SpecialSlot.A), pick, "the preferred special goes on the spam slot");
	}

	// Round 2: prefer_special set, another NEW card for the EMPTY spam slot, new-first order: it must wait empty (rule: no other NEW card).
	[Test]
	public static void PreferSet_OtherNewCardForEmptySpamSlot_WaitsEmpty()
	{
		var run = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5).Run;
		var offer = new RewardOffer("oni", new[]
		{
			C(0, CardKind.NewSpecial, "foxfire", T(SpecialSlot.C, "foxfire")),
			C(1, CardKind.Upgrade, "up", T(SpecialSlot.A), T(SpecialSlot.B)),
		});
		var cfg = Bot.Run with { RewardOrder = new[] { CardKind.NewSpecial, CardKind.Upgrade, CardKind.Modifier }, PreferSpecial = "iron-club-charge" };
		var pick = new RunBot(cfg, 1, SpecialSlot.C).ChooseCard(offer, run);
		Assert.Equal(1, pick.Card, "the upgrade, the slot waits for the preferred special");
	}

	// Round 2, A7/E20 edge: with every owned special at Lv 3, all carrying a modifier, and only spam-slot NEW cards left, the draft
	// (RewardDraft.cs:86, RunState.CanLevelUp) shows ONLY NEW cards for that slot and a pick is compulsory (RunSession.CompleteNode).
	// The rules are silent on what the bot does then; this probe pins today's behaviour (a forced pick onto the slot, no throw) for the designer.
	[Test]
	public static void OnlyNewCardsForFilledSpamSlot_ForcedPick_DoesNotThrow_DesignerAttention()
	{
		var run = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5).Run;
		var offer = new RewardOffer("oni", new[] { C(0, CardKind.NewSpecial, "foxfire", T(SpecialSlot.A, "foxfire")) });
		var pick = new RunBot(Bot.Run, 1, SpecialSlot.A).ChooseCard(offer, run);
		Assert.Equal(0, pick.Card, "a pick is compulsory, the only card is returned");
	}

	// Round 2: CheckAgainst. A prefer_special that is not in the pool, or whose slot is not special_slot_only, is refused; unset checks nothing.
	[Test]
	public static void CheckAgainst_RefusesUnknownAndMismatchedSlot_AllowsMatchAndUnset()
	{
		var pool = Content.Pool;
		var sp = pool.Specials.First(s => !s.Starter);
		var other = pool.Specials.First(s => s.Slot != sp.Slot).Slot;
		Bot.CheckAgainst(pool);
		var ok = Bot with { Run = Bot.Run with { PreferSpecial = sp.Id }, Fight = Bot.Fight with { SpecialSlotOnly = sp.Slot } };
		ok.CheckAgainst(pool);
		(Bot with { Run = Bot.Run with { PreferSpecial = sp.Id } }).CheckAgainst(pool);
		(Bot with { Fight = Bot.Fight with { SpecialSlotOnly = other } }).CheckAgainst(pool);
		foreach (var bad in new[]
		{
			Bot with { Run = Bot.Run with { PreferSpecial = "no-such-special" } },
			Bot with { Run = Bot.Run with { PreferSpecial = sp.Id }, Fight = Bot.Fight with { SpecialSlotOnly = other } },
		})
		{
			bool threw = false;
			try { bad.CheckAgainst(pool); } catch (FormatException) { threw = true; }
			Assert.True(threw, $"refused: {bad.Run.PreferSpecial} / {bad.Fight.SpecialSlotOnly}");
		}
	}

	// Round 2: the load-time id pattern: a trailing newline or capital must not pass.
	[Test]
	public static void PreferSpecial_IdPattern_RejectsNewlineAndEdgeHyphens()
	{
		string json = File.ReadAllText(Path.Combine(ContentPaths.DataDir, "harness", BotConfig.DefaultId + ".json"));
		const string runAnchor = "\"menu_seconds_per_node\": 45";
		foreach (var bad in new[] { "iron-club-charge\n", "-iron", "iron-", "iron--club", "Iron-club", "iron_club", " iron" })
		{
			bool threw = false;
			try { BotConfig.Parse(json.Replace(runAnchor, runAnchor + ", \"prefer_special\": \"" + bad + "\"")); } catch (FormatException) { threw = true; }
			Assert.True(threw, $"rejected: '{bad}'");
		}
		Assert.True(BotConfig.Parse(json.Replace(runAnchor, runAnchor + ", \"prefer_special\": \"a1-b2\"")).Run.PreferSpecial == "a1-b2", "a valid id parses");
	}

	// Same, with reward_order putting new first and a lower-priority modifier available: the modifier must win over replacing A.
	[Test]
	public static void NewFirstOrder_NewOnSpamSlotLoses_ToAnyOtherCard()
	{
		var run = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5).Run;
		var cfg = Bot.Run with { RewardOrder = new[] { CardKind.NewSpecial, CardKind.Modifier } };
		var offer = new RewardOffer("oni", new[]
		{
			C(0, CardKind.NewSpecial, "foxfire", T(SpecialSlot.A, "foxfire")),
			C(1, CardKind.Modifier, "mod", T(SpecialSlot.A), T(SpecialSlot.B)),
		});
		var pick = new RunBot(cfg, 1, SpecialSlot.A).ChooseCard(offer, run);
		Assert.Equal(1, pick.Card, "the modifier, not a NEW card over the spammed special");
	}

	// Contract 3 (schema prefer_special + special_slot_only "pairs to spam one special"): the preferred special must land in the spam slot.
	[Test]
	public static void PreferSpecial_WithSlotOnlyC_TargetsSlotC_NotLowest()
	{
		var run = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5).Run;
		var offer = new RewardOffer("oni", new[]
		{
			C(0, CardKind.NewSpecial, "iron-club-charge", T(SpecialSlot.A, "iron-club-charge"), T(SpecialSlot.B, "iron-club-charge"), T(SpecialSlot.C, "iron-club-charge")),
		});
		var pick = new RunBot(Bot.Run with { PreferSpecial = "iron-club-charge" }, 1, SpecialSlot.C).ChooseCard(offer, run);
		Assert.Equal((0, (SpecialSlot?)SpecialSlot.C), pick, "preferred special goes in the spam slot, not over A");
	}

	// Contract 1: with the fields unset, the pick is the old pick whatever run state is passed.
	[Test]
	public static void Unset_ChooseCard_IgnoresRunState()
	{
		var run = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5).Run;
		var offer = new RewardOffer("oni", new[]
		{
			C(0, CardKind.NewSpecial, "foxfire", T(SpecialSlot.C, "foxfire"), T(SpecialSlot.A, "foxfire")),
			C(1, CardKind.Upgrade, "up", T(SpecialSlot.B)),
		});
		Assert.Equal(new RunBot(Bot.Run, 3).ChooseCard(offer), new RunBot(Bot.Run, 3).ChooseCard(offer, run), "same pick");
	}

	// Contract 8: bad values rejected.
	[Test]
	public static void BadValues_AreRejected()
	{
		string json = File.ReadAllText(Path.Combine(ContentPaths.DataDir, "harness", BotConfig.DefaultId + ".json"));
		const string fightAnchor = "\"max_fight_ticks\": 108000", runAnchor = "\"menu_seconds_per_node\": 45";
		string With(string fight, string run = "") => json.Replace(fightAnchor, fight + fightAnchor).Replace(runAnchor, runAnchor + run);
		foreach (var bad in new[] { "\"special_slot_only\": \"c\", ", "\"special_slot_only\": \"\", ", "\"punish_after_block_pct\": 1.5, ",
			"\"punish_after_block_pct\": true, ", "\"crouch_guard_pct\": 100.5, " })
		{
			bool threw = false;
			try { BotConfig.Parse(With(bad)); } catch (Exception e) when (e is FormatException or InvalidOperationException) { threw = true; }
			Assert.True(threw, $"fight rejected: {bad}");
		}
		foreach (var bad in new[] { ", \"prefer_special\": 5", ", \"prefer_special\": \"\"", ", \"prefer_special\": \"Not An Id!\"", ", \"prefer_special\": true" })
		{
			bool threw = false;
			try { BotConfig.Parse(With("", bad)); } catch (Exception e) when (e is FormatException or InvalidOperationException) { threw = true; }
			Assert.True(threw, $"run rejected: {bad}");
		}
	}
}
