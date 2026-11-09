using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-119 probes of the map's copy chip, turned (YOK-238, AM20) into probes that the chip is gone from every state it used to appear in. Each expectation cites its rule.</summary>
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

	static System.Collections.Generic.IEnumerable<Node> Descendants(Node n)
	{
		foreach (var c in n.GetChildren()) { yield return c; foreach (var d in Descendants(c)) yield return d; }
	}

	static void AssertNoChip(MapScreen screen, string where)
	{
		var all = Descendants(screen).ToList();
		Assert.True(!all.OfType<CopyTalisman>().Any(), where + ": AM20 / T6: the map holds no copy talisman");
		var text = string.Join(" | ", all.OfType<Label>().Select(l => l.Text));
		Assert.True(!text.ToUpperInvariant().Contains("COPY TARGET") && !text.ToUpperInvariant().Contains("LOCKED"), where + ": no label names the copy target or its lock: " + text);
	}

	// T3 / T6: the tie-break and the levelled pop-up used to show on the map; now whichever order two specials are levelled in, the map says nothing.
	[Test]
	public static void NoPopUpWhicheverSpecialIsLevelledFirst(Node host)
	{
		foreach (bool aFirst in new[] { true, false })
		{
			var s = NewSession();
			var slots = Slots(s.Run);
			var screen = Show(host, s);
			s.Run.LevelUp(aFirst ? slots[0] : slots[1]);
			s.Run.LevelUp(aFirst ? slots[1] : slots[0]);
			screen.SetView(new SessionMapView(s));
			AssertNoChip(screen, $"aFirst={aFirst}");
			screen.GetParent().QueueFree();
		}
	}

	// T2: a released target moves on; T1: the lock. The map shows neither.
	[Test]
	public static void ReleasedTargetAndTheLockShowNothingOnTheMap(Node host)
	{
		var s = NewSession();
		var screen = Show(host, s);
		var slots = Slots(s.Run);
		s.Run.LevelUp(slots[1]);
		screen.SetView(new SessionMapView(s));
		s.Run.Release(slots[1]);
		screen.SetView(new SessionMapView(s));
		AssertNoChip(screen, "T2: the target slot was released");
		WalkToBossDoor(s);
		s.Enter(s.Reachable()[0]);
		screen.SetView(new SessionMapView(s));
		AssertNoChip(screen, "T1: the copy is locked (it was the LOCKED chip)");
		screen.GetParent().QueueFree();
	}

	// A view that carries a copy target does not make the screen show it: the view API may still hold the target, the screen ignores it.
	[Test]
	public static void AViewThatCarriesATargetDoesNotMakeTheChipAppear(Node host)
	{
		var s = NewSession();
		var view = new StubView { Target = CopyRule.Target(s.Run), Locked = true, Key = new object() };
		var screen = Show(host, view);
		AssertNoChip(screen, "a stub view with a locked target");
		view.Key = new object();
		screen.SetView(view);
		AssertNoChip(screen, "a second run key");
		screen.GetParent().QueueFree();
	}

	// The status panel keeps health and coins only; the toast label of the old chip is not left behind in the top bar.
	[Test]
	public static void TopBarHoldsOnlyTheStatusPanel(Node host)
	{
		var screen = Show(host, NewSession());
		var topBar = UiFind.Get<Control>(screen, "TopBar");
		Assert.True(topBar.GetChildren().OfType<Control>().Select(c => c.Name.ToString()).SequenceEqual(new[] { "Status" }), "TopBar: " + string.Join(",", topBar.GetChildren().Select(c => c.Name.ToString())));
		screen.GetParent().QueueFree();
	}
}
