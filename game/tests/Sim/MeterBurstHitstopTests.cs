using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-20: meter (C5), EX spend hook, burst (C6), counterhit (C7, kept from YOK-16), hitstop (V2),
/// screen shake (V3) and the live HUD view. Moves are the TEST FIXTURE files, never game content.
/// </summary>
public static class MeterBurstHitstopTests
{
	static readonly FighterInput Idle = FighterInput.None;
	static FighterInput In(InputBits b) => new(b);
	const InputBits LP = InputBits.LightPunch, MP = InputBits.MediumPunch, HP = InputBits.HeavyPunch;
	const InputBits Punches = LP | MP | HP;

	static MoveData[] Fixtures() => MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath("res://tests/fixtures/moves"));
	static MoveData[] Normals() => MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir));
	static int Slot(MoveData[] moves, string id) => Array.FindIndex(moves, m => m.Id == id);
	static int SlotOf(MoveData[] moves, InputBits b) => Array.FindIndex(moves, m => m.Button == b);

	static Match Close(MoveData[] moves, List<HitEvent>? hits = null, List<ImpactEvent>? impacts = null, SimConfig? cfg = null)
	{
		var m = new Match(cfg, moves, moves);
		m.P1.X = -m.Config.BodyWidth / 2;
		m.P2.X = m.Config.BodyWidth / 2;
		if (hits != null) m.Hit += (_, e) => hits.Add(e);
		if (impacts != null) m.Impact += (_, e) => impacts.Add(e);
		return m;
	}

	/// <summary>P2 pinned in the right corner so holding back blocks instead of walking out of range.</summary>
	static Match Cornered(MoveData[] moves, List<HitEvent>? hits = null, List<ImpactEvent>? impacts = null)
	{
		var m = Close(moves, hits, impacts);
		m.P2.X = m.Config.StageHalfWidth - m.Config.BodyWidth / 2;
		m.P1.X = m.P2.X - m.Config.BodyWidth;
		return m;
	}

	/// <summary>P1 plays the slot once; steps until it connects. Returns the tick it connected on.</summary>
	static int LandOne(Match m, int slot, FighterInput p2, List<HitEvent> hits)
	{
		for (int t = 1; t <= 60; t++)
		{
			m.Step(t == 1 ? FighterInput.Attack(slot) : Idle, p2);
			if (hits.Count > 0) return t;
		}
		throw new AssertFailed("move never connected");
	}

	// --- Meter (C5) ------------------------------------------------------------------------------

	[Test]
	public static void Meter_HitGivesAttackerSixAndDefenderThree()
	{
		var moves = Fixtures();
		var hits = new List<HitEvent>();
		var m = Close(moves, hits);
		LandOne(m, Slot(moves, "test-jab"), Idle, hits);
		Assert.True(!hits[0].Blocked, "clean hit");
		Assert.Equal(6, m.P1.Meter, "+6 per hit landed");
		Assert.Equal(3, m.P2.Meter, "+3 per hit taken");
	}

	[Test]
	public static void Meter_BlockGivesAttackerThreeAndDefenderNothing()
	{
		var moves = Fixtures();
		var hits = new List<HitEvent>();
		var m = Cornered(moves, hits);
		LandOne(m, Slot(moves, "test-jab"), In(InputBits.Right), hits); // P2 faces left: Right = back
		Assert.True(hits[0].Blocked, "blocked");
		Assert.Equal(3, m.P1.Meter, "+3 per hit blocked (attacker; schema meter_gain.on_block)");
		Assert.Equal(0, m.P2.Meter, "blocking defender gains nothing (designer call)");
	}

	[Test]
	public static void Meter_CapsAtThreeBars()
	{
		var moves = Fixtures();
		var hits = new List<HitEvent>();
		var m = Close(moves, hits);
		Assert.Equal(300, m.Config.MeterMax, "3 bars x 100");
		m.P1.Meter = 297;
		m.P2.Meter = 299;
		LandOne(m, Slot(moves, "test-jab"), Idle, hits);
		Assert.Equal(300, m.P1.Meter, "attacker capped at 300");
		Assert.Equal(300, m.P2.Meter, "defender capped at 300");
		// Many hits: never above the cap.
		var m2 = Close(moves);
		int jab = Slot(moves, "test-jab");
		for (int t = 1; t <= 6000 && m2.Phase == MatchPhase.Fighting; t++)
		{
			m2.P2.Health = m2.P2.MaxHealth; // keep the round alive
			m2.Step(m2.P1.Actionable ? FighterInput.Attack(jab) : Idle, Idle);
			m2.P2.X = m2.P1.X + m2.Config.BodyWidth * m2.P1.Facing;
			Assert.True(m2.P1.Meter <= 300, "never above 300");
		}
		Assert.Equal(300, m2.P1.Meter, "reaches the cap");
	}

	[Test]
	public static void Meter_MoveDataOverridesGain()
	{
		const string json = """
		{ "kind": "special", "id": "test-meter", "levels": { "1": { "frame_data": {
		  "startup": 4, "active": 2, "recovery": 7, "damage": 30, "hitstun": 12, "blockstun": 9,
		  "meter_gain": { "on_hit": 10, "on_block": 1 }, "strength": "heavy",
		  "hitboxes": [ { "frames": [5, 6], "rect": { "x": 30, "y": 100, "w": 90, "h": 30 } } ],
		  "hurtboxes": [ { "frames": [1, 13], "rect": { "x": -45, "y": 0, "w": 90, "h": 180 } } ] } } } }
		""";
		var mv = MoveLoader.Parse(json);
		Assert.Equal(10, mv.MeterOnHit, "on_hit read");
		Assert.Equal(1, mv.MeterOnBlock, "on_block read");
		Assert.Equal(HitStrength.Heavy, mv.Strength, "strength read");
		var hits = new List<HitEvent>();
		var m = Close(new[] { mv }, hits);
		LandOne(m, 0, Idle, hits);
		Assert.Equal(10, m.P1.Meter, "data gain on hit");
		Assert.Equal(3, m.P2.Meter, "taken stays C5");
		bool threw = false;
		try { MoveLoader.Parse(json.Replace("\"heavy\"", "\"mega\"")); } catch (FormatException) { threw = true; }
		Assert.True(threw, "unknown strength rejected");
	}

	[Test]
	public static void Ex_SpendsOneBarOnlyWhenAvailable()
	{
		var m = new Match();
		Assert.Equal(100, m.Config.ExCost, "EX costs 1 bar");
		Assert.True(!m.TrySpendEx(0), "no meter, no EX");
		m.P1.Meter = 150;
		Assert.True(m.TrySpendEx(0), "EX with 1.5 bars");
		Assert.Equal(50, m.P1.Meter, "one bar spent");
		Assert.True(!m.TrySpendEx(0), "half a bar is not enough");
		Assert.Equal(50, m.P1.Meter, "failed spend changes nothing");
		m.P2.Meter = 300;
		Assert.True(m.TrySpendMeter(1, 200) && m.P2.Meter == 100, "generic spend");
		Assert.True(!m.TrySpendMeter(1, -5), "negative cost refused");
		m.Reset();
		Assert.Equal(0, m.P1.Meter, "reset clears meter");
	}

	// --- Hitstop (V2) ------------------------------------------------------------------------------

	[Test]
	public static void Hitstop_LightMediumHeavyFromButtons()
	{
		var n = Normals();
		foreach (var (b, want) in new[] { (LP, 6), (InputBits.LightKick, 6), (MP, 9), (InputBits.MediumKick, 9), (HP, 12), (InputBits.HeavyKick, 12) })
		{
			var hits = new List<HitEvent>();
			var imp = new List<ImpactEvent>();
			var m = Close(n, hits, imp);
			int t0 = LandOne(m, SlotOf(n, b), Idle, hits);
			Assert.Equal(want, imp[0].Hitstop, $"{b} hitstop");
			Assert.Equal(want, m.HitstopLeft, $"{b} freeze starts");
			// Both fighters freeze for exactly `want` ticks: state, positions and frames hold.
			int wf = m.WorldFrame;
			ulong a = Snapshot(m);
			for (int k = 0; k < want; k++)
			{
				m.Step(Idle, Idle);
				Assert.Equal(wf, m.WorldFrame, $"{b}: world frozen on hitstop tick {k + 1}");
				Assert.Equal(a, Snapshot(m), $"{b}: fighters unchanged during hitstop");
			}
			m.Step(Idle, Idle);
			Assert.Equal(wf + 1, m.WorldFrame, $"{b}: world resumes after {want}");
		}
	}

	static ulong Snapshot(Match m)
	{
		ulong h = Fnv.Offset;
		foreach (var f in m.Fighters)
		{
			h = Fnv.Mix(h, f.X); h = Fnv.Mix(h, f.Y); h = Fnv.Mix(h, (int)f.State);
			h = Fnv.Mix(h, f.MoveFrame); h = Fnv.Mix(h, f.StunLeft); h = Fnv.Mix(h, f.Health + f.PendingDamage);
		}
		return h;
	}

	[Test]
	public static void Hitstop_OnBlockToo_AndCounterhitAddsFour()
	{
		var n = Normals();
		var hits = new List<HitEvent>();
		var imp = new List<ImpactEvent>();
		var m = Cornered(n, hits, imp);
		LandOne(m, SlotOf(n, LP), In(InputBits.Right), hits);
		Assert.True(imp[0].Blocked && imp[0].Hitstop == 6, "blocked light: 6");

		var moves = Fixtures();
		hits.Clear(); imp.Clear();
		m = Close(moves, hits, imp);
		m.Step(FighterInput.Attack(Slot(moves, "test-jab")), FighterInput.Attack(Slot(moves, "test-sweep")));
		for (int t = 0; t < 10 && hits.Count == 0; t++) m.Step(Idle, Idle);
		Assert.True(hits[0].Counter, "counterhit");
		Assert.Equal(HitStrength.Medium, moves[Slot(moves, "test-jab")].Strength, "a special without strength is medium");
		Assert.Equal(9 + 4, imp[0].Hitstop, "medium + counterhit 4");
		// C7 still applies (YOK-16): +20% damage, +6 hitstun.
		Assert.Equal(36, hits[0].Damage, "30 * 1.2");
		Assert.Equal(12 + 6, m.P2.StunLeft, "hitstun 12 + 6");
	}

	[Test]
	public static void Hitstop_AdvantageStillMatchesDataForEveryNormal()
	{
		var n = Normals();
		foreach (var mv in n.Where(x => !x.Knockdown))
			foreach (bool block in new[] { false, true })
			{
				var hits = new List<HitEvent>();
				var m = block ? Cornered(n, hits) : Close(n, hits);
				int slot = Array.IndexOf(n, mv);
				int p1Free = -1, p2Free = -1;
				for (int t = 1; t <= 120 && (p1Free < 0 || p2Free < 0); t++)
				{
					m.Step(t == 1 ? FighterInput.Attack(slot) : Idle, block ? In(InputBits.Right) : Idle);
					if (hits.Count == 0) continue;
					if (p1Free < 0 && m.P1.Actionable) p1Free = t;
					if (p2Free < 0 && m.P2.State == FighterState.Idle) p2Free = t;
				}
				Assert.True(hits.Count == 1 && hits[0].Blocked == block, $"{mv.Id} connects ({(block ? "block" : "hit")})");
				int adv = p2Free - p1Free;
				Assert.Equal(block ? mv.AdvantageOnBlock : mv.AdvantageOnHit, adv, $"{mv.Id} advantage with hitstop on {(block ? "block" : "hit")}");
			}
	}

	[Test]
	public static void Hitstop_TradeUsesTheLonger_AndConfigCanDisable()
	{
		var n = Normals();
		var imp = new List<ImpactEvent>();
		var m = Close(n, null, imp);
		m.Step(FighterInput.Attack(SlotOf(n, LP)), FighterInput.Attack(SlotOf(n, LP)));
		for (int t = 0; t < 10 && imp.Count == 0; t++) m.Step(Idle, Idle);
		Assert.Equal(2, imp.Count, "trade");
		Assert.Equal(6 + 4, m.HitstopLeft, "trade = both counterhit lights");
		var off = Close(n, null, null, new SimConfig { HitstopEnabled = false });
		var hits = new List<HitEvent>();
		off.Hit += (_, e) => hits.Add(e);
		LandOne(off, SlotOf(n, HP), Idle, hits);
		Assert.Equal(0, off.HitstopLeft, "disabled");
	}

	// --- Screen shake (V3) ---------------------------------------------------------------------

	[Test]
	public static void Shake_HeavyHitsAndExOnly_SixTicks()
	{
		var n = Normals();
		foreach (var (b, shakes) in new[] { (LP, false), (MP, false), (HP, true), (InputBits.HeavyKick, true) })
		{
			var hits = new List<HitEvent>();
			var imp = new List<ImpactEvent>();
			var m = Close(n, hits, imp);
			LandOne(m, SlotOf(n, b), Idle, hits);
			Assert.Equal(shakes, imp[0].Shake, $"{b} shake");
		}
		var blocked = new List<ImpactEvent>();
		var mb = Cornered(n, null, blocked);
		mb.Step(FighterInput.Attack(SlotOf(n, HP)), In(InputBits.Right));
		for (int t = 0; t < 20 && blocked.Count == 0; t++) mb.Step(Idle, In(InputBits.Right));
		Assert.True(blocked[0].Blocked && !blocked[0].Shake, "blocked heavy: no shake");
		Assert.True(new ImpactEvent(0, "x", HitStrength.Light, 6, true, false, true).Shake, "EX flag carried");

		var s = new ScreenShake(6);
		s.OnImpact(new ImpactEvent(0, "x", HitStrength.Heavy, 12, false, false, true));
		int shown = 0;
		for (int t = 0; t < 20; t++)
		{
			var (x, y) = s.OffsetPx;
			if (s.Active)
			{
				shown++;
				Assert.True(Math.Abs(x) is >= 2 and <= 4 && Math.Abs(y) is >= 2 and <= 4, "2-4 px");
			}
			else Assert.True(x == 0 && y == 0, "still when idle");
			s.Advance();
		}
		Assert.Equal(6, shown, "6 frames");
		s.OnImpact(new ImpactEvent(0, "x", HitStrength.Light, 6, false, false, false));
		Assert.True(!s.Active, "lights never shake");
	}

	// --- Burst (C6) ------------------------------------------------------------------------------

	/// <summary>P1 jabs P2; returns the tick the jab connected.</summary>
	static (Match m, int jab) Hurt(List<HitEvent> hits, int p2Meter = 140)
	{
		var moves = Fixtures();
		var m = Close(moves, hits);
		m.P2.Meter = p2Meter;
		int jab = Slot(moves, "test-jab");
		LandOne(m, jab, Idle, hits);
		return (m, jab);
	}

	[Test]
	public static void Burst_FromHitstopBreaksCombo_TwentyInvulnerableFrames_DrainsMeter()
	{
		var hits = new List<HitEvent>();
		var (m, jab) = Hurt(hits);
		int fired = -1;
		m.BurstFired += (_, i) => fired = i;
		Assert.Equal(FighterState.Hitstun, m.P2.State, "in hitstun");
		int gapBefore = m.P2.X - m.P1.X;
		m.Step(Idle, In(Punches)); // during hitstop
		Assert.Equal(1, fired, "P2 burst");
		Assert.Equal(FighterState.Burst, m.P2.State, "burst state");
		Assert.Equal(0, m.HitstopLeft, "burst breaks the freeze");
		Assert.Equal(0, m.P2.Meter, "all meter spent (143 -> 0)");
		Assert.True(m.P2.BurstUsed, "spent");
		Assert.Equal(gapBefore + m.Config.BurstPushback * SimConfig.Scale, m.P2.X - m.P1.X, "attacker thrown back (proposed 200)");

		int invuln = 1; // the burst tick
		int hitsBefore = hits.Count;
		for (int t = 0; t < 40; t++)
		{
			// Bring P1 back into range and jab whenever free: nothing may land while invulnerable.
			if (m.P2.Invulnerable) m.P1.X = m.P2.X - m.Config.BodyWidth;
			bool wasInv = m.P2.Invulnerable;
			// Jab only when its active frames (5-6) still fall inside the invulnerability.
			m.Step(m.P1.Actionable && m.P2.Invulnerable && m.P2.StunLeft >= 6 ? FighterInput.Attack(jab) : Idle, Idle);
			if (m.P2.Invulnerable) invuln++;
			else if (wasInv) { Assert.True(m.P2.Actionable, "acts right after the burst"); break; }
		}
		Assert.Equal(20, invuln, "20 invulnerable frames");
		Assert.Equal(hitsBefore, hits.Count, "no hit lands during the burst");
	}

	[Test]
	public static void Burst_OncePerFight_OnlyFromHitstun_NeedsAllThreePunches()
	{
		// Not from idle, not from blockstun.
		var m = new Match();
		m.Step(Idle, In(Punches));
		Assert.True(!m.P2.BurstUsed, "no burst from idle");
		var moves = Fixtures();
		var hits = new List<HitEvent>();
		var mb = Cornered(moves, hits);
		LandOne(mb, Slot(moves, "test-jab"), In(InputBits.Right), hits);
		mb.Step(Idle, In(InputBits.Right | Punches));
		Assert.True(!mb.P2.BurstUsed && mb.P2.State != FighterState.Burst, "no burst from blockstun");

		// Two punches are not a burst.
		hits.Clear();
		var (m2, _) = Hurt(hits);
		m2.Step(Idle, In(LP | MP));
		Assert.True(!m2.P2.BurstUsed, "two punches: no burst");

		// Staggered presses within the 3-tick window count (after hitstop, still in hitstun).
		hits.Clear();
		var (m3, jab) = Hurt(hits);
		while (m3.HitstopLeft > 0) m3.Step(Idle, Idle);
		m3.Step(Idle, In(LP));
		m3.Step(Idle, In(LP | MP));
		Assert.True(!m3.P2.BurstUsed, "not yet");
		m3.Step(Idle, In(Punches));
		Assert.True(m3.P2.BurstUsed && m3.P2.State == FighterState.Burst, "LP, MP, HP over 3 ticks bursts");

		// Too slow: LP then HP 3 ticks later.
		hits.Clear();
		var (m4, _) = Hurt(hits);
		m4.Step(Idle, In(LP | MP));
		m4.Step(Idle, In(LP | MP));
		m4.Step(Idle, In(LP | MP));
		m4.Step(Idle, In(Punches));
		Assert.True(!m4.P2.BurstUsed, "presses 3 ticks apart: no burst");

		// Once per fight: a second combo can't be burst; a reset gives it back.
		for (int t = 0; t < 60 && !m3.P2.Actionable; t++) m3.Step(Idle, Idle);
		m3.P1.X = m3.P2.X - m3.Config.BodyWidth;
		int before = hits.Count;
		for (int t = 0; t < 30 && hits.Count == before; t++) m3.Step(t == 0 ? FighterInput.Attack(jab) : Idle, Idle);
		Assert.Equal(FighterState.Hitstun, m3.P2.State, "hit again");
		m3.Step(Idle, Idle);
		m3.Step(Idle, In(Punches));
		Assert.True(m3.P2.State != FighterState.Burst, "second burst refused");
		m3.Reset();
		Assert.True(!m3.P2.BurstUsed && m3.P2.Meter == 0, "reset restores the burst");
	}

	// --- Determinism + HUD ---------------------------------------------------------------------------

	static ulong RandomFight(uint seed, out int bursts, out int maxMeter)
	{
		var moves = Fixtures().Concat(Normals()).ToArray();
		var m = new Match(null, moves, moves);
		m.P1.X = -m.Config.BodyWidth;
		m.P2.X = m.Config.BodyWidth;
		var rng = new SimRng(seed);
		int b = 0, mm = 0;
		m.BurstFired += (_, _) => b++;
		ulong h = Fnv.Offset;
		for (int t = 0; t < 4000 && m.Phase != MatchPhase.Over; t++)
		{
			FighterInput Pick(Fighter f, Fighter o)
			{
				int r = rng.Next(10);
				if (r < 3) return FighterInput.Attack(rng.Next(moves.Length));
				if (r < 4) return In(Punches);
				if (r < 7 || Math.Abs(o.X - f.X) > 200 * SimConfig.Scale) // close in, so things connect
					return In(o.X > f.X ? InputBits.Right : InputBits.Left);
				return In((InputBits)(rng.Next(16)) | (rng.Next(3) == 0 ? LP : InputBits.None));
			}
			var i1 = Pick(m.P1, m.P2);
			m.Step(i1, Pick(m.P2, m.P1));
			mm = Math.Max(mm, Math.Max(m.P1.Meter, m.P2.Meter));
			h = Fnv.Mix(h, (int)(m.StateHash() & 0x7fffffff));
		}
		bursts = b;
		maxMeter = mm;
		return h;
	}

	[Test]
	public static void Determinism_MeterBurstHitstopReplayExactly()
	{
		for (uint seed = 1; seed <= 4; seed++)
		{
			ulong a = RandomFight(seed, out int ba, out int ma);
			ulong b = RandomFight(seed, out int bb, out int mb);
			Assert.Equal(a, b, $"seed {seed}: same hash every tick");
			Assert.Equal(ba, bb, "same bursts");
			Assert.True(ma <= 300, "meter never above cap");
		}
		RandomFight(2, out int bursts, out int maxMeter);
		Assert.True(maxMeter > 0, "random fight builds meter");
		Assert.True(bursts <= 2, "at most one burst per fighter");
	}

	[Test]
	public static void Hud_ShowsLiveMeterAndBurst()
	{
		var hits = new List<HitEvent>();
		var (m, _) = Hurt(hits, 0);
		IHudView v = new MatchHudView(m, 0);
		Assert.Equal(300, v.MeterMax, "max");
		Assert.Equal(6, v.Meter, "Ryo's live meter after a hit");
		Assert.True(v.BurstAvailable, "burst unspent");
		var p2 = new MatchHudView(m, 1);
		m.Step(Idle, In(Punches));
		Assert.True(!p2.BurstAvailable && p2.Meter == 0, "view follows the burst");
	}

	[Test]
	public static void Hud_FightSceneUsesLiveView(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		try
		{
			var hud = scene.GetNode<FightHud>("Hud");
			Assert.True(hud.View is MatchHudView, "HUD reads the match, not a stub");
			scene.Match.P1.Meter = 120;
			Assert.Equal(120, hud.View!.Meter, "live value");
		}
		finally { scene.QueueFree(); }
	}
}
