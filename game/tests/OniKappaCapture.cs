using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// PLAYTEST windowed screenshot tool (not a test): a dev-jump Oni fight, a Kappa fight and an Elder Oni fight,
/// walked in to close range. Run: godot --path game res://tests/oni_kappa_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt;
/// </summary>
public partial class OniKappaCapture : Node
{
	private string _dir = ".";

	public override async void _Ready()
	{
		foreach (string a in OS.GetCmdlineUserArgs())
			if (a.StartsWith("out=")) _dir = a[4..];
		var pool = FightScene.LoadAbilityPool();
		foreach (var (yokai, elder) in new[] { ("oni", false), ("kappa", false), ("oni", true) })
		{
			var jump = DevJump.BuildFight(new DevFightSetup(yokai, Elder: elder), pool);
			if (!jump.Session.Enter(jump.NodeId).Ok) { GD.PushError($"{yokai}: node not entered"); continue; }
			var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			scene.ExternalDrive = true;
			scene.Session = jump.Session;
			AddChild(scene);
			for (int t = 0; t < 40; t++) scene.Step(new FighterInput(InputBits.Right), new FighterInput(InputBits.Left));
			for (int t = 0; t < 30; t++) scene.Step(FighterInput.None, FighterInput.None);
			string name = (elder ? "elder-" : "") + yokai + "-fight";
			GD.Print($"{name}: P2 body {scene.ModelOf(1)?.Fighter}, kit {FightScene.Opponent}, pose {scene.PresenterOf(1)?.Sample}");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			GetViewport().GetTexture().GetImage().SavePng($"{_dir}/{name}.png");
			scene.QueueFree();
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
		GetTree().Quit();
	}
}
