using System;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-150 breaker probes: tea house season beats and fades, the stage lookup, refused stage paths.</summary>
public static class FightStagesBreakerProbes
{
	static float Sum(Vector4 w) => w.X + w.Y + w.Z + w.W;
	static float MaxDelta(Vector4 a, Vector4 b) => Mathf.Max(Mathf.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y)), Mathf.Max(Mathf.Abs(a.Z - b.Z), Mathf.Abs(a.W - b.W)));

	// Rule: designer 2026-11-09 (TeaHouseStage): health strictly below 75/50/25 percent of max brings summer/autumn/winter.
	[Test]
	public static void SeasonForHealth_ExactBoundariesAndOddMaxima()
	{
		foreach (int max in new[] { 1, 3, 4, 7, 100, 999, 1000, 1001 })
			for (int h = -2; h <= max + 2; h++)
			{
				int want = 0;
				foreach (int p in new[] { 75, 50, 25 }) if ((long)h * 100 < (long)p * max) want++;
				Assert.Equal(want, TeaHouseStage.SeasonForHealth(h, max), $"health {h} of {max}");
			}
		Assert.Equal(0, TeaHouseStage.SeasonForHealth(0, 0), "no maximum = spring");
		Assert.Equal(0, TeaHouseStage.SeasonForHealth(5000, 1000), "above max = spring");
		Assert.Equal(3, TeaHouseStage.SeasonForHealth(0, 1000), "health 0 = winter");
		Assert.Equal(3, TeaHouseStage.SeasonForHealth(-50, 1000), "negative health = winter");
		Assert.Equal(0, TeaHouseStage.SeasonForHealth(750, 1000), "exactly 75% is still spring");
		Assert.Equal(1, TeaHouseStage.SeasonForHealth(749, 1000), "749/1000 is summer");
		Assert.Equal(3, TeaHouseStage.SeasonForHealth(249, 1000), "249/1000 is winter");
		Assert.Equal(2, TeaHouseStage.SeasonForHealth(250, 1000), "exactly 25% is still autumn");
		Assert.Equal(0, TeaHouseStage.SeasonForHealth(int.MaxValue, int.MaxValue), "no overflow at int.MaxValue");
	}

	// Rule: seasons never go back within a fight; shares sum to 1; no share moves more than 1/30 a tick (SeasonBeats doc).
	[Test]
	public static void Beats_AdversarialHealthWalk_SumsToOne_RateLimited_Monotonic()
	{
		foreach (int max in new[] { 1, 7, 999, 1000, 1001 })
			foreach (uint seed in new uint[] { 1, 2, 3 })
			{
				var rng = new SimRng(seed);
				var b = new SeasonBeats();
				Vector4 prev = SeasonBeats.Only(0); int prevSeason = 0;
				int health = max;
				for (int tick = 0; tick < 1500; tick++)
				{
					int r = rng.Next(10);
					health = r switch { 0 => max * 2, 1 => 0, 2 => health + max / 3, 3 => health - max / 2, 4 => -5, _ => health - rng.Next(3) };
					b.Observe(new StageClock(1, tick, health, max));
					var w = b.Weights(tick);
					Assert.True(Mathf.Abs(Sum(w) - 1f) < 1e-4f, $"max {max} tick {tick}: sum {Sum(w)}");
					Assert.True(MaxDelta(prev, w) <= 1f / 30f + 1e-4f, $"max {max} tick {tick}: moved {MaxDelta(prev, w)}");
					Assert.True(w.X >= -1e-5f && w.Y >= -1e-5f && w.Z >= -1e-5f && w.W >= -1e-5f, $"max {max} tick {tick}: negative share");
					Assert.True(b.Season >= prevSeason, "season went back");
					prev = w; prevSeason = b.Season;
				}
			}
	}

	// Rule: the season is the lowest health seen; the same end state reached by different paths gives the same settled picture.
	[Test]
	public static void Beats_SamePlaceThreeWays_SameSettledPicture_HealingDoesNotRewind()
	{
		var direct = new SeasonBeats(); var steps = new SeasonBeats(); var healed = new SeasonBeats();
		foreach (var s in new[] { direct, steps, healed }) s.Observe(new StageClock(1, 10, 100, 100));
		direct.Observe(new StageClock(1, 11, 20, 100));
		foreach (var (t, h) in new[] { (11, 70), (12, 45), (13, 20) }) steps.Observe(new StageClock(1, t, h, 100));
		foreach (var (t, h) in new[] { (11, 20), (12, 100), (13, 100) }) healed.Observe(new StageClock(1, t, h, 100));
		Assert.Equal(3, direct.Season, "direct winter"); Assert.Equal(3, steps.Season, "stepwise winter"); Assert.Equal(3, healed.Season, "healing does not bring spring back");
		Assert.True(MaxDelta(direct.Weights(400), SeasonBeats.Only(3)) < 1e-5f, "direct: all winter");
		Assert.True(MaxDelta(steps.Weights(400), SeasonBeats.Only(3)) < 1e-5f, "steps: all winter");
		Assert.True(MaxDelta(healed.Weights(400), SeasonBeats.Only(3)) < 1e-5f, "healed: all winter");
	}

	// Rule: a new round (new Match, Restart, tick going back) starts at spring, even mid-fade.
	[Test]
	public static void Beats_NewRoundMidFade_StartsAtSpring_NoLeakedFade()
	{
		var b = new SeasonBeats();
		b.Observe(new StageClock(1, 0, 100, 100));
		b.Observe(new StageClock(1, 100, 10, 100));
		b.Observe(new StageClock(1, 110, 10, 100)); // mid fade
		b.Observe(new StageClock(2, 111, 100, 100));
		Assert.Equal(0, b.Season, "spring");
		Assert.True(b.ChangedTick is null, "no fade left");
		Assert.True(MaxDelta(b.Weights(111), SeasonBeats.Only(0)) < 1e-6f, "pure spring on the first tick of the new round");
		b.Observe(new StageClock(2, 112, 60, 100));
		Assert.True(MaxDelta(b.Weights(112), SeasonBeats.Only(0)) < 1e-6f, "fade starts from spring, not the old mix");
		Assert.True(MaxDelta(b.Weights(142), SeasonBeats.Only(1)) < 1e-5f, "30 ticks later summer");
	}

	// Rule: observing a tick twice changes nothing; two instances fed the same ticks agree (determinism).
	[Test]
	public static void Beats_ReObservingATick_IsIdempotent_AndInstancesAgree()
	{
		var a = new SeasonBeats(); var c = new SeasonBeats();
		int[] hp = { 100, 100, 80, 70, 70, 49, 49, 24, 0, 100 };
		for (int t = 0; t < hp.Length; t++)
		{
			var clk = new StageClock(1, t * 7, hp[t], 100);
			a.Observe(clk); a.Observe(clk); c.Observe(clk);
			Assert.True(MaxDelta(a.Weights(t * 7), c.Weights(t * 7)) == 0f, $"step {t}");
			Assert.Equal(a.ChangedTick, c.ChangedTick, $"changed tick {t}");
		}
	}

	// Rule: a missing season painting must not crash or break the shares mid-fade (SeasonScreens doc).
	[Test]
	public static void SeasonScreens_MissingPaintingMidFade_KeepsSharesAndDoesNotThrow()
	{
		SeasonScreens.SeasonDirOverride = "res://assets/generated/stage/missing/seasons/";
		var s = new SeasonScreens();
		try
		{
			for (int t = 0; t < 120; t++)
			{
				int hp = t < 20 ? 100 : t < 40 ? 60 : t < 60 ? 40 : 10;
				s.Present(new StageClock(1, t, hp, 100));
				Assert.True(Mathf.Abs(Sum(s.Weights) - 1f) < 1e-4f, $"tick {t}");
			}
			Assert.Equal(4, s.Textures.Length, "four slots");
			foreach (var tx in s.Textures) Assert.True(tx is null, "missing painting is null");
		}
		finally { SeasonScreens.SeasonDirOverride = null; s.Free(); }
	}

	const string Table = """
	{ "kind": "node_scenes", "id": "t", "schema_version": 1, "default_scene": "res://scenes/fight.tscn", "fallback_fighter": "kitsune",
	  "default_stage": "res://scenes/stage/bamboo_grove.tscn",
	  "tiers": { "yokai": { "reaction_frames": 22 } },
	  "types": { "duel": { "tier": "yokai" } },
	  "opponents": { "kitsune": { "fighter": "kitsune" }, "elder-oni": { "plays_as": "oni" }, "oni": { "fighter": "oni" } },
	  "opponent_stages": { "oni": "res://scenes/stage/shrine_gate.tscn", "elder-oni": "res://scenes/stage/riverbank.tscn" } }
	""";

	// Rule: NodeSceneTable lookup order node -> yokai -> plays-as: the yokai's own entry beats the plays-as entry.
	[Test]
	public static void Lookup_OwnYokaiEntryBeatsItsPlaysAs_AndUnknownYokaiFallsToDefault()
	{
		var t = NodeSceneTable.Parse(Table);
		string? Stage(string yokai) => t.Resolve(new MapNodeData("n", 1, "duel", yokai)).Context.Stage;
		Assert.Equal(StageCatalog.RiverbankScene, Stage("elder-oni"), "elder-oni's own entry, not oni's");
		Assert.Equal(StageCatalog.ShrineGateScene, Stage("oni"), "oni");
		Assert.Equal("res://scenes/stage/bamboo_grove.tscn", Stage("never-heard-of-it"), "an unknown yokai gets default_stage");
	}

	// Rule: a stage path must be a scene under res://scenes/stage/ (StagePattern); traversal and odd forms are refused.
	[Test]
	public static void Lookup_BadStagePaths_AreRefused()
	{
		foreach (var bad in new[] { "res://scenes/stage/../fight.tscn", "res://scenes/stage//x.tscn", "res://scenes/stage/x.tscn ", "res://scenes/stage/.tscn",
			"res://scenes/stage/a/../../b.tscn", "res://scenes/stage/x.TSCN", "user://scenes/stage/x.tscn", "res://scenes/stage/x.tres", "", "res://scenes/stage/%2e%2e/x.tscn" })
		{
			bool threw = false;
			try { NodeSceneTable.Parse(Table.Replace("res://scenes/stage/shrine_gate.tscn", bad)); } catch (FormatException) { threw = true; }
			Assert.True(threw, $"refused: '{bad}'");
		}
		bool nullThrew = false;
		try { NodeSceneTable.Parse(Table.Replace("\"res://scenes/stage/shrine_gate.tscn\"", "null")); } catch (FormatException) { nullThrew = true; }
		Assert.True(nullThrew, "null stage refused");
		bool unknown = false;
		try { NodeSceneTable.Parse(Table.Replace("\"oni\": \"res://scenes/stage/shrine_gate", "\"onii\": \"res://scenes/stage/shrine_gate")); } catch (FormatException) { unknown = true; }
		Assert.True(unknown, "an unknown yokai key is refused");
	}

	// Rule: StageCatalog lookup of an unknown scene is null and never blocks a fight.
	[Test]
	public static void Catalog_UnknownScene_IsNullAndAssetsPresent()
	{
		Assert.True(StageCatalog.For("res://scenes/stage/nope.tscn") is null, "unknown");
		Assert.True(StageCatalog.AssetsPresent("res://scenes/stage/nope.tscn"), "unknown scene has no required assets");
		Assert.True(StageCatalog.For("") is null, "empty");
	}
}
