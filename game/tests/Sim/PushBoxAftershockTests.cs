using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-136: per-fighter push box (data/fighters/, kind fighter) and the Aftershock evolution key
/// (properties.aftershock_delay, A5). The quake below is a TEST FIXTURE, not game content: its numbers are made up.
/// </summary>
public static class PushBoxAftershockTests
{
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	const int S = SimConfig.Scale;

	// TEST FIXTURE: a low ground pound whose Lv 3 evolution adds an aftershock 30 frames later.
	const string QuakeJson = """
	{
	  "kind": "special", "id": "test-quake",
	  "name": "TEST FIXTURE: quake (made-up numbers; not game content)",
	  "source": "test-quaker", "starter": false, "rarity": "rare", "locked": true, "clip": "test-quake",
	  "rules": ["A5"],
	  "input": { "kata": { "slot": "D", "motion": "22" }, "kihon": { "slot": "D", "direction": "down" } },
	  "card": { "plain": "TEST FIXTURE", "frames": "9 / 3 / 20" },
	  "levels": {
	    "1": { "frame_data": {
	      "startup": 9, "active": 3, "recovery": 20, "damage": 50, "hitstun": 20, "blockstun": 15,
	      "hitboxes": [ { "frames": [10, 12], "rect": { "x": 0, "y": 0, "w": 300, "h": 110 } } ],
	      "hurtboxes": [ { "frames": [1, 32], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } } ],
	      "properties": { "low": true } } },
	    "2": { "status": "proposed", "tuning": { "target": "recovery", "op": "add", "value": -2 } },
	    "3": { "evolution": { "id": "test-aftershock", "name": "TEST FIXTURE", "preset": "test-aftershock",
	      "changes": [ { "target": "properties.aftershock_delay", "op": "set", "value": 30 } ] } }
	  }
	}
	""";

	static SpecialData Quake => SpecialLoader.Parse(QuakeJson);
	static readonly SimConfig NoHitstop = SimConfig.Default with { HitstopEnabled = false };

	/// <summary>P1 plays <paramref name="move"/> on tick 1; P2 stands <paramref name="gap"/> units away.</summary>
	static Match Setup(MoveData move, int gap, List<(int tick, HitEvent e)> hits, Func<int> tick)
	{
		var m = new Match(NoHitstop, new[] { move }, new[] { move });
		m.P1.X = -gap * S / 2;
		m.P2.X = gap * S / 2;
		m.Hit += (_, e) => hits.Add((tick(), e));
		return m;
	}

	// --- Push box -------------------------------------------------------------------------------

	[Test]
	public static void FighterData_OniFileIsProvisional150_OthersDefault()
	{
		var src = KitSources.Repo(RepoRoot);
		Assert.Equal(150, FighterData.Load(src.FightersDir, "oni").PushWidth, "data/fighters/oni.json: provisional 150");
		Assert.Equal(null, FighterData.Load(src.FightersDir, "ryo").PushWidth, "Ryo has no file: default");
		Assert.Equal(null, FighterData.Load(src.FightersDir, "kitsune").PushWidth, "Kitsune has no file: default");
		var f = new Fighter();
		Kit.Load("oni", src).Equip(f);
		Assert.Equal(150 * S, f.PushWidth, "the Oni's kit sets its push width");
		var r = new Fighter();
		Kit.Load(Kit.Ryo, src).Equip(r);
		Assert.Equal(0, r.PushWidth, "Ryo's kit leaves the config's BodyWidth");
		Assert.Equal(0, Kit.Load("oni", src.FixturesOnly()).Body.PushWidth ?? 0, "fixtures-only sources read no fighter data");
	}

	[Test]
	public static void FighterData_LoaderRejectsBadFiles()
	{
		void Bad(string json, string what)
		{
			try { FighterData.Parse(json, "oni"); }
			catch (FormatException) { return; }
			throw new AssertFailed($"accepted {what}");
		}
		Bad("""{ "kind": "fighter", "id": "oni", "push_width": 0 }""", "push_width 0");
		Bad("""{ "kind": "fighter", "id": "oni", "push_width": 1061 }""", "push_width over the screen walls");
		Bad("""{ "kind": "fighter", "id": "oni", "push_width": 150.5 }""", "a fractional push_width");
		Bad("""{ "kind": "fighter", "id": "kappa", "push_width": 150 }""", "an id that isn't the file name");
		Bad("""{ "kind": "normal", "id": "oni" }""", "another kind");
		Assert.Equal(null, FighterData.Parse("""{ "kind": "fighter", "id": "oni" }""", "oni").PushWidth, "no push_width = default");
	}

	[Test]
	public static void PushBox_DefaultPairKeepsBodyWidth_WidePairKeepsTheMean()
	{
		var m = new Match();
		Assert.Equal(m.Config.BodyWidth, m.PushGap(m.P1, m.P2), "both default: the gap is BodyWidth, as before");
		m.P2.PushWidth = 150 * S;
		Assert.Equal(135 * S, m.PushGap(m.P1, m.P2), "120 and 150: centres 135 apart");

		// P1 walks into a standing P2 with the wide box: they stop 135 apart, never closer.
		for (int t = 0; t < 120; t++)
		{
			m.Step(new FighterInput(InputBits.Right), FighterInput.None);
			Assert.True(Math.Abs(m.P2.X - m.P1.X) >= 135 * S, $"no overlap of the wide push box at tick {t}");
		}
		Assert.Equal(135 * S, m.P2.X - m.P1.X, "pressed together at the mean width");
	}

	[Test]
	public static void PushBox_WideFighterStopsEarlierAtTheCorner()
	{
		var m = new Match();
		m.P2.PushWidth = 150 * S;
		// Both walk right (the screen walls drag nobody): the wide one reaches the corner, the other presses it there.
		var right = new FighterInput(InputBits.Right);
		for (int t = 0; t < 600; t++) m.Step(right, right);
		Assert.Equal(m.Config.StageHalfWidth - 75 * S, m.P2.X, "the cornered wide fighter keeps the corner");
		Assert.Equal(m.P2.X - 135 * S, m.P1.X, "the presser stops at the mean gap");
		m.Reset();
		Assert.Equal(150 * S, m.P2.PushWidth, "Reset keeps the fighter's push width (identity, not round state)");
	}

	// --- Aftershock -----------------------------------------------------------------------------

	[Test]
	public static void Aftershock_IsReadFromData_AndOnlyByTheEvolution()
	{
		var q = Quake;
		Assert.Equal(0, q.Build(1, false).AftershockDelay, "Lv 1: no aftershock");
		Assert.Equal(0, q.Build(2, false).AftershockDelay, "Lv 2: no aftershock");
		Assert.Equal(30, q.Build(3, false).AftershockDelay, "Lv 3 evolution sets properties.aftershock_delay (A5)");

		void Bad(string props, string what)
		{
			string json = QuakeJson.Replace("\"properties\": { \"low\": true }", "\"properties\": { \"low\": true, " + props + " }");
			try { SpecialLoader.Parse(json); }
			catch (FormatException) { return; }
			throw new AssertFailed($"accepted {what}");
		}
		Bad("\"aftershock_delay\": 0", "a delay of 0");
		Bad("\"aftershock_delay\": -3", "a negative delay");
		Bad("\"aftershock_delay\": 2.5", "a fractional delay");
		try
		{
			MoveLoader.Parse("""{ "kind": "normal", "id": "x", "input": { "button": "LP" }, "frame_data": { "startup": 3, "active": 2, "recovery": 5, "damage": 0, "properties": { "aftershock_delay": 10 } } }""");
			throw new AssertFailed("accepted an aftershock on a move with no hitboxes");
		}
		catch (FormatException) { }
	}

	[Test]
	public static void Aftershock_SecondQuakeHitsDelayFramesLater_FromTheSimTick()
	{
		int tick = 0;
		var hits = new List<(int tick, HitEvent e)>();
		var events = new List<(int tick, AftershockEvent e)>();
		var m = Setup(Quake.Build(3, false), 120, hits, () => tick);
		m.AftershockChanged += (_, e) => events.Add((tick, e));
		for (tick = 1; tick <= 60; tick++)
			m.Step(tick == 1 ? FighterInput.Attack(0) : FighterInput.None, FighterInput.None);

		Assert.Equal(2, hits.Count, "the quake and its aftershock both hit");
		Assert.Equal(10, hits[0].tick, "first quake on its first active frame");
		Assert.Equal(40, hits[1].tick, "aftershock 30 frames later (delay from data)");
		Assert.Equal(50, hits[1].e.Damage, "the aftershock carries the move's damage");
		Assert.Equal(m.P2.MaxHealth - 100, m.P2.Health, "both hits applied");
		Assert.Equal(10, events[0].tick, "spawned in the sim tick of the first active frame");
		Assert.Equal(AftershockEventKind.Spawn, events[0].e.Kind, "spawn event");
		Assert.True(events.Exists(e => e.tick == 40 && e.e.Kind == AftershockEventKind.Quake), "quake event on the second quake's first frame");
		Assert.True(events.Exists(e => e.tick == 40 && e.e.Kind == AftershockEventKind.Hit), "hit event");
		Assert.Equal(0, m.Aftershocks.Count, "one connect, then gone");
	}

	[Test]
	public static void Aftershock_StaysWhereTheFirstQuakeStruck_AndExpires()
	{
		int tick = 0;
		var hits = new List<(int tick, HitEvent e)>();
		var m = Setup(Quake.Build(3, false), 900, hits, () => tick); // out of range: both whiff
		int anchor = 0;
		for (tick = 1; tick <= 50; tick++)
		{
			m.Step(tick == 1 ? FighterInput.Attack(0) : new FighterInput(tick > 33 ? InputBits.Left : InputBits.None), FighterInput.None);
			if (tick == 10) anchor = m.P1.X;
			if (tick == 41)
			{
				Assert.Equal(1, m.Aftershocks.Count, "still pending in its window");
				Assert.Equal(anchor, m.Aftershocks[0].X, "anchored where the first quake struck, not on the walking owner");
				Assert.True(m.P1.X < anchor, "the owner did walk away");
				Assert.True(m.AftershockBoxes(0).GetEnumerator().MoveNext(), "its live box is drawn by the debug overlay");
			}
		}
		Assert.Equal(0, hits.Count, "nothing in range");
		Assert.Equal(0, m.Aftershocks.Count, "expired after its replayed active window");
	}

	[Test]
	public static void Aftershock_IsLow_CrouchGuardBlocksBothQuakes()
	{
		int tick = 0;
		var hits = new List<(int tick, HitEvent e)>();
		var m = Setup(Quake.Build(3, false), 120, hits, () => tick);
		var guard = new FighterInput(InputBits.Right | InputBits.Down); // P2 faces left: back + down
		for (tick = 1; tick <= 60; tick++) m.Step(tick == 1 ? FighterInput.Attack(0) : FighterInput.None, guard);
		Assert.Equal(2, hits.Count, "two connects");
		Assert.True(hits[0].e.Blocked && hits[1].e.Blocked, "both blocked crouching (the aftershock keeps the move's low, C3)");

		hits.Clear();
		var m2 = Setup(Quake.Build(3, false), 120, hits, () => tick);
		var stand = new FighterInput(InputBits.Right);
		for (tick = 1; tick <= 60; tick++) m2.Step(tick == 1 ? FighterInput.Attack(0) : FighterInput.None, tick >= 39 ? stand : FighterInput.None); // guard only as it lands (holding back walks away)
		Assert.True(hits.Count == 2 && !hits[1].e.Blocked, "a standing guard doesn't block the low aftershock");
	}

	[Test]
	public static void Aftershock_IsInTheStateHash_AndAbsentOneLeavesItUnchanged()
	{
		var lv1 = new Match(NoHitstop, new[] { Quake.Build(1, false) }, new[] { Quake.Build(1, false) });
		var lv3 = new Match(NoHitstop, new[] { Quake.Build(3, false) }, new[] { Quake.Build(3, false) });
		var plain = new Match(NoHitstop, new[] { Quake.Build(1, false) }, new[] { Quake.Build(1, false) });
		foreach (var m in new[] { lv1, lv3, plain }) { m.P1.X = -450 * S; m.P2.X = 450 * S; }
		for (int t = 1; t <= 60; t++)
		{
			var a = t == 1 ? FighterInput.Attack(0) : FighterInput.None;
			lv1.Step(a, FighterInput.None);
			lv3.Step(a, FighterInput.None);
			plain.Step(a, FighterInput.None);
			Assert.Equal(lv1.StateHash(), plain.StateHash(), $"same fight, same hash at tick {t}");
			if (t < 10) Assert.Equal(lv1.StateHash(), lv3.StateHash(), $"no aftershock yet: hashes equal at tick {t}");
			else Assert.True(lv1.StateHash() != lv3.StateHash(), $"the pending/spent aftershock is in the hash at tick {t}");
		}
	}

	[Test]
	public static void Aftershock_ResetClearsIt()
	{
		var m = new Match(NoHitstop, new[] { Quake.Build(3, false) }, new[] { Quake.Build(3, false) });
		m.P1.X = -450 * S; m.P2.X = 450 * S;
		var fresh = new Match(NoHitstop, new[] { Quake.Build(3, false) }, new[] { Quake.Build(3, false) });
		for (int t = 1; t <= 15; t++) m.Step(t == 1 ? FighterInput.Attack(0) : FighterInput.None, FighterInput.None);
		Assert.Equal(1, m.Aftershocks.Count, "pending");
		m.Reset();
		Assert.Equal(0, m.Aftershocks.Count, "Reset clears pending aftershocks");
		Assert.Equal(fresh.StateHash(), m.StateHash(), "and the hash is a fresh match's again");
	}
}
