using System.Threading.Tasks;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-123 screenshot tool (not a test): the DEV badge and the dev menu on the title, over the pause menu, and in a fight.
/// Run: godot --path game res://tests/captures/dev_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt;
/// The boot scene is instanced (not the current scene), so no save or settings file is read or written.
/// </summary>
public partial class DevCapture : Node
{
	private string _dir = ".";

	public override async void _Ready()
	{
		foreach (string a in OS.GetCmdlineUserArgs()) if (a.StartsWith("out=")) _dir = a[4..];
		DevMode.Configure(new DevModeState(true, true));
		var shell = DevShell.Instance!;

		var flow = Boot(null);
		await Frames(10);
		await Shot("dev_title_badge");
		flow.OpenDevMenu();
		await Shot("dev_menu_tools");
		shell.Menu.ShowSection(DevMenu.MapId);
		await Shot("dev_menu_map_stub");
		shell.Menu.Close();
		flow.Router.Select(TitleEntry.Start);
		flow.Router.Confirm();
		await Frames(10);
		await Shot("dev_map_badge");
		RemoveChild(flow);
		flow.QueueFree();

		flow = Boot(ControlScheme.Kihon);
		await Frames(20);
		await Shot("dev_fight_badge");
		flow.Fight!.TryPause();
		await Shot("dev_pause_entry");
		flow.Fight.OpenDevMenu();
		await Shot("dev_menu_over_pause");
		shell.Menu.Close();
		GetTree().Quit();
	}

	private GameFlow Boot(ControlScheme? direct)
	{
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.DirectFight = direct;
		AddChild(flow);
		return flow;
	}

	private async Task Frames(int n)
	{
		for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private async Task Shot(string file)
	{
		await Frames(4);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		string path = $"{_dir}/{file}.png";
		GetViewport().GetTexture().GetImage().SavePng(path);
		GD.Print("saved " + path);
	}
}
