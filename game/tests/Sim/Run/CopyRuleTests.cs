using System;
using System.IO;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// T2-T4: the Tanuki's copy target, read from the run state. Pool = the TEST FIXTURES only
/// (<see cref="AbilityPool.LoadFixtures"/>): Spirit Wave (A), Rising Talisman (B), Foxfire (C). Specials for
/// the other cases (slot D, a second slot C move, a Lv 3 evolution) are made from the Spirit Wave fixture's
/// frame data through the loader, never from data/.
/// </summary>
public static class CopyRuleTests
{
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static AbilityPool Pool() => AbilityPool.LoadFixtures(RepoRoot);

	/// <summary>
	/// A special in <paramref name="slot"/> with the Spirit Wave fixture's Lv 1 frame data, a Lv 2 tuning of
	/// recovery -4 and (maxLevel 3) an evolution of recovery -10.
	/// </summary>
	static SpecialData Make(string id, string slot, int maxLevel)
	{
		string fd = Pool().Special("test-spirit-wave")!.Level1Json;
		string levels = "\"1\":{\"frame_data\":" + fd + "}";
		if (maxLevel >= 2) levels += ",\"2\":{\"tuning\":{\"target\":\"recovery\",\"op\":\"add\",\"value\":-4}}";
		if (maxLevel >= 3) levels += ",\"3\":{\"evolution\":{\"name\":\"Probe Evolution\",\"changes\":[{\"target\":\"recovery\",\"op\":\"add\",\"value\":-10}]}}";
		string json = "{\"kind\":\"special\",\"id\":\"" + id + "\",\"input\":{\"kata\":{\"slot\":\"" + slot + "\"}},\"levels\":{" + levels + "}}";
		return SpecialLoader.Parse(json);
	}

	static string TargetId(RunState run) => CopyRule.Target(run)?.Data.Id ?? "(none)";

	static string Orders(RunState run) =>
		$"{run.LevelledOrder(SpecialSlot.A)},{run.LevelledOrder(SpecialSlot.B)},{run.LevelledOrder(SpecialSlot.C)},{run.LevelledOrder(SpecialSlot.D)}";

	static bool Rejects(RunState run, int a, int b, int c, int d)
	{
		try { run.RestoreLevelledOrder(a, b, c, d); }
		catch (ArgumentException) { return true; }
		return false;
	}

	// --- T2: highest level in a slot -------------------------------------------------------------

	[Test]
	public static void HighestLevelSpecialIsTheTarget()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		run.LevelUp(SpecialSlot.B);
		var t = CopyRule.Target(run)!;
		Assert.Equal("test-rising-talisman", t.Data.Id, "T2: the Lv 2 special beats the Lv 1 one in a lower slot");
		Assert.Equal(SpecialSlot.B, t.Slot, "its slot");
		Assert.Equal(2, t.Level, "T4: at Ryo's level");

		run.Equip(pool.Special("test-foxfire")!);
		Assert.Equal("test-rising-talisman", TargetId(run), "T2: a newer Lv 1 special doesn't beat a Lv 2 one");
	}

	[Test]
	public static void HigherLevelBeatsAMoreRecentLevelUp()
	{
		var run = new RunState();
		var d = Make("probe-d", "D", 3);
		run.Equip(d);
		run.Equip(d);
		run.Equip(d);
		Assert.Equal(3, run[SpecialSlot.D]!.Level, "Lv 3 by drafting it three times (A3)");
		var a = Make("probe-a", "A", 2);
		run.Equip(a);
		run.Equip(a);
		Assert.True(run.LevelledOrder(SpecialSlot.A) > run.LevelledOrder(SpecialSlot.D), "A levelled after D");
		Assert.Equal("probe-d", TargetId(run), "T2 before T3: Lv 3 beats Lv 2 however recent, and whatever the slot");
	}

	[Test]
	public static void NoSpecialInAnySlotMeansNoTarget()
	{
		Assert.True(CopyRule.Target(new RunState()) is null, "nothing in a slot, nothing to copy");
	}

	// --- T3: ties ---------------------------------------------------------------------------------

	[Test]
	public static void TieGoesToTheMostRecentlyLevelled()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		run.LevelUp(SpecialSlot.A);
		run.LevelUp(SpecialSlot.B);
		Assert.Equal("test-rising-talisman", TargetId(run), "T3: both Lv 2, B levelled last, so B despite the higher slot");

		var other = RunState.NewRun(pool);
		other.LevelUp(SpecialSlot.B);
		other.LevelUp(SpecialSlot.A);
		Assert.Equal("test-spirit-wave", TargetId(other), "T3: same levels, the other order, the other target");
	}

	[Test]
	public static void DraftingAnOwnedSpecialCountsAsLevellingIt()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		run.LevelUp(SpecialSlot.A);
		run.Equip(pool.Special("test-foxfire")!);
		run.Equip(pool.Special("test-foxfire")!);
		Assert.Equal(2, run[SpecialSlot.C]!.Level, "A3: drafted twice = Lv 2");
		Assert.Equal("test-foxfire", TargetId(run), "T3: the level-up through the draft is the most recent one");
	}

	[Test]
	public static void FullTieGoesToTheLowerSlot()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		Assert.Equal("0,0,0,0", Orders(run), "nothing has levelled in a new run");
		Assert.Equal("test-spirit-wave", TargetId(run), "T3: all Lv 1, never levelled, so slot A");
		run.Equip(pool.Special("test-foxfire")!);
		Assert.Equal("test-spirit-wave", TargetId(run), "T3: a newly drafted Lv 1 special has not levelled; still slot A");

		var high = new RunState();
		high.Equip(Make("probe-d", "D", 2));
		high.Equip(Make("probe-c", "C", 2));
		Assert.Equal("probe-c", TargetId(high), "T3: the lower slot, not the first one equipped");
	}

	// --- T2: sold / replaced moves don't count -----------------------------------------------------

	[Test]
	public static void ReplacedSpecialNoLongerCounts()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		var fox = pool.Special("test-foxfire")!;
		run.Equip(fox);
		run.Equip(fox);
		Assert.Equal("test-foxfire", TargetId(run), "Foxfire Lv 2 is the target while it is in slot C");

		var probe = Make("probe-c", "C", 2);
		run.Equip(probe);
		Assert.Equal("probe-c", run[SpecialSlot.C]!.Data.Id, "A6: slot C replaced");
		Assert.Equal(0, run.LevelledOrder(SpecialSlot.C), "T2: the replaced move's level-up went with it");
		Assert.Equal("test-spirit-wave", TargetId(run), "T2/T3: all Lv 1 again and nothing in a slot has levelled, so slot A");

		var never = RunState.NewRun(pool);
		never.Equip(probe);
		Assert.Equal(never.StateHash(), run.StateHash(), "T2: a replaced move leaves no trace in the run state");
	}

	[Test]
	public static void ReplacementLevelsFromScratch()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		var fox = pool.Special("test-foxfire")!;
		run.Equip(fox);
		run.Equip(fox);
		run.LevelUp(SpecialSlot.A);
		var c = Make("probe-c", "C", 2);
		run.Equip(c);
		Assert.Equal("test-spirit-wave", TargetId(run), "T2: only Spirit Wave is Lv 2 now");
		run.Equip(c);
		Assert.Equal("probe-c", TargetId(run), "T3: the replacement's own level-up is the most recent");
	}

	// --- T4: strength --------------------------------------------------------------------------------

	[Test]
	public static void CopyIsAtRyosLevelWithoutHisModifier()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		var wisp = pool.Modifier("test-will-o-wisp")!;
		run.Attach(SpecialSlot.A, wisp);
		run.LevelUp(SpecialSlot.A);
		var owned = run[SpecialSlot.A]!;
		var t = CopyRule.Target(run)!;
		Assert.Equal("test-spirit-wave", t.Data.Id, "target");
		Assert.Equal(2, t.Level, "T4: Ryo's level");
		var copy = t.Build();
		var ryos = owned.Data.Build(owned.Level, false, owned.Modifier, null);
		Assert.Equal(26, copy.Recovery, "T4: the Lv 2 tuning is copied (recovery 30 -> 26)");
		Assert.Equal(12, copy.Projectile!.Speed, "T4: never his modifier (plain 12 units a tick)");
		Assert.Equal(16, ryos.Projectile!.Speed, "Ryo's own keeps Will-o'-wisp");
		Assert.Equal("test-will-o-wisp", run[SpecialSlot.A]!.Modifier?.Id, "asking takes nothing from Ryo");
	}

	[Test]
	public static void CopyOfAnEvolvedSpecialHasNoEvolution()
	{
		var run = new RunState();
		var d = Make("probe-evolving", "D", 3);
		run.Equip(d);
		run.Equip(d);
		run.Equip(d);
		var t = CopyRule.Target(run)!;
		Assert.Equal(3, t.Level, "T4: reported at Ryo's level");
		Assert.Equal(CopyRule.HighestUnevolvedLevel, t.MoveLevel, "T4: built without the Lv 3 evolution");
		int copy = t.Build().Recovery;
		Assert.Equal(d.Build(2, false).Recovery, copy, "T4: Lv 1 frame data + Lv 2 tuning, nothing more");
		Assert.Equal(d.Build(1, false).Recovery - 4, copy, "the tuning step is in");
		Assert.True(d.Build(3, false).Recovery < copy, "T4: the evolved Lv 3 move beats its copy");
	}

	[Test]
	public static void CopyBelowLevel3UsesRyosLevelExactly()
	{
		var run = new RunState();
		var d = Make("probe-evolving", "D", 3);
		run.Equip(d);
		Assert.Equal(1, CopyRule.Target(run)!.MoveLevel, "Lv 1 copy of a Lv 1 special");
		Assert.Equal(d.Build(1, false).Recovery, CopyRule.Target(run)!.Build().Recovery, "Lv 1 frame data");
		run.Equip(d);
		Assert.Equal(2, CopyRule.Target(run)!.MoveLevel, "Lv 2 copy of a Lv 2 special");
	}

	// --- Reads only, nothing locked ---------------------------------------------------------------

	[Test]
	public static void AskingChangesNothingAndFollowsTheRun()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		run.LevelUp(SpecialSlot.B);
		ulong before = run.StateHash();
		string first = TargetId(run);
		Assert.Equal(first, TargetId(run), "same run state, same answer");
		Assert.Equal(before, run.StateHash(), "the rule only reads the run");
		Assert.Equal(1, run.LevelledOrder(SpecialSlot.B), "level order untouched by asking");

		run.LevelUp(SpecialSlot.A);
		Assert.Equal("test-spirit-wave", TargetId(run), "not locked here (T1 is separate): the answer follows the run");
	}

	// --- T3's order is run state: hashed and restorable ---------------------------------------------

	[Test]
	public static void LevelUpOrderIsInTheStateHash()
	{
		var pool = Pool();
		var ab = RunState.NewRun(pool);
		ab.LevelUp(SpecialSlot.A);
		ab.LevelUp(SpecialSlot.B);
		var ba = RunState.NewRun(pool);
		ba.LevelUp(SpecialSlot.B);
		ba.LevelUp(SpecialSlot.A);
		Assert.True(TargetId(ab) != TargetId(ba), "the two orders give different copy targets (T3)");
		Assert.True(ab.StateHash() != ba.StateHash(), "so they must not share a state hash");

		var again = RunState.NewRun(pool);
		again.LevelUp(SpecialSlot.A);
		again.LevelUp(SpecialSlot.B);
		Assert.Equal(ab.StateHash(), again.StateHash(), "same level-ups in the same order, same hash");
	}

	[Test]
	public static void HashDependsOnRelativeOrderOnly()
	{
		var pool = Pool();
		var probe = Make("probe-c", "C", 2);
		// Foxfire levels first, then B, then A; then Foxfire is replaced.
		var x = RunState.NewRun(pool);
		x.Equip(pool.Special("test-foxfire")!);
		x.Equip(pool.Special("test-foxfire")!);
		x.LevelUp(SpecialSlot.B);
		x.LevelUp(SpecialSlot.A);
		x.Equip(probe);
		// The same slots with only B then A ever levelled.
		var y = RunState.NewRun(pool);
		y.LevelUp(SpecialSlot.B);
		y.LevelUp(SpecialSlot.A);
		y.Equip(probe);
		Assert.Equal("2,1,0,0", Orders(x), "ranks among the specials still in a slot");
		Assert.Equal(Orders(y), Orders(x), "same order");
		Assert.Equal(y.StateHash(), x.StateHash(), "the replaced move's level-up doesn't count (T2), in the hash either");
		x.LevelUp(SpecialSlot.C);
		y.LevelUp(SpecialSlot.C);
		Assert.Equal("probe-c", TargetId(x), "the next level-up is the most recent in both");
		Assert.Equal(y.StateHash(), x.StateHash(), "and they stay equal afterwards");
	}

	[Test]
	public static void RestoreLevelledOrderRebuildsTheSavedRun()
	{
		var pool = Pool();
		var saved = RunState.NewRun(pool);
		saved.LevelUp(SpecialSlot.A);
		saved.LevelUp(SpecialSlot.B);

		// Rebuilt from slots and levels in another sequence, then the order put back.
		var restored = RunState.NewRun(pool);
		restored.LevelUp(SpecialSlot.B);
		restored.LevelUp(SpecialSlot.A);
		Assert.Equal("test-spirit-wave", TargetId(restored), "before restoring: the replay order's target");
		restored.RestoreLevelledOrder(saved.LevelledOrder(SpecialSlot.A), saved.LevelledOrder(SpecialSlot.B),
			saved.LevelledOrder(SpecialSlot.C), saved.LevelledOrder(SpecialSlot.D));
		Assert.Equal(Orders(saved), Orders(restored), "order restored");
		Assert.Equal("test-rising-talisman", TargetId(restored), "T3: the saved run's target");
		Assert.Equal(saved.StateHash(), restored.StateHash(), "same hash as the saved run");

		saved.LevelUp(SpecialSlot.A);
		restored.LevelUp(SpecialSlot.A);
		Assert.Equal(saved.StateHash(), restored.StateHash(), "and the two runs go on identically");
	}

	[Test]
	public static void RestoreLevelledOrderRejectsWhatTheSlotsCantHold()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		run.LevelUp(SpecialSlot.A);
		run.LevelUp(SpecialSlot.B);
		ulong before = run.StateHash();
		Assert.True(Rejects(run, 1, 1, 0, 0), "two slots can't share a level-up");
		Assert.True(Rejects(run, -1, 2, 0, 0), "negative");
		Assert.True(Rejects(run, 1, 2, 3, 0), "slot C is empty");
		Assert.True(Rejects(run, 0, 1, 0, 0), "a Lv 2 special has levelled");
		run.Equip(pool.Special("test-foxfire")!);
		Assert.True(Rejects(run, 1, 2, 3, 0), "a Lv 1 special has not levelled");
		Assert.Equal("1,2,0,0", Orders(run), "a rejected restore changes nothing");
		run.RestoreLevelledOrder(5, 3, 0, 0);
		Assert.Equal("2,1,0,0", Orders(run), "only the relative order of the values matters");
		Assert.True(run.StateHash() != before, "the new order is a different run state");
	}

	[Test]
	public static void RestoredHugeValuesAreStoredAsRanks()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		run.LevelUp(SpecialSlot.A);
		run.LevelUp(SpecialSlot.B);
		run.Equip(pool.Special("test-foxfire")!);
		run.RestoreLevelledOrder(int.MaxValue, 1, 0, 0);
		Assert.Equal("2,1,0,0", Orders(run), "a huge saved value is just 'the most recent'");
		Assert.Equal("test-spirit-wave", TargetId(run), "T3: A is the most recently levelled Lv 2");

		run.LevelUp(SpecialSlot.C);
		Assert.Equal(2, run[SpecialSlot.C]!.Level, "the level-up went through");
		Assert.Equal("2,1,3,0", Orders(run), "T3: the level-up after the restore is the most recent, no wrap-around");
		Assert.Equal("test-foxfire", TargetId(run), "T3: so Foxfire is the target");

		// The same run played without a restore: B, then A, then C.
		var plain = RunState.NewRun(pool);
		plain.Equip(pool.Special("test-foxfire")!);
		plain.LevelUp(SpecialSlot.B);
		plain.LevelUp(SpecialSlot.A);
		plain.LevelUp(SpecialSlot.C);
		Assert.Equal(plain.StateHash(), run.StateHash(), "same state as the run that never restored");

		var two = RunState.NewRun(pool);
		two.LevelUp(SpecialSlot.A);
		two.LevelUp(SpecialSlot.B);
		two.RestoreLevelledOrder(int.MaxValue - 1, int.MaxValue, 0, 0);
		Assert.Equal("1,2,0,0", Orders(two), "two huge values keep their order");
		two.Equip(pool.Special("test-foxfire")!);
		two.Equip(pool.Special("test-foxfire")!);
		Assert.Equal("1,2,3,0", Orders(two), "and a later level-up still ranks above both");
	}
}
