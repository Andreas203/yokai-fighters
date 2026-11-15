using System;
using System.IO;
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

	// Contract 2 (schema special_slot_only: "never replaces its special with a NEW card"): the only card on offer is a NEW card
	// that can only go on the filled spam slot. The fallback after the slot-aware loop must not pick it onto that slot.
	[Test]
	public static void NewCardOnlyOffered_FilledSpamSlot_IsNeverTheTarget()
	{
		var run = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5).Run; // A, B filled
		var offer = new RewardOffer("oni", new[] { C(0, CardKind.NewSpecial, "foxfire", T(SpecialSlot.A, "foxfire")) });
		var pick = new RunBot(Bot.Run, 1, SpecialSlot.A).ChooseCard(offer, run);
		Assert.True(pick.Target != SpecialSlot.A, $"NEW card picked onto spam slot A: {pick}");
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
