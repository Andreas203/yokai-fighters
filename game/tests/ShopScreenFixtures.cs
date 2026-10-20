using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-104: sessions standing at the row-4 merchant for the shop screen tests and captures. The default pool's every
/// non-starter ability is the Kitsune's (bound), so the real shelves are empty; these fixture pools add made-up
/// Kappa and Oni commons (clones of the fixture Foxfire / modifiers) so the shelves have stock. Fixtures only:
/// nothing here writes a save file.
/// </summary>
public static class ShopScreenFixtures
{
	public static readonly string[] Walk = { "r1-apprentice", "r2-kitsune", "r3-oni" };
	public const string Merchant4 = "r4-merchant";
	static string MapDir => ContentPaths.Data("map");

	static SpecialData Clone(string id, string source, string rarity, string slot, string name, string plain)
	{
		var node = JsonNode.Parse(File.ReadAllText(Path.Combine(ContentPaths.FixturesDir, "rewards", "test-foxfire.json")))!.AsObject();
		node["id"] = id; node["name"] = name; node["source"] = source; node["rarity"] = rarity; node["locked"] = false;
		node["input"]!["kata"]!["slot"] = slot; node["input"]!["kihon"]!["slot"] = slot;
		node["card"]!["plain"] = plain;
		return SpecialLoader.Parse(node.ToJsonString(), id);
	}

	static ModifierData CloneMod(string from, string id, string source, string name, string plain)
	{
		var node = JsonNode.Parse(File.ReadAllText(Path.Combine(ContentPaths.FixturesDir, "rewards", from + ".json")))!.AsObject();
		node["id"] = id; node["name"] = name; node["source"] = source; node["rarity"] = "common"; node["locked"] = false;
		node["card"]!["plain"] = plain;
		return ModifierLoader.Parse(node.ToJsonString(), id);
	}

	/// <summary>
	/// The fixture starters are named "TEST FIXTURE: Spirit Wave (A-section numbers; ...)", a note to developers that would show
	/// on the shop's slot rows and in the captures. Same file, same data, the name cut down to what a player would read.
	/// </summary>
	static SpecialData Displayable(SpecialData d)
	{
		if (!d.Name.StartsWith("TEST FIXTURE", System.StringComparison.Ordinal)) return d;
		string name = System.Text.RegularExpressions.Regex.Replace(d.Name, @"^TEST FIXTURE:\s*|\s*\(.*$", "");
		foreach (string dir in new[] { "specials", "rewards" })
		{
			string path = Path.Combine(ContentPaths.FixturesDir, dir, d.Id + ".json");
			if (!File.Exists(path)) continue;
			var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
			node["name"] = name;
			return SpecialLoader.Parse(node.ToJsonString(), d.Id);
		}
		return d;
	}

	/// <summary>Fixture pool plus Kappa specials (C and D) and Kappa modifiers (any special; needs a projectile).</summary>
	public static AbilityPool StockedPool(bool modifiers = true, bool specials = true)
	{
		var fx = FightScene.LoadAbilityPool();
		var sp = specials ? new[]
		{
			Clone("test-river-grab", "kappa", "common", "D", "River Grab", "Command grab. Hard to block, slow to start."),
			Clone("test-slippery-skin", "kappa", "common", "C", "Slippery Skin", "Can't be thrown while the move starts up."),
		} : System.Array.Empty<SpecialData>();
		var md = modifiers ? new[]
		{
			CloneMod("test-fox-patience", "test-kappa-patience", "kappa", "Kappa's Patience", "Blocking charges your meter 25% faster."),
			CloneMod("test-will-o-wisp", "test-kappa-wisp", "kappa", "Pond Lantern", "Your projectiles fly 30% faster."),
		} : System.Array.Empty<ModifierData>();
		// YOK-103: data/modifiers/ also holds the Oni's and Kappa's commons now; keep only the Kitsune's (bound on the
		// walk) so the stock is exactly the clones above and an unstocked pool still has empty shelves.
		return new AbilityPool(fx.Specials.Select(Displayable).Concat(sp), fx.Modifiers.Where(m => m.Source == "kitsune").Concat(md));
	}

	static readonly string[] SpecialNames = { "Reed Whistle", "Pond Drum", "Lotus Slap", "Mud Splash", "Bamboo Spear" };
	static readonly string[] ModifierNames = { "Cold Water", "Thick Shell", "Long Reach", "Silt Cloak", "Heron Step" };

	/// <summary>The stocked pool plus extra Kappa specials and modifiers (made-up names), for a shelf with more items than the cabinet shows.</summary>
	public static AbilityPool WidePool(int extraSpecials, int extraModifiers)
	{
		var basePool = StockedPool();
		var sp = Enumerable.Range(0, extraSpecials).Select(i => Clone($"test-wide-sp{i}", "kappa", "common", i % 2 == 0 ? "C" : "D", SpecialNames[i % SpecialNames.Length], $"Wide special number {i} does one plain thing."));
		var md = Enumerable.Range(0, extraModifiers).Select(i => CloneMod("test-fox-patience", $"test-wide-md{i}", "kappa", ModifierNames[i % ModifierNames.Length], $"Wide modifier number {i} changes one plain thing."));
		return new AbilityPool(basePool.Specials.Concat(sp), basePool.Modifiers.Concat(md));
	}

	/// <summary>A run at the row-4 merchant (entered) holding <paramref name="slots"/> and <paramref name="coins"/>.</summary>
	public static RunSession At(AbilityPool pool, int coins, params (SpecialSlot Slot, string Id, int Level)[] slots)
	{
		var graph = FileMapSource.Default(MapDir).Load();
		var run = RunState.Restore(640, slots.Select(x => (x.Slot, new OwnedSpecial(pool.Special(x.Id)!, x.Level, null))));
		var order = new int[5];
		int rank = 0;
		foreach (var x in slots.OrderBy(x => x.Slot)) if (x.Level > 1) order[(int)x.Slot] = ++rank;
		run.RestoreLevelledOrder(order[1], order[2], order[3], order[4]);
		run.RestoreCoins(coins);
		run.SetMap(RunMap.Restore(graph, new RunMapSnapshot(graph.Id, Walk, Walk[^1])));
		var s = new RunSession(run, pool, NodeSceneTable.Default(MapDir), 7, MerchantChoice.None);
		Assert.True(s.Enter(Merchant4).Ok, "entered the merchant");
		return s;
	}

	public static readonly (SpecialSlot, string, int)[] Starters = { (SpecialSlot.A, "test-spirit-wave", 1), (SpecialSlot.B, "test-rising-talisman", 1) };
}
