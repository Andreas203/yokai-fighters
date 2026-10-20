using System.Linq;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-169 probes. Rule: designer 2026-10-20 (P6, Y2) punish window = plan running + next 3 hits.</summary>
public static class PunishWindowProbes
{
	static HitEvent Ryo(bool blocked = false) => new(0, "ryo-multi", 5, blocked, false, blocked ? 0 : 20);
	static HitEvent Opp(bool blocked = false) => new(1, "opp-multi", 5, blocked, false, blocked ? 0 : 20);
	static FightLog Started(bool ended)
	{
		var log = new FightLog(false);
		log.HabitStarted(); log.HabitTick(true);
		if (ended) log.HabitTick(false);
		return log;
	}

	// P6/Y2: each connect of a multi-hit move is one hit exchanged; three of them use up the window.
	[Test]
	public static void MultiHitMove_ThreeOpponentHits_CloseWindow()
	{
		var log = Started(true);
		log.Hit(10, Opp()); log.Hit(10, Opp()); log.Hit(10, Opp());
		log.Hit(11, Ryo());
		Assert.Equal(0, log.HabitPunished, "3 connects passed, even on one tick");
	}

	// P6/Y2: Ryo's multi-hit string punishes once, not once per hit.
	[Test]
	public static void MultiHitRyoString_PunishesOnce()
	{
		var log = Started(true);
		log.Hit(10, Ryo()); log.Hit(10, Ryo()); log.Hit(10, Ryo());
		Assert.Equal(1, log.HabitPunished, "one habit, one punish");
	}

	// Window: hits while the plan runs do not use up the 3.
	[Test]
	public static void HitsWhilePlanRuns_DoNotConsumeWindow()
	{
		var log = Started(false);
		for (int i = 0; i < 5; i++) log.Hit(10 + i, Opp());
		log.Hit(20, Ryo(blocked: true));
		Assert.Equal(0, log.HabitPunished, "blocked Ryo hit is no punish");
		log.HabitTick(false);
		log.Hit(21, Opp()); log.Hit(22, Opp());
		log.Hit(23, Ryo());
		Assert.Equal(1, log.HabitPunished, "3rd hit after the plan ended is still inside");
	}

	// Window: unblocked Ryo hit while the plan runs is a punish.
	[Test]
	public static void HabitSameTickAsHit_RyoHitAfterIt_Punishes()
	{
		var log = new FightLog(false);
		log.HabitStarted(); log.HabitTick(true); // AfterAi runs before the match steps
		log.Hit(5, Ryo());
		Assert.Equal(1, log.HabitPunished, "hit on the firing tick, plan running");
	}

	// Window: a new habit resets the window (2 hits used, new habit, 3 more allowed).
	[Test]
	public static void NewHabit_ResetsWindow()
	{
		var log = Started(true);
		log.Hit(1, Opp()); log.Hit(2, Opp());
		log.HabitStarted(); log.HabitTick(true); log.HabitTick(false);
		log.Hit(3, Opp()); log.Hit(4, Opp());
		log.Hit(5, Ryo());
		Assert.Equal(1, log.HabitPunished, "3rd hit of the new window");
	}

	// Window: unpunished habit replaced: old one is not also counted; two habits both punished count 2.
	[Test]
	public static void TwoHabits_EachPunishedOnce()
	{
		var log = Started(true);
		log.Hit(1, Ryo());
		log.Hit(2, Ryo());
		Assert.Equal(1, log.HabitPunished, "no habit open: no extra punish");
		log.HabitStarted(); log.HabitTick(true); log.HabitTick(false);
		log.Hit(3, Ryo());
		Assert.Equal(2, log.HabitPunished, "second habit punished");
	}

	// Window: no habit fired, no punish.
	[Test]
	public static void NoHabit_NoPunish()
	{
		var log = new FightLog(false);
		log.Hit(1, Ryo());
		Assert.Equal(0, log.HabitPunished, "nothing to punish");
	}

	// Trade: opponent and Ryo hit on the same tick; order of the two events must not change a rule-given outcome
	// (both are within 3 hits, Ryo's is unblocked).
	[Test]
	public static void Trade_EitherEventOrder_Punishes()
	{
		var a = Started(true); a.Hit(1, Opp()); a.Hit(1, Ryo());
		var b = Started(true); b.Hit(1, Ryo()); b.Hit(1, Opp());
		Assert.Equal(1, a.HabitPunished, "opp then Ryo");
		Assert.Equal(1, b.HabitPunished, "Ryo then opp");
	}

	// KO: a Ryo KO hit with full damage after two hits is a punish and its damage is tallied.
	[Test]
	public static void KoHit_CountsAndDamageTallied()
	{
		var log = Started(true);
		log.Hit(1, Opp()); log.Hit(2, Opp(true));
		log.Hit(3, new HitEvent(0, "ryo-ko", 5, false, false, 999));
		Assert.Equal(1, log.HabitPunished, "KO counts");
		Assert.Equal(999, log.Moves[0]["ryo-ko"].Damage, "damage in full");
	}

	// Unknown attacker ids are ignored and don't consume window.
	[Test]
	public static void ForeignAttacker_DoesNotConsumeWindow()
	{
		var log = Started(true);
		log.Hit(1, new HitEvent(2, "x", 1, false, false, 5)); log.Hit(1, new HitEvent(2, "x", 1, false, false, 5)); log.Hit(1, new HitEvent(2, "x", 1, false, false, 5));
		log.Hit(2, Ryo());
		Assert.Equal(1, log.HabitPunished, "not a fighter hit");
	}

	static HarnessContent Content => HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);

	// Y2: punished can never exceed the uncovered habits, per fight and in total, across 200 runs.
	[Test]
	public static void Batch200_PunishedNeverExceedsUncovered()
	{
		var b = RunHarness.PlayBatch(Content, BotConfig.LoadDefault(ContentPaths.DataDir), 1, 200);
		int fights = 0;
		foreach (var f in b.Runs.SelectMany(r => r.Nodes).Where(n => n.Fight is not null).Select(n => n.Fight!))
		{
			fights++;
			var l = f.Log!;
			Assert.True(l.HabitPunished <= l.HabitFired - l.HabitCovered, $"punished {l.HabitPunished} > uncovered {l.HabitFired - l.HabitCovered}");
			Assert.True(l.HabitCovered <= l.HabitFired && l.HabitFired <= l.HabitRolls, "rolls >= fired >= covered");
		}
		Assert.True(fights > 0, "fights played");
	}

	// R5: log on vs off, and two runs, give identical match hashes and results.
	[Test]
	public static void Batch_LogOnVsOff_SameHashes_AndDeterministic()
	{
		var bot = BotConfig.LoadDefault(ContentPaths.DataDir);
		var off = RunHarness.PlayBatch(Content with { HitLog = false }, bot, 7, 40);
		var on = RunHarness.PlayBatch(Content with { HitLog = true }, bot, 7, 40);
		var on2 = RunHarness.PlayBatch(Content with { HitLog = true }, bot, 7, 40);
		string H(BatchResult r) => string.Join(",", r.Runs.SelectMany(x => x.Nodes).Where(n => n.Fight is not null).Select(n => n.Fight!.MatchHash.ToString("x16")));
		Assert.True(H(off) == H(on), "log off == on");
		Assert.True(H(on) == H(on2), "same seed twice");
		Assert.True(HarnessReport.ToJson(on) == HarnessReport.ToJson(on2), "same JSON twice");
	}
}
