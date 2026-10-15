using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed screenshot tool (not a test) for YOK-78 / YOK-108: the map screen on a real run session (the real pool and the default map)
/// at the start, and part-way through the walk (rows 1-4 done, health 840, the first card of each draft taken, row 5 reachable).
/// Also the standalone way to run the screen for review: it needs no boot flow.
/// Run: godot --path game res://tests/map_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt; [suffix=_720p] [stub=12]
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
		GetTree().Quit();
	}

	private async System.Threading.Tasks.Task Snap(string path)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(path);
		GD.Print("saved " + path);
	}
}
