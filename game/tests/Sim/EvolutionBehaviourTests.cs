using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-226: the Lv 3 evolutions that were vocabulary only (YOK-220) now play: <c>launch</c> (Heaven Seal),
/// <c>wall_bounce</c> (Crushing Charge), <c>multi_hit</c> (Torrent Spin) (rules.md specials table, A5). The moves
/// here are TEST FIXTURES with made-up numbers. <see cref="EveryShippedEvolutionChangesWhatTheSimReads"/> runs over
/// data/moves, so a special added later is covered without a new id here.
/// </summary>
public static class EvolutionBehaviourTests
{
	const int S = SimConfig.Scale;
	static readonly SimConfig NoHitstop = SimConfig.Default with { HitstopEnabled = false };
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");

	/// <summary>A special in slot C: Lv 2 recovery -1, Lv 3 adds <paramref name="evolution"/> (a JSON effect list).</summary>
	static SpecialData Special(string id, string frameData, string evolution) => SpecialLoader.Parse($$"""
	{ "kind": "special", "id": "{{id}}", "name": "TEST FIXTURE (made-up numbers; not game content)",
	  "input": { "kata": { "slot": "C", "motion": "214" }, "kihon": { "slot": "C", "direction": "back" } },
	  "levels": {
	    "1": { "frame_data": {{frameData}} },
	    "2": { "tuning": { "target": "recovery", "op": "add", "value": -1 } },
	    "3": { "evolution": { "id": "{{id}}-evo", "name": "TEST FIXTURE", "preset": "{{id}}-evo", "changes": {{evolution}} } } } }
	""");

	// TEST FIXTURE: 3/3/6 tall strike reaching 150 units ahead.
	const string Strike = """
	{ "startup": 3, "active": 3, "recovery": 6, "damage": 50, "hitstun": 15, "blockstun": 10,
	  "hitboxes": [ { "frames": [4, 6], "rect": { "x": 0, "y": 0, "w": 150, "h": 600 } } ],
	  "hurtboxes": [ { "frames": [1, 12], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } } ] }
	""";

	// TEST FIXTURE: a 3/12/5 spin reaching 300 units ahead.
	const string Spin = """
	{ "startup": 3, "active": 12, "recovery": 5, "damage": 20, "hitstun": 10, "blockstun": 8,
	  "hitboxes": [ { "frames": [4, 15], "rect": { "x": 0, "y": 0, "w": 300, "h": 320 } } ],
	  "hurtboxes": [ { "frames": [1, 20], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } } ] }
	""";

	static SpecialData Launcher => Special("test-launcher", Strike, """[ { "target": "properties.launch", "op": "set", "value": true } ]""");
	static SpecialData Charger => Special("test-charger", Strike, """[ { "target": "properties.wall_bounce", "op": "set", "value": true } ]""");
	static SpecialData Spinner => Special("test-spinner", Spin,
		"""[ { "target": "properties.multi_hit", "op": "set", "value": { "hits": 3, "interval": 4 } } ]""");

	// TEST FIXTURE: a tall poke (2/4/4); "launch" variant for the no-relaunch check.
	static MoveData Poke(bool launch = false) => MoveLoader.Parse($$"""
	{ "kind": "normal", "id": "test-react-poke", "input": { "button": "LP" }, "frame_data": {
	  "startup": 2, "active": 4, "recovery": 4, "damage": 10, "hitstun": 12, "blockstun": 9,
	  "hitboxes": [ { "frames": [3, 6], "rect": { "x": 0, "y": 0, "w": 150, "h": 600 } } ],
	  "hurtboxes": [ { "frames": [1, 10], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } } ],
	  "properties": { "launch": {{(launch ? "true" : "false")}} } } }
	""");

	static Match Setup(SpecialData sp, int level, SimConfig? cfg = null, bool launchingPoke = false, int p1X = -50, int p2X = 50)
	{
		var m = new Match(cfg ?? NoHitstop, new[] { Poke(launchingPoke) }, new[] { Poke() });
		m.P1.Equip(SpecialSlot.C, sp, level);
		m.P1.X = p1X * S;
		m.P2.X = p2X * S;
		return m;
	}

	[Test]
	public static void ReactionKeysAreReadOnlyFromTheEvolution()
	{
		Assert.True(!Launcher.Build(2, false).Launch && Launcher.Build(3, false).Launch, "launch at Lv 3 only");
		Assert.True(!Charger.Build(2, false).WallBounce && Charger.Build(3, false).WallBounce, "wall_bounce at Lv 3 only");
		Assert.True(Spinner.Build(2, false).MultiHit is null, "no multi_hit at Lv 2");
		Assert.Equal(new MultiHit(3, 4), Spinner.Build(3, false).MultiHit, "multi_hit at Lv 3");
		foreach (var bad in new[] { "\"launch\": 1", "\"wall_bounce\": \"yes\"", "\"multi_hit\": { \"hits\": 1, \"interval\": 2 }" })
		{
			try { MoveLoader.Parse(Poke().Id.Length > 0 ? $$"""{ "kind": "normal", "id": "bad", "input": { "button": "LP" }, "frame_data": { "startup": 1, "active": 1, "recovery": 1, "damage": 0, "properties": { {{bad}} } } }""" : ""); }
			catch (FormatException) { continue; }
			throw new AssertFailed($"{bad}: expected FormatException");
		}
	}

	[Test]
	public static void Launch_SendsTheDefenderUp_JuggleableOnce_ThenKnockdownOnLanding()
	{
		var m = Setup(Launcher, 3);
		int hits = 0;
		m.Hit += (_, _) => hits++;
		m.Step(FighterInput.Special(SpecialSlot.C), FighterInput.None);
		for (int t = 2; t <= 4; t++) m.Step(FighterInput.None, FighterInput.None);
		Assert.Equal(1, hits, "the launcher hit on frame 4");
		Assert.True(m.P2.Airborne && m.P2.Launched && m.P2.State == FighterState.Hitstun, $"launched: air {m.P2.AirFrame}, {m.P2.State}");

		// Follow-up: P1 recovers and pokes the falling fighter.
		int t2 = 5;
		while (!m.P1.Actionable && t2++ < 60) m.Step(FighterInput.None, FighterInput.None);
		Assert.True(m.P2.Airborne, "still in the air when the launcher recovers: a follow-up window exists");
		m.Step(FighterInput.Attack(0), FighterInput.None);
		for (int i = 0; i < 3 && hits < 2; i++) m.Step(FighterInput.None, FighterInput.None);
		Assert.Equal(2, hits, "the follow-up connects on the launched fighter (juggle)");
		Assert.True(m.P2.Launched, "still launched after the follow-up");

		bool knockedDown = false;
		for (int i = 0; i < 60 && !knockedDown; i++)
		{
			m.Step(FighterInput.None, FighterInput.None);
			knockedDown = m.P2.State == FighterState.Knockdown && !m.P2.Airborne;
		}
		Assert.True(knockedDown && !m.P2.Launched, "a launch ends in a knockdown on landing");
	}

	[Test]
	public static void Launch_NotAtLv2_NotOnBlock_AndNeverRelaunched()
	{
		var m = Setup(Launcher, 2);
		for (int t = 1; t <= 4; t++) m.Step(t == 1 ? FighterInput.Special(SpecialSlot.C) : FighterInput.None, FighterInput.None);
		Assert.True(!m.P2.Airborne && m.P2.State == FighterState.Hitstun, "Lv 2: a plain hit (A5: the evolution carries launch)");

		var b = Setup(Launcher, 3);
		var back = new FighterInput(InputBits.Right); // P2 faces left: right is back (C3)
		for (int t = 1; t <= 4; t++) b.Step(t == 1 ? FighterInput.Special(SpecialSlot.C) : FighterInput.None, back);
		Assert.True(!b.P2.Airborne && b.P2.State == FighterState.Blockstun, "blocked: no launch");

		// A launching follow-up on a launched fighter doesn't reset its arc (no loops).
		var r = Setup(Launcher, 3, launchingPoke: true);
		r.Step(FighterInput.Special(SpecialSlot.C), FighterInput.None);
		while (!r.P1.Actionable) r.Step(FighterInput.None, FighterInput.None);
		int hits = 0;
		r.Hit += (_, _) => hits++;
		r.Step(FighterInput.Attack(0), FighterInput.None);
		for (int i = 0; i < 4 && hits == 0; i++)
		{
			int before = r.P2.AirFrame;
			r.Step(FighterInput.None, FighterInput.None);
			if (hits > 0) Assert.Equal(before + 1, r.P2.AirFrame, "a second launch keeps the arc going");
		}
		Assert.Equal(1, hits, "the launching poke connected");
	}

	[Test]
	public static void Launch_NeverOnAFighterThisHitKnocksOut()
	{
		// code-critic: KnockedOut is set in ApplyDamage, after ApplyHit; the guard reads the health left after this hit.
		var m = Setup(Launcher, 3);
		m.P2.Health = 30; // the launcher deals 50
		for (int t = 1; t <= 4; t++) m.Step(t == 1 ? FighterInput.Special(SpecialSlot.C) : FighterInput.None, FighterInput.None);
		Assert.True(m.P2.KnockedOut, "the hit KOs");
		Assert.True(!m.P2.Launched && !m.P2.Airborne, "a KO'd fighter is not launched");
		var alive = Setup(Launcher, 3);
		alive.P2.Health = 51;
		for (int t = 1; t <= 4; t++) alive.Step(t == 1 ? FighterInput.Special(SpecialSlot.C) : FighterInput.None, FighterInput.None);
		Assert.True(!alive.P2.KnockedOut && alive.P2.Launched, "1 health left: launched");
	}

	[Test]
	public static void Launch_ABurstLandsOnItsFeet()
	{
		var m = Setup(Launcher, 3);
		for (int t = 1; t <= 4; t++) m.Step(t == 1 ? FighterInput.Special(SpecialSlot.C) : FighterInput.None, FighterInput.None);
		Assert.True(m.P2.Launched, "launched");
		var burst = new FighterInput(InputBits.LightPunch | InputBits.MediumPunch | InputBits.HeavyPunch);
		m.Step(FighterInput.None, burst);
		Assert.True(m.P2.State == FighterState.Burst && !m.P2.Launched, "the burst clears the launch (E13)");
		for (int i = 0; i < 80; i++) m.Step(FighterInput.None, FighterInput.None);
		Assert.True(m.P2.State == FighterState.Idle, $"lands standing, not knocked down ({m.P2.State})");
	}

	[Test]
	public static void WallBounce_OnlyAtTheWall_OffItTowardTheAttacker()
	{
		var probe = new Match(NoHitstop);
		int wallX = probe.CornerHi(probe.P2) / S;

		var m = Setup(Charger, 3, p1X: wallX - 100, p2X: wallX);
		for (int t = 1; t <= 4; t++) m.Step(t == 1 ? FighterInput.Special(SpecialSlot.C) : FighterInput.None, FighterInput.None);
		Assert.True(m.P2.Launched && m.P2.Airborne && m.P2.JumpDir == -1, $"bounced off the wall toward P1 (dir {m.P2.JumpDir})");
		int x = m.P2.X;
		for (int i = 0; i < 5; i++) m.Step(FighterInput.None, FighterInput.None);
		Assert.True(m.P2.X < x, "drifts off the wall");
		bool down = false;
		for (int i = 0; i < 60 && !down; i++) { m.Step(FighterInput.None, FighterInput.None); down = m.P2.State == FighterState.Knockdown; }
		Assert.True(down, "lands in a knockdown");

		var mid = Setup(Charger, 3);
		for (int t = 1; t <= 4; t++) mid.Step(t == 1 ? FighterInput.Special(SpecialSlot.C) : FighterInput.None, FighterInput.None);
		Assert.True(!mid.P2.Airborne && mid.P2.State == FighterState.Hitstun, "mid-screen: a plain hit");

		var lv2 = Setup(Charger, 2, p1X: wallX - 100, p2X: wallX);
		for (int t = 1; t <= 4; t++) lv2.Step(t == 1 ? FighterInput.Special(SpecialSlot.C) : FighterInput.None, FighterInput.None);
		Assert.True(!lv2.P2.Airborne, "Lv 2 at the wall: no bounce (A5)");
	}

	static (int hits, int blocked) SpinHits(int level, SimConfig cfg, bool block)
	{
		var m = Setup(Spinner, level, cfg);
		int hits = 0, blocked = 0;
		m.Hit += (_, e) => { if (e.Attacker == 0) { hits++; if (e.Blocked) blocked++; } };
		var p2 = block ? new FighterInput(InputBits.Right) : FighterInput.None;
		m.Step(FighterInput.Special(SpecialSlot.C), p2);
		for (int t = 0; t < 60; t++) m.Step(FighterInput.None, p2);
		return (hits, blocked);
	}

	[Test]
	public static void MultiHit_ConnectsUpToItsHits_IntervalApart_HitOrBlock_WithOrWithoutHitstop()
	{
		Assert.Equal((1, 0), SpinHits(2, NoHitstop, false), "Lv 2: one connect per use");
		Assert.Equal((3, 0), SpinHits(3, NoHitstop, false), "Lv 3: 3 hits over 12 active frames, 4 apart");
		Assert.Equal((3, 3), SpinHits(3, NoHitstop, true), "blocked: 3 blocked connects");
		Assert.Equal((3, 0), SpinHits(3, SimConfig.Default, false), "hitstop freezes the move frame: still 3");

		// The hits land on move frames 4, 8, 12.
		var m = Setup(Spinner, 3);
		var frames = new List<int>();
		m.Hit += (_, e) => frames.Add(e.Frame);
		m.Step(FighterInput.Special(SpecialSlot.C), FighterInput.None);
		for (int t = 0; t < 30; t++) m.Step(FighterInput.None, FighterInput.None);
		Assert.Equal("4,8,12", string.Join(",", frames), "interval 4 from the first active frame");
	}

	/// <summary>
	/// Data-driven over data/moves: every special's Lv 3 evolution changes something the sim reads. A key that only
	/// loads (vocabulary with no behaviour) would leave the signature equal to Lv 2's and fail here.
	/// </summary>
	[Test]
	public static void EveryShippedEvolutionChangesWhatTheSimReads()
	{
		var specials = SpecialLoader.LoadDirectory(Path.Combine(RepoRoot, "data", "moves"));
		Assert.True(specials.Length > 0, "data/moves has specials");
		foreach (var sp in specials.Where(s => s.MaxLevel == 3))
		{
			string lv2 = Signature(sp.Build(2, false)), lv3 = Signature(sp.Build(3, false));
			Assert.True(lv2 != lv3, $"{sp.Id}: its evolution changes nothing the sim reads ({lv3})");
		}
	}

	/// <summary>
	/// Data-driven over data/moves: each shipped special whose Lv 3 launches, wall-bounces or multi-hits does so in a
	/// fight, from its own frame data (Heaven Seal, Crushing Charge, Torrent Spin today), against an idle P2 in reach.
	/// </summary>
	[Test]
	public static void ShippedReactionEvolutions_PlayInAFight()
	{
		var specials = SpecialLoader.LoadDirectory(Path.Combine(RepoRoot, "data", "moves"));
		int checkedCount = 0;
		foreach (var sp in specials.Where(s => s.MaxLevel == 3))
		{
			var lv3 = sp.Build(3, false);
			if (!lv3.Launch && !lv3.WallBounce && lv3.MultiHit is null) continue;
			var probe = new Match(NoHitstop);
			int wall = probe.CornerHi(probe.P2) / S;
			// In reach of the first hitbox: P2 just past its near edge, at the wall for a wall bounce.
			var first = lv3.Hitboxes[0].Box;
			int gap = Math.Max(first.X + first.W / 2, 60);
			int p2 = lv3.WallBounce ? wall : 200;
			var m = new Match(NoHitstop, new[] { Poke() }, new[] { Poke() });
			m.P1.Equip(sp.Slot, sp, 3);
			m.P1.X = (p2 - gap) * S;
			m.P2.X = p2 * S;
			int hits = 0;
			bool launched = false;
			m.Hit += (_, e) => { if (e.Attacker == 0 && !e.Blocked) hits++; };
			m.Step(FighterInput.Special(sp.Slot), FighterInput.None);
			for (int t = 0; t < lv3.TotalFrames + 5; t++) { m.Step(FighterInput.None, FighterInput.None); launched |= m.P2.Launched; }
			if (lv3.MultiHit is { } mh) Assert.Equal(mh.Hits, hits, $"{sp.Id} Lv 3: {mh.Hits} hits");
			if (lv3.Launch || lv3.WallBounce) Assert.True(hits > 0 && launched, $"{sp.Id} Lv 3: hit ({hits}) and launched/bounced");
			checkedCount++;
		}
		Assert.True(checkedCount >= 1, "some shipped special has a reaction evolution");
	}

	/// <summary>Every frame-data number and property the fight code reads.</summary>
	public static string Signature(MoveData m) => string.Join("|",
		m.Startup, m.Active, m.Recovery, m.Damage, m.Hitstun, m.Blockstun, m.HitPushback, m.BlockPushback, m.Knockdown, m.Low,
		string.Join(";", m.Hitboxes), string.Join(";", m.Hurtboxes), string.Join(";", m.Throwboxes),
		m.Projectile, string.Join(";", m.Invuln), m.Armour, m.CounterhitImmune, m.ProjectileInterruptImmune,
		m.ThrowBreakStartup, m.PullOnHit, m.HealPctMaxHealth, m.ProjectileAngleUp, m.IsCommandGrab, m.GrabRange, m.Breakable,
		m.ForwardTravel, m.AftershockDelay, m.Teleport, m.Decoy, m.Launch, m.WallBounce, m.MultiHit);
}
