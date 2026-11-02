using System;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-211: the map's "Habit to punish" line. It shows the profile the fight picks (the shared <see cref="NodeProfiles.Pick(NodeContext, BehaviourProfile[], BehaviourProfile[], uint, string?)"/>,
/// E23 debug temperament included), reads which nodes fight from node-type data, loads data/profiles/ once, and takes the
/// first sentence of the description however it ends.
/// </summary>
public static class MapHabitLineTests
{
	static RunSession StartSession(uint seed = 5)
	{
		string md = ContentPaths.Data("map");
		return RunSession.Start(Fight.FightScene.LoadAbilityPool(), FileMapSource.Default(md).Load(), NodeSceneTable.Default(md), seed);
	}

	/// <summary>Runs <paramref name="body"/> with each profile's description replaced by "AGGRESSIVE habit. Counter." / "PATIENT habit. Counter.".</summary>
	static void WithTemperamentHabits(Action body)
	{
		var saved = RunMapView.Profiles!;
		try
		{
			RunMapView.Profiles = saved.Select(p => p with { Habit = p.Habit with { Description = p.Temperament.ToUpperInvariant() + " habit. Counter." } }).ToArray();
			body();
		}
		finally { RunMapView.Profiles = saved; }
	}

	[Test]
	public static void HabitSentence_EndsAtAFullStopBangOrQuestion_NotAtAnAbbreviation()
	{
		foreach (var (text, want) in new (string, string?)[]
		{
			("Often jumps. Hit her.", "Often jumps."),
			("Grabs (60%). Break the throw.", "Grabs (60%)."),
			("Charges in! Block it.", "Charges in!"),
			("Will it jump? Wait and see.", "Will it jump?"),
			("Really?! Then punish.", "Really?!"),
			("Shouts \"Ha!\" then jumps. Punish.", "Shouts \"Ha!\" then jumps."),
			("Jumps, e.g. after a knockdown. Meet her.", "Jumps, e.g. after a knockdown."),
			("Uses specials, i.e. Foxfire, etc. and more. Block.", "Uses specials, i.e. Foxfire, etc. and more."),
			("Throws at Lv. 2 vs. you. Break it.", "Throws at Lv. 2 vs. you."),
			("Waits... then strikes. Bait it.", "Waits... then strikes."),
			("Steps 1.5 lengths back. Chase.", "Steps 1.5 lengths back."),
			("Just this", "Just this"),
			("Ends here.", "Ends here."),
			("  Padded.  Rest. ", "Padded."),
			("   ", null),
		})
			Assert.Equal(want ?? "<null>", RunMapView.HabitSentence(text) ?? "<null>", $"'{text}'");
		foreach (var p in RunMapView.Profiles!) // the shipped descriptions: unchanged from the ". " split
		{
			string d = p.Habit.Description.Trim();
			int end = d.IndexOf(". ", StringComparison.Ordinal);
			Assert.Equal(end < 0 ? d : d[..(end + 1)], RunMapView.HabitSentence(d) ?? "", p.Id + ": shipped habit line unchanged");
		}
	}

	[Test]
	public static void Profiles_LoadedOnce_AndANullSetIsKept()
	{
		var saved = RunMapView.Profiles;
		try
		{
			RunMapView.ReloadProfiles();
			int before = RunMapView.ProfileLoads;
			var sess = StartSession();
			var view = new RunMapView(sess.Map, sess.Run, sess.Scenes, sess.Seed);
			for (int i = 0; i < 5; i++) { _ = view.Detail("r2-oni"); _ = RunMapView.Profiles; }
			Assert.Equal(before + 1, RunMapView.ProfileLoads, "data/profiles/ read once for many habit lines");
			RunMapView.Profiles = null;
			Assert.True(RunMapView.Profiles == null && view.Detail("r2-oni")!.Habit == null, "null = no profile data: no habit line");
			Assert.Equal(before + 1, RunMapView.ProfileLoads, "a null set is not re-read from disk on every call");
		}
		finally { RunMapView.Profiles = saved; }
	}

	[Test]
	public static void HabitLine_NodeTypeDataSaysWhichNodesFight()
	{
		var sess = StartSession();
		var view = new RunMapView(sess.Map, sess.Run, sess.Scenes, sess.Seed);
		foreach (var n in sess.Map.Graph.Nodes)
		{
			bool fights = sess.Map.Graph.TypeOf(n)!.Fight;
			Assert.Equal(fights && n.Type != NodeTypeRegistry.Boss, view.Detail(n.Id)!.Habit != null, $"{n.Id} ({n.Type}): a habit line exactly on a non-boss fight node");
		}
		// The node-type data decides, not a list of type names: flip a type's fight flag and the habit line follows.
		var merchantFights = NodeTypeRegistry.Default().Register(new NodeType(NodeTypeRegistry.Merchant, Fight: true));
		var m = new MapGraph("t", new[] { new MapNodeData("m1", 1, NodeTypeRegistry.Merchant, "oni", false, "Oni") }, Array.Empty<MapRoute>(), merchantFights);
		Assert.True(new RunMapView(new RunMap(m), null, sess.Scenes, 3).Detail("m1")!.Habit != null, "a merchant registered as a fight shows its yokai's habit");
		var duelStops = NodeTypeRegistry.Default().Register(new NodeType(NodeTypeRegistry.Duel, Placeholder: true));
		var d = new MapGraph("t", new[] { new MapNodeData("d1", 1, NodeTypeRegistry.Duel, "oni", false, "Oni") }, Array.Empty<MapRoute>(), duelStops);
		Assert.True(new RunMapView(new RunMap(d), null, sess.Scenes, 3).Detail("d1")!.Habit == null, "a duel registered with no fight has no habit line");
	}

	[Test]
	public static void HabitLine_HonoursTheDebugTemperament_E23() => WithTemperamentHabits(() =>
	{
		for (uint seed = 1; seed <= 20; seed++)
		{
			var sess = StartSession(seed);
			var map = new SessionMapView(sess);
			Assert.Equal(NodeTemperament.Roll(seed, "r3-kappa").ToUpperInvariant() + " habit.", map.Detail("r3-kappa")!.Habit ?? "<null>", $"seed {seed}: no debug choice, the node's roll");
			foreach (string t in new[] { NodeTemperament.Patient, NodeTemperament.Aggressive })
			{
				sess.DebugTemperament = t;
				Assert.Equal(t.ToUpperInvariant() + " habit.", map.Detail("r3-kappa")!.Habit ?? "<null>", $"seed {seed}: the fight's debug temperament {t} shows on the map");
				Assert.Equal(t.ToUpperInvariant() + " habit.", map.Detail("r5-elder-oni")!.Habit ?? "<null>", $"seed {seed}: on every node");
			}
			sess.DebugTemperament = NodeTemperament.Patient;
			Assert.True(map.Detail("r1-apprentice")!.Habit == RunMapView.HabitSentence(RunMapView.Profiles!.First(p => p.Id == "apprentice").Habit.Description), "AM13: the apprentice's named profile is kept");
			sess.DebugTemperament = null;
			Assert.Equal(NodeTemperament.Roll(seed, "r3-kappa").ToUpperInvariant() + " habit.", map.Detail("r3-kappa")!.Habit ?? "<null>", $"seed {seed}: cleared, the roll again");
		}
	});
}
