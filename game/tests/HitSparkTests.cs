using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-198 (V5, V6, V8): hit sparks, the level-preset loader and the sound-cue hook. Moves are the TEST FIXTURE
/// files; the one preset is the fixture in tests/fixtures/presets/ (never game content).
/// </summary>
public static class HitSparkTests
{
	static readonly FighterInput Idle = FighterInput.None;
	static FighterInput In(InputBits b) => new(b);

	static MoveData[] Normals() => MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir));
	static int SlotOf(MoveData[] moves, InputBits b) => Array.FindIndex(moves, m => m.Button == b && !m.Air && m.DirectionMask == 0);
	static string PresetFixtures => ProjectSettings.GlobalizePath("res://tests/fixtures/presets");

	static Match Close(MoveData[] moves, List<ImpactEvent> impacts)
	{
		var m = new Match(null, moves, moves);
		m.P1.X = -m.Config.BodyWidth / 2;
		m.P2.X = m.Config.BodyWidth / 2;
		m.Impact += (_, e) => impacts.Add(e);
		return m;
	}

	/// <summary>P1 presses <paramref name="b"/>; P2 holds <paramref name="p2"/>; steps until the first impact.</summary>
	static void LandOne(Match m, MoveData[] n, InputBits b, FighterInput p2, List<ImpactEvent> impacts)
	{
		m.Step(FighterInput.Attack(SlotOf(n, b)), p2);
		for (int t = 0; t < 40 && impacts.Count == 0; t++) m.Step(Idle, p2);
		Assert.True(impacts.Count > 0, $"{b} connects");
	}

	[Test]
	public static void Impact_CarriesTheContactPointInsideBothBoxes()
	{
		var n = Normals();
		foreach (var b in new[] { InputBits.LightPunch, InputBits.HeavyPunch, InputBits.LightKick })
		{
			var imp = new List<ImpactEvent>();
			var m = Close(n, imp);
			int p1x = m.P1.X, p2x = m.P2.X;
			var hitbox = new List<(int x0, int y0, int x1, int y1)>();
			m.Hit += (mm, e) =>
			{
				var mv = mm.P1.CurrentMove!;
				foreach (var hb in mv.Hitboxes) if (hb.Covers(e.Frame)) hitbox.Add(Match.WorldBox(mm.P1, hb.Box));
			};
			LandOne(m, n, b, Idle, imp);
			var at = imp[0].At;
			Assert.True(at.X > p1x && at.X <= p2x, $"{b}: spark x {at.X} between the attacker {p1x} and the defender {p2x} (before the push)");
			Assert.True(hitbox.Any(h => at.X >= h.x0 && at.X <= h.x1 && at.Y >= h.y0 && at.Y <= h.y1), $"{b}: spark ({at.X},{at.Y}) inside the striking box");
			Assert.True(at.Y > 0, $"{b}: above the floor");
		}
	}

	// --- ImpactEvent.At on every other hit path (throw, projectile, aftershock, reflect) --------------------

	const int S = SimConfig.Scale;
	static SpecialData[] Starters() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));
	static MoveData ThrowMove() => MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir)).Single();

	/// <summary>An impact with both fighters' stance hurtboxes and move state as they stood before the tick that raised it.</summary>
	sealed record Caught(ImpactEvent E, (int x0, int y0, int x1, int y1)[] Stance, bool[] InMove);

	/// <summary>
	/// Steps ticks 1..n, snapshotting both stance boxes before each step. A fighter standing still with no move does not
	/// move within a tick before hits resolve, so its pre-step box is where it stood when the hit was found, before the push.
	/// </summary>
	static List<Caught> Capture(Match m, int n, Func<int, FighterInput> p1, Func<int, FighterInput>? p2 = null)
	{
		var got = new List<Caught>();
		var stance = new (int x0, int y0, int x1, int y1)[2];
		var inMove = new bool[2];
		m.Impact += (_, e) => got.Add(new Caught(e, ((int, int, int, int)[])stance.Clone(), (bool[])inMove.Clone()));
		for (int t = 1; t <= n; t++)
		{
			for (int i = 0; i < 2; i++)
			{
				stance[i] = Match.WorldBox(m.Fighters[i], m.BodyHurtbox(m.Fighters[i]));
				inMove[i] = m.Fighters[i].CurrentMove != null;
			}
			m.Step(p1(t), p2?.Invoke(t) ?? Idle);
		}
		return got;
	}

	/// <summary>At is set (not the world origin) and inside fighter <paramref name="who"/>'s stance box before the push.</summary>
	static void AssertInStance(Caught c, int who, string what)
	{
		var (x, y) = c.E.At;
		var b = c.Stance[who];
		Assert.True(c.E.At != default, $"{what}: At is set");
		Assert.True(!c.InMove[who], $"{what}: fighter {who} was in no move, so its stance box is its hurtbox");
		Assert.True(x >= b.x0 && x <= b.x1 && y >= b.y0 && y <= b.y1, $"{what}: At ({x},{y}) inside fighter {who}'s box {b} before the push");
	}

	[Test]
	public static void At_Throw_IsTheDefendersBodyCentre()
	{
		var moves = Normals().Append(ThrowMove()).ToArray();
		var m = new Match(null, moves, moves);
		m.P1.X = -m.Config.BodyWidth / 2;
		m.P2.X = m.Config.BodyWidth / 2;
		const InputBits Throw = InputBits.LightPunch | InputBits.LightKick;
		var got = Capture(m, 40, t => t == 1 ? In(Throw) : Idle);
		Assert.Equal(1, got.Count, "one impact: the landed throw");
		Assert.True(got[0].E.MoveId == ThrowMove().Id, "it is the throw");
		AssertInStance(got[0], 1, "throw");
		var b = got[0].Stance[1];
		Assert.Equal(((b.x0 + b.x1) / 2, (b.y0 + b.y1) / 2), got[0].E.At, "throw: the stance box's centre");
	}

	[Test]
	public static void At_Projectile_IsInsideTheDefendersHurtbox()
	{
		var n = Normals();
		var m = new Match(null, n, n);
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Starters());
		m.P1.X = -300 * S;
		m.P2.X = 300 * S;
		var got = Capture(m, 80, t => t == 1 ? FighterInput.Special(SpecialSlot.A) : Idle);
		Assert.Equal(1, got.Count, "one impact: the wave");
		Assert.True(m.Projectiles.Count == 0, "the projectile hit and despawned");
		AssertInStance(got[0], 1, "projectile");
		Assert.True(got[0].E.At.X > 0, "on the defender's side of the stage, far from the firer");
	}

	[Test]
	public static void At_Aftershock_IsInsideTheDefendersHurtbox()
	{
		var q = PushBoxAftershockFixture();
		var cfg = SimConfig.Default with { HitstopEnabled = false };
		var m = new Match(cfg, new[] { q }, new[] { q });
		m.P1.X = -60 * S;
		m.P2.X = 60 * S;
		var got = Capture(m, 60, t => t == 1 ? FighterInput.Attack(0) : Idle);
		Assert.Equal(2, got.Count, "the quake and its aftershock");
		AssertInStance(got[0], 1, "quake");
		AssertInStance(got[1], 1, "aftershock (the defender was pushed by the first quake; its box is taken the tick before)");
	}

	/// <summary>The Lv 3 test quake (TEST FIXTURE numbers, same as PushBoxAftershockTests): a low pound with a 30-frame aftershock.</summary>
	static MoveData PushBoxAftershockFixture() => SpecialLoader.Parse("""
	{
	  "kind": "special", "id": "test-quake", "name": "TEST FIXTURE", "source": "test-quaker", "starter": false,
	  "rarity": "rare", "locked": true, "clip": "test-quake", "rules": ["A5"],
	  "input": { "kata": { "slot": "D", "motion": "22" }, "kihon": { "slot": "D", "direction": "down" } },
	  "card": { "plain": "TEST FIXTURE", "frames": "9 / 3 / 20" },
	  "levels": {
	    "1": { "frame_data": {
	      "startup": 9, "active": 3, "recovery": 20, "damage": 50, "hitstun": 20, "blockstun": 15,
	      "hitboxes": [ { "frames": [10, 12], "rect": { "x": 0, "y": 0, "w": 300, "h": 110 } } ],
	      "hurtboxes": [ { "frames": [1, 32], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } } ],
	      "properties": { "low": true } } },
	    "2": { "status": "proposed", "tuning": { "target": "recovery", "op": "add", "value": -2 } },
	    "3": { "evolution": { "id": "test-aftershock", "name": "TEST FIXTURE", "preset": "test-aftershock",
	      "changes": [ { "target": "properties.aftershock_delay", "op": "set", "value": 30 } ] } }
	  }
	}
	""").Build(3, false);

	[Test]
	public static void At_ReflectedStrike_IsWhereTheElderCaughtIt()
	{
		var n = Normals();
		var m = new Match(null, n, n);
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Starters());
		m.P2.Twist = new ElderTwist("t-reflect", "nine-tailed-kitsune", TwistEffect.ReflectRepeatedSpecial, 2, 0, "twist.t", null, null, null);
		var talisman = FighterInput.Special(SpecialSlot.B);
		m.P1.X = -60 * S;
		m.P2.X = 60 * S;
		m.Step(talisman, Idle);
		for (int t = 0; t < 150; t++) m.Step(Idle, Idle);
		m.P1.X = -60 * S;
		m.P2.X = 60 * S;
		var got = Capture(m, 30, t => t == 1 ? talisman : Idle);
		Assert.Equal(1, got.Count, "one impact: the reflected talisman");
		Assert.Equal(1, got[0].E.Attacker, "raised as the elder's hit");
		// The spark is where Ryo's talisman met the elder's body (the overlap the reflect was caught at), not on Ryo.
		AssertInStance(got[0], 1, "reflect");
	}

	/// <summary>
	/// A match with a spark listener and one without, both on this branch, stay hash-identical every tick: listening
	/// never feeds back. (That the YOK-198 sim edit itself is payload-only is shown by the frame-exact suites, e.g.
	/// ReplayTests, MeterBurstHitstopTests, ThrowTests, passing unchanged; the repo keeps no golden hashes from main.)
	/// </summary>
	[Test]
	public static void Sparks_ListeningLeavesTheMatchUnchanged()
	{
		var n = Normals();
		int lp = SlotOf(n, InputBits.LightPunch), hp = SlotOf(n, InputBits.HeavyPunch);
		FighterInput P1(int t) => t % 37 == 0 ? FighterInput.Attack(t % 74 == 0 ? hp : lp) : t % 5 == 0 ? In(InputBits.Right) : Idle;
		FighterInput P2(int t) => t % 90 < 30 ? In(InputBits.Right) : Idle;
		var plain = Close(n, new List<ImpactEvent>());
		var watched = Close(n, new List<ImpactEvent>());
		var sparks = new HitSparks();
		int seen = 0;
		watched.Impact += (mm, e) => { sparks.OnImpact(mm, e); seen++; };
		for (int t = 0; t < 1200; t++)
		{
			plain.Step(P1(t), P2(t));
			watched.Step(P1(t), P2(t));
			sparks.Visible(watched.Tick);
			Assert.Equal(plain.StateHash(), watched.StateHash(), $"tick {t}: same state with sparks listening");
		}
		Assert.True(seen > 0, "the run had impacts");
	}

	[Test]
	public static void Kinds_HitBlockCounterLookDistinct()
	{
		var e = new ImpactEvent(0, "x", HitStrength.Medium, 9, false, false, false);
		Assert.Equal(HitSparkKind.Hit, HitSparkStyle.KindOf(e), "hit");
		Assert.Equal(HitSparkKind.Block, HitSparkStyle.KindOf(e with { Blocked = true }), "block");
		Assert.Equal(HitSparkKind.Counter, HitSparkStyle.KindOf(e with { Counter = true }), "counter");
		var hit = HitSparkStyle.Look(HitSparkKind.Hit, HitStrength.Medium, "ryo", null);
		var block = HitSparkStyle.Look(HitSparkKind.Block, HitStrength.Medium, "ryo", null);
		var counter = HitSparkStyle.Look(HitSparkKind.Counter, HitStrength.Medium, "ryo", null);
		Assert.True(hit.Shape != block.Shape && hit.Shape != counter.Shape && block.Shape != counter.Shape, "three shapes");
		Assert.True(block.SizePct < hit.SizePct && counter.SizePct > hit.SizePct, "block smaller, counter bigger");
		Assert.True(HitSparkArt.DefaultPath(HitSparkKind.Hit) != HitSparkArt.DefaultPath(HitSparkKind.Block)
			&& HitSparkArt.DefaultPath(HitSparkKind.Block) != HitSparkArt.DefaultPath(HitSparkKind.Counter), "three default images");
	}

	[Test]
	public static void Size_FollowsTheHitWeight()
	{
		int l = HitSparkStyle.Look(HitSparkKind.Hit, HitStrength.Light, "ryo", null).SizePct;
		int md = HitSparkStyle.Look(HitSparkKind.Hit, HitStrength.Medium, "ryo", null).SizePct;
		int h = HitSparkStyle.Look(HitSparkKind.Hit, HitStrength.Heavy, "ryo", null).SizePct;
		Assert.True(l < md && md < h, $"light {l} < medium {md} < heavy {h}");
		Assert.Equal(100, l, "light = 100%");
		Assert.Equal(200, h, "heavy = 12/6 of light (V2 weights)");
		// Life: the hitstop the hit rides on (V2) plus the fade, whether or not the fight's hitstop is switched on.
		Assert.Equal(12 + HitSparkStyle.FadeFrames, HitSparkStyle.Look(HitSparkKind.Hit, HitStrength.Heavy, "ryo", null).Frames, "heavy life");
		Assert.Equal(16 + HitSparkStyle.FadeFrames, HitSparkStyle.Look(HitSparkKind.Counter, HitStrength.Heavy, "ryo", null).Frames, "counter heavy life");
	}

	[Test]
	public static void Colour_BySource_V5()
	{
		Assert.Equal(0xF2A23Au, HitSparkStyle.SourceColour("kitsune"), "foxfire orange");
		Assert.Equal(0x3E7F86u, HitSparkStyle.SourceColour("kappa"), "kappa teal = water teal");
		Assert.Equal(0x1D1B21u, HitSparkStyle.SourceColour("ryo"), "Ryo ink black");
		Assert.Equal(HitSparkStyle.Ink, HitSparkStyle.SourceColour("tanuki"), "the Tanuki fights in Ryo's ink");
		Assert.Equal(HitSparkStyle.OniRed, HitSparkStyle.SourceColour("oni"), "oni red");
		Assert.Equal(HitSparkStyle.Foxfire, HitSparkStyle.SourceColour("nine-tailed-kitsune"), "an elder sparks as its yokai");
		Assert.Equal(HitSparkStyle.Neutral, HitSparkStyle.SourceColour(""), "unknown source: the neutral default");
		var set = new HashSet<uint> { HitSparkStyle.Ink, HitSparkStyle.Foxfire, HitSparkStyle.OniRed, HitSparkStyle.KappaTeal };
		Assert.Equal(4, set.Count, "four distinct source colours");
	}

	[Test]
	public static void Timing_SparkLivesItsFramesOnTheSimTick()
	{
		var n = Normals();
		var imp = new List<ImpactEvent>();
		var m = Close(n, imp);
		var sparks = new HitSparks();
		int spawn = -1;
		m.Impact += (mm, e) => { sparks.OnImpact(mm, e); spawn = mm.Tick; };
		LandOne(m, n, InputBits.HeavyPunch, Idle, imp);
		int life = sparks.All[0].Look.Frames;
		Assert.Equal(spawn, sparks.All[0].SpawnTick, "spawned on the impact tick");
		var first = sparks.Visible(m.Tick);
		Assert.Equal(1, first.Count, "shown on the impact tick");
		Assert.Equal(0, first[0].Age, "age 0");
		Assert.True(first[0].ScalePct < 100, "pops in");
		int lastAlpha = 101, shown = 1;
		while (true)
		{
			m.Step(Idle, Idle);
			var v = sparks.Visible(m.Tick);
			if (v.Count == 0) break;
			shown++;
			Assert.True(v[0].AlphaPct <= lastAlpha || v[0].Age < life - HitSparkStyle.FadeFrames, "fades, never brightens at the end");
			lastAlpha = v[0].AlphaPct;
			Assert.True(v[0].AlphaPct > 0, "a shown spark is never fully clear");
		}
		Assert.Equal(life, shown, "shown for exactly its frames (hitstop ticks included)");
		Assert.Equal(0, sparks.Count, "spent sparks dropped");

		// Same tick twice: the same frame (pause, frame step, re-render).
		var a = new HitSparks();
		var s = new ImpactEvent(0, "x", HitStrength.Light, 6, false, false, false, (100, 2000));
		var m2 = Close(n, new List<ImpactEvent>());
		m2.Step(Idle, Idle);
		a.OnImpact(m2, s);
		Assert.Equal(a.Visible(m2.Tick)[0], a.Visible(m2.Tick)[0], "re-rendering a tick is identical");
		// A reset (tick goes back) drops the sparks.
		m2.Reset();
		Assert.Equal(0, a.Visible(m2.Tick).Count, "reset clears");
	}

	[Test]
	public static void Capacity_DropsTheOldest()
	{
		var m = Close(Normals(), new List<ImpactEvent>());
		var sparks = new HitSparks();
		for (int k = 0; k < HitSparks.Capacity + 3; k++)
			sparks.OnImpact(m, new ImpactEvent(0, "x", HitStrength.Light, 6, false, false, false, (k, 0)));
		Assert.Equal(HitSparks.Capacity, sparks.Count, "capped");
		Assert.Equal(3, sparks.All[0].X, "oldest went first");
	}

	[Test]
	public static void Preset_DressesAnEquippedSpecialAtItsLevel_ElseDefault()
	{
		var lib = LevelPresetLibrary.LoadDirectory(PresetFixtures);
		var wave = SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir)).First(s => s.Id == "test-spirit-wave");
		var preset = lib.For("test-spirit-wave", 3)!;
		Assert.True(preset != null, "fixture preset loads for Lv 3");

		foreach (int level in new[] { 1, 2, 3 })
		{
			var m = Close(Normals(), new List<ImpactEvent>());
			m.P1.Equip(wave.Slot, wave, level);
			var (source, special, lv) = HitSparks.Identify(m, 0, wave.Id);
			Assert.Equal("ryo", source, "source from the special");
			Assert.Equal("test-spirit-wave", special, "special id");
			Assert.Equal(level, lv, "equipped level");
			var sparks = new HitSparks(lib);
			var cue = sparks.OnImpact(m, new ImpactEvent(0, wave.Id, HitStrength.Medium, 9, false, false, false, (0, 0)));
			var look = sparks.All[0].Look;
			if (level == 3)
			{
				Assert.Equal(preset!.Spark.Colour!.Value, look.Colour, "Lv 3: the preset's spark colour");
				Assert.Equal(150 * preset.Spark.ScalePct!.Value / 100, look.SizePct, "Lv 3: preset scale");
				Assert.Equal(preset.Spark.HitTexture, look.Texture, "Lv 3: preset image");
			}
			else
			{
				Assert.Equal(HitSparkStyle.Ink, look.Colour, $"Lv {level}: no preset, default ink spark");
				Assert.True(look.Texture is null, $"Lv {level}: default image");
			}
			Assert.Equal(level, cue.Level, "the cue carries the level");
		}
		// A normal: its file's source, no special.
		var mn = Close(Normals(), new List<ImpactEvent>());
		var jab = mn.P1.Moves[SlotOf(mn.P1.Moves, InputBits.LightPunch)];
		Assert.Equal(("ryo", (string?)null, 1), HitSparks.Identify(mn, 0, jab.Id), "a normal");
		Assert.Equal(("", (string?)null, 1), HitSparks.Identify(mn, 0, "no-such-move"), "unknown move");
	}

	[Test]
	public static void Preset_FallbackFillsWhatThePresetLeavesOut()
	{
		var p = new LevelPreset("p", "s", 2, 0x123456, SparkPreset.None);
		var look = HitSparkStyle.Look(HitSparkKind.Block, HitStrength.Heavy, "kitsune", p);
		var def = HitSparkStyle.Look(HitSparkKind.Block, HitStrength.Heavy, "kitsune", null);
		Assert.Equal(0x123456u, look.Colour, "preset colour when spark.colour is left out");
		Assert.Equal(def.SizePct, look.SizePct, "default size");
		Assert.Equal(def.Frames, look.Frames, "default life");
		Assert.True(look.Texture is null, "default image");
	}

	[Test]
	public static void Loader_ReadsTheSchemaAndRefusesWhatItRefuses()
	{
		const string ok = "{\"kind\":\"level_preset\",\"id\":\"a-lv2\",\"special\":\"a\",\"level\":2,\"colour\":\"#AABBCC\"}";
		var p = LevelPresetLibrary.Parse(ok);
		Assert.Equal(0xAABBCCu, p.Colour, "colour");
		Assert.Equal(2, p.Level, "level");
		Assert.Equal(SparkPreset.None, p.Spark, "no spark block = defaults");

		string With(string extra) => ok.TrimEnd('}') + "," + extra + "}";
		var bad = new (string json, string field)[]
		{
			(ok.Replace("level_preset", "special"), "kind"),
			(ok.Replace("\"level\":2", "\"level\":4"), "level"),
			(ok.Replace("\"level\":2", "\"level\":0"), "level"),
			(ok.Replace("\"level\":2", "\"level\":1.5"), "level"),
			(ok.Replace("#AABBCC", "AABBCC"), "colour"),
			(ok.Replace("#AABBCC", "#AABBC"), "colour"),
			(ok.Replace(",\"colour\":\"#AABBCC\"", ""), "colour"),
			(ok.Replace("\"special\":\"a\"", "\"special\":\"A\""), "special"),
			(With("\"sound\":1"), "sound"),
			(With("\"spark\":{\"glow\":true}"), "spark.glow"),
			(With("\"spark\":{\"scale_pct\":10}"), "spark.scale_pct"),
			(With("\"spark\":{\"frames\":1}"), "spark.frames"),
			(With("\"spark\":{\"sheet_columns\":0}"), "spark.sheet_columns"),
			(With("\"spark\":{\"tint\":\"yes\"}"), "spark.tint"),
			(With("\"spark\":{\"textures\":{\"hit\":\"../x.png\"}}"), "spark.textures.hit"),
			(With("\"spark\":{\"textures\":{\"hit\":\"res://vfx/x.png\"}}"), "spark.textures.hit"),
			(With("\"spark\":{\"textures\":{\"glow\":\"vfx/x.png\"}}"), "spark.textures.glow"),
			(With("\"status\":\"maybe\""), "status"),
		};
		foreach (var (json, field) in bad)
		{
			string? msg = null;
			try { LevelPresetLibrary.Parse(json); }
			catch (FormatException e) { msg = e.Message; }
			Assert.True(msg != null && msg.StartsWith(field), $"refused naming '{field}': {json} -> {msg ?? "accepted"}");
		}

		string full = With("\"status\":\"proposed\",\"spark\":{\"colour\":\"#010203\",\"scale_pct\":120,\"frames\":20,\"textures\":{\"hit\":\"vfx/a/hit.png\",\"counter\":\"vfx/a/c.webp\"},\"sheet_columns\":4,\"tint\":false}");
		var f = LevelPresetLibrary.Parse(full);
		Assert.Equal(new SparkPreset(0x010203, 120, 20, "vfx/a/hit.png", null, "vfx/a/c.webp", 4, false), f.Spark, "every spark field");
		Assert.Equal("vfx/a/c.webp", f.Spark.TextureFor(HitSparkKind.Counter), "counter image");
		Assert.True(f.Spark.TextureFor(HitSparkKind.Block) is null, "block left out");
	}

	[Test]
	public static void Loader_DirectoryRules()
	{
		Assert.Equal(LevelPresetLibrary.Empty, LevelPresetLibrary.LoadDirectory(Path.Combine(PresetFixtures, "no-such-folder")), "absent folder = no presets");
		string tmp = Path.Combine(Path.GetTempPath(), "yok198-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tmp);
		try
		{
			File.WriteAllText(Path.Combine(tmp, "a-lv1.json"), "{\"kind\":\"level_preset\",\"id\":\"a-lv1\",\"special\":\"a\",\"level\":1,\"colour\":\"#000000\"}");
			Assert.Equal(1, LevelPresetLibrary.LoadDirectory(tmp).All.Count, "one preset");
			File.WriteAllText(Path.Combine(tmp, "other.json"), "{\"kind\":\"level_preset\",\"id\":\"a-again\",\"special\":\"a\",\"level\":1,\"colour\":\"#000000\"}");
			string? why = null;
			try { LevelPresetLibrary.LoadDirectory(tmp); } catch (FormatException e) { why = e.Message; }
			Assert.True(why != null && why.Contains("file name"), $"id must equal the file name: {why}");
			File.Delete(Path.Combine(tmp, "other.json"));
			File.WriteAllText(Path.Combine(tmp, "a-again.json"), "{\"kind\":\"level_preset\",\"id\":\"a-again\",\"special\":\"a\",\"level\":1,\"colour\":\"#000000\"}");
			why = null;
			try { LevelPresetLibrary.LoadDirectory(tmp); } catch (FormatException e) { why = e.Message; }
			Assert.True(why != null && why.Contains("both dress 'a' Lv 1"), $"one preset per special and level: {why}");
			Assert.Equal(LevelPresetLibrary.Empty, FightScene.LoadPresets(tmp), "the scene falls back to default sparks on a bad folder");
		}
		finally { Directory.Delete(tmp, true); }
	}

	[Test]
	public static void Art_SeamPicksPresetThenDefaultThenProcedural()
	{
		var look = new HitSparkLook(0, 100, 10, HitSparkShape.Burst, "vfx/hit_sparks/test-lv3.png");
		Assert.Equal("res://vfx/hit_sparks/test-lv3.png", HitSparkArt.Pick(look, HitSparkKind.Hit, _ => true), "the preset's image first");
		Assert.Equal("res://vfx/hit_sparks/hit.png", HitSparkArt.Pick(look, HitSparkKind.Hit, p => p.EndsWith("/hit.png")), "else the default image");
		Assert.Equal("res://vfx/hit_sparks/counter.png", HitSparkArt.Pick(look with { Texture = null }, HitSparkKind.Counter, _ => true), "no preset image: default for the outcome");
		Assert.Equal("procedural:burst", HitSparkArt.Pick(look, HitSparkKind.Hit, _ => false), "else the procedural placeholder");
		var t1 = HitSparkArt.Procedural(HitSparkShape.Ring);
		Assert.True(ReferenceEquals(t1, HitSparkArt.Procedural(HitSparkShape.Ring)), "drawn once per shape");
		var img = t1.GetImage();
		Assert.True(img.GetPixel(HitSparkArt.ProceduralSize / 2, HitSparkArt.ProceduralSize / 2).A < 0.05f, "ring: clear centre");
		Assert.True(HitSparkArt.Procedural(HitSparkShape.Burst).GetImage().GetPixel(HitSparkArt.ProceduralSize / 2, HitSparkArt.ProceduralSize / 2).A > 0.9f, "burst: solid core");
	}

	sealed class Recorder : IHitCueSink
	{
		public readonly List<HitCue> Cues = new();
		public void Play(in HitCue cue) => Cues.Add(cue);
	}

	[Test]
	public static void Scene_SpawnsSparkAndCueOnTheImpactTick(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		try
		{
			Assert.True(scene.SparkLayer != null && scene.SparkLayer.GetParent() == scene, "the layer is in the fight scene");
			Assert.True(scene.HitCues is SilentHitCues, "the sound hook is silent by default");
			if (scene.P2Ai != null) scene.ToggleP2Ai();
			var rec = new Recorder();
			scene.HitCues = rec;
			var m = scene.Match;
			m.P1.X = -m.Config.BodyWidth / 2;
			m.P2.X = m.Config.BodyWidth / 2;
			int lp = SlotOf(m.P1.Moves, InputBits.LightPunch);
			Assert.True(lp >= 0, "Ryo has a jab");
			scene.Step(FighterInput.Attack(lp), Idle);
			for (int t = 0; t < 40 && rec.Cues.Count == 0; t++) scene.Step(Idle, Idle);
			Assert.Equal(1, rec.Cues.Count, "one cue for the jab");
			Assert.Equal(m.Tick, rec.Cues[0].Tick, "the cue is on the impact tick (V6)");
			Assert.Equal(1, scene.SparkLayer!.Shown, "one spark drawn on the impact tick");
			var sprite = scene.SparkLayer.Sprites[0];
			Assert.True(sprite.Visible && sprite.Texture != null, "the spark has an image");
			Assert.True(Mathf.Abs(sprite.Position.X - FightScene.ToMeters(rec.Cues[0].X)) < 1e-4f, "at the contact point");
			for (int t = 0; t < 60; t++) scene.Step(Idle, Idle);
			Assert.Equal(0, scene.SparkLayer.Shown, "gone after its life");
		}
		finally { scene.QueueFree(); }
	}
}
