using Godot;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Tests;

/// <summary>
/// Screenshot tool (not a test): both pages of the kit gallery, written over docs/screenshots/current/kit/.
/// Run: godot --path game res://tests/kit_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt;   (and again with --resolution 1280x720)
/// Also runs <see cref="KitGallery.Audit"/> on both pages and exits 1 on any problem.
/// </summary>
public partial class KitCapture : Node
{
	public override async void _Ready()
	{
		string dir = ".";
		foreach (string a in OS.GetCmdlineUserArgs()) if (a.StartsWith("out=")) dir = a[4..];
		string tag = GetWindow().Size.Y >= 1000 ? "1080p" : "720p";
		var g = GD.Load<PackedScene>("res://scenes/ui/kit/kit_gallery.tscn").Instantiate<KitGallery>();
		AddChild(g);
		int problems = 0;
		for (int page = 0; page < 2; page++)
		{
			g.ShowPage(page);
			for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			foreach (var p in KitGallery.Audit(g.PageRoot(page))) { GD.PrintErr("AUDIT " + p); problems++; }
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			string path = $"{dir}/gallery_{(page == 0 ? "a" : "b")}_{tag}.png";
			GetViewport().GetTexture().GetImage().SavePng(path);
			GD.Print("saved " + path);
		}
		GD.Print($"AUDIT problems: {problems}");
		GetTree().Quit(problems == 0 ? 0 : 1);
	}
}
