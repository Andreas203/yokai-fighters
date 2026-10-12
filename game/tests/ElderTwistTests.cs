using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Replay;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-90 (P5, Y2, Y3): elder twists as data (data/twists/), which fights get one (elder map nodes only, whoever
/// stands in), the three sim rules (reflect, special armour, unbreakable throws) and exact replay of a twist fight.
/// Sim fights use the TEST FIXTURE normals, throw and starters (Spirit Wave A = projectile, Rising Talisman B = strike).
/// </summary>
public static class ElderTwistTests
{
	const InputBits LP = InputBits.LightPunch, LK = InputBits.LightKick, THROW = LP | LK;
	static readonly FighterInput Idle = FighterInput.None;
	const int S = SimConfig.Scale;

	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static string MapDir => ContentPaths.Data("map");
	static ElderTwists DataTwists() => ElderTwists.LoadDir(ContentPaths.Data(ElderTwists.Folder));

	static readonly ElderTwist Reflect = new("t-reflect", "nine-tailed-kitsune", TwistEffect.ReflectRepeatedSpecial, 2, 0, "twist.t", null, null, null);
	static readonly ElderTwist Armour = new("t-armour", "elder-oni", TwistEffect.SpecialArmour, 0, 1, "twist.t", null, null, null);
	static readonly ElderTwist Grip = new("t-grip", "elder-kappa", TwistEffect.UnbreakableThrows, 0, 0, "twist.t", null, null, null);

	static SpecialData[] Starters() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));
	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();

	sealed class Log
	{
		public readonly List<(int tick, ProjectileEvent e)> Projectiles = new();
		public readonly List<(int tick, HitEvent e)> Hits = new();
		public readonly List<(int tick, TwistEvent e)> Twists = new();
		public readonly List<(int tick, ThrowEvent e)> Throws = new();
		public int Reflected => Twists.Count(t => t.e.Effect == TwistEffect.ReflectRepeatedSpecial);
	}

	static Match Setup(Log log, ElderTwist? p2Twist, int p1x = -300, int p2x = 300)
	{
		var m = new Match(null, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Starters());
		m.P1.X = p1x * S;
		m.P2.X = p2x * S;
		m.P2.Twist = p2Twist;
		m.ProjectileChanged += (mm, e) => log.Projectiles.Add((mm.Tick, e));
		m.Hit += (mm, e) => log.Hits.Add((mm.Tick, e));
		m.TwistTriggered += (mm, e) => log.Twists.Add((mm.Tick, e));
		m.Throw += (mm, e) => log.Throws.Add((mm.Tick, e));
		return m;
	}

	static void Idle_(Match m, int ticks) { for (int t = 0; t < ticks; t++) m.Step(Idle, Idle); }
	static void P1(Match m, FighterInput input, int thenIdle) { m.Step(input, Idle); Idle_(m, thenIdle); }
	static FighterInput Wave(bool ex = false) => FighterInput.Special(SpecialSlot.A, ex);
	static FighterInput Talisman() => FighterInput.Special(SpecialSlot.B);

	static bool Throws<T>(Action body) where T : Exception
	{
		try { body(); }
		catch (T) { return true; }
		return false;
	}

	// ---- Data: where the twists live and which fights get one ------------------------------------------

	[Test]
	public static void Twist_DataGivesEachElderOfTheMapOneTwist()
	{
		var twists = DataTwists();
		Assert.Equal(3, twists.All.Count, "three elders, three twists (Y2)");
		var byElder = twists.All.ToDictionary(t => t.Elder, t => t.Effect);
		Assert.Equal(TwistEffect.ReflectRepeatedSpecial, byElder["nine-tailed-kitsune"], "Nine-Tails reflects (Y2)");
		Assert.Equal(TwistEffect.SpecialArmour, byElder["elder-oni"], "Elder Oni: armour on every special (Y2)");
		Assert.Equal(TwistEffect.UnbreakableThrows, byElder["elder-kappa"], "Elder Kappa: throws can't be broken (Y2)");
		var graph = FileMapSource.Default(MapDir).Load();
		foreach (var n in graph.Nodes)
		{
			var t = twists.ForNode(n);
			Assert.Equal(n.Elder, t != null, $"{n.Id}: a twist exactly on elder nodes (Y3)");
			if (t != null) Assert.Equal(n.Yokai, t.Elder, $"{n.Id}: the node's own elder's twist");
		}
		var notElder = graph.Nodes.First(n => n.Elder) with { Elder = false };
		Assert.True(twists.ForNode(notElder) == null, "the elder's yokai on a node without the elder flag fights with no twist");
		foreach (var t in twists.All)
			Assert.True(t.LabelKey.StartsWith("twist.") && t.Icon != null, $"{t.Id}: a HUD label key and a map icon");
	}

	[Test]
	public static void Twist_LoaderRejectsWhatTheSchemaRejects()
	{
		const string ok = """{"kind":"elder_twist","id":"x","status":"proposed","elder":"elder-oni","effect":{"type":"special_armour","hits":1},"label_key":"twist.x","rules":["Y2"]}""";
		Assert.Equal(1, ElderTwistLoader.Parse(ok, "x").ArmourHits, "a good file loads");
		Assert.True(Throws<FormatException>(() => ElderTwistLoader.Parse(ok, "y")), "id must equal the file name");
		Assert.True(Throws<FormatException>(() => ElderTwistLoader.Parse(ok.Replace("special_armour", "teleports"))), "unknown effect");
		Assert.True(Throws<FormatException>(() => ElderTwistLoader.Parse(ok.Replace("\"hits\":1", "\"hits\":0"))), "armour takes at least one hit");
		Assert.True(Throws<FormatException>(() => ElderTwistLoader.Parse(ok.Replace("\"hits\":1", "\"repeat\":2"))), "a setting of another effect");
		Assert.True(Throws<FormatException>(() => ElderTwistLoader.Parse(ok.Replace("special_armour\",\"hits\":1", "reflect_repeated_special\",\"repeat\":1"))), "reflect needs repeat 2 or more");
		Assert.True(Throws<FormatException>(() => ElderTwistLoader.Parse(ok.Replace("\"label_key\":\"twist.x\",", ""))), "label key required");
		var a = ElderTwistLoader.Parse(ok);
		Assert.True(Throws<FormatException>(() => new ElderTwists(new[] { a, a with { Id = "z" } })), "two twists for one elder (Y2)");
	}

	[Test]
	public static void Twist_StandInFighterTakesTheNodesElderTwist()
	{
		var twists = DataTwists();
		var table = NodeSceneTable.Default(MapDir);
		var graph = FileMapSource.Default(MapDir).Load();
		foreach (var n in graph.Nodes)
		{
			var ctx = table.Resolve(n).Context;
			var m = new Match(null, Moves(), Moves());
			m.P1.Twist = Grip; // a stale twist never survives into the next fight
			var applied = twists.Apply(m, ctx);
			Assert.True(m.P1.Twist == null, $"{n.Id}: Ryo never holds a twist");
			Assert.True(m.P2.Twist == applied && applied == twists.ForNode(n), $"{n.Id}: P2 holds the node's twist");
			if (n.Elder) Assert.True(applied != null && applied.Elder == n.Yokai, $"{n.Id}: elder twist applies to fighter '{ctx.Fighter}' (stand-in {ctx.StandIn})");
		}
	}

	// ---- Reflect (Nine-Tails) ---------------------------------------------------------------------------

	[Test]
	public static void Twist_Reflect_SecondProjectileInARowTurnsAround()
	{
		var log = new Log();
		var m = Setup(log, Reflect);
		P1(m, Wave(), 140);
		Assert.Equal(940, m.P2.Health, "the first Spirit Wave hits her (60)");
		Assert.Equal(0, log.Reflected, "a first use is never reflected");
		P1(m, Wave(), 200);
		Assert.Equal(1, log.Reflected, "the second in a row is reflected");
		Assert.True(log.Projectiles.Any(p => p.e.Kind == ProjectileEventKind.Reflected && p.e.Owner == 1), "the projectile is hers now");
		Assert.Equal(940, m.P2.Health, "the reflected wave does not hurt her");
		Assert.Equal(940, m.P1.Health, "it comes back and hits Ryo (60)");
		Assert.True(log.Hits.Last().e.Attacker == 1, "the last hit is hers");

		var plain = new Log();
		var c = Setup(plain, null);
		P1(c, Wave(), 140);
		P1(c, Wave(), 200);
		Assert.Equal(880, c.P2.Health, "control: without the twist both waves hit");
		Assert.Equal(1000, c.P1.Health, "control: Ryo untouched");
	}

	[Test]
	public static void Twist_Reflect_RowIsBrokenOnlyByADifferentSpecial()
	{
		int Reflections(Action<Match> play)
		{
			var log = new Log();
			var m = Setup(log, Reflect);
			play(m);
			return log.Reflected;
		}
		Assert.Equal(0, Reflections(m => { P1(m, Wave(), 140); P1(m, Talisman(), 80); P1(m, Wave(), 200); }), "A, B, A: no repeat");
		Assert.Equal(1, Reflections(m => { P1(m, Wave(), 140); P1(m, new FighterInput(LP), 40); P1(m, Wave(), 200); }), "A, jab, A: a normal does not break the row");
		Assert.Equal(1, Reflections(m => { P1(m, Wave(), 140); m.P1.Meter = 100; P1(m, Wave(ex: true), 200); }), "A then EX A: the same special");
		Assert.Equal(2, Reflections(m => { P1(m, Wave(), 140); P1(m, Wave(), 200); P1(m, Wave(), 200); }), "A, A, A: every use after the first");
		Assert.Equal(0, Reflections(m => { P1(m, Wave(), 140); m.Reset(); P1(m, Wave(), 200); }), "a new fight starts a new row");
	}

	[Test]
	public static void Twist_Reflect_RepeatedStrikeLandsOnItsUser()
	{
		var log = new Log();
		var m = Setup(log, Reflect, -60, 60);
		P1(m, Talisman(), 150);
		Assert.Equal(910, m.P2.Health, "the first Rising Talisman hits her (90)");
		m.P1.X = -60 * S; m.P2.X = 60 * S;
		int hitTick = -1;
		m.TwistTriggered += (mm, _) => hitTick = mm.Tick;
		m.Step(Talisman(), Idle);
		for (int t = 0; t < 20 && hitTick < 0; t++) m.Step(Idle, Idle);
		Assert.True(hitTick > 0, "the second talisman is reflected when it would connect");
		Assert.Equal(910, m.P2.Health, "she takes nothing");
		Assert.Equal(910, m.P1.Health, "Ryo takes his own 90");
		Assert.True(m.P1.State is FighterState.Hitstun or FighterState.Knockdown, $"Ryo's special is interrupted ({m.P1.State})");
		Assert.True(log.Hits.Last().e is { Attacker: 1, MoveId: "test-rising-talisman" }, "the hit is his move, as hers");
	}

	[Test]
	public static void Twist_Reflect_OnlyTheOpponentsSpecialsAndOnlyWithTheTwist()
	{
		var log = new Log();
		var m = Setup(log, Reflect);
		m.Step(Idle, Wave());
		Idle_(m, 140);
		m.Step(Idle, Wave());
		Idle_(m, 200);
		Assert.Equal(0, log.Reflected, "the elder's own repeats are never reflected");
		Assert.Equal(880, m.P1.Health, "both of her waves hit Ryo");
		Assert.Equal(0, m.P1.ActiveRepeat + m.P2.ActiveRepeat, "nothing counted outside a move");
	}

	// ---- Armour (Elder Oni) ---------------------------------------------------------------------------

	/// <summary>Tick on which Ryo's Spirit Wave (fired on tick 1) hits an idle P2 at 600 units.</summary>
	static int WaveArrival()
	{
		var log = new Log();
		var m = Setup(log, null);
		P1(m, Wave(), 120);
		return log.Hits.Single().tick;
	}

	/// <summary>P2 starts Rising Talisman so Ryo's wave arrives on its move frame <paramref name="frame"/>; P2's state right after.</summary>
	static (FighterState state, int health, int frame, int armoured) WaveIntoTalisman(ElderTwist? twist, int frame)
	{
		int arrive = WaveArrival(), start = arrive - frame + 1;
		var log = new Log();
		var m = Setup(log, twist);
		int hitFrame = -1;
		m.Hit += (mm, e) => { if (e.Attacker == 0) hitFrame = mm.P2.MoveFrame; };
		for (int t = 1; t <= arrive; t++) m.Step(t == 1 ? Wave() : Idle, t == start ? Talisman() : Idle);
		return (m.P2.State, m.P2.Health, hitFrame, log.Twists.Count(x => x.e.Effect == TwistEffect.SpecialArmour));
	}

	[Test]
	public static void Twist_Armour_SpecialTakesTheHitAndPlaysOn()
	{
		var (state, health, frame, armoured) = WaveIntoTalisman(Armour, 9);
		Assert.Equal(9, frame, "the wave meets the talisman on frame 9 (active)");
		Assert.Equal(FighterState.Attack, state, "armour: the special is not interrupted");
		Assert.Equal(928, health, "the damage still lands (60, +20% counterhit, C7)");
		Assert.Equal(1, armoured, "one armoured hit");

		var (cState, cHealth, _, cArm) = WaveIntoTalisman(null, 9);
		Assert.True(cState != FighterState.Attack && cHealth == 928 && cArm == 0, $"control: no twist, the hit interrupts ({cState})");

		var (rState, _, rFrame, rArm) = WaveIntoTalisman(Armour, 20);
		Assert.True(rFrame == 20 && rState != FighterState.Attack && rArm == 0, $"recovery (frame 20 > last active 13) is not armoured ({rState})");
	}

	[Test]
	public static void Twist_Armour_HitsPerSpecialComeFromData()
	{
		// The fixtures can't land two jabs inside 13 frames, so the special starts as if it had already taken one hit:
		// Ryo's jab (startup 4) then meets P2's Spirit Wave on its frame 5.
		foreach (int hits in new[] { 1, 2 })
		{
			var log = new Log();
			var m = Setup(log, Armour with { ArmourHits = hits }, -60, 60);
			m.Step(new FighterInput(LP), Wave());
			Assert.True(m.P2.ActiveSpecial == SpecialSlot.A && m.P2.TwistArmourTaken == 0, "a new special starts with its armour whole");
			m.P2.TwistArmourTaken = 1;
			for (int t = 0; t < 8 && log.Hits.Count == 0; t++) m.Step(Idle, Idle);
			Assert.True(log.Hits.Count == 1 && log.Hits[0].e.Attacker == 0, $"hits {hits}: the jab connects");
			bool playsOn = m.P2.State == FighterState.Attack;
			Assert.Equal(hits == 2, playsOn, $"hits {hits}: a second hit is armoured only when the data allows two ({m.P2.State})");
			Assert.Equal(Math.Min(hits, 2), m.P2.TwistArmourTaken == 0 ? 1 : m.P2.TwistArmourTaken, $"hits {hits}: counted");
		}
	}

	// ---- Unbreakable throws (Elder Kappa) --------------------------------------------------------------

	static Match ThrowSetup(Log log, ElderTwist? twist)
	{
		var m = Setup(log, twist);
		m.P1.X = -m.Config.BodyWidth / 2;
		m.P2.X = m.Config.BodyWidth / 2;
		return m;
	}

	static void RunThrows(Match m, Func<int, InputBits> p1, Func<int, InputBits> p2, int n = 60)
	{
		for (int t = 1; t <= n; t++) m.Step(new FighterInput(p1(t)), new FighterInput(p2(t)));
	}

	[Test]
	public static void Twist_UnbreakableThrows_BreakPressDoesNothing()
	{
		var log = new Log();
		var m = ThrowSetup(log, Grip);
		RunThrows(m, t => t == 9 ? THROW : 0, t => t == 1 ? THROW : 0);
		Assert.True(!log.Throws.Any(x => x.e.Outcome == ThrowOutcome.Break), "Ryo's press inside the window breaks nothing");
		Assert.Equal(880, m.P1.Health, "the throw lands (120, C4)");
		Assert.True(log.Twists.Any(x => x.e is { Holder: 1, Effect: TwistEffect.UnbreakableThrows }), "the refused break is reported");

		var cLog = new Log();
		var c = ThrowSetup(cLog, null);
		RunThrows(c, t => t == 9 ? THROW : 0, t => t == 1 ? THROW : 0);
		Assert.True(cLog.Throws.Any(x => x.e.Outcome == ThrowOutcome.Break) && c.P1.Health == 1000, "control: the same press breaks");

		var hLog = new Log();
		var h = ThrowSetup(hLog, Grip);
		RunThrows(h, t => t == 1 ? THROW : 0, t => t == 9 ? THROW : 0);
		Assert.True(hLog.Throws.Any(x => x.e.Outcome == ThrowOutcome.Break), "the elder can still break Ryo's throw");

		var kLog = new Log();
		var k = ThrowSetup(kLog, Grip);
		RunThrows(k, t => t == 1 ? THROW : 0, t => t == 1 ? THROW : 0);
		Assert.True(kLog.Throws.Any(x => x.e.Outcome == ThrowOutcome.Clash), "a same-frame clash still clashes (E12)");
	}

	// ---- Determinism and replay ---------------------------------------------------------------------

	static FighterInput Spam(int t) => t % 70 == 0 ? Wave(t % 280 == 0) : t % 70 == 35 ? Talisman()
		: t % 23 == 9 ? new FighterInput(THROW) : t % 3 == 0 ? new FighterInput(InputBits.Right) : Idle; // THROW: a break press after her grab
	static FighterInput Foe(int t) => t % 50 == 0 ? Wave() : t % 23 == 0 ? new FighterInput(THROW) : t % 7 == 0 ? new FighterInput(InputBits.Left) : Idle;

	static ReplayRecording Record(ElderTwist twist, int steps, Log log)
	{
		var m = Setup(log, twist);
		m.P1.Meter = 100;
		var rec = ReplayRecorder.Begin(m, null, 0);
		for (int t = 0; t < steps; t++)
		{
			FighterInput a = Spam(t), b = Foe(t);
			m.Step(a, b);
			rec.Record(a, b, m.StateHash());
			if (m.Phase == MatchPhase.Over) break;
		}
		return rec.Finish(m);
	}

	[Test]
	public static void Twist_FightsReplayExactly_AndTheTwistIsPartOfTheRecording()
	{
		var catalog = ReplayCatalog.FromRepo(RepoRoot);
		foreach (var twist in DataTwists().All)
		{
			var log = new Log();
			var rec = Record(twist, 2400, log);
			Assert.True(log.Twists.Count > 0, $"{twist.Id}: the twist fired in the recorded fight");
			var back = ReplayRecording.FromBytes(rec.ToBytes());
			Assert.Equal(twist.Id, back.Header.Fighters[1].Twist, $"{twist.Id}: the header names P2's twist");
			Assert.True(back.Header.Fighters[0].Twist == null, "Ryo has none");
			var r = Replayer.Run(back, catalog);
			Assert.True(r.Ok, $"{twist.Id}: {r}");

			var again = Record(twist, 2400, new Log());
			Assert.True(again.Hashes.SequenceEqual(rec.Hashes), $"{twist.Id}: same inputs, same hashes every tick");

			var dropped = ReplayRecording.FromBytes(rec.ToBytes());
			dropped.Header.Fighters[1].Twist = null;
			Assert.True(!Replayer.Run(dropped, catalog).Ok, $"{twist.Id}: without its twist the fight does not replay");
			var changed = ReplayRecording.FromBytes(rec.ToBytes());
			changed.Header.Fighters[1].TwistFp = "0000000000000000";
			var rc = Replayer.Run(changed, catalog);
			Assert.True(!rc.Ok && rc.ToString().Contains("changed"), $"{twist.Id}: a changed twist fails at setup ({rc})");
		}
	}

	[Test]
	public static void Twist_NoTwistFightHashesAsBefore()
	{
		// A fight with no twist must hash exactly as it did before YOK-90 (saved replays keep working): the twist state
		// joins the hash only when a fighter holds a twist. Same inputs with and without an inert-for-this-fight twist differ.
		var a = Setup(new Log(), null);
		var b = Setup(new Log(), null);
		for (int t = 0; t < 600; t++) { a.Step(Spam(t), Foe(t)); b.Step(Spam(t), Foe(t)); }
		Assert.Equal(a.StateHash(), b.StateHash(), "deterministic");
		Assert.Equal(0, a.P1.ActiveRepeat, "no row is counted without a reflect twist");
	}
}
