using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-173. Expected values from T4 (copy at Ryo's level, capped Lv 2, no modifier) and T6 (cost = damage per hit from data, always shown).</summary>
public static class ForecastCostProbes
{
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static AbilityPool Pool() => AbilityPool.LoadFixtures(RepoRoot);
	static KitSources Fixtures => KitSources.Repo(RepoRoot).FixturesOnly();

	// T4+T6: the figure is the damage of the move the Tanuki's fighter really holds in a fight kit, at Lv 1-3, every fixture special.
	[Test]
	public static void Cost_EqualsTheMoveTheTanukiFighterEquips_AllLevels()
	{
		var ryo = Kit.Load(Kit.Ryo, Fixtures);
		foreach (var d in Pool().Specials)
			for (int lv = 1; lv <= 3; lv++)
			{
				var t = new CopyTarget(d.Slot, d, lv);
				var kit = TanukiKit.Build(ryo, t);
				var m = new Match(null, ryo.Moves, kit.Moves);
				kit.Equip(m.P2);
				var eq = m.P2.Specials[(int)d.Slot]!;
				Assert.Equal(eq.Move.Damage, RestStopScreenForecast.ExpectedCost(t), $"{d.Id} Lv {lv}: forecast vs the Tanuki's equipped move");
				Assert.True(eq.Modifier is null, "no modifier on the copy");
			}
	}
}
