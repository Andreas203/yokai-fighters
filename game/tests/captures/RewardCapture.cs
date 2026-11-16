using System.Linq;
using Godot;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed screenshot tool (not a test) for YOK-52: binding line, three cards, frame-data view, modifier target step; and
/// (designer 2026-11-15) an Oni offer to a run holding Foxfire: the card that would replace it, then the replace step.
/// Run: godot --path game res://tests/captures/reward_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt;
/// </summary>
public partial class RewardCapture : Node
{
	public override async void _Ready()
	{
		string dir = ".";
		foreach (string a in OS.GetCmdlineUserArgs()) if (a.StartsWith("out=")) dir = a[4..];
		var layer = new CanvasLayer(); AddChild(layer);
		var src = new StubRewardSource();
		var story = GD.Load<PackedScene>("res://scenes/ui/story_card.tscn").Instantiate<StoryCard>(); layer.AddChild(story);
		var reward = GD.Load<PackedScene>("res://scenes/ui/reward_screen.tscn").Instantiate<RewardScreen>(); layer.AddChild(reward);

		story.Display(src.BindingLine(src.Offer.Yokai));
		await Snap(dir + "/reward_binding.png");
		story.Continue();
		reward.Present(src);
		await Snap(dir + "/reward_cards.png");
		reward.Move(1); reward.ToggleFrameData();
		await Snap(dir + "/reward_framedata.png");
		reward.ToggleFrameData(); reward.Move(1); reward.Confirm();
		await Snap(dir + "/reward_target.png");
		reward.Back();
		var real = ReplacingOffer();
		if (real != null)
		{
			reward.Present(real);
			int at = System.Array.FindIndex(real.Offer.Cards.ToArray(), c => c.Replaces != null);
			for (int i = 0; i < at; i++) reward.Move(1);
			await Snap(dir + "/reward_replace_card.png");
			reward.Confirm();
			await Snap(dir + "/reward_replace.png");
		}
		GetTree().Quit();
	}

	/// <summary>A real Oni draft for a run that drafted Foxfire (slot C), on the first seed that offers a slot-C special.</summary>
	static DraftRewardSource? ReplacingOffer()
	{
		string root = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
		var pool = YokaiFighters.Sim.AbilityPool.LoadRepo(root);
		for (uint seed = 1; seed < 40; seed++)
		{
			var run = YokaiFighters.Sim.RunState.NewRun(pool);
			run.Equip(pool.Special("foxfire")!);
			var src = new DraftRewardSource(YokaiFighters.Sim.RewardDraft.Build(run, pool, "oni", seed), run);
			if (System.Linq.Enumerable.Any(src.Offer.Cards, c => c.Replaces != null)) return src;
		}
		return null;
	}

	private async System.Threading.Tasks.Task Snap(string path)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(path);
		GD.Print("saved " + path);
	}
}
