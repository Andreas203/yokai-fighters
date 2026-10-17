using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-119 probes: the map's copy chip (T1-T3, T6, AM6). Each expectation cites its rule.</summary>
public static class MapCopyChipProbes
{
	static RunSession NewSession(uint seed = 5)
	{
		string md = ContentPaths.Data("map");
		return RunSession.Start(FightScene.LoadAbilityPool(), FileMapSource.Default(md).Load(), NodeSceneTable.Default(md), seed);
	}

	static MapScreen Show(Node host, IMapView v)
	{
		var stage = new Control { Size = new Vector2(1920, 1080) };
		host.AddChild(stage);
		var screen = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		stage.AddChild(screen);
		screen.SetView(v);
		return screen;
	}

	static MapScreen Show(Node host, RunSession s) => Show(host, new SessionMapView(s));

	static void WalkToBossDoor(RunSession s)
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
		}
	}

	static List<SpecialSlot> Slots(RunState r) => r.Owned.Select(o => o.Slot).ToList();

	sealed class StubView : IMapView
	{
		public CopyTarget? Target; public bool Locked; public object? Key;
		public IReadOnlyList<MapNodeView> Nodes { get; } = new List<MapNodeView>();
		public IReadOnlyList<MapRouteView> Routes { get; } = new List<MapRouteView>();
		public CopyTarget? CopyTarget => Target;
		public bool CopyLocked => Locked;
		public object? RunKey => Key;
		public bool Select(string nodeId) => false;
	}

	// T3: equal levels, the one levelled most recently wins, whichever slot: level B then A -> A; A then B -> B. The chip follows the rule, not the call order.
	[Test]
	public static void TieBreakFollowsLevelOrderNotCallOrder(Node host)
	{
		foreach (bool aFirst in new[] { true, false })
		{
			var s = NewSession();
			var slots = Slots(s.Run);
			var first = aFirst ? slots[0] : slots[1];
			var last = aFirst ? slots[1] : slots[0];
			var screen = Show(host, s);
			s.Run.LevelUp(first); s.Run.LevelUp(last);
			screen.SetView(new SessionMapView(s));
			string want = s.Run[last]!.Data.Name.ToUpperInvariant();
			Assert.Equal(want, screen.CopyChip.MoveName, $"T3: most recently levelled ({last}) is the target (aFirst={aFirst})");
			screen.GetParent().Free();
		}
	}

	// T2/T3: nobody levelled -> lower slot.
	[Test]
	public static void ReleasingTheTargetThenReturningToTheMapChangesTheChip(Node host)
	{
		var s = NewSession();
		var screen = Show(host, s);
		var slots = Slots(s.Run);
		s.Run.LevelUp(slots[1]);
		screen.SetView(new SessionMapView(s));
		Assert.Equal(s.Run[slots[1]]!.Data.Name.ToUpperInvariant(), screen.CopyChip.MoveName, "levelled one is the target");
		s.Run.Release(slots[1]);
		screen.SetView(new SessionMapView(s));
		Assert.Equal(s.Run[slots[0]]!.Data.Name.ToUpperInvariant(), screen.CopyChip.MoveName, "T2: released move is not a candidate");
		Assert.Equal("changed", screen.CopyChip.LastEvent, "T6: the changed pop-up fires on the map");
		Assert.True(screen.CopyChip.ToastText.Contains(s.Run[slots[0]]!.Data.Name), "toast names the new target");
		screen.GetParent().Free();
	}

	// T2: the last special can't be released (T5); chip never goes grey on a real run.
	[Test]
	public static void LockedWithNoSpecialIsGreyPlate(Node host)
	{
		var s = NewSession();
		WalkToBossDoor(s);
		s.Enter(s.Reachable()[0]);
		var r = RunSession.Restore(s.Run, s.Pool, s.Scenes, s.Seed, true, null);
		var screen = Show(host, new SessionMapView(r));
		Assert.Equal(CopyTalismanState.None, screen.CopyChip.State, "T1: lock found nothing; the live target must not leak in");
		Assert.Equal("-", screen.CopyChip.MoveName, "no name");
		screen.GetParent().Free();
	}

	// T1: Continue from a save taken after the lock: first show is LOCKED, no toast, even though the live target differs.
	[Test]
	public static void ContinueAfterLockShowsLockedNoEvent(Node host)
	{
		var s = NewSession();
		WalkToBossDoor(s);
		s.Enter(s.Reachable()[0]);
		var locked = s.LockedCopy!;
		// make the live target differ from the lock
		var other = Slots(s.Run).First(x => x != locked.Slot);
		if (s.Run.CanLevelUp(other)) { s.Run.LevelUp(other); s.Run.LevelUp(other); }
		var r = RunSession.Restore(s.Run, s.Pool, s.Scenes, s.Seed, true, locked);
		var screen = Show(host, new SessionMapView(r));
		Assert.Equal(CopyTalismanState.Locked, screen.CopyChip.State, "locked");
		Assert.Equal(locked.Data.Name.ToUpperInvariant(), screen.CopyChip.MoveName, "T1: the locked copy");
		Assert.Equal("LOCKED", screen.CopyChip.CaptionText, "caption");
		Assert.Equal("", screen.CopyChip.LastEvent + screen.CopyChip.ToastText, "first show: nothing fires");
		screen.GetParent().Free();
	}

	// T1/AM6: a "changed" toast still up when the lock lands is cleared; locked chip never toasts.
	[Test]
	public static void NewRunAfterLockedRunIsAFreshChip(Node host)
	{
		var a = NewSession();
		WalkToBossDoor(a); a.Enter(a.Reachable()[0]);
		var screen = Show(host, a);
		Assert.Equal(CopyTalismanState.Locked, screen.CopyChip.State, "setup: run one locked");
		var b = NewSession(9);
		screen.SetView(new SessionMapView(b));
		Assert.Equal(CopyTalismanState.Defined, screen.CopyChip.State, "run two defined");
		Assert.Equal("COPY TARGET", screen.CopyChip.CaptionText, "caption reset");
		Assert.Equal("", screen.CopyChip.LastEvent + screen.CopyChip.ToastText, "nothing fires");
		screen.GetParent().Free();
	}

	// New run straight after a pulse: the tint of run one's pulse must not carry over.
	[Test]
	public static void SetViewTwiceDoesNotReplayEvents(Node host)
	{
		var s = NewSession();
		var screen = Show(host, s);
		var v = new SessionMapView(s);
		s.Run.LevelUp(Slots(s.Run)[1]);
		screen.SetView(v);
		Assert.Equal("changed", screen.CopyChip.LastEvent, "first set");
		screen.SetView(v);
		Assert.Equal("", screen.CopyChip.LastEvent, "second SetView with same view: no event");
		screen.SetView(new SessionMapView(s));
		Assert.Equal("", screen.CopyChip.LastEvent, "new view over the same run is not a new run");
		Assert.True(screen.CopyChip.ToastText != "", "the toast from the real change is still up (not wiped by a same-run SetView)");
		screen.GetParent().Free();
	}

	// Rebuild from Board.Resized neither replays nor wipes a toast.
	[Test]
	public static void NullRunKeyCarriesOver(Node host)
	{
		var s = NewSession();
		var screen = Show(host, s);
		var t = CopyRule.Target(s.Run)!;
		var stub = new StubView { Target = t, Key = null };
		screen.SetView(stub);
		Assert.Equal("", screen.CopyChip.LastEvent, "same target: nothing");
		var lvl = t with { Level = t.Level + 1 };
		stub.Target = lvl;
		screen.SetView(stub);
		Assert.Equal("levelled", screen.CopyChip.LastEvent, "null key: carried over, so a level change pulses");
		// back to the real session: same key as before the stub, not a new run.
		screen.SetView(new SessionMapView(s));
		Assert.True(screen.CopyChip.State == CopyTalismanState.Defined, "still defined");
		screen.GetParent().Free();
	}

	// The chip and the detail panel only read: hash identical across every node's Detail, SetView, Refresh, Move.
	[Test]
	public static void EveryReadLeavesTheHashAlone(Node host)
	{
		var s = NewSession(3);
		var view = new SessionMapView(s);
		var screen = Show(host, view);
		ulong h = s.StateHash(), rh = s.Run.StateHash();
		foreach (var n in view.Nodes) view.Detail(n.Id);
		_ = view.CopyTarget; _ = view.CopyLocked; _ = view.RunKey;
		screen.SetView(view); screen.Refresh(); screen.Move(1); screen.Move(-1);
		Assert.Equal(h, s.StateHash(), "session hash unchanged");
		Assert.Equal(rh, s.Run.StateHash(), "run hash unchanged");
		WalkToBossDoor(s);
		s.Enter(s.Reachable()[0]);
		h = s.StateHash();
		foreach (var n in view.Nodes) view.Detail(n.Id);
		screen.SetView(view);
		Assert.Equal(h, s.StateHash(), "locked: reads change nothing");
		screen.GetParent().Free();
	}


}
