using System.IO;
using Godot;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-173, T6 (designer 2026-10-20): the rest-stop forecast's expected cost is the copied move's damage per hit at
/// Ryo's level, from data, never past Lv 2 (T4: the copy never gets the evolution), plain not EX, and per hit for a
/// multi-hit move. Always a figure: there is no "not enough fights" state.
/// </summary>
public static class ForecastCostTests
{
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static AbilityPool Pool() => AbilityPool.LoadFixtures(RepoRoot);

	/// <summary>
	/// The Spirit Wave fixture's Lv 1 frame data (60 damage, projectile hits 1), a Lv 2 tuning and a Lv 3 evolution.
	/// </summary>
	static SpecialData Make(string lv2Tuning, string lv3Change)
	{
		string fd = Pool().Special("test-spirit-wave")!.Level1Json;
		string levels = "\"1\":{\"frame_data\":" + fd + "},\"2\":{\"tuning\":" + lv2Tuning + "},"
			+ "\"3\":{\"evolution\":{\"name\":\"Probe Evolution\",\"changes\":[" + lv3Change + "]}}";
		return SpecialLoader.Parse("{\"kind\":\"special\",\"id\":\"cost-probe\",\"input\":{\"kata\":{\"slot\":\"C\"}},"
			+ "\"ex\":{\"meter_cost\":100,\"changes\":[{\"target\":\"damage\",\"op\":\"add\",\"value\":500}]},\"levels\":{" + levels + "}}");
	}

	static SpecialData DamageLadder() => Make(
		"{\"target\":\"damage\",\"op\":\"add\",\"value\":10}",
		"{\"target\":\"damage\",\"op\":\"add\",\"value\":30}");

	static int Cost(SpecialData d, int ryoLevel) => RestStopScreenForecast.ExpectedCost(new CopyTarget(d.Slot, d, ryoLevel));

	[Test]
	public static void Cost_Lv1_IsTheLv1DamagePerHit()
	{
		var d = DamageLadder();
		Assert.Equal(60, d.Build(1, false, null, null).Damage, "fixture: Lv 1 deals 60");
		Assert.Equal(60, Cost(d, 1), "Ryo at Lv 1: the copy's Lv 1 damage per hit");
	}

	[Test]
	public static void Cost_Lv2_ReadsTheLv2Tuning()
	{
		var d = DamageLadder();
		Assert.Equal(70, Cost(d, 2), "Ryo at Lv 2: the Lv 2 tuning (+10) is in the figure");
	}

	[Test]
	public static void Cost_Lv3_IsCappedAtLv2_TheEvolutionIsNeverCopied()
	{
		var d = DamageLadder();
		Assert.Equal(100, d.Build(3, false, null, null).Damage, "fixture: Ryo's own evolved Lv 3 deals 100");
		Assert.Equal(70, Cost(d, 3), "T4: the copy never passes Lv 2, so the forecast reads the Lv 2 damage, not Ryo's 100");
		Assert.Equal(Cost(d, 2), Cost(d, 3), "Lv 3 forecasts the same as Lv 2");
	}

	[Test]
	public static void Cost_MultiHitMove_IsPerHit_NotTheSum()
	{
		var d = Make(
			"{\"target\":\"projectile.hits\",\"op\":\"set\",\"value\":3}",
			"{\"target\":\"damage\",\"op\":\"add\",\"value\":30}");
		var lv2 = d.Build(2, false, null, null);
		Assert.True(lv2.Projectile is { Hits: 3 }, "fixture: the Lv 2 move hits three times");
		Assert.Equal(60, Cost(d, 2), "a three-hit move forecasts one hit's damage (60), not 180");
	}

	[Test]
	public static void Cost_IsThePlainMove_NotEx()
	{
		var d = DamageLadder();
		Assert.Equal(570, d.Build(2, true, null, null).Damage, "fixture: the EX version deals +500");
		Assert.Equal(70, Cost(d, 2), "the forecast reads the plain move");
	}

	[Test]
	public static void Cost_EveryFixtureSpecial_EveryLevel_IsTheCopysOwnBuild()
	{
		var pool = Pool();
		foreach (var d in pool.Specials)
			for (int lv = 1; lv <= 3; lv++)
			{
				var t = new CopyTarget(d.Slot, d, lv);
				int want = d.Build(System.Math.Min(lv, CopyRule.HighestUnevolvedLevel), false, null, null).Damage;
				Assert.Equal(want, RestStopScreenForecast.ExpectedCost(t), $"{d.Id} at Ryo Lv {lv}: the copy's damage per hit from data");
				Assert.Equal(t.Build().Damage, RestStopScreenForecast.ExpectedCost(t), $"{d.Id} Lv {lv}: the same move the Tanuki fights with");
			}
	}
}
