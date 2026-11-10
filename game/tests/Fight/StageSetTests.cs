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
/// the gameplay plane and the clear band, lanterns deep, a handful of shadowless lights (the riverbank's swamp has no
/// lanterns, designer 2026-11-09), the art covering every camera framing; scenery moves only with the sim tick; the tea
/// house's seasons follow the round; the riverbank's far painting meets its water; missing assets fall back; each yokai's
/// fights are wired to its stage.
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

	/// <summary>Every placed prop under Props with its transform in Props space; a plain grouping node (no mesh of its own) counts its children one by one.</summary>
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
		if (st.Kind == "riverbank") Assert.Equal(0, lanterns, "riverbank: a swamp without lanterns (designer 2026-11-09)");
		else Assert.True(lanterns >= 2, $"{st.Kind}: its lanterns were checked ({lanterns})");
	});

	[Test]
	public static void NewStages_AHandfulOfShadowlessLanternLights(Node runner) => ForEachStage(runner, (scene, st) =>
	{
		var lights = scene.StageRoot.FindChildren("*", "OmniLight3D", true, false).Cast<OmniLight3D>().ToList();
		int fewest = st.Kind == "riverbank" ? 0 : 2; // the riverbank's swamp has no lanterns (designer 2026-11-09)
		Assert.True(lights.Count >= fewest && lights.Count <= StageRules.MaxLanternLights, $"{st.Kind}: a handful of lantern lights, got {lights.Count}");
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
			Assert.True(Mathf.IsEqualApprox(mat.GetShaderParameter("painting_bottom_v").AsSingle(), TeaHouseStage.PaintingBottomV), "the shader's painting bottom is the stage's");
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
	public static void TeaHouse_SeasonPaintings_RunDownToTheSill_NoBlankPaperStrip(Node runner)
	{
		string code = string.Concat(GD.Load<Shader>("res://shaders/season_backdrop.gdshader").Code.Where(c => !char.IsWhiteSpace(c)));
		Assert.True(code.Contains("vec2cuv=clamp(suv-vec2(0.0,1.0-painting_bottom_v)"), "the shader shifts the paintings down by painting_bottom_v before sampling");
		foreach (string season in TeaHouseStage.Seasons)
		{
			var img = Image.LoadFromFile(ProjectSettings.GlobalizePath($"{TeaHouseStage.DefaultDir}seasons/{season}.png"));
			int w = img.GetWidth(), h = img.GetHeight();
			int bottom = Mathf.RoundToInt(TeaHouseStage.PaintingBottomV * h) - 1; // the last painting row the screens show
			Assert.True(Mathf.IsEqualApprox((float)w / h, TeaHouseStage.PanelRectUv.Z * 1280f / (TeaHouseStage.PanelRectUv.W * 720f), 0.01f),
				$"{season}: the painting has the screens' aspect, so the shift is unscaled");
			bool Ink(Color c) => c.R8 + c.G8 + c.B8 < 210;
			int InkCount(int y) => Enumerable.Range(0, w).Count(x => Ink(img.GetPixel(x, y)));
			int inkLine = Enumerable.Range(0, h).Last(y => InkCount(y) * 10 > w * 6); // the ground line under the hills
			Assert.True(Enumerable.Range(bottom - 1, 3).All(y => InkCount(y) * 100 < w * 15) && inkLine > bottom + 1, $"{season}: no ink line (15% of a row; autumn's dark hills are ~9%) within a row of the sill (last shown row {bottom}; ground line at {inkLine})");
			Color paper = img.GetPixel(w / 2, h - 1);
			bool Paper(Color c) => Math.Abs(c.R8 - paper.R8) + Math.Abs(c.G8 - paper.G8) + Math.Abs(c.B8 - paper.B8) < 12;
			int shownPaper = Enumerable.Range(0, w).Count(x => Paper(img.GetPixel(x, bottom)));
			Assert.True(shownPaper * 4 < w, $"{season}: the screens' bottom row is landscape, not the blank paper strip (a paper row is all paper; winter's snow matches ~6%) ({shownPaper} of {w} px paper)");
			Color sky = img.GetPixel(w - 1, 0);
			Assert.True(Enumerable.Range(0, w).All(x => { Color c = img.GetPixel(x, 0); return Math.Abs(c.R8 - sky.R8) + Math.Abs(c.G8 - sky.G8) + Math.Abs(c.B8 - sky.B8) < 12; }),
				$"{season}: row 0 is flat sky, so repeating it above the painting leaves no seam");
		}
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

	// ---- riverbank: the far swamp (designer 2026-11-09, swamp retake: Astra take 4, no lanterns) ----

	static ShaderMaterial FarMaterial(MeshInstance3D backdrop) => (ShaderMaterial)backdrop.MaterialOverride;

	static Texture2D FarTexture(MeshInstance3D backdrop) => FarMaterial(backdrop).GetShaderParameter("tex").As<Texture2D>();

	/// <summary>Metres per painting pixel on the quad: (across, down).</summary>
	static (float Across, float Down) FarScale(MeshInstance3D backdrop)
	{
		var mat = FarMaterial(backdrop);
		var tex = FarTexture(backdrop);
		Vector4 art = mat.GetShaderParameter("art_rect").AsVector4();
		float sky = mat.GetShaderParameter("sky").AsSingle();
		var size = ((QuadMesh)backdrop.Mesh).Size;
		return (size.X / (art.Z * tex.GetWidth()), size.Y * (1f - sky) / (art.W * tex.GetHeight()));
	}

	/// <summary>World y of a pixel row (0 = the image's top) of the riverbank's far painting.</summary>
	static float RowY(MeshInstance3D backdrop, float row)
	{
		var mat = FarMaterial(backdrop);
		float sky = mat.GetShaderParameter("sky").AsSingle(), top0 = mat.GetShaderParameter("art_rect").AsVector4().Y * FarTexture(backdrop).GetHeight();
		var size = ((QuadMesh)backdrop.Mesh).Size;
		float top = backdrop.Position.Y + size.Y / 2 - sky * size.Y;
		return top - (row - top0) * FarScale(backdrop).Down;
	}

	/// <summary>The row (fractional) where the painting meets the 3D water plane at <paramref name="waterY"/>: rows below it are hidden.</summary>
	static float JoinRow(MeshInstance3D backdrop, float waterY)
	{
		float y0 = RowY(backdrop, 0f), y1 = RowY(backdrop, 1f);
		return (y0 - waterY) / (y0 - y1);
	}

	/// <summary>
	/// The painting's far waterline and its flat water, measured: the water colour is the median of the bottom row; a row is
	/// water where at least half its pixels are within 20 (summed RGB) of it. Waterline = the first row of the band of water
	/// rows that runs to the bottom of the image (the far bank's ink line sits just above it).
	/// </summary>
	static (int WaterlineRow, Color Water, int Rows) PaintedWater()
	{
		var img = Image.LoadFromFile(ProjectSettings.GlobalizePath(StageCatalog.Riverbank.DefaultDir + "backdrop.png"));
		int w = img.GetWidth(), h = img.GetHeight();
		int Median(Func<Color, int> ch) => Enumerable.Range(0, w).Select(x => ch(img.GetPixel(x, h - 1))).OrderBy(v => v).ElementAt(w / 2);
		int r = Median(c => c.R8), g = Median(c => c.G8), b = Median(c => c.B8);
		bool WaterRow(int y) => Enumerable.Range(0, w).Count(x => { Color c = img.GetPixel(x, y); return Math.Abs(c.R8 - r) + Math.Abs(c.G8 - g) + Math.Abs(c.B8 - b) < 20; }) * 2 >= w;
		int row = h - 1;
		while (row > 0 && WaterRow(row - 1)) row--;
		img.Dispose();
		return (row, Color.Color8((byte)r, (byte)g, (byte)b), h);
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

	[Test]
	public static void Riverbank_ASwampWithoutLanterns_StillAWarmFill(Node runner)
	{
		// Designer 2026-11-09: "resemble a swamp more without the lanterns". The other stages keep theirs (StageRules).
		var scene = Load(runner, StageCatalog.RiverbankScene);
		try
		{
			var stage = scene.StageRoot;
			var all = stage.FindChildren("*", "", true, false);
			Assert.Equal(0, all.Count(n => n.IsInGroup(LanternGroup)), "no lantern in the group");
			Assert.Equal(0, stage.FindChildren("*", "OmniLight3D", true, false).Count, "no lantern lights");
			Assert.Equal(0, all.Count(n => n is TickMotion), "nothing bobbing on the water");
			Assert.True(!stage.HasNode("Props/Floats"), "the floating lanterns are gone");
			Assert.True(!StageCatalog.Riverbank.Required.Contains("floating-lantern.glb"), "and the catalog no longer asks for their model");
			var fill = stage.GetNode<DirectionalLight3D>("Fill");
			Assert.True(fill.LightEnergy > 0f && fill.LightColor.R > fill.LightColor.B + 0.2f && !fill.ShadowEnabled, "a little warm fill keeps the fighters from going flat");
			foreach (var (n, _) in PropItems(stage.GetNode("Props")))
				Assert.True(n.Name.ToString().StartsWith("Reeds") || n.Name.ToString().StartsWith("Post"), $"{n.Name}: the reeds and mooring posts stay, nothing else");
		}
		finally { scene.QueueFree(); }
	}

	[Test]
	public static void Riverbank_ThePaintingDownToItsWaterline_MeetsTheWater_HorizonAtTheEyeLine(Node runner)
	{
		var scene = Load(runner, StageCatalog.RiverbankScene);
		try
		{
			var stage = scene.StageRoot;
			var backdrop = stage.GetNode<MeshInstance3D>("Backdrop");
			var water = stage.GetNode<MeshInstance3D>("Water");
			var ws = ((PlaneMesh)water.Mesh).Size;
			var mat = FarMaterial(backdrop);
			var (waterline, _, rows) = PaintedWater();
			Assert.True(waterline > rows * 0.7f && waterline < rows * 0.95f, $"the painting's far waterline is found low in the frame (row {waterline} of {rows})");
			Vector4 art = mat.GetShaderParameter("art_rect").AsVector4();
			float bottomRow = (art.Y + art.W) * rows, join = JoinRow(backdrop, water.Position.Y);
			Assert.True(art.X == 0f && art.Y == 0f && art.Z == 1f, "the whole painting from its top row, edge to edge");
			Assert.True(join >= waterline && join <= waterline + 12, $"the 3D water meets the painting just under its waterline: join row {join:0.0}, waterline {waterline}");
			Assert.True(bottomRow >= join + 1f, $"the art reaches below the join (bottom row {bottomRow:0.0}): no gap");
			Assert.True(backdrop.Position.Y - ((QuadMesh)backdrop.Mesh).Size.Y / 2 <= water.Position.Y, "the quad reaches down past the water");
			Assert.True(Mathf.Abs(mat.GetShaderParameter("water_v").AsSingle() * rows - waterline) <= 1f, "the shader's water starts at the measured waterline");
			Assert.True(backdrop.Position.Z <= -30f, $"the painting is far back (z {backdrop.Position.Z})");
			Assert.True(water.Position.Z - ws.Y / 2 <= backdrop.Position.Z && water.Position.Z + ws.Y / 2 >= -7f, "the 3D water runs from the bank to the painting: no gap");
			// The painted horizon (its far waterline) sits at about the eye line in the close framing at the stage's centre.
			var cam = new DynamicCamera(scene.Match.Config);
			cam.Place(cam.MinWidth, cam.Config.BaseHeightMeters, 0f);
			var (_, sy) = ProjectPoint(cam, new Vector3(0f, RowY(backdrop, waterline), backdrop.Position.Z));
			Assert.True(Mathf.Abs(sy) <= 0.15f, $"the far waterline is near the eye line: screen y {sy:0.000} (of -1..1)");
		}
		finally { scene.QueueFree(); }
	}

	[Test]
	public static void Riverbank_FarPainting_SharpUnstretched_TreesNotGiants(Node runner)
	{
		var scene = Load(runner, StageCatalog.RiverbankScene);
		try
		{
			var backdrop = scene.StageRoot.GetNode<MeshInstance3D>("Backdrop");
			var (across, down) = FarScale(backdrop);
			var (waterline, _, _) = PaintedWater();
			Assert.True(across / down >= 0.9f && across / down <= 1.1f, $"at most about 10% anamorphic stretch (across/down {across / down:0.000})");
			var cam = new DynamicCamera(scene.Match.Config);
			float tanV = Mathf.Tan(Mathf.DegToRad(FightScene.CameraFovDegrees / 2f));
			float near = cam.DistanceFor(cam.MinWidth), depth = near - backdrop.Position.Z;
			// Sharp: in the closest framing at 1080 rows, one painting pixel spans at most 1.5 screen pixels (it was 7.4).
			float screenPx = down * 1080f / (2f * tanV * depth);
			Assert.True(screenPx <= 1.5f, $"one painting pixel draws {screenPx:0.00} screen pixels at 1080p in the closest framing");
			// Not giants: everything the painting shows above its waterline (the trees' full height) draws at most 1.6 times a
			// fighter's height (the tallest body top) in that framing.
			float tallest = CameraConfig.Default.TallBodyTopMeters.Values.Append(CameraConfig.Default.ModelTopMeters).Max();
			float trees = waterline * down / depth, fighter = tallest / near;
			Assert.True(trees <= 1.6f * fighter, $"the far trees draw {trees / fighter:0.00} times a fighter's height");
		}
		finally { scene.QueueFree(); }
	}

	[Test]
	public static void Riverbank_SwampWater_EasesIntoThePaintedWater_KappaPartsFromIt(Node runner)
	{
		var scene = Load(runner, StageCatalog.RiverbankScene);
		try
		{
			var stage = scene.StageRoot;
			var backdrop = stage.GetNode<MeshInstance3D>("Backdrop");
			var wm = (ShaderMaterial)((PlaneMesh)stage.GetNode<MeshInstance3D>("Water").Mesh).Material;
			var fm = FarMaterial(backdrop);
			var (_, painted, _) = PaintedWater();
			Color far = wm.GetShaderParameter("far_color").AsColor(), near = wm.GetShaderParameter("near_color").AsColor();
			bool Close(Color a, Color b) => Mathf.Abs(a.R - b.R) <= 3f / 255f && Mathf.Abs(a.G - b.G) <= 3f / 255f && Mathf.Abs(a.B - b.B) <= 3f / 255f;
			Assert.True(Close(far, painted), $"the 3D water's far colour {far.ToHtml(false)} is the painted water's {painted.ToHtml(false)}: no seam");
			Assert.True(Mathf.IsEqualApprox(wm.GetShaderParameter("far_z").AsSingle(), backdrop.Position.Z), "and it reaches that colour at the painting's plane");
			float tint = fm.GetShaderParameter("water_tint_amount").AsSingle();
			Assert.True(tint == 0f || Close(fm.GetShaderParameter("water_tint").AsColor(), far), "any tint on the painted water pulls toward the same colour");
			// The Kappa's teal shell (#3E7F86) parts from the water behind it: near the bank the water is darker and greener.
			var teal = new Color("3e7f86");
			float Lum(Color x) => 0.299f * x.R + 0.587f * x.G + 0.114f * x.B;
			Assert.True(near.G > near.B && near.G > near.R, $"near water {near.ToHtml(false)} is green, not teal");
			Assert.True(Lum(near) <= 0.6f * Lum(teal) && Lum(near) < Lum(far), $"near water {near.ToHtml(false)} is darker than the Kappa's teal and the far water");
			Assert.True(wm.GetShaderParameter("ripple_mix").AsSingle() <= 0.3f, "the ripple lines stay faint");
		}
		finally { scene.QueueFree(); }
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
