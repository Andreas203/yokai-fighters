using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-226 Test Breaker probes: launch, wall bounce and multi-hit (rules.md specials table, A5, E4, E12, T4, F3).
/// Moves are TEST FIXTURES (made-up numbers) except where a probe says it reads data/moves.
/// </summary>
public static class ReactionBreakerProbes
{
	const int S = SimConfig.Scale;
	static readonly SimConfig NoHitstop = SimConfig.Default with { HitstopEnabled = false };
	static string Repo => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");

	static SpecialData Special(string id, string frameData, string evolution) => SpecialLoader.Parse($$"""
	{ "kind": "special", "id": "{{id}}", "name": "TEST FIXTURE (made-up numbers; not game content)",
	  "input": { "kata": { "slot": "C", "motion": "214" }, "kihon": { "slot": "C", "direction": "back" } },
	  "levels": {
	    "1": { "frame_data": {{frameData}} },
	    "2": { "tuning": { "target": "recovery", "op": "add", "value": -1 } },
	    "3": { "evolution": { "id": "{{id}}-evo", "name": "TEST FIXTURE", "preset": "{{id}}-evo", "changes": {{evolution}} } } } }
	""");

	const string Strike = """
	{ "startup": 3, "active": 3, "recovery": 6, "damage": 50, "hitstun": 15, "blockstun": 10,
	  "hitboxes": [ { "frames": [4, 6], "rect": { "x": 0, "y": 0, "w": 150, "h": 600 } } ],
	  "hurtboxes": [ { "frames": [1, 12], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } } ] }
	""";

	const string Spin = """
	{ "startup": 3, "active": 12, "recovery": 5, "damage": 20, "hitstun": 10, "blockstun": 8,
	  "hitboxes": [ { "frames": [4, 15], "rect": { "x": 0, "y": 0, "w": 300, "h": 320 } } ],
	  "hurtboxes": [ { "frames": [1, 20], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } } ] }
	""";

	static SpecialData Launcher => Special("probe-launcher", Strike, """[ { "target": "properties.launch", "op": "set", "value": true } ]""");
	static SpecialData Charger => Special("probe-charger", Strike, """[ { "target": "properties.wall_bounce", "op": "set", "value": true } ]""");
	static SpecialData Spinner => Special("probe-spinner", Spin,
		"""[ { "target": "properties.multi_hit", "op": "set", "value": { "hits": 3, "interval": 4 } } ]""");

	static MoveData Poke(int damage = 10) => MoveLoader.Parse($$"""
	{ "kind": "normal", "id": "probe-poke", "input": { "button": "LP" }, "frame_data": {
	  "startup": 2, "active": 4, "recovery": 4, "damage": {{damage}}, "hitstun": 12, "blockstun": 9,
	  "hitboxes": [ { "frames": [3, 6], "rect": { "x": 0, "y": 0, "w": 150, "h": 600 } } ],
	  "hurtboxes": [ { "frames": [1, 10], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } } ] } }
	""");

	static Match Setup(SpecialData sp, int level, int p1X = -50, int p2X = 50, SimConfig? cfg = null)
	{
		var m = new Match(cfg ?? NoHitstop, new[] { Poke() }, new[] { Poke() });
		m.P1.Equip(SpecialSlot.C, sp, level);
		m.P1.X = p1X * S;
		m.P2.X = p2X * S;
		return m;
	}

	static void Idle(Match m, int ticks) { for (int i = 0; i < ticks; i++) m.Step(FighterInput.None, FighterInput.None); }

	// E4/A5 (launch): a juggle follow-up shortens nothing: the launched fighter stays on the arc and still lands in the
	// standard 40 frame knockdown (E4), whenever the follow-up came.
	[Test]
	public static void Launch_FollowUpEarlyInTheRise_StillLandsInAFullKnockdown()
	{
		var m = Setup(Launcher, 3);
		m.Step(FighterInput.Special(SpecialSlot.C), FighterInput.None);
		Idle(m, 3);
		Assert.True(m.P2.Launched, "setup: launched");
		int guard = 0;
		while (!m.P1.Actionable && guard++ < 40) Idle(m, 1);
		m.Step(FighterInput.Attack(0), FighterInput.None);
		int ticksToLand = 0, lowestKnockdown = int.MaxValue;
		while (m.P2.Airborne && ticksToLand++ < 80) Idle(m, 1);
		Assert.True(!m.P2.Airborne && !m.P2.Launched, "landed and no longer launched");
		Assert.Equal(FighterState.Knockdown, m.P2.State, "E4: lands in a knockdown");
		lowestKnockdown = m.P2.StunLeft;
		Assert.True(lowestKnockdown >= m.Config.KnockdownFrames - 2, $"E4: a full {m.Config.KnockdownFrames} frame knockdown, {lowestKnockdown} left on touchdown");
	}

	// KO edge: a juggle follow-up that KOs a launched fighter ends the fight with Ryo the winner and the KO state sticks
	// (no wake-up, no second knockdown from landing).
	[Test]
	public static void Launch_FollowUpKosTheLaunchedFighter_StaysKnockedOut()
	{
		var m = Setup(Launcher, 3);
		m.P2.Health = 55; // launcher 50: 5 left, so launched; the 10 poke KOs
		m.Step(FighterInput.Special(SpecialSlot.C), FighterInput.None);
		Idle(m, 3);
		Assert.True(m.P2.Launched && m.P2.Health == 5, $"setup: launched at 5 health ({m.P2.Health})");
		int guard = 0;
		while (!m.P1.Actionable && guard++ < 40) Idle(m, 1);
		m.Step(FighterInput.Attack(0), FighterInput.None);
		Idle(m, 6);
		Assert.True(m.P2.KnockedOut && m.P2.Health == 0, "the follow-up KOs");
		Idle(m, 120);
		Assert.True(m.P2.KnockedOut && m.P2.Health == 0, "still KO'd long after landing");
		Assert.Equal(0, m.Winner, "Ryo wins");
		Assert.True(m.P2.State != FighterState.Idle && m.P2.State != FighterState.WakeUp, $"a KO'd fighter does not get up ({m.P2.State})");
	}

	// E12: a launched (airborne) fighter cannot be grabbed: a throw whiffs on it.
	[Test]
	public static void Launch_ALaunchedFighterIsNotThrowable()
	{
		var m = Setup(Launcher, 3);
		m.Step(FighterInput.Special(SpecialSlot.C), FighterInput.None);
		Idle(m, 3);
		Assert.True(m.P2.Launched && m.P2.Airborne, "setup");
		Assert.True(!Match.Throwable(m.P2), "E12: airborne launched fighter is not throwable");
		// Landed in knockdown / wake-up: also not throwable (E24).
		int guard = 0;
		while (m.P2.Airborne && guard++ < 80) Idle(m, 1);
		Assert.True(!Match.Throwable(m.P2), "knockdown after landing: not throwable");
	}

	// Reset: nothing of the last fight leaks. A launch mid-air and a multi-hit mid-spin, then Reset, hash like a fresh match.
	[Test]
	public static void Reset_AfterALaunchAndAfterAMultiHit_HashesLikeAFreshMatch()
	{
		foreach (var (sp, ticks) in new[] { (Launcher, 6), (Spinner, 9) })
		{
			var fresh = Setup(sp, 3); fresh.Reset();
			var used = Setup(sp, 3);
			used.Step(FighterInput.Special(SpecialSlot.C), FighterInput.None);
			Idle(used, ticks);
			Assert.True(used.P2.Launched || used.P1.HitsLanded > 0, $"{sp.Id}: setup: state is set");
			used.Reset();
			used.P1.X = -50 * S; used.P2.X = 50 * S; fresh.P1.X = -50 * S; fresh.P2.X = 50 * S;
			Assert.True(!used.P2.Launched && used.P1.HitsLanded == 0 && used.P1.LastHitFrame == 0, $"{sp.Id}: flags cleared");
			Assert.Equal(fresh.StateHash(), used.StateHash(), $"{sp.Id}: hash after Reset equals a fresh match");
		}
	}

	// F3: the new state is in the hash (a launched and an unlaunched fighter in the same spot differ), and the same
	// inputs twice give the same trace.
	[Test]
	public static void Hash_SameInputsSameTrace_AndLaunchedDiffersFromNot()
	{
		ulong[] Trace(SpecialData sp, int level)
		{
			var m = Setup(sp, level);
			var h = new List<ulong>();
			m.Step(FighterInput.Special(SpecialSlot.C), FighterInput.None); h.Add(m.StateHash());
			for (int i = 0; i < 70; i++) { Idle(m, 1); h.Add(m.StateHash()); }
			return h.ToArray();
		}
		foreach (var sp in new[] { Launcher, Charger, Spinner })
			Assert.True(Trace(sp, 3).SequenceEqual(Trace(sp, 3)), $"{sp.Id}: same trace twice");
		Assert.True(!Trace(Launcher, 3).SequenceEqual(Trace(Launcher, 2)), "Lv 3 launch differs from Lv 2 (no launch)");
		Assert.True(!Trace(Spinner, 3).SequenceEqual(Trace(Spinner, 2)), "Lv 3 multi-hit differs from Lv 2");
		// HitsLanded alone changes the hash (a state that is not hashed would break replay checks, F3).
		var a = Setup(Spinner, 3); var b = Setup(Spinner, 3);
		a.P1.HitsLanded = 1; a.P1.LastHitFrame = 4;
		Assert.True(a.StateHash() != b.StateHash(), "HitsLanded is hashed");
		var c = Setup(Spinner, 3); c.P2.Launched = true;
		Assert.True(c.StateHash() != b.StateHash(), "Launched is hashed");
	}

	// A5 multi-hit blocked partway: block the first two connects, let go, the third is a hit: 3 connects, [blocked, blocked, hit].
	[Test]
	public static void MultiHit_BlockedThenReleased_LaterConnectsHit()
	{
		var m = Setup(Spinner, 3);
		var seq = new List<bool>();
		m.Hit += (_, e) => { if (e.Attacker == 0) seq.Add(e.Blocked); };
		var back = new FighterInput(InputBits.Right); // P2 faces left: back is right
		m.Step(FighterInput.Special(SpecialSlot.C), back);
		for (int t = 2; t <= 30; t++) m.Step(FighterInput.None, seq.Count < 2 ? back : FighterInput.None);
		Assert.Equal("True,True,False", string.Join(",", seq), "blocked, blocked, then released: the last connect hits");
	}

	// A5: after the spin ends, a second use of the special gets its full count again (nothing carried over).
	[Test]
	public static void MultiHit_SecondUseGetsItsFullCountAgain()
	{
		var m = Setup(Spinner, 3);
		int hits = 0;
		m.Hit += (_, e) => { if (e.Attacker == 0) hits++; };
		m.Step(FighterInput.Special(SpecialSlot.C), FighterInput.None);
		int guard = 0;
		while ((hits < 3 || !m.P1.Actionable) && guard++ < 120) Idle(m, 1);
		Assert.Equal(3, hits, "first use: 3");
		guard = 0;
		while ((!m.P2.Actionable || m.P2.State == FighterState.Knockdown) && guard++ < 200) Idle(m, 1);
		hits = 0;
		m.Step(FighterInput.Special(SpecialSlot.C), FighterInput.None);
		Idle(m, 40);
		Assert.Equal(3, hits, "second use: 3 again");
	}

	// A5 launch vs armour: Iron-Club Charge's armour (data) absorbs a launching hit: no launch, the move plays on.
	[Test]
	public static void Launch_AgainstArmour_DoesNotLaunch_TheArmouredMovePlaysOn()
	{
		var charge = SpecialLoader.LoadFile($"{Repo}/data/moves/iron-club-charge.json");
		var m = new Match(NoHitstop, new[] { Poke() }, new[] { Poke() });
		m.P1.Equip(SpecialSlot.C, Launcher, 3);
		m.P2.Equip(charge.Slot, charge, 1);
		m.P1.X = -50 * S; m.P2.X = 50 * S;
		// P2 starts its charge; the armour begins on move frame 5 and lasts through the active frames.
		m.Step(FighterInput.None, FighterInput.Special(charge.Slot));
		Idle(m, 4);
		Assert.True(m.P2.State == FighterState.Attack && m.P2.MoveFrame >= 5, $"setup: P2 is inside its armour ({m.P2.State} frame {m.P2.MoveFrame})");
		int hp = m.P2.Health;
		m.Step(FighterInput.Special(SpecialSlot.C), FighterInput.None);
		Idle(m, 3);
		Assert.True(m.P2.Health < hp, "the launcher's damage landed");
		Assert.True(!m.P2.Launched && !m.P2.Airborne, "armour: no launch");
		Assert.True(m.P2.State == FighterState.Attack, $"armour: the move plays on ({m.P2.State})");
	}

	// Wall bounce: only the corner triggers it; away from the corner the same hit is a plain hit.
	[Test]
	public static void WallBounce_AwayFromTheWall_IsAPlainHit_AtTheWall_Bounces_AndNeverLeavesTheStage()
	{
		var mid = Setup(Charger, 3);
		mid.Step(FighterInput.Special(SpecialSlot.C), FighterInput.None); Idle(mid, 3);
		Assert.True(!mid.P2.Launched && !mid.P2.Airborne, "mid-stage: a plain hit");

		var probe = new Match(NoHitstop);
		int wall = probe.CornerHi(probe.P2) / S;
		var m = Setup(Charger, 3, wall - 60, wall);
		m.Step(FighterInput.Special(SpecialSlot.C), FighterInput.None); Idle(m, 3);
		Assert.True(m.P2.Launched, "at the wall: bounced");
		for (int i = 0; i < 60; i++)
		{
			Idle(m, 1);
			Assert.True(m.P2.X >= m.CornerLo(m.P2) && m.P2.X <= m.CornerHi(m.P2), $"tick {i}: stays on the stage ({m.P2.X})");
		}
		Assert.True(!m.P2.Launched && !m.P2.Airborne, "landed");
	}

	// T4: the Tanuki's copy of every shipped special never gets the Lv 3 behaviour (plain or EX).
	[Test]
	public static void TanukiCopyOfAnyShippedSpecialAtLv3_NeverLaunchesBouncesOrMultiHits()
	{
		var specials = SpecialLoader.LoadDirectory($"{Repo}/data/moves").Where(s => s.MaxLevel == 3).ToArray();
		Assert.True(specials.Length > 0, "setup");
		int reaction = 0;
		foreach (var sp in specials)
		{
			var lv3 = sp.Build(3, false);
			if (lv3.Launch || lv3.WallBounce || lv3.MultiHit is not null) reaction++;
			var kit = TanukiKit.Load(FightScene.Sources, new CopyTarget(sp.Slot, sp, 3));
			var m = new Match(null, kit.Moves, kit.Moves);
			kit.Equip(m.P2);
			var copied = m.P2.Specials[(int)sp.Slot]!;
			foreach (var mv in new[] { copied.Move, copied.ExMove }.Where(x => x is not null))
				Assert.True(!mv!.Launch && !mv.WallBounce && mv.MultiHit is null, $"T4: the copy of {sp.Id} has no Lv 3 reaction");
		}
		Assert.True(reaction >= 3, $"setup: Heaven Seal, Crushing Charge and Torrent Spin exist at Lv 3 ({reaction})");
	}

	// A5/F3: the shipped Lv 3 specials, played from random inputs against a random opponent, are deterministic and
	// never leave a fighter airborne without an owner state (Launched only while airborne).
	[Test]
	public static void ShippedReactionSpecials_RandomFights_AreDeterministic_AndLaunchedImpliesAirborne()
	{
		var shipped = new[] { "rising-talisman", "iron-club-charge", "shell-spin" }
			.Select(id => SpecialLoader.LoadFile($"{Repo}/data/moves/{id}.json")).ToArray();
		ulong Run(uint seed, bool lv3)
		{
			var m = new Match(null, new[] { Poke() }, new[] { Poke() });
			foreach (var sp in shipped) { m.P1.Equip(sp.Slot, sp, lv3 ? 3 : 2); m.P2.Equip(sp.Slot, sp, 3); }
			m.P1.X = -60 * S; m.P2.X = 60 * S;
			var rng = new SimRng(seed);
			ulong h = 0;
			for (int t = 0; t < 2000 && m.Phase != MatchPhase.Over; t++)
			{
				FighterInput In() => rng.Next(5) == 0
					? FighterInput.Special(shipped[rng.Next(shipped.Length)].Slot, rng.Next(2) == 0)
					: new FighterInput((InputBits)(rng.NextUInt() & (uint)(InputBits.Directions | InputBits.Attacks)));
				m.Step(In(), In());
				foreach (var f in m.Fighters) Assert.True(!f.Launched || f.Airborne, $"seed {seed} tick {t}: Launched but on the ground");
				h = h * 31 + m.StateHash();
			}
			return h;
		}
		for (uint seed = 1; seed <= 6; seed++)
		{
			Assert.Equal(Run(seed, true), Run(seed, true), $"seed {seed}: same trace twice");
		}
		Assert.True(Run(1, true) != Run(2, true), "a different seed gives a different trace");
	}
}
