using Godot;
using YokaiFighters.Ui;
using YokaiFighters.Ui.Kit;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>Probes for the replace step, its inputs, and the apprentice portrait (decision log 2026-11-15).</summary>
public static class DojoScrollReplaceProbes
{
	sealed class Src : IRewardSource
	{
		public (int Card, string? Target)? LastPick { get; private set; }
		public int Picks;
		public RewardOffer Offer { get; } = new("Oni", new[]
		{
			new CardView(CardKind.NewMove, "iron-club-charge", "Iron-Club Charge", "Oni - special", "p", "f", "NEW - Lv 1")
				{ Replaces = new TargetView("rising-talisman", "C", "Rising Talisman", 2, "Fox's Patience", true) { Note = "Loses its levels and Fox's Patience" } },
			new CardView(CardKind.Modifier, "will-o-wisp", "Will-o'-wisp", "Kitsune - modifier", "p", "f", "MODIFIER", "Pick the special"),
		}, new[]
		{
			new TargetView("spirit-wave", "A", "Spirit Wave", 1, null, true),
			new TargetView("foxfire", "B", "Foxfire", 1, null, true),
			new TargetView("rising-talisman", "C", "Rising Talisman", 2, null, true),
		});
		public void Pick(int c, string? t) { LastPick = (c, t); Picks++; }
	}

	static (RewardScreen, Src) Make(Node host)
	{
		var src = new Src();
		var s = GD.Load<PackedScene>("res://scenes/ui/reward_screen.tscn").Instantiate<RewardScreen>();
		host.AddChild(s);
		s.Present(src);
		return (s, src);
	}

	static void Key(RewardScreen s, Key k)
	{
		s.GetViewport().PushInput(new InputEventKey { Keycode = k, Pressed = true });
	}

	// Rule A6 + designer 2026-11-15: backing out of the replace step then taking a modifier must show the offer's own targets.
	[Test]
	public static void BackFromReplace_ThenModifier_ShowsTheOfferTargetsNotTheReplacedMove(Node host)
	{
		var (s, src) = Make(host);
		try
		{
			s.Confirm(); s.Back();
			s.Move(1); s.Confirm();
			Assert.Equal(RewardStep.Target, s.Step, "modifier opens target step");
			Assert.Equal("Spirit Wave", UiFind.Get<Label>(s.GetNode<TargetCard>("Targets/Target1"), "Name").Text, "target 1 is the offer's slot A");
			Assert.True(s.GetNode<TargetCard>("Targets/Target3").Visible, "three targets shown again");
			Assert.Equal("No modifier yet", UiFind.Get<Label>(s.GetNode<TargetCard>("Targets/Target1"), "Note").Text, "modifier note wording is back");
			Assert.True(src.LastPick is null, "nothing picked");
		}
		finally { s.Free(); }
	}

	// Escape through the real input path backs out of the replace step without picking.
	[Test]
	public static void EscapeKeyInReplaceStep_BacksOutAndPicksNothing(Node host)
	{
		var (s, src) = Make(host);
		try
		{
			s.Confirm();
			Key(s, Godot.Key.Escape);
			Assert.True(s.Step == RewardStep.Cards && src.Picks == 0 && s.Showing, "Esc backs out");
		}
		finally { s.Free(); }
	}

	// A right-click in the card step is not Back and does not pick; after the screen closes it does nothing.
	[Test]
	public static void RightClickOutsideTargetSteps_DoesNothing(Node host)
	{
		var (s, src) = Make(host);
		try
		{
			var p = new Vector2(10, 10);
			s.GetViewport().PushInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Right, Position = p, GlobalPosition = p });
			Assert.True(s.Step == RewardStep.Cards && src.Picks == 0, "right-click in card step is inert");
			s.Confirm(); s.Confirm();
			Assert.Equal(1, src.Picks, "replace confirmed once");
			s.GetViewport().PushInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Right, Position = p, GlobalPosition = p });
			Assert.Equal(1, src.Picks, "no extra pick after closing");
			Assert.True(!s.Showing, "stays closed");
		}
		finally { s.Free(); }
	}

	// A new Present while in the replace step starts at the cards again, with the offer's targets bound.
	[Test]
	public static void PresentWhileInReplaceStep_ResetsToCards(Node host)
	{
		var (s, src) = Make(host);
		try
		{
			s.Confirm();
			s.Present(new Src());
			Assert.Equal(RewardStep.Cards, s.Step, "back at cards");
			Assert.Equal(0, s.CardCursor, "cursor reset");
			s.Move(1); s.Confirm();
			Assert.Equal("Spirit Wave", UiFind.Get<Label>(s.GetNode<TargetCard>("Targets/Target1"), "Name").Text, "offer targets bound");
		}
		finally { s.Free(); }
	}

	// Replace step: the heading names the slot the card goes in and the replaced move's level (A6).
	[Test]
	public static void ReplaceStep_HeadingNamesSlotAndLevel(Node host)
	{
		var (s, _) = Make(host);
		try
		{
			s.Confirm();
			string h = UiFind.Get<Label>(s, "TargetHeading").Text;
			Assert.True(h.Contains("slot C") && h.Contains("Lv 2") && h.Contains("Rising Talisman"), $"heading '{h}'");
			Assert.Equal("Loses its levels and Fox's Patience", UiFind.Get<Label>(s.GetNode<TargetCard>("Targets/Target1"), "Note").Text, "modifier loss named");
		}
		finally { s.Free(); }
	}
}
