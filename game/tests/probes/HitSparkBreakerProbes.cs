using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-198 probes (test-breaker). Expected values come from V2 (hitstop 6/9/12, counter +4), V5, V8 and the preset schema.</summary>
public static class HitSparkBreakerProbes
{
	const InputBits LP = InputBits.LightPunch, LK = InputBits.LightKick, THROW = LP | LK;
	static readonly ElderTwist Reflect = new("t-reflect", "nine-tailed-kitsune", TwistEffect.ReflectRepeatedSpecial, 2, 0, "twist.t", null, null, null);
	static readonly ElderTwist Armour = new("t-armour", "elder-oni", TwistEffect.SpecialArmour, 0, 1, "twist.t", null, null, null);
	static readonly ElderTwist Grip = new("t-grip", "elder-kappa", TwistEffect.UnbreakableThrows, 0, 0, "twist.t", null, null, null);

	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();
	static SpecialData[] Starters() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));

	static Match Make(ElderTwist? twist, int gap)
	{
		var m = new Match(null, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Starters());
		m.P1.X = -gap / 2;
		m.P2.X = gap / 2;
		m.P2.Twist = twist;
		return m;
	}

	static FighterInput Pick(ref uint s, MoveData[] mv)
	{
		s = s * 1664525u + 1013904223u;
		uint r = s >> 16;
		int bucket = (int)(r % 12);
		uint d = (r >> 4) % 4;
		var dir = d == 0 ? InputBits.Right : d == 1 ? InputBits.Left : d == 2 ? InputBits.Down : InputBits.None;
		return bucket switch
		{
			0 or 1 => FighterInput.Attack((int)((r >> 8) % (uint)mv.Length)),
			2 => FighterInput.Special(SpecialSlot.A),
			3 => FighterInput.Special(SpecialSlot.B),
			4 => FighterInput.Special(SpecialSlot.A, true),
			5 => new FighterInput(THROW),
			6 => new FighterInput(InputBits.Up | dir),
			_ => new FighterInput(dir),
		};
	}

	// V5/V6 are presentation: a listener must never change the sim.
	[Test]
	public static void Hash_SameWithAndWithoutListeners_AcrossTwistsAndSeeds()
	{
		var mv = Moves();
		int impacts = 0;
		int body = SimConfig.Default.BodyWidth;
		foreach (var twist in new ElderTwist?[] { null, Reflect, Armour, Grip })
			for (uint seed = 1; seed <= 6; seed++)
			{
				int gap = seed % 2 == 0 ? body : 90000;
				var plain = Make(twist, gap);
				var watched = Make(twist, gap);
				var sparks = new HitSparks();
				watched.Impact += (mm, e) => { sparks.OnImpact(mm, e); impacts++; };
				uint a = seed * 7919, b = seed * 104729;
				for (int t = 0; t < 900; t++)
				{
					var p1 = Pick(ref a, mv); var p2 = Pick(ref b, mv);
					plain.Step(p1, p2);
					watched.Step(p1, p2);
					sparks.Visible(watched.Tick);
					Assert.Equal(plain.StateHash(), watched.StateHash(), $"twist {twist?.Id} seed {seed} tick {t}");
				}
			}
		Assert.True(impacts > 0, "the probe produced impacts");
	}

	// Contact point: never the unset (0,0), at or above the floor, near the defender, whatever the path.
	[Test]
	public static void At_IsSetAndNearTheDefender_OnEveryHitPath()
	{
		var mv = Moves();
		int seen = 0;
		foreach (var twist in new ElderTwist?[] { null, Reflect })
			foreach (int gap in new[] { 0, 60 * SimConfig.Scale, 150 * SimConfig.Scale, 600 * SimConfig.Scale })
				for (uint seed = 1; seed <= 4; seed++)
				{
					var m = Make(twist, gap);
					m.Impact += (mm, e) =>
					{
						seen++;
						var def = mm.Fighters[1 - e.Attacker];
						Assert.True(e.At != (0, 0), $"At unset: {e.MoveId} tick {mm.Tick}");
						Assert.True(e.At.Y >= 0, $"At below floor {e.At} {e.MoveId}");
						int bound = 3 * mm.Config.BodyWidth + 400 * SimConfig.Scale;
						Assert.True(Math.Abs(e.At.X - def.X) <= bound, $"At.X {e.At.X} far from defender {def.X} ({e.MoveId}, attacker {e.Attacker}, tick {mm.Tick})");
					};
					uint a = seed * 31, b = seed * 977;
					for (int t = 0; t < 700; t++) m.Step(Pick(ref a, mv), Pick(ref b, mv));
				}
		Assert.True(seen > 0, "impacts happened");
	}

	[Test]
	public static void Throw_AtIsTheDefendersBodyCentre()
	{
		var m = Make(null, SimConfig.Default.BodyWidth);
		ImpactEvent? got = null;
		m.Impact += (_, e) => got ??= e;
		for (int t = 0; t < 40 && got == null; t++) m.Step(t == 1 ? new FighterInput(THROW) : FighterInput.None, FighterInput.None);
		Assert.True(got != null, "the throw landed");
		Assert.True(got!.Value.At != (0, 0) && Math.Abs(got.Value.At.X - m.P2.X) <= SimConfig.Default.BodyWidth && got.Value.At.Y > 0, $"throw At {got.Value.At} on the defender at {m.P2.X}");
	}

	// V2: spark life = hitstop (6/9/12, counter +4) + fade.
	[Test]
	public static void Life_MatchesV2HitstopPlusFade_AllStrengthsAndKinds()
	{
		foreach (var (s, stop) in new[] { (HitStrength.Light, 6), (HitStrength.Medium, 9), (HitStrength.Heavy, 12) })
		{
			Assert.Equal(stop + HitSparkStyle.FadeFrames, HitSparkStyle.Look(HitSparkKind.Hit, s, "ryo", null).Frames, $"{s} hit");
			Assert.Equal(stop + HitSparkStyle.FadeFrames, HitSparkStyle.Look(HitSparkKind.Block, s, "ryo", null).Frames, $"{s} block");
			Assert.Equal(stop + 4 + HitSparkStyle.FadeFrames, HitSparkStyle.Look(HitSparkKind.Counter, s, "ryo", null).Frames, $"{s} counter");
		}
	}

	[Test]
	public static void Life_EqualsTheSimsOwnHitstopOnEveryImpact()
	{
		var mv = Moves();
		int n = 0;
		for (uint seed = 1; seed <= 4; seed++)
		{
			var m = Make(null, SimConfig.Default.BodyWidth);
			var sparks = new HitSparks();
			m.Impact += (mm, e) =>
			{
				sparks.OnImpact(mm, e);
				var sp = sparks.All[^1];
				Assert.Equal(e.Hitstop + HitSparkStyle.FadeFrames, sp.Look.Frames, $"{e.MoveId} {e.Strength} counter={e.Counter} block={e.Blocked}");
				n++;
			};
			uint a = seed * 13, b = seed * 17;
			for (int t = 0; t < 900; t++) m.Step(Pick(ref a, mv), Pick(ref b, mv));
		}
		Assert.True(n > 0, "impacts");
	}

	[Test]
	public static void Capacity_ManyImpactsOnOneTick_NoCrashBoundedPool()
	{
		var m = Make(null, SimConfig.Default.BodyWidth);
		var sparks = new HitSparks();
		for (int k = 0; k < 5000; k++)
			sparks.OnImpact(m, new ImpactEvent(k % 2, "x", HitStrength.Heavy, 12, k % 3 == 0, k % 5 == 0, false, (k, k)));
		Assert.Equal(HitSparks.Capacity, sparks.Count, "capped at 12");
		Assert.Equal(HitSparks.Capacity, sparks.Visible(m.Tick).Count, "visible capped");
		Assert.Equal(5000 - 1, sparks.All[^1].X, "newest kept");
	}

	// A different match whose tick is not lower than the old one must not show the old match's sparks.
	[Test]
	public static void ReplacedMatch_NoSparksLeak_EvenWhenItsTickIsNotLower()
	{
		var old = Make(null, SimConfig.Default.BodyWidth);
		for (int t = 0; t < 30; t++) old.Step(FighterInput.None, FighterInput.None);
		var sparks = new HitSparks();
		sparks.OnImpact(old, new ImpactEvent(0, "x", HitStrength.Heavy, 12, false, false, false, (10, 100)));
		var next = Make(null, SimConfig.Default.BodyWidth);
		for (int t = 0; t < 31; t++) next.Step(FighterInput.None, FighterInput.None);
		Assert.Equal(0, sparks.Visible(next.Tick).Count, "the old match's spark is gone after a different match is shown");
	}

	// A reset followed by several ticks before the next render must not resurrect the old spark.
	[Test]
	public static void Reset_ThenManyTicksBeforeRender_NoLeak()
	{
		var m = Make(null, SimConfig.Default.BodyWidth);
		for (int t = 0; t < 40; t++) m.Step(FighterInput.None, FighterInput.None);
		var sparks = new HitSparks();
		sparks.OnImpact(m, new ImpactEvent(0, "x", HitStrength.Heavy, 12, false, false, false, (10, 100)));
		sparks.Visible(m.Tick);
		m.Reset();
		for (int t = 0; t < 45; t++) m.Step(FighterInput.None, FighterInput.None);
		Assert.Equal(0, sparks.Visible(m.Tick).Count, "the spark belongs to the match before the reset");
	}

	[Test]
	public static void Colour_ElderYokaiIdsAndUnknowns()
	{
		Assert.Equal(HitSparkStyle.KappaTeal, HitSparkStyle.SourceColour("elder-kappa"), "elder kappa");
		Assert.Equal(HitSparkStyle.OniRed, HitSparkStyle.SourceColour("elder-oni"), "elder oni");
		Assert.Equal(HitSparkStyle.Neutral, HitSparkStyle.SourceColour(null), "null source");
		Assert.Equal(HitSparkStyle.Neutral, HitSparkStyle.SourceColour("Kitsune"), "case differs = unknown, never a crash");
	}

	// ---- loader ---------------------------------------------------------------------------------

	static string Doc(string extra = "", string id = "p", string special = "s", string level = "1", string colour = "\"#102030\"") =>
		"{\"kind\":\"level_preset\",\"id\":\"" + id + "\",\"special\":\"" + special + "\",\"level\":" + level + ",\"colour\":" + colour + extra + "}";

	static bool Refused(string json)
	{
		try { LevelPresetLibrary.Parse(json); }
		catch (FormatException) { return true; }
		return false;
	}

	static void MustRefuse(string what, string json) => Assert.True(Refused(json), "loader must refuse: " + what);

	[Test]
	public static void Loader_RefusesMalformedPresets()
	{
		Assert.True(!Refused(Doc()), "baseline parses");
		foreach (var lv in new[] { "0", "4", "-1", "1.5", "2.0", "\"2\"", "null", "1e0" })
			MustRefuse("level " + lv, Doc(level: lv));
		MustRefuse("short colour", Doc(colour: "\"#12345\""));
		MustRefuse("non-hex colour", Doc(colour: "\"#GG0000\""));
		MustRefuse("long colour", Doc(colour: "\"#1020300\""));
		MustRefuse("numeric colour", Doc(colour: "123"));
		MustRefuse("upper-case id", Doc(id: "A"));
		MustRefuse("double hyphen id", Doc(special: "a--b"));
		MustRefuse("unknown top field", Doc(extra: ",\"extra\":1"));
		MustRefuse("bad status", Doc(extra: ",\"status\":\"draft\""));
		MustRefuse("scale 24", Doc(extra: ",\"spark\":{\"scale_pct\":24}"));
		Assert.True(!Refused(Doc(extra: ",\"spark\":{\"scale_pct\":25,\"frames\":2,\"sheet_columns\":16}")), "lower/upper bounds accepted");
		Assert.True(!Refused(Doc(extra: ",\"spark\":{\"scale_pct\":400,\"frames\":60,\"sheet_columns\":1}")), "upper bounds accepted");
		MustRefuse("scale 401", Doc(extra: ",\"spark\":{\"scale_pct\":401}"));
		MustRefuse("frames 1", Doc(extra: ",\"spark\":{\"frames\":1}"));
		MustRefuse("frames 61", Doc(extra: ",\"spark\":{\"frames\":61}"));
		MustRefuse("columns 0", Doc(extra: ",\"spark\":{\"sheet_columns\":0}"));
		MustRefuse("columns 17", Doc(extra: ",\"spark\":{\"sheet_columns\":17}"));
		MustRefuse("path escape", Doc(extra: ",\"spark\":{\"textures\":{\"hit\":\"../x.png\"}}"));
		MustRefuse("upper-case extension", Doc(extra: ",\"spark\":{\"textures\":{\"hit\":\"a.PNG\"}}"));
		MustRefuse("unknown outcome", Doc(extra: ",\"spark\":{\"textures\":{\"crit\":\"a.png\"}}"));
		MustRefuse("tint not bool", Doc(extra: ",\"spark\":{\"tint\":\"yes\"}"));
		MustRefuse("array root", "[]");
		MustRefuse("spark array", Doc(extra: ",\"spark\":[]"));
	}

	[Test]
	public static void Loader_RefusesTrailingNewlineInIdsColoursAndPaths()
	{
		MustRefuse("colour with trailing newline", Doc(colour: "\"#102030\\n\""));
		MustRefuse("special id with trailing newline (ECMA $ does not match before newline)", Doc(special: "a\\n"));
		MustRefuse("texture trailing newline", Doc(extra: ",\"spark\":{\"textures\":{\"hit\":\"a/b.png\\n\"}}"));
	}

	[Test]
	public static void Library_RefusesDuplicateSpecialAndLevel_AndLevelOutsideOneToThree()
	{
		var a = new LevelPreset("a", "s", 1, 1, SparkPreset.None);
		var b = new LevelPreset("b", "s", 1, 2, SparkPreset.None);
		var c = new LevelPreset("c", "s", 2, 2, SparkPreset.None);
		bool refused = false;
		try { new LevelPresetLibrary(new[] { a, b }); } catch (FormatException) { refused = true; }
		Assert.True(refused, "duplicate (special, level)");
		var lib = new LevelPresetLibrary(new[] { a, c });
		Assert.True(lib.For("s", 2) == c && lib.For("s", 1) == a && lib.For("s", 3) is null && lib.For(null, 1) is null, "lookup");
		bool refusedLevel = false;
		try { new LevelPresetLibrary(new[] { new LevelPreset("z", "s", 4, 1, SparkPreset.None) }); } catch (FormatException) { refusedLevel = true; }
		Assert.True(refusedLevel, "A3: a level outside 1-3 is refused by the library too");
	}

	[Test]
	public static void Preset_NeverDressesANormal()
	{
		var p = new LevelPreset("p", "test-spirit-wave", 3, 0x123456, new SparkPreset(Colour: 0x654321));
		var lib = new LevelPresetLibrary(new[] { p });
		var m = Make(null, SimConfig.Default.BodyWidth);
		var sp = new HitSparks(lib);
		var jab = m.P1.Moves.First(x => !x.IsThrow);
		sp.OnImpact(m, new ImpactEvent(0, jab.Id, HitStrength.Light, 6, false, false, false, (5, 5)));
		Assert.Equal(HitSparkStyle.SourceColour(jab.Source), sp.All[0].Look.Colour, "a normal is not dressed by a special's preset");
	}
}
