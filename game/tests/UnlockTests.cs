using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-86, M1: unlocks by the depth a run reached (row ≤5 → 0; rows 6-8 → 1; reached the Tanuki → 2; Tanuki win → 4),
/// awarded at run end in a fixed order.
/// </summary>
public static class UnlockTests
{
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");

	static RunEnd Lost(int row) => new(RunOutcome.Lost, row, $"r{row}");
	static RunEnd AtTanuki(bool won) => new(won ? RunOutcome.Won : RunOutcome.Lost, 9, "boss-tanuki") { AtTanuki = true };

	static readonly string[] Twelve =
		{ "u1", "u2", "u3", "u4", "u5", "u6", "u7", "u8", "u9", "u10", "u11", "u12" };

	[Test]
	public static void CountFollowsM1Table()
	{
		for (int row = 1; row <= 5; row++)
			Assert.Equal(0, Unlocks.CountFor(Lost(row)), $"lost on row {row}");
		for (int row = 6; row <= 8; row++)
			Assert.Equal(1, Unlocks.CountFor(Lost(row)), $"lost on row {row}");
		Assert.Equal(2, Unlocks.CountFor(AtTanuki(false)), "lost to the Tanuki");
		Assert.Equal(4, Unlocks.CountFor(AtTanuki(true)), "beat the Tanuki");
		Assert.Equal(0, Unlocks.CountFor(new RunEnd(RunOutcome.Won, 2, "demo")), "a won map that ends on row 2 (demo)");
	}

	[Test]
	public static void PushingOnNeverEarnsLess()
	{
		int last = -1;
		foreach (var end in Enumerable.Range(1, 8).Select(Lost).Append(AtTanuki(false)).Append(AtTanuki(true)))
		{
			int n = Unlocks.CountFor(end);
			Assert.True(n >= last, $"{end}: {n} after {last}");
			last = n;
		}
	}

	[Test]
	public static void AwardTakesTheNextStillLockedInOrder()
	{
		var u = new Unlocks(Twelve);
		Assert.Equal(0, u.Award(Lost(5), Array.Empty<string>()).Count, "row 5: none");
		Assert.Equal("u1", string.Join(",", u.Award(Lost(7), Array.Empty<string>())), "row 7: the first");
		Assert.Equal("u1,u2", string.Join(",", u.Award(AtTanuki(false), Array.Empty<string>())), "Tanuki: the first two");
		Assert.Equal("u2,u4,u5,u6", string.Join(",", u.Award(AtTanuki(true), new[] { "u1", "u3" })),
			"a win skips ids already unlocked, whatever order they were saved in");
		Assert.Equal("u12", string.Join(",", u.Award(AtTanuki(true), Twelve.Take(11))), "fewer left than earned");
		Assert.Equal(0, u.Award(AtTanuki(true), Twelve).Count, "all unlocked: nothing more");
	}

	[Test]
	public static void SameEndAndSaveAlwaysGiveTheSameIds()
	{
		var a = new Unlocks(Twelve);
		var b = new Unlocks(Twelve);
		var have = new[] { "u2" };
		Assert.Equal(string.Join(",", a.Award(AtTanuki(true), have)), string.Join(",", b.Award(AtTanuki(true), have)), "deterministic");
	}

	[Test]
	public static void TwelveTakeFourToSixRuns_ForATypicalSpread()
	{
		// M2 is "roughly 4-6 runs"; pushing on: Tanuki loss, Tanuki loss, Tanuki win, Tanuki win = 12 in 4 runs.
		var u = new Unlocks(Twelve);
		var have = new System.Collections.Generic.List<string>();
		foreach (var end in new[] { AtTanuki(false), AtTanuki(false), AtTanuki(true), AtTanuki(true) })
			have.AddRange(u.Award(end, have));
		Assert.Equal(12, have.Count, "every lock opened");
		Assert.Equal(string.Join(",", Twelve), string.Join(",", have), "in the fixed order");
	}

	[Test]
	public static void OrderRejectsBlankAndDuplicateIds()
	{
		bool threw = false;
		try { _ = new Unlocks(new[] { "a", "a" }); } catch (FormatException) { threw = true; }
		Assert.True(threw, "duplicate id rejected");
		threw = false;
		try { _ = new Unlocks(new[] { "a", " " }); } catch (FormatException) { threw = true; }
		Assert.True(threw, "blank id rejected");
	}

	[Test]
	public static void OrderFromData_KeepsAnIdNamedTwiceOnce_AndReportsIt()
	{
		var pool = AbilityPool.LoadRepo(RepoRoot);
		string mod = pool.Modifiers.First(m => m.Locked).Id;
		var u = Unlocks.FromData(pool, new[] { mod, "cancel-x", "cancel-x", " " }, out var dropped);
		Assert.Equal(1, u.Order.Count(id => id == mod), "the shared id is kept once");
		Assert.True(u.Order.ToList().IndexOf(mod) < u.Order.ToList().IndexOf("cancel-x"), "first occurrence wins (the modifier's place)");
		Assert.Equal(1, u.Order.Count(id => id == "cancel-x"), "a cancel id twice: once");
		Assert.Equal(3, dropped.Count, "the shared id, the repeat and the blank are reported");
	}

	[Test]
	public static void OrderFromRepoDataIsEveryLockedAbility()
	{
		var pool = AbilityPool.LoadRepo(RepoRoot);
		var cancels = CancelRuleLoader.LockedIds(Path.Combine(RepoRoot, "data", "cancels"));
		var u = Unlocks.FromData(pool, cancels);
		var expected = pool.Specials.Where(s => s.Locked && !s.Starter).Select(s => s.Id)
			.Concat(pool.Modifiers.Where(m => m.Locked).Select(m => m.Id))
			.Concat(cancels).ToArray();
		Assert.Equal(string.Join(",", expected), string.Join(",", u.Order), "specials, then modifiers, then cancel rules");
		Assert.True(u.Order.Contains("undertow"), "the locked cancel rule (A11) is in the order");
		Assert.True(!u.Order.Contains("fox-step"), "an unlocked cancel rule is not");
		Assert.True(u.Order.All(id => pool.Special(id) is null || !pool.Special(id)!.Starter), "no starter is ever locked (A1)");
		Assert.True(u.Order.Count <= 12, $"A11: at most 12 locked, got {u.Order.Count}");
	}
}
