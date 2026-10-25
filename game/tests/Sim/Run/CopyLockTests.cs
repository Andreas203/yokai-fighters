using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// T1: the Tanuki's copy locks when Ryo enters its node (<see cref="RunSession.LockedCopy"/>). Sim only.
/// Pool = the TEST FIXTURES (<see cref="AbilityPool.LoadFixtures"/>: Spirit Wave in A, Rising Talisman in B);
/// the map and the node-scene table are written here, never read from data/. The map is
/// <c>boss-tanuki → b → den → y → z</c>: the first node only LOOKS like the Tanuki's (its id and its yokai), it
/// is a duel; <c>den</c> and <c>z</c> are of type boss with other yokai, so only the node type can decide.
/// </summary>
public static class CopyLockTests
{
	const uint Seed = 82;

	const string MapJson = """
	{
	  "kind": "run_map", "id": "copy-lock-map", "schema_version": 1,
	  "nodes": [
	    {"id": "boss-tanuki", "row": 1, "type": "duel", "yokai": "tanuki"},
	    {"id": "b", "row": 2, "type": "duel", "yokai": "kitsune"},
	    {"id": "den", "row": 3, "type": "boss", "yokai": "mujina"},
	    {"id": "y", "row": 4, "type": "duel", "yokai": "kitsune"},
	    {"id": "z", "row": 5, "type": "boss", "yokai": "kitsune"}
	  ],
	  "routes": [
	    {"from": "boss-tanuki", "to": "b"}, {"from": "b", "to": "den"}, {"from": "den", "to": "y"}, {"from": "y", "to": "z"}
	  ]
	}
	""";

	// No node binds, so no node offers a draft: the tests change Ryo's slots on the run state directly.
	const string TableJson = """
	{
	  "kind": "node_scenes", "id": "copy-lock-scenes", "schema_version": 1,
	  "default_scene": "res://scenes/fight.tscn", "fallback_fighter": "kitsune",
	  "tiers": { "yokai": { "reaction_frames": 22 } },
	  "types": { "duel": { "tier": "yokai" }, "boss": { "tier": "yokai" } },
	  "opponents": { "kitsune": { "fighter": "kitsune" } }
	}
	""";

	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static AbilityPool Pool() => AbilityPool.LoadFixtures(RepoRoot);
	static MapGraph Graph() => MapLoader.Parse(MapJson);
	static NodeSceneTable Table() => NodeSceneTable.Parse(TableJson);
	static RunSession NewSession() => RunSession.Start(Pool(), Graph(), Table(), Seed);

	/// <summary>A saved run put back: fixture starters, <paramref name="slots"/> applied, the walk restored on the test map.</summary>
	static (RunState Run, AbilityPool Pool) SavedRun(string[] visited, string? current, Action<RunState>? slots = null)
	{
		var pool = Pool();
		var graph = Graph();
		var run = RunState.NewRun(pool);
		slots?.Invoke(run);
		run.SetMap(RunMap.Restore(graph, new RunMapSnapshot(graph.Id, visited, current)));
		return (run, pool);
	}

	/// <summary>
	/// <see cref="RunSession.Restore"/> on a saved walk. <paramref name="copy"/> = the saved lock as (slot, special id,
	/// level), looked up in the session's own pool; null = no special locked.
	/// </summary>
	static RunSession Restored(string[] visited, string? current, bool locked, (SpecialSlot Slot, string Id, int Level)? copy = null, Action<RunState>? slots = null)
	{
		var (run, pool) = SavedRun(visited, current, slots);
		CopyTarget? target = copy is { } c ? new CopyTarget(c.Slot, pool.Special(c.Id)!, c.Level) : null;
		return RunSession.Restore(run, pool, Table(), Seed, locked, target);
	}

	/// <summary>The plain constructor on a saved walk (no lock given).</summary>
	static RunSession Reopened(string[] visited, string? current, Action<RunState>? slots = null)
	{
		var (run, pool) = SavedRun(visited, current, slots);
		return new RunSession(run, pool, Table(), Seed);
	}

	/// <summary>A Lv 1-2 special for <paramref name="slot"/> made from the Spirit Wave fixture's frame data.</summary>
	static SpecialData Make(string id, string slot)
	{
		string fd = Pool().Special("test-spirit-wave")!.Level1Json;
		string levels = "\"1\":{\"frame_data\":" + fd + "},\"2\":{\"tuning\":{\"target\":\"recovery\",\"op\":\"add\",\"value\":-4}}";
		return SpecialLoader.Parse("{\"kind\":\"special\",\"id\":\"" + id + "\",\"input\":{\"kata\":{\"slot\":\"" + slot + "\"}},\"levels\":{" + levels + "}}");
	}

	static void Pass(RunSession s, string id)
	{
		Assert.True(s.Enter(id).Ok, $"enter {id}");
		s.WinFight(600);
		Assert.True(s.CompleteNode().Ok, $"complete {id}");
	}

	/// <summary>The session hash as it was before the lock existed (YOK-77): run state, seed, outcome.</summary>
	static ulong HashWithoutLock(RunSession s)
	{
		ulong r = s.Run.StateHash();
		ulong h = Fnv.Mix(Fnv.Offset, unchecked((int)r));
		h = Fnv.Mix(h, unchecked((int)(r >> 32)));
		h = Fnv.Mix(h, unchecked((int)s.Seed));
		return Fnv.Mix(h, s.Ended is null ? 0 : 1 + (int)s.Ended.Outcome);
	}

	static string Show(CopyTarget? t) => t is null ? "(none)" : $"{t.Data.Id} in {t.Slot} at Lv {t.Level}";

	static void Throws<T>(Action body, string what) where T : Exception
	{
		try { body(); }
		catch (T) { return; }
		throw new AssertFailed($"{what}: expected {typeof(T).Name}");
	}

	[Test]
	public static void NothingIsLockedBeforeTheBossNode_AndTheHashIsTheOldOne()
	{
		var s = NewSession();
		Assert.True(!s.CopyLocked && s.LockedCopy is null, "a new run has no lock");
		Assert.Equal(HashWithoutLock(s), s.StateHash(), "a new run keeps its hash");

		// A node that is the Tanuki's by id and by yokai, but a duel by type: not the lock point.
		Assert.True(s.Enter("boss-tanuki").Ok, "enter the look-alike");
		Assert.True(!s.CopyLocked && s.LockedCopy is null, "T1: the node type decides, not an id or a yokai");
		s.Run.LevelUp(SpecialSlot.B);
		s.WinFight(700);
		Assert.True(s.CompleteNode().Ok, "complete");
		Pass(s, "b");
		Assert.True(!s.CopyLocked && s.LockedCopy is null, "still nothing locked on the map before the boss node");
		Assert.Equal(HashWithoutLock(s), s.StateHash(), "a run that has not reached the boss keeps its hash");

		// Lost before the boss: the run ends with nothing locked.
		Assert.True(s.Enter("den").Ok, "enter the boss node");
		var lostEarly = NewSession();
		lostEarly.Enter("boss-tanuki");
		Assert.True(lostEarly.LoseFight().Ok && lostEarly.IsOver, "lost on row 1");
		Assert.True(!lostEarly.CopyLocked && lostEarly.LockedCopy is null, "a run lost before the boss never locks");
		Assert.Equal(HashWithoutLock(lostEarly), lostEarly.StateHash(), "and keeps its hash");
	}

	[Test]
	public static void EnteringTheBossNodeLocksTheTargetOfThatMoment()
	{
		var s = NewSession();
		Pass(s, "boss-tanuki");
		s.Run.LevelUp(SpecialSlot.B);
		Pass(s, "b");
		var expected = CopyRule.Target(s.Run)!;
		string seen = "not raised";
		s.NodeEntered += n => seen = $"{n.NodeId}: locked {s.CopyLocked}, {Show(s.LockedCopy)}";

		Assert.True(s.Enter("den").Ok, "enter the boss node");
		Assert.True(s.CopyLocked, "T1: locked on entering");
		Assert.Equal("test-rising-talisman in B at Lv 2", Show(s.LockedCopy), "T2: the highest-level special at that moment");
		Assert.Equal(expected, s.LockedCopy, "the lock is CopyRule.Target as the run stood");
		Assert.Equal("den: locked True, test-rising-talisman in B at Lv 2", seen, "the lock is in place when NodeEntered is raised");
		Assert.True(s.StateHash() != HashWithoutLock(s), "the lock is part of the session hash");
	}

	[Test]
	public static void ARefusedEnterLocksNothingAndChangesNoHash()
	{
		var s = NewSession();
		ulong fresh = s.StateHash();
		Assert.True(!s.Enter("den").Ok && !s.Enter("z").Ok && !s.Enter("nowhere").Ok, "refused: boss nodes out of reach, unknown node");
		Assert.True(!s.CopyLocked && s.LockedCopy is null && s.StateHash() == fresh, "a refused pick locks nothing");

		s.Enter("boss-tanuki");
		ulong inNode = s.StateHash();
		Assert.True(!s.Enter("den").Ok, "refused: a node is already entered");
		Assert.True(!s.CopyLocked && s.StateHash() == inNode, "still nothing locked");

		Assert.True(s.LoseFight().Ok, "lost");
		ulong over = s.StateHash();
		Assert.True(!s.Enter("den").Ok, "refused: the run is over");
		Assert.True(!s.CopyLocked && s.LockedCopy is null && s.StateHash() == over, "a pick after the end locks nothing");
	}

	[Test]
	public static void SlotChangesAfterTheLockDoNotChangeTheLockedCopy()
	{
		var s = NewSession();
		Pass(s, "boss-tanuki");
		s.Run.LevelUp(SpecialSlot.B);
		Pass(s, "b");
		s.Enter("den");
		var locked = s.LockedCopy!;
		int lockedRecovery = locked.Build().Recovery;

		// Everything that can happen to Ryo's slots: another special out-levels it, its own slot is replaced.
		var c = Make("probe-c", "C");
		s.Run.Equip(c);
		s.Run.Equip(c);
		s.Run.Equip(Make("probe-b", "B"));
		s.Run.Equip(s.Run[SpecialSlot.B]!.Data);
		Assert.True(CopyRule.Target(s.Run)!.Data.Id != "test-rising-talisman", "the run's target has moved on");
		Assert.True(!s.Run.Owns("test-rising-talisman"), "Ryo no longer holds the locked special");
		Assert.True(ReferenceEquals(locked, s.LockedCopy), "the locked copy is the same object as at the lock");
		Assert.Equal("test-rising-talisman in B at Lv 2", Show(s.LockedCopy), "T1: fixed for the rest of the run");
		Assert.Equal(lockedRecovery, s.LockedCopy!.Build().Recovery, "and it still builds the same move (T4)");

		// On through the run: back on the map, another node, a second node of the boss type. Locked once.
		s.WinFight(500);
		Assert.True(s.CompleteNode().Ok, "the boss node is done");
		Assert.Equal("test-rising-talisman in B at Lv 2", Show(s.LockedCopy), "kept on the map");
		Pass(s, "y");
		Assert.True(s.Enter("z").Ok, "enter a second boss-type node");
		Assert.Equal("test-rising-talisman in B at Lv 2", Show(s.LockedCopy), "the lock happens once: no second lock");
		Assert.True(s.LoseFight().Ok, "lost there");
		Assert.True(s.CopyLocked && Show(s.LockedCopy) == "test-rising-talisman in B at Lv 2", "kept to the end of the run");
	}

	[Test]
	public static void TheSessionHashTellsTwoLocksApart_OnTheSameRunState()
	{
		// Same slots, same walk; only the moment of the level-up differs, so only the lock differs.
		var before = NewSession();
		Pass(before, "boss-tanuki");
		Pass(before, "b");
		before.Run.LevelUp(SpecialSlot.B);
		before.Enter("den");

		var after = NewSession();
		Pass(after, "boss-tanuki");
		Pass(after, "b");
		after.Enter("den");
		after.Run.LevelUp(SpecialSlot.B);

		Assert.Equal(before.Run.StateHash(), after.Run.StateHash(), "the two runs hold the same state");
		Assert.Equal("test-rising-talisman in B at Lv 2", Show(before.LockedCopy), "levelled before the node: Lv 2 is copied");
		Assert.Equal("test-spirit-wave in A at Lv 1", Show(after.LockedCopy), "levelled inside the node: too late (T1), T3 lower slot");
		Assert.True(before.StateHash() != after.StateHash(), "the hash covers the lock");

		// Same moves in the same order: the same lock, the same hash.
		var twin = NewSession();
		Pass(twin, "boss-tanuki");
		Pass(twin, "b");
		twin.Run.LevelUp(SpecialSlot.B);
		twin.Enter("den");
		Assert.Equal(before.StateHash(), twin.StateHash(), "deterministic");
	}

	[Test]
	public static void ARunWithNoSpecialLocksNoCopy()
	{
		var graph = Graph();
		var run = new RunState();
		run.StartMap(graph);
		var s = new RunSession(run, Pool(), Table(), Seed);
		Pass(s, "boss-tanuki");
		Pass(s, "b");
		s.Enter("den");
		Assert.True(s.CopyLocked && s.LockedCopy is null, "locked, with nothing to copy (CopyRule.Target is null)");
		s.Run.Equip(Make("probe-c", "C"));
		Assert.True(s.LockedCopy is null, "a special drafted after the lock is not copied");
		Assert.True(s.StateHash() != HashWithoutLock(s), "an empty lock still differs from no lock");
	}

	static readonly string[] ToDen = { "boss-tanuki", "b" };
	static readonly string[] PastDen = { "boss-tanuki", "b", "den" };
	static readonly string[] ToZ = { "boss-tanuki", "b", "den", "y" };
	static readonly (SpecialSlot, string, int) WaveLv1 = (SpecialSlot.A, "test-spirit-wave", 1);
	const string WaveLv1Text = "test-spirit-wave in A at Lv 1";
	static void BLv2At600(RunState r) { r.LevelUp(SpecialSlot.B); r.SetHealth(600); }

	[Test]
	public static void ARestoredWalk_KeepsTheSavedLock_WhenTheSlotsChangedInsideTheNode()
	{
		// Entered with everything at Lv 1 (lock: Spirit Wave in A), then B levelled inside the node.
		var original = NewSession();
		Pass(original, "boss-tanuki");
		Pass(original, "b");
		original.Enter("den");
		original.Run.LevelUp(SpecialSlot.B);
		Assert.Equal(WaveLv1Text, Show(original.LockedCopy), "the lock of the moment of entry");
		Assert.Equal("test-rising-talisman", CopyRule.Target(original.Run)!.Data.Id, "the slots now say otherwise");

		var restored = Restored(ToDen, "den", original.CopyLocked, WaveLv1, BLv2At600);
		Assert.True(restored.CopyLocked && Show(restored.LockedCopy) == WaveLv1Text, "T1: the saved lock, not today's slots");
		Assert.Equal(original.StateHash(), restored.StateHash(), "restored = original");
		Assert.True(restored.Node is { NodeId: "den" }, "still inside the node");

		// Without the saved lock there is no session: the lock is never guessed from the slots.
		Throws<ArgumentException>(() => Reopened(ToDen, "den", BLv2At600), "the plain constructor inside the boss node");
		Throws<ArgumentException>(() => Restored(ToDen, "den", locked: false, slots: BLv2At600), "Restore told nothing is locked, inside the boss node");
	}

	[Test]
	public static void ARestoredWalk_InsideASecondBossNode_KeepsTheFirstNodesLock()
	{
		var original = NewSession();
		Pass(original, "boss-tanuki");
		Pass(original, "b");
		Pass(original, "den");
		original.Run.LevelUp(SpecialSlot.B);
		Pass(original, "y");
		original.Enter("z");
		Assert.Equal(WaveLv1Text, Show(original.LockedCopy), "locked at den, before the level-up");

		var restored = Restored(ToZ, "z", true, WaveLv1, BLv2At600);
		Assert.Equal(WaveLv1Text, Show(restored.LockedCopy), "T1: the lock point was den, not the node Ryo is in now");
		Assert.Equal(original.StateHash(), restored.StateHash(), "restored = original");
		Throws<ArgumentException>(() => Reopened(ToZ, "z", BLv2At600), "no lock given inside the second boss node");
	}

	[Test]
	public static void ARestoredWalk_PastTheBossNode_NeedsItsLock_AndNeverLocksLater()
	{
		Throws<ArgumentException>(() => Reopened(PastDen, "den"), "on the map past the boss node, no lock given");
		Throws<ArgumentException>(() => Restored(PastDen, "den", locked: false), "Restore told nothing is locked, past the boss node");

		var past = Restored(PastDen, "den", true, WaveLv1);
		Assert.True(past.CopyLocked && Show(past.LockedCopy) == WaveLv1Text && past.Node is null, "locked, on the map");
		past.Run.LevelUp(SpecialSlot.B);
		Pass(past, "y");
		Assert.True(past.Enter("z").Ok, "enter the second boss-type node");
		Assert.Equal(WaveLv1Text, Show(past.LockedCopy), "no lock later than T1 allows");

		// A lock that found no special is restored as such, and stays empty.
		var empty = Restored(PastDen, "den", true);
		Assert.True(empty.CopyLocked && empty.LockedCopy is null, "an empty lock is still a lock");
		Pass(empty, "y");
		empty.Enter("z");
		Assert.True(empty.LockedCopy is null, "and is not filled in at a later boss-type node");
	}

	[Test]
	public static void ARestoredWalk_BeforeTheBossNode_HasNoLock_AndBadLocksAreRefused()
	{
		foreach (var (visited, current, where) in new[]
		{
			(Array.Empty<string>(), (string?)null, "at the start"),
			(new[] { "boss-tanuki" }, (string?)"boss-tanuki", "on the map after row 1"),
			(new[] { "boss-tanuki" }, (string?)"b", "inside a duel"),
		})
		{
			var early = Restored(visited, current, locked: false);
			Assert.True(!early.CopyLocked && early.LockedCopy is null, $"{where}: nothing locked");
			Assert.Equal(HashWithoutLock(early), early.StateHash(), $"{where}: the old hash");
			Assert.Equal(Reopened(visited, current).StateHash(), early.StateHash(), $"{where}: the plain constructor still opens it, the same");
			Throws<ArgumentException>(() => Restored(visited, current, true, WaveLv1), $"{where}: no lock before the boss node (T1)");
			Throws<ArgumentException>(() => Restored(visited, current, true), $"{where}: not even an empty one");
			Throws<ArgumentException>(() => Restored(visited, current, false, WaveLv1), $"{where}: a copy without a lock");
		}
		// It then locks on entering, as a run that was never saved does.
		var s = Restored(ToDen, "b", locked: false, slots: BLv2At600);
		Assert.True(s.Enter("den").Ok && Show(s.LockedCopy) == "test-rising-talisman in B at Lv 2", "restored before the node: locks on entry");

		// Values that can't be a copy target.
		Throws<ArgumentException>(() => Restored(PastDen, "den", false, WaveLv1), "a copy with the lock flag off");
		Throws<ArgumentException>(() => Restored(PastDen, "den", true, (SpecialSlot.C, "test-spirit-wave", 1)), "a slot that is not the special's own");
		Throws<ArgumentException>(() => Restored(PastDen, "den", true, (SpecialSlot.None, "test-spirit-wave", 1)), "no slot");
		Throws<ArgumentException>(() => Restored(PastDen, "den", true, (SpecialSlot.A, "test-spirit-wave", 0)), "level 0");
		int top = Pool().Special("test-spirit-wave")!.MaxLevel;
		Throws<ArgumentException>(() => Restored(PastDen, "den", true, (SpecialSlot.A, "test-spirit-wave", top + 1)), "a level the special does not have");
		Assert.True(Restored(PastDen, "den", true, (SpecialSlot.A, "test-spirit-wave", top)).LockedCopy!.Level == top, "its top level is fine");
		var (run, pool) = SavedRun(PastDen, "den");
		Throws<ArgumentException>(() => RunSession.Restore(run, pool, Table(), Seed, true, new CopyTarget(SpecialSlot.C, Make("probe-c", "C"), 1)), "a special that is not in the pool");
		Throws<ArgumentException>(() => RunSession.Restore(run, pool, Table(), Seed, true, new CopyTarget(SpecialSlot.A, Pool().Special("test-spirit-wave")!, 1)), "another pool's object under the same id");
	}
}
