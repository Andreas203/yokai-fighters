using Godot;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed screenshot tool (not a test) for YOK-52: binding line, three cards, frame-data view, modifier target step.
/// Run: godot --path game res://tests/reward_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt;
/// </summary>
public partial class RewardCapture : Node
{
	public override async void _Ready()
	{
		string dir = ".";
		foreach (string a in OS.GetCmdlineUserArgs()) if (a.StartsWith("out=")) dir = a[4..];
		var layer = new CanvasLayer(); AddChild(layer);
		var src = new StubRewardSource();
		var story = new StoryCard(); layer.AddChild(story);
		var reward = new RewardScreen(); layer.AddChild(reward);

		story.Display(src.BindingLine(src.Offer.Yokai));
		await Snap(dir + "/reward_binding.png");
		story.Continue();
		reward.Present(src);
		await Snap(dir + "/reward_cards.png");
		reward.Move(1); reward.ToggleFrameData();
		await Snap(dir + "/reward_framedata.png");
		reward.ToggleFrameData(); reward.Move(1); reward.Confirm();
		await Snap(dir + "/reward_target.png");
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
