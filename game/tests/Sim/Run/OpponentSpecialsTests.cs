using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-226 / YOK-234: which specials each fight node's opponent carries, over every fight node of the default map and
/// every special in data/moves. An elder carries all of its yokai's specials (its rare one too: the elder profiles
/// name it); a yokai-tier or apprentice opponent carries its commons only (A9: rares belong to the elders). The
/// Tanuki (copy_of_ryo) is TanukiKitTests'. Profiles reuse temperaments, so <c>AiLoadout</c> skips a special the
/// fighter doesn't carry.
/// </summary>
public static class OpponentSpecialsTests
{
	static string Root => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");

	[Test]
	public static void ElderNodesCarryTheRareSpecial_OtherNodesOnlyTheCommons()
	{
		var src = KitSources.Repo(Root);
		var specials = SpecialLoader.LoadDirectory(Path.Combine(Root, "data", "moves"));
		var graph = MapLoader.LoadFile(Path.Combine(Root, "data", "map", "default-map.json"));
		var scenes = NodeSceneTable.Default(Path.Combine(Root, "data", "map"));
		int elders = 0, duels = 0;
		foreach (var node in graph.Nodes)
		{
			var ctx = scenes.Resolve(node).Context;
			if (ctx.Yokai is null || ctx.Kit == TanukiKit.KitId || graph.TypeOf(node)?.Fight != true) continue;
			var kit = NodeFight.OpponentKit(src, ctx, ctx.Fighter);
			string yokai = ctx.MovesAndProfilesOf;
			var own = specials.Where(s => s.Source == yokai).ToArray();
			if (own.Length == 0) continue;
			string at = $"{node.Id} ({yokai}, elder={ctx.Elder})";
			foreach (var sp in own)
			{
				bool carried = kit.Specials.Any(s => s.Id == sp.Id);
				bool expected = ctx.Elder || sp.Rarity != "rare";
				Assert.Equal(expected, carried, $"{at}: carries {sp.Id} ({sp.Rarity})");
			}
			Assert.True(kit.Specials.All(s => s.Source == yokai), $"{at}: only its own specials (A8)");
			if (ctx.Elder) elders++; else duels++;
		}
		Assert.True(elders > 0 && duels > 0, $"the map has elder ({elders}) and duel ({duels}) nodes with specials");
	}

	[Test]
	public static void EveryProfileSpecialIsARealSpecialOfThatYokai()
	{
		var specials = SpecialLoader.LoadDirectory(Path.Combine(Root, "data", "moves")).ToDictionary(s => s.Id);
		foreach (string path in Directory.GetFiles(Path.Combine(Root, "data", "profiles"), "*.json"))
		{
			string text = File.ReadAllText(path);
			string yokai = Path.GetFileNameWithoutExtension(path).Split('-')[0];
			foreach (System.Text.RegularExpressions.Match mt in System.Text.RegularExpressions.Regex.Matches(text, "\"special:([a-z0-9-]+)\""))
			{
				string id = mt.Groups[1].Value;
				if (id == "copied") continue; // the Tanuki's copied slot (T0), not a move id
				Assert.True(specials.ContainsKey(id), $"{Path.GetFileName(path)}: special:{id} is in data/moves");
				if (yokai is "kitsune" or "oni" or "kappa")
					Assert.Equal(yokai, specials[id].Source, $"{Path.GetFileName(path)}: special:{id} is the {yokai}'s own");
			}
		}
	}
}
