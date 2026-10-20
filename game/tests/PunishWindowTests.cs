using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-169 (designer 2026-10-20; P6, Y2): a fired habit counts as punished if Ryo lands an unblocked hit while it runs,
/// or within the next <see cref="FightLog.PunishWindowHits"/> (3) hits exchanged afterwards, either side's. Driven
/// through the log's seams with the sim's own <see cref="HitEvent"/>s, so each case is exact.
/// </summary>
public static class PunishWindowTests
{
	static HitEvent Ryo(bool blocked = false, int damage = 50) => new(0, "ryo-jab", 5, blocked, false, blocked ? 0 : damage);
	static HitEvent Opp(bool blocked = false) => new(1, "kitsune-jab", 5, blocked, false, blocked ? 0 : 40);

	/// <summary>A log with one uncovered habit fired and already over (its plan ran one tick).</summary>
	static FightLog AfterHabit()
	{
		var log = new FightLog(false);
		log.HabitStarted();
		log.HabitTick(true);
		log.HabitTick(false);
		return log;
	}

	[Test]
	public static void WindowIsThreeHits()
	{
		Assert.Equal(3, FightLog.PunishWindowHits, "designer 2026-10-20: no more than 3 hits");
	}

	[Test]
	public static void Punished_OnTheThirdHitAfter_EitherSidesHitsCount()
	{
		var log = AfterHabit();
		log.Hit(10, Opp());
		log.Hit(11, Ryo(blocked: true));
		log.Hit(12, Ryo());
		Assert.Equal(1, log.HabitPunished, "Ryo's unblocked hit is the 3rd hit exchanged after the habit: punished");
		log.Hit(13, Ryo());
		Assert.Equal(1, log.HabitPunished, "one habit is punished once");
	}

	[Test]
	public static void Punished_OnTheFirstHitAfter()
	{
		var log = AfterHabit();
		log.Hit(10, Ryo());
		Assert.Equal(1, log.HabitPunished, "first hit after the habit");
	}

	[Test]
	public static void NotPunished_AfterThreeHits()
	{
		var log = AfterHabit();
		log.Hit(10, Opp());
		log.Hit(11, Opp(blocked: true));
		log.Hit(12, Ryo(blocked: true));
		log.Hit(13, Ryo());
		Assert.Equal(0, log.HabitPunished, "3 hits passed with no Ryo punish: the 4th is too late");
	}

	[Test]
	public static void NoTickLimit_TheWindowIsCountedInHits()
	{
		var log = AfterHabit();
		for (int t = 0; t < 600; t++) log.HabitTick(false); // ten seconds with nothing exchanged
		log.Hit(700, Ryo());
		Assert.Equal(1, log.HabitPunished, "no hits exchanged yet, so the window is still open");
	}

	[Test]
	public static void HabitRunning_HitsDoNotCountTowardTheThree()
	{
		var log = new FightLog(false);
		log.HabitStarted();
		log.HabitTick(true);
		for (int i = 0; i < 5; i++) log.Hit(10 + i, Opp());
		Assert.Equal(0, log.HabitPunished, "the opponent's hits are no punish");
		log.HabitTick(true);
		log.Hit(20, Ryo());
		Assert.Equal(1, log.HabitPunished, "an unblocked Ryo hit while the habit runs punishes it, however many hits came before");

		var after = new FightLog(false);
		after.HabitStarted();
		after.HabitTick(true);
		for (int i = 0; i < 5; i++) after.Hit(10 + i, Opp());
		after.HabitTick(false);
		after.Hit(30, Opp());
		after.Hit(31, Opp());
		after.Hit(32, Ryo());
		Assert.Equal(1, after.HabitPunished, "hits while it ran do not count: Ryo's is the 3rd after it ended");
	}

	[Test]
	public static void BlockedRyoHit_WhileRunning_IsNoPunish()
	{
		var log = new FightLog(false);
		log.HabitStarted();
		log.HabitTick(true);
		log.Hit(10, Ryo(blocked: true));
		Assert.Equal(0, log.HabitPunished, "a blocked hit is no punish");
	}

	[Test]
	public static void KoHit_IsAPunish_AndTalliedInFull()
	{
		var log = AfterHabit();
		log.Hit(10, Opp());
		log.Hit(11, Ryo(damage: 9999)); // the KO hit: far more than the health left
		Assert.Equal(1, log.HabitPunished, "the KO hit is a punish like any other");
		Assert.Equal(9999, log.Moves[0]["ryo-jab"].Damage, "KO damage is tallied as the full hit, no cap (designer)");
	}

	[Test]
	public static void NewHabit_ResetsTheCount_NoHabit_NoWindow()
	{
		var idle = new FightLog(false);
		idle.HabitTick(false);
		idle.Hit(1, Ryo());
		Assert.Equal(0, idle.HabitPunished, "no habit fired, nothing to punish");

		var log = AfterHabit();
		log.Hit(10, Opp());
		log.Hit(11, Opp());
		log.HabitStarted(); // a new habit replaces the old window and starts its own count
		log.HabitTick(true);
		log.HabitTick(false);
		log.Hit(20, Opp());
		log.Hit(21, Opp());
		log.Hit(22, Ryo());
		Assert.Equal(1, log.HabitPunished, "3rd hit after the new habit");
	}
}
