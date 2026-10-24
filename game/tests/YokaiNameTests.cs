using System;
using System.Linq;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Story;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-187: the screens name the yokai fought ("Oni", "Elder Oni"), never the stand-in body (the Kitsune) the Oni fights on.</summary>
public static class YokaiNameTests
{
	static StoryLibrary LibFor(ElderTwist t)
	{
		var ids = new[] { t.LabelKey, t.Id + "-label", t.IntroCard, t.CalloutCard }.Where(x => x != null).Select(x => x!).Distinct();
		return new(ids.Select(i => new StoryCardData(i, null, "{yokai} stands.", Array.Empty<string>(), "")));
	}

	static void WithNode(Node runner, string yokai, bool elder, Action<FightScene, FightHud> body)
	{
		var pool = FightScene.LoadAbilityPool();
		var jump = DevJump.BuildFight(new DevFightSetup(yokai, Elder: elder, Seed: 11), pool);
		Assert.True(jump.Session.Enter(jump.NodeId).Ok, "node entered");
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		scene.Session = jump.Session;
		scene.FirstMeeting = true;
		var savedStory = TwistCopy.Story;
		var twist = FightScene.Twists.For(elder ? DevJump.ElderOf(yokai)?.ElderYokai : yokai, elder);
		if (twist != null) TwistCopy.Story = LibFor(twist);
		runner.AddChild(scene);
		try
		{
			if (yokai != "tanuki") Assert.Equal("kitsune", FightScene.Opponent, "the body is still the Kitsune stand-in");
			body(scene, scene.GetNode<FightHud>("Hud"));
		}
		finally { TwistCopy.Story = savedStory; scene.QueueFree(); }
	}

	static void Check(Node runner, string yokai, bool elder, string name)
	{
		WithNode(runner, yokai, elder, (s, hud) =>
		{
			Assert.Equal(name, s.OpponentName, "scene name");
			Assert.Equal(name.ToUpperInvariant(), hud.OpponentLabel, "HUD health label");
			Assert.Equal(name, DevJump.NameOf(new DevFightSetup(yokai, Elder: elder)), "dev jump lose/demo-complete name");
			var pause = s.BuildPauseSummary();
			Assert.True(pause.Position.EndsWith("vs " + name), "pause line: " + pause.Position);
			Assert.Equal(name.ToUpperInvariant(), pause.Opponent.Name.ToUpperInvariant(), "pause health name");
			var (_, card) = FightScene.LoadStory().LoseScreen(name); // what ApplyLoseCard (FightScene._Ready) fills in
			Assert.True(card.Length > 0 && hud.LoseText == card, "lose screen holds the card filled with the yokai: " + hud.LoseText);
			Assert.True(hud.LoseText.Contains(name), "lose screen names " + name + ": " + hud.LoseText);
		});
	}

	[Test] public static void OniNode_ShowsOni(Node runner) => Check(runner, "oni", false, "Oni");
	[Test] public static void ElderNode_ShowsElderOni(Node runner) => Check(runner, "oni", true, "Elder Oni");

	[Test] public static void TanukiNode_ShowsTanuki(Node runner) => Check(runner, "tanuki", false, "Tanuki");
	/// <summary>Whatever BindingName yields today for the Kitsune's elder (designer and habit-writer own the string).</summary>
	[Test] public static void KitsuneElder_ShowsItsBindingName(Node runner) =>
		Check(runner, "kitsune", true, StoryLibrary.DisplayName(DevJump.ElderOf("kitsune")!.ElderYokai));
	[Test]
	public static void ApprenticeNode_ShowsKitsune(Node runner)
	{
		var saved = (FightScene.Sources, FightScene.Run);
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		try
		{
			Assert.Equal("Kitsune", scene.OpponentName, "apprentice");
			Assert.Equal("KITSUNE", scene.GetNode<FightHud>("Hud").OpponentLabel, "HUD");
			Assert.True(scene.GetNode<FightHud>("Hud").LoseText.Contains("Kitsune"), "lose text");
		}
		finally { scene.QueueFree(); (FightScene.Sources, FightScene.Run) = saved; }
	}

	[Test]
	public static void ElderNode_TwistSurfacesNameTheElder(Node runner) => WithNode(runner, "oni", true, (s, hud) =>
	{
		var t = s.Twist!;
		hud.Refresh(s.Match);
		Assert.Equal("Elder Oni stands.", hud.TwistLine, "strip");
		Assert.Equal("Elder Oni stands.", s.TwistCard!.Text, "intro card");
		s.TwistCard.Continue();
		Assert.Equal("Elder Oni stands.", hud.TwistCalloutText, "callout");
	});
}
