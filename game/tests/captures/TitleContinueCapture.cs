using System;
using System.IO;
using System.Threading.Tasks;
using Godot;
using YokaiFighters.Flow;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed capture tool (not a test) for YOK-73: the real boot scene's title on a temp save file, first with no
/// save (Continue greyed with its reason), then booted again after a run was saved inside its first node (Continue
/// lit with the run summary). Never touches the player's save or settings.
/// Run: godot --path game res://tests/captures/title_continue_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt;
/// </summary>
public partial class TitleContinueCapture : Node
{
	private string _dir = ".";

	public override async void _Ready()
	{
		foreach (string a in OS.GetCmdlineUserArgs())
			if (a.StartsWith("out=")) _dir = a[4..];
		string tmp = Path.Combine(Path.GetTempPath(), "yokai-title-capture-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tmp);
		try
		{
			var flow = Boot(tmp);
			await Snap("title_continue_none");
			flow.Router.Select(TitleEntry.Start);
			flow.Router.Confirm();
			flow.ChooseNode(flow.Session!.Reachable()[0]);
			flow.ReturnToTitle();
			RemoveChild(flow);
			flow.QueueFree();
			await Frames(2);
			Boot(tmp);
			await Snap("title_continue_saved");
		}
		catch (Exception e) { GD.PushError("TitleContinueCapture: " + e.Message); }
		finally { try { Directory.Delete(tmp, recursive: true); } catch (IOException) { } }
		GetTree().Quit();
	}

	private GameFlow Boot(string dir)
	{
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.SettingsFile = Path.Combine(dir, "title-capture-settings.json");
		flow.SaveFile = Path.Combine(dir, "title-capture-run.json");
		flow.FixedRunSeed = 73;
		AddChild(flow);
		return flow;
	}

	private async Task Frames(int n)
	{
		for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private async Task Snap(string name)
	{
		await Frames(30);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		string path = Path.Combine(_dir, name + ".png");
		GetViewport().GetTexture().GetImage().SavePng(path);
		GD.Print("TitleContinueCapture: saved " + path);
	}
}
