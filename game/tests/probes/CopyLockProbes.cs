using System;
using System.IO;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

public static class CopyLockProbes
{
	const string MapJson = """
	{ "kind": "run_map", "id": "probe-map", "schema_version": 1,
	  "nodes": [
	    {"id": "a", "row": 1, "type": "duel", "yokai": "kitsune"},
	    {"id": "boss1", "row": 2, "type": "boss", "yokai": "kitsune"},
	    {"id": "c", "row": 3, "type": "duel", "yokai": "kitsune"}
	  ],
	  "routes": [ {"from": "a", "to": "boss1"}, {"from": "boss1", "to": "c"} ] }
	""";
	const string FirstBossJson = """
	{ "kind": "run_map", "id": "probe-map2", "schema_version": 1,
	  "nodes": [
	    {"id": "boss0", "row": 1, "type": "boss", "yokai": "kitsune"},
	    {"id": "d", "row": 2, "type": "duel", "yokai": "kitsune"}
	  ],
	  "routes": [ {"from": "boss0", "to": "d"} ] }
	""";
	const string TableJson = """
	{ "kind": "node_scenes", "id": "probe-scenes", "schema_version": 1,
	  "default_scene": "res://scenes/fight.tscn", "fallback_fighter": "kitsune",
	  "tiers": { "yokai": { "reaction_frames": 22 } },
	  "types": { "duel": { "tier": "yokai" }, "boss": { "tier": "yokai" } },
	  "opponents": { "kitsune": { "fighter": "kitsune" } } }
	""";
	static string Root => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static AbilityPool Pool() => AbilityPool.LoadFixtures(Root);
	static RunSession Sess(string map = MapJson) => RunSession.Start(Pool(), MapLoader.Parse(map), NodeSceneTable.Parse(TableJson), 5);
	static string Show(CopyTarget? t) => t is null ? "(none)" : $"{t.Data.Id} {t.Slot} Lv{t.Level}";
	static void Pass(RunSession s, string id) { Assert.True(s.Enter(id).Ok, id); s.WinFight(600); Assert.True(s.CompleteNode().Ok, id); }

	// T3: tie on level -> most recently levelled, whichever order they were levelled in.
	[Test]
	public static void TieGoesToTheMostRecentlyLevelled_BothOrders()
	{
		var ab = Sess(); Pass(ab, "a");
		ab.Run.LevelUp(SpecialSlot.A); ab.Run.LevelUp(SpecialSlot.B);
		ab.Enter("boss1");
		Assert.Equal("test-rising-talisman B Lv2", Show(ab.LockedCopy), "A then B -> B");
		var ba = Sess(); Pass(ba, "a");
		ba.Run.LevelUp(SpecialSlot.B); ba.Run.LevelUp(SpecialSlot.A);
		ba.Enter("boss1");
		Assert.Equal("test-spirit-wave A Lv2", Show(ba.LockedCopy), "B then A -> A");
		Assert.True(ab.StateHash() != ba.StateHash(), "different locks, different hashes");
	}

	const string TwoBossJson = """
	{ "kind": "run_map", "id": "probe-map3", "schema_version": 1,
	  "nodes": [
	    {"id": "a", "row": 1, "type": "duel", "yokai": "kitsune"},
	    {"id": "boss1", "row": 2, "type": "boss", "yokai": "kitsune"},
	    {"id": "c", "row": 3, "type": "duel", "yokai": "kitsune"},
	    {"id": "boss2", "row": 4, "type": "boss", "yokai": "kitsune"}
	  ],
	  "routes": [ {"from": "a", "to": "boss1"}, {"from": "boss1", "to": "c"}, {"from": "c", "to": "boss2"} ] }
	""";

	static RunSession Rest(string[] visited, string? cur, bool locked, CopyTarget? copy, Action<RunState>? slots = null)
	{
		var g = MapLoader.Parse(TwoBossJson); var pool = Pool();
		var run = RunState.NewRun(pool); slots?.Invoke(run);
		run.SetMap(RunMap.Restore(g, new RunMapSnapshot(g.Id, visited, cur)));
		return RunSession.Restore(run, pool, NodeSceneTable.Parse(TableJson), 5, locked, copy is null ? null : new CopyTarget(copy.Slot, pool.Special(copy.Data.Id)!, copy.Level));
	}

	// T1: live session inside / past boss1 and inside boss2 equals the restored one (lock and hash); boss2 does not relock.
	[Test]
	public static void RestoredSessionsEqualLive_AtEveryPointAfterTheLock()
	{
		var live = Sess(TwoBossJson); Pass(live, "a");
		live.Run.LevelUp(SpecialSlot.B);
		live.Enter("boss1");
		var copy = live.LockedCopy!;
		var r1 = Rest(new[] { "a" }, "boss1", true, copy, r => r.LevelUp(SpecialSlot.B));
		r1.Run.SetHealth(live.Run.Health);
		Assert.Equal(Show(copy), Show(r1.LockedCopy), "inside boss1");
		Assert.Equal(live.StateHash(), r1.StateHash(), "hash inside boss1");
		live.WinFight(600); live.CompleteNode();
		var r2 = Rest(new[] { "a", "boss1" }, "boss1", true, copy, r => r.LevelUp(SpecialSlot.B));
		r2.Run.SetHealth(live.Run.Health);
		Assert.Equal(live.StateHash(), r2.StateHash(), "hash past boss1");
		// level-up after the lock, then a second boss-type node: neither side relocks
		live.Run.LevelUp(SpecialSlot.A); live.Run.LevelUp(SpecialSlot.A);
		Pass(live, "c");
		Assert.True(live.Enter("boss2").Ok, "enter boss2");
		Assert.Equal(Show(copy), Show(live.LockedCopy), "live keeps first lock");
		r2.Run.LevelUp(SpecialSlot.A); r2.Run.LevelUp(SpecialSlot.A);
		Pass(r2, "c");
		Assert.True(r2.Enter("boss2").Ok, "restored enters boss2");
		Assert.Equal(Show(copy), Show(r2.LockedCopy), "restored keeps first lock");
		Assert.True(r2.CopyLocked, "still locked");
	}

	// T1: a refused Restore does not alter the run it was given.
	[Test]
	public static void RefusedRestoreLeavesTheRunUntouched()
	{
		var g = MapLoader.Parse(TwoBossJson); var pool = Pool();
		var run = RunState.NewRun(pool);
		run.SetMap(RunMap.Restore(g, new RunMapSnapshot(g.Id, new[] { "a" }, "boss1")));
		ulong h = run.StateHash();
		Throws(() => RunSession.Restore(run, pool, NodeSceneTable.Parse(TableJson), 5, false, null));
		Throws(() => new RunSession(run, pool, NodeSceneTable.Parse(TableJson), 5));
		Assert.Equal(h, run.StateHash(), "run unchanged");
	}
	static void Throws(Action a)
	{
		try { a(); } catch (ArgumentException) { return; }
		throw new AssertFailed("expected ArgumentException");
	}
}
