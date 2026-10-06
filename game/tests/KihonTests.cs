using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-23: Kihon (K2) control scheme and per-player scheme selection (minimum of YOK-25). Recorded numpad
/// inputs ("5+S" = neutral + Special, see InputParserTests.Record) through the parser and the full Match.
/// </summary>
public static class KihonTests
{
	const InputBits LP = InputBits.LightPunch, MP = InputBits.MediumPunch, HP = InputBits.HeavyPunch,
		LK = InputBits.LightKick, MK = InputBits.MediumKick, S = InputBits.Special;
	static readonly FighterInput Idle = FighterInput.None;

	static SpecialData[] Fixtures() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));
	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();

	static List<FighterInput> Seq(string s, int facing = 1) => InputParserTests.Record(s, facing);

	/// <summary>Ryo's fixture kit on both sides; Wave in A, Talisman in B, and (for slot tests) Talisman in C, Wave in D.</summary>
	static Match Setup(ControlScheme scheme = ControlScheme.Kihon, int p1x = -300, int p2x = 300, bool fillCD = false,
		List<SpecialEvent>? specials = null, List<HitEvent>? hits = null)
	{
		var m = new Match(null, Moves(), Moves());
		var fx = Fixtures();
		foreach (var f in m.Fighters)
		{
			FightScene.EquipStarters(f, fx);
			if (fillCD)
			{
				f.Equip(SpecialSlot.C, fx.Single(s => s.Slot == SpecialSlot.B));
				f.Equip(SpecialSlot.D, fx.Single(s => s.Slot == SpecialSlot.A));
			}
			f.Input.Scheme = scheme;
		}
		m.P1.X = p1x * SimConfig.Scale;
		m.P2.X = p2x * SimConfig.Scale;
		if (specials != null) m.SpecialStarted += (_, e) => specials.Add(e);
		if (hits != null) m.Hit += (_, e) => hits.Add(e);
		return m;
	}

	static void Play(Match m, IReadOnlyList<FighterInput> p1, IReadOnlyList<FighterInput>? p2 = null, int total = 0)
	{
		int n = Math.Max(total, Math.Max(p1.Count, p2?.Count ?? 0));
		for (int t = 0; t < n; t++)
			m.Step(t < p1.Count ? p1[t] : Idle, p2 != null && t < p2.Count ? p2[t] : Idle);
	}

	static InputCommand Parse(string seq, ControlScheme scheme, int recFacing = 1, int playFacing = 0)
	{
		var r = new InputReader { Scheme = scheme };
		foreach (var t in Seq(seq, recFacing)) r.Update(t, playFacing == 0 ? recFacing : playFacing);
		return r.Latest;
	}

	// --- Parser (K2) ---------------------------------------------------------------------------

	[Test]
	public static void Parser_SpecialPlusDirectionPicksSlot_BothFacings()
	{
		var cases = new (string dir, SpecialSlot slot)[]
		{
			("5", SpecialSlot.A), ("6", SpecialSlot.B), ("4", SpecialSlot.C), ("2", SpecialSlot.D),
			// Diagonals (E18): down-diagonals are D; up-diagonals are their horizontal, up is A.
			("3", SpecialSlot.D), ("1", SpecialSlot.D), ("8", SpecialSlot.A), ("9", SpecialSlot.B), ("7", SpecialSlot.C),
		};
		foreach (int facing in new[] { 1, -1 })
			foreach (var (dir, slot) in cases)
			{
				var c = Parse($"5 {dir}+S", ControlScheme.Kihon, facing);
				Assert.Equal(CommandKind.Special, c.Kind, $"{dir}+S facing {facing}: special");
				Assert.Equal(slot, c.Slot, $"{dir}+S facing {facing}: slot");
				Assert.True(!c.Precision, $"{dir}+S: no precision bonus in Kihon (K2)");
			}
		// The same absolute stick flips with the side: holding right is forward (B) facing right, back (C) facing left.
		Assert.Equal(SpecialSlot.B, Parse("6+S", ControlScheme.Kihon, 1, 1).Slot, "right facing right = B");
		Assert.Equal(SpecialSlot.C, Parse("6+S", ControlScheme.Kihon, 1, -1).Slot, "right facing left = C");
	}

	[Test]
	public static void Parser_KihonAcceptsMotionsAt100_KataIgnoresSpecial()
	{
		var k = Parse("5 2 3 6+LP", ControlScheme.Kihon);
		Assert.True(k.Kind == CommandKind.Special && k.Slot == SpecialSlot.A && k.Motion == Motion.Qcf236,
			"Kihon: 236+LP fires slot A, exactly as Kata (E18)");
		Assert.True(!k.Precision, "Kihon motion: no precision bonus (100% damage, E18)");
		Assert.Equal(LP, k.Pressed, "Kihon motion: Pressed has no Special bit, so Kata EX pairs apply");
		var kata = Parse("5 2 3 6+LP", ControlScheme.Kata);
		Assert.Equal(kata with { Precision = false }, k, "Kihon motion command = Kata's minus precision");
		Assert.Equal(SpecialSlot.D, Parse("5 2 5 2+HK", ControlScheme.Kihon).Slot, "Kihon: 22+K fires slot D");
		Assert.Equal(CommandKind.None, Parse("5 5+S", ControlScheme.Kata).Kind, "Kata: Special does nothing");
		Assert.Equal(CommandKind.None, Parse("5 5+S 5+S", ControlScheme.Kihon).Kind, "held Special fires once (edge only)");
		var ex = Parse("5 6+S+MK", ControlScheme.Kihon);
		Assert.True(ex.Kind == CommandKind.Special && ex.Slot == SpecialSlot.B && ex.Button == MK, "Special + MK: slot B, button MK");
		Assert.True(Match.ExPair(ex.Pressed), "Special + one kick reads as EX (E18)");
		Assert.True(!Match.ExPair(Parse("5 6+S", ControlScheme.Kihon).Pressed), "Special alone is not EX");
		Assert.True(!Match.ExPair(LP) && Match.ExPair(LP | MP) && !Match.ExPair(LP | LK), "Kata EX pairs unchanged");
	}

	[Test]
	public static void Parser_SameTickSpecialBeatsMotion()
	{
		// 236 completes on the tick Special goes down: Special + direction (6 = forward = B) wins over the motion (A).
		var both = Parse("5 2 3 6+S+LP", ControlScheme.Kihon);
		Assert.True(both.Kind == CommandKind.Special && both.Slot == SpecialSlot.B && both.Motion == Motion.None,
			"same tick: Special + forward = B, motion ignored (E18)");
		Assert.True((both.Pressed & S) != 0 && Match.ExPair(both.Pressed), "the attack is the Kihon EX button");
		Assert.Equal(SpecialSlot.B, Parse("5 2 3 6+S", ControlScheme.Kihon).Slot, "Special alone after 236: B");
		// Facing left: the same physical input recorded mirrored still resolves to B, not the motion's A.
		Assert.Equal(SpecialSlot.B, Parse("5 2 3 6+S+LP", ControlScheme.Kihon, -1).Slot, "mirrored: B");
	}

	/// <summary>Raw stream over all nine directions and attacks (never Special), so K1 motions form often.</summary>
	static List<FighterInput> MotionStream(uint seed, int n)
	{
		var rng = new SimRng(seed);
		InputBits[] buttons = { InputBits.None, InputBits.None, LP, MP, HP, LK, MK, InputBits.HeavyKick, LP | MP, LK | MK, LP | LK };
		var list = new List<FighterInput>();
		int dir = 5;
		for (int t = 0; t < n; t++)
		{
			if (rng.Next(3) == 0) dir = 1 + rng.Next(9);
			list.Add(new FighterInput(Numpad.ToBits(dir, 1) | buttons[rng.Next(buttons.Length)]));
		}
		return list;
	}

	[Test]
	public static void K3_MotionCommandsIdenticalInBothSchemes_ExceptPrecision()
	{
		var kata = new InputReader { Scheme = ControlScheme.Kata };
		var kihon = new InputReader { Scheme = ControlScheme.Kihon };
		int specials = 0;
		var stream = MotionStream(7, 4000);
		for (int t = 0; t < stream.Count; t++)
		{
			int facing = (t / 250) % 2 == 0 ? 1 : -1; // flip sides now and then
			kata.Update(stream[t], facing);
			kihon.Update(stream[t], facing);
			Assert.Equal(kata.Latest with { Precision = false }, kihon.Latest, $"tick {t}: same command, 100% in Kihon");
			if (kata.Latest.Kind == CommandKind.Special) { specials++; Assert.True(kata.Latest.Precision, "Kata motion: precision"); }
		}
		Assert.True(specials > 20, $"the stream formed motions ({specials})");
	}

	// --- Match: slots, damage, facing ----------------------------------------------------------

	[Test]
	public static void Match_SpecialPlusDirectionFiresSlotsAtoD_BothSides()
	{
		foreach (var (dir, slot) in new[] { ("5", SpecialSlot.A), ("6", SpecialSlot.B), ("4", SpecialSlot.C), ("2", SpecialSlot.D) })
		{
			var ev = new List<SpecialEvent>();
			var m = Setup(fillCD: true, specials: ev);
			Play(m, Seq($"{dir}+S"), Seq($"{dir}+S", -1)); // P2 faces left: the same input recorded mirrored
			foreach (var f in m.Fighters)
			{
				Assert.Equal(slot, f.ActiveSpecial, $"{dir}+S: slot");
				Assert.True(!f.ActivePrecision && !f.ActiveEx, $"{dir}+S: plain, no precision");
				Assert.Equal(1, f.MoveFrame, $"{dir}+S: starts on the press tick");
			}
			Assert.Equal(2, ev.Count, "one SpecialStarted per fighter");
		}
	}

	[Test]
	public static void Match_KihonDealsFullDamage_KataGetsPrecision()
	{
		// Kihon Special + direction and Kihon motion: 100% of 60 (K2, E18); Kata motion: +10% (K1).
		foreach (var (scheme, seq, expected) in new[] {
			(ControlScheme.Kihon, "5+S", 60), (ControlScheme.Kihon, "2 3 6+LP", 60), (ControlScheme.Kata, "2 3 6+LP", 66) })
		{
			var hits = new List<HitEvent>();
			var m = Setup(scheme, hits: hits);
			Play(m, Seq(seq), total: 120);
			Assert.Equal(1, hits.Count, $"{scheme} [{seq}]: the wave hits once");
			Assert.Equal(1000 - expected, m.P2.Health, $"{scheme} [{seq}]: damage");
		}
	}

	[Test]
	public static void Match_KihonMotionFiresItsSlot_SameTickSpecialWins_MotionEx()
	{
		var m = Setup(fillCD: true);
		Play(m, Seq("2 3 6+LP"), Seq("2 1 4+LP", -1));
		Assert.Equal(SpecialSlot.A, m.P1.ActiveSpecial, "Kihon 236+P: slot A");
		Assert.Equal(SpecialSlot.C, m.P2.ActiveSpecial, "Kihon 214+P (facing left): slot C");
		Assert.True(!m.P1.ActivePrecision && !m.P2.ActivePrecision, "Kihon motion: no precision");

		var both = Setup(fillCD: true);
		both.P1.Meter = 100;
		Play(both, Seq("2 3 6+S+LP"));
		Assert.True(both.P1.ActiveSpecial == SpecialSlot.B && both.P1.ActiveEx, "same tick: Special + forward + LP = EX B, not the motion's A");

		var ex = Setup();
		ex.P1.Meter = 100;
		Play(ex, Seq("2 3 6+LP+MP"));
		Assert.True(ex.P1.ActiveSpecial == SpecialSlot.A && ex.P1.ActiveEx && !ex.P1.ActivePrecision && ex.P1.Meter == 0,
			"Kihon motion + two punches: EX exactly as Kata, at 100%");

		foreach (var scheme in new[] { ControlScheme.Kihon, ControlScheme.Kata })
		{
			var thr = Setup(scheme, p2x: -300 + 120);
			thr.P1.Meter = 100;
			Play(thr, Seq("2 3 6+LP+LK"));
			Assert.True(thr.P1.CurrentMove is { IsThrow: true } && thr.P1.Meter == 100, $"{scheme}: motion + LP+LK is still the throw");
		}
	}

	[Test]
	public static void Match_EmptySlotGivesTheButtonsNormal()
	{
		var m = Setup(); // C and D empty
		Play(m, Seq("4+S"));
		Assert.Equal(FighterState.Idle, m.P1.State, "Special alone on an empty slot: nothing");
		var m2 = Setup();
		Play(m2, Seq("2+S+MP"));
		Assert.True(m2.P1.CurrentMove is { IsNormal: true, Button: InputBits.MediumPunch }, "empty D + MP: the MP normal");
	}

	// --- EX (E16), throw (E12), burst (E13) ----------------------------------------------------

	[Test]
	public static void Ex_SpecialPlusOneButton_TwoTicksLeniency_MeterRules()
	{
		for (int gap = 0; gap <= 3; gap++)
		{
			var m = Setup();
			m.P1.Meter = 100;
			var input = Seq(gap == 0 ? "5+S+LK" : "5+S");
			for (int k = 1; k < gap; k++) input.Add(Idle);
			if (gap > 0) input.Add(new FighterInput(LK));
			Play(m, input);
			bool ex = gap <= 2;
			Assert.Equal(ex, m.P1.ActiveEx, $"kick {gap} ticks after Special: EX {ex}");
			Assert.Equal(ex ? 0 : 100, m.P1.Meter, $"gap {gap}: meter");
			Assert.Equal(SpecialSlot.A, m.P1.ActiveSpecial, $"gap {gap}: still slot A");
			Assert.True(!m.P1.ActivePrecision, "Kihon EX: no precision");
		}
		for (int early = 1; early <= 2; early++) // attack 1-2 ticks before Special: its normal, never EX (E18)
		{
			var m = Setup();
			m.P1.Meter = 100;
			var input = new List<FighterInput> { new(LK) };
			for (int k = 1; k < early; k++) input.Add(Idle);
			input.Add(new FighterInput(S));
			Play(m, input);
			Assert.True(m.P1.CurrentMove is { IsNormal: true, Button: InputBits.LightKick }, $"LK {early} early: the LK normal");
			Assert.True(!m.P1.ActiveEx && m.P1.Meter == 100, $"LK {early} early: no EX, no meter");
		}
		var poor = Setup();
		poor.P1.Meter = 99;
		Play(poor, Seq("6+S+HP"));
		Assert.True(poor.P1.ActiveSpecial == SpecialSlot.B && !poor.P1.ActiveEx && poor.P1.Meter == 99, "no meter: plain, nothing spent");
	}

	[Test]
	public static void Throw_LpLkWorksInBothSchemes_AndBeatsKihonEx()
	{
		foreach (var scheme in new[] { ControlScheme.Kihon, ControlScheme.Kata })
			foreach (var seq in new[] { "5+LP+LK", "5+LP 5+LK", "5+LK 5 5 5+LP" })
			{
				var m = Setup(scheme, p2x: -300 + 120);
				Play(m, Seq(seq));
				Assert.True(m.P1.CurrentMove is { IsThrow: true }, $"{scheme} [{seq}]: throw (E12)");
			}
		var t = Setup(p2x: -300 + 120);
		t.P1.Meter = 100;
		Play(t, Seq("5+S+LP+LK"));
		Assert.True(t.P1.CurrentMove is { IsThrow: true } && t.P1.Meter == 100, "Special + LP+LK: the throw wins, no meter");
	}

	[Test]
	public static void Burst_ThreePunchesWorkInBothSchemes()
	{
		foreach (var scheme in new[] { ControlScheme.Kihon, ControlScheme.Kata })
		{
			var hits = new List<HitEvent>();
			var m = Setup(scheme, p1x: -300, p2x: -300 + 100, hits: hits);
			m.P2.Meter = 140;
			int jab = Array.FindIndex(m.P1.Moves, mv => mv.Id == "test-ryo-light-punch");
			for (int t = 0; t < 30 && hits.Count == 0; t++) m.Step(t == 0 ? FighterInput.Attack(jab) : Idle, Idle);
			Assert.Equal(FighterState.Hitstun, m.P2.State, $"{scheme}: P2 in hitstun");
			m.Step(Idle, new FighterInput(LP | MP | HP));
			Assert.True(m.P2.BurstUsed && m.P2.State == FighterState.Burst, $"{scheme}: LP+MP+HP bursts (E13)");
		}
	}

	// --- Scheme selection (YOK-25 minimum) and K3 ------------------------------------------------

	/// <summary>Deterministic raw stream: directions 4/5/6/8 and buttons, never Special, so no K1 motion can form.</summary>
	static List<FighterInput> NoMotionStream(uint seed, int n)
	{
		var rng = new SimRng(seed);
		var dirs = new[] { 4, 5, 6, 8 };
		InputBits[] buttons = { InputBits.None, InputBits.None, InputBits.None, LP, MP, HP, LK, MK, InputBits.HeavyKick, LP | LK };
		var list = new List<FighterInput>();
		int dir = 5;
		for (int t = 0; t < n; t++)
		{
			if (rng.Next(6) == 0) dir = dirs[rng.Next(dirs.Length)];
			list.Add(new FighterInput(Numpad.ToBits(dir, 1) | buttons[rng.Next(buttons.Length)]));
		}
		return list;
	}

	[Test]
	public static void K3_NormalsCombosThrowsIdenticalInBothSchemes_AndSwitchingHasNoOtherEffect()
	{
		var p1 = NoMotionStream(11, 1500);
		var p2 = NoMotionStream(29, 1500);
		var kata = Setup(ControlScheme.Kata, p1x: -150, p2x: 150);
		var kihon = Setup(ControlScheme.Kihon, p1x: -150, p2x: 150);
		var flip = Setup(ControlScheme.Kata, p1x: -150, p2x: 150);
		for (int t = 0; t < p1.Count; t++)
		{
			if (t % 37 == 0) // switch both players back and forth mid-fight, mid-move, mid-buffer
				foreach (var f in flip.Fighters)
					f.Input.Scheme = f.Input.Scheme == ControlScheme.Kata ? ControlScheme.Kihon : ControlScheme.Kata;
			kata.Step(p1[t], p2[t]);
			kihon.Step(p1[t], p2[t]);
			flip.Step(p1[t], p2[t]);
			Assert.Equal(kata.StateHash(), kihon.StateHash(), $"tick {t}: Kata and Kihon identical without specials (K3)");
			Assert.Equal(kata.StateHash(), flip.StateHash(), $"tick {t}: switching changed nothing but parsing");
		}
		Assert.True(kata.P1.Health < 1000 || kata.P2.Health < 1000, "the stream actually fought");
	}

	[Test]
	public static void Switching_ChangesParsingOnly_KeepsPendingAndSurvivesReset()
	{
		var r = new InputReader();
		Assert.Equal(ControlScheme.Kata, r.Scheme, "sim default stays Kata (existing tests)");
		r.Update(new FighterInput(LP), 1);
		r.Scheme = ControlScheme.Kihon;
		Assert.True(r.Pending.Kind == CommandKind.Normal && r.Pending.Button == LP, "waiting command kept across a switch");
		r.Update(new FighterInput(S), 1);
		Assert.Equal(SpecialSlot.A, r.Latest.Slot, "after the switch Special parses");
		r.Scheme = ControlScheme.Kata;
		r.Update(Idle, 1);
		r.Update(new FighterInput(S), 1);
		Assert.Equal(CommandKind.None, r.Latest.Kind, "back on Kata, Special is ignored");

		var m = Setup();
		m.P1.Input.Scheme = ControlScheme.Kata;
		m.Reset();
		Assert.True(m.P1.Input.Scheme == ControlScheme.Kata && m.P2.Input.Scheme == ControlScheme.Kihon, "reset keeps each player's scheme");
	}

	[Test]
	public static void Determinism_KihonReplaysExactly()
	{
		static ulong Run()
		{
			var rng = new SimRng(1234);
			var m = Setup(fillCD: true, p1x: -200, p2x: 200);
			foreach (var f in m.Fighters) f.Meter = 300;
			ulong h = 0;
			InputBits[] pool = { InputBits.None, S, LP, MP, HP, LK, MK, InputBits.HeavyKick, S | LP, LP | LK, InputBits.Left,
				InputBits.Right, InputBits.Down, InputBits.Up };
			for (int t = 0; t < 2000; t++)
			{
				m.Step(new FighterInput(pool[rng.Next(pool.Length)] | pool[rng.Next(pool.Length)]),
					new FighterInput(pool[rng.Next(pool.Length)]));
				h = h * 31 + m.StateHash();
			}
			return h;
		}
		Assert.Equal(Run(), Run(), "same Kihon inputs, same fight");
	}

	[Test]
	public static void FightScene_DefaultsToKihon_F4TogglesP1(Node runner)
	{
		var nm = FightScene.NewMatch();
		Assert.True(nm.P1.Input.Scheme == ControlScheme.Kihon && nm.P2.Input.Scheme == ControlScheme.Kihon, "demo default: Kihon");
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		Assert.Equal(ControlScheme.Kihon, scene.SchemeOf(0), "scene starts on Kihon");
		scene.SetScheme(1, ControlScheme.Kata);
		Assert.Equal(ControlScheme.Kata, scene.SchemeOf(1), "per-player scheme settable");
		var overlay = scene.GetNodeOrNull<DebugOverlay>("DebugOverlay");
		if (overlay != null)
		{
			overlay.HandleKey(DebugOverlay.SchemeKey);
			Assert.Equal(ControlScheme.Kata, scene.SchemeOf(0), "F4: P1 to Kata");
			overlay.HandleKey(DebugOverlay.SchemeKey);
			Assert.Equal(ControlScheme.Kihon, scene.SchemeOf(0), "F4 again: back to Kihon");
			Assert.Equal(ControlScheme.Kata, scene.SchemeOf(1), "F4 leaves P2 alone");
		}
		scene.Step(new FighterInput(S), Idle);
		Assert.Equal(SpecialSlot.A, scene.Match.P1.ActiveSpecial, "Special in the scene fires slot A");
		scene.QueueFree();
	}
}
