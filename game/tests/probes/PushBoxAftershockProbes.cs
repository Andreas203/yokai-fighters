using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Replay;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-136 probes: per-fighter push box and the aftershock key. Expected values come from the rule in each comment.</summary>
public static class PushBoxAftershockProbes
{
	const int S = SimConfig.Scale;
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static readonly SimConfig NoHitstop = SimConfig.Default with { HitstopEnabled = false };
	static FighterInput Right => new(InputBits.Right);
	static FighterInput Left => new(InputBits.Left);

	static string QuakeJson(string lv1Props = "\"low\": true", string lv3Changes = "{ \"target\": \"properties.aftershock_delay\", \"op\": \"set\", \"value\": 30 }") => """
	{ "kind": "special", "id": "test-quake", "name": "TEST FIXTURE quake", "source": "test-quaker", "starter": false, "rarity": "rare", "locked": true, "clip": "test-quake",
	  "rules": ["A5"],
	  "input": { "kata": { "slot": "D", "motion": "22" }, "kihon": { "slot": "D", "direction": "down" } },
	  "card": { "plain": "TEST FIXTURE", "frames": "9 / 3 / 20" },
	  "levels": {
	    "1": { "frame_data": { "startup": 9, "active": 3, "recovery": 20, "damage": 50, "hitstun": 20, "blockstun": 15,
	      "hitboxes": [ { "frames": [10, 12], "rect": { "x": 0, "y": 0, "w": 300, "h": 110 } } ],
	      "hurtboxes": [ { "frames": [1, 32], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } } ],
	      "properties": { LV1PROPS } } },
	    "2": { "status": "proposed", "tuning": { "target": "recovery", "op": "add", "value": -2 } },
	    "3": { "evolution": { "id": "test-aftershock", "name": "TEST FIXTURE", "preset": "test-aftershock", "changes": [ LV3 ] } }
	  } }
	""".Replace("LV1PROPS", lv1Props).Replace("LV3", lv3Changes);

	static MoveData Quake(int delay = 30) => SpecialLoader.Parse(QuakeJson(lv3Changes: "{ \"target\": \"properties.aftershock_delay\", \"op\": \"set\", \"value\": " + delay + " }")).Build(3, false);

	static Match QuakeMatch(int gap, SimConfig? cfg = null, int delay = 30)
	{
		var q = Quake(delay);
		var m = new Match(cfg ?? NoHitstop, new[] { q }, new[] { q });
		m.P1.X = -gap * S / 2; m.P2.X = gap * S / 2;
		return m;
	}

	// ---- Push box -------------------------------------------------------------------------------

	// Rule C2 / push boxes never overlap, corners per fighter: 150 vs 150 pressed into either wall.
	[Test]
	public static void Push_150v150_BothWalls()
	{
		foreach (int side in new[] { 1, -1 })
		{
			var m = new Match();
			m.P1.PushWidth = m.P2.PushWidth = 150 * S;
			var inp = side > 0 ? Right : Left;
			for (int t = 0; t < 700; t++) m.Step(inp, inp);
			Fighter wall = side > 0 ? m.P2 : m.P1, other = side > 0 ? m.P1 : m.P2;
			Assert.Equal(side * (m.Config.StageHalfWidth - 75 * S), wall.X, $"side {side}: wall fighter keeps the 150 corner");
			Assert.Equal(150 * S, Math.Abs(wall.X - other.X), $"side {side}: centres exactly 150 apart");
		}
	}

	// Same, 150 vs 120, with the narrow fighter holding the wall and the wide one pressing.
	[Test]
	public static void Push_WidePressesNarrowAtWall_BothWalls()
	{
		foreach (int side in new[] { 1, -1 })
		{
			var m = new Match();
			Fighter wide = side > 0 ? m.P1 : m.P2, narrow = side > 0 ? m.P2 : m.P1;
			wide.PushWidth = 150 * S;
			var inp = side > 0 ? Right : Left;
			for (int t = 0; t < 700; t++) m.Step(side > 0 ? inp : FighterInput.None, side > 0 ? FighterInput.None : inp);
			// narrow stands still mid-stage: walk wide into it, then keep both walking into the wall
			for (int t = 0; t < 900; t++) m.Step(inp, inp);
			Assert.Equal(side * (m.Config.StageHalfWidth - 60 * S), narrow.X, $"side {side}: narrow (120) keeps its own corner");
			Assert.Equal(135 * S, Math.Abs(wide.X - narrow.X), $"side {side}: mean gap 135");
			Assert.True(Math.Abs(wide.X) <= m.Config.StageHalfWidth - 75 * S, $"side {side}: wide fighter inside its 150 corner");
		}
	}

	// C2: a jumper passes over; landing beside a cornered wide fighter must end legal (inside both corners, no overlap).
	[Test]
	public static void Push_CrossUpOverWideBoxInCorner_EndsLegal()
	{
		var m = new Match();
		m.P2.PushWidth = 150 * S;
		m.P2.X = m.Config.StageHalfWidth - 75 * S;
		m.P1.X = m.P2.X - 135 * S;
		for (int t = 0; t < 5; t++) m.Step(FighterInput.None, FighterInput.None);
		m.Step(new FighterInput(InputBits.DebugCrossUp), FighterInput.None);
		for (int t = 0; t < 120; t++)
		{
			m.Step(FighterInput.None, FighterInput.None);
			Assert.True(m.P2.X <= m.Config.StageHalfWidth - 75 * S && m.P1.X <= m.Config.StageHalfWidth - 60 * S, $"inside corners at {t}");
			Assert.True(m.P1.X >= -m.Config.StageHalfWidth + 60 * S, $"left corner at {t}");
			if (m.P1.Y == 0 && m.P2.Y == 0) Assert.True(Math.Abs(m.P2.X - m.P1.X) >= 135 * S, $"grounded fighters never overlap at {t}");
		}
	}

	// Burst thrown to a wall: the attacker (150) must not end past its own corner.
	[Test]
	public static void Burst_UnequalWidths_AttackerStaysInsideItsCorner()
	{
		var m = new Match();
		m.P1.PushWidth = 150 * S;
		m.P1.X = -m.Config.StageHalfWidth + 80 * S; m.P2.X = m.P1.X + 150 * S;
		m.P2.State = FighterState.Hitstun; m.P2.StunLeft = 30;
		var punches = new FighterInput(InputBits.LightPunch | InputBits.MediumPunch | InputBits.HeavyPunch);
		bool fired = false; m.BurstFired += (_, _) => fired = true;
		for (int t = 0; t < 4 && !fired; t++) m.Step(FighterInput.None, punches);
		Assert.True(fired, "burst fired (setup)");
		Assert.True(m.P1.X >= -m.Config.StageHalfWidth + 75 * S, $"150 fighter inside its corner, got {m.P1.X}");
	}

	// Throw into a corner with unequal widths: nobody ends outside own corner or overlapping.
	[Test]
	public static void Throw_UnequalWidthsAtCorner_EndsLegal()
	{
		var thrown = MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir));
		var normals = MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir));
		var moves = new List<MoveData>(normals); moves.AddRange(thrown);
		var m = new Match(null, moves.ToArray(), moves.ToArray());
		m.P2.PushWidth = 150 * S;
		m.P2.X = m.Config.StageHalfWidth - 75 * S; m.P1.X = m.P2.X - 135 * S;
		for (int t = 1; t <= 120; t++)
		{
			m.Step(new FighterInput(t == 1 ? InputBits.LightPunch | InputBits.LightKick : InputBits.None), FighterInput.None);
			Assert.True(m.P2.X <= m.Config.StageHalfWidth - 75 * S, $"P2 inside its 150 corner at {t}");
			Assert.True(m.P1.X <= m.P2.X - 135 * S || m.P1.Y != 0 || m.P2.Y != 0 , $"no overlap at {t}");
		}
	}

	// Replay of a fight where only P1 is wide (mirror of the author's P2 case) and of an Oni kit fight.
	[Test]
	public static void Replay_P1WideOnly_RoundTrips()
	{
		var m = FightScene.NewMatch();
		m.P1.PushWidth = 150 * S;
		var rec = ReplayRecorder.Begin(m, null, 0, null);
		for (int t = 0; t < 900; t++)
		{
			var a = new FighterInput(t % 200 < 100 ? InputBits.Right : InputBits.Left);
			var b = new FighterInput(t % 150 < 75 ? InputBits.Left : InputBits.Right);
			m.Step(a, b); rec.Record(a, b, m.StateHash());
		}
		var done = rec.Finish(m);
		Assert.Equal(150 * S, done.Header.Fighters[0].PushWidth, "recorded");
		var back = ReplayRecording.FromBytes(done.ToBytes());
		Assert.Equal(150 * S, back.Header.Fighters[0].PushWidth, "survives the file");
		var r = Replayer.Run(back, ReplayCatalog.FromRepo(RepoRoot));
		Assert.True(r.Ok, r.ToString());
	}

	// An old recording (no PushWidth key) replays as the default box.
	[Test]
	public static void Replay_OldHeaderWithoutPushWidth_Replays()
	{
		var m = FightScene.NewMatch();
		var rec = ReplayRecorder.Begin(m, null, 0, null);
		for (int t = 0; t < 300; t++) { m.Step(Right, Left); rec.Record(Right, Left, m.StateHash()); }
		var done = rec.Finish(m);
		var back = ReplayRecording.FromBytes(done.ToBytes());
		Assert.Equal(0, back.Header.Fighters[0].PushWidth, "absent = 0");
		Assert.True(Replayer.Run(back, ReplayCatalog.FromRepo(RepoRoot)).Ok, "replays");
		back.Header.Fighters[1].PushWidth = -5;
		Assert.True(!Replayer.Run(back, ReplayCatalog.FromRepo(RepoRoot)).Ok, "negative width in a header is refused, not applied");
	}

	// Prints a golden hash for the Ryo vs Kitsune fixture fight; compare with main's (same file run there).
	[Test]
	public static void Hash_RyoVsKitsune_GoldenPrint()
	{
		var src = FightScene.FixtureSources;
		var ryo = Kit.Load(Kit.Ryo, src); var kit = Kit.Load("kitsune", src);
		var m = new Match(null, ryo.Moves, kit.Moves);
		ryo.Equip(m.P1); kit.Equip(m.P2);
		var rng = new Random(7);
		ulong h = 0;
		for (int t = 0; t < 1200; t++)
		{
			var a = new FighterInput((InputBits)(rng.Next(64) & 0x1ff));
			var b = new FighterInput((InputBits)(rng.Next(64) & 0x1ff));
			m.Step(a, b); h = h * 31 + m.StateHash();
		}
		GD.Print($"GOLDEN_RYO_KITSUNE {h}");
	}

	// ---- Aftershock -----------------------------------------------------------------------------

	// A5 / world frames: with hitstop ON the aftershock's Quake is exactly `delay` world frames after the spawn.
	[Test]
	public static void Aftershock_HitstopOn_CountsWorldFramesNotTicks()
	{
		var m = QuakeMatch(120, SimConfig.Default);
		int spawn = -1, quake = -1, spawnTick = -1, quakeTick = -1;
		m.AftershockChanged += (mm, e) =>
		{
			if (e.Kind == AftershockEventKind.Spawn) { spawn = mm.WorldFrame; spawnTick = mm.Tick; }
			if (e.Kind == AftershockEventKind.Quake) { quake = mm.WorldFrame; quakeTick = mm.Tick; }
		};
		for (int t = 1; t <= 120; t++) m.Step(t == 1 ? FighterInput.Attack(0) : FighterInput.None, FighterInput.None);
		Assert.True(spawn > 0 && quake > 0, "both events happened");
		Assert.Equal(30, quake - spawn, "30 world frames");
		Assert.True(quakeTick - spawnTick > 30, "and the first hit's hitstop made it more ticks");
	}

	// delay 1: lands the world frame after spawning, once, and a move can't spawn twice on one first-active frame.
	[Test]
	public static void Aftershock_DelayOne_SpawnsOnce_QuakesNextFrame()
	{
		var m = QuakeMatch(900, null, 1); // out of range, hitstop on
		var ev = new List<(int tick, AftershockEventKind k)>();
		m.AftershockChanged += (mm, e) => ev.Add((mm.Tick, e.Kind));
		for (int t = 1; t <= 60; t++) m.Step(t == 1 ? FighterInput.Attack(0) : FighterInput.None, FighterInput.None);
		Assert.Equal(1, ev.FindAll(e => e.k == AftershockEventKind.Spawn).Count, "one spawn");
		Assert.Equal(10, ev.Find(e => e.k == AftershockEventKind.Spawn).tick, "first active tick");
		Assert.Equal(11, ev.Find(e => e.k == AftershockEventKind.Quake).tick, "next world frame");
		Assert.Equal(1, ev.FindAll(e => e.k == AftershockEventKind.Expired).Count, "expired once");
		Assert.Equal(0, m.Aftershocks.Count, "none left");
	}

	// Huge delay loads; it never lands within a round and Reset clears it.
	[Test]
	public static void Aftershock_HugeDelay_LoadsAndResetClears()
	{
		var m = QuakeMatch(120, null, int.MaxValue);
		for (int t = 1; t <= 300; t++) m.Step(t == 1 ? FighterInput.Attack(0) : FighterInput.None, FighterInput.None);
		Assert.Equal(1, m.Aftershocks.Count, "still pending");
		Assert.Equal(m.P2.MaxHealth - 50, m.P2.Health, "only the first quake hit");
		m.Reset();
		Assert.Equal(0, m.Aftershocks.Count, "cleared");
	}

	// Owner KO'd between spawn and land: no damage after the round is decided.
	[Test]
	public static void Aftershock_OwnerKoBeforeLanding_NoFurtherDamage()
	{
		var m = QuakeMatch(900);
		int ticks = 0;
		for (int t = 1; t <= 80; t++)
		{
			m.Step(t == 1 ? FighterInput.Attack(0) : FighterInput.None, FighterInput.None);
			if (t == 20) m.P1.PendingDamage += m.P1.MaxHealth; // owner dies; defender is out of the first box and walks nowhere
			if (t == 15) { m.P2.X = m.P1.X + 100 * S; }
			ticks = t;
		}
		Assert.True(m.P1.KnockedOut, "owner KO'd");
		Assert.Equal(m.P2.MaxHealth, m.P2.Health, "defender took nothing after the owner was already KO'd (round decided)");
	}

	// Round reset mid-pending: next round plays out identically to a fresh match (serial and hash reset).
	[Test]
	public static void Aftershock_ResetMidPending_ReplaysLikeFresh()
	{
		var a = QuakeMatch(120); var fresh = QuakeMatch(120);
		for (int t = 1; t <= 20; t++) a.Step(t == 1 ? FighterInput.Attack(0) : FighterInput.None, FighterInput.None);
		a.Reset(); a.P1.X = -60 * S; a.P2.X = 60 * S;
		for (int t = 1; t <= 70; t++)
		{
			var i = t == 1 ? FighterInput.Attack(0) : FighterInput.None;
			a.Step(i, FighterInput.None); fresh.Step(i, FighterInput.None);
			Assert.Equal(fresh.StateHash(), a.StateHash(), $"hash equals a fresh fight at {t}");
		}
	}

	// Two pending at once (delay longer than the move): both land, in order, deterministic hash on a second run.
	[Test]
	public static void Aftershock_TwoPending_BothLand_Deterministic()
	{
		ulong Run(out List<int> hitTicks, out int maxPending)
		{
			var m = QuakeMatch(900, null, 60);
			var ticks = new List<int>(); int mp = 0, tick = 0;
			m.Hit += (_, _) => ticks.Add(tick);
			for (tick = 1; tick <= 140; tick++)
			{
				if (tick == 20) m.P2.X = m.P1.X + 100 * S; // in range for both aftershocks
				m.Step(tick == 1 || tick == 34 ? FighterInput.Attack(0) : FighterInput.None, FighterInput.None);
				mp = Math.Max(mp, m.Aftershocks.Count);
			}
			hitTicks = ticks; maxPending = mp; return m.StateHash();
		}
		var h1 = Run(out var t1, out int mp1); var h2 = Run(out _, out _);
		Assert.Equal(2, mp1, "two pending at once");
		Assert.Equal(h1, h2, "same inputs, same hash");
		Assert.True(t1.Count >= 2, $"aftershocks connected ({t1.Count})");
	}

	// Defender busy with its own throw when the aftershock lands: no crash, deterministic, hit is a counter (C7, projectile rules).
	[Test]
	public static void Aftershock_DefenderMidThrow_Deterministic()
	{
		var thr = MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir));
		ulong Run(List<HitEvent> hits)
		{
			var q = Quake(30);
			var p2 = new List<MoveData>(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))); p2.AddRange(thr);
			var m = new Match(NoHitstop, new[] { q }, p2.ToArray());
			m.P1.X = -450 * S; m.P2.X = 450 * S;
			m.Hit += (_, e) => hits.Add(e);
			for (int t = 1; t <= 60; t++)
			{
				if (t == 14) { m.P2.X = m.P1.X + 140 * S; }
				m.Step(t == 1 ? FighterInput.Attack(0) : FighterInput.None, t == 14 ? new FighterInput(InputBits.LightPunch | InputBits.LightKick) : FighterInput.None);
			}
			return m.StateHash();
		}
		var h = new List<HitEvent>();
		Assert.Equal(Run(h), Run(new List<HitEvent>()), "deterministic");
	}

	// Loader: the key on a Lv 1 / Lv 2 move, and bad evolution values.
	[Test]
	public static void Aftershock_LoaderOnLv1AndBadEvolution()
	{
		var lv1 = SpecialLoader.Parse(QuakeJson("\"low\": true, \"aftershock_delay\": 20"));
		GD.Print($"  OBSERVE aftershock key on Lv1 data loads: Lv1 delay={lv1.Build(1, false).AftershockDelay}");
		foreach (var bad in new[] {
			"{ \"target\": \"properties.aftershock_delay\", \"op\": \"set\", \"value\": 0 }",
			"{ \"target\": \"properties.aftershock_delay\", \"op\": \"set\", \"value\": \"30\" }",
			"{ \"target\": \"properties.aftershock_delay\", \"op\": \"add\", \"value\": -5 }" })
		{
			try { SpecialLoader.Parse(QuakeJson(lv3Changes: bad)).Build(3, false); }
			catch (FormatException) { continue; }
			throw new AssertFailed("accepted bad evolution: " + bad);
		}
	}
}
