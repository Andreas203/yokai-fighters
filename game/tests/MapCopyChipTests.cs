using System;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-119 (T6, AM6): the map's copy-target talisman. Live target before the lock (T2-T4), the locked copy after it (T1), the grey
/// plate when no slot holds a special; the "levelled" pulse and "changed" toast fire here, on the return to the map, because AM6
/// keeps the HUD talisman out of every fight but the Tanuki's. The chip is read-only: no run state, hash or draw changes.
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

	[Test]
	public static void ShowsTheLiveTargetAndFollowsItAcrossReturnsToTheMap(Node host)
	{
		var s = NewSession();
		var screen = Show(host, s);
		var chip = screen.CopyChip;
		var t0 = CopyRule.Target(s.Run)!;
		Assert.True(chip.State == CopyTalismanState.Defined && chip.CaptionText == "COPY TARGET", "a new run: the target is defined at turn one (T2)");
		Assert.Equal(t0.Data.Name.ToUpperInvariant(), chip.MoveName, "names the target's move");
		Assert.Equal("", chip.LastEvent, "the first show has no pulse and no toast");
		Assert.Equal("", chip.ToastText, "no toast");

		// A different special overtakes it (T2: highest level): back on the map the chip says so.
		var other = s.Run.Owned.GetEnumerator(); other.MoveNext(); other.MoveNext();
		var slotB = other.Current.Slot;
		s.Run.LevelUp(slotB);
		screen.SetView(new SessionMapView(s));
		var t1 = CopyRule.Target(s.Run)!;
		Assert.True(t1.Data.Id != t0.Data.Id, "the levelled special is the new target");
		Assert.Equal("changed", chip.LastEvent, "another special overtook it");
		Assert.Equal($"Copy target: {t1.Data.Name}, Lv {t1.Level}", chip.ToastText, "the toast names it, here on the map");
		Assert.Equal(t1.Data.Name.ToUpperInvariant(), chip.MoveName, "chip follows");

		// The same special gains a level: pulse, no toast.
		s.Run.LevelUp(slotB);
		screen.SetView(new SessionMapView(s));
		Assert.Equal("levelled", chip.LastEvent, "the same special went up a level");
		Assert.Equal("", chip.ToastText, "levelled clears the toast");
		Assert.Equal($"Lv {CopyRule.Target(s.Run)!.Level}", chip.LevelText, "new level shown");

		// Nothing changed since: a refresh or a cursor move never replays the event.
		screen.Refresh(); screen.Move(1);
		Assert.Equal("", chip.LastEvent, "an unchanged target animates nothing");
		screen.GetParent().QueueFree();
	}

	[Test]
	public static void NoSpecialInAnySlotIsTheGreyPlate(Node host)
	{
		string md = ContentPaths.Data("map");
		var graph = FileMapSource.Default(md).Load();
		var run = RunState.Restore(840, Array.Empty<(SpecialSlot, OwnedSpecial)>());
		run.StartMap(graph);
		var s = new RunSession(run, FightScene.LoadAbilityPool(), NodeSceneTable.Default(md), 5);
		var screen = Show(host, s);
		Assert.True(CopyRule.Target(run) is null, "no slot holds a special");
		Assert.Equal(CopyTalismanState.None, screen.CopyChip.State, "the chip is the grey plate (T2: always defined once a special is owned)");
		Assert.Equal("-", screen.CopyChip.MoveName, "no move named");
		Assert.Equal("", screen.CopyChip.LevelText, "no level");
		screen.GetParent().QueueFree();
	}

	[Test]
	public static void AfterTheLockTheChipShowsTheLockedCopyNotTheLiveOne(Node host)
	{
		var s = NewSession();
		var screen = Show(host, s);
		WalkToBossDoor(s);
		Assert.True(s.Enter(s.Reachable()[0]).Ok && s.CopyLocked, "entering the Tanuki's node locks the copy (T1)");
		var locked = s.LockedCopy!;
		// Ryo's slots move on after the lock (they cannot in play, but the view must still not follow them).
		foreach (var (slot, sp) in new System.Collections.Generic.List<(SpecialSlot, OwnedSpecial)>(s.Run.Owned))
			if (s.Run.CanLevelUp(slot) && slot != locked.Slot) { s.Run.LevelUp(slot); break; }
		screen.SetView(new SessionMapView(s));
		var chip = screen.CopyChip;
		Assert.Equal(CopyTalismanState.Locked, chip.State, "locked state");
		// The locked slot itself moves on: levelled if it can be, then emptied. The snapshot ignores both (T1).
		if (s.Run.CanLevelUp(locked.Slot)) s.Run.LevelUp(locked.Slot);
		s.Run.Release(locked.Slot);
		Assert.True(s.Run[locked.Slot] is null, "the locked special's slot is empty now");
		screen.SetView(new SessionMapView(s));
		Assert.Equal(CopyTalismanState.Locked, chip.State, "still locked");
		Assert.Equal(locked.Data.Name.ToUpperInvariant(), chip.MoveName, "still the copy that was locked");
		Assert.Equal($"Lv {locked.Level}", chip.LevelText, "at the locked level");
		Assert.Equal("", chip.LastEvent, "no pulse or toast: the snapshot did not change");
		Assert.Equal("LOCKED", chip.CaptionText, "caption");
		Assert.Equal(locked.Data.Name.ToUpperInvariant(), chip.MoveName, "the copy that was locked");
		Assert.Equal($"Lv {locked.Level}", chip.LevelText, "at the level it was locked at");
		Assert.Equal("", chip.ToastText, "no toast once locked");
		screen.GetParent().QueueFree();
	}

	[Test]
	public static void ANewRunStartsWithoutAPulseOrToast(Node host)
	{
		var a = NewSession();
		var screen = Show(host, a);
		var other = a.Run.Owned.GetEnumerator(); other.MoveNext(); other.MoveNext();
		a.Run.LevelUp(other.Current.Slot);
		screen.SetView(new SessionMapView(a));
		Assert.Equal("changed", screen.CopyChip.LastEvent, "run one: the chip announced a change");
		var b = NewSession(9);
		screen.SetView(new SessionMapView(b)); // GameFlow does this for every new run
		Assert.Equal("", screen.CopyChip.LastEvent, "run two's first show is a first show");
		Assert.Equal("", screen.CopyChip.ToastText, "the old toast is gone");
		Assert.Equal(CopyRule.Target(b.Run)!.Data.Name.ToUpperInvariant(), screen.CopyChip.MoveName, "and shows run two's target");
		screen.GetParent().QueueFree();
	}

	[Test]
	public static void ShowingTheChipChangesNothingInTheRun(Node host)
	{
		// Same seed, same walk: one session with the map screen refreshed at every step, one without. Hash and the next draft must match.
		var plain = NewSession(7);
		var watched = NewSession(7);
		var screen = Show(host, watched);
		ulong h0 = watched.StateHash();
		screen.Refresh(); screen.Move(1); screen.Move(-1);
		Assert.Equal(h0, watched.StateHash(), "reading the target and drawing the chip leave the run's hash alone");
		WalkToBossDoor(plain);
		WalkToBossDoor(watched, () => { screen.SetView(new SessionMapView(watched)); screen.Move(1); screen.Move(-1); }); // Move reads the detail panel too
		Assert.Equal(plain.StateHash(), watched.StateHash(), "a whole walk with the chip on screen ends in the same state (no RNG drawn, no state written)");
		Assert.True(plain.Enter(plain.Reachable()[0]).Ok && watched.Enter(watched.Reachable()[0]).Ok, "both enter the boss node");
		Assert.Equal(plain.StateHash(), watched.StateHash(), "and lock the same copy");
		screen.GetParent().QueueFree();
	}
}
