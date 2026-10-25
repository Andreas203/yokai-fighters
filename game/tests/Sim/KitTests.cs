using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-56: each fighter loads its own kit (by the file's <c>source</c>), with TEST FIXTURE fallback per fighter
/// and per move kind. Built in a temp moves folder from fixture copies, so it never depends on data/.
/// </summary>
public static class KitTests
{
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static string Fx(string sub) => ProjectSettings.GlobalizePath("res://tests/fixtures/" + sub);

	/// <summary>A temp data/moves with one Kitsune light punch and a Kitsune Foxfire (renamed fixture copies).</summary>
	static void WithKitsuneData(Action<KitSources> body)
	{
		string tmp = Path.Combine(Path.GetTempPath(), "yok56-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tmp);
		try
		{
			string lp = File.ReadAllText(Path.Combine(Fx("ryo-normals"), "test-ryo-light-punch.json"))
				.Replace("test-ryo-light-punch", "kitsune-lp").Replace("\"source\": \"ryo\"", "\"source\": \"kitsune\"");
			File.WriteAllText(Path.Combine(tmp, "kitsune-lp.json"), lp);
			string fox = File.ReadAllText(Path.Combine(Fx("rewards"), "test-foxfire.json")).Replace("test-foxfire", "foxfire");
			File.WriteAllText(Path.Combine(tmp, "foxfire.json"), fox);
			body(KitSources.Repo(RepoRoot) with { MovesDir = tmp });
		}
		finally { Directory.Delete(tmp, true); }
	}

	[Test]
	public static void RyoIgnoresKitsuneFilesAndFallsBackPerKind()
	{
		WithKitsuneData(src =>
		{
			var ryo = Kit.Load(Kit.Ryo, src);
			Assert.True(ryo.Moves.All(m => m.Source == "ryo"), "no Kitsune move in Ryo's kit");
			Assert.Equal(6, ryo.Moves.Count(m => m.IsNormal && !m.Air), "Ryo keeps his six fixture normals (C8)");
			Assert.Equal(1, ryo.Moves.Count(m => m.IsThrow), "and his fixture throw (C4)");
			Assert.Equal(2, ryo.Moves.Count(m => m.Air), "and his fixture jump-ins (E11)");
			Assert.Equal("test-rising-talisman,test-spirit-wave", string.Join(",", ryo.Specials.Select(s => s.Id)), "starters only, no Foxfire (A1)");
		});
	}

	[Test]
	public static void KitsuneLoadsHerOwnFilesAndFallsBackPerKind()
	{
		WithKitsuneData(src =>
		{
			var kit = Kit.Load("kitsune", src);
			Assert.Equal("kitsune-lp", string.Join(",", kit.Moves.Where(m => m.IsNormal && !m.Air).Select(m => m.Id)), "her ground normals: only her real one (no fixture mix)");
			Assert.Equal(1, kit.Moves.Count(m => m.IsThrow), "no throw of hers yet: the fixture throw stands in");
			Assert.True(kit.Notes.Any(n => n.Contains("throw") && n.Contains("stand in")), "the stand-in is logged");
			Assert.Equal("foxfire", string.Join(",", kit.Specials.Select(s => s.Id)), "her own Foxfire");
			Assert.Equal(SpecialSlot.C, kit.Specials[0].Slot, "Foxfire in slot C");
		});
	}

	[Test]
	public static void KitsuneWithoutDataGetsHerFixtureSpecialNotRyos()
	{
		var kit = Kit.Load("kitsune", KitSources.Repo(RepoRoot).FixturesOnly());
		Assert.Equal("test-foxfire", string.Join(",", kit.Specials.Select(s => s.Id)), "her TEST FIXTURE Foxfire, never Ryo's starters");
		Assert.Equal(6, kit.Moves.Count(m => m.IsNormal && !m.Air), "Ryo's fixture normals stand in");
		Assert.Equal(0, Kit.Load("oni", KitSources.Repo(RepoRoot).FixturesOnly()).Specials.Length, "a yokai with no fixture special has none");
	}

	[Test]
	public static void PoolKeepsDraftablesOfEverySourceAndFallsBackPerYokai()
	{
		WithKitsuneData(src =>
		{
			var pool = AbilityPool.Load(src, "");
			Assert.Equal(2, pool.Starters.Count(), "Ryo's starters from his kit (fixture fallback, A1)");
			Assert.True(pool.Special("foxfire") != null && pool.Special("test-foxfire") == null, "real Foxfire replaces the Kitsune fixture");
			var run = RunState.NewRun(pool);
			for (uint seed = 1; seed <= 10; seed++)
			{
				var d = RewardDraft.Build(run, pool, "kitsune", seed);
				Assert.True(d.Offer.Cards.Any(c => c.Kind == YokaiFighters.Sim.CardKind.NewSpecial && c.AbilityId == "foxfire"), $"seed {seed}: NEW Foxfire offered");
			}
		});
	}

	[Test]
	public static void SceneGivesEachFighterItsKit()
	{
		var saved = FightScene.Sources;
		try
		{
			WithKitsuneData(src =>
			{
				FightScene.Sources = src;
				var m = FightScene.NewMatch();
				Assert.True(m.P1.Moves.All(mv => mv.Source == "ryo"), "P1 = Ryo's kit");
				Assert.True(m.P1.Specials[(int)SpecialSlot.A] != null && m.P1.Specials[(int)SpecialSlot.B] != null && m.P1.Specials[(int)SpecialSlot.C] == null, "Ryo: starters, no Foxfire");
				Assert.True(m.P2.Moves.Any(mv => mv.Id == "kitsune-lp"), "P2 (AI) = the Kitsune's kit");
				Assert.Equal("foxfire", m.P2.Specials[(int)SpecialSlot.C]?.Data.Id ?? "", "P2 has Foxfire in C");
				Assert.True(m.P2.Specials[(int)SpecialSlot.A] == null, "P2 no longer borrows Ryo's starters");
			});
		}
		finally { FightScene.Sources = saved; }
	}
}
