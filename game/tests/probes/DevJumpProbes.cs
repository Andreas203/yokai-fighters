using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-125 probes: ShapeCopy over varied run states (T2-T4, A1, R6).</summary>
public static class DevJumpProbes
{
	// Builds one varied run: starters levelled to the given extras, then the listed yokai specials equipped at the given levels.
	static RunState Build(AbilityPool pool, int starterExtra, (string Id, int Level)[] held)
	{
		var run = RunState.NewRun(pool);
		foreach (var st in pool.Starters)
			for (int i = 0; i < starterExtra && run.CanLevelUp(st.Slot); i++) run.LevelUp(st.Slot);
		foreach (var (id, lv) in held)
		{
			var d = pool.Special(id)!;
			if (run[d.Slot] is { } cur && cur.Data.Starter) continue; // keep starters
			run.Equip(d);
			while (run[d.Slot]!.Level < lv && run.CanLevelUp(d.Slot)) run.LevelUp(d.Slot);
		}
		return run;
	}

	// T2-T4 + A1/R6: success means exactly (special, level) is the target and every starter is still held at its original level.
	[Test]
	public static void ShapeCopy_OnVariedRunStates_IsExactOrRefuses_NeverReleasesOrLowersAStarter()
	{
		var pool = FightScene.LoadAbilityPool();
		var yokai = pool.Specials.Where(s => !s.Starter && !s.Locked).ToArray();
		var holds = new List<(string, int)[]> { Array.Empty<(string, int)>() };
		foreach (var y in yokai) for (int lv = 1; lv <= Math.Min(3, y.MaxLevel); lv++) holds.Add(new[] { (y.Id, lv) });
		if (yokai.Length >= 2) holds.Add(yokai.Select(y => (y.Id, Math.Min(3, y.MaxLevel))).ToArray());
		int cases = 0;
		foreach (int extra in new[] { 0, 1, 2 })
			foreach (var h in holds)
				foreach (var s in pool.Specials)
					for (int lv = 1; lv <= Math.Min(3, s.MaxLevel); lv++)
					{
						var run = Build(pool, extra, h);
						var before = pool.Starters.Select(st => (st.Id, st.Slot, Level: run[st.Slot]?.Level ?? -1)).ToArray();
						string tag = $"extra {extra}, held [{string.Join(",", h.Select(x => x.Item1 + x.Item2))}], {s.Id} Lv {lv}";
						CopyTarget t;
						try { t = DevJump.ShapeCopy(run, pool, s.Id, lv); }
						catch (ArgumentException) { continue; }
						cases++;
						Assert.True(t.Data.Id == s.Id && t.Level == lv, tag + ": target is the chosen special and level");
						Assert.True(CopyRule.Target(run) == t, tag + ": the copy rule agrees");
						foreach (var (id, slot, level) in before.Where(b => b.Id != s.Id))
							Assert.True(run[slot] is { } o && o.Data.Id == id && o.Level == level, tag + $": starter {id} kept at Lv {level}");
					}
		Assert.True(cases > 0, "some case is reachable");
	}

	// A3/A6: a yokai special sharing a slot with a starter would replace it; ShapeCopy must refuse, not replace.
	[Test]
	public static void ShapeCopy_NeverReplacesAStarterThroughASharedSlot()
	{
		var pool = FightScene.LoadAbilityPool();
		foreach (var s in pool.Specials.Where(s => !s.Starter))
		{
			var run = RunState.NewRun(pool);
			var starterInSlot = pool.Starters.FirstOrDefault(st => st.Slot == s.Slot);
			if (starterInSlot is null) continue;
			bool refused = false;
			try { DevJump.ShapeCopy(run, pool, s.Id, 1); } catch (ArgumentException) { refused = true; }
			Assert.True(refused || run.Owns(starterInSlot.Id), $"{s.Id} shares slot {s.Slot} with starter {starterInSlot.Id}: starter must survive");
		}
	}
}
