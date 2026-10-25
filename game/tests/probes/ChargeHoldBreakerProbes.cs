using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// Test Breaker probes for YOK-171 (AM11, Sumo Stance charge hold). Fixture modifier as in ChargeHoldTests: window 0-30,
/// step 10, 14 / 18 / 22 / 26 units per tick, on the fixture Rising Talisman (slot B, startup 5).
/// </summary>
public static class ChargeHoldBreakerProbes
{
	const int S = SimConfig.Scale;
	const int Startup = 5;
	static readonly FighterInput Idle = FighterInput.None;

	static string Charge(int min = 0, int max = 30, int step = 10, string units = "14, 18, 22, 26", int cap = 26) => $$"""
		{
		  "kind": "modifier", "id": "test-charge", "name": "Test Charge", "source": "kappa", "rarity": "common", "locked": false,
		  "rules": ["A10"], "applies_to": { "requires": [], "excludes": ["projectile"] },
		  "charge": {
		    "button": "special_trigger", "window_ticks": { "min": {{min}}, "max": {{max}} }, "step_ticks": {{step}}, "grace_ticks": 0,
		    "release": "dash_then_move", "hold_pose_clip": "ryo-crouch",
		    "dash": { "duration": "startup", "units_per_tick_by_step": [{{units}}], "cap_units_per_tick": {{cap}} }
		  },
		  "effects": [{ "target": "properties.forward_travel", "op": "set", "value": "charge_dash", "when": "charge_released" }],
		  "card": { "plain": "Test.", "frames": "Test." }
		}
		""";

	static SpecialData[] Fixtures() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));
	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();

	static Match Setup(string? json = null, int p1x = -300, int p2x = 300, bool bothCharge = false, ControlScheme scheme = ControlScheme.Kihon)
	{
		var m = new Match(null, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Fixtures());
		m.P1.AttachModifier(SpecialSlot.B, ModifierLoader.Parse(json ?? Charge()), m.Config);
		if (bothCharge) m.P2.AttachModifier(SpecialSlot.B, ModifierLoader.Parse(json ?? Charge()), m.Config);
		m.P1.Input.Scheme = scheme;
		m.P2.Input.Scheme = ControlScheme.Kihon;
		m.P1.X = p1x * S;
		m.P2.X = p2x * S;
		return m;
	}

	static FighterInput KihonHold(int t, int hold) =>
		t == 1 ? new FighterInput(InputBits.Special | InputBits.Right)
		: t <= 1 + hold ? new FighterInput(InputBits.Special) : Idle;

	static string Violations(Match m)
	{
		foreach (var f in m.Fighters)
		{
			if (f.State != FighterState.Bracing && (f.BraceSlot != SpecialSlot.None || f.BraceHeld != 0 || f.BraceFrame != 0))
				return $"brace state outside Bracing ({f.State}, slot {f.BraceSlot}, held {f.BraceHeld}, frame {f.BraceFrame})";
			if (f.State == FighterState.Bracing && (f.BraceSlot == SpecialSlot.None || f.BraceHeld >= 30 || f.CurrentMove != null || f.Guarding))
				return $"bad Bracing state (slot {f.BraceSlot}, held {f.BraceHeld}, move {f.CurrentMove?.Id}, guarding {f.Guarding})";
			if (f.ChargeSpeed > 0 && (f.State != FighterState.Attack || f.ActiveSpecial == SpecialSlot.None))
				return $"ChargeSpeed {f.ChargeSpeed} outside a special ({f.State})";
			if (f.BraceLead != 0 && f.ActiveSpecial == SpecialSlot.None) return $"BraceLead {f.BraceLead} outside a special";
			if (f.X < m.CornerLo(f) || f.X > m.CornerHi(f)) return $"X {f.X} outside the stage";
		}
		return "";
	}

	// A: A8 (rules E16) / determinism: a held trigger must not re-fire the special when it ends or auto-releases.
	[Test]
	public static void HeldTrigger_AfterAutoRelease_DoesNotFireASecondSpecial()
	{
		var m = Setup();
		int started = 0;
		m.SpecialStarted += (mm, e) => { if (e.Fighter == 0) started++; };
		for (int t = 1; t <= 220; t++)
		{
			m.Step(t == 1 ? new FighterInput(InputBits.Special | InputBits.Right) : new FighterInput(InputBits.Special), Idle);
			Assert.True(Violations(m) == "", $"tick {t}: {Violations(m)}");
		}
		Assert.Equal(1, started, "one press, one special, however long Special stays down (E16)");
	}

	// Invariants under random play (both fighters carry the charge, hits, throws, jumps, bursts included).
	[Test]
	public static void Fuzz_BraceStateNeverLeaks()
	{
		for (int seed = 1; seed <= 12; seed++)
		{
			var m = Setup(p1x: -60 - 10 * seed, p2x: 60 + 10 * seed, bothCharge: true);
			m.P1.Meter = m.P2.Meter = 1000;
			var rng = new SimRng((uint)(9000 + seed));
			InputBits h1 = 0, h2 = 0;
			for (int t = 0; t < 2500 && m.Phase != MatchPhase.Over; t++)
			{
				FighterInput In(ref InputBits held, Fighter f)
				{
					int r = rng.Next(14);
					int fwd = f.Facing > 0 ? (int)InputBits.Right : (int)InputBits.Left;
					if (r == 0) held = InputBits.Special | (InputBits)fwd | (rng.Next(3) == 0 ? InputBits.LightPunch : 0);
					else if (r == 1) held = InputBits.LightPunch | InputBits.LightKick; // throw
					else if (r < 9 && (held & InputBits.Special) != 0) held = InputBits.Special;
					else if (r == 9) held = InputBits.Up | (InputBits)fwd;
					else held = (InputBits)(rng.NextUInt() & (uint)(InputBits.Directions | InputBits.Attacks));
					return new FighterInput(held);
				}
				m.Step(In(ref h1, m.P1), In(ref h2, m.P2));
				string v = Violations(m);
				Assert.True(v == "", $"seed {seed} tick {t}: {v}");
			}
		}
	}

	// A hit that lands while the special's startup frame 1 plays (release tick) ends the dash: no movement in hitstun, speed cleared.
	[Test]
	public static void HitOnTheReleaseTick_ClearsTheDash_AtEveryOffset()
	{
		for (int offset = 0; offset < 16; offset++)
		{
			var m = Setup(p1x: -90, p2x: 90);
			int lp = Array.FindIndex(m.P2.Moves, mv => mv.Button == InputBits.LightPunch && !mv.Air);
			for (int t = 1; t <= 60; t++)
			{
				m.Step(KihonHold(t, 6), t == 1 + offset ? new FighterInput(InputBits.None, (byte)(lp + 1)) : Idle);
				Assert.True(Violations(m) == "", $"offset {offset} tick {t}: {Violations(m)}");
				if (m.P1.State is FighterState.Hitstun or FighterState.Blockstun or FighterState.Knockdown)
					Assert.Equal(0, m.P1.ChargeSpeed, $"offset {offset} tick {t}: a stunned fighter carries no dash");
			}
		}
	}

	// KO while holding: no special afterwards, brace cleared; then a new round (Reset) hashes like a fresh match.
	[Test]
	public static void KoWhileBracing_ThenReset_LeavesNothing()
	{
		var fresh = Setup(p1x: -60, p2x: 60);
		var m = Setup(p1x: -60, p2x: 60);
		m.P1.Health = 1;
		int lp = Array.FindIndex(m.P2.Moves, mv => mv.Button == InputBits.LightPunch && !mv.Air);
		bool started = false;
		m.SpecialStarted += (mm, e) => started |= e.Fighter == 0;
		for (int t = 1; t <= 120; t++)
		{
			m.Step(KihonHold(t, 60), t == 2 ? new FighterInput(InputBits.None, (byte)(lp + 1)) : Idle);
			Assert.True(Violations(m) == "", $"tick {t}: {Violations(m)}");
		}
		Assert.True(m.P1.KnockedOut, "the lethal hit landed on the bracing fighter");
		Assert.True(!started, "a KO'd fighter starts no special (E16)");
		m.Reset();
		fresh.Reset();
		m.P1.X = fresh.P1.X; m.P2.X = fresh.P2.X;
		Assert.Equal(fresh.StateHash(), m.StateHash(), "after Reset the state equals a new match's");
	}

	// Round reset in the middle of a hold must not leave the brace behind.
	[Test]
	public static void ResetMidHold_HashesLikeAFreshMatch()
	{
		var m = Setup();
		for (int t = 1; t <= 8; t++) m.Step(KihonHold(t, 20), Idle);
		Assert.Equal(FighterState.Bracing, m.P1.State, "holding");
		m.Reset();
		var fresh = Setup();
		fresh.Reset();
		m.Reset();
		Assert.Equal(fresh.StateHash(), m.StateHash(), "Reset clears the hold");
		for (int t = 1; t <= 40; t++)
		{
			m.Step(Idle, Idle);
			fresh.Step(Idle, Idle);
			Assert.Equal(fresh.StateHash(), m.StateHash(), $"idle tick {t}: no residue of the hold");
		}
	}

	// A dash that runs into a cornered opponent must neither tunnel through nor leave the stage.
	[Test]
	public static void Dash_IntoACorneredOpponent_StaysOnTheNearSide()
	{
		foreach (int side in new[] { 1, -1 })
		{
			var m = Setup();
			int corner = side > 0 ? m.CornerHi(m.P2) : m.CornerLo(m.P2);
			m.P2.X = corner;
			m.P1.X = corner - side * 30 * S;
			m.P1.Facing = side;
			m.P2.Facing = -side;
			var input = side > 0 ? KihonHold(1, 20) : new FighterInput(InputBits.Special | InputBits.Left);
			for (int t = 1; t <= 40; t++)
			{
				var cur = side > 0 ? KihonHold(t, 20) : t == 1 ? input : t <= 21 ? new FighterInput(InputBits.Special) : Idle;
				m.Step(cur, Idle);
				Assert.True(Violations(m) == "", $"side {side} tick {t}: {Violations(m)}");
				Assert.True(side * (m.P2.X - m.P1.X) > 0, $"side {side} tick {t}: P1 did not pass through P2 ({m.P1.X} vs {m.P2.X})");
			}
		}
	}

	// F3 / AM11: a dash is forward and the sim is integer. A speed the loader accepts must not wrap the position backwards.
	[Test]
	public static void HugeSpeed_DoesNotOverflowIntoABackwardsDash()
	{
		string huge = Charge(units: "30000000", cap: 30000000); // 30,000,000 * 100 centi-units overflows int32
		var m = Setup(huge, p1x: 0, p2x: 900);
		int x0 = m.P1.X;
		for (int t = 1; t <= 8; t++) m.Step(KihonHold(t, 0), Idle);
		Assert.True(m.P1.X >= x0, $"a forward dash never moves the fighter back (x {x0} -> {m.P1.X})");
	}

	// Control for the overflow probe: the largest speed whose centi-unit step still fits int32 must go forward (into the opponent, who is pushed to the wall).
	[Test]
	public static void LargestNonOverflowingSpeed_StillDashesForward()
	{
		var m = Setup(Charge(units: "20000000", cap: 20000000), p1x: 0, p2x: 900);
		int x0 = m.P1.X;
		for (int t = 1; t <= 8; t++) m.Step(KihonHold(t, 0), Idle);
		Assert.True(m.P1.X > x0, $"forward (x {x0} -> {m.P1.X}, P2 {m.P2.X})");
	}

	// A legal-looking speed (a few stage widths per tick) must also stay forward.
	[Test]
	public static void StageWidthSpeed_StaysForward_AndInBounds()
	{
		var m = Setup(Charge(units: "5000", cap: 5000), p1x: -300, p2x: 900);
		int x0 = m.P1.X;
		for (int t = 1; t <= 8; t++) m.Step(KihonHold(t, 0), Idle);
		Assert.True(m.P1.X > x0 && Violations(m) == "", $"forward and in bounds (x {x0} -> {m.P1.X}, P2 {m.P2.X}) {Violations(m)}");
	}

	// X1/X3: cancelling a special into a charge special while the trigger is held brace-holds it, leaves no dead hitbox, then dashes.
	[Test]
	public static void CancelIntoACharge_Braces_ThenDashes()
	{
		var m = Setup(p1x: -700, p2x: 700);
		Assert.True(m.P1.HoldCancelRule(new CancelRule("test-cancel", CancelFrom.Specials, CancelInto.Specials, true, true, true)), "rule held");
		// Spirit Wave (slot A, Kihon 8) cancels on frames 14-20; then Special + forward = slot B (charge).
		m.Step(new FighterInput(InputBits.Special | InputBits.Up), Idle);
		for (int t = 0; t < 13; t++) m.Step(Idle, Idle);
		Assert.Equal(FighterState.Attack, m.P1.State, "Spirit Wave playing");
		m.Step(new FighterInput(InputBits.Special | InputBits.Right), Idle);
		Assert.Equal(FighterState.Bracing, m.P1.State, "cancelled into the charge special: bracing");
		Assert.True(m.P1.CurrentMove is null, "no leftover move while bracing");
		int x = m.P1.X;
		for (int t = 0; t < 10; t++) m.Step(new FighterInput(InputBits.Special), Idle);
		Assert.Equal(x, m.P1.X, "no movement while holding");
		m.Step(Idle, Idle); // release
		Assert.Equal(FighterState.Attack, m.P1.State, "special plays on release");
		Assert.Equal(SpecialSlot.B, m.P1.ActiveSpecial, "slot B");
		Assert.Equal(18, m.P1.ChargeSpeed, "10 ticks held = step 1");
		Assert.True(Violations(m) == "", Violations(m));
	}

	// Loader and validator must agree on odd charge JSON. Validator verdicts below come from tools/validate_data.py run on the same documents.
	static JsonObject Doc() => JsonNode.Parse(Charge())!.AsObject();
	static bool Loads(Action<JsonObject> mutate)
	{
		var d = Doc();
		mutate(d);
		try
		{
			var mod = ModifierLoader.Parse(d.ToJsonString());
			var m = new Match(null, Moves(), Moves());
			foreach (var f in m.Fighters) FightScene.EquipStarters(f, Fixtures());
			m.P1.AttachModifier(SpecialSlot.B, mod, m.Config); // builds every level, plain and EX, as the game does
			return true;
		}
		catch (Exception e) when (e is FormatException or InvalidOperationException or ArgumentException or KeyNotFoundException or System.Text.Json.JsonException) { return false; }
	}

	[Test]
	public static void Loader_AgreesWithTheValidator_OnOddChargeJson()
	{
		var cases = new List<(string Name, bool Validator, Action<JsonObject> Mut)>
		{
			("recovery-overflow-released", true, d => d["effects"]!.AsArray().Add(JsonNode.Parse("""{"target":"recovery","op":"add","value":2147483600,"when":"charge_released"}"""))),
			("recovery-below-zero-released", false, d => d["effects"]!.AsArray().Add(JsonNode.Parse("""{"target":"recovery","op":"add","value":-9999,"when":"charge_released"}"""))),
			("damage-on-release", true, d => d["effects"]!.AsArray().Add(JsonNode.Parse("""{"target":"damage","op":"add","value":5,"when":"charge_released"}"""))),
			("ex-cost-released", false, d => d["effects"]!.AsArray().Add(JsonNode.Parse("""{"target":"ex.meter_cost","op":"add","value":5,"when":"charge_released"}"""))),
			("ex-cost-always", true, d => d["effects"]!.AsArray().Add(JsonNode.Parse("""{"target":"ex.meter_cost","op":"set","value":50}"""))),
			("two-dash-effects", true, d => d["effects"]!.AsArray().Add(d["effects"]![0]!.DeepClone())),
			("dash-effect-extra-key", false, d => d["effects"]![0]!["foo"] = 1),
			("dash-effect-when-holding-up", false, d => d["effects"]![0]!["when"] = "holding_up"),
			("huge-speed", true, d => { var dash = d["charge"]!["dash"]!; dash["units_per_tick_by_step"] = JsonNode.Parse("[2000000000]"); dash["cap_units_per_tick"] = 2000000000; }),
			("window-max-huge", true, d => d["charge"]!["window_ticks"]!["max"] = 2000000000),
			("min-eq-max", true, d => d["charge"]!["window_ticks"]!["min"] = 30),
			("step-written-10.0", true, d => d["charge"]!["step_ticks"] = JsonNode.Parse("10.0")),
			("no-hold-pose", true, d => d["charge"]!.AsObject().Remove("hold_pose_clip")),
			("window-status-key", true, d => d["charge"]!["window_ticks"]!["status"] = "proposed"),
			("dash-status-key", true, d => d["charge"]!["dash"]!["status"] = "proposed"),
			("window-extra-key", false, d => d["charge"]!["window_ticks"]!["foo"] = 1),
			("dash-extra-key", false, d => d["charge"]!["dash"]!["foo"] = 1),
			("charge-null", false, d => d["charge"] = null),
			("charge-array", false, d => d["charge"] = new JsonArray()),
			("min-bool", false, d => d["charge"]!["window_ticks"]!["min"] = false),
			("units-bool", false, d => d["charge"]!["dash"]!["units_per_tick_by_step"] = JsonNode.Parse("[true]")),
			("units-string", false, d => d["charge"]!["dash"]!["units_per_tick_by_step"] = JsonNode.Parse("[\"14\"]")),
			("no-cap", false, d => d["charge"]!["dash"]!.AsObject().Remove("cap_units_per_tick")),
			("excludes-bogus", false, d => d["applies_to"]!["excludes"] = JsonNode.Parse("[\"bogus\"]")),
			("requires-and-excludes-same", true, d => { d["applies_to"]!["requires"] = JsonNode.Parse("[\"projectile\"]"); }),
			("excludes-all", true, d => d["applies_to"]!["excludes"] = JsonNode.Parse("[\"throw\",\"armour\",\"low\",\"anti_air\",\"projectile\"]")),
		};
		var wrong = new List<string>();
		foreach (var (name, validator, mut) in cases)
			if (Loads(mut) != validator) wrong.Add($"{name}: validator {(validator ? "accepts" : "rejects")}, loader {(validator ? "rejects" : "accepts")}");
		Assert.True(wrong.Count == 0, string.Join("; ", wrong));
	}

	// Bot hold plan (AM11): a hold longer than the window max is auto-released at the max, the button staying down must not start another special.
	[Test]
	public static void Bot_HoldLongerThanTheWindow_ReleasesAtMax_AndFiresOncePerPlan()
	{
		var content = HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
		var baseBot = BotConfig.LoadDefault(ContentPaths.DataDir);
		var cfg = baseBot.Fight with
		{
			WalkWeight = 0, NormalWeight = 0, SpecialWeight = 1, ThrowWeight = 0, JumpWeight = 0, WaitWeight = 0,
			MistakePct = 0, BlockPct = 0, AntiAirPct = 0, IdleGuardPct = 0, SpecialHoldTicks = 45,
		};
		var session = RunSession.Start(content.Pool, content.Map, content.Scenes, 5);
		Assert.True(session.Enter(session.Reachable()[0]).Ok, "enter row 1");
		var m = NodeFight.NewMatch(content.Sources, session.Run, session, session.Node!.Fighter, ControlScheme.Kihon);
		var mod = ModifierLoader.Parse(Charge());
		for (int s = (int)SpecialSlot.A; s <= (int)SpecialSlot.D; s++)
			if (m.P1.Specials[s] is { } eq && mod.AppliesTo(eq.Data)) m.P1.AttachModifier((SpecialSlot)s, mod, m.Config);
		var bot = new NewcomerBot(cfg, BotKit.From(m.P1), 3);
		int maxHeld = 0, started = 0;
		var speeds = new HashSet<int>();
		m.SpecialStarted += (mm, e) => { if (e.Fighter == 0) started++; };
		for (int t = 0; t < 1500 && m.Phase == MatchPhase.Fighting; t++)
		{
			m.Step(bot.Next(AiView.Capture(m)), Idle);
			maxHeld = Math.Max(maxHeld, m.P1.BraceHeld);
			if (m.P1.ChargeSpeed > 0) speeds.Add(m.P1.ChargeSpeed);
			Assert.True(Violations(m) == "", $"tick {t}: {Violations(m)}");
		}
		Assert.Equal(29, maxHeld, "the bot's 45-tick hold is cut by the fight at the window max (BraceHeld reaches 29, the 30th tick releases)");
		Assert.True(speeds.All(v => v == 26), $"a capped hold dashes at the top speed ({string.Join(",", speeds)})");
		Assert.True(started <= bot.Stats.Specials, $"one special per bot plan ({started} started, {bot.Stats.Specials} planned)");
	}

	// Bot config boundaries: 0 and 600 load, 601 and -1 do not.
	[Test]
	public static void BotConfig_SpecialHoldTicks_Boundaries()
	{
		string baseJson = System.IO.File.ReadAllText(System.IO.Path.Combine(ContentPaths.DataDir, "harness", BotConfig.DefaultId + ".json"));
		string With(string v)
		{
			var o = JsonNode.Parse(baseJson)!.AsObject();
			o["fight"]!["special_hold_ticks"] = JsonNode.Parse(v);
			return o.ToJsonString();
		}
		Assert.Equal(0, BotConfig.Parse(With("0")).Fight.SpecialHoldTicks, "0 loads");
		Assert.Equal(600, BotConfig.Parse(With("600")).Fight.SpecialHoldTicks, "600 loads");
		foreach (string bad in new[] { "601", "-1", "1.5", "\"20\"" })
		{
			bool threw = false;
			try { BotConfig.Parse(With(bad)); } catch (Exception e) when (e is FormatException or InvalidOperationException or ArgumentException or KeyNotFoundException) { threw = true; }
			Assert.True(threw, $"special_hold_ticks {bad} is refused");
		}
	}

	// Release before min while the late EX button comes: plain build, no dash, and the EX still spends its meter once.
	[Test]
	public static void BelowMinRelease_WithEx_StillSpendsOnce_NoDash()
	{
		var m = Setup(Charge(min: 5));
		m.P1.Meter = 300;
		for (int t = 1; t <= 3; t++) m.Step(t == 1 ? new FighterInput(InputBits.Special | InputBits.Right | InputBits.LightPunch) : t == 2 ? new FighterInput(InputBits.Special) : Idle, Idle);
		Assert.Equal(0, m.P1.ChargeSpeed, "held 1 < min 5: no dash");
		Assert.True(m.P1.ActiveEx, "EX build (E16)");
		int spent = 300 - m.P1.Meter;
		Assert.True(spent > 0, "meter spent");
		for (int t = 0; t < 60; t++) m.Step(Idle, Idle);
		Assert.Equal(300 - spent, m.P1.Meter, "spent once, not again by a later upgrade");
	}
}
