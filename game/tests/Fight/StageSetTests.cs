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
			float front = p.Z + radius, limit = StageRules.FrontLimit(p.X, lantern);
			Assert.True(p.Z - radius > backdrop.Position.Z, $"{st.Kind}/{n.Name} at z {p.Z} (radius {radius}) pokes through the backdrop at z {backdrop.Position.Z}");
			if (lantern)
			{
				lanterns++;
				Assert.True(p.Z <= StageRules.LanternMaxZ, $"{st.Kind}/{n.Name} at z {p.Z}: lanterns stay deep");
			}
			if (Mathf.Abs(p.X) < StageRules.ClearBandHalfX)
				Assert.True(front <= StageRules.ClearBandBackZ + 1e-3f, $"{st.Kind}/{n.Name} at ({p.X}, {p.Z}) reaches z {front}, inside the clear band");
			Assert.True(front <= limit + 1e-3f, $"{st.Kind}/{n.Name}: front z {front} past its limit {limit}");
		}
		Assert.True(lanterns >= 2, $"{st.Kind}: its lanterns were checked ({lanterns})");
		if (st.Kind == "riverbank") Assert.True(lanterns >= 8 && lanterns <= 10, $"riverbank: 8-10 floating lanterns (designer 2026-11-09), got {lanterns}");
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

	[Test]
	public static void TeaHouse_SeasonBeats_MonotonicFadeAndReset()
	{
		var b = new SeasonBeats();
		b.Observe(At(1, 0, 1000));
		Assert.Equal((0, 0, 1f), b.Shown(0), "spring at the fight's start");
		b.Observe(At(1, 100, 740));
		Assert.Equal((0, 1, 0f), b.Shown(100), "below 75%: the fade to summer starts on that tick");
		Assert.Equal((0, 1, 0.5f), b.Shown(100 + TeaHouseStage.SeasonFadeTicks / 2), "half way");
		Assert.Equal((1, 1, 1f), b.Shown(100 + TeaHouseStage.SeasonFadeTicks), "summer after the fade");
		b.Observe(At(1, 140, 900));
		Assert.Equal(1, b.Season, "health back up: still summer (never back)");
		b.Observe(At(1, 140, 900));
		Assert.Equal<int?>(100, b.ChangedTick, "the same tick seen twice changes nothing");
		b.Observe(At(1, 200, 200));
		Assert.Equal((1, 3, 0f), b.Shown(200), "two beats in one hit: summer fades straight to winter");
		Assert.Equal((3, 3, 1f), b.Shown(230), "winter");
		b.Observe(At(1, 260, 600));
		Assert.Equal(3, b.Season, "winter holds");
		b.Observe(At(2, 0, 1000));
		Assert.Equal((0, 0, 1f), b.Shown(0), "a new round (fight, Restart, reset): spring again");
		Assert.Equal<int?>(null, b.ChangedTick, "no fade left over from the last round");
		b.Observe(At(3, 0, 600));
		Assert.Equal((0, 1, 0f), b.Shown(0), "a round that opens below a beat fades in from spring");
		Assert.Equal((0, 0, 1f), new SeasonBeats().Shown(500), "nothing seen yet: spring");
		var cut = new SeasonBeats();
		cut.Observe(At(1, 10, 400));
		Assert.Equal((2, 2, 1f), cut.Shown(10, fadeTicks: 0), "a fade of 0 ticks is a hard cut");
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
			Assert.True(seasons.Textures.All(t => t is not null), "the four season paintings are checked in");
			Assert.True(mat.GetShaderParameter("panel_rect").AsVector4().IsEqualApprox(TeaHouseStage.PanelRectUv), "the shader's panel box is the stage's");
			Assert.Equal((0, 0, 1f), seasons.Shown, "spring at the start");
			Assert.True(ReferenceEquals(mat.GetShaderParameter("season_to").AsGodotObject(), seasons.Textures[0]) && mat.GetShaderParameter("use_to").AsSingle() == 1f, "the shader shows the spring painting");
			for (int i = 0; i < 100; i++) scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal((0, 0, 1f), seasons.Shown, "full health: still spring");
			p2.Health = p2.MaxHealth * 74 / 100; // a health beat (the test drives health directly)
			scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal<int?>(scene.Match.Tick, seasons.Beats.ChangedTick, "the beat is seen on the tick it happens");
			Assert.Equal(1, seasons.Beats.Season, "summer");
			for (int i = 0; i < TeaHouseStage.SeasonFadeTicks; i++) scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal((1, 1, 1f), seasons.Shown, "summer after the 30-tick fade");
			Assert.True(ReferenceEquals(mat.GetShaderParameter("season_to").AsGodotObject(), seasons.Textures[1]) && Mathf.IsEqualApprox(mat.GetShaderParameter("blend").AsSingle(), 1f), "the shader shows summer");
			p2.Health = p2.MaxHealth;
			for (int i = 0; i < 10; i++) scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal(1, seasons.Beats.Season, "health restored: summer stays");
			p2.Health = p2.MaxHealth * 49 / 100;
			for (int i = 0; i < 40; i++) scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal((2, 2, 1f), seasons.Shown, "below 50%: autumn");
			scene.ResetFight(); // a reset (R after a KO, practice reset): spring again
			Assert.Equal((0, 0, 1f), seasons.Shown, "after a reset: spring");
			p2.Health = p2.MaxHealth * 20 / 100;
			for (int i = 0; i < 40; i++) scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal((3, 3, 1f), seasons.Shown, "below 25%: winter, straight from spring");
			Assert.True(ReferenceEquals(mat.GetShaderParameter("season_to").AsGodotObject(), seasons.Textures[3]), "the shader shows winter");
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
				Assert.True(mat.GetShaderParameter("use_to").AsSingle() == 0f && mat.GetShaderParameter("use_from").AsSingle() == 0f, "the screens show the plain backdrop");
				scene.Match.P2.Health = 100;
				for (int i = 0; i < 40; i++) scene.Step(FighterInput.None, FighterInput.None);
				Assert.Equal(3, scene.StageRoot.GetNode<SeasonScreens>("Seasons").Beats.Season, "the beats still count");
				Assert.True(mat.GetShaderParameter("use_to").AsSingle() == 0f, "winter missing too: still the plain backdrop");
			}
			finally { scene.QueueFree(); }
		}
		finally { SeasonScreens.SeasonDirOverride = null; }
	}

	[Test]
	public static void NewStages_NeverChangeTheSim(Node runner)
	{
		// The stage reads the clock; the sim never reads the stage: the same inputs give the same state hash on any stage.
		var hashes = new List<(string, ulong)>();
		foreach (string stage in new[] { FightScene.GroveScene }.Concat(Stages.Select(s => s.Scene)))
		{
			var scene = Load(runner, stage);
			for (int t = 0; t < 240; t++)
				scene.Step(new FighterInput(t % 40 < 20 ? InputBits.Right : InputBits.LightPunch), FighterInput.None);
			hashes.Add((stage, scene.Match.StateHash()));
			scene.QueueFree();
		}
		Assert.True(hashes.All(h => h.Item2 == hashes[0].Item2), "state hash the same on every stage: " + string.Join(", ", hashes));
	}

	// ---- riverbank depth and lanterns (designer 2026-11-09) ----

	/// <summary>The backdrop row (0 = the image's top, of 720) the riverbank's far painting shows at world height <paramref name="y"/>'s inverse: the world y of a row.</summary>
	static float RowY(MeshInstance3D backdrop, int row)
	{
		var mat = (ShaderMaterial)backdrop.MaterialOverride;
		float sky = mat.GetShaderParameter("sky").AsSingle(), dv = mat.GetShaderParameter("art_rect").AsVector4().W;
		var size = ((QuadMesh)backdrop.Mesh).Size;
		float top = backdrop.Position.Y + size.Y / 2;
		return top - sky * size.Y - row / 720f / dv * (1f - sky) * size.Y;
	}

	/// <summary>The painted horizon (the far reed line) and the first of the painting's big near lanterns, rows of backdrop.png (720 tall).</summary>
	const int PaintedHorizonRow = 185, FirstBigPaintedLanternRow = 210;
	/// <summary>The tallest painted lantern above that row (measured: rows 199-210 and 200-210, 11 px), with a pixel to spare.</summary>
	const int PaintedLanternMaxPx = 12;

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
			float eye = scene.Cam.Config.BaseHeightMeters, horizon = RowY(backdrop, PaintedHorizonRow);
			Assert.True(Mathf.Abs(horizon - eye) <= 0.3f, $"the painted horizon sits at the camera's eye line: y {horizon:0.00} vs {eye:0.00}");
			float sinkRow = Enumerable.Range(0, 720).First(r => RowY(backdrop, r) <= 0f);
			Assert.True(sinkRow <= FirstBigPaintedLanternRow, $"the painting's near water and big lanterns sit below the water plane (y 0 at row {sinkRow})");
			Assert.True(backdrop.Position.Z <= -30f, $"the painting is far back (z {backdrop.Position.Z})");
			Assert.True(water.Position.Z - ws.Y / 2 <= backdrop.Position.Z && water.Position.Z + ws.Y / 2 >= -7f, "the 3D water runs from the bank to the painting: no gap, no seam to fill");
			// Perspective from bank to horizon: the nearest 3D lantern reads larger on screen than any painted one.
			float px = ((QuadMesh)backdrop.Mesh).Size.X / 1280f, painted = PaintedLanternMaxPx * px;
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

	[Test]
	public static void Riverbank_FewLanterns_NoneBehindAFighterBetweenHipAndHead(Node runner)
	{
		// The standard framings (the stage capture's: close, mid, max separation, both corners), the Kappa as the opponent.
		// No floating lantern's screen box overlaps a fighter's from hip (0.8 m) to head (1.8 m), body half-width 0.6 m.
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
				Assert.True(floats.Count >= 8 && floats.Count <= 10, $"8-10 floating lanterns, got {floats.Count}");
				int onScreen = 0;
				foreach (var f in floats)
				{
					Aabb box = Meshes(f).Select(m => m.GlobalTransform * m.Mesh.GetAabb()).Aggregate((a, b) => a.Merge(b));
					Rect2 lantern = ScreenRect(cam, box);
					if (lantern.Position.X < 1f && lantern.End.X > -1f) onScreen++;
					foreach (var fighter in scene.Match.Fighters)
					{
						float x = FightScene.ToMeters(fighter.X), y = FightScene.ToMeters(fighter.Y);
						Rect2 body = ScreenRect(cam, new Aabb(new Vector3(x - 0.6f, y + 0.8f, 0f), new Vector3(1.2f, 1.0f, 0f)));
						Assert.True(!lantern.Intersects(body), $"{name}: {f.Name} at ({f.Position.X}, {f.Position.Z}) shows behind a fighter at x {x:0.00}");
					}
				}
				if (name is "close" or "max") Assert.True(onScreen >= 2, $"{name}: some lanterns show ({onScreen})");
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
	  "opponents": { "kitsune": { "fighter": "kitsune" }, "elder-oni": { "plays_as": "oni" } },
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
		})
		{
			bool threw = false;
			try { NodeSceneTable.Parse(json); } catch (FormatException) { threw = true; }
			Assert.True(threw, $"{what} is refused");
		}
	}
}
