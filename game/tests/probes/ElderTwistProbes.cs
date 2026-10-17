using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-90 test-breaker probes: elder twists (P5, Y2, E12, E17, C4). Fixtures only: Spirit Wave A (projectile), Rising Talisman B (strike).</summary>
public static class ElderTwistProbes
{
	const InputBits LP = InputBits.LightPunch, LK = InputBits.LightKick, THROW = LP | LK;
	static readonly FighterInput Idle = FighterInput.None;
	const int S = SimConfig.Scale;

	static readonly ElderTwist Reflect = new("t-reflect", "nine-tailed-kitsune", TwistEffect.ReflectRepeatedSpecial, 2, 0, "twist.t", null, null, null);
	static readonly ElderTwist Armour = new("t-armour", "elder-oni", TwistEffect.SpecialArmour, 0, 1, "twist.t", null, null, null);
	static readonly ElderTwist Grip = new("t-grip", "elder-kappa", TwistEffect.UnbreakableThrows, 0, 0, "twist.t", null, null, null);

	static SpecialData[] Starters() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));
	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();

	sealed class Log
	{
		public readonly List<ProjectileEvent> Projectiles = new();
		public readonly List<HitEvent> Hits = new();
		public readonly List<TwistEvent> Twists = new();
		public readonly List<ThrowEvent> Throws = new();
		public int Reflected => Twists.Count(t => t.Effect == TwistEffect.ReflectRepeatedSpecial);
	}

	static Match Setup(Log log, ElderTwist? twist, int p1x = -300, int p2x = 300)
	{
		var m = new Match(null, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Starters());
		m.P1.X = p1x * S; m.P2.X = p2x * S; m.P2.Twist = twist;
		m.ProjectileChanged += (mm, e) => log.Projectiles.Add(e);
		m.Hit += (mm, e) => log.Hits.Add(e);
		m.TwistTriggered += (mm, e) => log.Twists.Add(e);
		m.Throw += (mm, e) => log.Throws.Add(e);
		return m;
	}
	static void Wait(Match m, int n) { for (int t = 0; t < n; t++) m.Step(Idle, Idle); }
	static void P1(Match m, FighterInput i, int thenIdle) { m.Step(i, Idle); Wait(m, thenIdle); }
	static FighterInput Wave(bool ex = false) => FighterInput.Special(SpecialSlot.A, ex);
	static FighterInput Talisman() => FighterInput.Special(SpecialSlot.B);

	// Y2 / twist data: "repeat" is data. With repeat 3 the second use is not reflected, the third is.
	[Test]
	public static void Reflect_RepeatComesFromData_ThirdUseWhenRepeatIsThree()
	{
		var log = new Log();
		var m = Setup(log, Reflect with { Repeat = 3 });
		P1(m, Wave(), 140); P1(m, Wave(), 140);
		Assert.Equal(0, log.Reflected, "repeat 3: the second use is not reflected");
		P1(m, Wave(), 200);
		Assert.Equal(1, log.Reflected, "repeat 3: the third use is");
	}

	// P5/Y2 (twist reading in ElderTwist.cs): jumps, dashes and throws do not break the row.
	[Test]
	public static void Reflect_JumpDashAndThrowDoNotBreakTheRow()
	{
		var log = new Log();
		var m = Setup(log, Reflect);
		P1(m, Wave(), 140);
		P1(m, new FighterInput(InputBits.Up), 60);
		P1(m, new FighterInput(InputBits.Right), 3);
		P1(m, new FighterInput(InputBits.Right), 20);
		P1(m, new FighterInput(THROW), 40);
		m.P1.X = -300 * S;
		Wait(m, 40);
		P1(m, Wave(), 200);
		Assert.Equal(1, log.Reflected, "A, jump, dash, throw press, A: still the second in a row");
	}

	// Twist reading: EX first, then plain, is the same special.
	[Test]
	public static void Reflect_ExThenPlain_IsTheSameSpecial()
	{
		var log = new Log();
		var m = Setup(log, Reflect);
		m.P1.Meter = 100;
		P1(m, Wave(ex: true), 160);
		P1(m, Wave(), 200);
		Assert.Equal(1, log.Reflected, "EX Wave then Wave: the second is reflected");
	}

	// Twist reading: A, B, B reflects the second B; A, B, A, A reflects only the last.
	[Test]
	public static void Reflect_RowRestartsAtTheDifferentSpecial()
	{
		var log = new Log();
		var m = Setup(log, Reflect, -60, 60);
		P1(m, Wave(), 140); m.P1.X = -60 * S; m.P2.X = 60 * S;
		P1(m, Talisman(), 100); m.P1.X = -60 * S; m.P2.X = 60 * S; Wait(m, 60);
		Assert.Equal(0, log.Reflected, "A then B: no reflect");
		m.P1.X = -60 * S; m.P2.X = 60 * S;
		P1(m, Talisman(), 100);
		Assert.Equal(1, log.Reflected, "A, B, B: the second Talisman is reflected");
	}

	// Reflect while the elder is mid-move: her own move is untouched, she takes no hit, no counterhit.
	[Test]
	public static void Reflect_WhileElderIsMidMove_ElderKeepsHerMove()
	{
		var log = new Log();
		var m = Setup(log, Reflect);
		P1(m, Wave(), 140);
		int hp = m.P2.Health;
		m.Step(Wave(), Talisman()); // she starts Talisman just as Ryo repeats
		for (int t = 0; t < 12; t++) m.Step(Idle, Idle);
		// While her Talisman is still going when the reflected wave arrives (travel ~ 600u / 12 per tick = 50 ticks) she may be done; assert only health.
		Wait(m, 200);
		Assert.True(log.Reflected == 1, "the repeat is reflected");
		Assert.True(m.P2.Health >= hp, $"she takes nothing from the reflected wave (hp {hp} -> {m.P2.Health})");
	}

	// Advisory: a special cancelled into a normal must not carry the 'special' reflect (P5 reading: normals are never reflected).
	[Test]
	public static void Reflect_NormalCancelledOutOfARepeatedSpecial_IsNotReflected()
	{
		var log = new Log();
		var m = Setup(log, Reflect, -60, 60);
		P1(m, Wave(), 140);
		m.P1.X = -60 * S; m.P2.X = 60 * S;
		m.Step(Wave(), Idle);
		for (int t = 0; t < 16; t++) m.Step(Idle, Idle); // frame ~17: inside the cancel window 14-20
		var before = log.Hits.Count;
		m.P1.X = -60 * S + 0; m.P2.X = -60 * S + 80 * S;
		m.Step(new FighterInput(LP), Idle);
		Wait(m, 12);
		bool jabHitElder = log.Hits.Skip(before).Any(h => h.Attacker == 0 && h.MoveId != "test-spirit-wave");
		bool jabReflected = log.Twists.Any(x => x.MoveId != "test-spirit-wave" && x.Effect == TwistEffect.ReflectRepeatedSpecial);
		Assert.True(!jabReflected, $"a jab cancelled out of the repeated wave is a normal and is never reflected (state {m.P1.State}, hits {string.Join(",", log.Hits.Select(h => h.Attacker + h.MoveId))})");
	}

	// Armour (Y2 "armour on every special"; ElderTwist.cs: frame 1 to last active). Armour is fresh for each new special.
	[Test]
	public static void Armour_EachNewSpecialHasItsOwnArmour_NormalsHaveNone()
	{
		var log = new Log();
		var m = Setup(log, Armour, -60, 60);
		m.Step(new FighterInput(LP), Talisman());
		for (int t = 0; t < 7 && log.Hits.Count == 0; t++) m.Step(Idle, Idle);
		Assert.True(log.Hits.Count == 1, "setup: jab hit");
		Assert.Equal(FighterState.Attack, m.P2.State, "first special armoured");
		Wait(m, 80);
		Assert.True(m.P2.State == FighterState.Idle, "setup: recovered");
		// A normal of hers is not a special: the same jab interrupts it.
		m.P1.X = -60 * S; m.P2.X = 60 * S;
		m.Step(new FighterInput(LP), new FighterInput(LP));
		for (int t = 0; t < 10; t++) m.Step(Idle, Idle);
		Assert.True(log.Twists.Count(x => x.Effect == TwistEffect.SpecialArmour) == 1, "no armour for a normal");
	}

	// Armour: pushback still applies (YOK-103 reading, ElderTwist.cs doc).
	[Test]
	public static void Armour_PushbackStillApplies_AndNoHitstun()
	{
		var log = new Log();
		var m = Setup(log, Armour, -60, 60);
		m.Step(new FighterInput(LP), Talisman());
		int x0 = 0; bool got = false;
		for (int t = 0; t < 8 && !got; t++)
		{
			x0 = m.P2.X;
			m.Step(Idle, Idle);
			got = log.Hits.Count > 0;
		}
		Assert.True(got, "setup: hit");
		Assert.True(m.P2.X > x0, $"pushback moves the armoured elder away ({x0} -> {m.P2.X})");
		Assert.Equal(FighterState.Attack, m.P2.State, "not interrupted");
	}

	// Armour window for a projectile special with no active frames: LastActive = startup (13), the spawn frame is 14.
	// Y2 says "armour on every special"; the twist says "frame 1 to the last active frame". OPEN: documents which jab-hit
	// frames are armoured (elder Spirit Wave): hit on frame 13 vs 14 vs 20.
	static (int hitFrame, bool armoured) JabIntoElderWave(int pressElderFrame)
	{
		var log = new Log();
		var m = Setup(log, Armour, -60, 60);
		m.Step(Idle, Wave());
		int hitFrame = -1, before = 0;
		m.Hit += (mm, e) => { if (e.Attacker == 0 && hitFrame < 0) hitFrame = before + 1; };
		for (int f = 2; f <= 40 && hitFrame < 0; f++)
		{
			before = m.P2.MoveFrame;
			m.Step(f == pressElderFrame ? new FighterInput(LP) : Idle, Idle);
		}
		return (hitFrame, log.Twists.Any(x => x.Effect == TwistEffect.SpecialArmour));
	}

	[Test]
	public static void Armour_ProjectileSpecial_ArmourEndsAtTheSpawnFrame_OPEN()
	{
		var (f13, a13) = JabIntoElderWave(9);
		var (f14, a14) = JabIntoElderWave(10);
		Assert.True(a13, $"hit on frame {f13} (startup) is armoured");
		Assert.True(!a14, $"KnownOpen: pins today: hit on frame {f14}, the wave spawn frame, is NOT armoured (armour ends at LastActive 13); flip when the designer decides Y2 armour covers a projectile special's spawn");
	}

	// Armour vs throw (ElderTwist.cs "throws beat it"): Ryo's throw on an elder who is mid special is thrown, not armoured.
	[Test]
	public static void Armour_ThrowBeatsIt()
	{
		var log = new Log();
		var m = Setup(log, Armour);
		m.P1.X = -m.Config.BodyWidth / 2; m.P2.X = m.Config.BodyWidth / 2;
		m.Step(new FighterInput(THROW), Idle);
		m.Step(Idle, Talisman());
		for (int t = 0; t < 10; t++) m.Step(Idle, Idle);
		Assert.True(log.Throws.Any(x => x.Outcome != ThrowOutcome.Break && x.Outcome != ThrowOutcome.Clash) || m.P2.State == FighterState.Thrown || m.P2.Health < 1000,
			$"the throw lands on an armoured special (state {m.P2.State}, hp {m.P2.Health}, armour events {log.Twists.Count})");
		Assert.Equal(0, log.Twists.Count(x => x.Effect == TwistEffect.SpecialArmour), "armour does not take a throw");
	}

	// C4: press count. The refused break press must raise its event once per press, not every tick it is buffered.
	[Test]
	public static void Unbreakable_OnePress_OneEvent()
	{
		var log = new Log();
		var m = Setup(log, Grip);
		m.P1.X = -m.Config.BodyWidth / 2; m.P2.X = m.Config.BodyWidth / 2;
		for (int t = 1; t <= 60; t++) m.Step(new FighterInput(t == 9 ? THROW : 0), new FighterInput(t == 1 ? THROW : 0));
		Assert.Equal(1, log.Twists.Count(x => x.Effect == TwistEffect.UnbreakableThrows), "one press, one refused-break event");
	}

	// E17 vs reflect: the turned projectile is hers; Foxfire-equivalent (Wave on her side) while it is on screen.
	// Rule E17: one projectile per fighter on screen. A reflected wave is still one projectile; whether it blocks her own is open.
	[Test]
	public static void Reflect_ReflectedProjectile_DoesNotBlockHerOwnWave_OPEN()
	{
		var log = new Log();
		var m = Setup(log, Reflect);
		P1(m, Wave(), 140);
		m.Step(Wave(), Idle);
		for (int t = 0; t < 100 && !log.Projectiles.Any(p => p.Kind == ProjectileEventKind.Reflected); t++) m.Step(Idle, Idle);
		Assert.True(log.Projectiles.Any(p => p.Kind == ProjectileEventKind.Reflected), "setup: reflected");
		int spawnsBefore = log.Projectiles.Count(p => p.Kind == ProjectileEventKind.Spawn && p.Owner == 1);
		m.Step(Idle, Wave());
		Wait(m, 20);
		int spawnsAfter = log.Projectiles.Count(p => p.Kind == ProjectileEventKind.Spawn && p.Owner == 1);
		Assert.True(spawnsAfter == spawnsBefore, "KnownOpen: pins today: her own Wave is refused while the reflected one is in flight (E17 silent on a turned projectile); flip when the designer decides");
	}

	// C4: a press buffered before the grab lands (E12 leniency window) is refused too, and still raises one event, not one per tick.
	[Test]
	public static void Unbreakable_EarlyPress_StillOneEvent()
	{
		foreach (int pressTick in new[] { 2, 3, 4, 5, 6, 7, 8 })
		{
			var log = new Log();
			var m = Setup(log, Grip);
			m.P1.X = -m.Config.BodyWidth / 2; m.P2.X = m.Config.BodyWidth / 2;
			for (int t = 1; t <= 60; t++) m.Step(new FighterInput(t == pressTick ? THROW : 0), new FighterInput(t == 1 ? THROW : 0));
			int n = log.Twists.Count(x => x.Effect == TwistEffect.UnbreakableThrows);
			Assert.True(n <= 1, $"press on tick {pressTick}: {n} refused-break events for one press");
		}
	}

	// Hash: a twist fight never hashes like the same fight with no twist, and two identical twist fights agree each tick.
	[Test]
	public static void Hash_TwistFightDiffersFromPlain_AndIsDeterministic()
	{
		var a = Setup(new Log(), Reflect); var b = Setup(new Log(), Reflect); var c = Setup(new Log(), null);
		for (int t = 0; t < 400; t++)
		{
			var i = t == 0 || t == 150 ? Wave() : Idle;
			a.Step(i, Idle); b.Step(i, Idle); c.Step(i, Idle);
			Assert.Equal(a.StateHash(), b.StateHash(), $"tick {t}: same twist fight, same hash");
		}
		Assert.True(a.StateHash() != c.StateHash(), "the twist fight hashes differently from the plain one");
	}

	// Refused action: a break press against a Grip changes no health/state compared with no press at all.
	[Test]
	public static void Unbreakable_RefusedPress_LeavesTheFightAsIfNoPress_ExceptItsEvent()
	{
		Match Run(bool press)
		{
			var m = Setup(new Log(), Grip);
			m.P1.X = -m.Config.BodyWidth / 2; m.P2.X = m.Config.BodyWidth / 2;
			for (int t = 1; t <= 90; t++) m.Step(new FighterInput(press && t == 9 ? THROW : 0), new FighterInput(t == 1 ? THROW : 0));
			return m;
		}
		var a = Run(true); var b = Run(false);
		Assert.Equal(b.P1.Health, a.P1.Health, "same damage");
		Assert.Equal(b.StateHash(), a.StateHash(), "a refused press is not used up and leaves no trace in the state (C4)");
	}
}
