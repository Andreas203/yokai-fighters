using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>Windowed screenshot of the start screen: godot --path game res://tests/start_capture.tscn --resolution 1920x1080 -- out=&lt;png&gt;</summary>
public partial class StartCapture : Node
{
	public override async void _Ready()
	{
		string path = "start.png";
		foreach (string a in OS.GetCmdlineUserArgs()) if (a.StartsWith("out=")) path = a[4..];
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.StartScreenOverride = true;
		AddChild(scene);
		for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(path);
		GD.Print("saved " + path);
		GetTree().Quit();
	}
}
