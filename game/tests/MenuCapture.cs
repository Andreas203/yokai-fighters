using Godot;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-58 screenshot tool (not a test): every menu mockup screen to a PNG.
/// Run: godot --path game res://tests/menu_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt; [only=name,name]
/// </summary>
public partial class MenuCapture : Node
{
	private static readonly (MockScreen Screen, string File)[] Shots =
	{
		(MockScreen.Title, "title"), (MockScreen.TitleNoSave, "title_no_saved_run"), (MockScreen.Pause, "pause"),
		(MockScreen.PauseConfirm, "pause_confirm"), (MockScreen.MoveList, "move_list"), (MockScreen.MoveListFrames, "move_list_frame_data"),
		(MockScreen.MoveListKata, "move_list_kata"), (MockScreen.Sound, "settings_sound"), (MockScreen.Controls, "settings_controls"),
	};

	public override async void _Ready()
	{
		string dir = ".", only = "";
		foreach (string a in OS.GetCmdlineUserArgs())
		{
			if (a.StartsWith("out=")) dir = a[4..];
			if (a.StartsWith("only=")) only = "," + a[5..] + ",";
		}
		var host = GD.Load<PackedScene>("res://scenes/ui/menu_mockups.tscn").Instantiate<MenuMockups>();
		AddChild(host);
		for (int i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		host.StripVisible = false;
		foreach (var (screen, file) in Shots)
		{
			if (only != "" && !only.Contains("," + file + ",")) continue;
			host.Show(screen);
			for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			string path = $"{dir}/{file}.png";
			GetViewport().GetTexture().GetImage().SavePng(path);
			GD.Print("saved " + path);
		}
		GetTree().Quit();
	}
}
