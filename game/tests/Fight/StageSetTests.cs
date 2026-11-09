using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-150/151/152: the shrine gate, the riverbank and the tea house keep the grove's rules (StageTests): every prop behind
/// the gameplay plane and the clear band, lanterns deep, a handful of shadowless lights, the art covering every camera
/// framing; scenery moves only with the sim tick; the tea house's seasons follow the round; missing assets fall back;
/// each yokai's fights are wired to its stage.
/// </summary>
public static class StageSetTests
{
	/// <summary>Each new stage: its scene, kind, catalog entry and the ground planes a camera ray may land on.</summary>
	static readonly (string Scene, string Kind, StageAssets Assets, string[] Grounds)[] Stages =
	{
		(StageCatalog.ShrineGateScene, "shrine-gate", StageCatalog.ShrineGate, new[] { "Floor" }),
		(StageCatalog.RiverbankScene, "riverbank", StageCatalog.Riverbank, new[] { "Floor", "Water" }),
		(StageCatalog.TeaHouseScene, TeaHouseStage.Kind, StageCatalog.TeaHouse, new[] { "Floor" }),
	};

	const string LanternGroup = "stage_lantern";

	/// <summary>The fight scene showing <paramref name="stage"/> (forced, as <c>--stage=</c> does).</summary>
	static FightScene Load(Node runner, string stage)
	{
		string? was = FightScene.ForcedStage;
		FightScene.ForcedStage = stage;
		try
		{
			var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			scene.ExternalDrive = true;
			runner.AddChild(scene);
			return scene;
		}
		finally { FightScene.ForcedStage = was; }
	}

	static void ForEachStage(Node runner, Action<FightScene, (string Scene, string Kind, StageAssets Assets, string[] Grounds)> check)
	{
		foreach (var st in Stages)
		{
			var scene = Load(runner, st.Scene);
			try { check(scene, st); }
			finally { scene.QueueFree(); }
		}
	}

	static IEnumerable<MeshInstance3D> Meshes(Node n) => n.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>();

	/// <summary>Every placed prop under Props with its transform in Props space; a plain grouping node (no mesh of its own, e.g. Props/Floats) counts its children one by one.</summary>
	static IEnumerable<(Node3D Node, Transform3D Xf)> PropItems(Node props)
	{
		foreach (var n in props.GetChildren().Cast<Node3D>())
		{
			bool group = n.GetChildCount() > 0 && n.GetChildren().All(c => c is Node3D && c.Name.ToString() != "Pivot") && !n.IsInGroup(LanternGroup);
			if (group)
				foreach (var c in n.GetChildren().Cast<Node3D>()) yield return (c, n.Transform * c.Transform);
			else yield return (n, n.Transform);
		}
	}

	[Test]
	public static void NewStages_LoadWithTheirLayers_ToonAndOutlineOnEveryProp(Node runner) => ForEachStage(runner, (scene, st) =>
	{
		Assert.Equal(st.Kind, scene.StageKind, $"{st.Scene}: the fight builds it");
		Assert.Equal(st.Scene, scene.StageRoot.SceneFilePath, $"{st.Kind}: the scene itself, no fallback");
		var stage = scene.StageRoot;
		foreach (string part in new[] { "Environment", "Sun", "Fill", "Backdrop", "Floor", "Props", "PropToon" })
			Assert.True(stage.HasNode(part), $"{st.Kind}: has {part}");
		foreach (string g in st.Grounds) Assert.True(stage.GetNode(g) is MeshInstance3D { Mesh: PlaneMesh }, $"{st.Kind}: {g} is a plane");
		Assert.True(PropItems(stage.GetNode("Props")).Count() >= 3, $"{st.Kind}: midground and edge props");
		foreach (var mi in Meshes(stage.GetNode("Props")))
			Assert.True(mi.GetActiveMaterial(0) is BaseMaterial3D { DiffuseMode: BaseMaterial3D.DiffuseModeEnum.Toon, NextPass: not null }, $"{st.Kind}/{mi.Name}: toon + outline");
		Assert.True(!stage.GetNode<WorldEnvironment>("Environment").Environment.GlowEnabled, $"{st.Kind}: no bloom");
		scene.Step(FighterInput.None, FighterInput.None);
	});

	[Test]
	public static void NewStages_PropsStayBehindTheGameplayPlane(Node runner) => ForEachStage(runner, (scene, st) =>
	{
		float water = scene.StageRoot.GetNodeOrNull<Node3D>("Water")?.Position.Y ?? 0f;
		foreach (var (n, _) in PropItems(scene.StageRoot.GetNode("Props")))
			foreach (var mi in Meshes(n))
			{
				Aabb b = mi.GlobalTransform * mi.Mesh.GetAabb();
				Assert.True(b.End.Z <= -1f, $"{st.Kind}/{n.Name} reaches z {b.End.Z} (fighters live at Z = 0)");
				float bob = n is TickMotion t ? Mathf.Abs(t.BobMetres) : 0f; // the bob is in the parent's space, unscaled
				if (n is TickMotion { SwayDegrees: > 0f })
					Assert.True(b.Position.Y > 2.5f, $"{st.Kind}/{n.Name}: a hanging lantern hangs above the fighters' heads, bottom y {b.Position.Y}");
				else if (n is TickMotion { BobMetres: > 0f })
					Assert.True(b.Position.Y >= water - bob - 0.15f && b.Position.Y <= water + 0.05f, $"{st.Kind}/{n.Name}: floats at the waterline {water}, got y {b.Position.Y}");
				else
					Assert.True(b.Position.Y >= -0.05f, $"{st.Kind}/{n.Name}: base on the floor, got y {b.Position.Y}");
			}
	});

	[Test]
	public static void NewStages_ClearBandBehindTheFight_LanternsDeep_InFrontOfTheBackdrop(Node runner) => ForEachStage(runner, (scene, st) =>
	{
		// The grove's rule (StageRules): each prop's front = centre z + footprint radius (largest horizontal extent / 2, any yaw).
		var backdrop = scene.StageRoot.GetNode<MeshInstance3D>("Backdrop");
		int lanterns = 0, items = 0;
		foreach (var (n, xf) in PropItems(scene.StageRoot.GetNode("Props")))
		{
			items++;
			var p = xf.Origin;
			bool lantern = n.IsInGroup(LanternGroup);
			Aabb own = n.GetChildren().Select(StageRules.LocalBounds).Aggregate((a, b) => a.Merge(b));
			float radius = Mathf.Max(own.Size.X, own.Size.Z) * 0.5f * xf.Basis.Scale.Y;
			float front = p.Z + radius, limit = StageRules.FrontLimit(p.X, radius, lantern);
			Assert.True(p.Z - radius > backdrop.Position.Z, $"{st.Kind}/{n.Name} at z {p.Z} (radius {radius}) pokes through the backdrop at z {backdrop.Position.Z}");
			if (lantern)
			{
				lanterns++;
				Assert.True(p.Z <= StageRules.LanternMaxZ, $"{st.Kind}/{n.Name} at z {p.Z}: lanterns stay deep");
			}
			if (StageRules.InClearBand(p.X, radius)) // the footprint's x extent, not only its centre
				Assert.True(front <= StageRules.ClearBandBackZ + 1e-3f, $"{st.Kind}/{n.Name} at ({p.X}, {p.Z}), footprint x {p.X - radius:0.00}..{p.X + radius:0.00}, reaches z {front}, inside the clear band");
			Assert.True(front <= limit + 1e-3f, $"{st.Kind}/{n.Name}: front z {front} past its limit {limit}");
		}
		Assert.True(lanterns >= 2, $"{st.Kind}: its lanterns were checked ({lanterns})");
		if (st.Kind == "riverbank") Assert.True(lanterns >= 12 && lanterns <= 15, $"riverbank: 12-15 floating lanterns (decision log 2026-11-09, riverbank lanterns second pass), got {lanterns}");
	});

	[Test]
	public static void NewStages_AHandfulOfShadowlessLanternLights(Node runner) => ForEachStage(runner, (scene, st) =>
	{
		var lights = scene.StageRoot.FindChildren("*", "OmniLight3D", true, false).Cast<OmniLight3D>().ToList();
		Assert.True(lights.Count >= 2 && lights.Count <= StageRules.MaxLanternLights, $"{st.Kind}: a handful of lantern lights, got {lights.Count}");
		foreach (var l in lights)
		{
			Assert.True(!l.ShadowEnabled && l.OmniRange <= 4f && l.LightSpecular == 0f, $"{st.Kind}/{l.GetPath()}: shadowless, local, no highlight");
			Assert.True(l.LightEnergy > 0f && l.IsVisibleInTree(), $"{st.Kind}/{l.GetPath()}: on");
		}
	});

	[Test]
	public static void NewStages_CoverTheCameraRangeAtBothCorners(Node runner) => ForEachStage(runner, (scene, st) =>
		StageTests.AssertCoversCameraRange(scene, st.Grounds));

	[Test]
	public static void NewStages_LayoutIsTheSameEveryBuild(Node runner)
	{
		foreach (var st in Stages)
		{
			string Layout()
			{
				var scene = Load(runner, st.Scene);
				string s = string.Join(";", PropItems(scene.StageRoot.GetNode("Props")).Select(i => $"{i.Node.Name}:{i.Xf}"));
				scene.QueueFree();
				return s;
			}
			Assert.Equal(Layout(), Layout(), $"{st.Kind}: same stage every build");
		}
	}

	// ---- tick-driven scenery ----

	[Test]
	public static void TickMotion_IsAPureFunctionOfTheTick()
	{
		Assert.Equal(0f, TickMotion.Wave(0, 200, 0), "tick 0, no phase: rest");
		Assert.True(Mathf.IsEqualApprox(1f, TickMotion.Wave(50, 200, 0)), "a quarter period: the crest");
		Assert.True(Mathf.IsEqualApprox(TickMotion.Wave(7, 200, 30), TickMotion.Wave(207, 200, 30)), "periodic");
		Assert.True(Mathf.IsEqualApprox(TickMotion.Wave(1_000_007, 200, 30), TickMotion.Wave(7, 200, 30)), "no float drift over a long fight");
		Assert.True(float.IsFinite(TickMotion.Wave(5, 0, -9)) && float.IsFinite(TickMotion.Wave(int.MaxValue, 1, int.MaxValue)), "a bad period or phase stays finite");
		var bob = new TickMotion { BobMetres = 0.05f, SwayDegrees = 3f, PeriodTicks = 120, PhaseTicks = 17, Position = new Vector3(1, 2, -12) };
		bob.Present(new StageClock(1, 40));
		Transform3D at40 = bob.Transform;
		bob.Present(new StageClock(1, 3));
		bob.Present(new StageClock(3, 40));
		Assert.True(bob.Transform.IsEqualApprox(at40), "the same tick gives the same pose, whatever came before");
		float expect = 0.05f * Mathf.Sin(Mathf.Tau * (40 + 17) / 120f);
		Assert.True(Mathf.Abs(bob.Position.Y - (2f + expect)) < 1e-5f, $"offset = A sin(tick w + phase): {bob.Position.Y} vs {2f + expect}");
		Assert.True(Mathf.IsEqualApprox(bob.Position.X, 1f) && Mathf.IsEqualApprox(bob.Position.Z, -12f), "bobs only up and down");
		bob.Free();
	}

	[Test]
	public static void Riverbank_LanternsBobWithTheSimTick_NeitherWallClockNorFrames(Node runner)
	{
		var scene = Load(runner, StageCatalog.RiverbankScene);
		try
		{
			var floats = scene.StageRoot.GetNode("Props/Floats").GetChildren().Cast<TickMotion>().ToList();
			Assert.True(floats.All(f => f.BobMetres > 0f && f.BobMetres <= 0.1f), "every floating lantern bobs, gently");
			Assert.True(floats.Select(f => f.PhaseTicks).Distinct().Count() > floats.Count / 2, "out of step with each other");
			string Pose() => string.Join(";", floats.Select(f => f.Position.Y.ToString("0.00000")));
			for (int i = 0; i < 37; i++) scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal(37, ((FightStage)scene.StageRoot).Clock.Tick, "the stage got the sim tick");
			string at37 = Pose();
			var rests = floats.Select(f => f.Rest.Origin.Y).ToList();
			for (int i = 0; i < floats.Count; i++)
				Assert.True(Mathf.Abs(floats[i].Position.Y - (rests[i] + floats[i].BobMetres * TickMotion.Wave(37, floats[i].PeriodTicks, floats[i].PhaseTicks))) < 1e-4f, $"{floats[i].Name}: offset from the tick");
			scene.ResetFight(); // a new round: tick 0 again
			for (int i = 0; i < 37; i++) scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal(at37, Pose(), "tick 37 of the next round: the same pose");
		}
		finally { scene.QueueFree(); }
	}

	// ---- tea house seasons (designer 2026-11-09: on the Tanuki's health beats) ----

	[Test]
	public static void TeaHouse_SeasonForHealth_BelowEachBeat()
	{
		Assert.Equal("75,50,25", string.Join(",", TeaHouseStage.SeasonBelowPct), "the beats: below 75%, 50%, 25%");
		var cases = new (int Health, int Season)[] { (1000, 0), (751, 0), (750, 0), (749, 1), (500, 1), (499, 2), (250, 2), (249, 3), (1, 3), (0, 3), (-20, 3), (1200, 0) };
		foreach (var (h, season) in cases)
			Assert.Equal(season, TeaHouseStage.SeasonForHealth(h, 1000), $"health {h}/1000: {TeaHouseStage.Seasons[season]}");
		Assert.Equal(1, TeaHouseStage.SeasonForHealth(2, 3), "below 75% of an odd maximum (2 of 3 = 66%)");
		Assert.Equal(0, TeaHouseStage.SeasonForHealth(3, 4), "exactly 75% is not below it");
		Assert.Equal(0, TeaHouseStage.SeasonForHealth(0, 0), "no maximum: spring");
		Assert.Equal(3, TeaHouseStage.SeasonForHealth(int.MinValue, int.MaxValue), "no overflow at the extremes");
	}

	static StageClock At(int round, int tick, int health) => new(round, tick, health, 1000);

	/// <summary>Weights (spring, summer, autumn, winter).</summary>
	static Vector4 W(float spring, float summer, float autumn, float winter) => new(spring, summer, autumn, winter);

	static void Mix(Vector4 expect, Vector4 got, string what) =>
		Assert.True(got.IsEqualApprox(expect), $"{what}: expected {expect}, got {got}");

	[Test]
	public static void TeaHouse_SeasonBeats_MonotonicFadeAndReset()
	{
		var b = new SeasonBeats();
		b.Observe(At(1, 0, 1000));
		Mix(W(1, 0, 0, 0), b.Weights(0), "spring at the fight's start");
		b.Observe(At(1, 100, 740));
		Mix(W(1, 0, 0, 0), b.Weights(100), "below 75%: the fade to summer starts on that tick");
		Mix(W(0.5f, 0.5f, 0, 0), b.Weights(100 + TeaHouseStage.SeasonFadeTicks / 2), "half way");
		Mix(W(0, 1, 0, 0), b.Weights(100 + TeaHouseStage.SeasonFadeTicks), "summer after the fade");
		b.Observe(At(1, 140, 900));
		Assert.Equal(1, b.Season, "health back up: still summer (never back)");
		b.Observe(At(1, 140, 900));
		Assert.Equal<int?>(100, b.ChangedTick, "the same tick seen twice changes nothing");
		b.Observe(At(1, 200, 200));
		Mix(W(0, 1, 0, 0), b.Weights(200), "two beats in one hit: summer fades straight to winter, from summer");
		Mix(W(0, 0, 0, 1), b.Weights(230), "winter");
		b.Observe(At(1, 260, 600));
		Assert.Equal(3, b.Season, "winter holds");
		b.Observe(At(2, 0, 1000));
		Mix(W(1, 0, 0, 0), b.Weights(0), "a new round (fight, Restart, reset): spring again");
		Assert.Equal<int?>(null, b.ChangedTick, "no fade left over from the last round");
		b.Observe(At(3, 0, 600));
		Mix(W(1, 0, 0, 0), b.Weights(0), "a round that opens below a beat fades in from spring");
		Mix(W(1, 0, 0, 0), new SeasonBeats().Weights(500), "nothing seen yet: spring");
		var cut = new SeasonBeats();
		cut.Observe(At(1, 10, 400));
		Mix(W(0, 0, 1, 0), cut.Weights(10, fadeTicks: 0), "a fade of 0 ticks is a hard cut");
	}

	[Test]
	public static void TeaHouse_BeatCrossedMidFade_TheScreensNeverJump()
	{
		// Code Critic: health 740 at tick 100, then 490 at tick 110 (summer's fade a third done). The autumn fade starts
		// from what shows at tick 110 (two thirds spring, a third summer), so from tick to tick no season's share moves
		// more than one tick's fade step, and every mix sums to 1.
		float step = 1f / TeaHouseStage.SeasonFadeTicks + 1e-5f;
		foreach (var (second, health2) in new[] { (110, 490), (101, 490), (129, 490), (110, 100), (100, 100) })
		{
			var b = new SeasonBeats();
			Vector4 last = W(1, 0, 0, 0);
			for (int t = 0; t <= 200; t++)
			{
				int health = t < 100 ? 1000 : t < second ? 740 : health2;
				b.Observe(At(1, t, health));
				Vector4 w = b.Weights(t);
				Vector4 d = (w - last).Abs();
				Assert.True(Mathf.Max(Mathf.Max(d.X, d.Y), Mathf.Max(d.Z, d.W)) <= step, $"beat at {second} (health {health2}): tick {t} jumps from {last} to {w}");
				Assert.True(Mathf.Abs(w.X + w.Y + w.Z + w.W - 1f) < 1e-4f, $"tick {t}: the shares sum to 1 ({w})");
				last = w;
			}
			Mix(SeasonBeats.Only(b.Season), last, $"beat at {second}: the last season reached in the end");
		}
		var mid = new SeasonBeats();
		mid.Observe(At(1, 100, 740));
		mid.Observe(At(1, 110, 490));
		Mix(W(2f / 3f, 1f / 3f, 0, 0), mid.Weights(110), "the autumn fade starts from the mix showing at tick 110");
		Mix(W(1f / 3f, 1f / 6f, 0.5f, 0), mid.Weights(125), "half way to autumn from that mix");
	}

	[Test]
	public static void TeaHouse_ScreensFollowTheTanukisHealth_InTheFight(Node runner)
	{
		var scene = Load(runner, StageCatalog.TeaHouseScene);
		try
		{
			var seasons = scene.StageRoot.GetNode<SeasonScreens>("Seasons");
			var mat = (ShaderMaterial)scene.StageRoot.GetNode<MeshInstance3D>("Backdrop").MaterialOverride;
			var p2 = scene.Match.P2;
			Vector4 Shader() => mat.GetShaderParameter("weights").AsVector4();
			Assert.True(seasons.Textures.All(t => t is not null), "the four season paintings are checked in");
			Assert.True(mat.GetShaderParameter("panel_rect").AsVector4().IsEqualApprox(TeaHouseStage.PanelRectUv), "the shader's panel box is the stage's");
			for (int i = 0; i < 4; i++)
				Assert.True(ReferenceEquals(mat.GetShaderParameter($"season_{i}").AsGodotObject(), seasons.Textures[i]), $"the shader has the {TeaHouseStage.Seasons[i]} painting");
			Mix(W(1, 1, 1, 1), mat.GetShaderParameter("present").AsVector4(), "all four paintings present");
			Mix(W(1, 0, 0, 0), Shader(), "spring at the start");
			for (int i = 0; i < 100; i++) scene.Step(FighterInput.None, FighterInput.None);
			Mix(W(1, 0, 0, 0), seasons.Weights, "full health: still spring");
			p2.Health = p2.MaxHealth * 74 / 100; // a health beat (the test drives health directly)
			scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal<int?>(scene.Match.Tick, seasons.Beats.ChangedTick, "the beat is seen on the tick it happens");
			Assert.Equal(1, seasons.Beats.Season, "summer");
			for (int i = 0; i < TeaHouseStage.SeasonFadeTicks; i++) scene.Step(FighterInput.None, FighterInput.None);
			Mix(W(0, 1, 0, 0), Shader(), "the shader shows summer after the 30-tick fade");
			p2.Health = p2.MaxHealth;
			for (int i = 0; i < 10; i++) scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal(1, seasons.Beats.Season, "health restored: summer stays");
			p2.Health = p2.MaxHealth * 49 / 100;
			for (int i = 0; i < 40; i++) scene.Step(FighterInput.None, FighterInput.None);
			Mix(W(0, 0, 1, 0), Shader(), "below 50%: autumn");
			scene.ResetFight(); // a reset (R after a KO, practice reset): spring again
			Mix(W(1, 0, 0, 0), Shader(), "after a reset: spring");
			p2.Health = p2.MaxHealth * 20 / 100;
			for (int i = 0; i < 40; i++) scene.Step(FighterInput.None, FighterInput.None);
			Mix(W(0, 0, 0, 1), Shader(), "below 25%: winter, straight from spring");
		}
		finally { scene.QueueFree(); }
	}

	[Test]
	public static void TeaHouse_MissingSeasonPaintings_ShowThePlainBackdrop_NoCrash(Node runner)
	{
		var none = SeasonScreens.Load("res://assets/generated/stage/missing/seasons/");
		Assert.True(none.Length == 4 && none.All(t => t is null), "nothing found, nothing thrown");
		SeasonScreens.SeasonDirOverride = "res://assets/generated/stage/missing/seasons/";
		try
		{
			var scene = Load(runner, StageCatalog.TeaHouseScene);
			try
			{
				Assert.Equal(TeaHouseStage.Kind, scene.StageKind, "the tea house still builds");
				var mat = (ShaderMaterial)scene.StageRoot.GetNode<MeshInstance3D>("Backdrop").MaterialOverride;
				Mix(W(0, 0, 0, 0), mat.GetShaderParameter("present").AsVector4(), "no painting present: the screens show the plain backdrop");
				scene.Match.P2.Health = 100;
				for (int i = 0; i < 40; i++) scene.Step(FighterInput.None, FighterInput.None);
				Assert.Equal(3, scene.StageRoot.GetNode<SeasonScreens>("Seasons").Beats.Season, "the beats still count");
				Mix(W(0, 0, 0, 0), mat.GetShaderParameter("present").AsVector4(), "winter missing too: still the plain backdrop");
			}
			finally { scene.QueueFree(); }
		}
		finally { SeasonScreens.SeasonDirOverride = null; }
	}

	// ---- riverbank depth and lanterns (designer 2026-11-09) ----

	/// <summary>World y of a row (0 = the image's top, of 720) of the riverbank's far painting.</summary>
	static float RowY(MeshInstance3D backdrop, float row)
	{
		var mat = (ShaderMaterial)backdrop.MaterialOverride;
		float sky = mat.GetShaderParameter("sky").AsSingle(), dv = mat.GetShaderParameter("art_rect").AsVector4().W;
		var size = ((QuadMesh)backdrop.Mesh).Size;
		float top = backdrop.Position.Y + size.Y / 2;
		return top - sky * size.Y - row / 720f / dv * (1f - sky) * size.Y;
	}

	/// <summary>The row (fractional) where the painting meets the 3D water plane (world y 0): pixel rows below it are hidden.</summary>
	static float JoinRow(MeshInstance3D backdrop)
	{
		float y0 = RowY(backdrop, 0f), y1 = RowY(backdrop, 1f);
		return y0 / (y0 - y1);
	}

	/// <summary>The painted horizon (the far reed line) and the first of the painting's big near lanterns, rows of backdrop.png (720 tall).</summary>
	const int PaintedHorizonRow = 185, FirstBigPaintedLanternRow = 210;
	/// <summary>A painted lantern's dark wooden cap sits above its orange body, which is what the colour search finds (measured: rows 192-193 over a body from 194).</summary>
	const int PaintedLanternCapRows = 2;

	/// <summary>
	/// The painting's floating lanterns near the far water (rows 186-299), found by colour: persimmon/orange pixels (red well
	/// above green and blue) stand out against the indigo water and the tan reeds. One blob per lantern (8-connected, 6 px
	/// or more): its top (cap included, <see cref="PaintedLanternCapRows"/>) and bottom rows.
	/// </summary>
	static List<(int Top, int Bottom)> PaintedLanterns()
	{
		var img = Image.LoadFromFile(ProjectSettings.GlobalizePath(StageCatalog.Riverbank.DefaultDir + "backdrop.png"));
		const int r0 = 186, r1 = 300;
		int w = img.GetWidth();
		var hit = new bool[r1 - r0, w];
		for (int y = r0; y < r1; y++)
			for (int x = 0; x < w; x++)
			{
				Color c = img.GetPixel(x, y);
				hit[y - r0, x] = c.R8 - c.B8 > 60 && c.R8 - c.G8 > 35 && c.R8 > 110;
			}
		var seen = new bool[r1 - r0, w];
		var blobs = new List<(int, int)>();
		for (int y = 0; y < r1 - r0; y++)
			for (int x = 0; x < w; x++)
			{
				if (!hit[y, x] || seen[y, x]) continue;
				var stack = new Stack<(int X, int Y)>();
				stack.Push((x, y)); seen[y, x] = true;
				int top = y, bottom = y, n = 0;
				while (stack.Count > 0)
				{
					var (px, py) = stack.Pop();
					n++; top = Math.Min(top, py); bottom = Math.Max(bottom, py);
					for (int dy = -1; dy <= 1; dy++)
						for (int dx = -1; dx <= 1; dx++)
						{
							int u = px + dx, v = py + dy;
							if (u < 0 || u >= w || v < 0 || v >= r1 - r0 || !hit[v, u] || seen[v, u]) continue;
							seen[v, u] = true; stack.Push((u, v));
						}
				}
				if (n >= 6) blobs.Add((top + r0 - PaintedLanternCapRows, bottom + r0));
			}
		img.Dispose();
		return blobs;
	}

	[Test]
	public static void Riverbank_NoPaintedLanternIsCutByTheWater(Node runner)
	{
		// Each painted lantern is either wholly above the join (shown with its reflection) or wholly under the 3D water,
		// never a half-dome at the cut.
		var scene = Load(runner, StageCatalog.RiverbankScene);
		try
		{
			float join = JoinRow(scene.StageRoot.GetNode<MeshInstance3D>("Backdrop"));
			var lanterns = PaintedLanterns();
			Assert.True(lanterns.Count >= 10, $"the painting's lanterns are found by colour ({lanterns.Count})");
			foreach (var (top, bottom) in lanterns)
				Assert.True(bottom + 1 <= join || top >= join, $"the painted lantern at rows {top}-{bottom} is cut by the water at row {join:0.0}");
		}
		finally { scene.QueueFree(); }
	}

	[Test]
	public static void Riverbank_ThePaintingIsTheFarViewOnly_HorizonAtTheEyeLine(Node runner)
	{
		var scene = Load(runner, StageCatalog.RiverbankScene);
		try
		{
			var stage = scene.StageRoot;
			var backdrop = stage.GetNode<MeshInstance3D>("Backdrop");
			var water = stage.GetNode<MeshInstance3D>("Water");
			var ws = ((PlaneMesh)water.Mesh).Size;
			float eye = scene.Cam.Config.BaseHeightMeters, horizon = RowY(backdrop, PaintedHorizonRow), join = JoinRow(backdrop);
			Assert.True(Mathf.Abs(horizon - eye) <= 0.3f, $"the painted horizon sits at the camera's eye line: y {horizon:0.00} vs {eye:0.00}");
			Assert.True(join <= FirstBigPaintedLanternRow, $"the painting's near water and big lanterns sit below the water plane (join at row {join:0.0})");
			Assert.True(backdrop.Position.Z <= -30f, $"the painting is far back (z {backdrop.Position.Z})");
			Assert.True(water.Position.Z - ws.Y / 2 <= backdrop.Position.Z && water.Position.Z + ws.Y / 2 >= -7f, "the 3D water runs from the bank to the painting: no gap, no seam to fill");
			// Perspective from bank to horizon: the nearest 3D lantern reads larger on screen than any painted one left showing.
			float px = ((QuadMesh)backdrop.Mesh).Size.X / 1280f;
			float painted = PaintedLanterns().Where(l => l.Bottom + 1 <= join).Select(l => (l.Bottom - l.Top + 1) * px).DefaultIfEmpty(0f).Max();
			var floats = stage.GetNode("Props/Floats").GetChildren().Cast<Node3D>().ToList();
			var nearest = floats.OrderByDescending(f => f.Position.Z).First();
			float nearSize = nearest.Scale.Y, nearZ = nearest.Position.Z;
			foreach (float d in new[] { FightScene.CameraDistance(scene.Match.Config) * scene.Cam.MinWidth / scene.Cam.MaxWidth, FightScene.CameraDistance(scene.Match.Config) })
				Assert.True(nearSize / (d - nearZ) > painted / (d - backdrop.Position.Z), $"camera {d:0.0} m out: the nearest 3D lantern ({nearSize:0.00} m at z {nearZ}) reads larger than a painted one ({painted:0.00} m at z {backdrop.Position.Z})");
		}
		finally { scene.QueueFree(); }
	}

	/// <summary>Normalised screen position (-1..1, y up) of a world point, as <see cref="DynamicCamera.Project"/> does for the Z = 0 plane.</summary>
	static (float X, float Y) ProjectPoint(DynamicCamera cam, Vector3 p)
	{
		var (cx, h, d, rigPitch) = cam.Transform;
		float pitch = -rigPitch, c = Mathf.Cos(pitch), s = Mathf.Sin(pitch), tanV = Mathf.Tan(Mathf.DegToRad(FightScene.CameraFovDegrees / 2f));
		float rx = p.X - cx, ry = p.Y - h, rz = p.Z - d;
		float yc = ry * c - rz * s, depth = -ry * s - rz * c;
		return (rx / (depth * tanV * cam.Config.Aspect), yc / (depth * tanV));
	}

	static Rect2 ScreenRect(DynamicCamera cam, Aabb box)
	{
		var pts = Enumerable.Range(0, 8).Select(i => ProjectPoint(cam, box.GetEndpoint(i))).ToList();
		float x0 = pts.Min(p => p.X), y0 = pts.Min(p => p.Y);
		return new Rect2(x0, y0, pts.Max(p => p.X) - x0, pts.Max(p => p.Y) - y0);
	}

	static void Walk(FightScene s, InputBits p1, InputBits p2, int ticks) { for (int t = 0; t < ticks; t++) s.Step(new FighterInput(p1), new FighterInput(p2)); }

	static void Corner(FightScene s, int side)
	{
		var toward = side < 0 ? InputBits.Left : InputBits.Right;
		for (int t = 0, lastX = int.MinValue; t < 1500 && s.Match.Fighters[side < 0 ? 0 : 1].X != lastX; t++)
		{
			lastX = s.Match.Fighters[side < 0 ? 0 : 1].X;
			s.Step(new FighterInput(toward), new FighterInput(toward));
		}
		Walk(s, toward, toward, 30);
	}

	/// <summary>A lantern drawn at most this share of the nearest one's size reads as background (designer 2026-11-09): it may pass behind a fighter.</summary>
	const float DeepLanternShare = 0.6f;

	[Test]
	public static void Riverbank_Lanterns_NearOnesNeverBehindAFighter_CentreShotsShowFourOrMore(Node runner)
	{
		// The standard framings (the stage capture's: close, mid, max separation, both corners), the Kappa as the opponent.
		// A near lantern's screen box never overlaps a fighter's from hip (0.8 m) to head (1.8 m), body half-width 0.6 m.
		// A deep one, drawn at most DeepLanternShare of the nearest lantern's size in that framing, is background and may.
		// The centre framings (close, max) show at least four lanterns clear of the fighters.
		var poses = new (string Name, Action<FightScene> Pose)[]
		{
			("close", s => Walk(s, InputBits.Right, InputBits.Left, 40)),
			("mid", s => Walk(s, InputBits.Left, InputBits.Right, 11)),
			("max", s => Walk(s, InputBits.Left, InputBits.Right, 400)),
			("left corner", s => Corner(s, -1)),
			("right corner", s => Corner(s, 1)),
		};
		foreach (var (name, pose) in poses)
		{
			var jump = YokaiFighters.Dev.DevJump.BuildFight(new YokaiFighters.Dev.DevFightSetup("kappa"), FightScene.LoadAbilityPool());
			Assert.True(jump.Session.Enter(jump.NodeId).Ok, "the Kappa's node");
			var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			scene.ExternalDrive = true;
			scene.Session = jump.Session;
			runner.AddChild(scene);
			try
			{
				Assert.Equal("riverbank", scene.StageKind, "the Kappa fights on the riverbank");
				pose(scene);
				Walk(scene, InputBits.None, InputBits.None, 90);
				var cam = scene.Cam;
				var z0 = new Vector3(0.37f, 1.1f, 0f);
				var (px, py) = cam.Project(z0.X * 200f, z0.Y);
				var mine = ProjectPoint(cam, z0);
				Assert.True(Mathf.Abs(px - mine.X) < 1e-4f && Mathf.Abs(py - mine.Y) < 1e-4f, "the depth projection agrees with DynamicCamera.Project on the Z = 0 plane");
				var floats = scene.StageRoot.GetNode("Props/Floats").GetChildren().Cast<Node3D>().ToList();
				Assert.True(floats.Count >= 12 && floats.Count <= 15, $"12-15 floating lanterns (decision log 2026-11-09, riverbank lanterns second pass), got {floats.Count}");
				float d = cam.Transform.Z;
				var nearest = floats.OrderByDescending(f => f.Position.Z).First();
				float Drawn(Node3D f) => f.Scale.Y / (d - f.Position.Z);
				int deep = 0, clearShown = 0;
				foreach (var f in floats)
				{
					Aabb box = Meshes(f).Select(m => m.GlobalTransform * m.Mesh.GetAabb()).Aggregate((a, b) => a.Merge(b));
					Rect2 lantern = ScreenRect(cam, box);
					bool background = Drawn(f) <= DeepLanternShare * Drawn(nearest);
					if (background) deep++;
					bool behind = false;
					foreach (var fighter in scene.Match.Fighters)
					{
						float x = FightScene.ToMeters(fighter.X), y = FightScene.ToMeters(fighter.Y);
						Rect2 body = ScreenRect(cam, new Aabb(new Vector3(x - 0.6f, y + 0.8f, 0f), new Vector3(1.2f, 1.0f, 0f)));
						if (!lantern.Intersects(body)) continue;
						behind = true;
						Assert.True(background, $"{name}: near lantern {f.Name} at ({f.Position.X}, {f.Position.Z}) shows behind a fighter at x {x:0.00}");
					}
					if (!behind && lantern.Position.X > -1f && lantern.End.X < 1f && lantern.Position.Y > -1f && lantern.End.Y < 1f) clearShown++;
				}
				Assert.True(deep >= 2 && deep < floats.Count, $"{name}: some lanterns are deep background, most are not ({deep})");
				if (name is "close" or "max") Assert.True(clearShown >= 4, $"{name}: at least four lanterns show clear of the fighters ({clearShown})");
			}
			finally { scene.QueueFree(); }
		}
	}

	// ---- fallbacks ----

	[Test]
	public static void StageSceneFor_AStageWithAssetsMissing_FallsBackToTheGrove()
	{
		var warnings = new List<string>();
		Func<string, bool> all = _ => true;
		Assert.Equal(StageCatalog.ShrineGateScene, FightScene.StageSceneFor(StageCatalog.ShrineGateScene, false, true, all, warnings.Add, _ => true), "assets present: the stage");
		Assert.Equal(FightScene.GroveScene, FightScene.StageSceneFor(StageCatalog.ShrineGateScene, false, true, all, warnings.Add, _ => false), "assets missing: the grove");
		Assert.True(warnings.Count == 1 && warnings[0].Contains("shrine_gate"), "and says so");
		Assert.Equal(FightScene.PlaceholderScene, FightScene.StageSceneFor(StageCatalog.RiverbankScene, false, false, all, null, _ => false), "and the grove's missing too: the placeholder");
		Assert.True(StageCatalog.AssetsPresent(FightScene.GroveScene) && StageCatalog.AssetsPresent("res://scenes/stage/unknown.tscn"), "nothing to check for a scene the catalog does not know");
		foreach (var st in Stages) Assert.True(st.Assets.Present(), $"{st.Kind}: its assets are checked in");
	}

	[Test]
	public static void NewStages_AssetsMissing_TheFightShowsTheGrove(Node runner)
	{
		foreach (var st in Stages)
		{
			st.Assets.Dir = "res://assets/generated/stage/missing/";
			try
			{
				var scene = Load(runner, st.Scene);
				Assert.Equal(BambooGroveStage.Kind, scene.StageKind, $"{st.Kind} assets missing: the grove");
				Assert.Equal(1, scene.GetNode(FightScene.StageContainerName).GetChildCount(), "one stage, nothing half built");
				scene.Step(FighterInput.None, FighterInput.None);
				scene.QueueFree();
			}
			finally { st.Assets.Dir = st.Assets.DefaultDir; }
		}
	}

	[Test]
	public static void ForcedStageArg_NamesAStageScene()
	{
		Assert.Equal(StageCatalog.TeaHouseScene, FightScene.ForcedStageArg(new[] { "--stage=tea_house" }), "--stage=tea_house");
		Assert.Equal<string?>(null, FightScene.ForcedStageArg(new[] { "--stage=placeholder" }), "the placeholder has its own flag");
		Assert.Equal<string?>(null, FightScene.ForcedStageArg(Array.Empty<string>()), "none");
		Assert.Equal<string?>(null, FightScene.ForcedStageArg(new[] { "--stage=../fight" }), "no escaping the stage folder");
	}

	[Test]
	public static void DevMenu_StageChoices_ForceEachStage()
	{
		Assert.Equal<string?>(null, YokaiFighters.Flow.GameFlow.DevStageScene(YokaiFighters.Dev.DevStage.Node), "the node's own stage: nothing forced");
		Assert.Equal<string?>(null, YokaiFighters.Flow.GameFlow.DevStageScene(YokaiFighters.Dev.DevStage.Placeholder), "the placeholder has its own flag");
		Assert.Equal(FightScene.GroveScene, YokaiFighters.Flow.GameFlow.DevStageScene(YokaiFighters.Dev.DevStage.Grove), "grove");
		Assert.Equal(StageCatalog.ShrineGateScene, YokaiFighters.Flow.GameFlow.DevStageScene(YokaiFighters.Dev.DevStage.ShrineGate), "shrine gate");
		Assert.Equal(StageCatalog.RiverbankScene, YokaiFighters.Flow.GameFlow.DevStageScene(YokaiFighters.Dev.DevStage.Riverbank), "riverbank");
		Assert.Equal(StageCatalog.TeaHouseScene, YokaiFighters.Flow.GameFlow.DevStageScene(YokaiFighters.Dev.DevStage.TeaHouse), "tea house");
	}

	[Test]
	public static void SelfWalkedRun_TheNextNodeGetsItsOwnStage(Node runner)
	{
		// Code Critic: a run the fight scene walks by itself goes on to its next node in the same scene; a Kitsune node
		// then an Oni node ends on the shrine gate (the stage is built again), not on the first node's grove.
		string was = FightScene.OwnMapFile;
		FightScene.OwnMapFile = ProjectSettings.GlobalizePath("res://tests/fixtures/maps/kitsune-then-oni.json");
		var s = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		s.ExternalDrive = true;
		s.FixedRunSeed = 48;
		try
		{
			runner.AddChild(s);
			Assert.True(s.OwnsSession && s.Node.NodeId == "fx-kitsune", "its own session, the Kitsune node first");
			Assert.Equal(BambooGroveStage.Kind, s.StageKind, "the Kitsune's grove");
			s.Match.P2.Health = 1;
			for (int t = 0; t < 900 && s.Match.Phase != MatchPhase.Over; t++)
				s.Step(new FighterInput(t % 2 == 0 ? InputBits.DebugStrike : InputBits.None), FighterInput.None);
			Assert.True(s.Match.Phase == MatchPhase.Over && s.Match.Winner == 0, "Ryo won the first node");
			s.BindingCard.Continue();
			s.Rewards.Confirm();
			if (s.Rewards.Step == YokaiFighters.Ui.RewardStep.Target && s.Stage == DemoStage.Reward) s.Rewards.Confirm();
			Assert.True(s.Stage == DemoStage.Fighting && s.Node.NodeId == "fx-oni", "straight into the Oni node");
			Assert.Equal("shrine-gate", s.StageKind, "the Oni's node: the stage is built again, the shrine gate");
			Assert.Equal(StageCatalog.ShrineGateScene, s.StageRoot.SceneFilePath, "the shrine gate scene");
			Assert.Equal(1, s.GetNode(FightScene.StageContainerName).GetChildCount(), "one stage under the container, the old one gone");
			s.Step(FighterInput.None, FighterInput.None);
		}
		finally { FightScene.OwnMapFile = was; s.QueueFree(); }
	}

	// ---- wiring ----

	[Test]
	public static void DataTable_EachYokaiFightsOnItsStage()
	{
		var table = NodeSceneTable.Default(ContentPaths.Data("map"));
		var expect = new Dictionary<string, string>
		{
			["oni"] = StageCatalog.ShrineGateScene, ["elder-oni"] = StageCatalog.ShrineGateScene,
			["kappa"] = StageCatalog.RiverbankScene, ["elder-kappa"] = StageCatalog.RiverbankScene,
			["tanuki"] = StageCatalog.TeaHouseScene,
			["kitsune"] = FightScene.GroveScene, ["nine-tailed-kitsune"] = FightScene.GroveScene, ["apprentice"] = FightScene.GroveScene,
		};
		int seen = 0;
		foreach (string map in new[] { "default-map.json", "demo-map.json" })
		{
			using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(ContentPaths.Data("map"), map)));
			foreach (var n in doc.RootElement.GetProperty("nodes").EnumerateArray())
			{
				if (!n.TryGetProperty("yokai", out var y)) continue;
				string yokai = y.GetString()!, type = n.GetProperty("type").GetString()!;
				bool elder = n.TryGetProperty("elder", out var e) && e.GetBoolean();
				var ctx = table.Resolve(new MapNodeData(n.GetProperty("id").GetString()!, n.GetProperty("row").GetInt32(), type, yokai, elder)).Context;
				Assert.True(expect.ContainsKey(yokai), $"{map}: a stage is expected for '{yokai}'");
				Assert.Equal(expect[yokai], ctx.Stage, $"{map} {n.GetProperty("id").GetString()} ({yokai})");
				Assert.True(ResourceLoader.Exists(ctx.Stage!) && StageCatalog.AssetsPresent(ctx.Stage!), $"{ctx.Stage}: exists with its assets");
				seen++;
			}
		}
		Assert.True(seen >= 15, $"every fight node checked ({seen})");
	}

	const string Table = """
	{ "kind": "node_scenes", "id": "t", "schema_version": 1, "default_scene": "res://scenes/fight.tscn", "fallback_fighter": "kitsune",
	  "default_stage": "res://scenes/stage/bamboo_grove.tscn",
	  "tiers": { "yokai": { "reaction_frames": 22 } },
	  "types": { "duel": { "tier": "yokai" }, "elder": { "tier": "yokai", "stage": "res://scenes/stage/placeholder.tscn" } },
	  "nodes": { "den": { "stage": "res://scenes/stage/den.tscn" } },
	  "opponents": { "kitsune": { "fighter": "kitsune" }, "elder-oni": { "plays_as": "oni" }, "elder-kappa": { "plays_as": "kappa" } },
	  "opponent_stages": { "oni": "res://scenes/stage/shrine_gate.tscn", "elder-kappa": "res://scenes/stage/riverbank.tscn" } }
	""";

	[Test]
	public static void Table_OpponentStages_NodeThenYokaiThenPlaysAsThenTypeThenDefault()
	{
		var t = NodeSceneTable.Parse(Table);
		string? Stage(string id, string type, string yokai) => t.Resolve(new MapNodeData(id, 1, type, yokai)).Context.Stage;
		Assert.Equal("res://scenes/stage/den.tscn", Stage("den", "duel", "oni"), "the node's own stage wins");
		Assert.Equal(StageCatalog.ShrineGateScene, Stage("a", "duel", "oni"), "then the yokai's");
		Assert.Equal(StageCatalog.ShrineGateScene, Stage("b", "elder", "elder-oni"), "then the yokai it plays as, over the type's");
		Assert.Equal(StageCatalog.RiverbankScene, Stage("c", "elder", "elder-kappa"), "an elder named by its own id");
		Assert.Equal(FightScene.PlaceholderScene, Stage("d", "elder", "kitsune"), "then the type's");
		Assert.Equal(FightScene.GroveScene, Stage("e", "duel", "kitsune"), "then default_stage");
		foreach (var (json, what) in new[]
		{
			(Table.Replace("\"oni\": \"res://scenes/stage/shrine_gate.tscn\"", "\"oni\": \"res://scenes/fight.tscn\""), "a yokai stage outside scenes/stage/"),
			(Table.Replace("\"oni\": \"res://scenes/stage/shrine_gate.tscn\"", "\"oni\": \"res://scenes/stage/../fight.tscn\""), "a '..' in a yokai stage"),
			(Table.Replace("\"oni\": \"res://scenes/stage/shrine_gate.tscn\"", "\"oni\": 3"), "a yokai stage that is not a string"),
			(Table.Replace("\"oni\": \"res://scenes/stage/shrine_gate.tscn\"", "\"oni\": \"\""), "an empty yokai stage"),
			(Table.Replace("\"opponent_stages\": {", "\"opponent_stages\": [ ], \"x\": {"), "opponent_stages not an object"),
			(Table.Replace("\"oni\": \"res://scenes/stage/shrine_gate.tscn\"", "\"onii\": \"res://scenes/stage/shrine_gate.tscn\""), "a yokai the table does not know (a typo: onii)"),
		})
		{
			bool threw = false;
			try { NodeSceneTable.Parse(json); } catch (FormatException) { threw = true; }
			Assert.True(threw, $"{what} is refused");
		}
	}
}
