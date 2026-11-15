using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-253 (R5, P5, T2, C3): the three opt-in bot fields. <c>special_slot_only</c> makes one slot's special the bot's only
/// attack (and, with <c>prefer_special</c>, the special it drafts and levels); <c>punish_after_block_pct</c> answers a
/// blocked move with Ryo's fastest move in reach; <c>crouch_guard_pct</c> crouches a held guard against a seen low and
/// under a shot that flies over a crouching body (Foxfire). Unset, every one is off and draws nothing from the RNG.
/// Fights are real row-1 matches against a scripted opponent; health is refilled every tick so no fight ends.
/// </summary>
public static class BotSpamPunishCrouchTests
{
	static HarnessContent? _content;
	static HarnessContent Content => _content ??= HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static BotConfig Bot => BotConfig.LoadDefault(ContentPaths.DataDir);

	/// <summary>The bot standing in a held guard: waits only, every wait guarded, every threat blocked; no attack of its own.</summary>
	static BotFightConfig Guard(int punish = 0, int crouch = 0, int reaction = 24) => Bot.Fight with
	{
		WalkWeight = 0, NormalWeight = 0, SpecialWeight = 0, ThrowWeight = 0, JumpWeight = 0, WaitWeight = 1,
		MistakePct = 0, BlockPct = 100, IdleGuardPct = 100, AntiAirPct = 0, ThrowBreakPct = 0,
		PunishAfterBlockPct = punish, CrouchGuardPct = crouch, ReactionFrames = reaction,
	};

	static Match RowOne()
	{
		var session = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5);
		Assert.True(session.Enter(session.Reachable()[0]).Ok, "enter row 1");
		return NodeFight.NewMatch(Content.Sources, session.Run, session, session.Node!.Fighter, ControlScheme.Kihon);
	}

	static void Place(Match m, int gap)
	{
		m.P1.X = -(gap / 2) * SimConfig.Scale;
		m.P2.X = (gap - gap / 2) * SimConfig.Scale;
	}

	static SpecialData Special(string id) => SpecialLoader.Parse(File.ReadAllText(Path.Combine(ContentPaths.DataDir, "moves", id + ".json")));

	sealed record Fight(List<HitEvent> Hits, NewcomerBot Bot, List<string> Trace, HashSet<string> RyoMoves);

	/// <summary>Plays <paramref name="ticks"/> steps; <paramref name="foe"/> gives P2's input from the live match.</summary>
	static Fight Play(Match m, BotFightConfig cfg, Func<Match, FighterInput> foe, int ticks = 3000, uint seed = 7, bool foeKit = true)
	{
		var bot = new NewcomerBot(cfg, foeKit ? BotKit.From(m.P1, m.P2) : BotKit.From(m.P1), seed);
		var hits = new List<HitEvent>();
		var trace = new List<string>();
		var moves = new HashSet<string>();
		m.Hit += (_, e) => hits.Add(e);
		for (int t = 0; t < ticks && m.Phase != MatchPhase.Over; t++)
		{
			m.P1.Health = m.P1.MaxHealth;
			m.P2.Health = m.P2.MaxHealth;
			var input = bot.Next(AiView.Capture(m));
			trace.Add(((int)input.Bits).ToString());
			if (m.P1.CurrentMove is { } mv) moves.Add(mv.Id);
			m.Step(input, m.Phase == MatchPhase.Fighting ? foe(m) : FighterInput.None);
		}
		return new Fight(hits, bot, trace, moves);
	}

	static InputBits Toward(Match m) => m.P1.X < m.P2.X ? InputBits.Left : InputBits.Right;
	static int Gap(Match m) => Math.Abs(m.P1.X - m.P2.X) / SimConfig.Scale;
	static bool Free(Fighter f) => f.State == FighterState.Idle && !f.Airborne;

	[Test]
	public static void Fields_AbsentAreOff_InEveryDefaultAndLevel_ParsedAndChecked()
	{
		foreach (var b in new[] { Bot }.Concat(BotLevels.LoadDefault(ContentPaths.DataDir).Levels))
		{
			Assert.True(b.Fight.SpecialSlotOnly is null, $"{b.Id}: special_slot_only off (R5: calibrated levels unchanged)");
			Assert.Equal(0, b.Fight.PunishAfterBlockPct, $"{b.Id}: punish_after_block_pct off");
			Assert.Equal(0, b.Fight.CrouchGuardPct, $"{b.Id}: crouch_guard_pct off");
			Assert.True(b.Run.PreferSpecial is null, $"{b.Id}: prefer_special off");
		}
		string json = File.ReadAllText(Path.Combine(ContentPaths.DataDir, "harness", BotConfig.DefaultId + ".json"));
		const string fightAnchor = "\"max_fight_ticks\": 108000", runAnchor = "\"menu_seconds_per_node\": 45";
		Assert.True(json.Contains(fightAnchor) && json.Contains(runAnchor), "anchors are where the test expects");
		string With(string fight, string run = "") => json.Replace(fightAnchor, fight + fightAnchor).Replace(runAnchor, runAnchor + run);
		var on = BotConfig.Parse(With("\"special_slot_only\": \"C\", \"punish_after_block_pct\": 40, \"crouch_guard_pct\": 60, ", ", \"prefer_special\": \"iron-club-charge\""));
		Assert.True(on.Fight.SpecialSlotOnly == SpecialSlot.C, "slot C");
		Assert.Equal(40, on.Fight.PunishAfterBlockPct, "punish 40");
		Assert.Equal(60, on.Fight.CrouchGuardPct, "crouch 60");
		Assert.Equal("iron-club-charge", on.Run.PreferSpecial, "prefer_special");
		foreach (var bad in new[] { "\"special_slot_only\": \"E\", ", "\"special_slot_only\": 3, ", "\"punish_after_block_pct\": 101, ",
			"\"crouch_guard_pct\": -1, ", "\"crouch_guard_pct\": \"50\", " })
		{
			bool threw = false;
			try { BotConfig.Parse(With(bad)); }
			catch (FormatException) { threw = true; }
			Assert.True(threw, $"rejected: {bad}");
		}
	}

	/// <summary>The opt-in bot files are the newcomer file with only their YOK-253 field(s) changed.</summary>
	[Test]
	public static void OptInFiles_AreTheNewcomerPlusTheirFieldOnly()
	{
		var nc = Bot;
		BotConfig F(string id) => BotConfig.LoadFile(Path.Combine(ContentPaths.DataDir, "harness", id + ".json"));
		var spam = F("newcomer-kihon-bot-spam-charge");
		Assert.True(spam.Fight == nc.Fight with { SpecialSlotOnly = SpecialSlot.C }, "spam-charge fight = newcomer + special_slot_only C");
		Assert.Equal("iron-club-charge", spam.Run.PreferSpecial, "spam-charge drafts Iron-Club Charge");
		var punish = F("newcomer-kihon-bot-punish");
		Assert.True(punish.Fight == nc.Fight with { PunishAfterBlockPct = 100 }, "punish fight = newcomer + punish_after_block_pct 100");
		var crouch = F("newcomer-kihon-bot-crouch-guard");
		Assert.True(crouch.Fight == nc.Fight with { CrouchGuardPct = 100 }, "crouch fight = newcomer + crouch_guard_pct 100");
		foreach (var b in new[] { punish, crouch })
			Assert.True(b.Run.PreferSpecial is null && b.Run.RewardOrder.SequenceEqual(nc.Run.RewardOrder) && b.Run.Merchant == nc.Run.Merchant, $"{b.Id}: run = newcomer's");
		Assert.True(spam.Run.RewardOrder.SequenceEqual(nc.Run.RewardOrder) && spam.Run.Merchant == nc.Run.Merchant, "spam-charge: run = newcomer's but prefer_special");
	}

	/// <summary>Fields off: giving the bot the opponent's kit (BotKit.From(p1, p2)) changes nothing it presses.</summary>
	[Test]
	public static void FieldsOff_FoeKit_ChangesNoInput()
	{
		Func<Match, FighterInput> walker = m => Free(m.P2) && Gap(m) > 160 ? new FighterInput(Toward(m)) : m.P2.State == FighterState.Idle ? FighterInput.Attack(0) : FighterInput.None;
		var a = Play(RowOne(), Bot.Fight, walker, foeKit: true);
		var b = Play(RowOne(), Bot.Fight, walker, foeKit: false);
		Assert.Equal(string.Join(",", b.Trace), string.Join(",", a.Trace), "same inputs with and without the foe's kit");
		Assert.Equal(b.Bot.RngState, a.Bot.RngState, "same RNG state");
	}

	[Test]
	public static void SlotOnly_AttacksWithThatSlotAlone_EmptySlotPlaysAsUsual()
	{
		var m = RowOne();
		var talisman = m.P1.Specials[(int)SpecialSlot.B]!.Move.Id;
		Func<Match, FighterInput> idle = _ => FighterInput.None;
		var spam = Play(m, Bot.Fight with { SpecialSlotOnly = SpecialSlot.B, AntiAirPct = 100 }, idle);
		Assert.True(spam.Bot.Stats.SlotOnly == SpecialSlot.B, "slot B is filled (Ryo's starter)");
		Assert.True(spam.Bot.Stats.Specials >= 10, $"it spams ({spam.Bot.Stats.Specials})");
		Assert.Equal(0, spam.Bot.Stats.Normals, "no normal");
		Assert.Equal(0, spam.Bot.Stats.Throws, "no throw");
		Assert.True(spam.RyoMoves.SetEquals(new[] { talisman }), $"Ryo only ever performed {talisman}: {string.Join(", ", spam.RyoMoves)}");

		var empty = Play(RowOne(), Bot.Fight with { SpecialSlotOnly = SpecialSlot.C }, idle);
		Assert.True(empty.Bot.Stats.SlotOnly == SpecialSlot.None, "slot C is empty at row 1");
		Assert.True(empty.Bot.Stats.Normals > 0, "an empty slot plays the usual bot (normals)");
		var usual = Play(RowOne(), Bot.Fight, idle);
		Assert.Equal(string.Join(",", usual.Trace), string.Join(",", empty.Trace), "empty slot = exactly the usual bot");
	}

	[Test]
	public static void RunBot_SlotOnly_InvestsInTheSlot_PreferSpecialDrafted()
	{
		var run = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5).Run; // starters in A and B, C empty
		Assert.True(run[SpecialSlot.A] is not null && run[SpecialSlot.B] is not null && run[SpecialSlot.C] is null, "starter slots");
		CardTarget T(SpecialSlot s, string id = "x") => new(s, id, id, 1, "", "");
		CardView C(int i, CardKind k, string id, params CardTarget[] t) => new(i, k, k.ToString(), id, id, "", "", "oni", t);
		var offer = new RewardOffer("oni", new[]
		{
			C(0, CardKind.Upgrade, "up", T(SpecialSlot.A), T(SpecialSlot.B)),
			C(1, CardKind.NewSpecial, "iron-club-charge", T(SpecialSlot.C, "iron-club-charge")),
			C(2, CardKind.Modifier, "mod", T(SpecialSlot.A), T(SpecialSlot.B)),
		});
		var cfg = Bot.Run;
		Assert.Equal((0, (SpecialSlot?)SpecialSlot.A), new RunBot(cfg, 1).ChooseCard(offer, run), "off: upgrade first, lowest slot");
		Assert.Equal((0, (SpecialSlot?)SpecialSlot.A), new RunBot(cfg, 1).ChooseCard(offer), "off, no run state: the same");
		Assert.Equal((1, (SpecialSlot?)SpecialSlot.C), new RunBot(cfg, 1, SpecialSlot.C).ChooseCard(offer, run), "slot C empty: fill it");
		Assert.Equal((0, (SpecialSlot?)SpecialSlot.B), new RunBot(cfg, 1, SpecialSlot.B).ChooseCard(offer, run), "slot B: upgrade B, not the lowest slot");
		Assert.Equal((1, (SpecialSlot?)SpecialSlot.C), new RunBot(cfg with { PreferSpecial = "iron-club-charge" }, 1).ChooseCard(offer, run), "prefer_special first");
		var other = new RewardOffer("kitsune", new[]
		{
			C(0, CardKind.Upgrade, "up", T(SpecialSlot.A), T(SpecialSlot.B)),
			C(1, CardKind.NewSpecial, "foxfire", T(SpecialSlot.C, "foxfire")),
		});
		Assert.Equal((1, (SpecialSlot?)SpecialSlot.C), new RunBot(cfg, 1, SpecialSlot.C).ChooseCard(other, run), "slot C empty, no prefer_special: any C special fills it");
		Assert.Equal((0, (SpecialSlot?)SpecialSlot.A), new RunBot(cfg with { PreferSpecial = "iron-club-charge" }, 1, SpecialSlot.C).ChooseCard(other, run),
			"with prefer_special, another C special never fills the slot: the usual pick");
		var replace = new RewardOffer("kitsune", new[]
		{
			C(0, CardKind.NewSpecial, "foxfire", T(SpecialSlot.A, "foxfire")),
			C(1, CardKind.Modifier, "mod", T(SpecialSlot.A)),
		});
		Assert.Equal((1, (SpecialSlot?)SpecialSlot.A), new RunBot(cfg, 1, SpecialSlot.A).ChooseCard(replace, run), "never a NEW card over the spammed special");
	}

	[Test]
	public static void ConnectFrame_ReachTravelAndFlight()
	{
		var m = RowOne();
		var kit = BotKit.From(m.P1);
		var jab = kit.Punishes.First(p => p.Move.IsNormal);
		var hb = jab.Move.Hitboxes[0];
		int reach = hb.Box.X + hb.Box.W; // overlaps a body whose front (centre - 45) is nearer than this
		Assert.Equal(hb.First, jab.ConnectFrame(reach + 45 - 1)!.Value, "in reach: its first active frame");
		Assert.True(jab.Move.Hitboxes.All(b => b.Box.X + b.Box.W <= reach) ? jab.ConnectFrame(reach + 45) is null : true, "just out of reach: never");
		var wave = kit.Punishes.First(p => p.Move.Projectile is not null);
		var pd = wave.Move.Projectile!;
		Assert.Equal(pd.SpawnFrame, wave.ConnectFrame(100)!.Value, "a shot spawned overlapping reaches on its spawn frame");
		int far = wave.ConnectFrame(800)!.Value;
		Assert.True(far > pd.SpawnFrame, $"a far shot flies first ({far})");
		var charge = Special("iron-club-charge");
		m.P1.Equip(SpecialSlot.C, charge);
		var icc = BotKit.From(m.P1).Punishes.First(p => p.Slot == SpecialSlot.C);
		int near = icc.ConnectFrame(100)!.Value, travelled = icc.ConnectFrame(400)!.Value;
		Assert.True(near == icc.Move.FirstActive && travelled > near && travelled <= icc.Move.LastActive, $"travel reaches later in the active frames ({near}, {travelled})");
		Assert.True(icc.ConnectFrame(2000) is null, "beyond travel + reach: never");
	}

	/// <summary>The opponent walks in and swings its worst-on-block ground normal whenever it is free and in reach.</summary>
	static Fight PunishFight(int punish, int reaction, out string foeMove)
	{
		var m = RowOne();
		int slot = -1, worst = int.MaxValue, reach = 0;
		for (int i = 0; i < m.P2.Moves.Length; i++)
		{
			var mv = m.P2.Moves[i];
			if (!mv.IsNormal || mv.Air || mv.Hitboxes.Count == 0 || mv.AdvantageOnBlock >= worst) continue;
			worst = mv.AdvantageOnBlock; slot = i; reach = mv.Hitboxes.Max(b => b.Box.X + b.Box.W);
		}
		Assert.True(slot >= 0, "the row-1 opponent has a ground normal");
		foeMove = m.P2.Moves[slot].Id;
		Place(m, Math.Min(reach + 30, 300));
		int s = slot, r = reach;
		return Play(m, Guard(punish, 0, reaction), mm => !Free(mm.P2) ? FighterInput.None : Gap(mm) > r + 30 ? new FighterInput(Toward(mm)) : FighterInput.Attack(s));
	}

	[Test]
	public static void Punish_AfterABlockedMove_FastestInReach_LandsWithinReaction()
	{
		var off = PunishFight(0, 24, out string foe);
		int blocked = off.Hits.Count(h => h.Attacker == 1 && h.Blocked);
		Assert.True(blocked >= 10, $"the guard blocks the opponent's {foe} ({blocked})");
		Assert.Equal(0, off.Bot.Stats.PunishRolls + off.Bot.Stats.Punishes, "off: no punish");
		Assert.Equal(0, off.Hits.Count(h => h.Attacker == 0), "off: Ryo never hits (he only guards)");

		foreach (int reaction in new[] { 24, 8 })
		{
			var on = PunishFight(100, reaction, out _);
			var st = on.Bot.Stats;
			int landed = on.Hits.Count(h => h.Attacker == 0 && !h.Blocked);
			GD.Print($"YOK-253 punish vs {foe} (reaction {reaction}): rolls {st.PunishRolls}, pressed {st.Punishes}, out of reach {st.PunishesOutOfReach}, landed {landed}, Ryo's moves {string.Join("/", on.RyoMoves)}");
			Assert.True(st.PunishRolls >= 5, $"reaction {reaction}: every block rolls at 100 ({st.PunishRolls})");
			Assert.True(st.Punishes + st.PunishesOutOfReach <= st.PunishRolls, "at most one press per roll");
			Assert.True(st.Punishes > 0, $"reaction {reaction}: punishes pressed ({st.Punishes})");
			if (reaction == 8) Assert.True(landed > 0, $"a fast reaction lands punishes on {foe} ({landed})");
		}
		var a = PunishFight(50, 24, out _);
		var b = PunishFight(50, 24, out _);
		Assert.Equal(string.Join(",", a.Trace), string.Join(",", b.Trace), "same seed, same inputs");
	}

	/// <summary>Oni Quake (C3: a low) from P2 slot D whenever it is free and close.</summary>
	static Fight QuakeFight(int crouch)
	{
		var m = RowOne();
		m.P2.Equip(SpecialSlot.D, Special("oni-quake"));
		Place(m, 200);
		return Play(m, Guard(0, crouch), mm => !Free(mm.P2) ? FighterInput.None : Gap(mm) > 240 ? new FighterInput(Toward(mm)) : FighterInput.Special(SpecialSlot.D));
	}

	/// <summary>Foxfire from P2 slot C at long range whenever it is free.</summary>
	static Fight FoxfireFight(int crouch)
	{
		var m = RowOne();
		m.P2.Equip(SpecialSlot.C, Special("foxfire"));
		Place(m, 500);
		return Play(m, Guard(0, crouch), mm => Free(mm.P2) ? FighterInput.Special(SpecialSlot.C) : FighterInput.None);
	}

	[Test]
	public static void CrouchGuard_BlocksTheLow_DucksFoxfire()
	{
		var m = RowOne();
		m.P2.Equip(SpecialSlot.D, Special("oni-quake"));
		m.P2.Equip(SpecialSlot.C, Special("foxfire"));
		var kit = BotKit.From(m.P1, m.P2);
		Assert.True(kit.FoeLows.Contains("oni-quake"), "Oni Quake is a low (C3)");
		Assert.True(kit.FoeDuckShots.Contains("foxfire"), "Foxfire flies over a crouching body (y 240 over 0-240)");
		Assert.True(!kit.FoeDuckShots.Contains("oni-quake") && !kit.FoeLows.Contains("foxfire"), "and nothing else");

		var stand = QuakeFight(0);
		int standQuakes = stand.Hits.Count(h => h.Attacker == 1 && h.MoveId == "oni-quake");
		Assert.True(standQuakes >= 5 && stand.Hits.Count(h => h.Attacker == 1 && h.MoveId == "oni-quake" && h.Blocked) == 0, $"standing guard never blocks the low ({standQuakes} quakes)");
		var duck = QuakeFight(100);
		int quakes = duck.Hits.Count(h => h.Attacker == 1 && h.MoveId == "oni-quake"), blockedQ = duck.Hits.Count(h => h.Attacker == 1 && h.MoveId == "oni-quake" && h.Blocked);
		GD.Print($"YOK-253 crouch vs Oni Quake: {blockedQ}/{quakes} blocked, crouch guards {duck.Bot.Stats.CrouchGuards}");
		Assert.True(quakes >= 5 && blockedQ == quakes, $"crouch guard blocks every quake ({blockedQ}/{quakes})");

		var fs = FoxfireFight(0);
		int fsHits = fs.Hits.Count(h => h.Attacker == 1 && h.MoveId == "foxfire");
		Assert.True(fsHits >= 5, $"standing, Foxfire connects (blocked) ({fsHits})");
		var fd = FoxfireFight(100);
		int fdHits = fd.Hits.Count(h => h.Attacker == 1 && h.MoveId == "foxfire");
		GD.Print($"YOK-253 crouch vs Foxfire: {fdHits} connected (standing {fsHits}), crouch guards {fd.Bot.Stats.CrouchGuards}");
		Assert.True(fd.Bot.Stats.CrouchGuards >= 5, $"it crouches under the flames ({fd.Bot.Stats.CrouchGuards})");
		Assert.Equal(0, fdHits, "no flame touches a crouching Ryo");
	}
}
