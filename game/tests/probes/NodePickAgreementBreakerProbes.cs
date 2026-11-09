using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Harness;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-211 probes: the map's habit line, the real fight scene and the harness pick the same profile for every fight node of
/// the default map (elders that play as another yokai, the apprentice, the boss), every seed and every debug temperament
/// string, valid or not. Expectation: Y1/Y2 (seeded roll per node), AM13 (a named profile is kept), E23 (debug temperament
/// holds over the roll, never over a named profile; an unknown one is no choice at all).
/// </summary>
public static class NodePickAgreementBreakerProbes
{
	static MapGraph DefaultMap() => new FileMapSource(Path.Combine(ContentPaths.Data("map"), "default-map.json")).Load();

	static IEnumerable<MapNodeData> FightNodes(MapGraph g) => g.Nodes.Where(n => g.TypeOf(n) is { Fight: true });

	static RunSession SessionOn(MapNodeData node, AbilityPool pool, uint seed)
	{
		var run = DevRunStates.Source.Build(pool);
		run.StartMap(new MapGraph("probe", new[] { node }, Array.Empty<MapRoute>(), NodeTypeRegistry.Default(), "probe"));
		var s = new RunSession(run, pool, NodeSceneTable.Default(ContentPaths.Data("map")), seed);
		Assert.True(s.Enter(node.Id).Ok, "enter " + node.Id);
		return s;
	}

	static FightScene Host(Node runner, RunSession s)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		scene.Session = s;
		runner.AddChild(scene);
		return scene;
	}

	// Y1/Y2 + AM13 + E23: map line == fight profile's habit == harness pick (harness has no debug temperament, so it is
	// compared only with none); the debug temperament never overrides a named profile or an unknown value.
	[Test]
	public static void MapFightHarness_PickTheSameProfile_ForEveryNodeSeedAndDebugString(Node runner)
	{
		var graph = DefaultMap();
		var pool = FightScene.LoadAbilityPool();
		var content = HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
		var scenes = NodeSceneTable.Default(ContentPaths.Data("map"));
		var bad = new List<string>();
		int scenesBuilt = 0;
		foreach (var node in FightNodes(graph))
		{
			var ctx = scenes.Resolve(node).Context;
			foreach (uint seed in new uint[] { 1, 2, 0xFFFFFFFF })
			{
				string? rolled = null;
				foreach (string? dbg in new[] { null, "patient", "aggressive", "sleepy", "Patient", "" })
				{
					string tag = $"{node.Id} (yokai {node.Yokai}, plays as {ctx.MovesAndProfilesOf}, profile {ctx.Profile ?? "-"}) seed {seed} debug '{dbg ?? "null"}'";
					var session = SessionOn(node, pool, seed);
					session.DebugTemperament = dbg;
					var scene = Host(runner, session);
					try
					{
						scenesBuilt++;
						if (scene.P2Ai is null || scene.AiSetupError != null) { bad.Add(tag + ": fight did not build its AI: " + scene.AiSetupError); continue; }
						var fight = scene.P2Ai.Profile;
						string? mapLine = new RunMapView(new RunMap(graph), null, scenes, seed) { DebugTemperament = () => dbg }.Detail(node.Id)!.Habit;
						string? wantLine = node.Type == NodeTypeRegistry.Boss ? null : RunMapView.HabitSentence(fight.Habit.Description);
						if (mapLine != wantLine) bad.Add($"{tag}: map shows '{mapLine ?? "<null>"}', fight plays {fight.Id} ('{wantLine ?? "<null>"}')");
						if (dbg is null)
						{
							var h = content.PickForNode(ctx, seed).Profile!;
							if (h.Id != fight.Id) bad.Add($"{tag}: harness picks {h.Id}, fight plays {fight.Id}");
							rolled = fight.Id;
						}
						else
						{
							bool valid = dbg is "patient" or "aggressive";
							bool named = ctx.Profile != null;
							bool hasIt = content.AllProfiles.Any(p => p.Yokai == ctx.MovesAndProfilesOf && p.Temperament == dbg);
							string want = valid && !named && hasIt ? content.AllProfiles.First(p => p.Yokai == ctx.MovesAndProfilesOf && p.Temperament == dbg).Id : rolled!;
							if (fight.Id != want) bad.Add($"{tag}: fight plays {fight.Id}, E23/Y1 says {want}");
						}
					}
					finally { scene.QueueFree(); }
				}
			}
		}
		Assert.True(scenesBuilt >= 12 * 3 * 6, $"fight nodes covered ({scenesBuilt})");
		Assert.True(bad.Count == 0, string.Join("\n", bad.Take(12)) + (bad.Count > 12 ? $"\n(+{bad.Count - 12} more)" : ""));
	}

	static string Save(RunSession s) => System.Text.Json.JsonSerializer.Serialize(RunSave.Capture(s));

	// E23: debug state only. Not in the run's StateHash, not in the save text, not restored; F9 and a refused F9 leave the hash alone.
	[Test]
	public static void DebugTemperament_NeverEntersTheHashTheSaveOrARestoredRun(Node runner)
	{
		var pool = FightScene.LoadAbilityPool();
		var map = DefaultMap();
		var scenes = NodeSceneTable.Default(ContentPaths.Data("map"));
		RunSession New() => RunSession.Start(pool, map, scenes, 9);
		var plain = New();
		var dbg = New();
		dbg.DebugTemperament = "patient";
		Assert.Equal(plain.StateHash(), dbg.StateHash(), "fresh session: debug temperament not in StateHash");
		Assert.Equal(Save(plain), Save(dbg), "not in the saved run's fields");
		Assert.True(plain.Enter("r1-apprentice").Ok && dbg.Enter("r1-apprentice").Ok, "enter");
		Assert.Equal(plain.StateHash(), dbg.StateHash(), "entered node: same hash");
		Assert.Equal(Save(plain), Save(dbg), "entered node: same saved fields");
		Assert.True(!Save(dbg).Contains("patient", StringComparison.OrdinalIgnoreCase), "no temperament in the saved run");
		var restored = RunSave.Capture(dbg).Restore(pool, map, scenes);
		Assert.True(restored.DebugTemperament == null, "a restored run starts with no debug temperament");
		Assert.Equal(dbg.StateHash(), restored.StateHash(), "and hashes as the saved one");

		// the real scene: F9 switch and a refused one
		var session = RunSession.Start(pool, map, scenes, 9);
		Assert.True(session.Enter("r1-apprentice").Ok, "enter r1-apprentice");
		var scene = Host(runner, session);
		try
		{
			ulong before = session.StateHash();
			Assert.True(scene.SetTemperament("patient") || scene.SetTemperament("aggressive"), "a switch works");
			string chosen = session.DebugTemperament ?? "<null>";
			Assert.Equal(before, session.StateHash(), "F9 switch: hash unchanged");
			Assert.True(!scene.SetTemperament("sleepy") && !scene.SetTemperament("Patient") && !scene.SetTemperament(""), "unknown / wrong-case / empty refused");
			Assert.Equal(chosen, session.DebugTemperament ?? "<null>", "a refused switch leaves the session's choice as it was");
			Assert.Equal(before, session.StateHash(), "refused switch: hash unchanged");
		}
		finally { scene.QueueFree(); }
	}

	// Pure pick: callers' arrays are never changed (Of sorts; fixtures are cloned first), a named profile that is already
	// among the fixtures is not appended twice, and the pick is the same on a second call.
	[Test]
	public static void Pick_LeavesItsInputsAlone_NoDuplicateNamed_RepeatableAfterwards()
	{
		var content = HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
		var all = content.AllProfiles;
		var allIds = all.Select(p => p.Id).ToArray();
		var fixtures = BehaviourProfile.LoadDirectory(Path.Combine(ContentPaths.FixturesDir, "profiles")).Reverse().ToArray(); // unsorted on purpose
		var fixtureIds = fixtures.Select(p => p.Id).ToArray();
		var scenes = NodeSceneTable.Default(ContentPaths.Data("map"));
		var oni = scenes.Resolve(DefaultMap().Node("r2-oni")).Context;
		var nobody = oni with { PlaysAs = "nobody-at-all" }; // no profile in data/: the fixtures stand in
		var first = NodeProfiles.Pick(nobody, all, fixtures, 5);
		Assert.True(fixtures.Select(p => p.Id).SequenceEqual(fixtureIds) && all.Select(p => p.Id).SequenceEqual(allIds), "inputs untouched");
		Assert.True(first.Profiles.Select(p => p.Id).Distinct().Count() == first.Profiles.Length, "no duplicate profile");
		var second = NodeProfiles.Pick(nobody, all, fixtures, 5);
		Assert.Equal(first.Profile!.Id, second.Profile!.Id, "same call, same pick");
		string fixtureName = fixtureIds[0];
		var named = NodeProfiles.Pick(nobody with { Profile = fixtureName }, all, fixtures, 5, "patient");
		Assert.Equal(fixtureName, named.Profile!.Id, "a named profile among the fixtures is kept, over the debug temperament");
		Assert.Equal(fixtures.Length, named.Profiles.Length, "and not appended twice");
	}
}
