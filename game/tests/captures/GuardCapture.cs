using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// Finish-build guard screenshot tool (not a test): Ryo holding down-back and back while the Kitsune's heavy punch is in
/// startup (the presenter's proximity guard), Ryo in low blockstun, and the Tanuki true form in the low guard and low blockstun.
/// Run: godot --path game res://tests/captures/guard_capture.tscn --resolution 1920x1080 --audio-driver Dummy -- out=&lt;dir&gt;
/// </summary>
public partial class GuardCapture : Node
{
	private string _dir = ".";

	public override async void _Ready()
	{
		foreach (string a in OS.GetCmdlineUserArgs())
			if (a.StartsWith("out=")) _dir = a[4..];
		var pool = FightScene.LoadAbilityPool();
		// (opponent, defender is P2, hold down, walk in first, stop in blockstun, still name)
		foreach (var (yokai, p2Guards, down, close, stun, name) in new (string, bool, bool, bool, bool, string)[]
		{
			("kitsune", false, true, false, false, "ryo-down-back-vs-attack"),
			("kitsune", false, false, false, false, "ryo-back-vs-attack"),
			("kitsune", false, true, true, true, "ryo-low-blockstun"),
			("tanuki", true, true, false, false, "tanuki-down-back-vs-attack"),
			("tanuki", true, true, true, true, "tanuki-low-blockstun"),
		})
		{
			var jump = DevJump.BuildFight(new DevFightSetup(yokai), pool);
			if (!jump.Session.Enter(jump.NodeId).Ok) { GD.PushError($"{yokai}: node not entered"); continue; }
			var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			scene.ExternalDrive = true;
			scene.Session = jump.Session;
			AddChild(scene);
			if (close)
			{
				for (int t = 0; t < 40; t++) scene.Step(new FighterInput(InputBits.Right), new FighterInput(InputBits.Left));
				for (int t = 0; t < 30; t++) scene.Step(FighterInput.None, FighterInput.None);
			}
			int def = p2Guards ? 1 : 0;
			Fighter attacker = scene.Match.Fighters[1 - def], defender = scene.Match.Fighters[def];
			var hold = new FighterInput((def == 0 ? InputBits.Left : InputBits.Right) | (down ? InputBits.Down : InputBits.None));
			string move = def == 0 ? "kitsune-heavy-punch" : "ryo-heavy-punch";
			int slot = System.Array.FindIndex(attacker.Moves, m => m.Id == move);
			FighterInput A(FighterInput d, FighterInput a) => def == 0 ? d : a;
			FighterInput B(FighterInput d, FighterInput a) => def == 0 ? a : d;
			for (int t = 0; t < 4; t++) scene.Step(A(hold, FighterInput.None), B(hold, FighterInput.None));
			scene.Step(A(hold, FighterInput.Attack(slot)), B(hold, FighterInput.Attack(slot)));
			if (stun)
			{
				int seen = 0;
				for (int t = 0; t < 90 && seen < 6; t++)
				{
					scene.Step(A(hold, FighterInput.None), B(hold, FighterInput.None));
					if (defender.State == FighterState.Blockstun) seen++;
				}
			}
			else
				while (attacker.State == FighterState.Attack && attacker.MoveFrame < attacker.CurrentMove!.Startup)
					scene.Step(A(hold, FighterInput.None), B(hold, FighterInput.None));
			GD.Print($"guard {name}: defender {defender.State} guarding {defender.Guarding} crouching {defender.Crouching}, attacker frame {attacker.MoveFrame}, pose {scene.PresenterOf(def)?.Sample}");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			GetViewport().GetTexture().GetImage().SaveJpg($"{_dir}/{name}.jpg", 0.88f);
			scene.QueueFree();
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
		GetTree().Quit();
	}
}
