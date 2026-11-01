using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Replay;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-224: Fox Mirage's <c>properties.teleport</c> and Mirage Feint's <c>properties.decoy</c> (rules.md specials
/// table, A5, E12, P6, T0, T4). The mirage and the poke below are TEST FIXTURES with made-up numbers (the shipped
/// data/moves/fox-mirage.json is YOK-231's).
/// </summary>
public static class FoxMirageTests
{
	const int S = SimConfig.Scale;
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static readonly SimConfig NoHitstop = SimConfig.Default with { HitstopEnabled = false };

	// TEST FIXTURE: 18/-/10, teleport on frame 18; Lv 2 recovery -2; Lv 3 adds a one-hit decoy.
	const string MirageJson = """
	{
	  "kind": "special", "id": "test-mirage",
	  "name": "TEST FIXTURE: mirage (made-up numbers; not game content)",
	  "input": { "kata": { "slot": "C", "motion": "214" }, "kihon": { "slot": "C", "direction": "back" } },
	  "levels": {
	    "1": { "frame_data": {
	      "startup": 18, "active": null, "recovery": 10, "damage": 0,
	      "hurtboxes": [ { "frames": [1, 28], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } } ],
	      "properties": { "teleport": { "to": "behind_opponent", "frame": 18 } } } },
	    "2": { "tuning": { "target": "recovery", "op": "add", "value": -2 } },
	    "3": { "evolution": { "id": "test-mirage-feint", "name": "TEST FIXTURE", "preset": "test-mirage-feint",
	      "changes": [ { "target": "properties.decoy", "op": "set", "value": 1 } ] } }
	  }
	}
	""";

	// TEST FIXTURE: a long poke, active frames 5-14, reaching 30-230 units ahead at chest height.
	const string PokeJson = """
	{ "kind": "normal", "id": "test-mirage-poke", "input": { "button": "LP" }, "frame_data": {
	  "startup": 4, "active": 10, "recovery": 6, "damage": 30, "hitstun": 12, "blockstun": 9,
	  "hitboxes": [ { "frames": [5, 14], "rect": { "x": 30, "y": 100, "w": 200, "h": 30 } } ],
	  "hurtboxes": [ { "frames": [1, 20], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } } ] } }
	""";

	static SpecialData Mirage => SpecialLoader.Parse(MirageJson);
	static MoveData Poke => MoveLoader.Parse(PokeJson);

	/// <summary>P1 holds the mirage at <paramref name="level"/> in C; both hold the poke in slot 1.</summary>
	static Match Setup(int level, int p1X = -100, int p2X = 100)
	{
		var m = new Match(NoHitstop, new[] { Poke }, new[] { Poke });
		m.P1.Equip(SpecialSlot.C, Mirage, level);
		m.P1.X = p1X * S;
		m.P2.X = p2X * S;
		return m;
	}

	static FighterInput Mirage1(int t) => t == 1 ? FighterInput.Special(SpecialSlot.C) : FighterInput.None;

	static void Throws(string json, string what)
	{
		try { SpecialLoader.Parse(json).Build(1, false); }
		catch (FormatException) { return; }
		throw new AssertFailed($"{what}: expected FormatException");
	}

	[Test]
	public static void TeleportAndDecoyAreReadFromData_DecoyOnlyByTheEvolution()
	{
		for (int lv = 1; lv <= 3; lv++)
		{
			var mv = Mirage.Build(lv, false);
			Assert.Equal(18, mv.Teleport?.Frame ?? 0, $"Lv {lv}: teleport on frame 18");
			Assert.Equal(lv == 3 ? 1 : 0, mv.Decoy, $"Lv {lv}: decoy only at Lv 3 (A5)");
		}
		Assert.True(Poke.Teleport is null && Poke.Decoy == 0, "a move without the keys has neither");
		string tp = "\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 18 }";
		Throws(MirageJson.Replace(tp, "\"teleport\": { \"to\": \"in_front\", \"frame\": 18 }"), "unknown to");
		Throws(MirageJson.Replace(tp, "\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 0 }"), "frame 0");
		Throws(MirageJson.Replace(tp, "\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 29 }"), "frame past the move");
		Throws(MirageJson.Replace(tp, "\"teleport\": { \"to\": \"behind_opponent\", \"frame\": 18, \"x\": 1 }"), "extra key");
		Throws(MirageJson.Replace(tp, "\"teleport\": { \"to\": \"behind_opponent\" }"), "no frame");
		Throws(MirageJson.Replace(tp, tp + ", \"decoy\": 0"), "decoy 0");
		Throws(MirageJson.Replace(tp, tp + ", \"decoy\": true"), "decoy as a flag");
		Assert.Equal(2, SpecialLoader.Parse(MirageJson.Replace(tp, tp + ", \"decoy\": 2.0")).Build(1, false).Decoy, "2.0 is 2 hits");
	}

	[Test]
	public static void Teleport_LandsJustBehindOnItsFrame_AndTurns()
	{
		var m = Setup(1);
		int gap = m.PushGap(m.P1, m.P2);
		for (int t = 1; t <= 18; t++)
		{
			m.Step(Mirage1(t), FighterInput.None);
			if (t < 18) Assert.True(m.P1.X == -100 * S && m.P1.Facing == 1, $"tick {t}: still at the origin");
		}
		Assert.Equal(100 * S + gap, m.P1.X, "frame 18: one push gap behind the opponent");
		Assert.Equal(-1, m.P1.Facing, "turned to face the opponent");
		Assert.Equal(1, m.P2.Facing, "the idle opponent turns to the new side");
		Assert.True(m.DecoyOf(0) is null, "Lv 1 leaves no decoy");
	}

	/// <summary>
	/// Code Critic (YOK-224): both teleport on the same tick (Ryo vs the Tanuki's copy). Each lands behind where the
	/// other stood, facing them, whichever slot is on the left: the result never depends on who is P1.
	/// </summary>
	[Test]
	public static void Teleport_BothOnTheSameTick_EachLandsBehindTheOthersStart_InBothSlotOrders()
	{
		foreach (int left in new[] { 0, 1 })
		{
			var m = new Match(NoHitstop, new[] { Poke }, new[] { Poke });
			m.P1.Equip(SpecialSlot.C, Mirage, 1);
			m.P2.Equip(SpecialSlot.C, Mirage, 1);
			m.Fighters[left].X = -100 * S;
			m.Fighters[1 - left].X = 100 * S;
			m.Step(FighterInput.None, FighterInput.None); // face each other
			int gap = m.PushGap(m.P1, m.P2);
			for (int t = 1; t <= 18; t++) m.Step(Mirage1(t), Mirage1(t));
			Fighter l = m.Fighters[left], r = m.Fighters[1 - left];
			Assert.Equal(100 * S + gap, l.X, $"left = P{left + 1}: lands behind the right one's start");
			Assert.Equal(-100 * S - gap, r.X, $"left = P{left + 1}: the right one lands behind the left one's start");
			Assert.True(l.Facing == -1 && r.Facing == 1, $"left = P{left + 1}: both face each other on the new sides");
		}
	}

	[Test]
	public static void Teleport_BehindACorneredOpponent_TakesTheStageLimit()
	{
		foreach (int dir in new[] { 1, -1 })
		{
			var m = new Match(NoHitstop, new[] { Poke }, new[] { Poke });
			m.P1.Equip(SpecialSlot.C, Mirage, 1);
			m.P2.X = dir > 0 ? m.CornerHi(m.P2) : m.CornerLo(m.P2);
			m.P1.X = m.P2.X - dir * 400 * S;
			m.Step(FighterInput.None, FighterInput.None); // face each other
			int gap = m.PushGap(m.P1, m.P2);
			for (int t = 1; t <= 18; t++) m.Step(Mirage1(t), FighterInput.None);
			int limit = dir > 0 ? m.CornerHi(m.P1) : m.CornerLo(m.P1);
			Assert.Equal(limit, m.P1.X, $"corner {dir}: lands at the stage limit");
			Assert.Equal(limit - dir * gap, m.P2.X, $"corner {dir}: the opponent is pushed out, push boxes touching");
			Assert.Equal(-dir, m.P1.Facing, $"corner {dir}: faces the opponent");
			Assert.True(m.P2.X >= m.CornerLo(m.P2) && m.P2.X <= m.CornerHi(m.P2), "the opponent stays on the stage");
		}
	}

	[Test]
	public static void Teleport_IsNoInvulnerability_StartupIsHittable_RecoveryIsPunishable()
	{
		// E12: a strike in the startup stops it: the teleport never happens.
		var a = Setup(1);
		for (int t = 1; t <= 30; t++) a.Step(Mirage1(t), t == 5 ? FighterInput.Attack(0) : FighterInput.None);
		Assert.True(a.P1.Health < a.P1.MaxHealth, "hit in the startup");
		Assert.True(a.P1.X < a.P2.X, "and never got behind");

		// The recovery is open: the opponent turns and pokes Ryo before he can act (recovery = ticks 19-28).
		var b = Setup(1);
		bool hitInRecovery = false;
		for (int t = 1; t <= 30; t++)
		{
			bool wasRecovering = b.P1.State == FighterState.Attack && b.P1.MoveFrame > 18;
			b.Step(Mirage1(t), t == 20 ? FighterInput.Attack(0) : FighterInput.None);
			if (wasRecovering && b.P1.State == FighterState.Hitstun) hitInRecovery = true;
		}
		Assert.True(hitInRecovery, "punished in the recovery");
		Assert.True(b.P1.Health < b.P1.MaxHealth, "for damage");
	}

	[Test]
	public static void Decoy_StandsAtTheOrigin_AndTheAiSeesIt()
	{
		var m = Setup(3);
		for (int t = 1; t <= 18; t++) m.Step(Mirage1(t), FighterInput.None);
		var d = m.DecoyOf(0) ?? throw new AssertFailed("Lv 3 leaves a decoy");
		Assert.True(d.X == -100 * S && d.Facing == 1 && d.HitsLeft == 1, "at the origin, facing as Ryo did, one hit");
		var view = AiView.Capture(m);
		Assert.Equal(1, view.Decoys.Count, "P6: the decoy is on screen");
		Assert.True(view.Decoys[0] == new DecoyView(0, -100 * S, 1), "whose, where and which way");
		Assert.Equal(0, AiView.Capture(Setup(1)).Decoys.Count, "none without one");

		// One per fighter: a second mirage replaces it.
		for (int t = 19; t <= 60; t++) m.Step(t == 30 ? FighterInput.Special(SpecialSlot.C) : FighterInput.None, FighterInput.None);
		Assert.Equal(1, AiView.Capture(m).Decoys.Count, "still one");
		Assert.True(m.DecoyOf(0)!.X != d.X || !ReferenceEquals(m.DecoyOf(0), d), "a new decoy at the second origin");
	}

	[Test]
	public static void Decoy_AbsorbsTheNextHit_ThenVanishes()
	{
		var m = Setup(3);
		var events = new System.Collections.Generic.List<DecoyEvent>();
		m.DecoyChanged += (_, e) => events.Add(e);
		// P2 pokes toward the origin on tick 14 (facing held during the move): active ticks 18-27.
		for (int t = 1; t <= 40; t++) m.Step(Mirage1(t), t == 14 ? FighterInput.Attack(0) : FighterInput.None);
		Assert.True(events.Select(e => e.Kind).SequenceEqual(new[] { DecoyEventKind.Spawn, DecoyEventKind.Absorbed, DecoyEventKind.Vanished }),
			$"spawned, absorbed one hit, vanished ({string.Join(",", events.Select(e => e.Kind))})");
		Assert.True(m.DecoyOf(0) is null && AiView.Capture(m).Decoys.Count == 0, "gone");
		Assert.True(m.P1.Health == m.P1.MaxHealth && m.P2.Health == m.P2.MaxHealth, "nobody took damage");
		Assert.True(m.P2.Meter == 0, "no meter for hitting an image");

		// The same poke at Lv 1 (no decoy) whiffs on the empty origin: the decoy changes only that one hit.
		var plain = Setup(1);
		for (int t = 1; t <= 40; t++) plain.Step(Mirage1(t), t == 14 ? FighterInput.Attack(0) : FighterInput.None);
		Assert.True(plain.P1.Health == plain.P1.MaxHealth, "no decoy: the poke whiffs");
	}

	[Test]
	public static void Decoy_IsInTheStateHash_ReplaysExactly_AndResetClearsIt()
	{
		// The same Lv 3 with an evolution that changes nothing (damage 0 stays 0): it differs only by the decoy.
		var lv2 = Setup(3);
		lv2.P1.Equip(SpecialSlot.C, SpecialLoader.Parse(MirageJson.Replace(
			"\"target\": \"properties.decoy\", \"op\": \"set\", \"value\": 1", "\"target\": \"damage\", \"op\": \"set\", \"value\": 0")), 3);
		Assert.True(lv2.P1.Specials[(int)SpecialSlot.C]!.Move.Decoy == 0, "the twin has no decoy");
		var lv3 = Setup(3);
		for (int t = 1; t <= 30; t++)
		{
			lv2.Step(Mirage1(t), FighterInput.None);
			lv3.Step(Mirage1(t), FighterInput.None);
			if (t < 18) Assert.Equal(lv2.StateHash(), lv3.StateHash(), $"tick {t}: same until the teleport");
			else Assert.True(lv2.StateHash() != lv3.StateHash(), $"tick {t}: the decoy is in the hash");
		}
		lv3.Reset();
		var fresh = Setup(3);
		fresh.Reset();
		Assert.True(lv3.DecoyOf(0) is null, "Reset clears the decoy");
		Assert.Equal(fresh.StateHash(), lv3.StateHash(), "and the hash is a fresh match's");

		var cat = ReplayCatalog.FromRepo(RepoRoot);
		cat.Specials.Add(Mirage);
		cat.Moves.Add(Poke);
		var m = new Match(null, new[] { Poke }, new[] { Poke }); // the fight's own config and start positions
		m.P1.Equip(SpecialSlot.C, Mirage, 3);
		var rec = ReplayRecorder.Begin(m, null, 0, null);
		for (int t = 1; t <= 90; t++)
		{
			var a = t == 1 || t == 50 ? FighterInput.Special(SpecialSlot.C) : FighterInput.None;
			var b = t == 14 || t == 40 || t == 70 ? FighterInput.Attack(0) : FighterInput.None;
			m.Step(a, b);
			rec.Record(a, b, m.StateHash());
		}
		var r = Replayer.Run(ReplayRecording.FromBytes(rec.Finish(m).ToBytes()), cat);
		Assert.True(r.Ok, $"the mirage fight replays: {r}");
		Assert.Equal(90, r.StepsRun, "every step");
		Assert.True(r.FirstDivergentStep < 0, "no step diverges");
	}

	[Test]
	public static void TanukiCopy_TeleportsAtRyosLevel_WithoutTheDecoy()
	{
		var ryo = Kit.Load(Kit.Ryo, KitSources.Repo(RepoRoot).FixturesOnly());
		var copy = new CopyTarget(SpecialSlot.C, Mirage, 3);
		var kit = TanukiKit.Build(ryo, copy);
		ulong[] Run()
		{
			var m = new Match(NoHitstop, ryo.Moves, kit.Moves);
			ryo.Equip(m.P1);
			kit.Equip(m.P2);
			m.P1.Equip(SpecialSlot.C, Mirage, 3);
			m.P1.X = -100 * S;
			m.P2.X = 100 * S;
			var eq = m.P2.Specials[(int)SpecialSlot.C] ?? throw new AssertFailed("T0: the copy sits in Ryo's slot C");
			Assert.True(eq.Data.Id == "test-mirage" && eq.Level == 2 && eq.Move.Decoy == 0, "T4: Lv 2 strength, no evolution");
			var hashes = new ulong[40];
			for (int t = 1; t <= 40; t++)
			{
				m.Step(FighterInput.None, t == 1 ? FighterInput.Special(SpecialSlot.C) : FighterInput.None);
				hashes[t - 1] = m.StateHash();
				if (t == 18)
				{
					Assert.Equal(-100 * S - m.PushGap(m.P1, m.P2), m.P2.X, "the Tanuki lands behind Ryo");
					Assert.Equal(1, m.P2.Facing, "facing Ryo");
					Assert.True(m.DecoyOf(1) is null, "T4: the copy leaves no decoy");
				}
			}
			return hashes;
		}
		Assert.True(Run().SequenceEqual(Run()), "the copy fight replays to the same hash every tick");
	}
}
