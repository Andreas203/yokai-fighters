using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed screenshot tool (not a test) for YOK-78 / YOK-108: the map screen on a real run session (the real pool and the default map)
/// at the start, and part-way through the walk (rows 1-4 done, health 840, the first card of each draft taken, row 5 reachable).
/// YOK-109: the map is a paper scroll panned inside the screen; every capture ends the ink / paint animation and snaps the pan first, so no
/// frame is half-drawn, and one more capture (map_midrun_walked) is panned 120 px in from the left end to show the walked brush path from its start.
/// Also the standalone way to run the screen for review: it needs no boot flow.
/// Run: godot --path game res://tests/captures/map_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt; [suffix=_720p] [stub=12]
/// </summary>
public partial class MapCapture : Node
{
	public override async void _Ready()
	{
		string dir = ".", suffix = ""; int stubRows = 0;
		foreach (string a in OS.GetCmdlineUserArgs())
		{
			if (a.StartsWith("out=")) dir = a[4..];
			if (a.StartsWith("suffix=")) suffix = a[7..];
			if (a.StartsWith("stub=")) stubRows = int.Parse(a[5..]);
		}
		var screen = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		AddChild(screen);
		_screen = screen;
		if (stubRows > 0)
		{
			screen.SetView(StubMapView.Grid(stubRows, 3));
			await Snap(dir + $"/map_stub_{stubRows}{suffix}.png");
			GetTree().Quit();
			return;
		}
		string md = ContentPaths.Data("map");
		var session = RunSession.Start(FightScene.LoadAbilityPool(), FileMapSource.Default(md).Load(), NodeSceneTable.Default(md), 5);
		screen.SetView(new SessionMapView(session));
		await Snap(dir + $"/map{suffix}.png");
		foreach (string id in new[] { "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant" })
		{
			session.Enter(id);
			if (session.Node!.Type != "merchant")
			{
				var d = session.WinFight(840);
				if (d != null) { var c = d.Offer.Cards[0]; d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null); }
			}
			session.CompleteNode();
		}
		session.Run.SetHealth(840);
		screen.SetView(new SessionMapView(session));
		await Snap(dir + $"/map_midrun{suffix}.png");
		screen.PanTo(120); // YOK-109: the same screen near the paper's left end (the first row and the walked brush path from its first stroke; 120 px in keeps the edge chevron and shade off the Merchant card)
		await Snap(dir + $"/map_midrun_walked{suffix}.png");

		// YOK-119: the chip's other states. Changed: another special overtakes the target, so the toast shows on the way back to the map.
		var fresh = RunSession.Start(FightScene.LoadAbilityPool(), FileMapSource.Default(md).Load(), NodeSceneTable.Default(md), 5);
		screen.SetView(new SessionMapView(fresh));
		var owned = fresh.Run.Owned.GetEnumerator(); owned.MoveNext(); owned.MoveNext();
		fresh.Run.LevelUp(owned.Current.Slot);
		screen.SetView(new SessionMapView(fresh));
		await Snap(dir + $"/map_copy_changed{suffix}.png");

		// Locked: the Tanuki's node entered (T1).
		while (fresh.Map.Graph.Node(fresh.Reachable()[0]).Type != "boss")
		{
			fresh.Enter(fresh.Reachable()[0]);
			if (fresh.Node!.Type is not ("merchant" or "dojo"))
			{
				var d = fresh.WinFight(840);
				if (d != null) { var c = d.Offer.Cards[0]; d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null); }
			}
			fresh.CompleteNode();
		}
		fresh.Enter(fresh.Reachable()[0]);
		screen.SetView(new SessionMapView(fresh));
		await Snap(dir + $"/map_copy_locked{suffix}.png");

		// None: no slot holds a special.
		var graph = FileMapSource.Default(md).Load();
		var bare = RunState.Restore(840, System.Array.Empty<(SpecialSlot, OwnedSpecial)>());
		bare.StartMap(graph);
		screen.SetView(new SessionMapView(new RunSession(bare, FightScene.LoadAbilityPool(), NodeSceneTable.Default(md), 5)));
		await Snap(dir + $"/map_copy_none{suffix}.png");

		// YOK-109: mid-animation frame (not kept in docs/screenshots/current): Ryo has just come back to the map, the choice is inking in and its strokes are painting.
		if (OS.GetCmdlineUserArgs().Contains("anim=1"))
		{
			var back = RunSession.Start(FightScene.LoadAbilityPool(), FileMapSource.Default(md).Load(), NodeSceneTable.Default(md), 5);
			foreach (string id in new[] { "r1-apprentice", "r2-oni", "r3-kappa" }) { back.Enter(id); var d = back.WinFight(840); if (d != null) { var c = d.Offer.Cards[0]; d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null); } back.CompleteNode(); }
			screen.SetView(new SessionMapView(back));
			screen.FinishPresentation(); screen.SnapScroll();
			back.Enter("r4-merchant"); back.CompleteNode();
			screen.SetView(new SessionMapView(back));
			await ToSignal(GetTree().CreateTimer(0.28), SceneTreeTimer.SignalName.Timeout);
			_screen = null; // no finishing: the frame as the player sees it a moment after returning
			await Snap(dir + $"/map_anim_midway{suffix}.png");
		}
		GetTree().Quit();
	}

	private MapScreen? _screen;

	private async System.Threading.Tasks.Task Snap(string path)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (_screen != null) { _screen.FinishPresentation(); _screen.SnapScroll(); }
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(path);
		GD.Print("saved " + path);
	}
}
