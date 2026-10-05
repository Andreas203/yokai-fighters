using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed screenshot tool (not a test): renders the fight HUD mid-fight and on the lose screen, saves PNGs, quits.
/// Run: godot --path game res://tests/hud_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt;
/// </summary>
public partial class HudCapture : Node
{
	public override async void _Ready()
	{
		string dir = ".";
		foreach (string a in OS.GetCmdlineUserArgs()) if (a.StartsWith("out=")) dir = a[4..];
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		AddChild(scene);
		for (int i = 0; i < 4; i++) { scene.Step(new FighterInput(InputBits.DebugStrike), FighterInput.None); scene.Step(FighterInput.None, FighterInput.None); }
		await Snap(dir + "/hud_fight.png");
		for (int t = 0; t < 600 && scene.Match.Phase != MatchPhase.Over; t++)
			scene.Step(FighterInput.None, new FighterInput(t % 2 == 0 ? InputBits.DebugStrike : InputBits.None));
		await Snap(dir + "/hud_lose.png");
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
