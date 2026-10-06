using Godot;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-52: reward screen against the stub source: cards from data, picks reach the source, toggle, modifier target step.</summary>
public static class RewardScreenTests
{
	static (RewardScreen, StubRewardSource) Make(Node host)
	{
		var src = new StubRewardSource();
		var s = GD.Load<PackedScene>("res://scenes/ui/reward_screen.tscn").Instantiate<RewardScreen>();
		host.AddChild(s);
		s.Present(src);
		return (s, src);
	}

	[Test]
	public static void NewCardPickReturnsIndex(Node host)
	{
		var (s, src) = Make(host);
		(int, string?)? got = null;
		s.Picked += (c, t) => got = (c, t);
		s.Confirm();
		Assert.Equal((0, (string?)null), src.LastPick!.Value, "source got pick");
		Assert.Equal((0, (string?)null), got!.Value, "signal");
		Assert.True(!s.Showing, "hidden after pick");
		s.QueueFree();
	}

	[Test]
	public static void ModifierNeedsEligibleTarget(Node host)
	{
		var (s, src) = Make(host);
		s.Move(2); s.Confirm();
		Assert.Equal(RewardStep.Target, s.Step, "opens target step");
		Assert.True(src.LastPick == null, "no pick yet");
		s.Move(1); s.Confirm(); // slot B is not eligible
		Assert.True(src.LastPick == null, "ineligible refused");
		s.Back();
		Assert.Equal(RewardStep.Cards, s.Step, "back");
		s.Confirm(); s.Confirm();
		Assert.Equal((2, (string?)"spirit-wave"), src.LastPick!.Value, "modifier + target");
		s.QueueFree();
	}

	[Test]
	public static void FrameDataToggle(Node host)
	{
		var (s, _) = Make(host);
		Assert.True(!s.FrameDataVisible, "off by default");
		s.ToggleFrameData();
		Assert.True(s.FrameDataVisible, "on");
		s.ToggleFrameData();
		Assert.True(!s.FrameDataVisible, "off");
		s.QueueFree();
	}

	[Test]
	public static void BindingLineTemplate()
	{
		Assert.Equal("Forgive me, Kitsune. I'll give it back.", new StubRewardSource().BindingLine("Kitsune"), "S3 line");
	}
}
