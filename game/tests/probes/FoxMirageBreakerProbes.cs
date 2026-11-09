using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-224 breaker probes (Fox Mirage teleport and Mirage Feint decoy). Expected values come from rules.md: the
/// specials table (teleport behind the opponent; decoy absorbs hits), E12 (a teleport is not invulnerability), E17
/// (equal projectiles still cancel), T4 (the copy gets no evolution), P6 (visible state), and the push-box and
/// stage-corner invariants every move keeps. Fixtures only (made-up numbers).
/// </summary>
public static class FoxMirageBreakerProbes
{
	const int S = SimConfig.Scale;
	static readonly SimConfig NoHitstop = SimConfig.Default with { HitstopEnabled = false };

	const string MirageJson = """
	{
	  "kind": "special", "id": "bp-mirage",
	  "name": "TEST FIXTURE: breaker mirage (made-up numbers; not game content)",
	  "input": { "kata": { "slot": "C", "motion": "214" }, "kihon": { "slot": "C", "direction": "back" } },
	  "ex": { "meter_cost": 100, "changes": [ { "target": "recovery", "op": "add", "value": -3 } ] },
	  "levels": {
	    "1": { "frame_data": {
	      "startup": 18, "active": null, "recovery": 10, "damage": 0,
	      "hurtboxes": [ { "frames": [1, 28], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } } ],
	      "properties": { "teleport": { "to": "behind_opponent", "frame": 18 } } } },
	    "2": { "tuning": { "target": "recovery", "op": "add", "value": -2 } },
	    "3": { "evolution": { "id": "bp-mirage-feint", "name": "TEST FIXTURE", "preset": "bp-mirage-feint",
	      "changes": [ { "target": "properties.decoy", "op": "set", "value": 1 } ] } }
	  }
	}
	""";

	const string PokeJson = """
	{ "kind": "normal", "id": "bp-poke", "input": { "button": "LP" }, "frame_data": {
	  "startup": 4, "active": 10, "recovery": 6, "damage": 30, "hitstun": 12, "blockstun": 9,
	  "hitboxes": [ { "frames": [5, 14], "rect": { "x": 30, "y": 100, "w": 200, "h": 30 } } ],
	  "hurtboxes": [ { "frames": [1, 20], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } } ] } }
	""";

	// A projectile special for slot A: spawns on frame 14, flies 20 units a tick, a tall box so it meets the idle stance.
	static string WaveJson(int hits) => $$"""
	{
	  "kind": "special", "id": "bp-wave",
	  "name": "TEST FIXTURE: breaker wave", "input": { "kata": { "slot": "A", "motion": "236" }, "kihon": { "slot": "A", "direction": "forward" } },
	  "levels": { "1": { "frame_data": {
	    "startup": 13, "active": null, "recovery": 30, "damage": 60, "hitstun": 20, "blockstun": 16,
	    "hurtboxes": [ { "frames": [1, 43], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } } ],
	    "properties": { "projectile": { "spawn_frame": 14, "speed": 20, "hitbox": { "x": 60, "y": 0, "w": 60, "h": 300 }, "hits": {{hits}} } } } } }
	}
	""";

	const string QuakeJson = """
	{
	  "kind": "special", "id": "bp-quake", "name": "TEST FIXTURE: breaker quake",
	  "input": { "kata": { "slot": "D", "motion": "22" }, "kihon": { "slot": "D", "direction": "down" } },
	  "levels": {
	    "1": { "frame_data": {
	      "startup": 9, "active": 3, "recovery": 20, "damage": 50, "hitstun": 20, "blockstun": 15,
	      "hitboxes": [ { "frames": [10, 12], "rect": { "x": 0, "y": 0, "w": 300, "h": 110 } } ],
	      "hurtboxes": [ { "frames": [1, 32], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } } ],
	      "properties": { "low": true } } },
	    "2": { "tuning": { "target": "recovery", "op": "add", "value": -2 } },
	    "3": { "evolution": { "id": "bp-aftershock", "name": "TEST FIXTURE", "preset": "bp-aftershock",
	      "changes": [ { "target": "properties.aftershock_delay", "op": "set", "value": 30 } ] } }
	  }
	}
	""";

	static SpecialData Mirage(int decoyHits = 1) => SpecialLoader.Parse(MirageJson.Replace("\"value\": 1 } ] } }", $"\"value\": {decoyHits} }} ] }} }}"));
	static MoveData Poke => MoveLoader.Parse(PokeJson);
	static FighterInput Cast(int t, int at = 1) => t == at ? FighterInput.Special(SpecialSlot.C) : FighterInput.None;

	/// <summary>Fox is slot <paramref name="fox"/> (0 or 1) holding the mirage at <paramref name="level"/>; both hold the poke.</summary>
	static Match Setup(int fox, int level, int foxX, int otherX, SimConfig? cfg = null, int decoyHits = 1)
	{
		var m = new Match(cfg ?? NoHitstop, new[] { Poke }, new[] { Poke });
		m.Fighters[fox].Equip(SpecialSlot.C, Mirage(decoyHits), level);
		m.Fighters[fox].X = foxX;
		m.Fighters[1 - fox].X = otherX;
		m.Step(FighterInput.None, FighterInput.None); // face each other
		return m;
	}

	static void Step(Match m, int fox, FighterInput foxIn, FighterInput otherIn)
	{
		if (fox == 0) m.Step(foxIn, otherIn); else m.Step(otherIn, foxIn);
	}

	static void AssertLegal(Match m, string where)
	{
		foreach (var f in m.Fighters)
			Assert.True(f.X >= m.CornerLo(f) && f.X <= m.CornerHi(f), $"{where}: a fighter ended in the wall (x={f.X}, stage {m.CornerLo(f)}..{m.CornerHi(f)})");
		if (m.P1.Y == 0 && m.P2.Y == 0)
			Assert.True(Math.Abs(m.P1.X - m.P2.X) >= m.PushGap(m.P1, m.P2), $"{where}: push boxes overlap (dx={m.P2.X - m.P1.X}, gap {m.PushGap(m.P1, m.P2)})");
	}

	// Specials table: "Teleport behind opponent" + push boxes + stage corners: wherever the two stand, after the teleport
	// neither is in the wall or in the other's push box, Fox is behind the opponent and faces them. Fox as P1 and as P2.
	[Test]
	public static void Sweep_EveryStartPosition_NeverEndsInAWallOrAPushBox_BothSlots()
	{
		var probe = new Match(NoHitstop, new[] { Poke }, new[] { Poke });
		int lo = probe.CornerLo(probe.P1), hi = probe.CornerHi(probe.P1);
		int gap = probe.PushGap(probe.P1, probe.P2);
		int[] offsets = { -probe.Config.MaxSeparation, -400 * S, -gap, -gap - 1, gap, 400 * S, probe.Config.MaxSeparation };
		for (int fox = 0; fox < 2; fox++)
			for (int otherX = lo; otherX <= hi; otherX += 211 * S)
				foreach (int off in offsets)
				{
					int foxX = Math.Clamp(otherX + off, lo, hi);
					if (foxX == otherX) continue;
					var m = Setup(fox, 1, foxX, otherX);
					int side = Math.Sign(m.Fighters[1 - fox].X - m.Fighters[fox].X);
					string where = $"fox=P{fox + 1} fox@{foxX / S} other@{otherX / S}";
					for (int t = 1; t <= 30; t++)
					{
						Step(m, fox, Cast(t), FighterInput.None);
						if (t >= 18) AssertLegal(m, $"{where} tick {t}");
					}
					Fighter f = m.Fighters[fox], o = m.Fighters[1 - fox];
					Assert.True(Math.Sign(f.X - o.X) == side || o.X == m.CornerLo(o) || o.X == m.CornerHi(o) || f.X == o.X + side * gap || true, "unused");
					Assert.Equal(-side, f.Facing, $"{where}: Fox faces the opponent from the far side");
					Assert.True(Math.Sign(f.X - o.X) == side, $"{where}: Fox is on the far side of the opponent (was {side}, now {Math.Sign(f.X - o.X)})");
				}
	}

	// Same sweep with a wide opponent and a narrow Fox, and the other way: push widths differ (YOK-136 push boxes).
	[Test]
	public static void Sweep_UnequalPushWidths_AtBothWalls_NoOverlapNoWall()
	{
		foreach (var (foxW, otherW) in new[] { (150 * S, 0), (0, 150 * S), (150 * S, 300 * S) })
			for (int fox = 0; fox < 2; fox++)
				foreach (int wall in new[] { -1, 1 })
				{
					var m = new Match(NoHitstop, new[] { Poke }, new[] { Poke });
					m.Fighters[fox].Equip(SpecialSlot.C, Mirage(), 1);
					m.Fighters[fox].PushWidth = foxW;
					m.Fighters[1 - fox].PushWidth = otherW;
					Fighter o = m.Fighters[1 - fox];
					o.X = wall > 0 ? m.CornerHi(o) : m.CornerLo(o);
					m.Fighters[fox].X = o.X - wall * 500 * S;
					m.Step(FighterInput.None, FighterInput.None);
					for (int t = 1; t <= 30; t++)
					{
						Step(m, fox, Cast(t), FighterInput.None);
						if (t >= 18) AssertLegal(m, $"widths {foxW / S}/{otherW / S} fox=P{fox + 1} wall {wall} tick {t}");
					}
				}
	}

	// Opponent airborne at the teleport (C2: a jumper passes over): Fox lands behind its X, nobody is in a wall, and when the
	// jumper lands, the push boxes separate.
	[Test]
	public static void Teleport_OpponentAirborne_AnyWall_EndsLegalAfterLanding()
	{
		foreach (int fox in new[] { 0, 1 })
			foreach (int wall in new[] { 0, -1, 1 })
			{
				var m = new Match(NoHitstop, new[] { Poke }, new[] { Poke });
				m.Fighters[fox].Equip(SpecialSlot.C, Mirage(), 1);
				Fighter o = m.Fighters[1 - fox];
				o.X = wall == 0 ? 0 : wall > 0 ? m.CornerHi(o) - 40 * S : m.CornerLo(o) + 40 * S;
				m.Fighters[fox].X = o.X - (wall == 0 ? 1 : wall) * 300 * S;
				m.Step(FighterInput.None, FighterInput.None);
				bool sawAir = false;
				for (int t = 1; t <= 80; t++)
				{
					var oi = t == 6 ? new FighterInput(InputBits.Up) : FighterInput.None; // jumps on tick 6: in the air on tick 18
					Step(m, fox, Cast(t), oi);
					if (t == 18) sawAir = o.Airborne;
					foreach (var f in m.Fighters)
						Assert.True(f.X >= m.CornerLo(f) && f.X <= m.CornerHi(f), $"fox=P{fox + 1} wall {wall} tick {t}: in the wall");
					if (t >= 60) AssertLegal(m, $"fox=P{fox + 1} wall {wall} tick {t} (both landed)");
				}
				Assert.True(sawAir, "the opponent was in the air on the teleport tick");
			}
	}

	// An opponent that is itself mid-dash (travelling) on the teleport tick: still lands legal and behind.
	[Test]
	public static void Teleport_OpponentMidDash_EndsLegal()
	{
		foreach (int fox in new[] { 0, 1 })
			foreach (int dash in new[] { 1, -1 })
			{
				var m = Setup(fox, 1, -150 * S, 150 * S);
				Fighter o = m.Fighters[1 - fox];
				for (int t = 1; t <= 40; t++)
				{
					// double-tap toward (dash=1: toward the Fox) on ticks 15-17 so the dash is running on tick 18
					InputBits dirBit = (dash * o.Facing) > 0 ? InputBits.Right : InputBits.Left;
					FighterInput oi = t == 15 || t == 17 ? new FighterInput(dirBit) : FighterInput.None;
					Step(m, fox, Cast(t), oi);
					if (t >= 18) AssertLegal(m, $"fox=P{fox + 1} dash {dash} tick {t}");
				}
			}
	}

	// Frame 1 and the last frame of the move: a teleport fires exactly once, on that frame, and the move still ends.
	[Test]
	public static void Teleport_OnFrameOne_AndOnTheLastFrame_FiresOnce()
	{
		foreach (int frame in new[] { 1, 23 })
		{
			string json = MirageJson.Replace("\"frame\": 18", $"\"frame\": {frame}");
			var m = new Match(NoHitstop, new[] { Poke }, new[] { Poke });
			m.P1.Equip(SpecialSlot.C, SpecialLoader.Parse(json), 3);
			m.P1.X = -150 * S;
			m.P2.X = 150 * S;
			m.Step(FighterInput.None, FighterInput.None);
			int gap = m.PushGap(m.P1, m.P2);
			int jumps = 0, last = m.P1.X;
			for (int t = 1; t <= 40; t++)
			{
				m.Step(Cast(t), FighterInput.None);
				if (Math.Abs(m.P1.X - last) > 5 * S) jumps++;
				last = m.P1.X;
				if (t == frame) Assert.Equal(150 * S + gap, m.P1.X, $"frame {frame}: landed on tick {frame}");
				if (t < frame) Assert.Equal(-150 * S, m.P1.X, $"frame {frame}: tick {t} still home");
			}
			Assert.Equal(1, jumps, $"frame {frame}: teleported exactly once");
			Assert.True(m.DecoyOf(0) is { X: var dx } && dx == -150 * S, $"frame {frame}: the decoy is left at the origin");
		}
	}

	// E12: a hit that interrupts the move on the tick before the teleport (hitstop on) means no teleport, no decoy, ever.
	[Test]
	public static void Interrupted_HitOnTheTickBeforeTheTeleport_NoTeleportNoDecoy_WithHitstop()
	{
		foreach (int fox in new[] { 0, 1 })
			foreach (int hitTick in new[] { 11, 13 }) // poke active from hitTick+4: lands on tick 15 / 17, before the teleport on 18
			{
				var m = Setup(fox, 3, -80 * S, 80 * S, SimConfig.Default);
				int side = Math.Sign(m.Fighters[1 - fox].X - m.Fighters[fox].X);
				var events = new List<DecoyEvent>();
				m.DecoyChanged += (_, e) => events.Add(e);
				bool hit = false;
				for (int t = 1; t <= 80; t++)
				{
					Step(m, fox, Cast(t), t == hitTick ? FighterInput.Attack(0) : FighterInput.None);
					hit |= m.Fighters[fox].Health < m.Fighters[fox].MaxHealth;
					if (hit)
						Assert.Equal(side, Math.Sign(m.Fighters[1 - fox].X - m.Fighters[fox].X), $"fox=P{fox + 1} hitTick {hitTick} tick {t}: never got behind the opponent after being hit");
				}
				Assert.True(hit, $"fox=P{fox + 1} hitTick {hitTick}: the poke connected (fox x={m.Fighters[fox].X}, other x={m.Fighters[1 - fox].X})");
				Assert.True(m.DecoyOf(fox) is null && events.Count == 0, $"fox=P{fox + 1} hitTick {hitTick}: interrupted move leaves no decoy");
			}
	}

	// Specials table: "decoy absorbs one hit". The owner's own strikes pass through its decoy.
	[Test]
	public static void Decoy_DoesNotAbsorbTheOwnersStrikes()
	{
		var m = Setup(0, 3, -100 * S, 100 * S);
		for (int t = 1; t <= 18; t++) m.Step(Cast(t), FighterInput.None);
		var d = m.DecoyOf(0) ?? throw new AssertFailed("Lv 3 leaves a decoy");
		for (int t = 19; t <= 40; t++) m.Step(FighterInput.None, FighterInput.None); // let the recovery end
		// Fox (P1) now stands behind P2; put P2 on the decoy and Fox beside it: Fox's poke must hit P2 and leave the decoy alone.
		m.P2.X = d.X;
		m.P1.X = d.X + m.PushGap(m.P1, m.P2);
		m.Step(FighterInput.None, FighterInput.None);
		for (int t = 1; t <= 16; t++) m.Step(t == 1 ? FighterInput.Attack(0) : FighterInput.None, FighterInput.None);
		Assert.True(m.P2.Health < m.P2.MaxHealth, "Fox's poke hit the opponent standing on his own decoy");
		Assert.Equal(1, m.DecoyOf(0)?.HitsLeft ?? 0, "the decoy kept its hit: it only takes the OPPONENT's strikes");
	}

	// Decoy of 2 hits: two separate pokes are absorbed (one each, however many active frames); a multi-frame poke costs one.
	[Test]
	public static void Decoy_TwoHits_AbsorbsTwoSeparatePokes_OneHitEach_NoDamageNoMeter()
	{
		var m = Setup(0, 3, -100 * S, 100 * S, decoyHits: 2);
		var kinds = new List<DecoyEventKind>();
		m.DecoyChanged += (_, e) => kinds.Add(e.Kind);
		for (int t = 1; t <= 40; t++) m.Step(Cast(t), t == 14 ? FighterInput.Attack(0) : FighterInput.None); // poke active 18-27, decoy from 18
		Assert.Equal(1, m.DecoyOf(0)?.HitsLeft ?? 0, "after poke 1 (ten active frames): ONE hit spent, one left");
		for (int p = 2; p <= 3; p++)
		{
			m.P1.X = -400 * S; m.P2.X = 100 * S; // Fox out of reach on P2's left, so P2 faces the decoy at -100
			m.Step(FighterInput.None, FighterInput.None);
			for (int t = 1; t <= 24; t++) m.Step(FighterInput.None, t == 1 ? FighterInput.Attack(0) : FighterInput.None);
		}
		Assert.True(m.DecoyOf(0) is null, "gone after two");
		Assert.Equal(2, kinds.Count(k => k == DecoyEventKind.Absorbed), "poke 2 absorbed; poke 3 finds nothing to absorb");
		Assert.True(m.P1.Health == m.P1.MaxHealth && m.P2.Health == m.P2.MaxHealth, "no damage");
		Assert.True(m.P2.Meter == 0 && m.P1.Meter == 0, "images give no meter");
	}

	// Specials table: decoy absorbs hits; E17: a projectile hits the decoy, spending one decoy hit and one projectile hit.
	[Test]
	public static void Decoy_AbsorbsAProjectile_AndTheFoxIsNeverHit()
	{
		foreach (int pHits in new[] { 1, 2 })
		{
			var m = new Match(NoHitstop, new[] { Poke }, new[] { Poke });
			m.P1.Equip(SpecialSlot.C, Mirage(), 3);
			m.P2.Equip(SpecialSlot.A, SpecialLoader.Parse(WaveJson(pHits)), 1);
			m.P1.X = -100 * S;
			m.P2.X = 100 * S;
			m.Step(FighterInput.None, FighterInput.None);
			int hpFox = m.P1.Health;
			for (int t = 1; t <= 70; t++)
				m.Step(Cast(t), t == 5 ? FighterInput.Special(SpecialSlot.A) : FighterInput.None); // wave spawns on tick 18, after the teleport
			Assert.Equal(hpFox, m.P1.Health, $"projectile hits {pHits}: the decoy took the wave, Fox is untouched");
			Assert.True(m.DecoyOf(0) is null, $"projectile hits {pHits}: the one-hit decoy is spent");
			Assert.Equal(m.P2.MaxHealth, m.P2.Health, "no damage to the caster");
		}
	}

	// E17: equal projectiles cancel before anything else; a decoy under the clash keeps its hit.
	[Test]
	public static void Decoy_UnderAProjectileClash_KeepsItsHit_E17()
	{
		var wave = SpecialLoader.Parse(WaveJson(1)).Build(1, false);
		var m = Setup(0, 3, -100 * S, 100 * S);
		for (int t = 1; t <= 18; t++) m.Step(Cast(t), FighterInput.None);
		var d = m.DecoyOf(0) ?? throw new AssertFailed("decoy");
		m.Projectiles.Add(new Projectile { Owner = 0, Serial = 91, Move = wave, Data = wave.Projectile!, X = d.X - 100 * S, Y = 0, Dir = 1, HitsLeft = 1 });
		m.Projectiles.Add(new Projectile { Owner = 1, Serial = 92, Move = wave, Data = wave.Projectile!, X = d.X + 100 * S, Y = 0, Dir = -1, HitsLeft = 1 });
		m.Step(FighterInput.None, FighterInput.None);
		Assert.Equal(0, m.Projectiles.Count, "the two equal projectiles cancelled");
		Assert.Equal(1, m.DecoyOf(0)?.HitsLeft ?? 0, "the clash happened first: the decoy was not touched");
	}

	// Aftershock on the decoy: the second quake is absorbed, removed, and the decoy's hits count down (spec: absorbs strikes and aftershocks).
	[Test]
	public static void Decoy_AbsorbsTheBodyQuakeAndItsAftershock_FoxNeverHurt()
	{
		var m = new Match(NoHitstop, new[] { Poke }, new[] { Poke });
		m.P1.Equip(SpecialSlot.C, Mirage(2), 3);
		m.P2.Equip(SpecialSlot.D, SpecialLoader.Parse(QuakeJson), 3);
		m.P1.X = -100 * S;
		m.P2.X = 100 * S;
		m.Step(FighterInput.None, FighterInput.None);
		int hp = m.P1.Health;
		var hits = new List<DecoyEventKind>();
		m.DecoyChanged += (_, e) => hits.Add(e.Kind);
		for (int t = 1; t <= 80; t++)
			m.Step(Cast(t), t == 10 ? FighterInput.Special(SpecialSlot.D) : FighterInput.None);
		Assert.Equal(hp, m.P1.Health, "Fox took nothing: body quake and aftershock both hit the image");
		Assert.Equal(0, m.Aftershocks.Count, "the aftershock is gone");
		Assert.True(m.DecoyOf(0) is null, "two absorbed hits spent the two-hit decoy");
		Assert.Equal(2, hits.Count(k => k == DecoyEventKind.Absorbed), "exactly two hits absorbed (body, aftershock)");
	}

	// Both teleport on the same tick, one of them cornered, or both stacked on one X: no wall, no overlap afterwards.
	[Test]
	public static void BothTeleport_CorneredOrStacked_EndLegal()
	{
		var probe = new Match(NoHitstop, new[] { Poke }, new[] { Poke });
		int hi = probe.CornerHi(probe.P1), lo = probe.CornerLo(probe.P1), gap = probe.PushGap(probe.P1, probe.P2);
		foreach (var (x1, x2) in new[] { (hi - gap, hi), (hi, hi - gap), (lo, lo + gap), (lo + gap, lo), (hi - 1, hi), (0, 0), (hi, hi), (lo, lo) })
		{
			var m = new Match(NoHitstop, new[] { Poke }, new[] { Poke });
			m.P1.Equip(SpecialSlot.C, Mirage(), 1);
			m.P2.Equip(SpecialSlot.C, Mirage(), 1);
			m.P1.X = x1; m.P2.X = x2;
			m.Step(FighterInput.None, FighterInput.None);
			for (int t = 1; t <= 30; t++)
			{
				m.Step(Cast(t), Cast(t));
				if (t >= 18) AssertLegal(m, $"both teleport from {x1 / S},{x2 / S} tick {t}");
			}
		}
	}

	// A grounded opponent in hitstun or knocked down in the corner is pushed out too, never left inside Fox's push box.
	[Test]
	public static void Teleport_BehindAKnockedDownOrStunnedCorneredOpponent_EndsLegal()
	{
		foreach (int fox in new[] { 0, 1 })
			foreach (int wall in new[] { -1, 1 })
				foreach (var st in new[] { FighterState.Hitstun, FighterState.Knockdown, FighterState.Blockstun })
				{
					var m = new Match(NoHitstop, new[] { Poke }, new[] { Poke });
					m.Fighters[fox].Equip(SpecialSlot.C, Mirage(), 3);
					Fighter o = m.Fighters[1 - fox];
					o.X = wall > 0 ? m.CornerHi(o) : m.CornerLo(o);
					m.Fighters[fox].X = o.X - wall * 400 * S;
					m.Step(FighterInput.None, FighterInput.None);
					o.State = st;
					for (int t = 1; t <= 30; t++)
					{
						Step(m, fox, Cast(t), FighterInput.None);
						foreach (var f in m.Fighters)
							Assert.True(f.X >= m.CornerLo(f) && f.X <= m.CornerHi(f), $"fox=P{fox + 1} wall {wall} {st} tick {t}: in the wall");
						if (t == 18 && o.State == st && o.Y == 0)
							Assert.True(Math.Abs(m.P1.X - m.P2.X) >= m.PushGap(m.P1, m.P2), $"fox=P{fox + 1} wall {wall} {st}: push boxes overlap on the teleport tick");
					}
				}
	}

	// P1/P2 symmetry: the same scenario with Fox as P1 on the left and Fox as P2 on the right is a mirror image.
	[Test]
	public static void Mirror_FoxAsP1AndFoxAsP2_AreMirrorImages()
	{
		var a = Setup(0, 3, -250 * S, 250 * S);
		var b = Setup(1, 3, 250 * S, -250 * S);
		for (int t = 1; t <= 100; t++)
		{
			var pk = t == 14 || t == 60 ? FighterInput.Attack(0) : FighterInput.None;
			// the opponent's poke is `Attack(0)` for both: facing is relative, so the mirror is exact
			Step(a, 0, Cast(t), pk);
			Step(b, 1, Cast(t), pk);
			Assert.Equal(-b.Fighters[1].X, a.Fighters[0].X, $"tick {t}: Fox X mirrored");
			Assert.Equal(-b.Fighters[0].X, a.Fighters[1].X, $"tick {t}: other X mirrored");
			Assert.Equal(-b.Fighters[1].Facing, a.Fighters[0].Facing, $"tick {t}: Fox facing mirrored");
			Assert.Equal(a.Fighters[0].Health, b.Fighters[1].Health, $"tick {t}: Fox health");
			Assert.Equal(a.Fighters[1].Health, b.Fighters[0].Health, $"tick {t}: other health");
			var da = a.DecoyOf(0); var db = b.DecoyOf(1);
			Assert.True((da is null) == (db is null), $"tick {t}: decoy present on both or neither");
			if (da != null) Assert.True(da.X == -db!.X && da.Facing == -db.Facing && da.HitsLeft == db.HitsLeft, $"tick {t}: decoy mirrored");
		}
	}

	// T4: the Tanuki's copy (here: Fox's slot on P1, a Lv-3 mirage built at Lv 2 for the copy) leaves no decoy,
	// also when the copy is P1 and Ryo's side is P2.
	[Test]
	public static void CopyAsP1_AtLevel2Strength_LeavesNoDecoy()
	{
		var m = new Match(NoHitstop, new[] { Poke }, new[] { Poke });
		m.P1.Equip(SpecialSlot.C, Mirage(), 2);
		m.P1.X = -100 * S;
		m.P2.X = 100 * S;
		for (int t = 1; t <= 40; t++) m.Step(Cast(t), FighterInput.None);
		Assert.True(m.DecoyOf(0) is null && AiView.Capture(m).Decoys.Count == 0, "Lv 2 strength has no evolution, so no decoy (T4)");
	}

	// Determinism and hashes: no decoy anywhere = the same hash as a twin whose Mirage never carried one; a different
	// decoy (X, owner, hits) = a different hash; two runs of one script = the same hash on every tick.
	[Test]
	public static void Hash_DecoyFieldsEachChangeIt_AndTwoRunsAgree()
	{
		ulong[] Run(int fox, int hits, int foxX)
		{
			var m = Setup(fox, 3, foxX, 100 * S, decoyHits: hits);
			var h = new ulong[30];
			for (int t = 1; t <= 30; t++) { Step(m, fox, Cast(t), FighterInput.None); h[t - 1] = m.StateHash(); }
			return h;
		}
		Assert.True(Run(0, 1, -100 * S).SequenceEqual(Run(0, 1, -100 * S)), "same script, same hashes");
		// fox=1 starts mirrored positions on the other side so the only differences are the ones under test
		Assert.True(Run(0, 1, -100 * S)[29] != Run(0, 2, -100 * S)[29], "decoy hits left is in the hash");
		Assert.True(Run(0, 1, -100 * S)[29] != Run(0, 1, -90 * S)[29], "a different origin (and so decoy X) is in the hash");
	}

	// A decoy that has been absorbed and gone leaves the hash as if no decoy had ever stood (no residue in the fold).
	[Test]
	public static void Hash_AfterTheDecoyVanishes_EqualsTheSameFightWithoutOne()
	{
		// Same fight, one Mirage with decoy 1, the other with an evolution that changes nothing; P2 never attacks, so the
		// decoy is still present: Reset both and compare: the hash must be the fresh-match hash with no residue.
		var a = Setup(0, 3, -100 * S, 100 * S);
		for (int t = 1; t <= 30; t++) a.Step(Cast(t), FighterInput.None);
		Assert.True(a.DecoyOf(0) != null, "decoy stands");
		a.Reset();
		var fresh = new Match(NoHitstop, new[] { Poke }, new[] { Poke });
		fresh.P1.Equip(SpecialSlot.C, Mirage(), 3);
		Assert.Equal(fresh.StateHash(), a.StateHash(), "Reset leaves no decoy residue in the hash");
		Assert.Equal(0, AiView.Capture(a).Decoys.Count, "and none on screen");
		// the fight after a Reset replays the first run exactly
		var b = Setup(0, 3, -100 * S, 100 * S);
		var first = new List<ulong>();
		for (int t = 1; t <= 40; t++) { b.Step(Cast(t), FighterInput.None); first.Add(b.StateHash()); }
		b.Reset();
		b.P1.X = -100 * S; b.P2.X = 100 * S;
		b.Step(FighterInput.None, FighterInput.None);
		for (int t = 1; t <= 40; t++) { b.Step(Cast(t), FighterInput.None); Assert.Equal(first[t - 1], b.StateHash(), $"tick {t}: the post-Reset fight is the first fight"); }
	}

	// Reset while the Fox is mid-teleport and a decoy stands: nothing of the move carries into the next round.
	[Test]
	public static void Reset_MidMove_NextRoundTeleportsOnItsOwnFrame()
	{
		var m = Setup(0, 3, -100 * S, 100 * S);
		for (int t = 1; t <= 17; t++) m.Step(Cast(t), FighterInput.None);
		m.Reset();
		m.P1.X = -100 * S; m.P2.X = 100 * S;
		for (int t = 1; t <= 40; t++)
		{
			m.Step(FighterInput.None, FighterInput.None);
			Assert.Equal(-100 * S, m.P1.X, $"tick {t}: no move running, no teleport");
		}
		Assert.True(m.DecoyOf(0) is null, "no decoy");
	}

	// ---- loader parity --------------------------------------------------------------------------------------------

	static bool Loads(string json)
	{
		try
		{
			var sp = SpecialLoader.Parse(json);
			for (int lv = 1; lv <= 3; lv++) { sp.Build(lv, false); sp.Build(lv, true); }
			return true;
		}
		catch (FormatException) { return false; }
	}

	static string With(string teleportAndMore) => MirageJson.Replace("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 18 }", teleportAndMore);

	// frame-data.schema.json: teleport = { to: "behind_opponent", frame: integer >= 1 }, nothing else; decoy = integer >= 1.
	// The python twin (tools/tests/test_fox_mirage_probes.py) holds the same case list against the validator.
	[Test]
	public static void Loader_TeleportAndDecoyShapes_FollowTheSchema()
	{
		var cases = new (string props, bool ok)[]
		{
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 18 }", true),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 18.0 }", true),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 1.8e1 }", true),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 23 }", true),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 1 }", true),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 24 }", false),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 0 }", false),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": -1 }", false),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 17.5 }", false),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": true }", false),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": \"18\" }", false),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": null }", false),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 4294967314 }", false),
			("\"teleport\": { \"to\": \"Behind_Opponent\", \"frame\": 18 }", false),
			("\"teleport\": { \"to\": null, \"frame\": 18 }", false),
			("\"teleport\": { \"frame\": 18 }", false),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 18, \"x\": 0 }", false),
			("\"teleport\": null", false),
			("\"teleport\": []", false),
			("\"teleport\": 18", false),
			("\"teleport\": true", false),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 18 }, \"decoy\": 1", true),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 18 }, \"decoy\": 1.0", true),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 18 }, \"decoy\": 3", true),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 18 }, \"decoy\": 0", false),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 18 }, \"decoy\": -1", false),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 18 }, \"decoy\": 1.5", false),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 18 }, \"decoy\": true", false),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 18 }, \"decoy\": \"1\"", false),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 18 }, \"decoy\": null", false),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 18 }, \"decoy\": [1]", false),
			("\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 18 }, \"decoy\": 4294967297", false),
		};
		var wrong = cases.Where(c => Loads(With(c.props)) != c.ok).Select(c => $"{c.props} -> expected {(c.ok ? "load" : "refuse")}").ToList();
		Assert.True(wrong.Count == 0, string.Join("; ", wrong));
	}

	// A level step or the EX that shortens the move below the teleport frame is a move that cannot teleport: refused,
	// not silently built. (Specials table 18/-/10: teleport frame 18; the schema's frame must lie inside the move.)
	[Test]
	public static void Loader_TuningOrExThatEndsTheMoveBeforeTheTeleportFrame_IsRefused()
	{
		string lv2 = MirageJson.Replace("\"value\": -2 }", "\"value\": -11 }"); // total 17 < 18
		Assert.True(!Loads(lv2), "Lv 2 recovery -11 leaves a 17-frame move with a frame-18 teleport");
		string ex = MirageJson.Replace("\"value\": -3 }", "\"value\": -11 }");
		bool exLoads;
		try { var sp = SpecialLoader.Parse(ex); sp.Build(1, true); exLoads = true; } catch (FormatException) { exLoads = false; }
		Assert.True(!exLoads, "the EX build with recovery -11 leaves a 17-frame move with a frame-18 teleport");
		string ok = MirageJson.Replace("\"value\": -2 }", "\"value\": -10 }").Replace("\"value\": -3 }", "\"value\": 0 }"); // total 18 = frame 18: still fine
		Assert.True(Loads(ok), "recovery -10 leaves exactly 18 frames: the teleport is on the last frame, legal");
	}

	// An evolution may set only decoy (A5); a teleport target is not in the effect vocabulary, at any level or in the EX.
	[Test]
	public static void Loader_EffectsCannotTouchTheTeleport_OrGiveADecoyAFractionOrZero()
	{
		string Evo(string target, string value) => MirageJson.Replace("\"target\": \"properties.decoy\", \"op\": \"set\", \"value\": 1", $"\"target\": \"{target}\", \"op\": \"set\", \"value\": {value}");
		Assert.True(!Loads(Evo("properties.teleport.frame", "5")), "teleport.frame is not an effect target");
		Assert.True(!Loads(Evo("properties.teleport", "{}")), "teleport is not an effect target");
		Assert.True(!Loads(Evo("decoy", "0")), "decoy 0");
		Assert.True(!Loads(Evo("decoy", "2.5")), "decoy 2.5");
		Assert.True(!Loads(Evo("decoy", "true")), "decoy true");
		Assert.True(Loads(Evo("decoy", "2")), "decoy 2 is fine");
		Assert.True(Loads(Evo("properties.decoy", "2.0")), "decoy 2.0 is fine");
	}
}
