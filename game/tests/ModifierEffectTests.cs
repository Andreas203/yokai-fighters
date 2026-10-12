using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-103: every effect the real files in data/modifiers/ and data/cancels/ use, played in a fight. Each test
/// loads the real modifier / cancel file and attaches it to a TEST FIXTURE special (Spirit Wave in slot A: a
/// projectile, spawn frame 14, speed 12, 60 damage; Rising Talisman in slot B: 5 / 8 / 28, 90 damage, knockdown),
/// so the numbers checked are the modifier's own. A special requested on tick 1 plays frame n on tick n.
/// </summary>
public static class ModifierEffectTests
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

	sealed class Log
	{
		public readonly List<(int tick, HitEvent e)> Hits = new();
		public readonly List<(int frame, SpecialEvent e)> Specials = new();
		public readonly List<(int frame, ThrowEvent e)> Throws = new();
	}

	/// <summary>P1 (Ryo) with the modifier on <paramref name="slot"/>, P2 plain; both hold the fixture starters.</summary>
	static Match Setup(string? modifier, SpecialSlot slot, int p1x, int p2x, Log? log = null)
	{
		var m = new Match(null, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Fixtures());
		if (modifier != null) m.P1.AttachModifier(slot, Mod(modifier), m.Config);
		Place(m, p1x, p2x);
		if (log != null)
		{
			m.Hit += (mm, e) => log.Hits.Add((mm.Tick, e));
			m.SpecialStarted += (mm, e) => log.Specials.Add((mm.WorldFrame, e));
			m.Throw += (mm, e) => log.Throws.Add((mm.WorldFrame, e));
		}
		return m;
	}

	static void Place(Match m, int p1x, int p2x)
	{
		m.P1.X = p1x * S;
		m.P2.X = p2x * S;
	}

	/// <summary>Runs <paramref name="ticks"/> ticks; the input functions get the tick within this run (1 = first).</summary>
	static void Run(Match m, int ticks, Func<int, FighterInput>? p1 = null, Func<int, FighterInput>? p2 = null)
	{
		for (int t = 1; t <= ticks; t++) m.Step(p1?.Invoke(t) ?? Idle, p2?.Invoke(t) ?? Idle);
	}

	static Func<int, FighterInput> At(int tick, FighterInput input) => t => t == tick ? input : Idle;
	/// <summary>P2 (facing left) holds back from this tick on: late, so it hasn't walked out of range first.</summary>
	static Func<int, FighterInput> BlockFrom(int tick) => t => t >= tick ? new FighterInput(InputBits.Right) : Idle;
	static readonly Func<int, FighterInput> P2HoldsBack = BlockFrom(6); // the talisman connects on tick 6

	// --- The data loads ---------------------------------------------------------------------------

	[Test]
	public static void EveryDataModifierLoadsOnEverySpecialItAppliesTo()
	{
		var mods = ModifierLoader.LoadDirectory($"{Repo}/data/modifiers");
		foreach (string id in new[] { "brute-recovery", "club-momentum", "cucumber-gift", "fox-patience", "illusion-cloak", "iron-will",
			"ninefold-spark", "onis-hide", "rage-ember", "river-pull", "slippery-skin", "tricksters-angle", "will-o-wisp" })
			Assert.True(mods.Any(x => x.Id == id), $"{id} loads");
		var pool = AbilityPool.LoadRepo(Repo); // builds every modifier on every special it applies to, plain and EX
		Assert.True(pool.Modifiers.Count() >= 13, "the run's pool holds them");
		foreach (var mod in mods)
			foreach (var sp in Fixtures().Where(mod.AppliesTo))
				for (int level = 1; level <= sp.MaxLevel; level++)
				{
					sp.Build(level, false, mod, null);
					if (sp.HasEx) sp.Build(level, true, mod, null);
				}
	}

	[Test]
	public static void LoaderRejectsWhatTheVocabularyDoesNotName()
	{
		static string File(string effect) =>
			"{\"kind\":\"modifier\",\"id\":\"x\",\"source\":\"oni\",\"applies_to\":{\"requires\":[]},\"effects\":[" + effect + "]}";
		void Bad(string effect, string why)
		{
			try { ModifierLoader.Parse(File(effect)); }
			catch (FormatException) { return; }
			throw new AssertFailed($"accepted: {why}");
		}
		Bad("{\"target\":\"damage_bonus\",\"op\":\"add\",\"value\":1}", "unknown target");
		Bad("{\"target\":\"damage\",\"op\":\"mul_pct\",\"value\":120,\"when\":\"holding_charge\"}", "holding_charge (Sumo Stance, left out)");
		Bad("{\"target\":\"damage\",\"op\":\"add\",\"value\":1,\"when\":\"on_whiff\"}", "on_whiff");
		Bad("{\"target\":\"damage\",\"op\":\"double\",\"value\":1}", "unknown op");
		Bad("{\"target\":\"damage\",\"op\":\"add\",\"value\":true}", "integer target with a flag");
		Bad("{\"target\":\"damage\",\"op\":\"add\",\"value\":1.5}", "fraction");
		Bad("{\"target\":\"properties.counterhit_immune\",\"op\":\"add\",\"value\":1}", "flag with add");
		Bad("{\"target\":\"properties.armour\",\"op\":\"set\",\"value\":{\"frames\":[5,1]}}", "armour window backwards");
		Bad("{\"target\":\"properties.armour\",\"op\":\"set\",\"value\":true}", "armour without frames");
		Bad("{\"target\":\"properties.throw_invuln\",\"op\":\"set\",\"value\":\"active\"}", "throw_invuln other than startup");
		Bad("{\"target\":\"pushback\",\"op\":\"set\",\"value\":10}", "set on the pushback pair");
		Bad("{\"target\":\"ex.meter_cost\",\"op\":\"set\",\"value\":50,\"when\":\"on_hit\"}", "conditional EX cost");
		ModifierLoader.Parse(File("{\"target\":\"ex.meter_cost\",\"op\":\"set\",\"value\":50}")); // the modifier-only target

		// Levels and EX: no conditions, no EX cost.
		string wave = System.IO.File.ReadAllText(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir) + "/test-spirit-wave.json");
		foreach (string swap in new[] { "\"target\": \"ex.meter_cost\"", "\"target\": \"recovery\", \"when\": \"on_block\"" })
		{
			string json = wave.Replace("\"target\": \"recovery\"", swap);
			Assert.True(json != wave, "fixture has a recovery step to swap");
			try { SpecialLoader.Parse(json); }
			catch (FormatException) { continue; }
			throw new AssertFailed($"a level step accepted {swap}");
		}

		// An armour window past the end of the move fails when the move is built.
		var late = ModifierLoader.Parse(File("{\"target\":\"properties.armour\",\"op\":\"set\",\"value\":{\"frames\":[1,500]}}"));
		try { Fixtures()[0].Build(1, false, late, null); }
		catch (FormatException) { return; }
		throw new AssertFailed("armour frames outside the move accepted");
	}

	/// <summary>
	/// The shared case list both gates run (tools/test_validate_data.py runs the same file through validate_data.py):
	/// the loader accepts a case exactly when it parses and builds on every special it applies to, at every level,
	/// plain and EX: what AbilityPool does at load.
	/// </summary>
	[Test]
	public static void ValidatorAndLoaderAgreeOnTheSharedCases()
	{
		var specials = AbilityPool.LoadRepo(Repo).Specials.Concat(AbilityPool.LoadFixtures(Repo).Specials)
			.GroupBy(sp => sp.Id).Select(g => g.First()).ToArray();
		Assert.True(specials.Any(sp => sp.HasProperty("projectile")) && specials.Any(sp => !sp.HasProperty("projectile")), "specials with and without a projectile");
		var cases = System.Text.Json.Nodes.JsonNode.Parse(System.IO.File.ReadAllText($"{Repo}/game/tests/fixtures/modifier-gate/cases.json"))!["cases"]!.AsArray();
		Assert.True(cases.Count >= 30, "the case list is there");
		var wrong = new List<string>();
		foreach (var c in cases)
		{
			string name = c!["name"]!.GetValue<string>();
			var file = new System.Text.Json.Nodes.JsonObject
			{
				["kind"] = "modifier", ["id"] = name, ["name"] = name, ["source"] = "oni", ["rarity"] = "common", ["locked"] = false,
				["applies_to"] = new System.Text.Json.Nodes.JsonObject { ["requires"] = c["requires"]!.DeepClone() },
				["effects"] = c["effects"]!.DeepClone(),
			};
			string? refused = null;
			try
			{
				var mod = ModifierLoader.Parse(file.ToJsonString());
				foreach (var sp in specials.Where(mod.AppliesTo))
					for (int level = 1; level <= sp.MaxLevel; level++)
					{
						sp.Build(level, false, mod, null);
						if (sp.HasEx) sp.Build(level, true, mod, null);
					}
			}
			catch (FormatException e) { refused = e.Message; }
			if ((refused is null) != c["accept"]!.GetValue<bool>())
				wrong.Add($"{name}: loader {(refused is null ? "accepted" : "refused: " + refused)}");
		}
		Assert.Equal("", string.Join(" | ", wrong), "loader vs the shared cases");

		// A special's own file (special_cases): SpecialLoader.Parse builds every level, plain and EX.
		var root = System.Text.Json.Nodes.JsonNode.Parse(System.IO.File.ReadAllText($"{Repo}/game/tests/fixtures/modifier-gate/cases.json"))!;
		var specialCases = root["special_cases"]!.AsArray();
		Assert.True(specialCases.Count >= 8, "the special case list is there");
		foreach (var c in specialCases)
		{
			string name = c!["name"]!.GetValue<string>();
			var doc = System.Text.Json.Nodes.JsonNode.Parse(System.IO.File.ReadAllText(
				ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir) + $"/{c["base"]!.GetValue<string>()}.json"))!.AsObject();
			doc["id"] = name;
			var fd = doc["levels"]!["1"]!["frame_data"]!.AsObject();
			foreach (var kv in c["set"]!.AsObject()) fd[kv.Key] = kv.Value!.DeepClone();
			foreach (var k in c["remove"]!.AsArray()) fd.Remove(k!.GetValue<string>());
			string? refused = null;
			try { SpecialLoader.Parse(doc.ToJsonString()); }
			catch (FormatException e) { refused = e.Message; }
			if ((refused is null) != c["accept"]!.GetValue<bool>())
				wrong.Add($"{name}: loader {(refused is null ? "accepted" : "refused: " + refused)}");
		}
		Assert.Equal("", string.Join(" | ", wrong), "loader vs the shared special cases");
	}

	// --- Oni ----------------------------------------------------------------------------------------

	/// <summary>The tick P1 is Idle again after a Rising Talisman requested on tick 1.</summary>
	static int TalismanEnds(string? modifier, int p2x, Func<int, FighterInput>? p2 = null)
	{
		var m = Setup(modifier, SpecialSlot.B, -60, p2x);
		for (int t = 1; t <= 200; t++)
		{
			m.Step(t == 1 ? FighterInput.Special(SpecialSlot.B) : Idle, p2?.Invoke(t) ?? Idle);
			if (t > 1 && m.P1.State == FighterState.Idle) return t;
		}
		throw new AssertFailed("the talisman never ended");
	}

	[Test]
	public static void BruteRecoveryRecoversFourFramesSoonerOnlyOnBlock()
	{
		Assert.Equal(TalismanEnds(null, 60, P2HoldsBack) - 4, TalismanEnds("brute-recovery", 60, P2HoldsBack), "blocked: 4 frames sooner");
		Assert.Equal(TalismanEnds(null, 60), TalismanEnds("brute-recovery", 60), "on hit: unchanged");
		Assert.Equal(TalismanEnds(null, 600), TalismanEnds("brute-recovery", 600), "on whiff: unchanged");
	}

	/// <summary>
	/// Code-critic repro: Brute Recovery applies to any special (requires []), so on a projectile special the block
	/// must shorten the owner's recovery too, as long as the owner is still in the move that fired it.
	/// </summary>
	[Test]
	public static void BruteRecoveryAlsoCountsWhenTheSpecialsProjectileIsBlocked()
	{
		int WaveEnds(string? modifier, bool block, int p2x)
		{
			var m = Setup(modifier, SpecialSlot.A, -100, p2x);
			for (int t = 1; t <= 200; t++)
			{
				m.Step(t == 1 ? FighterInput.Special(SpecialSlot.A) : Idle,
					block && t >= 12 ? new FighterInput(InputBits.Right | InputBits.Down) : Idle); // crouch-block: no walking away
				if (t > 1 && m.P1.State == FighterState.Idle) return t;
			}
			throw new AssertFailed("the wave's caster never recovered");
		}
		int plain = WaveEnds(null, true, 100);
		Assert.Equal(plain - 4, WaveEnds("brute-recovery", true, 100), "wave blocked while Ryo still recovers: 4 frames sooner");
		Assert.Equal(WaveEnds(null, false, 100), WaveEnds("brute-recovery", false, 100), "wave hits: unchanged");
		Assert.Equal(WaveEnds(null, true, 1200), WaveEnds("brute-recovery", true, 1200), "blocked after Ryo has recovered: nothing left to shorten");
	}

	/// <summary>Every connect of one use picks its outcome from the move as it started: two hits heal twice.</summary>
	[Test]
	public static void EachConnectOfAMultiHitMoveGetsItsOutcome()
	{
		var log = new Log();
		var m = Setup("cucumber-gift", SpecialSlot.A, -300, 300, log);
		m.P1.Health = 500;
		m.P1.Meter = 100;
		for (int t = 1; t <= 120 && log.Hits.Count < 2; t++)
			m.Step(t == 1 ? FighterInput.Special(SpecialSlot.A, ex: true) : Idle, Idle);
		Assert.Equal(2, log.Hits.Count, "the EX wave hits twice");
		Assert.Equal(540, m.P1.Health, "2% healed on each hit");

		// Unit level: a blocked connect, then a hit, of the same use (what a multi-hit strike would do).
		var eq = m.P1.Specials[(int)SpecialSlot.A]!;
		Assert.True(eq.Move.OnHitVariant != null && eq.Move.OnBlockVariant == null, "cucumber: an on_hit variant only");
		Assert.Equal(2, eq.Move.Outcome(false).HealPctMaxHealth, "hit");
		Assert.Equal(0, eq.Move.Outcome(true).HealPctMaxHealth, "blocked: the move itself");
	}

	static (int p2x, HitEvent hit, Match m) TalismanConnects(string? modifier, int p1x, int p2x, bool block, Action<Match>? before = null)
	{
		var log = new Log();
		var m = Setup(modifier, SpecialSlot.B, p1x, p2x, log);
		before?.Invoke(m);
		Run(m, 6, At(1, FighterInput.Special(SpecialSlot.B)), block ? P2HoldsBack : null);
		Assert.Equal(1, log.Hits.Count, "the talisman connects on tick 6");
		Assert.Equal(block, log.Hits[0].e.Blocked, "blocked");
		return (m.P2.X, log.Hits[0].e, m);
	}

	[Test]
	public static void ClubMomentumPushesHalfAgainAsFar()
	{
		var move = Fixtures().Single(s => s.Slot == SpecialSlot.B).Build(1, false, Mod("club-momentum"), null);
		Assert.Equal((60, 75), (move.HitPushback!.Value, move.BlockPushback!.Value), "E5's 40 / 50 x 1.5");
		Assert.Equal(TalismanConnects(null, -60, 60, false).p2x + 20 * S, TalismanConnects("club-momentum", -60, 60, false).p2x, "hit: 60 units, not 40");
		Assert.Equal(TalismanConnects(null, -60, 60, true).p2x + 25 * S, TalismanConnects("club-momentum", -60, 60, true).p2x, "block: 75 units, not 50");
	}

	[Test]
	public static void RageEmberOnlyBelowThirtyPercent()
	{
		Assert.Equal(104, TalismanConnects("rage-ember", -60, 60, false, m => m.P1.Health = 299).hit.Damage, "299 of 1,000: 90 x 1.15 = 103.5 -> 104 (E20)");
		Assert.Equal(90, TalismanConnects("rage-ember", -60, 60, false, m => m.P1.Health = 300).hit.Damage, "exactly 30% is not below");
		Assert.Equal(90, TalismanConnects("rage-ember", -60, 60, false).hit.Damage, "full health");
		Assert.Equal(90, TalismanConnects(null, -60, 60, false, m => m.P1.Health = 100).hit.Damage, "no modifier");
	}

	[Test]
	public static void OnisHideTakesOneHitInFramesOneToFive()
	{
		var log = new Log();
		var m = Setup("onis-hide", SpecialSlot.B, -60, 60, log);
		Assert.Equal(new ArmourWindow(1, 5, 1), m.P1.Specials[(int)SpecialSlot.B]!.Move.Armour, "armour 1-5, one hit");
		int lp = Lp(m);
		Run(m, 5, At(1, FighterInput.Special(SpecialSlot.B)), At(1, FighterInput.Attack(lp))); // LP lands on tick 5 = talisman frame 5
		Assert.Equal(1, log.Hits.Count(h => h.e.Attacker == 1), "P2's punch landed");
		Assert.Equal(FighterState.Attack, m.P1.State, "armour: the talisman plays on");
		Assert.Equal(1, m.P1.ArmourHitsTaken, "one hit taken");
		Run(m, 40);
		Assert.Equal(1000 - 36, m.P1.Health, "the damage still lands (30, +20% counterhit, C7)");
		Assert.True(log.Hits.Any(h => h.e.Attacker == 0 && h.e.MoveId.Contains("talisman")), "and the talisman hits back");

		// Frame 6 is outside the window: the same punch a tick later interrupts.
		var m2 = Setup("onis-hide", SpecialSlot.B, -60, 60);
		Run(m2, 6, At(1, FighterInput.Special(SpecialSlot.B)), At(2, FighterInput.Attack(lp)));
		Assert.Equal(FighterState.Hitstun, m2.P1.State, "frame 6: no armour");

		// Without the modifier frame 5 is interrupted.
		var m3 = Setup(null, SpecialSlot.B, -60, 60);
		Run(m3, 5, At(1, FighterInput.Special(SpecialSlot.B)), At(1, FighterInput.Attack(lp)));
		Assert.Equal(FighterState.Hitstun, m3.P1.State, "no modifier: interrupted");
	}

	[Test]
	public static void IronWillIsNotInterruptedByProjectiles()
	{
		FighterState AfterWave(string? modifier, out int health, out int hits)
		{
			var log = new Log();
			var m = Setup(modifier, SpecialSlot.B, -300, 300, log);
			// P2's wave spawns on tick 14 and reaches P1 around tick 51; P1's talisman (41 frames) starts on tick 40.
			for (int t = 1; t <= 70 && log.Hits.Count == 0; t++)
				m.Step(t == 40 ? FighterInput.Special(SpecialSlot.B) : Idle, t == 1 ? FighterInput.Special(SpecialSlot.A) : Idle);
			hits = log.Hits.Count;
			health = m.P1.Health;
			return m.P1.State;
		}
		Assert.Equal(FighterState.Hitstun, AfterWave(null, out int h0, out int n0), "no modifier: the wave interrupts");
		Assert.Equal(FighterState.Attack, AfterWave("iron-will", out int h1, out int n1), "Iron Will: the talisman plays on");
		Assert.Equal((1, 1), (n0, n1), "the wave hit both times");
		Assert.Equal(h0, h1, "the projectile still deals its damage");
		Assert.Equal(1000 - 72, h1, "60 +20% (C7: hit during a move)");

		// A strike still interrupts.
		var m2 = Setup("iron-will", SpecialSlot.B, -60, 60);
		Run(m2, 5, At(1, FighterInput.Special(SpecialSlot.B)), At(1, FighterInput.Attack(Lp(m2))));
		Assert.Equal(FighterState.Hitstun, m2.P1.State, "strikes are not projectiles");
	}

	// --- Kitsune ------------------------------------------------------------------------------------

	[Test]
	public static void IllusionCloakFirstUseEachRoundCannotBeCounterhit()
	{
		var log = new Log();
		var m = Setup("illusion-cloak", SpecialSlot.B, -60, 60, log);
		int lp = Lp(m);
		bool Exchange()
		{
			Place(m, -60, 60);
			log.Hits.Clear();
			Run(m, 5, At(1, FighterInput.Special(SpecialSlot.B)), At(1, FighterInput.Attack(lp)));
			var hit = log.Hits.Single(h => h.e.Attacker == 1).e;
			Run(m, 120);
			return hit.Counter;
		}
		Assert.True(!Exchange(), "first use: hit during the move, not a counterhit");
		Assert.True(Exchange(), "second use the same round: counterhit (C7)");
		m.Reset();
		Assert.True(!Exchange(), "next round: the first use is covered again");

		var plain = Setup(null, SpecialSlot.B, -60, 60, log);
		log.Hits.Clear();
		Run(plain, 5, At(1, FighterInput.Special(SpecialSlot.B)), At(1, FighterInput.Attack(lp)));
		Assert.True(log.Hits.Single().e.Counter, "no modifier: counterhit");
	}

	[Test]
	public static void TrickstersAngleClimbsOnlyWhenUpIsHeldAtTheStart()
	{
		Assert.True(!Mod("tricksters-angle").AppliesTo(Fixtures().Single(s => s.Slot == SpecialSlot.B)), "projectile specials only");
		int YAfter(string? modifier, InputBits held)
		{
			var m = Setup(modifier, SpecialSlot.A, -800, 800);
			Run(m, 20, At(1, FighterInput.Special(SpecialSlot.A, false, held)));
			return m.Projectiles.Single().Y;
		}
		// Spawned on tick 14, six steps by tick 20; speed 12, climb 50% (SimConfig.ProjectileAngleUpRisePct, proposed).
		Assert.Equal(6 * 6 * S, YAfter("tricksters-angle", InputBits.Up), "up held: climbs 6 units a tick");
		Assert.Equal(0, YAfter("tricksters-angle", InputBits.None), "no up: flies level");
		Assert.Equal(0, YAfter(null, InputBits.Up), "no modifier: flies level");
		var late = Setup("tricksters-angle", SpecialSlot.A, -800, 800);
		Run(late, 20, t => t == 1 ? FighterInput.Special(SpecialSlot.A) : new FighterInput(InputBits.Up));
		Assert.Equal(0, late.Projectiles.Single().Y, "up after the start does nothing");
	}

	[Test]
	public static void NinefoldSparkHalvesTheExCost()
	{
		(bool ex, int meter) ExWith(string? modifier, int meter)
		{
			var m = Setup(modifier, SpecialSlot.B, -600, 600);
			m.P1.Meter = meter;
			Run(m, 1, At(1, FighterInput.Special(SpecialSlot.B, ex: true)));
			return (m.P1.ActiveEx, m.P1.Meter);
		}
		Assert.Equal((true, 0), ExWith("ninefold-spark", 50), "50 meter buys the EX");
		Assert.Equal((true, 50), ExWith("ninefold-spark", 100), "and a bar leaves half");
		Assert.Equal((false, 49), ExWith("ninefold-spark", 49), "49 is not enough: plain version, nothing spent (E16)");
		Assert.Equal((false, 50), ExWith(null, 50), "no modifier: EX costs the data's 100");
	}

	// --- Kappa --------------------------------------------------------------------------------------

	[Test]
	public static void CucumberGiftHealsTwoPercentOnHit()
	{
		Assert.Equal(520, TalismanConnects("cucumber-gift", -60, 60, false, m => m.P1.Health = 500).m.P1.Health, "hit: +2% of 1,000");
		Assert.Equal(500, TalismanConnects("cucumber-gift", -60, 60, true, m => m.P1.Health = 500).m.P1.Health, "blocked: nothing");
		Assert.Equal(1000, TalismanConnects("cucumber-gift", -60, 60, false, m => m.P1.Health = 990).m.P1.Health, "capped at max health");
		Assert.Equal(500, TalismanConnects(null, -60, 60, false, m => m.P1.Health = 500).m.P1.Health, "no modifier");
	}

	[Test]
	public static void RiverPullBringsTheOpponentOneStepCloserOnHit()
	{
		int WaveLeavesP2At(string? modifier, bool block)
		{
			var log = new Log();
			var m = Setup(modifier, SpecialSlot.A, -300, 300, log);
			for (int t = 1; t <= 80 && log.Hits.Count == 0; t++)
				m.Step(t == 1 ? FighterInput.Special(SpecialSlot.A) : Idle, block && t >= 45 ? new FighterInput(InputBits.Right | InputBits.Down) : Idle);
			Assert.Equal(1, log.Hits.Count, "the wave connects");
			return m.P2.X;
		}
		// P2 crouch-blocks (down-back): walking back at 12.8 a tick would outrun the wave (12).
		Assert.Equal(340 * S, WaveLeavesP2At(null, false), "plain: pushed 40 (E5)");
		Assert.Equal(260 * S, WaveLeavesP2At("river-pull", false), "River Pull: pulled 40 (SimConfig.PullStepUnits, proposed) instead");
		Assert.Equal(WaveLeavesP2At(null, true), WaveLeavesP2At("river-pull", true), "blocked: the normal push");

		// Up close the pull stops at the bodies (120 apart), never through them.
		var close = TalismanConnects("river-pull", -70, 70, false).m;
		Assert.Equal(120 * S, close.P2.X - close.P1.X, "bodies touch");
	}

	[Test]
	public static void SlipperySkinCannotBeThrownDuringStartup()
	{
		bool Thrown(string? modifier, int p1Starts)
		{
			var m = Setup(modifier, SpecialSlot.B, -60, 60);
			bool thrown = false;
			// P2's throw (startup 5) is active on ticks 6-7.
			for (int t = 1; t <= 8; t++)
			{
				m.Step(t == p1Starts ? FighterInput.Special(SpecialSlot.B) : Idle, t == 1 ? FighterInput.Attack(Throw(m)) : Idle);
				thrown |= m.P1.State == FighterState.Thrown;
			}
			return thrown;
		}
		Assert.True(Thrown(null, 3), "no modifier: grabbed out of the talisman's startup");
		Assert.True(!Thrown("slippery-skin", 3), "Slippery Skin: frames 4-5 on ticks 6-7 are startup, the throw whiffs");
		Assert.True(Thrown("slippery-skin", 1), "frame 6 on tick 6 is no longer startup: grabbed");
	}

	// --- Cancel rules (X1-X4), from data/cancels/ ------------------------------------------------------

	/// <summary>236+MP (Spirit Wave, slot A) over and over from <paramref name="from"/>, so a command is waiting whenever a cancel opens.</summary>
	static Func<int, FighterInput> Then236(int from, FighterInput first)
	{
		var motion = InputParserTests.Record("2 3 6+MP");
		return t => t == 1 ? first : t < from ? Idle : motion[(t - from) % motion.Count];
	}

	static void GiveWindow(Match m, int slot, int first, int last) =>
		m.P1.Moves[slot] = m.P1.Moves[slot] with { CancelWindows = new[] { new CancelWindow(first, last, true, true, false) } };

	[Test]
	public static void CancelFilesLoadAsTheirPaths()
	{
		Assert.Equal(new CancelRule("fox-step", CancelFrom.Specials, CancelInto.Dash, true, true, true), Cancel("fox-step"), "Fox Step");
		Assert.Equal(new CancelRule("oni-chain", CancelFrom.HeavyNormals, CancelInto.Specials, true, true, false), Cancel("oni-chain"), "Oni Chain");
		Assert.Equal(new CancelRule("undertow", CancelFrom.SuccessfulThrows, CancelInto.OneSpecial, true, false, false), Cancel("undertow"), "Undertow");
		Assert.Equal("fox-step,oni-chain,undertow", string.Join(",", CancelRuleLoader.LoadDirectory($"{Repo}/data/cancels").Select(c => c.Id)), "the folder");
		string fox = File.ReadAllText($"{Repo}/data/cancels/fox-step.json");
		foreach (var (from, to, why) in new[]
		{
			("\"once_per_combo\": true", "\"once_per_combo\": false", "X3 switched off"),
			("\"whiff\"", "\"miss\"", "unknown outcome"),
			("\"into\": \"dash\"", "\"into\": \"jump\"", "unknown target"),
		})
		{
			Assert.True(fox.Contains(from), $"fox-step.json has {from}");
			try { CancelRuleLoader.Parse(fox.Replace(from, to)); }
			catch (FormatException) { continue; }
			throw new AssertFailed($"cancel loader accepted: {why}");
		}
	}

	[Test]
	public static void FoxStepCancelsASpecialIntoADash()
	{
		var seq = new List<FighterInput> { FighterInput.Special(SpecialSlot.A) };
		while (seq.Count < 13) seq.Add(Idle);
		seq.AddRange(InputParserTests.Record("6 5 6")); // 66 finishing on move frame 16, inside the wave's window 14-20
		var m = Setup(null, SpecialSlot.A, -800, 800);
		m.P1.HoldCancelRule(Cancel("fox-step"));
		Run(m, seq.Count, t => seq[t - 1]);
		Assert.Equal(FighterState.Dash, m.P1.State, "special -> dash on whiff");
		var none = Setup(null, SpecialSlot.A, -800, 800);
		Run(none, seq.Count, t => seq[t - 1]);
		Assert.Equal(FighterState.Attack, none.P1.State, "X1: no rule, no cancel");
	}

	[Test]
	public static void OniChainCancelsAHeavyNormalIntoASpecialOnHitNotOnWhiff()
	{
		int FirstSpecialFrame(bool rule, int p2x)
		{
			var log = new Log();
			var m = Setup(null, SpecialSlot.A, -60, p2x, log);
			int hp = Hp(m);
			GiveWindow(m, hp, 11, 30); // the fixture normals carry no cancel window; real ones come from clip data
			if (rule) m.P1.HoldCancelRule(Cancel("oni-chain"));
			Run(m, 90, Then236(10, FighterInput.Attack(hp)));
			return log.Specials.First(s => s.frame > 0).frame;
		}
		// Heavy punch: 10 / 4 / 20 = 34 frames, so without a cancel the wave can't start before world frame 35.
		Assert.True(FirstSpecialFrame(true, 60) < 35, "on hit: cancelled into Spirit Wave");
		Assert.True(FirstSpecialFrame(true, 800) >= 35, "on whiff: Oni Chain lists hit and block only");
		Assert.True(FirstSpecialFrame(false, 60) >= 35, "X1: no rule, no cancel");
	}

	[Test]
	public static void UndertowCancelsOnlyALandedThrow()
	{
		(int special, int land) Go(bool rule)
		{
			var log = new Log();
			var m = Setup(null, SpecialSlot.A, -60, 60, log);
			int th = Throw(m);
			GiveWindow(m, th, 6, 27);
			if (rule) m.P1.HoldCancelRule(Cancel("undertow"));
			Run(m, 90, Then236(2, FighterInput.Attack(th)));
			var landed = log.Throws.Single(t => t.e.Outcome == ThrowOutcome.Land);
			Assert.Equal(120, landed.e.Damage, "the throw lands for its damage");
			return (log.Specials.First().frame, landed.frame);
		}
		var (special, land) = Go(true);
		// Throw: 5 / 2 / 20 = 27 frames. Grab on frame 6, break window 7, lands on world frame 14.
		Assert.True(special >= land && special < 28, $"cancelled after landing (frame {land}), not while it can be broken: {special}");
		Assert.True(Go(false).special >= 28, "X1: no rule, no cancel");
	}

	// --- Determinism --------------------------------------------------------------------------------

	[Test]
	public static void ModifierStateIsInTheHashAndReplaysExactly()
	{
		ulong Play(string modifier)
		{
			var m = Setup(modifier, SpecialSlot.B, -60, 60);
			m.P1.AttachModifier(SpecialSlot.A, Mod("tricksters-angle"), m.Config);
			var rng = new SimRng(103);
			ulong h = 0;
			for (int t = 0; t < 1500 && m.Phase != MatchPhase.Over; t++)
			{
				FighterInput In() => rng.Next(8) == 0
					? FighterInput.Special((SpecialSlot)(1 + rng.Next(2)), rng.Next(2) == 0, rng.Next(2) == 0 ? InputBits.Up : InputBits.None)
					: new FighterInput((InputBits)(rng.NextUInt() & (uint)(InputBits.Directions | InputBits.Attacks)));
				m.Step(In(), In());
				h = h * 31 + m.StateHash();
			}
			return h;
		}
		foreach (string id in new[] { "onis-hide", "illusion-cloak", "cucumber-gift", "brute-recovery" })
			Assert.Equal(Play(id), Play(id), $"{id}: same inputs, same hashes");
		var m = Setup("illusion-cloak", SpecialSlot.B, -600, 600);
		ulong before = m.StateHash();
		m.P1.SpecialUses[(int)SpecialSlot.B] = 1;
		Assert.True(m.StateHash() != before, "the round's use count is state");
		m.P1.SpecialUses[(int)SpecialSlot.B] = 0;
		m.P1.MoveSerial = 1;
		Assert.True(m.StateHash() != before, "so is the move serial a projectile is tied to");
		m.P1.MoveSerial = 0;
		Assert.True(m.StateHash() == before, "and nothing else moved");
	}
}
