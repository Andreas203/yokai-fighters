using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-172 (AM12): Slippery Skin breaks a throw that connects during the carrying move's startup: a clash, no damage,
/// both freed and pushed apart, like throw vs throw (E12). Not throw-invulnerability (E17). Uses the real
/// data/modifiers/slippery-skin.json on the TEST FIXTURE Rising Talisman (slot B, startup 5, active 6-13, 41 frames)
/// and the TEST FIXTURE throw (LP+LK, grab on frame 6, on_break pushback 60). P1 throws, P2 carries the modifier.
/// A throw pressed on tick t grabs on tick t+5; a special requested on tick t plays frame n on tick t+n-1.
/// </summary>
public static class SlipperySkinTests
{
	const int S = SimConfig.Scale;
	const InputBits THROW = InputBits.LightPunch | InputBits.LightKick;
	const int Push = 60; // the fixture throw's pushback.on_break
	static readonly FighterInput Idle = FighterInput.None;
	static string Repo => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static ModifierData Skin() => ModifierLoader.LoadFile($"{Repo}/data/modifiers/slippery-skin.json");
	static SpecialData[] Fixtures() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));
	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();

	sealed class Log
	{
		public readonly List<(int tick, ThrowEvent e)> Throws = new();
		public readonly List<(int tick, HitEvent e)> Hits = new();
	}

	/// <summary>Touching mid-stage; P2 wears Slippery Skin on the talisman when <paramref name="skin"/>.</summary>
	static Match Setup(Log log, bool skin)
	{
		var m = new Match(null, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Fixtures());
		if (skin) m.P2.AttachModifier(SpecialSlot.B, Skin(), m.Config);
		m.P1.X = -m.Config.BodyWidth / 2;
		m.P2.X = m.Config.BodyWidth / 2;
		m.Throw += (mm, e) => log.Throws.Add((mm.Tick, e));
		m.Hit += (mm, e) => log.Hits.Add((mm.Tick, e));
		return m;
	}

	static void Run(Match m, int ticks, Func<int, FighterInput> p1, Func<int, FighterInput> p2)
	{
		for (int t = 1; t <= ticks; t++) m.Step(p1(t), p2(t));
	}

	static Func<int, FighterInput> At(int tick, FighterInput input) => t => t == tick ? input : Idle;
	static FighterInput Throw => new(THROW);
	static FighterInput Talisman => FighterInput.Special(SpecialSlot.B);

	/// <summary>P1 throws on tick 1 (grab tick 6); P2 starts the talisman on <paramref name="talismanTick"/>.</summary>
	static (Match m, Log log, int gapBefore) ThrowIntoTalisman(int talismanTick, bool skin, int ticks = 6)
	{
		var log = new Log();
		var m = Setup(log, skin);
		int gap = m.P2.X - m.P1.X;
		Run(m, ticks, At(1, Throw), At(talismanTick, Talisman));
		return (m, log, gap);
	}

	[Test]
	public static void DataSaysThrowBreakDuringStartup()
	{
		var mod = Skin();
		var m = Setup(new Log(), skin: true);
		Assert.True(m.P2.Specials[(int)SpecialSlot.B]!.Move.ThrowBreakStartup, "the talisman wearing Slippery Skin reads throw_break");
		Assert.True(!m.P1.Specials[(int)SpecialSlot.B]!.Move.ThrowBreakStartup, "the plain talisman does not");
		Assert.True(mod.AppliesTo(Fixtures().First(s => s.Slot == SpecialSlot.B)), "applies to any special");
	}

	[Test]
	public static void ThrowInStartup_Clashes_NoDamage_BothFreedAndPushedApart()
	{
		foreach (int start in new[] { 2, 3, 6 }) // talisman frame 5 (last startup), 4 and 1 on the grab tick
		{
			var (m, log, gap) = ThrowIntoTalisman(start, skin: true);
			var e = log.Throws.Single();
			Assert.Equal((6, ThrowOutcome.Clash, 0, 0), (e.tick, e.e.Outcome, e.e.Attacker, e.e.Damage), $"talisman from tick {start}: a clash on the grab tick");
			Assert.Equal(FighterState.Idle, m.P1.State, "the thrower is freed");
			Assert.Equal(FighterState.Idle, m.P2.State, "the carrier is freed: the talisman ends, as in a throw-vs-throw clash");
			Assert.Equal(gap + 2 * Push * S, m.P2.X - m.P1.X, "both slid apart by the throw's on_break pushback");
			Run(m, 60, _ => Idle, _ => Idle);
			Assert.True(log.Hits.Count == 0, "no damage, no strike lands");
			Assert.Equal((1000, 1000), (m.P1.Health, m.P2.Health), "no damage either side");
			Assert.Equal(1, log.Throws.Count, "no later grab or land");
		}
	}

	[Test]
	public static void SlipClashMatchesThrowVsThrowClash()
	{
		var (slip, _, _) = ThrowIntoTalisman(3, skin: true);
		var log = new Log();
		var both = Setup(log, skin: false);
		Run(both, 6, At(1, Throw), At(1, Throw));
		Assert.Equal(ThrowOutcome.Clash, log.Throws.Single().e.Outcome, "control: same-frame throws clash");
		Assert.Equal((both.P1.X, both.P2.X, both.P1.State, both.P2.State, both.P1.Meter, both.P2.Meter),
			(slip.P1.X, slip.P2.X, slip.P1.State, slip.P2.State, slip.P1.Meter, slip.P2.Meter),
			"the slip ends exactly where a throw-vs-throw clash ends");
	}

	[Test]
	public static void ThrowInActiveFrames_GrabsAsUsual()
	{
		var (m, log, _) = ThrowIntoTalisman(1, skin: true, ticks: 20); // grab tick 6 = talisman frame 6, its first active
		Assert.Equal(ThrowOutcome.Grab, log.Throws[0].e.Outcome, "first active frame: the grab wins (E12), no slip");
		Assert.True(log.Throws.Any(t => t.e.Outcome == ThrowOutcome.Land && t.e.Damage == 120), "and lands for full damage");
		Assert.True(m.P2.Health < 1000, "damage taken");
	}

	[Test]
	public static void ThrowInRecovery_GrabsAsUsual()
	{
		var log = new Log();
		var m = Setup(log, skin: true);
		int p1x = m.P1.X;
		m.P1.X = p1x - 400 * S; // out of the talisman's reach while it is active
		Run(m, 15, _ => Idle, At(1, Talisman));
		Assert.True(m.P2.State == FighterState.Attack && m.P2.MoveFrame == 15, "talisman in recovery (frame 15)");
		m.P1.X = m.P2.X - m.Config.BodyWidth; // walk-in skipped: touching again
		Run(m, 20, At(1, Throw), _ => Idle); // grab on talisman frame 20
		Assert.Equal(ThrowOutcome.Grab, log.Throws[0].e.Outcome, "recovery: a normal grab");
		Assert.True(log.Throws.Any(t => t.e.Outcome == ThrowOutcome.Land), "and it lands");
	}

	[Test]
	public static void WithoutTheModifier_ThrowInStartupGrabs()
	{
		var (_, log, _) = ThrowIntoTalisman(3, skin: false, ticks: 20);
		Assert.Equal(ThrowOutcome.Grab, log.Throws[0].e.Outcome, "a plain talisman's startup is grabbed");
		Assert.True(log.Throws.Any(t => t.e.Outcome == ThrowOutcome.Land), "and the throw lands");
	}

	[Test]
	public static void SlipIsNotThrowInvulnerability()
	{
		var log = new Log();
		var m = Setup(log, skin: true);
		Run(m, 2, _ => Idle, At(1, Talisman));
		Assert.True(Match.SlipsThrows(m.P2), "in startup: slips throws");
		Assert.True(Match.Throwable(m.P2), "E17: still throwable, the throw has to connect to be broken");
		Run(m, 4, _ => Idle, _ => Idle);
		Assert.True(!Match.SlipsThrows(m.P2), "frame 6 is active: no slip");
	}

	[Test]
	public static void ElderUnbreakableThrowStillClashes()
	{
		var grip = new ElderTwist("t-grip", "elder-kappa", TwistEffect.UnbreakableThrows, 0, 0, "twist.t", null, null, null);
		var log = new Log();
		var m = Setup(log, skin: true);
		m.P1.Twist = grip;
		Run(m, 6, At(1, Throw), At(3, Talisman));
		Assert.Equal(ThrowOutcome.Clash, log.Throws.Single().e.Outcome, "like a same-frame clash, the slip is not a break press (YOK-90)");
	}

	[Test]
	public static void TanukiCopyHasNoSlipperySkin_T4()
	{
		var src = KitSources.Repo(Repo).FixturesOnly();
		var run = RunState.NewRun(AbilityPool.LoadFixtures(Repo));
		run.Attach(SpecialSlot.B, Skin());
		run.LevelUp(SpecialSlot.B); // the most invested special: the copy target
		var copy = CopyRule.Target(run)!;
		Assert.Equal(SpecialSlot.B, copy.Slot, "the copy rule picks the talisman");
		var ryo = Kit.Load(Kit.Ryo, src);
		var kit = TanukiKit.Build(ryo, copy);
		var m = new Match(null, ryo.Moves, kit.Moves);
		ryo.Equip(m.P1);
		m.P1.Equip(SpecialSlot.B, copy.Data, run[SpecialSlot.B]!.Level, Skin(), m.Config);
		kit.Equip(m.P2);
		Assert.True(m.P1.Specials[(int)SpecialSlot.B]!.Move.ThrowBreakStartup, "Ryo's own talisman wears the skin");
		Assert.True(!m.P2.Specials[(int)SpecialSlot.B]!.Move.ThrowBreakStartup, "T4: the Tanuki's copy never gets his modifier");

		// In the fight: Ryo's throw catches the Tanuki's copied talisman in startup and grabs.
		var log = new Log();
		m.Throw += (mm, e) => log.Throws.Add((mm.Tick, e));
		m.P1.X = -m.Config.BodyWidth / 2;
		m.P2.X = m.Config.BodyWidth / 2;
		Run(m, 6, At(1, Throw), At(3, Talisman));
		Assert.Equal(ThrowOutcome.Grab, log.Throws.Single().e.Outcome, "the copy's startup is grabbed, not slipped");
	}
}
