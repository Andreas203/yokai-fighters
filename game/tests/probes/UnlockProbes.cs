using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-86 (M1 unlocks by depth, A11 locked cancel rules).</summary>
public static class UnlockProbes
{
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static readonly string[] Ids = { "a", "b", "c", "d", "e", "f" };

	static void WithDir(Action<string> body)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-unlock-probes-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try { body(dir); } finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
	}

	static string Rule(string id, string lockedJson) => "{ \"kind\":\"cancel_rule\", \"id\":\"" + id + "\"," + lockedJson +
		" \"path\":{\"from\":\"specials\",\"into\":\"dash\",\"on\":[\"hit\"]}, \"once_per_combo\":true }";

	// M1: row 9 reached without the Tanuki node (a lost run on a longer map) is still "rows 6+" -> 1, not 2 or 4.
	[Test]
	public static void NonTanukiRowNine_IsStillOne_M1()
	{
		Assert.Equal(1, Unlocks.CountFor(new RunEnd(RunOutcome.Lost, 9, "x")), "lost row 9, not Tanuki");
		Assert.Equal(1, Unlocks.CountFor(new RunEnd(RunOutcome.Won, 12, "x")), "won past row 9 without the Tanuki node");
		Assert.Equal(0, Unlocks.CountFor(new RunEnd(RunOutcome.Lost, 0, "x")), "row 0");
	}

	// M1: unlock ids not in the order (stale save / removed ability) must not eat the award.
	[Test]
	public static void UnknownSavedIds_DoNotConsumeTheAward_M1()
	{
		var u = new Unlocks(Ids);
		var end = new RunEnd(RunOutcome.Won, 9, "t") { AtTanuki = true };
		Assert.Equal("a,b,c,d", string.Join(",", u.Award(end, new[] { "ghost1", "ghost2", "ghost3", "ghost4" })), "unknown ids ignored");
		Assert.Equal("", string.Join(",", u.Award(end, Ids.Append("ghost"))), "all known unlocked");
	}

	// M1 "fixed order": the award does not depend on the order the save lists its ids, nor on duplicates.
	[Test]
	public static void SaveOrderAndDuplicatesDoNotChangeTheAward_M1()
	{
		var u = new Unlocks(Ids);
		var end = new RunEnd(RunOutcome.Lost, 9, "t") { AtTanuki = true };
		Assert.Equal("a,d", string.Join(",", u.Award(end, new[] { "c", "b", "b", "c" })), "b,c in any order or twice");
	}

	// Re-entry: two award rounds on the same end give consecutive ids, never the same one twice.
	[Test]
	public static void RepeatedAwardsNeverRepeatAnId_M1()
	{
		var u = new Unlocks(Ids);
		var have = new System.Collections.Generic.List<string>();
		var end = new RunEnd(RunOutcome.Lost, 7, "r");
		for (int i = 0; i < 8; i++) have.AddRange(u.Award(end, have));
		Assert.Equal("a,b,c,d,e,f", string.Join(",", have), "one per run, six ids, then nothing");
	}

	// Robustness: a locked modifier and a locked cancel rule sharing an id make FromData throw. GameFlow.UnlockOrder
	// (GameFlow.cs:413) does not catch it, so RunEnded would skip the whole write. FromData itself should not throw on that.
	[Test]
	public static void LockedModifierAndCancelRuleSharingAnId_DoNotThrow()
	{
		var pool = AbilityPool.LoadRepo(RepoRoot);
		var mod = pool.Modifiers.FirstOrDefault(m => m.Locked);
		Assert.True(mod is not null, "the data has a locked modifier");
		bool threw = false;
		try { _ = Unlocks.FromData(pool, new[] { mod!.Id }); } catch (FormatException) { threw = true; }
		Assert.True(!threw, "a shared id must not abort the run-end write; skip or dedupe it");
	}

	// A11: a cancel file without `locked` is not locked; `locked: false` is not; a non-bool `locked` must not crash.
	[Test]
	public static void LockedIds_MissingFalseAndStringLocked()
	{
		WithDir(dir =>
		{
			File.WriteAllText(Path.Combine(dir, "a.json"), Rule("a", ""));
			File.WriteAllText(Path.Combine(dir, "b.json"), Rule("b", "\"locked\":false,"));
			File.WriteAllText(Path.Combine(dir, "c.json"), Rule("c", "\"locked\":true,"));
			File.WriteAllText(Path.Combine(dir, "d.json"), Rule("d", "\"locked\":\"true\","));
			File.WriteAllText(Path.Combine(dir, "e.json"), Rule("e", "\"locked\":null,"));
			Assert.Equal("c", string.Join(",", CancelRuleLoader.LockedIds(dir)), "only a boolean true locks");
		});
	}

	// Determinism / order: sorted by file name, whatever the creation order.
	[Test]
	public static void LockedIds_SortedByFileName()
	{
		WithDir(dir =>
		{
			File.WriteAllText(Path.Combine(dir, "z.json"), Rule("zed", "\"locked\":true,"));
			File.WriteAllText(Path.Combine(dir, "b.json"), Rule("bee", "\"locked\":true,"));
			Assert.Equal("bee,zed", string.Join(",", CancelRuleLoader.LockedIds(dir)), "file-name order");
			Assert.Equal(0, CancelRuleLoader.LockedIds(Path.Combine(dir, "missing")).Length, "missing folder");
		});
	}

	// A malformed file throws a type GameFlow's catch lists (FormatException/JsonException/IOException/InvalidOperation).
	[Test]
	public static void LockedIds_MalformedFilesThrowOnlyCaughtTypes()
	{
		string[] bodies = { "{ not json", "[]", "null", "{\"kind\":\"cancel_rule\"}", "{\"kind\":\"cancel_rule\",\"path\":[]}" };
		foreach (string body in bodies)
			WithDir(dir =>
			{
				File.WriteAllText(Path.Combine(dir, "x.json"), body);
				try { CancelRuleLoader.LockedIds(dir); }
				catch (Exception e) when (e is FormatException or IOException or System.Text.Json.JsonException or InvalidOperationException) { }
				catch (Exception e) { Assert.True(false, $"'{body}' threw uncaught {e.GetType().Name}: {e.Message}"); }
			});
	}

	// Round 2: a duplicate keeps its FIRST position (specials, then modifiers, then cancels) and is reported once per extra copy.
	[Test]
	public static void FromData_DuplicateKeepsFirstPosition_AndReportsDropped()
	{
		var pool = AbilityPool.LoadRepo(RepoRoot);
		var baseOrder = Unlocks.FromData(pool).Order;
		var mod = pool.Modifiers.First(m => m.Locked).Id;
		var withDup = Unlocks.FromData(pool, new[] { mod, "zz-new", "zz-new" }, out var dropped);
		Assert.Equal(string.Join(",", baseOrder) + ",zz-new", string.Join(",", withDup.Order), "first occurrence wins, later copies gone");
		Assert.Equal(2, dropped.Count, "modifier copy and second zz-new reported");
		var again = Unlocks.FromData(pool, new[] { mod, "zz-new", "zz-new" });
		Assert.Equal(string.Join(",", withDup.Order), string.Join(",", again.Order), "deterministic, both overloads agree");
	}

	// Round 2: blank / whitespace / null cancel ids are skipped, not thrown on; padded ids are kept as distinct ids.
	[Test]
	public static void FromData_BlankAndNullCancelIdsAreSkipped()
	{
		var pool = AbilityPool.LoadRepo(RepoRoot);
		var baseCount = Unlocks.FromData(pool).Order.Count;
		var u = Unlocks.FromData(pool, new string[] { "", "  ", null!, "	", "ok" }, out var dropped);
		Assert.Equal(baseCount + 1, u.Order.Count, "only 'ok' added");
		Assert.Equal(4, dropped.Count, "four blanks reported");
		Assert.True(u.Order.Last() == "ok", "ok last");
	}

	// Round 2: empty-order fallback: the run end still awards nothing and does not throw, whatever the depth.
	[Test]
	public static void EmptyOrder_AwardsNothingAtAnyDepth()
	{
		var u = new Unlocks(Array.Empty<string>());
		foreach (var end in new[] { new RunEnd(RunOutcome.Lost, 3, "a"), new RunEnd(RunOutcome.Lost, 7, "a"),
			new RunEnd(RunOutcome.Won, 9, "t") { AtTanuki = true } })
			Assert.Equal(0, u.Award(end, new[] { "x" }).Count, "nothing to award");
	}

	// Round 2: the constructor stays strict for a designer-written order.
	[Test]
	public static void Constructor_StaysStrict()
	{
		bool threw = false;
		try { _ = new Unlocks(new[] { "a", "b", "a" }); } catch (FormatException) { threw = true; }
		Assert.True(threw, "designer order with a duplicate is rejected");
	}

	// Round 2: LockedIds still parses once and yields the same ids as before; a cancel file with a blank id is rejected or skipped, not crashing uncaught.
	[Test]
	public static void LockedIds_BlankIdFile_ThrowsOnlyCaughtTypes()
	{
		WithDir(dir =>
		{
			File.WriteAllText(Path.Combine(dir, "x.json"), Rule("  ", "\"locked\":true,"));
			try { var ids = CancelRuleLoader.LockedIds(dir); Unlocks.FromData(AbilityPool.LoadRepo(RepoRoot), ids, out _); }
			catch (Exception e) when (e is FormatException or IOException or System.Text.Json.JsonException or InvalidOperationException) { }
		});
	}
}
