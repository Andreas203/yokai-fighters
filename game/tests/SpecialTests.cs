using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-21: special slots A-D (A1), Ryo's starters from the TEST FIXTURE specials (tests/fixtures/specials/),
/// levels from data (A3, A4), EX (C5), Kata precision (K1), projectiles and the cancel-rule hook (X1-X4).
/// Fighters start 600 units apart (P1 at -300 facing right). A special requested on tick t plays frame n on
/// tick t+n-1, so Spirit Wave (spawn frame 14) spawns on tick t+13 and moves 12 units a tick after that.
/// </summary>
public static class SpecialTests
{
	const InputBits LP = InputBits.LightPunch, MP = InputBits.MediumPunch, LK = InputBits.LightKick,
		MK = InputBits.MediumKick, Rt = InputBits.Right, Lt = InputBits.Left;
	static readonly FighterInput Idle = FighterInput.None;

	static SpecialData[] Fixtures() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));
	static SpecialData Wave() => Fixtures().Single(s => s.Slot == SpecialSlot.A);
	static SpecialData Talisman() => Fixtures().Single(s => s.Slot == SpecialSlot.B);
	static string FixtureText(string id) => File.ReadAllText(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir) + $"/{id}.json");

	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();

	sealed class Log
	{
		public readonly List<(int tick, ProjectileEvent e)> Projectiles = new();
		public readonly List<(int tick, HitEvent e)> Hits = new();
		public readonly List<SpecialEvent> Specials = new();
		public readonly List<ImpactEvent> Impacts = new();
		public int Count(ProjectileEventKind k) => Projectiles.Count(p => p.e.Kind == k);
	}

	static Match Setup(Log? log = null, int p1x = -300, int p2x = 300, SimConfig? cfg = null)
	{
		var m = new Match(cfg, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Fixtures());
		m.P1.X = p1x * SimConfig.Scale;
		m.P2.X = p2x * SimConfig.Scale;
		if (log != null)
		{
			m.ProjectileChanged += (mm, e) => log.Projectiles.Add((mm.Tick, e));
			m.Hit += (mm, e) => log.Hits.Add((mm.Tick, e));
			m.SpecialStarted += (_, e) => log.Specials.Add(e);
			m.Impact += (_, e) => log.Impacts.Add(e);
		}
		return m;
	}

	/// <summary>Plays p1 (and p2) tick by tick, then idles until <paramref name="total"/> ticks have run.</summary>
	static void Play(Match m, IReadOnlyList<FighterInput> p1, IReadOnlyList<FighterInput>? p2 = null, int total = 0)
	{
		int n = Math.Max(total, Math.Max(p1.Count, p2?.Count ?? 0));
		for (int t = 0; t < n; t++)
			m.Step(t < p1.Count ? p1[t] : Idle, p2 != null && t < p2.Count ? p2[t] : Idle);
	}

	static List<FighterInput> Seq(string s, int facing = 1) => InputParserTests.Record(s, facing);
	static List<FighterInput> Req(SpecialSlot slot, bool ex = false) => new() { FighterInput.Special(slot, ex) };

	// --- Slots and starters (A1) ---------------------------------------------------------------

	[Test]
	public static void StartersFillSlotsAAndBAtLevel1()
	{
		var m = Setup();
		foreach (var f in m.Fighters)
		{
			Assert.Equal("test-spirit-wave", f.Specials[(int)SpecialSlot.A]?.Data.Id, "slot A = Spirit Wave");
			Assert.Equal("test-rising-talisman", f.Specials[(int)SpecialSlot.B]?.Data.Id, "slot B = Rising Talisman");
			Assert.Equal(1, f.Specials[(int)SpecialSlot.A]!.Level, "starters at Lv 1 (A3)");
			Assert.True(f.Specials[(int)SpecialSlot.C] is null && f.Specials[(int)SpecialSlot.D] is null, "C and D start empty");
		}
		var sw = Wave().Build(1, false);
		Assert.Equal((13, 0, 30, 60), (sw.Startup, sw.Active, sw.Recovery, sw.Damage), "Spirit Wave 13/—/30, 60 (rules.md)");
		Assert.True(sw.Projectile != null && sw.Projectile.SpawnFrame == 14, "projectile spawns after startup");
		var rt = Talisman().Build(1, false);
		Assert.Equal((5, 8, 28, 90), (rt.Startup, rt.Active, rt.Recovery, rt.Damage), "Rising Talisman 5/8/28, 90");
		for (int fr = 1; fr <= 5; fr++) Assert.True(rt.InvulnAt(fr, InvulnAgainst.Air), $"air-invulnerable frame {fr}");
		Assert.True(!rt.InvulnAt(6, InvulnAgainst.Air) && !rt.InvulnAt(1, InvulnAgainst.Strike), "only air, only 1-5");
	}

	[Test]
	public static void KataMotionsFireSlotsInBothFacings()
	{
		foreach (var (seq, slot) in new[] { ("2 3 6+LP", SpecialSlot.A), ("6 2 3+LP", SpecialSlot.B) })
		{
			var log = new Log();
			var m = Setup(log);
			Play(m, Seq(seq), Seq(seq, -1)); // P2 faces left: the same motion recorded mirrored
			foreach (var f in m.Fighters)
			{
				Assert.Equal(slot, f.ActiveSpecial, $"{seq}: slot");
				Assert.True(f.ActivePrecision, $"{seq}: Kata motion carries precision (K1)");
				Assert.Equal(1, f.MoveFrame, $"{seq}: starts on the press tick");
			}
			Assert.Equal(2, log.Specials.Count, "one SpecialStarted per fighter");
		}
	}

	[Test]
	public static void DirectRequestFiresSlotWithoutPrecision()
	{
		foreach (var slot in new[] { SpecialSlot.A, SpecialSlot.B })
		{
			var m = Setup();
			Play(m, Req(slot));
			Assert.Equal(slot, m.P1.ActiveSpecial, "FighterInput.Special starts the slot");
			Assert.True(!m.P1.ActivePrecision, "a direct request is not a motion: no precision bonus");
			Assert.Equal(m.P1.Specials[(int)slot]!.Move.Damage, m.P1.CurrentMove!.Damage, "100% damage");
			int total = m.P1.CurrentMove.TotalFrames;
			Play(m, Array.Empty<FighterInput>(), total: total - 1);
			Assert.Equal(total, m.P1.MoveFrame, "plays its whole length");
			Play(m, Array.Empty<FighterInput>(), total: 1);
			Assert.Equal(FighterState.Idle, m.P1.State, "then acts again");
		}
	}

	[Test]
	public static void EmptySlotGivesTheNormalOrNothing()
	{
		var m = Setup();
		m.P1.Unequip(SpecialSlot.A);
		Play(m, Seq("2 3 6+LP"));
		Assert.True(m.P1.CurrentMove is { IsNormal: true, Button: InputBits.LightPunch }, "236+LP with slot A empty = light punch");
		Assert.Equal(30, m.P1.CurrentMove!.Damage, "normals never get the precision bonus");
		var m2 = Setup();
		Play(m2, Req(SpecialSlot.C));
		Assert.Equal(FighterState.Idle, m2.P1.State, "request for an empty slot does nothing");
	}

	// --- Levels from data (A3, A4) -----------------------------------------------------------

	[Test]
	public static void LevelUpSwitchesDataWithNoCodeChange()
	{
		var m = Setup();
		Assert.Equal(43, m.P1.Specials[(int)SpecialSlot.A]!.Move.TotalFrames, "Lv 1 Spirit Wave 43 frames");
		Assert.Equal(2, m.P1.LevelUp(SpecialSlot.A), "level up");
		Assert.Equal(39, m.P1.Specials[(int)SpecialSlot.A]!.Move.TotalFrames, "Lv 2 tuning: recovery -4 (data)");
		Play(m, Req(SpecialSlot.A), total: 39);
		Assert.Equal(FighterState.Attack, m.P1.State, "still in the move on its last frame");
		Play(m, Array.Empty<FighterInput>(), total: 1);
		Assert.Equal(FighterState.Idle, m.P1.State, "Lv 2 acts again 4 ticks sooner than Lv 1");

		m.P1.LevelUp(SpecialSlot.B);
		Assert.Equal(99, m.P1.Specials[(int)SpecialSlot.B]!.Move.Damage, "Lv 2 Rising Talisman: +10% damage (data)");
		Assert.Equal(3, m.P1.LevelUp(SpecialSlot.A), "Lv 3");
		Assert.Equal(1, m.P1.Specials[(int)SpecialSlot.A]!.Move.Projectile!.AbsorbsProjectiles, "Lv 3 evolution: Great Wave absorbs one");
		Assert.Equal(39, m.P1.Specials[(int)SpecialSlot.A]!.Move.TotalFrames, "Lv 3 keeps the Lv 2 step");
		Assert.Equal(3, m.P1.LevelUp(SpecialSlot.A), "Lv 3 is the cap");
		m.Reset();
		Assert.Equal(3, m.P1.Specials[(int)SpecialSlot.A]!.Level, "levels are run state: a fight reset keeps them");

		// An edited data file changes behaviour: tuning recovery -10 instead of -4.
		var edited = SpecialLoader.Parse(FixtureText("test-spirit-wave").Replace("\"value\": -4", "\"value\": -10"));
		Assert.Equal(33, edited.Build(2, false).TotalFrames, "edited Lv 2 step");
	}

	[Test]
	public static void Level2DamageLandsInTheFight()
	{
		var m = Setup(p2x: -300 + 100);
		m.P1.LevelUp(SpecialSlot.B);
		Play(m, Req(SpecialSlot.B), total: 6);
		Assert.Equal(1000 - 99, m.P2.Health, "Lv 2 Rising Talisman hits for 99 on frame 6");
	}

	// --- Projectiles ---------------------------------------------------------------------------

	[Test]
	public static void ProjectileSpawnsTravelsAndHits()
	{
		var log = new Log();
		var m = Setup(log);
		Play(m, Req(SpecialSlot.A), total: 13);
		Assert.Equal(0, m.Projectiles.Count, "nothing before the spawn frame");
		Play(m, Array.Empty<FighterInput>(), total: 1);
		Assert.Equal(1, m.Projectiles.Count, "spawns on frame 14 (tick 14)");
		var p = m.Projectiles[0];
		Assert.Equal(m.P1.X, p.X, "spawns at the owner's feet");
		Assert.Equal(+1, p.Dir, "flies the way the owner faces");
		int x0 = p.X;
		for (int k = 1; k <= 20; k++)
		{
			Play(m, Array.Empty<FighterInput>(), total: 1);
			Assert.Equal(x0 + k * 12 * SimConfig.Scale, p.X, $"12 units a tick ({k})");
		}
		// Hitbox front edge (x 60..120) meets P2's standing hurtbox (-45..45 mirrored) at 37 ticks of flight: tick 51.
		Play(m, Array.Empty<FighterInput>(), total: 51 - m.Tick);
		Assert.Equal(1, log.Hits.Count, "hit");
		Assert.Equal(51, log.Hits[0].tick, "lands on tick 51");
		Assert.True(!log.Hits[0].e.Blocked, "unblocked");
		Assert.Equal(60, log.Hits[0].e.Damage, "direct request: 60");
		Assert.Equal(0, m.Projectiles.Count, "despawns on hit");
		Assert.Equal(FighterState.Hitstun, m.P2.State, "hitstun");
		Assert.Equal(20, m.P2.StunLeft, "fixture hitstun");
		Play(m, Array.Empty<FighterInput>(), total: 1);
		Assert.Equal(1000 - 60, m.P2.Health, "damage applied");
		Assert.Equal(6, m.P1.Meter, "C5 +6 for the owner");
	}

	[Test]
	public static void ProjectileIsBlockedStandingAndCrouching()
	{
		foreach (var guard in new[] { Rt, Rt | InputBits.Down })
		{
			var log = new Log();
			var m = Setup(log);
			var p2 = Enumerable.Repeat(new FighterInput(guard), 200).ToList(); // P2 faces left: back = right
			Play(m, Req(SpecialSlot.A), p2, total: 200);
			Assert.Equal(1, log.Count(ProjectileEventKind.Blocked), $"blocked ({guard})");
			Assert.Equal(1000, m.P2.Health, "no chip (E6)");
			Assert.Equal(0, m.Projectiles.Count, "despawns on block");
			Assert.Equal(3, m.P1.Meter, "C5 +3 when blocked");
		}
	}

	[Test]
	public static void KataPrecisionAppliesToProjectileDamage()
	{
		var log = new Log();
		var m = Setup(log);
		Play(m, Seq("2 3 6+LP"), total: 70);
		Assert.Equal(66, log.Hits.Single().e.Damage, "Kata Spirit Wave: 60 +10% (K1)");
		var log2 = new Log();
		var m2 = Setup(log2, p2x: -300 + 100);
		Play(m2, Seq("6 2 3+LP"), total: 10);
		Assert.Equal(99, log2.Hits.Single().e.Damage, "Kata Rising Talisman: 90 +10%");
	}

	[Test]
	public static void ProjectilesClash()
	{
		var log = new Log();
		var m = Setup(log);
		Play(m, Req(SpecialSlot.A), Req(SpecialSlot.A), total: 80);
		Assert.Equal(2, log.Count(ProjectileEventKind.Clash), "equal projectiles clash");
		Assert.Equal(0, m.Projectiles.Count, "both gone");
		Assert.Equal(0, log.Hits.Count, "nobody hit");
		Assert.True(m.P1.Health == 1000 && m.P2.Health == 1000, "no damage");

		// EX (2 hits) beats a plain one (1 hit) and still hits once.
		var log2 = new Log();
		var m2 = Setup(log2);
		m2.P1.Meter = 100;
		Play(m2, Req(SpecialSlot.A, ex: true), Req(SpecialSlot.A), total: 90);
		Assert.Equal(2, log2.Count(ProjectileEventKind.Clash), "clash");
		Assert.Equal(1, log2.Hits.Count, "EX wave survives with one hit and lands");
		Assert.Equal(0, log2.Hits[0].e.Attacker, "P1's wave hits P2");
	}

	[Test]
	public static void GreatWaveAbsorbsOneProjectile()
	{
		var log = new Log();
		var m = Setup(log);
		m.P1.LevelUp(SpecialSlot.A);
		m.P1.LevelUp(SpecialSlot.A);
		Play(m, Req(SpecialSlot.A), Req(SpecialSlot.A), total: 90);
		Assert.Equal(1, log.Count(ProjectileEventKind.Absorbed), "P2's wave absorbed");
		Assert.Equal(0, log.Count(ProjectileEventKind.Clash), "no trade");
		Assert.Equal(0, log.Hits.Single().e.Attacker, "Great Wave flies on and hits P2");
	}

	[Test]
	public static void OneProjectilePerOwner()
	{
		var log = new Log();
		var m = Setup(log, -800, 800);
		Play(m, Req(SpecialSlot.A), total: 44);
		Assert.Equal(FighterState.Idle, m.P1.State, "Spirit Wave over after 43 frames");
		Assert.Equal(1, m.Projectiles.Count, "wave still flying");
		Play(m, Req(SpecialSlot.A));
		Assert.Equal(FighterState.Idle, m.P1.State, "second wave refused while one is on screen");
		Play(m, Seq("2 3 6+LP"));
		Assert.True(m.P1.CurrentMove is { IsNormal: true }, "Kata 236+LP gives the normal instead");
		Play(m, Array.Empty<FighterInput>(), total: 150);
		Assert.Equal(0, m.Projectiles.Count, "first wave landed");
		Play(m, Req(SpecialSlot.A));
		Assert.Equal(SpecialSlot.A, m.P1.ActiveSpecial, "then a new one comes out");

		// Data overrides the limit: max_active 2.
		var two = SpecialLoader.Parse(FixtureText("test-spirit-wave").Replace("\"hits\": 1", "\"hits\": 1, \"max_active\": 2"));
		var m2 = Setup(null, -800, 800);
		m2.P1.Equip(SpecialSlot.A, two);
		Play(m2, Req(SpecialSlot.A), total: 44);
		Play(m2, Req(SpecialSlot.A), total: 20);
		Assert.Equal(2, m2.Projectiles.Count, "max_active 2 allows two at once");
	}

	[Test]
	public static void ProjectileDespawnsOffScreenOrAfterLifetime()
	{
		var log = new Log();
		var m = Setup(log, -800, 800);
		m.P2.State = FighterState.Knockdown; // can't be hit: the wave flies past
		m.P2.StunLeft = 1000;
		int edge = FightCamera.CenterX(m) + m.Config.ViewWidth / 2;
		Play(m, Req(SpecialSlot.A), total: 14);
		var p = m.Projectiles.Single();
		int lastX = 0;
		while (m.Projectiles.Count > 0 && m.Tick < 400) { lastX = p.X; Play(m, Array.Empty<FighterInput>(), total: 1); }
		Assert.Equal(1, log.Count(ProjectileEventKind.OffScreen), "off-screen despawn");
		Assert.True(p.WorldBox().x0 >= edge && lastX + (p.Data.Box.X * SimConfig.Scale) < edge, "gone on the first tick fully past the view edge");

		var timed = SpecialLoader.Parse(FixtureText("test-spirit-wave").Replace("\"hits\": 1", "\"hits\": 1, \"lifetime\": 10"));
		var log2 = new Log();
		var m2 = Setup(log2, -800, 800);
		m2.P1.Equip(SpecialSlot.A, timed);
		Play(m2, Req(SpecialSlot.A), total: 23);
		Assert.Equal(1, m2.Projectiles.Count, "lives ticks 14..23");
		Play(m2, Array.Empty<FighterInput>(), total: 1);
		Assert.Equal(1, log2.Count(ProjectileEventKind.Expired), "fades after lifetime 10");
	}

	// --- EX (C5) -------------------------------------------------------------------------------

	[Test]
	public static void ExSpendsABarAndHitsTwice()
	{
		var log = new Log();
		var m = Setup(log);
		m.P1.Meter = 150;
		var input = Seq("2 3 6");
		input.Add(new FighterInput(Numpad.ToBits(6, 1) | LP | MP));
		Play(m, input);
		Assert.True(m.P1.ActiveEx && m.P1.ActivePrecision, "236+LP+MP = EX, still a Kata motion");
		Assert.Equal(50, m.P1.Meter, "1 bar spent (data meter_cost 100)");
		Play(m, Array.Empty<FighterInput>(), total: 100);
		Assert.Equal(2, log.Hits.Count, "EX wave: 2 hits (data)");
		Assert.Equal(1000 - 132, m.P2.Health, "2 x 66");
		Assert.True(log.Impacts.All(i => i.Shake), "EX hits shake (V3)");
	}

	[Test]
	public static void ExWithoutMeterFallsBackCleanly()
	{
		var m = Setup();
		Play(m, Req(SpecialSlot.A, ex: true));
		Assert.Equal(SpecialSlot.A, m.P1.ActiveSpecial, "plain special comes out");
		Assert.True(!m.P1.ActiveEx, "not EX");
		Assert.Equal(0, m.P1.Meter, "nothing spent");
		var m2 = Setup();
		m2.P1.Meter = 99;
		Play(m2, Seq("2 3 6+MK+LK"));
		Assert.True(m2.P1.ActiveSpecial == SpecialSlot.A && !m2.P1.ActiveEx, "99 meter: plain");
		Assert.Equal(99, m2.P1.Meter, "meter kept");
	}

	[Test]
	public static void ExInputLeniencyAndThrowPriority()
	{
		for (int gap = 0; gap <= 3; gap++)
		{
			var m = Setup();
			m.P1.Meter = 100;
			var input = Seq(gap == 0 ? "2 3 6+LK+MK" : "2 3 6+LK");
			for (int k = 1; k < gap; k++) input.Add(new FighterInput(LK));
			if (gap > 0) input.Add(new FighterInput(LK | MK));
			Play(m, input);
			bool ex = gap <= 2;
			Assert.Equal(ex, m.P1.ActiveEx, $"second kick {gap} ticks late: EX {ex}");
			Assert.Equal(ex ? 0 : 100, m.P1.Meter, $"gap {gap}: meter");
			Assert.Equal(SpecialSlot.A, m.P1.ActiveSpecial, $"gap {gap}: still Spirit Wave");
		}
		var t = Setup(p2x: -300 + 120);
		t.P1.Meter = 100;
		Play(t, Seq("2 3 6+LP+LK"));
		Assert.True(t.P1.CurrentMove is { IsThrow: true }, "LP+LK is the throw (E12), never EX");
		Assert.Equal(100, t.P1.Meter, "no meter spent");
	}

	// --- Cancel hook (X1-X4) -------------------------------------------------------------------

	static CancelRule FoxStep() =>
		CancelRuleLoader.LoadFile(ProjectSettings.GlobalizePath("res://") + "../data/samples/fox-step.json");

	[Test]
	public static void CancelsNeedAHeldRuleAndItsWindow()
	{
		List<FighterInput> DashAt(int frame) // special on tick 1, then 66 finishing on move frame `frame`
		{
			var l = Req(SpecialSlot.A);
			while (l.Count < frame - 3) l.Add(Idle);
			l.AddRange(Seq("6 5 6"));
			return l;
		}
		var m = Setup(null, -800, 800);
		Play(m, DashAt(16));
		Assert.Equal(FighterState.Attack, m.P1.State, "X1: no rule held, no cancel");

		var m2 = Setup(null, -800, 800);
		Assert.True(m2.P1.HoldCancelRule(FoxStep()), "hold Fox Step");
		Play(m2, DashAt(16));
		Assert.Equal(FighterState.Dash, m2.P1.State, "Fox Step: Spirit Wave frame 16 cancels into a dash");
		Assert.Equal(1, m2.Projectiles.Count, "the wave already spawned and keeps flying");

		var m3 = Setup(null, -800, 800);
		m3.P1.HoldCancelRule(FoxStep());
		Play(m3, DashAt(25));
		Assert.Equal(FighterState.Attack, m3.P1.State, "outside the data cancel window (14-20)");

		Assert.True(m3.P1.HoldCancelRule(FoxStep()), "second rule");
		Assert.True(!m3.P1.HoldCancelRule(FoxStep()), "X2: at most two");

		// Normals have no cancel windows and Ryo holds no Oni Chain: LP never cancels into a special.
		var m4 = Setup();
		var seq = Seq("5+LP 2 3 6+MP");
		Play(m4, seq);
		Assert.True(m4.P1.CurrentMove is { IsNormal: true }, "no default normal-to-special cancel (X1)");
	}

	// --- Overlay, scene, determinism -------------------------------------------------------------

	[Test]
	public static void OverlayDrawsProjectileHitboxes()
	{
		var m = Setup();
		Play(m, Req(SpecialSlot.A), Req(SpecialSlot.A), total: 20);
		for (int i = 0; i < 2; i++)
		{
			var p = m.Projectiles.Single(q => q.Owner == i);
			var box = DebugBoxes.For(m, i).Single(b => b.Kind == DebugBoxKind.Projectile);
			var (x0, y0, x1, y1) = p.WorldBox();
			Assert.Equal((x0, y0, x1, y1), (box.X0, box.Y0, box.X1, box.Y1), $"P{i + 1} overlay box = sim box");
		}
		var p2 = m.Projectiles.Single(q => q.Owner == 1);
		Assert.Equal(p2.X - 120 * SimConfig.Scale, p2.WorldBox().x0, "P2's wave mirrored (x 60..120 toward P1)");
		Assert.True(DebugBoxes.Label(m.P1).Contains("special A"), "label names the slot");
	}

	[Test]
	public static void FightSceneEquipsStarters()
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources; // YOK-56: never depend on what is in data/
		try
		{
			var m = FightScene.NewMatch();
			Assert.True(m.P1.Specials[(int)SpecialSlot.A] != null && m.P1.Specials[(int)SpecialSlot.B] != null, "Ryo's starters in the fight scene");
			Assert.True(!m.P1.Moves.Any(mv => mv.Id.Contains("spirit-wave")), "specials are not loaded as slot moves");
		}
		finally { FightScene.Sources = saved; }
	}

	[Test]
	public static void LoaderRejectsWhatTheSimCannotRun()
	{
		string sw = FixtureText("test-spirit-wave");
		void Rejects(string json, string why)
		{
			try { SpecialLoader.Parse(json); }
			catch (FormatException) { return; }
			throw new AssertFailed($"loader accepted: {why}");
		}
		Rejects(sw.Replace("\"slot\": \"A\", \"motion\"", "\"slot\": \"E\", \"motion\""), "slot E");
		Rejects(sw.Replace("\"target\": \"recovery\"", "\"target\": \"properties.armour.hits\""), "add on a missing value");
		Rejects(sw.Replace("\"op\": \"add\", \"value\": -4", "\"op\": \"add\", \"value\": -4, \"when\": \"on_hit\""), "conditional level step");
		Rejects(sw.Replace("\"hitstun\": 20,", ""), "damaging projectile without hitstun");
		Rejects(sw.Replace("\"spawn_frame\": 14", "\"spawn_frame\": 50"), "spawn after the move ends");
	}

	[Test]
	public static void DeterministicWithRandomSpecials()
	{
		ulong RunOnce()
		{
			var m = Setup(null, -500, 500);
			m.P1.Meter = m.P2.Meter = 300;
			m.P1.HoldCancelRule(FoxStep());
			var rng = new SimRng(2121);
			string[] motions = { "2 3 6", "6 2 3", "2 1 4", "2 5 2" };
			InputBits[] buttons = { LP, MP, InputBits.HeavyPunch, LK, MK, InputBits.HeavyKick, LP | MP, LK | MK };
			var q = new[] { new Queue<FighterInput>(), new Queue<FighterInput>() };
			for (int t = 0; t < 3000 && m.Phase != MatchPhase.Over; t++)
			{
				var ins = new FighterInput[2];
				for (int i = 0; i < 2; i++)
				{
					int facing = m.Fighters[i].Facing;
					if (q[i].Count == 0)
					{
						int r = rng.Next(10);
						if (r < 4)
						{
							foreach (var s in Seq(motions[rng.Next(4)], facing)) q[i].Enqueue(s);
							q[i].Enqueue(new FighterInput(Numpad.ToBits(6, facing) | buttons[rng.Next(buttons.Length)]));
						}
						else if (r < 5) q[i].Enqueue(FighterInput.Special((SpecialSlot)(1 + rng.Next(2)), rng.Next(2) == 0));
						else q[i].Enqueue(new FighterInput((InputBits)(rng.NextUInt() & (uint)(InputBits.Directions | InputBits.Attacks))));
					}
					ins[i] = q[i].Dequeue();
				}
				m.Step(ins[0], ins[1]);
			}
			return m.StateHash();
		}
		Assert.Equal(RunOnce(), RunOnce(), "same inputs, same hash");
	}
}
