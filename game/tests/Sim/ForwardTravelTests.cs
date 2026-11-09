using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Replay;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-221: <c>properties.forward_travel</c> moves the fighter that many units over the move's active frames, in whole
/// centi-units with no drift (F3), stops at the opponent's push box and the walls (E1), keeps armour, adds to the
/// YOK-171 charge dash (YOK-210 e) and moves a Tanuki copy the same way (T0). The special is a TEST FIXTURE written
/// here with Iron-Club Charge's proposed 12/6/14 (YOK-219) and a made-up 300 units of travel: 50 units on each of
/// frames 13-18. A direct request on tick 1 plays frame n on tick n.
/// </summary>
public static class ForwardTravelTests
{
	const int S = SimConfig.Scale;
	const int Startup = 12, Active = 6, Recovery = 14, Total = Startup + Active + Recovery;
	static readonly FighterInput Idle = FighterInput.None;
	static readonly FighterInput Go = FighterInput.Special(SpecialSlot.B);

	static string Special(int travel = 300, bool hitbox = false, string armour = "", int active = Active, string id = "test-forward-charge") => $$"""
		{
		  "kind": "special", "id": "{{id}}", "name": "TEST FIXTURE: forward charge (not game content)", "status": "proposed",
		  "source": "oni", "starter": false, "rarity": "common", "locked": false, "clip": "oni-heavy-punch", "rules": ["A10"],
		  "input": { "kata": { "slot": "B", "motion": "623" }, "kihon": { "slot": "B", "direction": "forward" } },
		  "card": { "plain": "Test.", "frames": "Test." },
		  "levels": {
		    "1": { "frame_data": {
		      "startup": {{Startup}}, "active": {{active}}, "recovery": {{Recovery}}, "damage": 110, "hitstun": 20, "blockstun": 14,
		      "hitboxes": [{{(hitbox ? $$"""{ "frames": [{{Startup + active}}, {{Startup + active}}], "rect": { "x": 20, "y": 100, "w": 80, "h": 200 } }""" : "")}}],
		      "hurtboxes": [{ "frames": [1, {{Startup + active + Recovery}}], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } }],
		      "properties": { "forward_travel": {{travel}}{{armour}} } } },
		    "2": { "status": "proposed", "tuning": { "target": "damage", "op": "mul_pct", "value": 110 } },
		    "3": { "evolution": { "id": "test-crush", "name": "Test Crush", "preset": "heaven-seal",
		      "changes": [{ "target": "damage", "op": "add", "value": 10 }], "card": { "plain": "Test.", "frames": "Test." } } }
		  }
		}
		""";

	const string Armour = """, "armour": { "frames": [1, 18], "hits": 1 }""";

	/// <summary>The YOK-142 Sumo Stance numbers (window 0-30, step 10, 14 / 18 / 22 / 26 units per tick), as ChargeHoldTests.</summary>
	const string SumoCharge = """
		{
		  "kind": "modifier", "id": "test-charge", "name": "Test Charge", "source": "kappa", "rarity": "common", "locked": false,
		  "rules": ["A10"], "applies_to": { "requires": [], "excludes": ["projectile"] },
		  "charge": {
		    "button": "special_trigger", "window_ticks": { "min": 0, "max": 30 }, "step_ticks": 10, "grace_ticks": 0,
		    "release": "dash_then_move", "hold_pose_clip": "ryo-crouch",
		    "dash": { "duration": "startup", "units_per_tick_by_step": [14, 18, 22, 26], "cap_units_per_tick": 26 }
		  },
		  "effects": [{ "target": "properties.forward_travel", "op": "set", "value": "charge_dash", "when": "charge_released" }],
		  "card": { "plain": "Test.", "frames": "Test." }
		}
		""";

	static SpecialData[] Fixtures() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));
	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();

	/// <summary>Both fighters carry the starters, then <paramref name="special"/> in slot B (P2 too when <paramref name="both"/>).</summary>
	/// <param name="p1x">Start X in units; int.MinValue keeps the match's own start (a replay doesn't record positions).</param>
	static Match Setup(string special, int p1x, int p2x, bool both = false, string? modifier = null)
	{
		var m = new Match(null, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Fixtures());
		var data = SpecialLoader.Parse(special);
		m.P1.Equip(SpecialSlot.B, data);
		if (both) m.P2.Equip(SpecialSlot.B, data);
		if (modifier != null) m.P1.AttachModifier(SpecialSlot.B, ModifierLoader.Parse(modifier), m.Config);
		if (p1x != int.MinValue) { m.P1.X = p1x * S; m.P2.X = p2x * S; }
		return m;
	}

	/// <summary>Each fighter's X after every tick of <paramref name="ticks"/>.</summary>
	static List<(int x1, int x2)> Run(Match m, int ticks, Func<int, FighterInput> p1, Func<int, FighterInput>? p2 = null)
	{
		var xs = new List<(int, int)>();
		for (int t = 1; t <= ticks; t++)
		{
			m.Step(p1(t), p2?.Invoke(t) ?? Idle);
			xs.Add((m.P1.X, m.P2.X));
		}
		return xs;
	}

	static FighterInput OnTick1(int t) => t == 1 ? Go : Idle;

	[Test]
	public static void TravelAt_AddsUpExactly_OnTheActiveFramesOnly()
	{
		foreach (var (travel, active) in new[] { (300, 6), (301, 6), (1, 6), (5, 7), (0, 6), (1_000_000, 1), (int.MaxValue, 7) })
		{
			var m = MoveLoader.Parse(Special(travel, active: active));
			Assert.Equal(travel, m.ForwardTravel, "read from properties.forward_travel");
			long sum = 0, lo = long.MaxValue, hi = long.MinValue;
			for (int n = 1; n <= m.TotalFrames; n++)
			{
				long step = m.TravelAt(n);
				bool activeFrame = n > Startup && n <= Startup + active;
				if (!activeFrame) Assert.Equal(0L, step, $"{travel}/{active}: no travel on frame {n} (startup or recovery)");
				else { lo = Math.Min(lo, step); hi = Math.Max(hi, step); }
				sum += step;
			}
			Assert.Equal((long)travel * S, sum, $"{travel} over {active}: the steps add up to the whole distance, no drift");
			Assert.True(hi - lo <= 1, $"{travel} over {active}: spread evenly (steps {lo}..{hi})");
		}
		Assert.Equal(0L, MoveLoader.Parse(Special(300)).TravelAt(0), "frame 0 is not a frame");
	}

	[Test]
	public static void Loader_RejectsTravelTheFightCantRun()
	{
		static bool Rejects(string json) { try { MoveLoader.Parse(json); return false; } catch (FormatException) { return true; } }
		Assert.True(Rejects(Special().Replace("\"forward_travel\": 300", "\"forward_travel\": -1")), "negative");
		Assert.True(Rejects(Special().Replace("\"forward_travel\": 300", "\"forward_travel\": 2.5")), "fractional");
		Assert.True(Rejects(Special().Replace("\"forward_travel\": 300", "\"forward_travel\": \"charge_dash\"")), "the modifier switch is not a distance");
		Assert.True(Rejects(Special(active: 0)), "no active frames to travel over");
		Assert.Equal(300, MoveLoader.Parse(Special().Replace("\"forward_travel\": 300", "\"forward_travel\": 3e2")).ForwardTravel, "3e2 is the whole number 300");
		Assert.Equal(0, MoveLoader.Parse(Special(active: 0, travel: 0)).ForwardTravel, "0 travel with no active frames is fine");
	}

	[Test]
	public static void Unobstructed_TravelsTheWholeDistance_OverTheActiveFrames()
	{
		var m = Setup(Special(), -500, 500);
		int x0 = m.P1.X;
		var xs = Run(m, Total, OnTick1);
		for (int t = 1; t <= Total; t++)
		{
			int expect = t <= Startup ? 0 : Math.Min(t - Startup, Active) * 50 * S;
			Assert.Equal(expect, xs[t - 1].x1 - x0, $"tick {t} (frame {t}): 50 units per active frame, still otherwise");
		}
		Assert.Equal(500 * S, m.P2.X, "the opponent never moved");
		Assert.Equal(FighterState.Attack, m.P1.State, "the move's last frame plays on tick Total");
		m.Step(Idle, Idle);
		Assert.Equal(FighterState.Idle, m.P1.State, "the fighter acts again on tick Total + 1");
		Assert.Equal(300 * S, m.P1.X - x0, "and travels no further");

		// Facing the other way: P2 travels left by the same amount.
		var mirror = Setup(Special(), -500, 500, both: true);
		Run(mirror, Total, _ => Idle, OnTick1);
		Assert.Equal(200 * S, mirror.P2.X, "P2 (facing left) travels 300 units left");
	}

	[Test]
	public static void StopsAtTheOpponentsPushBox_WithoutPushingIt()
	{
		var m = Setup(Special(), -150, 50);
		int gap = m.PushGap(m.P1, m.P2);
		var xs = Run(m, Total, OnTick1);
		foreach (var (x1, x2) in xs) Assert.True(x2 - x1 >= gap, $"no overlap ({x1} vs {x2}, gap {gap})");
		Assert.Equal(50 * S, m.P2.X, "travel never pushes the opponent");
		Assert.Equal(m.P2.X - gap, m.P1.X, "the traveller stops touching the opponent's push box");

		// Already touching: no travel at all.
		var touching = Setup(Special(), -10, 110);
		Run(touching, Total, OnTick1);
		Assert.Equal(-10 * S, touching.P1.X, "no room: no travel");
		Assert.Equal(110 * S, touching.P2.X, "and no push");
	}

	[Test]
	public static void StopsAtTheStageWall_AndTheCorneredOpponent()
	{
		// Facing the stage edge (P2 behind): stops at the corner.
		var wall = Setup(Special(), 0, 0);
		wall.P1.X = wall.CornerHi(wall.P1) - 100 * S;
		wall.P2.X = wall.P1.X - 600 * S;
		wall.P1.Facing = 1;
		foreach (var (x1, _) in Run(wall, Total, OnTick1))
			Assert.True(x1 <= wall.CornerHi(wall.P1), $"never past the corner ({x1})");
		Assert.Equal(wall.CornerHi(wall.P1), wall.P1.X, "stopped at the corner");

		// Into a cornered opponent: stops on its push box, which stays in the corner.
		foreach (int side in new[] { 1, -1 })
		{
			var m = Setup(Special(), 0, 0, both: true);
			Fighter mover = side > 0 ? m.P1 : m.P2, other = side > 0 ? m.P2 : m.P1;
			other.X = side > 0 ? m.CornerHi(other) : m.CornerLo(other);
			mover.X = other.X - side * 200 * S;
			mover.Facing = side;
			other.Facing = -side;
			int gap = m.PushGap(m.P1, m.P2);
			if (side > 0) Run(m, Total, OnTick1); else Run(m, Total, _ => Idle, OnTick1);
			Assert.Equal(side > 0 ? m.CornerHi(other) : m.CornerLo(other), other.X, $"side {side}: the cornered fighter stays in its corner");
			Assert.Equal(other.X - side * gap, mover.X, $"side {side}: the traveller stops on its push box");
		}

		// Facing away from the opponent: the screen walls (MaxSeparation) give the distance back.
		var screen = Setup(Special(), 0, 900);
		screen.P1.Facing = -1;
		foreach (var (x1, x2) in Run(screen, Total, OnTick1))
			Assert.True(x2 - x1 <= screen.Config.MaxSeparation, $"never past the screen wall ({x2 - x1})");
		Assert.Equal(900 * S - screen.Config.MaxSeparation, screen.P1.X, "stopped at the screen wall");
	}

	[Test]
	public static void BothChargingIn_MeetHalfway_WithoutOverlap()
	{
		var m = Setup(Special(), -300, 300, both: true);
		int gap = m.PushGap(m.P1, m.P2);
		foreach (var (x1, x2) in Run(m, Total, OnTick1, OnTick1))
			Assert.True(x2 - x1 >= gap, $"no overlap, no crossing ({x1} vs {x2})");
		Assert.Equal(gap, m.P2.X - m.P1.X, "they meet push box to push box");
		Assert.Equal(-m.P1.X, m.P2.X, "and share the room evenly");
	}

	[Test]
	public static void Armour_StillTakesAHit_WhileTravelling()
	{
		// P2's fixture Spirit Wave (slot A) flies at P1; find a start tick for P1 whose armour takes it mid-travel.
		bool found = false;
		for (int start = 1; start <= 150 && !found; start++)
		{
			var m = Setup(Special(armour: Armour), -500, 500);
			int hp0 = m.P1.Health, frameHit = 0, xBefore = 0, stepAfter = -1;
			for (int t = 1; t <= start + Total + 30; t++)
			{
				int frameBefore = m.P1.MoveFrame, x = m.P1.X;
				m.Step(t == start ? Go : Idle, t == 1 ? FighterInput.Special(SpecialSlot.A) : Idle);
				if (frameHit == 0 && m.P1.ArmourHitsTaken == 1)
				{
					frameHit = m.P1.MoveFrame;
					Assert.Equal(FighterState.Attack, m.P1.State, "armour: the move plays on");
				}
				// The first frame after the hit (hitstop holds the world in between): it still travels its share.
				if (frameHit > 0 && stepAfter < 0 && frameBefore == frameHit && m.P1.MoveFrame == frameHit + 1)
					(xBefore, stepAfter) = (x, m.P1.X - x);
			}
			if (frameHit <= Startup || frameHit >= Startup + Active) continue; // not hit mid-travel with travel to come
			found = true;
			Assert.True(m.P1.Health < hp0, "the armoured hit still deals its damage");
			Assert.Equal(50 * S, stepAfter, $"hit on frame {frameHit}: frame {frameHit + 1} still travels its 50 units (from x {xBefore})");
		}
		Assert.True(found, "some start tick puts the projectile on P1's armour between its travel frames");
	}

	[Test]
	public static void ChargeDash_AddsToTheMovesTravel()
	{
		static FighterInput KihonHold(int t, int hold) =>
			t == 1 ? new FighterInput(InputBits.Special | InputBits.Right)
			: t <= 1 + hold ? new FighterInput(InputBits.Special) : Idle;
		foreach (var (hold, speed) in new[] { (0, 14), (10, 18), (30, 26) })
		{
			var m = Setup(Special(), -600, 450, modifier: SumoCharge);
			m.P1.Input.Scheme = ControlScheme.Kihon;
			int x0 = m.P1.X;
			Run(m, hold + 2 + Total, t => KihonHold(t, hold));
			Assert.Equal((Startup * speed + 300) * S, m.P1.X - x0, $"hold {hold}: {speed} x {Startup} startup frames of dash, then the move's 300");
		}

		// Together they still stop at the push box.
		var close = Setup(Special(), -300, 100, modifier: SumoCharge);
		close.P1.Input.Scheme = ControlScheme.Kihon;
		int gap = close.PushGap(close.P1, close.P2);
		foreach (var (x1, x2) in Run(close, 2 + Total, t => KihonHold(t, 0)))
			Assert.True(x2 - x1 >= gap, $"no overlap ({x1} vs {x2})");
		Assert.Equal(100 * S - gap, close.P1.X, "dash plus travel stops on the opponent's push box");
		Assert.Equal(100 * S, close.P2.X, "and never pushes it");
	}

	[Test]
	public static void ChargeDash_StillShovesTheOpponent_AsYok210Approved()
	{
		// Only forward_travel stops at the push box. The dash part is uncapped: ResolvePositions shares each tick's
		// 26-unit overlap (13 each), as on main before YOK-221.
		var m = Setup(Special(travel: 0), -60, 60, modifier: SumoCharge);
		m.P1.Input.Scheme = ControlScheme.Kihon;
		int gap = m.PushGap(m.P1, m.P2);
		Assert.Equal(gap, m.P2.X - m.P1.X, "they start touching");
		int started = 0;
		m.SpecialStarted += (mm, e) => { if (e.Fighter == 0 && started == 0) started = mm.Tick; };
		for (int t = 1; started == 0 || m.Tick < started + Startup - 1; t++)
		{
			m.Step(t == 1 ? new FighterInput(InputBits.Special | InputBits.Right) : t <= 31 ? new FighterInput(InputBits.Special) : Idle, Idle);
			Assert.True(t < 100, "the special starts");
		}
		Assert.Equal(31, started, "held to the window's max: the top speed, 26 units per tick");
		Assert.Equal(60 * S + Startup * 13 * S, m.P2.X, "P2 is shoved back half of each 26-unit dash step");
		Assert.Equal(m.P2.X - gap, m.P1.X, "and P1 keeps touching it");
	}

	[Test]
	public static void OpponentSteppingAway_LeavesThatMuchMoreRoom()
	{
		// P1 travels 600 (100 a frame) at P2, who faces away and travels 300 (50 a frame) the same ticks.
		var m = new Match(null, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Fixtures());
		m.P1.Equip(SpecialSlot.B, SpecialLoader.Parse(Special(600)));
		m.P2.Equip(SpecialSlot.B, SpecialLoader.Parse(Special(300, id: "test-forward-charge-slow")));
		m.P1.X = -150 * S;
		m.P2.X = 50 * S;
		m.P2.Facing = 1;
		int gap = m.PushGap(m.P1, m.P2);
		var xs = Run(m, Total, OnTick1, OnTick1);
		foreach (var (x1, x2) in xs) Assert.True(x2 - x1 >= gap, $"no overlap ({x1} vs {x2})");
		Assert.Equal(350 * S, m.P2.X, "P2 travelled all its 300 units away");
		Assert.Equal(m.P2.X - gap, m.P1.X, "P1 used the room P2 left and ends touching it, not a step short");
		for (int t = Startup + 2; t <= Startup + Active; t++)
			Assert.Equal(gap, xs[t - 1].x2 - xs[t - 1].x1, $"tick {t}: P1 keeps up with P2 each travel frame");
	}

	[Test]
	public static void TanukiCopy_TravelsLikeRyo()
	{
		var special = SpecialLoader.Parse(Special());
		var ryo = Kit.Load(Kit.Ryo, FightScene.FixtureSources);
		var tanuki = TanukiKit.Build(ryo, new CopyTarget(SpecialSlot.B, special, 1));

		Match NewMatch()
		{
			var m = new Match(null, ryo.Moves, tanuki.Moves);
			ryo.Equip(m.P1);
			tanuki.Equip(m.P2);
			m.P1.Equip(SpecialSlot.B, special);
			m.P1.X = -500 * S;
			m.P2.X = 500 * S;
			return m;
		}

		var a = NewMatch();
		var ryoXs = Run(a, Total, OnTick1);
		var b = NewMatch();
		var tanukiXs = Run(b, Total, _ => Idle, OnTick1);
		Assert.Equal((int)SpecialSlot.B, (int)b.P2.CopiedSlot, "the copy sits in Ryo's slot (T0)");
		for (int t = 0; t < Total; t++)
			Assert.Equal(ryoXs[t].x1 + 500 * S, 500 * S - tanukiXs[t].x2, $"tick {t + 1}: the copy travels as Ryo's does, mirrored");
		Assert.Equal(200 * S, b.P2.X, "300 units toward Ryo");
	}

	static List<ulong> Hashes(int travel)
	{
		var m = Setup(Special(travel, hitbox: true, armour: Armour), -400, 300, both: true, modifier: SumoCharge);
		var hs = new List<ulong>();
		for (int t = 1; t <= 240; t++)
		{
			var p1 = t % 50 == 1 ? Go : t % 50 == 30 ? FighterInput.Special(SpecialSlot.B) : new FighterInput(t % 7 < 3 ? InputBits.Right : InputBits.None);
			var p2 = t % 37 == 5 ? Go : t % 11 == 0 ? FighterInput.Special(SpecialSlot.A) : new FighterInput(t % 9 < 4 ? InputBits.Left : InputBits.None);
			m.Step(p1, p2);
			hs.Add(m.StateHash());
		}
		return hs;
	}

	[Test]
	public static void SameInputs_SameHashes_AndTravelIsInThem()
	{
		var a = Hashes(300);
		Assert.True(a.SequenceEqual(Hashes(300)), "same inputs, same hash every tick (F3)");
		Assert.True(!a.SequenceEqual(Hashes(0)), "travel changes the state the hash covers");
	}

	[Test]
	public static void TravellingFight_ReplaysIdentically()
	{
		string root = Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
		var catalog = ReplayCatalog.FromRepo(root);
		catalog.Specials.Add(SpecialLoader.Parse(Special(hitbox: true, armour: Armour)));
		catalog.Modifiers.Add(ModifierLoader.Parse(SumoCharge));

		var m = Setup(Special(hitbox: true, armour: Armour), int.MinValue, int.MinValue, both: true, modifier: SumoCharge);
		m.P1.Input.Scheme = ControlScheme.Kihon;
		var rec = ReplayRecorder.Begin(m, null, 0);
		int starts = 0;
		m.SpecialStarted += (_, e) => starts += e.Slot == SpecialSlot.B ? 1 : 0;
		for (int t = 1; t <= 600 && m.Phase != MatchPhase.Over; t++)
		{
			// P1: a Kihon charge (hold 12) every 60 ticks; P2: its own travel special, walking in between.
			int k = t % 60;
			var p1 = k == 1 ? new FighterInput(InputBits.Special | InputBits.Right) : k is > 1 and <= 13 ? new FighterInput(InputBits.Special) : Idle;
			var p2 = t % 45 == 20 ? Go : new FighterInput(t % 6 < 2 ? InputBits.Left : InputBits.None);
			m.Step(p1, p2);
			rec.Record(p1, p2, m.StateHash());
		}
		var done = rec.Finish(m);
		Assert.True(starts >= 4, $"both fighters' travelling specials played ({starts})");
		var back = ReplayRecording.FromBytes(done.ToBytes());
		var r = Replayer.Run(back, catalog);
		Assert.True(r.Ok, $"replays exactly: {r}");
		Assert.Equal(done.Steps, r.StepsRun, "every step replayed");
	}
}
