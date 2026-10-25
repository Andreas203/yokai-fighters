using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-131 windowed screenshot tool (not a test): a dev-jump Oni fight, a Kappa fight and an Elder Oni fight,
/// walked in to close range, plus Oni stills mid-swing (MP, the club swing), in the heavy (HP, the overhead club) and the heavy kick (his highest club point), and Ryo vs the Kitsune (YOK-133 framing comparison). Run: godot --path game res://tests/captures/oni_kappa_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt;
/// </summary>
public partial class OniKappaCapture : Node
{
	private string _dir = ".";

	public override async void _Ready()
	{
		foreach (string a in OS.GetCmdlineUserArgs())
			if (a.StartsWith("out=")) _dir = a[4..];
		var pool = FightScene.LoadAbilityPool();
		// YOK-133: plus Ryo vs the Kitsune (default framing, for comparison) and the Oni's heavy kick, whose club swing is his highest point.
		foreach (var (yokai, elder, move, frame, shot) in new (string, bool, string?, int, string?)[]
		{
			("oni", false, null, 0, null), ("kappa", false, null, 0, null), ("oni", true, null, 0, null),
			("oni", false, "kitsune-medium-punch", 10, "oni-mp-swing"), ("oni", false, "kitsune-heavy-punch", 9, "oni-hp-overhead"),
			("oni", false, "kitsune-heavy-kick", 11, "oni-hk-club"),
			("kitsune", false, null, 0, "ryo-vs-kitsune-fight"), ("kitsune", false, "kitsune-heavy-punch", 9, "ryo-vs-kitsune-hp"),
		})
		{
			var jump = DevJump.BuildFight(new DevFightSetup(yokai, Elder: elder), pool);
			if (!jump.Session.Enter(jump.NodeId).Ok) { GD.PushError($"{yokai}: node not entered"); continue; }
			var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			scene.ExternalDrive = true;
			scene.Session = jump.Session;
			AddChild(scene);
			for (int t = 0; t < 40; t++) scene.Step(new FighterInput(InputBits.Right), new FighterInput(InputBits.Left));
			for (int t = 0; t < 30; t++) scene.Step(FighterInput.None, FighterInput.None);
			string name = shot ?? (elder ? "elder-" : "") + yokai + "-fight";
			if (move != null)
			{
				int slot = System.Array.FindIndex(scene.Match.P2.Moves, m => m.Id == move);
				scene.Step(FighterInput.None, FighterInput.Attack(slot));
				while (scene.Match.P2.State == FighterState.Attack && scene.Match.P2.MoveFrame < frame)
					scene.Step(FighterInput.None, FighterInput.None);
			}
			var (cx, cy, cz, cp) = scene.Cam.Transform;
			GD.Print($"YOK-133 {name}: camera top {scene.Cam.Config.ModelTopMeters} m, width {scene.Cam.Width:0.###}, rig ({cx:0.####}, {cy:0.####}, {cz:0.####}) pitch {cp:0.#####}");
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
