using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-97, M3: the best special of a run that reached row 6 carries to the next run at Lv 2; borrowed ink never
/// carries (R6); the carried special is ranked by T2/T3 and becomes the next run's copy target like any other.
/// </summary>
public static class CarryOverTests
{
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static AbilityPool Pool() => AbilityPool.LoadFixtures(RepoRoot);
	static string MapDir => ContentPaths.Data("map");

	static RunEnd EndOn(int row, RunOutcome outcome = RunOutcome.Lost) => new(outcome, row, $"r{row}-node");

	/// <summary>A fixture special (Spirit Wave's Lv 1 data) in <paramref name="slot"/>, with Lv 2 and Lv 3.</summary>
	static SpecialData Make(string id, string slot, bool starter = false)
	{
		string fd = Pool().Special("test-spirit-wave")!.Level1Json;
		string levels = "\"1\":{\"frame_data\":" + fd + "},\"2\":{\"tuning\":{\"target\":\"recovery\",\"op\":\"add\",\"value\":-4}},"
			+ "\"3\":{\"evolution\":{\"name\":\"Probe Evolution\",\"changes\":[{\"target\":\"recovery\",\"op\":\"add\",\"value\":-10}]}}";
		string json = "{\"kind\":\"special\",\"id\":\"" + id + "\"," + (starter ? "\"starter\":true," : "")
			+ "\"input\":{\"kata\":{\"slot\":\"" + slot + "\"}},\"levels\":{" + levels + "}}";
		return SpecialLoader.Parse(json);
	}

	// --- M3: the row-6 condition -------------------------------------------------------------------

	[Test]
	public static void CarryOver_OnlyARunThatReachedRow6Carries()
	{
		var run = RunState.NewRun(Pool());
		run.LevelUp(SpecialSlot.B);
		Assert.True(CarryOver.FromRunEnd(run, EndOn(5)) is null, "lost on row 5: nothing carries");
		Assert.Equal("test-rising-talisman", CarryOver.FromRunEnd(run, EndOn(6)), "lost on row 6 carries");
		Assert.Equal("test-rising-talisman", CarryOver.FromRunEnd(run, EndOn(8)), "rows 6-8 carry");
		Assert.Equal("test-rising-talisman", CarryOver.FromRunEnd(run, EndOn(9, RunOutcome.Won)), "a Tanuki win carries");
		Assert.True(CarryOver.FromRunEnd(run, EndOn(1)) is null, "row 1: nothing");
	}

	// --- "best": T2/T3 ranking over Ryo's own specials --------------------------------------------

	[Test]
	public static void CarryOver_BestIsTheHighestLevel_ThenMostRecentlyLevelled_ThenLowerSlot()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		Assert.Equal("test-spirit-wave", CarryOver.Best(run)!.Data.Id, "full tie at Lv 1: lower slot");
		run.LevelUp(SpecialSlot.B);
		Assert.Equal("test-rising-talisman", CarryOver.Best(run)!.Data.Id, "higher level");
		run.LevelUp(SpecialSlot.A);
		Assert.Equal("test-spirit-wave", CarryOver.Best(run)!.Data.Id, "equal level: most recently levelled");
		run.Equip(pool.Special("test-foxfire")!);
		run.LevelUp(SpecialSlot.C);
		Assert.Equal("test-foxfire", CarryOver.Best(run)!.Data.Id, "a drafted special levelled last wins the tie");
		run.LevelUp(SpecialSlot.B);
		Assert.Equal("test-rising-talisman", CarryOver.Best(run)!.Data.Id, "Lv 3 beats Lv 2");
	}

	[Test]
	public static void CarryOver_BorrowedInkNeverCarries_EvenWhenItIsTheCopyTarget()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		run.Borrow(pool.Special("test-foxfire")!);
		run.LevelUp(SpecialSlot.C);
		run.LevelUp(SpecialSlot.C);
		Assert.Equal("test-foxfire", CopyRule.Target(run)!.Data.Id, "the Tanuki copies a borrowed move (T2 has no filter)");
		Assert.Equal("test-spirit-wave", CarryOver.FromRunEnd(run, EndOn(7)), "but borrowed ink never carries (R6): the best own special does");

		run.Release(SpecialSlot.A);
		run.Release(SpecialSlot.B);
		Assert.True(CarryOver.Best(run) is null && CarryOver.FromRunEnd(run, EndOn(9, RunOutcome.Won)) is null,
			"only borrowed ink left: nothing carries");
	}

	// --- the next run --------------------------------------------------------------------------------

	[Test]
	public static void CarryOver_NextRunStartsWithTheSpecialAtLv2_NoModifierNoEvolution()
	{
		var pool = Pool();
		var ended = RunState.NewRun(pool);
		ended.Equip(pool.Special("test-foxfire")!);
		ended.LevelUp(SpecialSlot.C);
		ended.LevelUp(SpecialSlot.C); // Lv 3, the evolution (A4)
		var mod = pool.Modifiers.First(m => m.AppliesTo(pool.Special("test-foxfire")!));
		ended.Attach(SpecialSlot.C, mod);
		string carried = CarryOver.FromRunEnd(ended, EndOn(6))!;
		Assert.Equal("test-foxfire", carried, "the evolved Lv 3 special is the best");

		var next = CarryOver.NewRun(pool, carried, FileMapSource.Default(MapDir).Load(), out string? dropped);
		Assert.True(dropped is null, "known id");
		var c = next[SpecialSlot.C]!;
		Assert.True(c.Data.Id == "test-foxfire" && c.Level == CarryOver.Level, "carried in its own slot at Lv 2 (M3)");
		Assert.True(c.Modifier is null && !c.Borrowed && !c.ModifierBorrowed, "no modifier, Ryo's own ink (provisional)");
		Assert.True(next[SpecialSlot.A]!.Level == 1 && next[SpecialSlot.B]!.Level == 1, "the starters are Lv 1 (A1)");
		Assert.True(next.Health == RunState.MaxHealth && next.Coins == 0 && next.Map is { Visited.Count: 0 }, "otherwise a fresh run");
		Assert.Equal(1, next.LevelledOrder(SpecialSlot.C), "a Lv 2 special has a level-up order (T3)");
		Assert.Equal("test-foxfire", CopyRule.Target(next)!.Data.Id, "until something passes it, the carried special is the copy target (T2)");
	}

	[Test]
	public static void CarryOver_ALv1BestIsRaisedToLv2_AndTakesTheStartersSlot()
	{
		var pool = Pool();
		string carried = CarryOver.FromRunEnd(RunState.NewRun(pool), EndOn(6))!;
		Assert.Equal("test-spirit-wave", carried, "nothing levelled: the lower slot");
		var next = CarryOver.NewRun(pool, carried, null, out _);
		Assert.True(next[SpecialSlot.A] is { Level: CarryOver.Level } a && a.Data.Id == "test-spirit-wave", "Lv 1 best starts at Lv 2 (provisional)");
		Assert.Equal(2, next.SpecialCount, "no duplicate starter");

		// A drafted special that shares a starter's slot replaces it (A3, provisional).
		var rival = Make("probe-slot-a", "A");
		var mixed = new AbilityPool(pool.Specials.Append(rival), pool.Modifiers);
		var run = CarryOver.NewRun(mixed, "probe-slot-a", null, out _);
		Assert.True(run[SpecialSlot.A] is { Level: 2 } r && r.Data.Id == "probe-slot-a", "the carried special holds slot A");
		Assert.True(!run.Owns("test-spirit-wave") && run.Owns("test-rising-talisman"), "the slot-A starter gives way, the others stay");
	}

	[Test]
	public static void CarryOver_UnknownOrNoCarry_StartsAPlainRun_AndIsDeterministic()
	{
		var pool = Pool();
		var plain = RunState.NewRun(pool);
		var none = CarryOver.NewRun(pool, null, null, out string? d0);
		Assert.True(d0 is null && none.StateHash() == plain.StateHash(), "no carry: the same run as NewRun");
		var gone = CarryOver.NewRun(pool, "removed-special", null, out string? d1);
		Assert.True(d1 is not null && d1.Contains("removed-special") && gone.StateHash() == plain.StateHash(), "an id the data lost is dropped, with a reason");
		var a = CarryOver.NewRun(pool, "test-foxfire", null, out _);
		var b = CarryOver.NewRun(pool, "test-foxfire", null, out _);
		Assert.True(a.StateHash() == b.StateHash() && a.StateHash() != plain.StateHash(), "same carry, same run");
	}

	// --- whole run on the default map ------------------------------------------------------------------

	/// <summary>Walks the first reachable node of every row, winning each, until <paramref name="row"/>; loses there.</summary>
	static RunSession LoseOnRow(int row, uint seed)
	{
		var s = RunSession.Start(Pool(), FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir), seed);
		while (true)
		{
			string id = s.Reachable()[0];
			Assert.True(s.Enter(id).Ok, $"enter {id}");
			if (s.Entered!.Context.Row >= row) { Assert.True(s.LoseFight().Ok, "lost"); return s; }
			if (s.WinFight(600) is { } d)
				Assert.True(d.Pick(0, d.Offer.Cards[0].Targets.Count > 0 ? d.Offer.Cards[0].Targets[0].Slot : null).Ok, "pick");
			Assert.True(s.CompleteNode().Ok, $"complete {id}");
		}
	}

	[Test]
	public static void CarryOver_AWholeRunLostOnRow6CarriesItsBest_Row5DoesNot()
	{
		var six = LoseOnRow(6, 11);
		Assert.True(six.Ended is { RowReached: 6, Won: false }, "lost on row 6");
		string? carried = CarryOver.FromRunEnd(six.Run, six.Ended!);
		Assert.Equal(CarryOver.Best(six.Run)!.Data.Id, carried, "the best special carries");
		var five = LoseOnRow(5, 11);
		Assert.True(CarryOver.FromRunEnd(five.Run, five.Ended!) is null, "row 5: nothing carries");
	}
}
