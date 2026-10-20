using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-174 (Y1, Y2, F7 of the YOK-154 report): every yokai node fights with its OWN behaviour profiles and its own
/// move files, even while its body is the fallback Kitsune kit. The Oni fights with his five data normals, the
/// Kappa (no move files yet) with his profiles' generic actions on the Kitsune's normals and throw; neither carries
/// Foxfire. A profile naming a move its kit lacks fails at load. The harness report lists the fight mix.
/// </summary>
public static class PerYokaiProfileTests
{
	static string MapDir => ContentPaths.Data("map");
	static KitSources Sources => KitSources.At(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static HarnessContent? _content;
	static HarnessContent Content => _content ??= HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);

	static NodeContext Resolve(string yokai, string type = "duel", bool elder = false) =>
		NodeSceneTable.Default(MapDir).Resolve(new MapNodeData("n", 2, type, yokai, elder, yokai)).Context;

	[Test]
	public static void NodeScenes_EachYokaiPlaysAsItself_ElderAsItsYokai_ApprenticeAsTheFallback()
	{
		foreach (var (yokai, type, elder, fighter, playsAs) in new[]
		{
			("kitsune", "duel", false, "kitsune", "kitsune"), ("oni", "duel", false, "kitsune", "oni"), ("kappa", "duel", false, "kitsune", "kappa"),
			("elder-oni", "elder", true, "kitsune", "oni"), ("elder-kappa", "elder", true, "kitsune", "kappa"),
			("nine-tailed-kitsune", "elder", true, "kitsune", "kitsune"), ("apprentice", "duel", false, "kitsune", "kitsune"),
			("tanuki", "boss", false, "tanuki", "tanuki"),
		})
		{
			var ctx = Resolve(yokai, type, elder);
			Assert.Equal(fighter, ctx.Fighter, yokai + ": body (fighter)");
			Assert.Equal(playsAs, ctx.MovesAndProfilesOf, yokai + ": plays as");
		}
	}

	[Test]
	public static void NodeScenes_PlaysAs_OnlyOnAnOpponentWithoutAFighter()
	{
		string json = File.ReadAllText(Path.Combine(MapDir, NodeSceneTable.DefaultFile));
		foreach (var (from, to) in new[]
		{
			("\"kitsune\": { \"fighter\": \"kitsune\" }", "\"kitsune\": { \"fighter\": \"kitsune\", \"plays_as\": \"oni\" }"),
			("\"duel\": { ", "\"duel\": { \"plays_as\": \"oni\", "),
			("\"elder-oni\": { \"plays_as\": \"oni\" }", "\"elder-oni\": { \"plays_as\": 3 }"),
		})
		{
			Assert.True(json.Contains(from), "fixture text present: " + from);
			bool threw = false;
			try { NodeSceneTable.Parse(json.Replace(from, to)); } catch (FormatException) { threw = true; }
			Assert.True(threw, "rejected: " + to);
		}
		// Older callers that build a NodeContext without PlaysAs get the fighter.
		var ctx = Resolve("kitsune") with { PlaysAs = null };
		Assert.Equal("kitsune", ctx.MovesAndProfilesOf, "null PlaysAs = the fighter");
	}

	[Test]
	public static void KitLoadAs_OniHasHisOwnNormals_NoFoxfire_TheKappaStandsInOnTheKitsunesMoves()
	{
		var kitsune = Kit.Load("kitsune", Sources);
		Assert.True(Kit.LoadAs("kitsune", "kitsune", Sources).Moves.Select(m => m.Id).SequenceEqual(kitsune.Moves.Select(m => m.Id)), "same fighter = Kit.Load");

		var oni = Kit.LoadAs("oni", "kitsune", Sources);
		var ground = oni.Moves.Where(m => m.IsNormal && !m.Air).Select(m => m.Id).ToArray();
		Assert.Equal(5, ground.Length, "the Oni's five data normals: " + string.Join(",", ground));
		Assert.True(ground.All(id => id.StartsWith("oni-")), "only Oni ground normals: " + string.Join(",", ground));
		Assert.Equal(0, oni.Specials.Length, "the Oni has no special in data, and never the Kitsune's Foxfire");
		Assert.True(oni.Moves.Count(m => m.IsThrow) == 1 && oni.Moves.First(m => m.IsThrow).Id == kitsune.Moves.First(m => m.IsThrow).Id, "throw: the body's (C4)");
		Assert.Equal(150, FighterData.Load(Sources.FightersDir, "oni").PushWidth ?? 0, "data/fighters/oni.json push width");

		var kappa = Kit.LoadAs("kappa", "kitsune", Sources);
		Assert.True(kappa.Moves.Select(m => m.Id).SequenceEqual(kitsune.Moves.Select(m => m.Id)), "the Kappa has no move files: the Kitsune's normals, throw and air normals");
		Assert.Equal(0, kappa.Specials.Length, "the Kappa never carries Foxfire");
	}

	[Test]
	public static void AiLoadout_Missing_NamesEveryMoveTheKitLacks()
	{
		var p = BehaviourProfile.LoadFile(Path.Combine(ContentPaths.Data("profiles"), "kitsune-aggressive.json"));
		var oniOnKitsune = Kit.LoadAs("oni", "kitsune", Sources);
		var m = new Match(null, null, oniOnKitsune.Moves);
		oniOnKitsune.Equip(m.P2);
		var missing = AiLoadout.From(m.P2).Missing(p);
		Assert.True(missing.Contains("special:foxfire") && missing.Contains("normal:kitsune-light-punch"), "Kitsune moves missing on the Oni: " + string.Join(",", missing));

		var k = Kit.Load("kitsune", Sources);
		var mk = new Match(null, null, k.Moves);
		k.Equip(mk.P2);
		Assert.Equal(0, AiLoadout.From(mk.P2).Missing(p).Count, "the Kitsune has every move its profile names");
	}

	[Test]
	public static void HarnessLoad_EveryRealProfileResolves_ABadOneFailsLoudly()
	{
		Content.CheckProfiles(); // the real data passes (Load already ran it)
		var real = Content;
		var oniBad = BehaviourProfile.Parse(File.ReadAllText(Path.Combine(ContentPaths.Data("profiles"), "oni-aggressive.json"))
			.Replace("\"do\": \"throw\"", "\"do\": \"normal:kitsune-heavy-punch\""));
		Assert.True(oniBad.Behaviours.Any(b => b.Do.MoveId == "kitsune-heavy-punch"), "probe profile names a Kitsune normal");
		var bad = real with { Profiles = y => y == "oni" ? new[] { oniBad } : real.Profiles(y) };
		string? message = null;
		try { bad.CheckProfiles(); } catch (InvalidOperationException e) { message = e.Message; }
		Assert.True(message is not null && message.Contains("oni-aggressive") && message.Contains("normal:kitsune-heavy-punch"), "load fails naming the profile and move: " + message);
	}

	[Test]
	public static void Harness_OniAndKappaNodesFightWithTheirOwnProfiles_AndTheReportListsTheMix()
	{
		var bot = BotConfig.LoadDefault(ContentPaths.DataDir);
		var batch = RunHarness.PlayBatch(Content, bot, 1, 20);
		var fights = batch.Runs.SelectMany(r => r.Nodes).Where(n => n.Fight is not null).ToList();
		foreach (var n in fights)
		{
			string want = n.Yokai switch { "oni" or "elder-oni" => "oni", "kappa" or "elder-kappa" => "kappa", "tanuki" => "tanuki", _ => "kitsune" };
			Assert.Equal(want, n.PlaysAs, $"{n.Id}: plays as");
			Assert.True(n.Fight!.Profile.StartsWith(want), $"{n.Id}: profile {n.Fight.Profile} is {want}'s");
		}
		Assert.True(fights.Any(n => n.PlaysAs == "oni") && fights.Any(n => n.PlaysAs == "kappa"), "20 runs reach Oni and Kappa nodes");
		string json = HarnessReport.ToJson(batch);
		var doc = System.Text.Json.JsonDocument.Parse(json);
		var mix = doc.RootElement.GetProperty("summary").GetProperty("fight_mix");
		Assert.Equal(fights.Count, mix.EnumerateArray().Sum(e => e.GetProperty("fights").GetInt32()), "fight_mix counts every fight once");
		Assert.True(mix.EnumerateArray().Any(e => e.GetProperty("profile").GetString() == "oni-aggressive" && e.GetProperty("fighter").GetString() == "kitsune"), "the Oni on the Kitsune body is listed");
		Assert.Equal(json, HarnessReport.ToJson(RunHarness.PlayBatch(Content, bot, 1, 20)), "deterministic: same seed, same bytes");
	}
}
