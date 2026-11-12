using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-242 (V8, A5, F3): projectile visuals and special-move effects. Moves are the TEST FIXTURE files and hand-built
/// <see cref="MoveData"/>; presets are parsed in place, except the check that data/presets/ loads.
/// </summary>
public static class SpecialVfxTests
{
	const int S = SimConfig.Scale;
	static readonly FighterInput Idle = FighterInput.None;

	static MoveData[] Normals() => MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir));
	static SpecialData[] Starters() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));

	const string WavePreset = """
		{ "kind": "level_preset", "id": "w", "special": "test-spirit-wave", "level": 1, "colour": "#2D3A5E",
		  "projectile": { "look": "ink_wave", "core": "#F1E8D4", "scale_pct": 150, "texture": "vfx/projectiles/w.png", "sheet_columns": 4, "tint": false },
		  "cast": { "look": "seal", "colour": "#B5332B", "frames": 30 },
		  "release": { "look": "none" },
		  "impact": { "look": "dust", "scale_pct": 200, "core": "#FBF6EC" } }
		""";

	/// <summary>Both fighters with the fixture starters, far apart: P1's Spirit Wave (slot A) flies at P2.</summary>
	static Match Apart()
	{
		var n = Normals();
		var m = new Match(null, n, n);
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Starters());
		m.P1.X = -300 * S;
		m.P2.X = 300 * S;
		return m;
	}

	static FighterInput P1At(int t, int fireOn) => t == fireOn ? FighterInput.Special(SpecialSlot.A) : Idle;

	// --- data ------------------------------------------------------------------------------------------------------

	[Test]
	public static void Preset_ParsesEveryVfxBlock()
	{
		var p = LevelPresetLibrary.Parse(WavePreset);
		Assert.Equal(new VfxPreset(VfxLook.InkWave, null, 0xF1E8D4, 150, null, "vfx/projectiles/w.png", 4, false), p.Projectile, "projectile block");
		Assert.Equal(new VfxPreset(VfxLook.Seal, 0xB5332B, null, null, 30), p.Cast, "cast block");
		Assert.Equal(VfxLook.None, p.Release!.Look, "release turned off");
		Assert.Equal(new VfxPreset(VfxLook.Dust, null, 0xFBF6EC, 200), p.Impact, "impact block");
		var bare = LevelPresetLibrary.Parse("""{ "kind": "level_preset", "id": "b", "special": "s", "level": 2, "colour": "#102030" }""");
		Assert.True(bare.Projectile is null && bare.Cast is null && bare.Release is null && bare.Impact is null, "blocks are optional");
	}

	[Test]
	public static void Preset_RefusesBadVfxBlocks()
	{
		const string head = """{ "kind": "level_preset", "id": "b", "special": "s", "level": 1, "colour": "#102030", """;
		var cases = new (string Body, string Needle)[]
		{
			("""  "projectile": { "look": "seal" } }""", "projectile.look"),
			("""  "cast": { "look": "fox_flame" } }""", "cast.look"),
			("""  "impact": { "look": "sparkle" } }""", "impact.look"),
			("""  "projectile": { "frames": 10 } }""", "projectile.frames"),
			("""  "release": { "glow": 1 } }""", "release.glow"),
			("""  "cast": { "scale_pct": 10 } }""", "cast.scale_pct"),
			("""  "cast": { "frames": 1 } }""", "cast.frames"),
			("""  "impact": { "core": "#12345" } }""", "impact.core"),
			("""  "projectile": { "texture": "res://x.png" } }""", "projectile.texture"),
			("""  "projectile": { "tint": 1 } }""", "projectile.tint"),
			("""  "cast": [] }""", "cast: must be an object"),
		};
		foreach (var (body, needle) in cases)
		{
			string? msg = null;
			try { LevelPresetLibrary.Parse(head + body); } catch (FormatException e) { msg = e.Message; }
			Assert.True(msg != null && msg.Contains(needle), $"refused naming {needle}: {msg ?? "accepted"}");
		}
	}

	[Test]
	public static void DataPresets_GiveEachProjectileADistinctLook()
	{
		var lib = LevelPresetLibrary.LoadDirectory(ContentPaths.Data("presets"));
		var wave = SpecialVfxStyle.ProjectileStyle(lib.For("spirit-wave", 1), "ryo");
		var fox = SpecialVfxStyle.ProjectileStyle(lib.For("foxfire", 1), "kitsune");
		var great = SpecialVfxStyle.ProjectileStyle(lib.For("spirit-wave", 3), "ryo");
		Assert.Equal(VfxLook.InkWave, wave.Look, "Spirit Wave flies as an ink wave");
		Assert.Equal(VfxLook.FoxFlame, fox.Look, "Foxfire flies as a fox flame");
		Assert.Equal(VfxLook.InkWave, great.Look, "Great Wave is a wave too");
		Assert.True(great.ScalePct > wave.ScalePct, "Great Wave is larger");
		Assert.True(great.Colour != wave.Colour, "Great Wave changes colour (V8)");
		Assert.True(new[] { wave, fox, great }.Distinct().Count() == 3, "three distinct looks");
	}

	[Test]
	public static void NoPreset_DefaultsToAnOrbInTheSourceColour()
	{
		var none = SpecialVfxStyle.ProjectileStyle(null, "kitsune");
		Assert.Equal(new VfxStyle(VfxLook.Orb, HitSparkStyle.Foxfire, SpecialVfxStyle.DefaultCore), none, "no preset: orb, V5 source colour, rice-paper core");
		var colourOnly = LevelPresetLibrary.Parse("""{ "kind": "level_preset", "id": "c", "special": "s", "level": 1, "colour": "#D8632C" }""");
		Assert.Equal(new VfxStyle(VfxLook.Orb, 0xD8632C, SpecialVfxStyle.DefaultCore), SpecialVfxStyle.ProjectileStyle(colourOnly, "kitsune"), "colour-only preset: orb in its colour");
		Assert.Equal("procedural:orb", VfxArt.Pick(none, _ => false), "no image anywhere: the procedural shader");
	}

	[Test]
	public static void ArtSeam_PresetImageThenDefaultFileThenProcedural()
	{
		var style = SpecialVfxStyle.ProjectileStyle(LevelPresetLibrary.Parse(WavePreset), "ryo");
		Assert.Equal("res://vfx/projectiles/w.png", VfxArt.Pick(style, _ => true), "the preset's image first");
		Assert.Equal("res://assets/generated/vfx/projectiles/proj-ink-wave.png", VfxArt.Pick(style, p => p.EndsWith("proj-ink-wave.png")), "else the YOK-243 slot for the look");
		Assert.Equal("procedural:ink_wave", VfxArt.Pick(style, _ => false), "else procedural");
	}

	[Test]
	public static void ArtSlots_AreTheYok243FileNames()
	{
		// assets/specs/vfx/finish-vfx-textures.md: the eight files the asset-smith delivers, under game/assets/generated/vfx/.
		var want = new Dictionary<VfxLook, string>
		{
			[VfxLook.InkWave] = "res://assets/generated/vfx/projectiles/proj-ink-wave.png",
			[VfxLook.FoxFlame] = "res://assets/generated/vfx/projectiles/proj-fox-flame.png",
			[VfxLook.Seal] = "res://assets/generated/vfx/effects/fx-seal-burst.png",
			[VfxLook.Afterimage] = "res://assets/generated/vfx/effects/fx-afterimage.png",
			[VfxLook.Trail] = "res://assets/generated/vfx/effects/fx-speed-trail.png",
			[VfxLook.Dust] = "res://assets/generated/vfx/effects/fx-ground-wave.png",
			[VfxLook.Grab] = "res://assets/generated/vfx/effects/fx-water-swirl.png",
			[VfxLook.Ring] = "res://assets/generated/vfx/effects/fx-spin-ring.png",
		};
		foreach (var (look, path) in want) Assert.Equal(path, VfxArt.DefaultPath(look), $"{look} slot");
		foreach (var look in new[] { VfxLook.Orb, VfxLook.InkSplash, VfxLook.FlameBurst })
			Assert.True(VfxArt.DefaultPath(look) is null, $"{look} is procedural only");
		Assert.True(VfxLayer.Contain(100f, 100f, 2f) == (100f, 50f), "a 2:1 image fits a square box by height");
		Assert.True(VfxLayer.Contain(100f, 100f, 0.5f) == (50f, 100f), "a 1:2 image fits by width");
		Assert.True(Mathf.Abs(VfxLayer.FrameAspect(1024, 256, 1) - 4f) < 1e-6f && Mathf.Abs(VfxLayer.FrameAspect(1024, 256, 4) - 1f) < 1e-6f, "a flipbook frame's aspect");
	}

	// --- every special in data/moves (YOK-226) ------------------------------------------------------------------------

	static SpecialData[] DataSpecials() => SpecialLoader.LoadDirectory(ContentPaths.Data("moves"));

	/// <summary>The eight specials this build ships (YOK-226): each, and its evolution, must be dressed by data.</summary>
	static readonly string[] Shipped = { "spirit-wave", "rising-talisman", "foxfire", "fox-mirage", "iron-club-charge", "oni-quake", "river-grab", "shell-spin" };

	/// <summary>The YOK-21 placeholder box's albedo, (0.6, 0.9, 1).</summary>
	const uint Yok21Cyan = 0x99E6FF;

	[Test]
	public static void EverySpecial_AtEveryLevel_ResolvesAPresetDrivenVfx()
	{
		var lib = LevelPresetLibrary.LoadDirectory(ContentPaths.Data("presets"));
		var specials = DataSpecials();
		foreach (string id in Shipped) Assert.True(specials.Any(s => s.Id == id), $"{id} is in data/moves");
		foreach (var data in specials)
		{
			Assert.True(data.MaxLevel == 3, $"{data.Id} has Lv 2 and its evolution");
			VfxStyle[]? lv1 = null;
			for (int level = 1; level <= data.MaxLevel; level++)
			{
				var preset = lib.Resolve(data.Id, level);
				Assert.True(preset != null, $"{data.Id} Lv {level}: a level preset dresses it (V8)");
				var move = new EquippedSpecial(data, level).Move;
				var looks = new[]
				{
					SpecialVfxStyle.EffectStyle(VfxMoment.Cast, move, preset, data.Source),
					SpecialVfxStyle.EffectStyle(VfxMoment.Release, move, preset, data.Source),
					SpecialVfxStyle.EffectStyle(VfxMoment.Impact, move, preset, data.Source),
					SpecialVfxStyle.ProjectileStyle(preset, data.Source),
				};
				Assert.True(looks.Take(3).Any(l => l.Look != VfxLook.None), $"{data.Id} Lv {level}: at least one effect shows");
				if (move.Projectile != null)
					Assert.True(looks[3].Look != VfxLook.Orb, $"{data.Id} Lv {level}: its projectile has a preset look, not the bare orb");
				foreach (var l in looks)
				{
					Assert.True(l.Colour != Yok21Cyan && l.Core != Yok21Cyan, $"{data.Id} Lv {level}: never the YOK-21 cyan");
					string src = VfxArt.Pick(l, _ => false);
					Assert.Equal("procedural:" + VfxLooks.Name(l.Look), src, $"{data.Id} Lv {level}: with no texture yet it draws the tinted shape");
				}
				if (level == 1) lv1 = looks;
				else if (level == 3)
					Assert.True(!looks.SequenceEqual(lv1!), $"{data.Id} evolution: its look differs from Lv 1 (V8, A5)");
			}
		}
	}

	[Test]
	public static void NoCyanPlaceholderPath_RemainsInTheFightCode()
	{
		var dir = ProjectSettings.GlobalizePath("res://scripts");
		foreach (var f in System.IO.Directory.GetFiles(dir, "*.cs", System.IO.SearchOption.AllDirectories))
		{
			string text = System.IO.File.ReadAllText(f);
			Assert.True(!text.Contains("new Color(0.6f, 0.9f, 1f)") && !text.Contains("new Color(0.4f, 0.8f, 1f)") && !text.Contains("$\"Projectile{"),
				$"{System.IO.Path.GetFileName(f)}: no YOK-21 cyan projectile box");
		}
		Assert.True(typeof(FightScene).GetField("_projectiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) is null, "FightScene keeps no box pool");
	}

	/// <summary>P1 holds one data special at a level in slot A; P2 stands <paramref name="gap"/> units away.</summary>
	static Match WithSpecial(SpecialData data, int level, int gap = 300)
	{
		var n = Normals();
		var m = new Match(null, n, n);
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Starters());
		m.P1.Equip(SpecialSlot.A, data, level);
		m.P1.X = -gap / 2 * S;
		m.P2.X = gap / 2 * S;
		return m;
	}

	[Test]
	public static void Aftershock_ShowsASecondGroundWave()
	{
		var lib = LevelPresetLibrary.LoadDirectory(ContentPaths.Data("presets"));
		var quake = DataSpecials().Single(s => s.Id == "oni-quake");
		var m = WithSpecial(quake, 3, 1000);
		Assert.True(m.P1.Specials[(int)SpecialSlot.A]!.Move.AftershockDelay > 0, "Lv 3 Aftershock has a second quake");
		var fx = new SpecialFx(lib);
		fx.Attach(m);
		var dustTicks = new List<int>();
		for (int t = 1; t <= 120; t++)
		{
			m.Step(t == 1 ? FighterInput.Special(SpecialSlot.A) : Idle, Idle);
			fx.Observe(m);
			foreach (var f in fx.Visible(m)) if (f.Age == 0 && f.Effect.Style.Look == VfxLook.Dust) dustTicks.Add(m.Tick);
		}
		fx.Detach();
		Assert.True(dustTicks.Distinct().Count() >= 2, $"the slam and the aftershock each raise a ground wave (ticks {string.Join(",", dustTicks)})");
	}

	[Test]
	public static void MirageFeint_DecoyIsDrawnWhileTheSimKeepsIt()
	{
		var lib = LevelPresetLibrary.LoadDirectory(ContentPaths.Data("presets"));
		var mirage = DataSpecials().Single(s => s.Id == "fox-mirage");
		var m = WithSpecial(mirage, 3);
		var fx = new SpecialFx(lib);
		fx.Attach(m);
		bool sawDecoy = false;
		for (int t = 1; t <= 90; t++)
		{
			m.Step(t == 1 ? FighterInput.Special(SpecialSlot.A) : Idle, Idle);
			fx.Observe(m);
			var on = fx.Visible(m).Where(f => f.Effect.Anchor == VfxAnchor.Decoy).ToList();
			if (m.DecoyOf(0) is { } d)
			{
				Assert.True(on.Count == 1, $"t{t}: one afterimage on the decoy");
				Assert.Equal(d.X, on[0].CentreX, $"t{t}: standing on the decoy");
				Assert.Equal(VfxLook.Afterimage, on[0].Effect.Style.Look, "the decoy is an afterimage");
				sawDecoy = true;
			}
			else Assert.True(on.Count == 0, $"t{t}: no decoy, no decoy afterimage");
		}
		fx.Detach();
		Assert.True(sawDecoy, "Mirage Feint left a decoy");
	}

	[Test]
	public static void EverySpecial_EffectsWatching_KeepTheStateHash()
	{
		// F3: VFX is presentation only. Each data special at Lv 1 and Lv 3, fired on a schedule: identical hashes with
		// and without the effects listening, and a second watched run draws the same frames.
		var lib = LevelPresetLibrary.LoadDirectory(ContentPaths.Data("presets"));
		foreach (var data in DataSpecials())
			foreach (int level in new[] { 1, 3 })
			{
				static FighterInput P1(int t) => t % 61 == 1 ? FighterInput.Special(SpecialSlot.A) : Idle;
				static FighterInput P2(int t) => t % 47 == 9 ? FighterInput.Special(SpecialSlot.A) : Idle;
				var bare = WithSpecial(data, level, 260);
				var hashes = new List<ulong>();
				for (int t = 1; t <= 200; t++) { bare.Step(P1(t), P2(t)); hashes.Add(bare.StateHash()); }
				var drawn = new List<string>[2];
				int shown = 0;
				for (int run = 0; run < 2; run++)
				{
					var m = WithSpecial(data, level, 260);
					var fx = new SpecialFx(lib);
					fx.Attach(m);
					drawn[run] = new List<string>();
					for (int t = 1; t <= 200; t++)
					{
						m.Step(P1(t), P2(t));
						fx.Observe(m);
						var vis = fx.Visible(m);
						shown += vis.Count(f => f.Effect.Special == data.Id);
						Assert.Equal(hashes[t - 1], m.StateHash(), $"{data.Id} Lv {level} t{t}: state hash unchanged");
						drawn[run].Add(string.Join(";", vis.Select(f => $"{f.Effect.Style.Look}@{f.CentreX},{f.CentreY} a{f.Age}")));
					}
					fx.Detach();
				}
				Assert.True(shown > 0, $"{data.Id} Lv {level}: its effects showed");
				Assert.True(drawn[0].SequenceEqual(drawn[1]), $"{data.Id} Lv {level}: a replay draws the same effects");
			}
	}

	// --- the hook for every special --------------------------------------------------------------------------------

	static MoveData Move(string id) => new() { Id = id, Startup = 10, Active = 3, Recovery = 20, Damage = 50 };

	[Test]
	public static void DefaultLooks_ComeFromGenericMoveProperties()
	{
		var plain = Move("plain");
		var mirage = Move("m") with { Teleport = new Teleport(8) };
		var charge = Move("c") with { Charge = new ChargeSpec(5, 30, 10, new[] { 4, 6 }, 600, "hold") };
		var travel = Move("t") with { ForwardTravel = 120 };
		var spin = Move("s") with { Invuln = new[] { new InvulnWindow(1, 20, InvulnAgainst.Projectile) } };
		var quake = Move("q") with { Low = true };
		var grab = Move("g") with { IsCommandGrab = true };
		var anti = Move("a") with { Invuln = new[] { new InvulnWindow(1, 5, InvulnAgainst.Air) } };

		Assert.Equal(VfxLook.Seal, SpecialVfxStyle.DefaultLook(VfxMoment.Cast, plain), "plain cast: talisman seal");
		Assert.Equal(VfxLook.None, SpecialVfxStyle.DefaultLook(VfxMoment.Release, plain), "plain release: nothing");
		Assert.Equal(VfxLook.InkSplash, SpecialVfxStyle.DefaultLook(VfxMoment.Impact, plain), "plain impact: ink splash");
		Assert.Equal(VfxLook.Afterimage, SpecialVfxStyle.DefaultLook(VfxMoment.Cast, mirage), "teleport (Fox Mirage): afterimage");
		Assert.Equal(VfxLook.Trail, SpecialVfxStyle.DefaultLook(VfxMoment.Cast, charge), "charge (Iron-Club Charge): trail");
		Assert.Equal(VfxLook.Trail, SpecialVfxStyle.DefaultLook(VfxMoment.Cast, travel), "forward travel: trail");
		Assert.Equal(VfxLook.Ring, SpecialVfxStyle.DefaultLook(VfxMoment.Cast, spin), "projectile invulnerability (Shell Spin): ring");
		Assert.Equal(VfxLook.Seal, SpecialVfxStyle.DefaultLook(VfxMoment.Cast, anti), "anti-air invulnerability is not a spin");
		Assert.Equal(VfxLook.Dust, SpecialVfxStyle.DefaultLook(VfxMoment.Release, quake), "hits low (Oni Quake): dust on the pound");
		Assert.Equal(VfxLook.Dust, SpecialVfxStyle.DefaultLook(VfxMoment.Impact, quake), "and on the hit");
		Assert.Equal(VfxLook.Grab, SpecialVfxStyle.DefaultLook(VfxMoment.Impact, grab), "command grab (River Grab): grab whirl");
		Assert.Equal(VfxLook.None, SpecialVfxStyle.DefaultLook(VfxMoment.Release, grab), "a grab does not release");

		var preset = LevelPresetLibrary.Parse(WavePreset);
		Assert.Equal(VfxLook.Seal, SpecialVfxStyle.EffectStyle(VfxMoment.Cast, mirage, preset, "kitsune").Look, "the preset's look wins");
		Assert.Equal(0xB5332Bu, SpecialVfxStyle.EffectStyle(VfxMoment.Cast, mirage, preset, "kitsune").Colour, "and its colour");
		Assert.Equal(30, SpecialVfxStyle.FramesOf(VfxLook.Seal, mirage, preset.Cast), "and its frames");
		Assert.Equal(VfxLook.None, SpecialVfxStyle.EffectStyle(VfxMoment.Release, quake, preset, "oni").Look, "look none turns the default off");
		Assert.Equal(mirage.Teleport!.Frame + 2 * SpecialVfxStyle.FadeFrames, SpecialVfxStyle.FramesOf(VfxLook.Afterimage, mirage, null), "the afterimage holds until the teleport");
		Assert.Equal(spin.TotalFrames, SpecialVfxStyle.FramesOf(VfxLook.Ring, spin, null), "a ring lasts the move");
	}

	// --- following the sim -------------------------------------------------------------------------------------------

	[Test]
	public static void ProjectileVisual_SitsOnTheDataHitboxEveryTick()
	{
		var lib = new LevelPresetLibrary(new[] { LevelPresetLibrary.Parse(WavePreset) });
		foreach (int shooter in new[] { 0, 1 })
		{
			var m = Apart();
			int seen = 0, lastX = int.MinValue;
			for (int t = 1; t <= 120; t++)
			{
				var go = t == 1 ? FighterInput.Special(SpecialSlot.A) : Idle;
				m.Step(shooter == 0 ? go : Idle, shooter == 0 ? Idle : go);
				var vis = SpecialFx.Projectiles(m, lib);
				Assert.Equal(m.Projectiles.Count, vis.Count, $"t{t}: one visual per projectile");
				for (int k = 0; k < vis.Count; k++)
				{
					var p = m.Projectiles[k];
					var v = vis[k];
					var box = p.WorldBox();
					Assert.Equal(box, v.Box, $"t{t}: the data rectangle the sim hits with");
					Assert.Equal(p.Serial, v.Serial, "same projectile");
					Assert.Equal(p.Age, v.Age, "animated from the sim age");
					Assert.Equal(p.Dir > 0 ? box.x1 : box.x0, p.Dir > 0 ? v.X1 : v.X0, $"t{t}: leading edge on the box's");
					Assert.Equal((box.y0 + box.y1) / 2, (v.Y0 + v.Y1) / 2, $"t{t}: vertical centre on the box's");
					Assert.True(v.X0 <= box.x0 && v.X1 >= box.x1 && v.Y0 <= box.y0 && v.Y1 >= box.y1, $"t{t}: the drawn quad covers the box");
					Assert.Equal(shooter == 0 ? 1 : -1, v.Dir, "flies away from the shooter");
					Assert.Equal(VfxLook.InkWave, v.Style.Look, "the preset's look");
					Assert.True(lastX == int.MinValue || (p.Dir > 0 ? p.X > lastX : p.X < lastX), $"t{t}: moved with the sim");
					lastX = p.X;
					seen++;
				}
			}
			Assert.True(seen > 5, $"P{shooter + 1}'s wave was followed over {seen} ticks");
		}
	}

	[Test]
	public static void Effects_CastReleaseAndImpact_OnTheirSimTicks()
	{
		var m = Apart();
		var fx = new SpecialFx();
		fx.Attach(m);
		var impacts = new List<(int Tick, ImpactEvent E)>();
		m.Impact += (mm, e) => impacts.Add((mm.Tick, e));
		int spawnTick = -1;
		for (int t = 1; t <= 120; t++)
		{
			m.Step(P1At(t, 1), Idle);
			if (spawnTick < 0 && m.Projectiles.Count > 0) spawnTick = m.Tick;
			fx.Observe(m);
		}
		var cast = fx.All.Where(e => e.Moment == VfxMoment.Cast).ToList();
		Assert.Equal(1, cast.Count, "one cast");
		Assert.Equal(1, cast[0].SpawnTick, "on the tick the special started");
		Assert.Equal(VfxLook.Seal, cast[0].Style.Look, "default seal (no preset)");
		Assert.Equal(HitSparkStyle.Ink, cast[0].Style.Colour, "Ryo's ink (V5)");
		var rel = fx.All.Where(e => e.Moment == VfxMoment.Release).ToList();
		Assert.Equal(1, rel.Count, "one release");
		Assert.Equal(spawnTick, rel[0].SpawnTick, "on the projectile's spawn tick");
		Assert.Equal(1, impacts.Count, "the wave hit once");
		var hit = fx.All.Where(e => e.Moment == VfxMoment.Impact).ToList();
		Assert.Equal(1, hit.Count, "one impact effect");
		Assert.Equal(impacts[0].Tick, hit[0].SpawnTick, "on the impact tick");
		Assert.Equal((impacts[0].E.At.X, impacts[0].E.At.Y), (hit[0].X, hit[0].Y), "at the contact point");
		fx.Detach();
	}

	[Test]
	public static void Effects_AreAFunctionOfTheTick_NotOfRenderCalls()
	{
		var m = Apart();
		var fx = new SpecialFx();
		fx.Attach(m);
		for (int t = 1; t <= 3; t++) { m.Step(P1At(t, 1), Idle); fx.Observe(m); }
		var a = fx.Visible(m);
		var b = fx.Visible(m);
		Assert.True(a.Count > 0 && a.SequenceEqual(b), "drawing twice on one tick shows the same frames");
		var first = a[0];
		m.Step(Idle, Idle); fx.Observe(m);
		var c = fx.Visible(m);
		Assert.Equal(first.Age + 1, c.First(f => f.Effect == first.Effect).Age, "one tick later = one tick older");
	}

	[Test]
	public static void Replay_DrawsTheSameFramesAndKeepsTheStateHash()
	{
		// The same inputs, once bare and twice with the effects watching: identical hashes every tick (the sim never
		// reads the effects), identical effect frames and projectile visuals in both watched runs (a replay looks the same).
		var lib = LevelPresetLibrary.LoadDirectory(ContentPaths.Data("presets"));
		FighterInput P2At(int t) => t == 20 ? FighterInput.Special(SpecialSlot.B) : t % 37 == 5 ? FighterInput.Special(SpecialSlot.A) : Idle;
		FighterInput P1(int t) => t % 29 == 1 ? FighterInput.Special(SpecialSlot.A) : t % 53 == 7 ? FighterInput.Special(SpecialSlot.B) : Idle;

		var bare = Apart();
		var hashes = new List<ulong>();
		for (int t = 1; t <= 240; t++) { bare.Step(P1(t), P2At(t)); hashes.Add(bare.StateHash()); }

		var drawn = new List<string>[2];
		for (int run = 0; run < 2; run++)
		{
			var m = Apart();
			var fx = new SpecialFx(lib);
			fx.Attach(m);
			drawn[run] = new List<string>();
			for (int t = 1; t <= 240; t++)
			{
				m.Step(P1(t), P2At(t));
				fx.Observe(m);
				var vis = fx.Visible(m);
				var proj = SpecialFx.Projectiles(m, lib);
				Assert.Equal(hashes[t - 1], m.StateHash(), $"run {run} t{t}: state hash unchanged with effects watching");
				drawn[run].Add(string.Join(";", vis.Select(f => $"{f.Effect.Moment}:{f.Effect.Style.Look}@{f.CentreX},{f.CentreY} a{f.Age} o{f.AlphaPct}"))
					+ "|" + string.Join(";", proj.Select(p => $"{p.Serial}:{p.X0},{p.Y0},{p.X1},{p.Y1} a{p.Age}")));
			}
			fx.Detach();
		}
		Assert.True(drawn[0].Any(s => s.Contains("Cast")) && drawn[0].Any(s => s.Contains('|') && s.Split('|')[1].Length > 0), "the run drew casts and projectiles");
		for (int t = 0; t < 240; t++) Assert.Equal(drawn[0][t], drawn[1][t], $"t{t + 1}: the replay draws the same frame");
	}

	[Test]
	public static void OwnerEffects_RideTheOwner_AndEndWithTheMove()
	{
		var m = Apart();
		var fx = new SpecialFx();
		fx.Attach(m);
		m.Step(FighterInput.Special(SpecialSlot.A), Idle);
		var ring = new VfxStyle(VfxLook.Ring, 0x3E7F86, SpecialVfxStyle.DefaultCore);
		fx.Spawn(new VfxEffect(m.Tick, VfxMoment.Cast, 0, m.P1.MoveSerial, VfxAnchor.Owner, 0, 160 * S, 330 * S, 230 * S, 1, 999, ring));
		var on = fx.Visible(m).Single(f => f.Effect.Style.Look == VfxLook.Ring);
		Assert.Equal((m.P1.X, m.P1.Y + 160 * S), (on.CentreX, on.CentreY), "centred on its owner");
		for (int t = 0; t < 120 && m.P1.State == FighterState.Attack; t++) m.Step(Idle, Idle);
		Assert.True(fx.Visible(m).All(f => f.Effect.Style.Look != VfxLook.Ring), "gone once the owner's move ends");
		fx.Spawn(new VfxEffect(m.Tick, VfxMoment.Cast, 0, 0, VfxAnchor.Point, 0, 0, 10, 10, 1, 5, ring with { Look = VfxLook.FoxFlame }));
		Assert.Equal(0, fx.Visible(m).Count(f => f.Effect.Style.Look == VfxLook.FoxFlame), "a projectile look is not an effect");
		fx.Detach();
	}

	[Test]
	public static void Scene_DrawsTheWaveOverItsHitbox_NoPlaceholderBox(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		try
		{
			Assert.True(scene.FxLayer != null && scene.FxLayer.GetParent() == scene, "the layer is in the fight scene");
			if (scene.P2Ai != null) scene.ToggleP2Ai();
			var m = scene.Match;
			int slot = Array.FindIndex(m.P1.Specials, s => s?.Move.Projectile != null);
			Assert.True(slot > 0, "Ryo has a projectile special");
			scene.Step(FighterInput.Special((SpecialSlot)slot), Idle);
			for (int t = 0; t < 40 && m.Projectiles.Count == 0; t++) scene.Step(Idle, Idle);
			scene.Step(Idle, Idle);
			Assert.True(m.Projectiles.Count == 1, "the projectile is in flight");
			Assert.True(scene.GetNodeOrNull("Projectile0") is null, "the YOK-21 cyan box is gone");
			var quad = scene.FxLayer!.Quads[0];
			Assert.True(quad.Visible, "the projectile is drawn first");
			var v = SpecialFx.Projectiles(m, scene.Fx.Presets)[0];
			Assert.True(Mathf.Abs(quad.Position.X - FightScene.ToMeters(v.X0 + v.X1) / 2f) < 1e-4f, "at the visual's centre");
			var mat = (ShaderMaterial)quad.MaterialOverride;
			Assert.Equal((float)m.Projectiles[0].Age, (float)mat.GetShaderParameter("age"), "the shader is driven by the sim age");
			Assert.True(scene.FxLayer.Shown >= 2, "the projectile plus its cast/release effects");
		}
		finally { scene.QueueFree(); }
	}
}
