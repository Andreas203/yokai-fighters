using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-226 Test Breaker probes: a yokai's rare special is its elder's (A9), at yokai nodes and elder nodes.</summary>
public static class HeldBackRareBreakerProbes
{
	// A9: OpponentKit holds back every rare at a non-elder node and a node-less fight, and keeps all of them at an elder node.
	[Test]
	public static void OpponentKit_RaresOnlyAtAnElderNode_ForEveryYokai()
	{
		var sources = YokaiFighters.Fight.FightScene.Sources;
		int raresSeen = 0;
		foreach (string yokai in new[] { "kitsune", "oni", "kappa" })
		{
			var all = Kit.LoadAs(yokai, yokai, sources).Specials;
			var commons = NodeFight.OpponentKit(sources, null, yokai).Specials;
			Assert.True(commons.All(s => s.Rarity != "rare"), $"{yokai}: a bare fight holds no rare");
			Assert.Equal(all.Count(s => s.Rarity != "rare"), commons.Count(), $"{yokai}: commons are all kept");
			raresSeen += all.Count(s => s.Rarity == "rare");
			var held = NodeFight.HeldBack(sources, null, yokai);
			Assert.Equal(all.Count(s => s.Rarity == "rare"), held.Count(), $"{yokai}: HeldBack names exactly the rares");
		}
		Assert.True(raresSeen >= 3, $"setup: Fox Mirage, Oni Quake and Shell Spin are rares ({raresSeen})");
	}
}

public static class HeldBackRarePlayProbes
{
	// A9: at a yokai node (not an elder's) the opponent's AI, on every shipped temperament profile, never starts a
	// rare special across whole fights; at an elder node the same AI may.
	[Test]
	public static void YokaiNodeAi_NeverStartsARare_OnAnyShippedProfile()
	{
		var sources = YokaiFighters.Fight.FightScene.Sources;
		int rareStartsAtElder = 0;
		foreach (string yokai in new[] { "kitsune", "oni", "kappa" })
		foreach (string temper in new[] { "aggressive", "patient" })
		foreach (bool elder in new[] { false, true })
		foreach (uint seed in new[] { 1u, 2u, 3u })
		{
			var profile = BehaviourProfile.LoadFile(ContentPaths.Data("profiles") + $"/{yokai}-{temper}.json");
			var ryo = Kit.Load(Kit.Ryo, sources);
			var full = Kit.LoadAs(yokai, yokai, sources);
			var rares = full.Specials.Where(s => s.Rarity == "rare").Select(s => s.Id).ToHashSet();
			var foe = elder ? full : full.CommonsOnly();
			var m = new Match(null, ryo.Moves, foe.Moves);
			ryo.Equip(m.P1); foe.Equip(m.P2);
			var held = elder ? Array.Empty<string>() : rares.ToArray();
			var ai = new ProfileAi(profile, 1, AiLoadout.From(m.P2, held), seed, null, AiFightTier.Standard, 0, null, true, true);
			var rng = new SimRng(seed + 40);
			InputBits h = InputBits.None; int left = 0;
			for (int t = 0; t < 5000 && m.Phase != MatchPhase.Over; t++)
			{
				m.Step(AiHarness.RandomInput(rng, ref h, ref left), ai.Next(AiView.Capture(m)));
				if (m.P2.State == FighterState.Attack && m.P2.CurrentMove is { } mv && rares.Contains(mv.Id))
				{
					Assert.True(elder, $"{yokai}-{temper} seed {seed}: a yokai node started the rare {mv.Id}");
					rareStartsAtElder++;
				}
			}
		}
		GD.Print($"probe: rare starts at elder nodes: {rareStartsAtElder}");
	}
}
