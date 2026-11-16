using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed screenshot tool (not a test) for the dojo trial (YOK-98, R7): a run walked to the row-7 dojo, its first offered
/// trial chosen and played on the fight scene. Snaps the opening (the trial's quest on its scroll), the fight after the scroll
/// is closed (the attempt strip) and the result.
/// Run: godot --path game res://tests/captures/trial_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt;
/// </summary>
public partial class TrialCapture : Node
{
	public override async void _Ready()
	{
		string dir = ".";
		foreach (string a in OS.GetCmdlineUserArgs()) if (a.StartsWith("out=")) dir = a[4..];
		var s = DojoTrialTests.AtDojo(500);
		var offer = s.Dojo!.Trials[0];
		s.Dojo.ChooseTrial(offer.Trial.Id);
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		scene.Session = s;
		scene.TrialStop = s.Dojo;
		AddChild(scene);
		await Snap(dir + "/trial_open.png");
		scene.TwistCard!.Continue();
		for (int t = 0; t < 30; t++) scene.Step(FighterInput.None, FighterInput.None);
		await Snap(dir + "/trial_attempt.png");
		int guard = 0;
		while (!scene.TrialDone && guard++ < 3 * offer.Trial.AttemptFrames + 10) scene.Step(FighterInput.None, FighterInput.None);
		await Snap(dir + "/trial_result.png");
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
