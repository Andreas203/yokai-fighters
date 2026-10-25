using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;
using SimCardKind = YokaiFighters.Sim.CardKind;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed screenshot tool (not a test) for YOK-186: the row-5 Elder Oni's reward with its cancel rule (Oni Chain) as a
/// rare card, with frame data on, and a rare second special. Real data for the cancel rule (a walked run on the default
/// map); the rare special is a TEST FIXTURE clone made here (no yokai has a second special in data/ yet).
/// Run: godot --path game res://tests/captures/elder_reward_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt; [suffix=_720p]
/// </summary>
public partial class ElderRewardCapture : Node
{
	public override async void _Ready()
	{
		string dir = ".", suffix = "";
		foreach (string a in OS.GetCmdlineUserArgs()) { if (a.StartsWith("out=")) dir = a[4..]; if (a.StartsWith("suffix=")) suffix = a[7..]; }
		var layer = new CanvasLayer(); AddChild(layer);
		var reward = GD.Load<PackedScene>("res://scenes/ui/reward_screen.tscn").Instantiate<RewardScreen>(); layer.AddChild(reward);

		// 1. A walked run to the row-5 Elder Oni: its offer holds Oni Chain (card 0, the cursor's card).
		var pool = FightScene.LoadAbilityPool();
		var (draft, run) = ElderOffer(pool, seed: 5);
		reward.Present(new DraftRewardSource(draft, run));
		await Snap(dir + "/reward_elder_cancel" + suffix + ".png");
		reward.ToggleFrameData();
		await Snap(dir + "/reward_elder_cancel_frames" + suffix + ".png");
		reward.ToggleFrameData();

		// 2. A rare second special (TEST FIXTURE clone, rarity rare, source oni) beside the cancel rule.
		var rare = RareFixture();
		var withRare = new AbilityPool(pool.Specials.Append(rare), pool.Modifiers, pool.Notes, pool.CancelRules);
		for (uint seed = 1; seed < 64; seed++)
		{
			var d = RewardDraft.Build(RunState.NewRun(withRare), withRare, "oni", seed, elder: true, elderId: "elder-oni");
			int at = d.Offer.Cards.ToList().FindIndex(c => c.Kind == SimCardKind.NewSpecial && c.Rare);
			if (at < 0) continue;
			reward.Present(new DraftRewardSource(d, RunState.NewRun(withRare)));
			reward.Move(at);
			await Snap(dir + "/reward_rare_special" + suffix + ".png");
			break;
		}
		GetTree().Quit();
	}

	static (RewardDraft Draft, RunState Run) ElderOffer(AbilityPool pool, uint seed)
	{
		string mapDir = ContentPaths.Data("map");
		var s = RunSession.Start(pool, FileMapSource.Default(mapDir).Load(), NodeSceneTable.Default(mapDir), seed);
		foreach (string id in new[] { "r1-apprentice", "r2-oni", "r3-oni", "r4-merchant" })
		{
			s.Enter(id);
			if (s.Node!.Type is not (NodeTypeRegistry.Merchant or NodeTypeRegistry.Dojo) && s.WinFight(600) is { } d)
				d.Pick(0, d.Offer.Cards[0].Targets.Count > 0 ? d.Offer.Cards[0].Targets[0].Slot : null);
			s.CompleteNode();
		}
		s.Enter("r5-elder-oni");
		return (s.WinFight(600)!, s.Run);
	}

	/// <summary>TEST FIXTURE: the fixture Foxfire as a rare Oni special in slot D (as MerchantShopTests' clones).</summary>
	static SpecialData RareFixture()
	{
		var node = JsonNode.Parse(File.ReadAllText(Path.Combine(ContentPaths.FixturesDir, "rewards", "test-foxfire.json")))!.AsObject();
		node["id"] = "test-oni-rare";
		node["name"] = "Test Rare Special";
		node["source"] = "oni";
		node["rarity"] = "rare";
		node["input"]!["kata"]!["slot"] = "D";
		node["input"]!["kihon"]!["slot"] = "D";
		return SpecialLoader.Parse(node.ToJsonString(), "test-oni-rare");
	}

	private async System.Threading.Tasks.Task Snap(string path)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(path);
		GD.Print("saved " + path);
	}
}
