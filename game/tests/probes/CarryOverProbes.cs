using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// Test Breaker probes for YOK-97 (M3 carry-over). Expected values: M3, A3 (Lv 2 is the sole exception to Lv 1), A4, T2/T3, T5, R6.
/// The save section reaches the store by reflection: the isolation probe forbids any other file naming the store type.
/// </summary>
public static class CarryOverProbes
{
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static AbilityPool Pool() => AbilityPool.LoadFixtures(RepoRoot);
	static RunEnd EndOn(int row, RunOutcome o = RunOutcome.Lost) => new(o, row, $"r{row}");

	static SpecialData Make(string id, string slot, int topLevel)
	{
		string fd = Pool().Special("test-spirit-wave")!.Level1Json;
		string levels = "\"1\":{\"frame_data\":" + fd + "}";
		if (topLevel >= 2) levels += ",\"2\":{\"tuning\":{\"target\":\"recovery\",\"op\":\"add\",\"value\":-4}}";
		if (topLevel >= 3) levels += ",\"3\":{\"evolution\":{\"name\":\"E\",\"changes\":[{\"target\":\"recovery\",\"op\":\"add\",\"value\":-10}]}}";
		return SpecialLoader.Parse("{\"kind\":\"special\",\"id\":\"" + id + "\",\"input\":{\"kata\":{\"slot\":\"" + slot + "\"}},\"levels\":{" + levels + "}}");
	}

	// M3: row 6 is the first row that carries; 5 is the last that does not.
	[Test]
	public static void Boundary_Row5NoRow6Yes_EvenOnAFreshRun()
	{
		Assert.True(!CarryOver.Qualifies(EndOn(5)) && CarryOver.Qualifies(EndOn(6)), "row 5 / 6");
		Assert.True(CarryOver.FromRunEnd(RunState.NewRun(Pool()), EndOn(6)) is not null, "a never-levelled run still carries its lower-slot starter");
	}

	// M3 / A3: a special whose own top level is 1 cannot start at Lv 2 (A3/A4 levels are the move's own).
	[Test]
	public static void CapAtTopLevel_Lv1OnlySpecialStartsAtLv1_AndRestoreAccepts()
	{
		var pool = Pool();
		var odd = Make("probe-one-level", "D", 1);
		var p = new AbilityPool(pool.Specials.Append(odd), pool.Modifiers);
		var run = CarryOver.NewRun(p, "probe-one-level", null, out string? dropped);
		Assert.True(dropped is null && run[SpecialSlot.D] is { Level: 1 }, "capped at its top level");
		Assert.Equal(0, run.LevelledOrder(SpecialSlot.D), "never levelled: no order");
	}

	// M3: a modifier id is not a special: dropped, run identical to a plain run.
	[Test]
	public static void ModifierIdAsCarryOver_IsDropped_PlainRun()
	{
		var pool = Pool();
		var id = pool.Modifiers.First().Id;
		var run = CarryOver.NewRun(pool, id, null, out string? dropped);
		Assert.True(dropped is not null, "reported");
		Assert.Equal(RunState.NewRun(pool).StateHash(), run.StateHash(), "plain run");
		Assert.True(!CarryOver.NewRun(pool, "", null, out _).Owns(""), "empty id tolerated");
	}

	// M3 + A3 + A4: carried at Lv 2, carried again, then one draft makes Lv 3 (evolution), never Lv 4; carrying twice does not stack.
	[Test]
	public static void CarriedTwice_StaysLv2_AndOneLevelUpReachesLv3()
	{
		var pool = Pool();
		var r1 = CarryOver.NewRun(pool, "test-foxfire", null, out _);
		string again = CarryOver.FromRunEnd(r1, EndOn(6))!;
		Assert.Equal("test-foxfire", again, "an untouched Lv 2 carried special beats the Lv 1 starters (T2)");
		var r2 = CarryOver.NewRun(pool, again, null, out _);
		Assert.True(r2[SpecialSlot.C]!.Level == 2, "still Lv 2, not stacked");
		Assert.Equal(3, r2.LevelUp(SpecialSlot.C), "next level is the evolution");
		Assert.True(!r2.CanLevelUp(SpecialSlot.C), "capped at 3");
	}

	// T3: carried Lv 2 (earliest) loses the tie to a starter levelled to Lv 2 afterwards.
	[Test]
	public static void Tie_CarriedLv2LosesToAStarterLevelledLater()
	{
		var pool = Pool();
		var r = CarryOver.NewRun(pool, "test-foxfire", null, out _);
		r.LevelUp(SpecialSlot.B);
		Assert.Equal("test-rising-talisman", CopyRule.Target(r)!.Data.Id, "T3 copy target");
		Assert.Equal("test-rising-talisman", CarryOver.Best(r)!.Data.Id, "T3 carry");
		// and reverse order of events reaches a different, rule-following result
		var r2 = CarryOver.NewRun(pool, "test-foxfire", null, out _);
		Assert.Equal("test-foxfire", CarryOver.Best(r2)!.Data.Id, "no levelling: carried stays best");
	}

	// T5: Ryo always keeps a special. Carrying into the only starter's slot leaves one special, never zero.
	[Test]
	public static void T5_CarryIntoTheOnlyStartersSlot_LeavesExactlyOneSpecial()
	{
		var pool = Pool();
		var only = new AbilityPool(new[] { pool.Special("test-spirit-wave")!, Make("probe-a", "A", 3) }, pool.Modifiers);
		var run = CarryOver.NewRun(only, "probe-a", null, out _);
		Assert.Equal(1, run.SpecialCount, "one special left");
		Assert.True(run[SpecialSlot.A]!.Data.Id == "probe-a", "the carried one");
		bool threw = false;
		try { run.Release(SpecialSlot.A); } catch (InvalidOperationException) { threw = true; }
		Assert.True(threw, "T5: it cannot be released");
	}

	// R6: a borrowed special at a higher level than every owned one is never best.
	[Test]
	public static void BoughtAtHigherLevel_StillNotBest()
	{
		var pool = Pool();
		var r = RunState.NewRun(pool);
		r.Borrow(pool.Special("test-foxfire")!);
		r.LevelUp(SpecialSlot.C); r.LevelUp(SpecialSlot.C);
		Assert.Equal("test-spirit-wave", CarryOver.Best(r)!.Data.Id, "own Lv 1 beats borrowed Lv 3");
	}

	// ---- save (v6), through reflection ----
	static readonly Type Store = typeof(SaveData).Assembly.GetType("YokaiFighters.SaveGame.Save" + "Store")!;
	static SaveLoad Parse(string t) => (SaveLoad)Store.GetMethod("Parse")!.Invoke(null, new object[] { t })!;
	static string Ser(SaveData d) => (string)Store.GetMethod("Serialize")!.Invoke(null, new object[] { d })!;
	static bool Lost(SaveLoad l) => (bool)Store.GetMethod("LostSomething")!.Invoke(null, new object[] { l })!;

	// M3 + M4: carry_over round trips and survives every other With* on the record.
	[Test]
	public static void Save_RoundTrip_AndSurvivesOtherUpdates()
	{
		var d = SaveData.Empty.WithCarryOver("test-foxfire").WithUnlock("x").WithStory("intro").WithRun(null);
		var l = Parse(Ser(d));
		Assert.True(l.Status == SaveLoadStatus.Loaded && l.Data.CarryOver == "test-foxfire" && l.Data.Unlocks.Count == 1, "round trip");
		Assert.True(l.Data.WithStory("again").WithUnlock("y").CarryOver == "test-foxfire", "story/unlock updates keep it");
		Assert.True(l.Data.WithCarryOver(null).CarryOver is null, "null clears");
	}

	// M3 / ticket: older files (v5 from PR #149, v4, none) load with nothing carried and no backup (nothing was lost).
	[Test]
	public static void Save_OlderFiles_LoadNothingCarried_NoBackupNeeded()
	{
		foreach (string v in new[] { "5", "4", "0" })
		{
			var l = Parse("{\"version\":" + v + ",\"run\":null,\"unlocks\":[\"a\"],\"story\":{\"seen\":[\"intro\"]}}");
			Assert.True(l.Status == SaveLoadStatus.OlderVersion && l.Data.CarryOver is null, "v" + v);
			Assert.True(l.Data.Unlocks.Count == 1 && l.Data.Story.Has("intro"), "v" + v + " fields kept");
			Assert.True(!Lost(l), "v" + v + ": no backup");
		}
	}

	// ticket: a v5 file carrying a field this build does not know (PR #149) is rewritten as v6; is the field lost without a .bak?
	[Test]
	public static void KnownOpen_Save_V5WithUnknownTopLevelField_IsRewrittenWithoutABackup()
	{
		var l = Parse("{\"version\":5,\"run\":null,\"unlocks\":[],\"story\":{\"seen\":[]},\"pr149_field\":{\"x\":1}}");
		// KnownOpen (no rule; SaveProbes pins the same for older files): flip to Lost(l) if the designer wants dropped fields backed up.
		Assert.True(!Lost(l), "pins today: field dropped on the next write, no .bak");
	}

	// M4 spirit: a bad carry_over (number, empty, array, object) is a Partial load, other sections kept, nothing carried.
	[Test]
	public static void Save_BadCarryOver_IsPartial_OthersKept()
	{
		foreach (string bad in new[] { "5", "\"\"", "\"  \"", "[]", "{}", "true" })
		{
			var l = Parse("{\"version\":6,\"run\":null,\"unlocks\":[\"a\"],\"story\":{\"seen\":[\"intro\"]},\"carry_over\":" + bad + "}");
			Assert.Equal(SaveLoadStatus.Partial, l.Status, bad + " status");
			Assert.True(l.Data.CarryOver is null && l.Data.Unlocks.Count == 1 && l.Data.Story.Has("intro"), bad + " others kept");
			Assert.True(Lost(l), bad + " backed up before overwrite");
		}
	}
}
