using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Replay;

namespace YokaiFighters.Tests;

/// <summary>YOK-103 round 4 probes: loader vocabulary, whole numbers, Describe, ApplyHit order, cancels, determinism.</summary>
public static class AbilityLoaderEffectsProbes
{
	const int S = SimConfig.Scale;
	static readonly FighterInput Idle = FighterInput.None;
	static string Repo => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static ModifierData Mod(string id) => ModifierLoader.LoadFile($"{Repo}/data/modifiers/{id}.json");
	static CancelRule Cancel(string id) => CancelRuleLoader.LoadFile($"{Repo}/data/cancels/{id}.json");
	static SpecialData[] Fixtures() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));
	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();
	static int Slot(Match m, Func<MoveData, bool> pick) => Array.FindIndex(m.P1.Moves, mv => pick(mv));
	static int Lp(Match m) => Slot(m, mv => mv.Button == InputBits.LightPunch && !mv.Air);
	static int Hp(Match m) => Slot(m, mv => mv.Button == InputBits.HeavyPunch && !mv.Air);
	static int Throw(Match m) => Slot(m, mv => mv.IsThrow);

	static string ModJson(string effect) =>
		"{\"kind\":\"modifier\",\"id\":\"x\",\"source\":\"oni\",\"applies_to\":{\"requires\":[]},\"effects\":[" + effect + "]}";
	static string Eff(string target, string op, string value, string? when = null) =>
		$"{{\"target\":{target},\"op\":\"{op}\",\"value\":{value}" + (when == null ? "" : $",\"when\":{when}") + "}";
	static ModifierData Custom(string effect) => ModifierLoader.Parse(ModJson(effect));

	static Match Setup(ModifierData? mod, SpecialSlot slot, int p1x, int p2x, ElderTwist? p1Twist = null)
	{
		var m = new Match(null, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Fixtures());
		if (mod != null) m.P1.AttachModifier(slot, mod, m.Config);
		m.P1.X = p1x * S; m.P2.X = p2x * S;
		m.P1.Twist = p1Twist;
		return m;
	}

	static bool Refused(Action a)
	{
		try { a(); } catch (FormatException) { return true; }
		return false;
	}

	// --- Loader: closed list (task: unknown / typo / nested target) ------------------------------------

	// Rule: the loader accepts a closed list of targets; anything else is refused (claim 2).
	[Test]
	public static void UnknownTypoAndNestedTargetsAreRefused()
	{
		foreach (var t in new[] { "damge", "Damage", " damage", "damage ", "damage.", ".damage", "", "properties", "properties.damage",
			"properties.properties.armour", "properties.armour.hits", "properties.armour.frames", "properties.throw_invuln.startup",
			"properties.projectile.angle_up.x", "projectile.angle_up.x", "projectile", "pushback.on_hit.x", "pushback.both",
			"meter_gain", "meter_gain.on_whiff", "ex", "ex.meter_cost.x", "ex.meter_gain", "EX.meter_cost", "heal_pct_max_health.x",
			"armour.hits", "counterhit_immune.x", "aftershock_delay.x", "pull_on_hit.x", "__proto__", "damage[0]" })
			Assert.True(Refused(() => Custom(Eff($"\"{t}\"", "set", "1"))), $"target '{t}' was accepted");
		foreach (var t in new[] { "5", "null", "true", "[\"damage\"]", "{\"a\":1}" })
			Assert.True(Refused(() => Custom(Eff(t, "set", "1"))), $"non-string target {t} was accepted as a FormatException refusal");
	}

	// Rule: closed list also for cancels (path.from / into / on); a non-string value is refused the same way.
	[Test]
	public static void CancelLoaderRefusesTypoedAndNonStringPaths()
	{
		string Rule(string from, string into, string on, string opc = "true") =>
			$"{{\"kind\":\"cancel_rule\",\"id\":\"c\",\"path\":{{\"from\":{from},\"into\":{into},\"on\":{on}}},\"once_per_combo\":{opc}}}";
		CancelRuleLoader.Parse(Rule("\"specials\"", "\"dash\"", "[\"hit\"]")); // control
		foreach (var (f, i, o, opc) in new[] {
			("\"special\"", "\"dash\"", "[\"hit\"]", "true"), ("\"specials\"", "\"dashes\"", "[\"hit\"]", "true"),
			("\"specials\"", "\"dash\"", "[\"Hit\"]", "true"), ("\"specials\"", "\"dash\"", "[]", "true"),
			("\"specials\"", "\"dash\"", "[\"hit\",\"hit2\"]", "true"), ("\"specials\"", "\"dash\"", "[\"hit\"]", "false"),
			("\"specials\"", "\"dash\"", "[\"hit\"]", "\"true\""), ("\"specials\"", "\"dash\"", "[\"hit\"]", "1"),
			("5", "\"dash\"", "[\"hit\"]", "true"), ("\"specials\"", "5", "[\"hit\"]", "true"), ("\"specials\"", "\"dash\"", "[5]", "true"),
			("\"specials\"", "\"dash\"", "\"hit\"", "true") })
			Assert.True(Refused(() => CancelRuleLoader.Parse(Rule(f, i, o, opc))), $"accepted {f} {i} {o} once={opc}");
	}

	// Rule: whole-number fields (Effects.Whole): 1.5, "1", 1e400 refused; 2.0 / 1e1 accepted as the whole number.
	[Test]
	public static void WholeNumberFieldsRefuseFractionStringAndOverflow()
	{
		foreach (var v in new[] { "1.5", "\"1\"", "1e400", "-1e400", "2147483648", "-2147483649", "0.5e1x".Replace("x", "") == "" ? "1" : "1.0000001", "null", "[1]", "{}" })
		{
			if (v == "1") continue;
			Assert.True(Refused(() => Custom(Eff("\"damage\"", "add", v))), $"damage add {v} accepted");
			Assert.True(Refused(() => Custom(Eff("\"ex.meter_cost\"", "set", v))), $"ex.meter_cost set {v} accepted");
			Assert.True(Refused(() => Custom(Eff("\"properties.pull_on_hit\"", "set", v, "\"on_hit\""))), $"pull set {v} accepted");
			Assert.True(Refused(() => Custom(Eff("\"properties.armour\"", "set", $"{{\"frames\":[1,{v}]}}"))), $"armour last {v} accepted");
			Assert.True(Refused(() => Custom(Eff("\"properties.armour\"", "set", $"{{\"frames\":[{v},9]}}"))), $"armour first {v} accepted");
			if (v != "null") Assert.True(Refused(() => Custom(Eff("\"properties.armour\"", "set", $"{{\"frames\":[1,3],\"hits\":{v}}}"))), $"armour hits {v} accepted");
		}
		foreach (var v in new[] { "2.0", "1e1", "20e-1" })
		{
			var d = Custom(Eff("\"damage\"", "add", v));
			Assert.True(d.FrameEffects.Count == 1, $"damage add {v} should load");
		}
		foreach (var h in new[] { "0", "-1" })
			Assert.True(Refused(() => Custom(Eff("\"properties.armour\"", "set", $"{{\"frames\":[1,3],\"hits\":{h}}}"))), $"armour hits {h} accepted");
		Assert.True(Refused(() => Custom(Eff("\"properties.armour\"", "set", "{\"frames\":[0,3]}"))), "armour from frame 0");
		Assert.True(Refused(() => Custom(Eff("\"properties.armour\"", "set", "{\"frames\":[1,3,5]}"))), "armour three frames");
		Assert.True(Refused(() => Custom(Eff("\"properties.armour\"", "set", "{\"frames\":[1,3],\"extra\":1}"))), "armour extra key");
	}

	// Rule: a field present with a non-number value is not "absent": "hits": null must not load as the default 1.
	[Test]
	public static void ArmourHitsNullIsRefusedNotDefaulted()
	{
		Assert.True(Refused(() => Custom(Eff("\"properties.armour\"", "set", "{\"frames\":[1,3],\"hits\":null}"))), "armour hits null accepted");
	}

	// Rule: Describe of a card whose loader-accepted value is int.MinValue: ex.meter_cost mul_pct int.MinValue loads (cost floors at 0).
	[Test]
	public static void ExCostMulPctMinValueLoadsButItsCardMustNotShowAnIncrease()
	{
		var mod = Custom(Eff("\"ex.meter_cost\"", "mul_pct", "-2147483648"));
		Assert.Equal(0, mod.ExCostFor(100), "cost floors at 0");
		string text = RewardDraft.Describe(mod.Effects[0]);
		Assert.True(!text.Contains("+"), "a -2147483748% change printed as: " + text);
	}

	// Rule: a refusal is a FormatException (loader contract in every author test); a non-string "when" must not leak another exception.
	[Test]
	public static void NonStringConditionIsRefusedAsFormatException()
	{
		foreach (var w in new[] { "5", "true", "null", "[\"on_hit\"]" })
		{
			try { Custom(Eff("\"damage\"", "add", "1", w)); throw new AssertFailed($"when {w} accepted"); }
			catch (FormatException) { }
			catch (AssertFailed) { throw; }
			catch (Exception e) { throw new AssertFailed($"when {w}: {e.GetType().Name}, not FormatException"); }
		}
	}

	// Rule: ex.meter_cost add / mul_pct that overflow int32 are refused at load (author claims no silent wrap).
	[Test]
	public static void ExCostOverflowIsRefusedAndNegativeCostIsFloored()
	{
		var sp = Fixtures().First(s => s.HasEx);
		var huge = Custom(Eff("\"ex.meter_cost\"", "add", "2147483647"));
		Assert.True(Refused(() => sp.Build(1, true, huge, null)), "ex cost overflow accepted");
		var neg = Custom(Eff("\"ex.meter_cost\"", "add", "-100000"));
		Assert.Equal(0, neg.ExCostFor(100), "cost never below 0");
	}

	// --- Describe -------------------------------------------------------------------------------------

	static JsonNode J(string s) => JsonNode.Parse(s)!;

	// Rule: Describe shows whole numbers in any notation (claim 4); never throws.
	[Test]
	public static void DescribeWholeNumbersInAnyNotationAndNeverThrows()
	{
		string D(string op, string v) => RewardDraft.Describe(J($"{{\"target\":\"recovery\",\"op\":\"{op}\",\"value\":{v}}}"));
		Assert.Equal("recovery -4", D("add", "-4"), "-4");
		Assert.Equal("recovery -4", D("add", "-4.0"), "-4.0");
		Assert.Equal("recovery -4", D("add", "-4e0"), "-4e0");
		Assert.Equal("recovery -4", D("add", "-0.4e1"), "-0.4e1");
		Assert.Equal(D("add", "0"), D("add", "-0"), "-0 reads as 0");
		Assert.Equal(D("add", "0"), D("add", "-0.0"), "-0.0 reads as 0");
		Assert.True(!D("add", "-0").Contains("-"), "-0 shows no sign");
		Assert.Equal("recovery +50%", D("mul_pct", "150.0"), "mul_pct 150.0");
		Assert.Equal("recovery +50%", D("mul_pct", "1.5e2"), "mul_pct 1.5e2");
		Assert.Equal("recovery +2147483647", D("add", "2147483647"), "int max");
		Assert.Equal("recovery -2147483648", D("add", "-2147483648"), "int min");
		Assert.True(D("mul_pct", "-2147483648").Contains("-2147483748%"), "mul_pct int.MinValue is a -2147483748% change, got " + D("mul_pct", "-2147483648"));
		Assert.True(D("mul_pct", "2147483647").Contains("+2147483547%"), "mul_pct int.MaxValue, got " + D("mul_pct", "2147483647"));
		foreach (var v in new[] { "1.5", "\"1\"", "1e400", "-1e400", "true", "null", "[1]", "{}", "2147483648", "99999999999999999999999" })
			foreach (var op in new[] { "add", "mul_pct", "set" })
				_ = D(op, v); // must not throw
		Assert.True(D("add", "\"1\"").Contains("\"1\""), "a string value is shown raw: " + D("add", "\"1\""));
		Assert.True(D("add", "1.5").Contains("1.5"), "a fraction is shown raw: " + D("add", "1.5"));
		_ = RewardDraft.Describe(null);
		_ = RewardDraft.Describe(J("{}"));
		_ = RewardDraft.Describe(J("{\"target\":5,\"op\":7,\"value\":1}"));
	}

	// --- Fights ---------------------------------------------------------------------------------------

	static void Revive(Fighter f) { if (f.State is FighterState.Hitstun or FighterState.Knockdown) f.State = FighterState.Idle; f.Health = f.MaxHealth; }
	static MoveData FastJab(MoveData m) => m with { Recovery = 0 };
	static int Cycle(MoveData m) => m.Startup + m.Active; // ticks between P2's jabs

	/// <summary>P2 jabs every 3 ticks at point blank (positions reset each tick); returns hits taken and final state.</summary>
	static int maxTaken;
	static (Match m, int hits, List<FighterState> states) JabStorm(ModifierData? mod, ElderTwist? twist, int ticks, Func<int, bool>? special = null)
	{
		var m = Setup(mod, SpecialSlot.B, -60, 60, twist);
		int lp = Lp(m);
		m.P2.Moves[lp] = FastJab(m.P2.Moves[lp]);
		int hits = 0;
		var states = new List<FighterState>(); maxTaken = 0;
		m.Hit += (mm, e) => { if (e.Attacker == 1) hits++; };
		for (int t = 1; t <= ticks; t++)
		{
			m.P1.X = -60 * S; m.P2.X = 60 * S;
			m.Step(t == 1 ? FighterInput.Special(SpecialSlot.B) : Idle, (t - 1) % Cycle(m.P2.Moves[lp]) == 0 ? FighterInput.Attack(lp) : Idle);
			states.Add(m.P1.State);
			maxTaken = Math.Max(maxTaken, m.P1.ArmourHitsTaken);
			Revive(m.P2); // P1's own talisman would knock the jabber down: this probe is about P1's armour only
		}
		return (m, hits, states);
	}

	// Rule: armour hits h absorbs exactly h hits inside the window (author: default 1); 1 hit and many.
	[Test]
	public static void ArmourAbsorbsExactlyItsHitCount()
	{
		foreach (int hitsAllowed in new[] { 1, 2 })
		{
			var mod = Custom(Eff("\"properties.armour\"", "set", $"{{\"frames\":[1,30],\"hits\":{hitsAllowed}}}"));
			var (m, hits, states) = JabStorm(mod, null, 60);
			Assert.True(hits >= hitsAllowed + 1, $"storm landed {hits} hits: " + string.Join(" ", states.Select((x, i) => $"{i + 1}:{x}")));
			Assert.Equal(hitsAllowed, maxTaken, $"hits {hitsAllowed}: absorbed count");
			// first interrupt must come only after hitsAllowed hits were absorbed: before that the special plays on
			int interruptTick = states.FindIndex(s => s == FighterState.Hitstun) + 1;
			Assert.True(interruptTick > 0, $"hits {hitsAllowed}: eventually interrupted");
			Assert.True(interruptTick > 1 + Cycle(m.P2.Moves[Lp(m)]) * hitsAllowed, $"hits {hitsAllowed}: interrupted too early at tick {interruptTick}");
		}
	}

	// Rule (E17-style per-use): armour is per use of the move; a second use gets fresh hits.
	[Test]
	public static void ArmourHitsAreFreshForEachUseOfTheMove()
	{
		var mod = Mod("onis-hide");
		var m = Setup(mod, SpecialSlot.B, -60, 60);
		int lp = Lp(m);
		for (int use = 0; use < 2; use++)
		{
			for (int t = 0; t < 120 && m.P1.State != FighterState.Idle; t++) m.Step(Idle, Idle);
			m.P1.X = -60 * S; m.P2.X = 60 * S;
			m.Step(FighterInput.Special(SpecialSlot.B), Idle);
			for (int t = 0; t < 3; t++) m.Step(Idle, Idle);
			m.Step(Idle, FighterInput.Attack(lp));
			for (int t = 0; t < 2; t++) m.Step(Idle, Idle);
			Assert.Equal(FighterState.Attack, m.P1.State, $"use {use + 1}: armour holds on the first hit of the use");
			Assert.True(m.P1.ArmourHitsTaken <= 1, $"use {use + 1}: count {m.P1.ArmourHitsTaken}");
		}
	}

	static readonly ElderTwist OniArmour = new("t-armour", "elder-oni", TwistEffect.SpecialArmour, 0, 1, "twist.t", null, null, null);

	// Rule: modifier armour is checked before twist armour (claim): hit 1 uses the modifier's hit, hit 2 the twist's, hit 3 interrupts.
	[Test]
	public static void ModifierArmourIsSpentBeforeTwistArmour()
	{
		var twistLog = new List<TwistEvent>();
		var m = Setup(Mod("onis-hide"), SpecialSlot.B, -60, 60, OniArmour); // Oni's Hide: frames 1-5, one hit
		m.TwistTriggered += (mm, e) => twistLog.Add(e);
		int lp = Lp(m);
		m.P2.Moves[lp] = FastJab(m.P2.Moves[lp]);
		m.P2.Moves[lp] = FastJab(m.P2.Moves[lp]);
		var seen = new List<(int mod, int twist, FighterState s, int f, int p2)>();
		for (int t = 1; t <= 40; t++)
		{
			m.P1.X = -60 * S; m.P2.X = 60 * S;
			m.Step(t == 1 ? FighterInput.Special(SpecialSlot.B) : Idle, FighterInput.Attack(lp));
			if (t == 1) { m.P1.ActiveMove = m.P1.ActiveMove! with { Hitboxes = Array.Empty<TimedBox>() }; m.P1.ActiveBase = m.P1.ActiveMove; } // P1's own talisman must not disturb the jabber
			seen.Add((m.P1.ArmourHitsTaken, m.P1.TwistArmourTaken, m.P1.State, m.P1.MoveFrame, m.P2.MoveFrame));
		}
		int firstMod = seen.FindIndex(x => x.mod == 1);
		Assert.True(firstMod >= 0, "modifier armour took a hit");
		Assert.True(seen.Take(firstMod + 1).All(x => x.twist == 0), "twist armour untouched while the modifier still had a hit");
		string trace = string.Join(" ", seen.Select((x, i) => $"{i + 1}:{x.mod}/{x.twist}/{x.s}/f{x.f}/p{x.p2}"));
		Assert.Equal(1, seen.Max(x => x.mod), "modifier armour spent exactly one " + trace);
		Assert.Equal(1, twistLog.Count(e => e.Effect == TwistEffect.SpecialArmour), "twist armour spent exactly one hit, after the modifier's " + trace);
	}

	// Rule (C4/E12 via author: armour != throw invuln; E17 throws ignore invulnerability): Iron Will / Oni's Hide do not stop a throw.
	[Test]
	public static void IronWillAndOnisHideDoNotStopThrows()
	{
		foreach (var id in new[] { "iron-will", "onis-hide", "slippery-skin" })
			foreach (int p1Starts in new[] { 1, 3 })
			{
				// P2 throw (startup 5) active ticks 6-7. P1 talisman: startup frames 1-5, active 6-13.
				bool thrown = false;
				var m = Setup(Mod(id), SpecialSlot.B, -60, 60);
				for (int t = 1; t <= 9; t++)
				{
					m.Step(t == p1Starts ? FighterInput.Special(SpecialSlot.B) : Idle, t == 1 ? FighterInput.Attack(Throw(m)) : Idle);
					thrown |= m.P1.State == FighterState.Thrown;
				}
				bool expected = !(id == "slippery-skin" && p1Starts == 3 && false);
				if (id != "slippery-skin") Assert.True(thrown, $"{id} (start tick {p1Starts}): a throw still lands on it");
			}
	}

	static (Match m, int corner) CornerMatch(ModifierData? mod, bool rightCorner)
	{
		var m = Setup(mod, SpecialSlot.B, -60, 60);
		m.P1.X = rightCorner ? 1_000_000_000 : -1_000_000_000; m.P2.X = m.P1.X + (rightCorner ? 60 * S : -60 * S);
		m.Step(Idle, Idle);
		return (m, rightCorner ? Math.Max(m.P1.X, m.P2.X) : Math.Min(m.P1.X, m.P2.X));
	}

	// Rule (River Pull card: pulls the opponent closer on hit): at a wall the opponent is still pulled toward the
	// attacker, the attacker is not shoved, and the pulled fighter never ends behind the attacker.
	[Test]
	public static void RiverPullAtAWallPullsTowardAttackerAndNeverThroughIt()
	{
		foreach (bool right in new[] { true, false })
			foreach (bool attackerInCorner in new[] { true, false })
			{
				var m = Setup(Mod("river-pull"), SpecialSlot.B, -60, 60);
				// find the corner
				m.P1.X = right ? 100_000_000 : -100_000_000; m.P2.X = m.P1.X; m.Step(Idle, Idle);
				int corner = right ? Math.Max(m.P1.X, m.P2.X) : Math.Min(m.P1.X, m.P2.X);
				int dir = right ? 1 : -1;
				var m2 = Setup(Mod("river-pull"), SpecialSlot.B, -60, 60);
				// attacker faces the defender: P1 left of P2 means facing right; mirror by swapping which fighter is where
				int gap = 140 * S;
				if (attackerInCorner) { m2.P1.X = corner; m2.P2.X = corner - dir * gap; }
				else { m2.P2.X = corner; m2.P1.X = corner - dir * gap; }
				m2.Step(Idle, Idle);
				int a0 = m2.P1.X, d0 = m2.P2.X;
				int ownerFacing = m2.P1.Facing;
				m2.Step(FighterInput.Special(SpecialSlot.B), Idle);
				bool landed = false; m2.Hit += (mm, e) => landed |= e.Attacker == 0 && !e.Blocked;
				for (int t = 0; t < 12; t++) m2.Step(Idle, Idle);
				if (!landed) continue; // attacker not in range in this layout
				int distBefore = Math.Abs(d0 - a0), distAfter = Math.Abs(m2.P2.X - m2.P1.X);
				Assert.True(distAfter <= distBefore, $"right={right} attackerInCorner={attackerInCorner}: pull must not push away ({distBefore} -> {distAfter})");
				Assert.Equal(Math.Sign(d0 - a0), Math.Sign(m2.P2.X - m2.P1.X), "pulled fighter stays on its own side of the attacker");
				if (attackerInCorner) Assert.Equal(a0, m2.P1.X, "the attacker in the corner is not moved by the pull (no corner hand-off)");
			}
	}

	// Rule: on_hit variants only follow the attacker's own connecting move: a reflected special (YOK-90) must not
	// heal the elder from Ryo's Cucumber Gift, and must not pull Ryo (River Pull) either.
	[Test]
	public static void ReflectedWaveDoesNotHealTheElderFromCucumberGift() => ReflectedWave("cucumber-gift");
	[Test]
	public static void ReflectedWaveDoesNotPullRyoFromRiverPull() => ReflectedWave("river-pull");

	static void ReflectedWave(string id)
	{
		var reflect = new ElderTwist("t-reflect", "nine-tailed-kitsune", TwistEffect.ReflectRepeatedSpecial, 2, 0, "twist.t", null, null, null);
		{
			var m = Setup(Mod(id), SpecialSlot.A, -300, 300);
			m.P2.Twist = reflect;
			m.P2.Health = 500;
			var hits = new List<HitEvent>();
			m.Hit += (mm, e) => hits.Add(e);
			int reflected = 0; m.TwistTriggered += (mm, e) => reflected += e.Effect == TwistEffect.ReflectRepeatedSpecial ? 1 : 0;
			int hpBefore = -1, hpAfter = -1, x0 = 0, x1 = 0;
			for (int use = 0; use < 2; use++)
			{
				m.Step(FighterInput.Special(SpecialSlot.A), Idle);
				for (int t = 0; t < 140; t++)
				{
					int h0 = m.P2.Health, n0 = hits.Count, px = m.P1.X;
					m.Step(Idle, Idle);
					if (use == 1 && hits.Skip(n0).Any(h => h.Attacker == 1)) { hpBefore = h0; hpAfter = m.P2.Health; x0 = px; x1 = m.P1.X; break; }
				}
			}
			Assert.True(reflected >= 1, $"{id}: wave was reflected");
			var back = hits.FirstOrDefault(h => h.Attacker == 1);
			Assert.True(back.MoveId != null, $"{id}: the reflected wave hit Ryo");
			Assert.Equal(hpBefore, hpAfter, $"{id}: the elder gained nothing from the reflected hit");
			// a plain hit shoves Ryo away from the elder; River Pull's pull toward her would be Ryo's own modifier firing on her move
			Assert.True(Math.Sign(x1 - x0) * Math.Sign(m.P1.X - m.P2.X) >= 0, $"{id}: Ryo moved toward the elder on the reflected hit ({x0} -> {x1})");
		}
	}

	// --- Cancels --------------------------------------------------------------------------------------

	static Func<int, FighterInput> Then236(int from, FighterInput first)
	{
		var motion = InputParserTests.Record("2 3 6+MP");
		return t => t == 1 ? first : t < from ? Idle : motion[(t - from) % motion.Count];
	}
	static void GiveWindow(Match m, int slot, int first, int last, bool hit = true, bool block = true, bool whiff = false) =>
		m.P1.Moves[slot] = m.P1.Moves[slot] with { CancelWindows = new[] { new CancelWindow(first, last, hit, block, whiff) } };

	// Rule X4: Oni Chain = heavy normal -> special on hit OR block, once per combo; blocked heavy must cancel too.
	[Test]
	public static void OniChainCancelsOnBlockToo()
	{
		int First(bool p2Blocks)
		{
			var m = Setup(null, SpecialSlot.A, -60, 60);
			var specials = new List<int>(); m.SpecialStarted += (mm, e) => specials.Add(mm.WorldFrame);
			int hp = Hp(m);
			GiveWindow(m, hp, 11, 30);
			m.P1.HoldCancelRule(Cancel("oni-chain"));
			var in1 = Then236(10, FighterInput.Attack(hp));
			for (int t = 1; t <= 90; t++)
				m.Step(in1(t), p2Blocks ? new FighterInput(InputBits.Right | InputBits.Down) : Idle);
			return specials.First(f => f > 0);
		}
		Assert.True(First(true) < 35, "blocked heavy cancels into the special");
	}

	// Rule X4 / E17: Fox Step on the wave's real window (14-20): whiff dashes (author covers); a wave that HITS in the
	// window is still a "hit" of the special, so the cancel to dash must also be open.
	[Test]
	public static void FoxStepDashesWhenTheWaveHitsOrIsBlocked()
	{
		bool Dashed(int p2x, bool block)
		{
			var m = Setup(null, SpecialSlot.A, -60, p2x);
			m.P1.HoldCancelRule(Cancel("fox-step"));
			var seq = new List<FighterInput> { FighterInput.Special(SpecialSlot.A) };
			while (seq.Count < 13) seq.Add(Idle);
			seq.AddRange(InputParserTests.Record("6 5 6"));
			bool dashed = false;
			for (int t = 1; t <= seq.Count + 6; t++)
			{
				m.Step(t <= seq.Count ? seq[t - 1] : Idle, block ? new FighterInput(InputBits.Right | InputBits.Down) : Idle);
				dashed |= m.P1.State == FighterState.Dash;
			}
			return dashed;
		}
		Assert.True(Dashed(800, false), "whiff");
		Assert.True(Dashed(120, false), "wave hit in the window");
		Assert.True(Dashed(120, true), "wave blocked in the window");
	}

	// Rule X3: each target once per combo, whichever rule opened it: two rules into the same slot cancel it once.
	[Test]
	public static void TwoRulesStillCancelIntoTheSameSpecialOnlyOnce()
	{
		var m = Setup(null, SpecialSlot.A, -60, 60);
		var starts = new List<int>(); m.SpecialStarted += (mm, e) => starts.Add(mm.WorldFrame);
		int hp = Hp(m);
		GiveWindow(m, hp, 11, 30);
		m.P1.HoldCancelRule(Cancel("oni-chain"));
		m.P1.HoldCancelRule(CancelRuleLoader.Parse("{\"kind\":\"cancel_rule\",\"id\":\"normals-too\",\"path\":{\"from\":\"normals\",\"into\":\"specials\",\"on\":[\"hit\",\"block\"]},\"once_per_combo\":true}"));
		var in1 = Then236(10, FighterInput.Attack(hp));
		for (int t = 1; t <= 34; t++) m.Step(in1(t), Idle);
		Assert.True(starts.Count(f => f > 0) <= 1, $"cancelled into slot A {starts.Count} times in one combo window: {string.Join(",", starts)}");
	}

	// --- Determinism ----------------------------------------------------------------------------------

	static ulong RandomFight(int seed, ModifierData a, ModifierData b)
	{
		var m = Setup(null, SpecialSlot.A, -60, 60);
		m.P1.Equip(SpecialSlot.A, Fixtures().First(s => a.AppliesTo(s)), 1, a, m.Config);
		m.P1.Equip(SpecialSlot.B, Fixtures().First(s => b.AppliesTo(s) && s.Id != m.P1.Specials[(int)SpecialSlot.A]!.Data.Id), 1, b, m.Config);
		m.P1.HoldCancelRule(Cancel("fox-step")); m.P1.HoldCancelRule(Cancel("oni-chain"));
		var rng = new SimRng((uint)seed);
		ulong h = 0;
		for (int t = 0; t < 2400 && m.Phase != MatchPhase.Over; t++)
		{
			FighterInput In() => rng.Next(6) == 0
				? FighterInput.Special((SpecialSlot)(1 + rng.Next(2)), rng.Next(2) == 0, rng.Next(2) == 0 ? InputBits.Up : InputBits.None)
				: new FighterInput((InputBits)(rng.NextUInt() & (uint)(InputBits.Directions | InputBits.Attacks)));
			m.Step(In(), In());
			h = h * 31 + m.StateHash();
		}
		return h;
	}

	// Rule: same seed + choices => same StateHash; different choice => different hash (every modifier equipped).
	[Test]
	public static void EveryModifierPairIsDeterministicAndSeedSensitive()
	{
		var mods = ModifierLoader.LoadDirectory($"{Repo}/data/modifiers");
		var fx = Fixtures();
		var seen = new HashSet<ulong>();
		for (int i = 0; i < mods.Length; i++)
		{
			var a = mods[i]; var b = mods[(i + 5) % mods.Length];
			if (!fx.Any(a.AppliesTo) || !fx.Any(b.AppliesTo)) continue;
			ulong h1 = RandomFight(7 + i, a, b), h2 = RandomFight(7 + i, a, b);
			Assert.Equal(h1, h2, $"{a.Id}+{b.Id}: same seed, same hashes");
			ulong h3 = RandomFight(1000 + i, a, b);
			Assert.True(h3 != h1, $"{a.Id}+{b.Id}: a different seed gave the same hash");
			seen.Add(h1);
		}
	}

	// Rule: a recorded fight with modifiers equipped replays hash-for-hash through bytes (header carries the modifier).
	[Test]
	public static void ModifierFightReplayRoundTrips()
	{
		var catalog = ReplayCatalog.FromRepo(Repo);
		foreach (var id in new[] { "onis-hide", "tricksters-angle", "ninefold-spark", "cucumber-gift", "river-pull", "rage-ember" })
		{
			var mod = Mod(id);
			var m = Setup(null, SpecialSlot.A, -60, 60);
			var fx = Fixtures();
			var target = fx.First(mod.AppliesTo);
			var slot = target.Id == m.P1.Specials[(int)SpecialSlot.A]!.Data.Id ? SpecialSlot.A : SpecialSlot.B;
			m.P1.AttachModifier(slot, mod, m.Config);
			m.Reset();
			var rec = ReplayRecorder.Begin(m, null, 0);
			var rng = new SimRng(55);
			for (int t = 0; t < 1500 && m.Phase != MatchPhase.Over; t++)
			{
				FighterInput In() => rng.Next(6) == 0
					? FighterInput.Special((SpecialSlot)(1 + rng.Next(2)), rng.Next(2) == 0, rng.Next(2) == 0 ? InputBits.Up : InputBits.None)
					: new FighterInput((InputBits)(rng.NextUInt() & (uint)(InputBits.Directions | InputBits.Attacks)));
				FighterInput a = In(), b = In();
				m.Step(a, b);
				rec.Record(a, b, m.StateHash());
			}
			var back = ReplayRecording.FromBytes(rec.Finish(m).ToBytes());
			Assert.True(back.Header.Fighters[0].Specials.Any(s => s.Modifier == id), $"{id}: recorded in the header");
			var r = Replayer.Run(back, catalog);
			Assert.True(r.Ok, $"{id}: {r}");
		}
	}
}
