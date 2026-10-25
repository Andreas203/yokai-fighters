using System;
using System.Linq;
using System.Text.Json;
using YokaiFighters.Ai;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-157 (E7, R5, P5, P6, Y2, Y4). Expected values come from rules.md.</summary>
public static class HitLogProbes
{
	static HarnessContent? _content;
	static HarnessContent Content => _content ??= HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static BotConfig Bot => BotConfig.LoadDefault(ContentPaths.DataDir);

	static BatchResult Batch(HarnessContent c, uint seed = 1, int runs = 12) => RunHarness.PlayBatch(c, Bot, seed, runs);
	static System.Collections.Generic.IEnumerable<FightRecord> Fights(BatchResult b) =>
		b.Runs.SelectMany(r => r.Nodes).Where(n => n.Fight is not null).Select(n => n.Fight!);

	// Listening only: the same seed plays the same fights with the log on and off.
	[Test]
	public static void LogOnOff_SameSimHashes()
	{
		var on = Fights(Batch(Content with { HitLog = true })).ToList();
		var off = Fights(Batch(Content with { HitLog = false })).ToList();
		var raw = Fights(Batch(Content with { HitLog = true, RawHitLog = true })).ToList();
		Assert.True(on.Count > 0 && on.Count == off.Count && on.Count == raw.Count, "same number of fights");
		for (int i = 0; i < on.Count; i++)
		{
			Assert.Equal(off[i].MatchHash, on[i].MatchHash, $"fight {i}: log on vs off");
			Assert.Equal(off[i].MatchHash, raw[i].MatchHash, $"fight {i}: raw log vs off");
			Assert.True(off[i].Log is null && on[i].Log is not null, "log present only when on");
		}
	}

	// P5: the tallies are the sim's own events; per fight, damage by Ryo's moves = damage dealt, by the opponent's = damage taken.
	// A landed throw is a throw and a hit: its damage is counted once.
	[Test]
	public static void Tallies_DamageEqualsHealthLost_ThrowNotDoubleCounted()
	{
		foreach (var f in Fights(Batch(Content with { RawHitLog = true })))
		{
			// A KO hit may carry more damage than the health left (overkill), so a KO side only has to reach the health lost.
			bool ryoDown = f.HealthLeft <= 0;
			int dealt = f.Log!.Moves[0].Values.Sum(t => t.Damage), taken = f.Log.Moves[1].Values.Sum(t => t.Damage);
			if (f.Won) Assert.True(dealt >= f.DamageDealt, $"{f.Profile}: Ryo tallied {dealt} >= {f.DamageDealt}"); else Assert.Equal(f.DamageDealt, dealt, $"{f.Profile}: Ryo tallied damage = opponent health lost");
			if (ryoDown) Assert.True(taken >= f.DamageTaken, $"{f.Profile}: opponent tallied {taken} >= {f.DamageTaken}"); else Assert.Equal(f.DamageTaken, taken, $"{f.Profile}: opponent tallied damage = Ryo health lost (Ryo alive)");
			foreach (var side in f.Log.Moves) foreach (var t in side.Values)
				Assert.True(t.Blocked <= t.Hits && t.Counter <= t.Hits, "blocked and counter are subsets of hits");
			Assert.True(f.Log.Raw!.Where(h => h.Kind != "hit").All(h => h.Damage == 0), "only hit lines carry damage");
			for (int s = 0; s < 2; s++)
				Assert.Equal(f.Log.Moves[s].Values.Sum(t => t.Damage), f.Log.Raw!.Where(h => h.Attacker == s).Sum(h => h.Damage), "raw lines sum to the tally");
		}
	}

	// Moves by profile: damage shares add to 100 (rounding to 0.1) per profile and side.
	[Test]
	public static void MovesByProfile_SharesAddTo100()
	{
		using var d = JsonDocument.Parse(HarnessReport.ToJson(Batch(Content, 1, 20)));
		var byProfile = d.RootElement.GetProperty("summary").GetProperty("moves_by_profile");
		int checkedSides = 0;
		foreach (var prof in byProfile.EnumerateObject())
			foreach (var side in prof.Value.EnumerateObject())
			{
				if (side.Value.ValueKind != JsonValueKind.Object) continue;
				double sum = 0; int n = 0; bool nullShare = false;
				foreach (var mv in side.Value.EnumerateObject())
				{
					var p = mv.Value.GetProperty("damage_pct");
					if (p.ValueKind == JsonValueKind.Null) { nullShare = true; break; }
					sum += p.GetDouble(); n++;
				}
				if (nullShare || n == 0) continue;
				checkedSides++;
				Assert.True(Math.Abs(sum - 100) <= 0.05 * n + 1e-9, $"{prof.Name}/{side.Name}: shares sum to {sum}");
			}
		Assert.True(checkedSides > 0, "sanity: some shares were checked");
	}

	// Determinism with the log on: same bytes twice.
	[Test]
	public static void LogOn_SameSeedSameBytes()
	{
		Assert.Equal(HarnessReport.ToJson(Batch(Content with { RawHitLog = true }, 3, 6)), HarnessReport.ToJson(Batch(Content with { RawHitLog = true }, 3, 6)), "same bytes");
	}

	// ---- Bot probes on staged views (E7, R5, Y4) ----
	static (NewcomerBot Bot, int Total) Setup(int guardPct, out AiView ground, int antiAir = 100)
	{
		var session = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5);
		session.Enter(session.Reachable()[0]);
		var m = NodeFight.NewMatch(Content.Sources, session.Run, session, session.Node!.Fighter, ControlScheme.Kihon);
		var kit = BotKit.From(m.P1);
		var cfg = Bot.Fight with
		{
			ReactionFrames = 1, DecideEvery = 600, WaitTicks = 200, WalkWeight = 0, WaitWeight = 1, NormalWeight = 0, SpecialWeight = 0,
			ThrowWeight = 0, JumpWeight = 0, MistakePct = 0, BlockPct = 0, IdleGuardPct = guardPct, AntiAirPct = antiAir,
		};
		var bot = new NewcomerBot(cfg, kit, 3);
		var b = AiView.Capture(m);
		ground = b with { P1 = b.P1 with { X = 0, Facing = 1 }, P2 = b.P2 with { X = 100 * SimConfig.Scale, Facing = -1 } };
		return (bot, kit.MoveFrames[bot.AntiAirMove!]);
	}

	static AiView Jump(AiView g, int tick) => g with { Tick = tick, P2 = g.P2 with { Airborne = true } };
	static AiView Ground(AiView g, int tick) => g with { Tick = tick };

	static AiView Busy(AiView g, int tick, string id, int remaining, int total, int hitstop) =>
		Jump(g, tick) with { HitstopLeft = hitstop, P1 = g.P1 with { State = FighterState.Attack, MoveId = id, MoveFrame = total - remaining } };

	// E7: a press is used only if sent within the 5-tick buffer: remaining + hitstop < 5 rolls, >= 5 does not.
	[Test]
	public static void FreeToPress_Boundary_RemainingPlusHitstop()
	{
		foreach (var (remaining, hitstop, wantRoll) in new[] { (4, 0, true), (5, 0, false), (2, 2, true), (2, 3, false), (1, 40, false) })
		{
			var (bot, total) = Setup(0, out var g);
			bot.Next(Busy(g, 0, bot.AntiAirMove!, remaining, total, hitstop));
			Assert.Equal(wantRoll ? 1 : 0, bot.Stats.AntiAirs, $"remaining {remaining} + hitstop {hitstop}");
		}
	}

	// E7 / R5: while Ryo cannot act the jump's single roll is not spent: when he is free during the same jump, it is rolled (100% = pressed).
	[Test]
	public static void NotFree_SpendsNoRoll_LaterFreeTickStillRolls()
	{
		foreach (var state in new[] { FighterState.Hitstun, FighterState.Knockdown })
		{
			var (bot, _) = Setup(0, out var g);
			for (int i = 0; i < 10; i++) bot.Next(Jump(g, i) with { P1 = g.P1 with { State = state } });
			Assert.Equal(0, bot.Stats.AntiAirs, $"{state}: no press while unable to act");
			bot.Next(Jump(g, 10));
			Assert.Equal(1, bot.Stats.AntiAirs, $"{state}: the first free tick of the same jump rolls and presses");
		}
	}

	// R5/Y4 designer decision: under a held idle guard the first jump of the fight is let go, later ones roll.
	[Test]
	public static void FirstJumpLetGo_PerFight_AndLaterJumpsRoll()
	{
		for (int fight = 0; fight < 2; fight++)
		{
			var (bot, _) = Setup(100, out var g);
			int t = 0;
			for (int i = 0; i < 4; i++) bot.Next(Ground(g, t++));
			for (int i = 0; i < 8; i++) bot.Next(Jump(g, t++)); // whole first jump: let go, even though Ryo is free
			Assert.Equal(0, bot.Stats.AntiAirs, $"fight {fight}: first guarded jump gets no anti-air");
			Assert.Equal(1, bot.Stats.GuardedJumpsLetGo, $"fight {fight}: counted once");
			for (int i = 0; i < 4; i++) bot.Next(Ground(g, t++));
			for (int i = 0; i < 8; i++) bot.Next(Jump(g, t++));
			Assert.Equal(1, bot.Stats.AntiAirs, $"fight {fight}: the second jump gets its roll (100% = one press)");
			Assert.Equal(1, bot.Stats.GuardedJumpsLetGo, $"fight {fight}: still only one let go");
		}
	}

	// At idle_guard_pct 0 the rule is inert: the first jump rolls like any other.
	[Test]
	public static void FirstJump_AtPct0_IsNotLetGo()
	{
		var (bot, _) = Setup(0, out var g);
		for (int i = 0; i < 4; i++) bot.Next(Ground(g, i));
		bot.Next(Jump(g, 4));
		Assert.Equal(1, bot.Stats.AntiAirs, "anti-air on the first jump");
		Assert.Equal(0, bot.Stats.GuardedJumpsLetGo, "nothing let go");
	}
}
