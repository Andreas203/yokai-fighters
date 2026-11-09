using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// Test Breaker probes for YOK-221 (properties.forward_travel). The special is a TEST FIXTURE like ForwardTravelTests':
/// 12/6/14 frames, 300 units over the active frames unless a probe says otherwise. Expected values come from the rules
/// named on each probe (E1 walls and screen, C2 jumper passes, E10 drift, E12 throws, F3 determinism, T0/T4 the copy).
/// </summary>
public static class ForwardTravelBreakerProbes
{
	const int S = SimConfig.Scale;
	const int Startup = 12, Active = 6, Recovery = 14, Total = Startup + Active + Recovery;
	static readonly FighterInput Idle = FighterInput.None;
	static readonly FighterInput Go = FighterInput.Special(SpecialSlot.B);

	static string Special(int travel = 300, string hitRect = "", string id = "bp-forward-charge") => $$"""
		{
		  "kind": "special", "id": "{{id}}", "name": "TEST FIXTURE: forward charge (not game content)", "status": "proposed",
		  "source": "oni", "starter": false, "rarity": "common", "locked": false, "clip": "oni-heavy-punch", "rules": ["A10"],
		  "input": { "kata": { "slot": "B", "motion": "623" }, "kihon": { "slot": "B", "direction": "forward" } },
		  "card": { "plain": "Test.", "frames": "Test." },
		  "levels": {
		    "1": { "frame_data": {
		      "startup": {{Startup}}, "active": {{Active}}, "recovery": {{Recovery}}, "damage": 110, "hitstun": 20, "blockstun": 14,
		      "hitboxes": [{{(hitRect.Length > 0 ? "{ \"frames\": [" + (Startup + 1) + ", " + (Startup + 1) + "], \"rect\": " + hitRect + " }" : "")}}],
		      "hurtboxes": [{ "frames": [1, {{Total}}], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } }],
		      "properties": { "forward_travel": {{travel}} } } },
		    "2": { "status": "proposed", "tuning": { "target": "damage", "op": "mul_pct", "value": 110 } },
		    "3": { "evolution": { "id": "bp-crush", "name": "Test Crush", "preset": "heaven-seal",
		      "changes": [{ "target": "damage", "op": "add", "value": 10 }], "card": { "plain": "Test.", "frames": "Test." } } }
		  }
		}
		""";

	const string SumoCharge = """
		{
		  "kind": "modifier", "id": "bp-charge", "name": "Test Charge", "source": "kappa", "rarity": "common", "locked": false,
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

	/// <param name="charge">Which fighters carry the Sumo Stance charge on slot B (bit 0 = P1, bit 1 = P2).</param>
	static Match Setup(int t1, int t2, int p1x, int p2x, int charge = 0, MoveData[]? moves = null)
	{
		var mv = moves ?? Moves();
		var m = new Match(null, mv, mv);
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Fixtures());
		if (t1 >= 0) m.P1.Equip(SpecialSlot.B, SpecialLoader.Parse(Special(t1, id: "bp-a")));
		if (t2 >= 0) m.P2.Equip(SpecialSlot.B, SpecialLoader.Parse(Special(t2, id: "bp-b")));
		for (int i = 0; i < 2; i++)
			if ((charge >> i & 1) == 1)
			{
				m.Fighters[i].AttachModifier(SpecialSlot.B, ModifierLoader.Parse(SumoCharge), m.Config);
				m.Fighters[i].Input.Scheme = ControlScheme.Kihon;
			}
		m.P1.X = p1x;
		m.P2.X = p2x;
		return m;
	}

	/// <summary>Fighter <paramref name="who"/> acts at tick <paramref name="at"/>: a Kihon forward + Special then a hold when charged, else the direct request.</summary>
	static FighterInput Act(Match m, int who, int t, int at, int hold)
	{
		if (t < at) return Idle;
		var f = m.Fighters[who];
		InputBits fwd = who == 0 ? InputBits.Right : InputBits.Left;
		if (f.Input.Scheme == ControlScheme.Kihon)
			return t == at ? new FighterInput(InputBits.Special | fwd) : t <= at + hold ? new FighterInput(InputBits.Special) : Idle;
		return t == at ? Go : Idle;
	}

	static void Invariants(Match m, string where)
	{
		int gap = m.PushGap(m.P1, m.P2);
		if (m.P1.Y == 0 && m.P2.Y == 0)
			Assert.True(m.P2.X - m.P1.X >= gap, $"{where}: bodies overlap or crossed (P1 {m.P1.X}, P2 {m.P2.X}, gap {gap})");
		foreach (var f in m.Fighters)
			Assert.True(f.X >= m.CornerLo(f) && f.X <= m.CornerHi(f), $"{where}: fighter outside its corners ({f.X})");
		Assert.True(Math.Abs(m.P2.X - m.P1.X) <= m.Config.MaxSeparation || m.P1.Y != 0 || m.P2.Y != 0, $"{where}: past the screen wall (E1)");
	}

	// E1 (walls, screen), C2 (grounded bodies don't pass through each other): whatever mix of travel, dash and start
	// distance, nobody ends a tick overlapping, crossed or outside the stage; and the run is a pure function (F3).
	[Test]
	public static void Sweep_TravelAndDashMixes_NeverOverlapCrossOrLeaveTheStage_AndAreDeterministic()
	{
		var travels = new[] { (300, -1), (-1, 300), (300, 300), (301, 77), (7, 301), (300, 0), (0, 300), (int.MaxValue, int.MaxValue) };
		var starts = new[] { (-150, 50), (-100, 100), (-60, 60), (-451, 449), (-30, 30) };
		int runs = 0;
		foreach (var (t1, t2) in travels)
		foreach (int charge in new[] { 0, 1, 2, 3 })
		foreach (var (a, b) in starts)
		foreach (int offset in new[] { 0, 1, 37, 99 })
		foreach (int p2at in new[] { 1, 5, 14 })
		{
			if (t1 < 0 && t2 < 0) continue;
			if (((charge & 1) == 1 && t1 < 0) || ((charge & 2) == 2 && t2 < 0)) continue;
			List<ulong> Play()
			{
				var m = Setup(t1, t2, a * S + offset, b * S + offset, charge);
				var hs = new List<ulong>();
				for (int t = 1; t <= Total + 40; t++)
				{
					m.Step(t1 >= 0 ? Act(m, 0, t, 1, 12) : Idle, t2 >= 0 ? Act(m, 1, t, p2at, 20) : Idle);
					Invariants(m, $"travel {t1}/{t2} charge {charge} start {a}/{b}+{offset} p2@{p2at} tick {t}");
					hs.Add(m.StateHash());
				}
				return hs;
			}
			var first = Play();
			Assert.True(first.SequenceEqual(Play()), $"travel {t1}/{t2} charge {charge} start {a}/{b}+{offset}: same inputs, different hashes (F3)");
			runs++;
		}
		Assert.True(runs > 300, $"the sweep ran ({runs})");
	}

	// E1 symmetry: a lone traveller (nothing else moves, so ResolvePositions' own P1-first odd split stays out of it)
	// ends on mirrored squares whichever side it plays, from any odd centi-unit start, the corners included.
	[Test]
	public static void MirroredTraveller_GivesMirroredPositions()
	{
		int half = new SimConfig().StageHalfWidth;
		var starts = new List<(int x1, int x2)>();
		for (int g = 0; g < 70; g += 7) starts.Add((-9000 + g, -9000 + 12000 + g * 11));
		for (int g = 0; g < 70; g += 7) starts.Add((half - 60 * S - 9100 - g, half - 60 * S - 100 - g));
		starts.Add((-half + 6000, half - 6000));
		foreach (var (x1, x2) in starts)
		foreach (int travel in new[] { 301, 1, 99999 })
		{
			var a = Setup(travel, -1, x1, x2);
			var b = Setup(-1, travel, -x2, -x1);
			for (int t = 1; t <= Total; t++)
			{
				a.Step(t == 1 ? Go : Idle, Idle);
				b.Step(Idle, t == 1 ? Go : Idle);
				Assert.Equal(-b.P2.X, a.P1.X, $"travel {travel} from {x1}/{x2}, tick {t}: mirrored mover");
				Assert.Equal(-b.P1.X, a.P2.X, $"travel {travel} from {x1}/{x2}, tick {t}: mirrored bystander");
			}
		}
	}

	// F3 / frame data: TravelAt on an active window of a billion frames must neither overflow nor leave a +-1 spread.
	[Test]
	public static void TravelAt_HugeActive_DoesNotOverflow()
	{
		const int n = 1_000_000_000;
		var json = Special(int.MaxValue).Replace($"\"active\": {Active}", $"\"active\": {n}").Replace($"[1, {Total}]", "[1, 1000000026]");
		MoveData m;
		try { m = MoveLoader.Parse(json); }
		catch (FormatException) { return; } // refusing a move this long is fine; wrapping is not
		long total = (long)int.MaxValue * S, lo = total / n;
		foreach (int k in new[] { 1, 2, 3, 1000, n / 2, n - 1, n })
		{
			long step = m.TravelAt(Startup + k);
			Assert.True(step == lo || step == lo + 1, $"k={k}: step {step} outside [{lo}, {lo + 1}]");
		}
	}

	// C2 (a jumper passes) + E10 (drift 8 units per frame): an air normal with forward_travel adds its share on top of the
	// drift and is not stopped by a grounded fighter underneath (push boxes only count between grounded fighters).
	[Test]
	public static void AirNormal_Travel_AddsToDrift_AndPassesAGroundedOpponent()
	{
		var node = JsonNode.Parse(System.IO.File.ReadAllText(
			ProjectSettings.GlobalizePath(FightScene.FixtureAirNormalsDir) + "/test-ryo-air-punch.json"))!;
		node["frame_data"]!["hitboxes"] = new JsonArray();
		node["frame_data"]!["properties"] = new JsonObject { ["forward_travel"] = 600 };
		var air = MoveLoader.Parse(node.ToJsonString());
		Assert.Equal(600, air.ForwardTravel, "the air normal reads forward_travel");

		List<(int x, int y, int frame)> Fly(int p2x, int travelled)
		{
			var a = air with { ForwardTravel = travelled };
			var mv = Moves().Append(a).ToArray();
			int slot = mv.Length - 1;
			var m = new Match(null, mv, mv);
			m.P1.X = -300 * S;
			m.P2.X = p2x;
			var trace = new List<(int, int, int)>();
			for (int t = 1; t <= 30; t++)
			{
				var input = t == 1 ? new FighterInput(InputBits.Up | InputBits.Right) : t == 4 ? FighterInput.Attack(slot) : Idle;
				m.Step(input, Idle);
				trace.Add((m.P1.X, m.P1.Y, m.P1.State == FighterState.Attack ? m.P1.MoveFrame : 0));
			}
			return trace;
		}
		var control = Fly(900 * S, 0);
		var far = Fly(900 * S, 600);
		var under = Fly(-300 * S + 60 * S, 600);
		int travelling = 0;
		for (int t = 1; t < 30; t++)
		{
			if (far[t].y == 0 || far[t - 1].y == 0) continue;
			int dFar = far[t].x - far[t - 1].x, dControl = control[t].x - control[t - 1].x;
			long share = far[t].frame > 0 ? air.TravelAt(far[t].frame) : 0;
			if (share > 0) travelling++;
			Assert.Equal(dControl + (int)share, dFar, $"tick {t + 1}: the drift plus the move's share on frame {far[t].frame}");
			Assert.Equal(dFar, under[t].x - under[t - 1].x, $"tick {t + 1}: a grounded fighter under the jumper changes nothing in the air (C2)");
		}
		Assert.True(travelling >= Active - 1, $"the air normal travelled on its active frames ({travelling})");
	}

	// E12 + E1: a throw with forward_travel (the thrower keeps travelling through the break window) never overlaps the
	// grabbed fighter and never moves it before the throw lands; two travelling throws clashing on the same frame likewise.
	[Test]
	public static void ThrowWithTravel_NeverOverlapsTheGrabbed_AndClashBothTravelling()
	{
		var node = JsonNode.Parse(System.IO.File.ReadAllText(
			ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir) + "/test-ryo-throw.json"))!;
		node["frame_data"]!["properties"] = new JsonObject { ["forward_travel"] = 400 };
		var throwMove = MoveLoader.Parse(node.ToJsonString());
		var moves = MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir)).Append(throwMove).ToArray();
		var grab = new FighterInput(InputBits.LightPunch | InputBits.LightKick);
		foreach (int gap in new[] { 90, 120, 121, 150, 199, 240, 300 })
		foreach (bool both in new[] { false, true })
		foreach (int breakAt in new[] { 0, 7, 9 })
		{
			var m = Setup(-1, -1, -gap * S / 2, gap * S / 2 + 7, 0, moves);
			for (int t = 1; t <= 60; t++)
			{
				int x2 = m.P2.X;
				bool thrown = m.P2.State == FighterState.Thrown;
				var in2 = both && t == 1 ? grab : breakAt > 0 && t == breakAt ? grab : Idle;
				m.Step(t == 1 ? grab : Idle, in2);
				Invariants(m, $"throw gap {gap} both {both} break {breakAt} tick {t}");
				if (thrown && m.P2.State == FighterState.Thrown && !both)
					Assert.Equal(x2, m.P2.X, $"gap {gap} tick {t}: the grabbed fighter is not pushed by the thrower's travel");
			}
		}
	}

	// F3, hitstop (V2): travel runs on world frames only. A hit on the first active frame freezes both fighters for the
	// hitstop; the traveller then still covers exactly D, one share per world frame, none repeated, none dropped.
	[Test]
	public static void Hitstop_FreezesTravel_AndTheTotalStaysD()
	{
		var m = Setup(-1, -1, -700 * S, -300 * S);
		var special = SpecialLoader.Parse(Special(300, hitRect: "{ \"x\": 20, \"y\": 100, \"w\": 600, \"h\": 200 }", id: "bp-hitter"));
		m.P1.Equip(SpecialSlot.B, special);
		int x0 = m.P1.X, frozen = 0;
		bool hit = false;
		m.Hit += (_, _) => hit = true;
		for (int t = 1; t <= Total + 30; t++)
		{
			int before = m.P1.X, wf = m.WorldFrame;
			m.Step(t == 1 ? Go : Idle, Idle);
			if (m.WorldFrame == wf)
			{
				frozen++;
				Assert.Equal(before, m.P1.X, $"tick {t}: in hitstop the traveller does not move");
			}
			else if (m.P1.State == FighterState.Attack)
				Assert.Equal(m.P1.ActiveMove!.TravelAt(m.P1.MoveFrame), (long)(m.P1.X - before),
					$"tick {t}: frame {m.P1.MoveFrame} moves exactly its own share");
		}
		Assert.True(hit && frozen > 0, $"the fixture hit and froze the world (hit {hit}, frozen ticks {frozen})");
		Assert.Equal(300 * S, m.P1.X - x0, "hitstop in the middle of the move does not change the distance travelled");
	}

	// DebugCrossUp (the side-switch placeholder) while the other fighter travels: still no overlap.
	[Test]
	public static void Crossup_WhileTheOpponentTravels_NeverOverlaps()
	{
		foreach (int who in new[] { 0, 1 })
		foreach (int crossAt in new[] { 2, 5, 14, 15, 17, 18 })
		foreach (int gap in new[] { 130, 160, 301 })
		{
			var m = Setup(300, 300, -gap * S / 2, gap * S / 2 + 3);
			for (int t = 1; t <= Total + 5; t++)
			{
				var p1 = who == 0 ? Act(m, 0, t, 1, 0) : (t == crossAt ? new FighterInput(InputBits.DebugCrossUp) : Idle);
				var p2 = who == 1 ? Act(m, 1, t, 1, 0) : (t == crossAt ? new FighterInput(InputBits.DebugCrossUp) : Idle);
				m.Step(p1, p2);
				Assert.True(Math.Abs(m.P2.X - m.P1.X) >= m.PushGap(m.P1, m.P2), $"crossup by P{2 - who} at {crossAt}, gap {gap}, tick {t}: bodies overlap (P1 {m.P1.X}, P2 {m.P2.X})");
			}
		}
	}

	// T0/T4: the Tanuki copies the special at Ryo's level with the move's own data: the same distance at levels 1, 2 and 3,
	// and a Ryo-side charge modifier gives it no dash (T4: never his modifiers).
	[Test]
	public static void TanukiCopy_TravelsTheSame_AtEveryLevel_AndGetsNoDash()
	{
		var special = SpecialLoader.Parse(Special());
		var ryo = Kit.Load(Kit.Ryo, FightScene.FixtureSources);
		foreach (int level in new[] { 1, 2, 3 })
		{
			var tanuki = TanukiKit.Build(ryo, new CopyTarget(SpecialSlot.B, special, level));
			var m = new Match(null, ryo.Moves, tanuki.Moves);
			ryo.Equip(m.P1);
			tanuki.Equip(m.P2);
			m.P1.Equip(SpecialSlot.B, special);
			m.P1.AttachModifier(SpecialSlot.B, ModifierLoader.Parse(SumoCharge), m.Config);
			m.P1.X = -500 * S;
			m.P2.X = 500 * S;
			for (int t = 1; t <= Total; t++) m.Step(Idle, t == 1 ? Go : Idle);
			Assert.Equal(200 * S, m.P2.X, $"level {level}: the copy travels 300 units toward Ryo, no charge dash on top (T4)");
			Assert.Equal(0, m.P2.ChargeSpeed, $"level {level}: no dash speed carried over");
		}
	}

	// A charge special that has ended must not dash a later normal (F3: nothing leaks from the last move).
	[Test]
	public static void ChargeDash_DoesNotLeakIntoTheNextMove()
	{
		var m = Setup(0, -1, -800 * S, 800 * S, 1);
		for (int t = 1; t <= 40 + Total; t++) m.Step(Act(m, 0, t, 1, 0), Idle);
		Assert.Equal(FighterState.Idle, m.P1.State, "the special is over");
		int x = m.P1.X;
		for (int t = 0; t < 30; t++) m.Step(FighterInput.Attack(0), Idle);
		Assert.Equal(x, m.P1.X, "a normal after a charge special stays put");
	}

	// Loader half of tools/tests/test_forward_travel_probes.py: the same values, the same verdicts.
	[Test]
	public static void Loader_AgreesWithTheValidatorOnForwardTravelValues()
	{
		var values = new[]
		{
			("300", true), ("300.0", true), ("3e2", true), ("0", true), ("0.0", true), ("-0", true), ("-0.0", true),
			("2147483647", true), ("2147483648", false), ("-1", false), ("2.5", false), ("\"300\"", false), ("true", false),
			("[]", false), ("{}", false), ("1e400", false), ("300.0000000000000000001", false),
		};
		var wrong = new List<string>();
		foreach (var (raw, accepts) in values)
		{
			bool ok;
			try { MoveLoader.Parse(Special(0).Replace("\"forward_travel\": 0", $"\"forward_travel\": {raw}")); ok = true; }
			catch (FormatException) { ok = false; }
			catch (System.Text.Json.JsonException) { ok = false; }
			if (ok != accepts) wrong.Add($"{raw}: loader {(ok ? "accepts" : "refuses")}");
		}
		Assert.True(wrong.Count == 0, "loader verdicts differ from the list the validator probe uses: " + string.Join("; ", wrong));
	}
}
