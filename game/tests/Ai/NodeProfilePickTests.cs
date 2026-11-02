using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-211: <see cref="NodeProfiles.Pick(NodeContext, BehaviourProfile[], BehaviourProfile[], uint, string?)"/> is the one
/// habit pick the map, the fight scene and the harness make. It must choose exactly what each of the three chose before it
/// was shared (Y1/Y2 seeded roll per node, a named profile kept, AM13 apprentice profile, E23 debug temperament).
/// </summary>
public static class NodeProfilePickTests
{
	static HarnessContent? _content;
	static HarnessContent Content => _content ??= HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static BehaviourProfile[] Fixtures => BehaviourProfile.LoadDirectory(System.IO.Path.Combine(ContentPaths.FixturesDir, "profiles"));

	/// <summary>Every fight node of the default map, resolved, plus named-profile variants.</summary>
	static IEnumerable<NodeContext> Nodes()
	{
		var map = Content.Map;
		foreach (var n in map.Nodes.Where(n => map.TypeOf(n) is { Fight: true }))
		{
			var ctx = Content.Scenes.Resolve(n).Context;
			yield return ctx;
			if (ctx.MovesAndProfilesOf == "oni") { yield return ctx with { Profile = "oni-patient" }; yield return ctx with { Profile = "oni-aggressive" }; }
			if (ctx.MovesAndProfilesOf == "kitsune" && ctx.Profile is null) yield return ctx with { Profile = "apprentice" }; // AM13: a profile from outside the yokai's own
		}
	}

	// The three picks as they were before YOK-211, copied from RunMapView.HabitLine, FightScene.LoadNodeProfiles and RunHarness.PlayFight.

	static string? OldMap(NodeContext ctx, BehaviourProfile[] all, uint seed)
	{
		var own = NodeProfiles.Of(ctx.MovesAndProfilesOf, all, Array.Empty<BehaviourProfile>());
		try { own = NodeProfiles.WithNamed(ctx.NodeId, own, ctx.Profile, all); }
		catch (InvalidOperationException) { return null; }
		int i = own.Length == 0 ? -1 : NodeTemperament.Pick(own, ctx, seed);
		return i < 0 ? null : own[i].Id;
	}

	static string OldFight(NodeContext ctx, BehaviourProfile[] all, BehaviourProfile[] fixtures, uint seed, string? debug)
	{
		var profiles = NodeProfiles.Of(ctx.MovesAndProfilesOf, all, fixtures);
		profiles = NodeProfiles.WithNamed(ctx.NodeId, profiles, ctx.Profile, all);
		if (ctx.Profile is null && debug is { } t && Array.FindIndex(profiles, p => p.Temperament == t) is var d and >= 0) return profiles[d].Id;
		return profiles[Math.Max(0, NodeTemperament.Pick(profiles, ctx, seed))].Id;
	}

	static string OldHarness(NodeContext ctx, uint seed)
	{
		var profiles = Content.ProfilesForNode(ctx);
		return profiles[Math.Max(0, NodeTemperament.Pick(profiles, ctx, seed))].Id;
	}

	[Test]
	public static void SharedPick_ChoosesWhatTheMapTheFightAndTheHarnessChoseBefore()
	{
		var all = Content.AllProfiles;
		var fixtures = Fixtures;
		int checkedNodes = 0;
		foreach (var ctx in Nodes())
		{
			for (uint seed = 0; seed <= 40; seed++)
			{
				string where = $"{ctx.NodeId} (profile {ctx.Profile ?? "-"}), seed {seed}";
				Assert.Equal(OldMap(ctx, all, seed) ?? "<none>", NodeProfiles.Pick(ctx, all, Array.Empty<BehaviourProfile>(), seed).Profile?.Id ?? "<none>", where + ": map");
				Assert.Equal(OldHarness(ctx, seed), Content.PickForNode(ctx, seed).Profile!.Id, where + ": harness");
				foreach (string? debug in new[] { null, NodeTemperament.Aggressive, NodeTemperament.Patient, "sleepy" })
					Assert.Equal(OldFight(ctx, all, fixtures, seed, debug), NodeProfiles.Pick(ctx, all, fixtures, seed, debug).Profile!.Id, $"{where}, debug {debug ?? "-"}: fight");
			}
			checkedNodes++;
		}
		Assert.True(checkedNodes >= 15, $"every fight node of the default map and its named variants ({checkedNodes})");
	}

	[Test]
	public static void SharedPick_RollsPerNode_KeepsANamedProfile_DebugOnlyOverTheRoll()
	{
		var all = Content.AllProfiles;
		var node = Content.Scenes.Resolve(Content.Map.Node("r2-oni")).Context;
		int aggressive = 0;
		for (uint seed = 1; seed <= 40; seed++)
		{
			var pick = NodeProfiles.Pick(node, all, Array.Empty<BehaviourProfile>(), seed);
			Assert.Equal(NodeTemperament.Roll(seed, node.NodeId), pick.Profile!.Temperament, $"seed {seed}: Y1/Y2 seeded roll");
			Assert.Equal(pick.Index, NodeProfiles.Pick(node, all, Array.Empty<BehaviourProfile>(), seed).Index, $"seed {seed}: same seed, same pick");
			if (pick.Profile.Temperament == NodeTemperament.Aggressive) aggressive++;
			Assert.Equal("patient", NodeProfiles.Pick(node, all, Array.Empty<BehaviourProfile>(), seed, "patient").Profile!.Temperament, $"seed {seed}: E23 debug temperament holds over the roll");
			Assert.Equal("oni-aggressive", NodeProfiles.Pick(node with { Profile = "oni-aggressive" }, all, Array.Empty<BehaviourProfile>(), seed, "patient").Profile!.Id, $"seed {seed}: a named profile is kept, even over the debug temperament");
		}
		Assert.True(aggressive is > 5 and < 35, $"both temperaments roll ({aggressive}/40 aggressive)");
		var apprentice = Content.Scenes.Resolve(Content.Map.Node("r1-apprentice")).Context;
		Assert.Equal("apprentice", NodeProfiles.Pick(apprentice, all, Array.Empty<BehaviourProfile>(), 7, "patient").Profile!.Id, "AM13: the apprentice node keeps its own profile");
		bool threw = false;
		try { NodeProfiles.Pick(node with { Profile = "no-such-profile" }, all, Array.Empty<BehaviourProfile>(), 1); } catch (InvalidOperationException) { threw = true; }
		Assert.True(threw, "a named profile found nowhere is an error, never a fallback");
		Assert.Equal(-1, NodeProfiles.Pick(Content.Scenes.Resolve(Content.Map.Node("r2-oni")).Context with { PlaysAs = "nobody" }, all, Array.Empty<BehaviourProfile>(), 1).Index, "no profile at all: index -1");
	}
}
