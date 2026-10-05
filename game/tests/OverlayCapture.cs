using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed screenshot tool (not a test) for the YOK-22 debug overlay: walks the fighters in, P1 jabs (whiffs)
/// while P2 sweeps, pauses on the jab's first active frame with boxes shown, saves a PNG, quits.
/// Run: godot --path game res://tests/overlay_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt;
/// </summary>
public partial class OverlayCapture : Node
{
	public override async void _Ready()
	{
		string dir = ".";
		foreach (string a in OS.GetCmdlineUserArgs()) if (a.StartsWith("out=")) dir = a[4..];
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		AddChild(scene);
		var overlay = scene.GetNode<DebugOverlay>("DebugOverlay");
		overlay.HandleKey(DebugOverlay.ToggleKey);
		overlay.HandleKey(DebugOverlay.PauseKey);
		var moves = scene.Match.P1.Moves;
		int jab = System.Array.FindIndex(moves, m => m.Id == "test-jab");
		int sweep = System.Array.FindIndex(moves, m => m.Id == "test-sweep");
		for (int t = 0; t < 15; t++) scene.Step(new FighterInput(InputBits.Right), new FighterInput(InputBits.Left));
		scene.Step(FighterInput.Attack(jab), FighterInput.Attack(sweep));
		for (int t = 0; t < 4; t++) scene.Step(FighterInput.None, FighterInput.None);
		await Snap(dir + "/overlay.png");
		GetTree().Quit();
	}

	private async System.Threading.Tasks.Task Snap(string path)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(path);
		GD.Print("saved " + path);
	}
}
