using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed screenshot tool (not a test) for YOK-197 practice mode: the fight scene in practice against the dojo
/// dummy set to Block, Ryo walked in and throwing a Spirit Wave, the YOK-22 boxes on, frozen on a guarded hit.
/// Run: godot --path game res://tests/practice_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt;
/// </summary>
public partial class PracticeCapture : Node
{
	public override async void _Ready()
	{
		string dir = ".";
		foreach (string a in OS.GetCmdlineUserArgs()) if (a.StartsWith("out=")) dir = a[4..];
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		scene.PracticeMode = true;
		AddChild(scene);
		scene.HandlePracticeKey(FightScene.PracticeStanceKey); // Block
		scene.GetNode<DebugOverlay>("DebugOverlay").HandleKey(DebugOverlay.ToggleKey);
		bool blocked = false;
		scene.Match.Hit += (_, e) => blocked |= e.Blocked;
		for (int t = 0; t < 40; t++) scene.Step(new FighterInput(InputBits.Right), FighterInput.None);
		scene.Step(FighterInput.Special(SpecialSlot.A), FighterInput.None);
		for (int t = 0; t < 300 && !blocked; t++) scene.Step(FighterInput.None, FighterInput.None);
		for (int t = 0; t < 2; t++) scene.Step(FighterInput.None, FighterInput.None);
		await Snap(dir + "/practice.png");
		GetTree().Quit();
	}

	private async System.Threading.Tasks.Task Snap(string path)
	{
		for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(path);
		GD.Print("saved " + path);
	}
}
