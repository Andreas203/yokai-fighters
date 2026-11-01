using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-172 (AM12) breaker probes. Expected values come from AM12 / rules.md E12, E17 and the modifier table:
/// Slippery Skin breaks a grab that connects during the carrying move's startup (clash, no damage, like throw vs
/// throw); not throw-invulnerability; only that move's startup. Fixture talisman (slot B, startup 5, active 6-13),
/// fixture throw (grab on frame 6, on_break pushback 60); a throw pressed on tick t grabs on tick t+5.
/// </summary>
public static class SlipperySkinBreakerProbes
{
	const int S = SimConfig.Scale;
	const InputBits THROW = InputBits.LightPunch | InputBits.LightKick;
	static readonly FighterInput Idle = FighterInput.None;
	static string Repo => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static ModifierData Skin() => ModifierLoader.LoadFile($"{Repo}/data/modifiers/slippery-skin.json");
	static SpecialData[] Fixtures() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));
	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();
	static FighterInput Throw => new(THROW);
	static FighterInput Talisman => FighterInput.Special(SpecialSlot.B);
	static Func<int, FighterInput> At(int tick, FighterInput i) => t => t == tick ? i : Idle;

	/// <summary>skinOn: fighter index wearing the skin on <paramref name="slot"/>, or -1 for nobody.</summary>
	static Match Setup(List<ThrowEvent> log, int skinOn, SpecialSlot slot = SpecialSlot.B, int level = 1)
	{
		var m = new Match(null, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Fixtures());
		if (skinOn >= 0)
			m.Fighters[skinOn].Equip(slot, m.Fighters[skinOn].Specials[(int)slot]!.Data, level, Skin(), m.Config);
		m.P1.X = -m.Config.BodyWidth / 2;
		m.P2.X = m.Config.BodyWidth / 2;
		m.Throw += (_, e) => log.Add(e);
		return m;
	}

	static void Run(Match m, int ticks, Func<int, FighterInput> p1, Func<int, FighterInput> p2)
	{
		for (int t = 1; t <= ticks; t++) m.Step(p1(t), p2(t));
	}

	// AM12 / E12: the slip works for either side. Here P2 throws and P1 carries the skin.
	[Test]
	public static void MirrorSide_P2ThrowsIntoP1sSkinnedStartup_Clashes()
	{
		var log = new List<ThrowEvent>();
		var m = Setup(log, skinOn: 0);
		Run(m, 6, At(3, Talisman), At(1, Throw));
		Assert.Equal(1, log.Count, "one throw event");
		Assert.Equal((ThrowOutcome.Clash, 0), (log[0].Outcome, log[0].Damage), "clash, no damage");
		Assert.Equal((1000, 1000), (m.P1.Health, m.P2.Health), "no damage");
		Assert.Equal(FighterState.Idle, m.P1.State, "carrier freed");
		Assert.Equal(FighterState.Idle, m.P2.State, "thrower freed");
	}

	// AM12: only "this move's startup": the skin on slot A does not protect slot B's startup.
	[Test]
	public static void SkinOnAnotherSlot_DoesNotSlipTheOtherSlotsStartup()
	{
		var log = new List<ThrowEvent>();
		var m = Setup(log, skinOn: 1, slot: SpecialSlot.A);
		Run(m, 8, At(1, Throw), At(3, Talisman));
		Assert.Equal(ThrowOutcome.Grab, log[0].Outcome, "plain talisman startup is grabbed although slot A wears the skin");
	}

	// AM12: the modifier is on the special; its EX version is that special too (E16).
	[Test]
	public static void ExVersionOfSkinnedSpecial_AlsoSlipsInStartup()
	{
		var log = new List<ThrowEvent>();
		var m = Setup(log, skinOn: 1);
		m.P2.Meter = 1000;
		Run(m, 6, At(1, Throw), At(3, FighterInput.Special(SpecialSlot.B, ex: true)));
		Assert.True(m.P2.Specials[(int)SpecialSlot.B]!.ExMove!.ThrowBreakStartup, "EX move carries the property");
		Assert.Equal(ThrowOutcome.Clash, log.First().Outcome, "EX startup slips too");
	}

	// AM12: the modifier holds at every special level (A3: levels 1-3).
	[Test]
	public static void SlipsAtEveryLevel_1To3()
	{
		for (int lv = 1; lv <= 3; lv++)
		{
			var log = new List<ThrowEvent>();
			var m = Setup(log, skinOn: 1, level: lv);
			Run(m, 6, At(1, Throw), At(3, Talisman));
			Assert.Equal(ThrowOutcome.Clash, log.Single().Outcome, $"level {lv}: slip");
		}
	}

	// AM12 + A2: levelling up after attaching keeps the modifier's property (LevelUp rebuilds from data).
	[Test]
	public static void LevelUpAfterAttach_StillSlips()
	{
		var log = new List<ThrowEvent>();
		var m = Setup(log, skinOn: 1);
		m.P2.LevelUp(SpecialSlot.B);
		m.P2.LevelUp(SpecialSlot.B);
		Run(m, 6, At(1, Throw), At(3, Talisman));
		Assert.Equal(ThrowOutcome.Clash, log.Single().Outcome, "Lv 3 after attach still slips");
	}

	// E17: not invulnerability, so a throw that does not reach the carrier is no event at all.
	[Test]
	public static void ThrowOutOfRange_AgainstSkinnedStartup_RaisesNoEvent()
	{
		var log = new List<ThrowEvent>();
		var m = Setup(log, skinOn: 1);
		m.P2.X = m.P1.X + 600 * S;
		Run(m, 8, At(1, Throw), At(3, Talisman));
		Assert.Equal(0, log.Count, "a whiff is not a clash");
		Assert.Equal(1000, m.P2.Health, "no damage");
	}

	// E12: after a slip clash the same as throw vs throw, also in the corner (pushback handed over, same end state).
	[Test]
	public static void SlipClashInCorner_MatchesThrowVsThrowClashInCorner()
	{
		var la = new List<ThrowEvent>();
		var slip = Setup(la, skinOn: 1);
		slip.P2.X = slip.Config.StageHalfWidth * S; slip.P1.X = slip.P2.X - slip.Config.BodyWidth;
		Run(slip, 6, At(1, Throw), At(3, Talisman));
		var lb = new List<ThrowEvent>();
		var both = Setup(lb, skinOn: -1);
		both.P2.X = both.Config.StageHalfWidth * S; both.P1.X = both.P2.X - both.Config.BodyWidth;
		Run(both, 6, At(1, Throw), At(1, Throw));
		Assert.Equal(ThrowOutcome.Clash, la.Single().Outcome, "slip clash");
		Assert.Equal(ThrowOutcome.Clash, lb.Single().Outcome, "control clash");
		Assert.Equal((both.P1.X, both.P2.X), (slip.P1.X, slip.P2.X), "same positions out of the corner");
	}

	// Determinism: same inputs twice give the same StateHash; skin vs no skin on the same inputs differ (grab vs clash).
	[Test]
	public static void SameInputs_SameHash_SkinChangesIt()
	{
		ulong H(int skin)
		{
			var m = Setup(new List<ThrowEvent>(), skin);
			Run(m, 30, At(1, Throw), At(3, Talisman));
			return m.StateHash();
		}
		Assert.True(H(1) == H(1), "same run, same hash");
		Assert.True(H(1) != H(-1), "slip and grab end differently");
	}

	// AM12 / E12: the slipped carrier is free at once: the talisman does not keep playing, no hit lands afterwards.
	[Test]
	public static void AfterSlip_CarrierMoveEnded_NoLateHit()
	{
		var log = new List<ThrowEvent>();
		var m = Setup(log, skinOn: 1);
		var hits = 0;
		m.Hit += (_, _) => hits++;
		Run(m, 80, At(1, Throw), At(3, Talisman));
		Assert.Equal(0, hits, "no strike lands after the clash");
		Assert.True(m.P2.CurrentMove is null, "carrier has no move in progress");
	}
}
