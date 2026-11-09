using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed screenshot tool (not a test) for YOK-218: the pause run panel and the move list holding 0, 1 and 2 cancel
/// rules (real data/cancels), the move list with frame data on, and the reward screen's cap notice.
/// Run: godot --path game res://tests/captures/held_cancel_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt; [suffix=_720p]
/// </summary>
public partial class HeldCancelCapture : Node
{
	public override async void _Ready()
	{
		string dir = ".", suffix = "";
		foreach (string a in OS.GetCmdlineUserArgs()) { if (a.StartsWith("out=")) dir = a[4..]; if (a.StartsWith("suffix=")) suffix = a[7..]; }
		var pool = FightScene.LoadAbilityPool();
		var layer = new CanvasLayer(); AddChild(layer);
		var back = new ColorRect { Color = new Color(0.16f, 0.12f, 0.1f) }; back.SetAnchorsPreset(Control.LayoutPreset.FullRect); layer.AddChild(back);
		var pause = GD.Load<PackedScene>("res://scenes/ui/pause_menu.tscn").Instantiate<PauseMenu>();
		layer.AddChild(pause); pause.FreezeTree = false; pause.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		for (int n = 0; n <= 2; n++)
		{
			var run = RunState.NewRun(pool);
			run.LevelUp(SpecialSlot.B); run.SetHealth(720);
			if (pool.Special("foxfire") is { } fox) run.Equip(fox);
			if (pool.Modifier("will-o-wisp") is { } wisp && run.CanAttach(SpecialSlot.C, wisp)) run.Attach(SpecialSlot.C, wisp);
			foreach (var c in pool.CancelRules.Where(x => x.Id != "undertow").Concat(pool.CancelRules.Where(x => x.Id == "undertow")).Take(n)) run.HoldCancelRule(c);
			pause.Bind(run, 4, 9, "Kitsune", ContentPaths.Data("moves"));
			pause.Open();
			await Snap(dir + $"/pause_cancel_{n}{suffix}.png");
			pause.OpenMoveList();
			await Snap(dir + $"/move_list_cancel_{n}{suffix}.png");
			if (n == 2) { pause.MoveList.ToggleFrameData(); await Snap(dir + $"/move_list_cancel_2_frames{suffix}.png"); pause.MoveList.ToggleFrameData(); }
			pause.Close();
		}
		pause.Visible = false;

		// Reward: an elder whose rule is held out (Ryo holds the other two).
		var elder = pool.CancelRules.First(c => c.Id == "fox-step");
		var full = RunState.NewRun(pool);
		foreach (var c in pool.CancelRules.Where(c => c.Id != elder.Id).Take(2)) full.HoldCancelRule(c);
		var reward = GD.Load<PackedScene>("res://scenes/ui/reward_screen.tscn").Instantiate<RewardScreen>(); layer.AddChild(reward);
		reward.Present(new DraftRewardSource(RewardDraft.Build(full, pool, elder.Source, 3, elder: true, elderId: elder.OfferedBy), full));
		await Snap(dir + $"/reward_cancel_cap{suffix}.png");
		GetTree().Quit();
	}

	private async System.Threading.Tasks.Task Snap(string path)
	{
		for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(path);
		GD.Print("saved " + path);
	}
}
