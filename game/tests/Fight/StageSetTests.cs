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
		if (st.Kind == "riverbank") Assert.True(lanterns >= 15 && lanterns <= 30, $"riverbank: 15-30 floating lanterns, got {lanterns}");
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

	// ---- tea house seasons ----

	[Test]
	public static void TeaHouse_SeasonIsAPureFunctionOfTheRound()
	{
		int[] expect = { 0, 1, 2, 3, 0, 1, 2, 3 };
		for (int round = 1; round <= expect.Length; round++)
			Assert.Equal(expect[round - 1], TeaHouseStage.SeasonForRound(round), $"round {round}: (n - 1) mod 4");
		Assert.Equal("spring", TeaHouseStage.Seasons[TeaHouseStage.SeasonForRound(1)], "the first round is spring");
		Assert.Equal(0, TeaHouseStage.SeasonForRound(0), "no round yet: as round 1");
		Assert.Equal(0, TeaHouseStage.SeasonForRound(-5), "a negative round: as round 1");
		int huge = TeaHouseStage.SeasonForRound(int.MaxValue);
		Assert.True(huge >= 0 && huge < TeaHouseStage.Seasons.Length, $"a huge round stays in range ({huge})");
		Assert.Equal((0, 0, 1f), SeasonScreens.At(1, 0), "round 1: spring at once, no fade");
		Assert.Equal((0, 1, 0f), SeasonScreens.At(2, 0), "round 2 starts on spring");
		Assert.Equal((0, 1, 0.5f), SeasonScreens.At(2, TeaHouseStage.SeasonFadeTicks / 2), "half way through the fade");
		Assert.Equal((0, 1, 1f), SeasonScreens.At(2, TeaHouseStage.SeasonFadeTicks), "summer by the end of the fade");
		Assert.Equal((0, 1, 1f), SeasonScreens.At(2, 100_000), "and it holds for the rest of the round (never changes mid-round)");
		Assert.Equal((3, 0, 1f), SeasonScreens.At(5, 999), "winter back to spring");
		Assert.Equal((2, 2, 1f), SeasonScreens.At(3, 0, fadeTicks: 0), "a fade of 0 ticks is a hard cut to autumn");
	}

	[Test]
	public static void TeaHouse_ScreensFollowTheRoundStart_InTheFight(Node runner)
	{
		var scene = Load(runner, StageCatalog.TeaHouseScene);
		try
		{
			var seasons = scene.StageRoot.GetNode<SeasonScreens>("Seasons");
			var mat = (ShaderMaterial)scene.StageRoot.GetNode<MeshInstance3D>("Backdrop").MaterialOverride;
			Assert.True(seasons.Textures.All(t => t is not null), "the four season paintings are checked in");
			Assert.True(mat.GetShaderParameter("panel_rect").AsVector4().IsEqualApprox(TeaHouseStage.PanelRectUv), "the shader's panel box is the stage's");
			Assert.Equal(1, scene.StageRound, "the first round");
			Assert.Equal((0, 0, 1f), seasons.Shown, "spring");
			Assert.True(ReferenceEquals(mat.GetShaderParameter("season_to").AsGodotObject(), seasons.Textures[0]) && mat.GetShaderParameter("use_to").AsSingle() == 1f, "the shader shows the spring painting");
			for (int i = 0; i < 400; i++) scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal((0, 0, 1f), seasons.Shown, "still spring 400 ticks in: never mid-round");
			scene.ResetFight(); // the duel replayed: the next round starts
			Assert.Equal(2, scene.StageRound, "round 2");
			Assert.Equal((0, 1, 0f), seasons.Shown, "round 2 opens on spring, fading to summer");
			for (int i = 0; i < TeaHouseStage.SeasonFadeTicks; i++) scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal((0, 1, 1f), seasons.Shown, "summer after the fade");
			Assert.True(ReferenceEquals(mat.GetShaderParameter("season_to").AsGodotObject(), seasons.Textures[1]) && Mathf.IsEqualApprox(mat.GetShaderParameter("blend").AsSingle(), 1f), "the shader shows summer");
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
				for (int i = 0; i < 10; i++) scene.Step(FighterInput.None, FighterInput.None);
				scene.ResetFight();
				for (int i = 0; i < 40; i++) scene.Step(FighterInput.None, FighterInput.None);
				Assert.Equal(2, scene.StageRound, "rounds still count");
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
