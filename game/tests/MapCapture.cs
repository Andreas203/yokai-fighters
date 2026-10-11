using Godot;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed screenshot tool (not a test) for YOK-78: the map screen on the default map at the start, and part-way through the walk.
/// Also the standalone way to run the screen for review: it needs no run flow, only a RunMap.
/// Run: godot --path game res://tests/map_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt; [stub=12]
/// </summary>
public partial class MapCapture : Node
{
	public override async void _Ready()
	{
		string dir = "."; int stubRows = 0;
		foreach (string a in OS.GetCmdlineUserArgs())
		{
			if (a.StartsWith("out=")) dir = a[4..];
			if (a.StartsWith("stub=")) stubRows = int.Parse(a[5..]);
		}
		var screen = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		AddChild(screen);
		if (stubRows > 0)
		{
			screen.SetView(StubMapView.Grid(stubRows, 3));
			await Snap(dir + $"/map_stub_{stubRows}.png");
			GetTree().Quit();
			return;
		}
		var run = new RunMap(FileMapSource.Default(ContentPaths.Data("map")).Load());
		screen.SetView(new RunMapView(run));
		await Snap(dir + "/map.png");
		foreach (string id in new[] { "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant" })
		{
			run.Select(id); run.Complete(id);
		}
		screen.Refresh();
		await Snap(dir + "/map_midrun.png");
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
