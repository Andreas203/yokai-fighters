using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-119 built the map's copy-target chip (T6); YOK-238 removes it (AM20, T6): the map does not show the copy target, and the rest-stop
/// forecast names it. These tests keep the old coverage as its opposite: whatever state the run is in (live target, a different special overtaking
/// it, the lock, no special at all, a new run), the map screen holds no copy talisman, no toast and no "copy target" text, and showing the map
/// still changes nothing in the run.
/// </summary>
public static class MapCopyChipTests
{
	static RunSession NewSession(uint seed = 5)
	{
		string md = ContentPaths.Data("map");
		return RunSession.Start(FightScene.LoadAbilityPool(), FileMapSource.Default(md).Load(), NodeSceneTable.Default(md), seed);
	}

	static MapScreen Show(Node host, RunSession s)
	{
		var stage = new Control { Size = new Vector2(1920, 1080) };
		host.AddChild(stage);
		var screen = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		stage.AddChild(screen);
		screen.SetView(new SessionMapView(s));
		return screen;
	}

	/// <summary>Plays the first reachable node each step (fights won with 840 left, first card taken) until the next one would be the boss.</summary>
	static void WalkToBossDoor(RunSession s, Action? afterNode = null)
	{
		while (s.Map.Graph.Node(s.Reachable()[0]).Type != NodeTypeRegistry.Boss)
		{
			s.Enter(s.Reachable()[0]);
			if (s.Node!.Type is not ("merchant" or "dojo"))
			{
				var d = s.WinFight(840);
				if (d != null) { var c = d.Offer.Cards[0]; d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null); }
			}
			s.CompleteNode();
			afterNode?.Invoke();
		}
	}

	static System.Collections.Generic.IEnumerable<Node> Descendants(Node n)
	{
		foreach (var c in n.GetChildren()) { yield return c; foreach (var d in Descendants(c)) yield return d; }
	}

	/// <summary>The chip is absent: no CopyTalisman, no node named like it, no "Toast" label, and no text on the screen names the copy target.</summary>
	static void AssertNoChip(MapScreen screen, string why)
	{
		var all = Descendants(screen).ToList();
		Assert.True(!all.OfType<CopyTalisman>().Any(), why + ": no copy talisman anywhere on the map (AM20)");
		Assert.True(!all.Any(n => n.Name.ToString() is "CopyChip" or "Toast" or "Caption" or "MoveName" or "Level"), why + ": none of the chip's parts or its toast label");
		var text = string.Join(" | ", all.OfType<Label>().Where(l => l.IsVisibleInTree()).Select(l => l.Text));
		Assert.True(!text.ToUpperInvariant().Contains("COPY TARGET") && !text.Contains("Lv "), why + ": no label names the copy target: " + text);
	}

	[Test]
	public static void TheMapNeverShowsTheCopyTarget(Node host)
	{
		var s = NewSession();
		var screen = Show(host, s);
		AssertNoChip(screen, "a new run (the target is defined at turn one, T2)");
		var other = s.Run.Owned.GetEnumerator(); other.MoveNext(); other.MoveNext();
		s.Run.LevelUp(other.Current.Slot); // another special overtakes the target: no pop-up on the map any more
		screen.SetView(new SessionMapView(s));
		AssertNoChip(screen, "after another special overtook the target");
		s.Run.LevelUp(other.Current.Slot);
		screen.SetView(new SessionMapView(s));
		AssertNoChip(screen, "after the same special gained a level");
		screen.Refresh(); screen.Move(1);
		AssertNoChip(screen, "after a refresh and a cursor move");
		screen.GetParent().QueueFree();
	}

	[Test]
	public static void NoChipWithNoSpecialAndNoChipAfterTheLock(Node host)
	{
		string md = ContentPaths.Data("map");
		var graph = FileMapSource.Default(md).Load();
		var run = RunState.Restore(840, Array.Empty<(SpecialSlot, OwnedSpecial)>());
		run.StartMap(graph);
		var bare = new RunSession(run, FightScene.LoadAbilityPool(), NodeSceneTable.Default(md), 5);
		var screen = Show(host, bare);
		Assert.True(CopyRule.Target(run) is null, "no slot holds a special");
		AssertNoChip(screen, "no special in any slot (it was the grey plate)");
		screen.GetParent().QueueFree();

		var s = NewSession();
		var locked = Show(host, s);
		WalkToBossDoor(s);
		Assert.True(s.Enter(s.Reachable()[0]).Ok && s.CopyLocked, "entering the Tanuki's node locks the copy (T1)");
		locked.SetView(new SessionMapView(s));
		AssertNoChip(locked, "after the lock (it was the LOCKED chip)");
		locked.GetParent().QueueFree();
	}

	[Test]
	public static void ANewRunOnTheSameScreenStillShowsNoChip(Node host)
	{
		var a = NewSession();
		var screen = Show(host, a);
		var other = a.Run.Owned.GetEnumerator(); other.MoveNext(); other.MoveNext();
		a.Run.LevelUp(other.Current.Slot);
		screen.SetView(new SessionMapView(a));
		screen.SetView(new SessionMapView(NewSession(9))); // GameFlow does this for every new run
		AssertNoChip(screen, "run two on the same screen");
		screen.GetParent().QueueFree();
	}

	[Test]
	public static void ShowingTheMapChangesNothingInTheRun(Node host)
	{
		// Same seed, same walk: one session with the map screen refreshed at every step, one without. Hash and the next draft must match.
		var plain = NewSession(7);
		var watched = NewSession(7);
		var screen = Show(host, watched);
		ulong h0 = watched.StateHash();
		screen.Refresh(); screen.Move(1); screen.Move(-1);
		Assert.Equal(h0, watched.StateHash(), "drawing the map leaves the run's hash alone");
		WalkToBossDoor(plain);
		WalkToBossDoor(watched, () => { screen.SetView(new SessionMapView(watched)); screen.Move(1); screen.Move(-1); }); // Move reads the detail panel too
		Assert.Equal(plain.StateHash(), watched.StateHash(), "a whole walk with the map on screen ends in the same state (no RNG drawn, no state written)");
		Assert.True(plain.Enter(plain.Reachable()[0]).Ok && watched.Enter(watched.Reachable()[0]).Ok, "both enter the boss node");
		Assert.Equal(plain.StateHash(), watched.StateHash(), "and lock the same copy");
		screen.GetParent().QueueFree();
	}
}
