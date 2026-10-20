using System;
using System.Linq;
using Godot;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-109 dojo selector. Expected values from R7, T2, T4, A4.</summary>
public static class DojoSelectorProbes
{
	static InputEventKey Key(Godot.Key k) => new() { Pressed = true, Keycode = k };

	static RestStopScreen Open(Node runner, RunSession s)
	{
		var screen = GD.Load<PackedScene>("res://scenes/ui/rest_stop.tscn").Instantiate<RestStopScreen>();
		screen.LineSource = _ => "A line.";
		screen.BindRun(s, ControlScheme.Kihon);
		runner.AddChild(screen);
		return screen;
	}

	// R7: once the trial is taken, the card shows the chosen trial, even after a landed trial shrinks the offers.
	[Test]
	public static void TrialOffer_StaysOnTheChosenSpecial_AfterALandedTrialShrinksOffers()
	{
		var s = DojoTrialTests.AtDojo(500);
		var m = RestStopScreenModel.Create(s)!;
		m.SelectTrial(-1);
		var slot = m.TrialOffer!.Slot;
		while (s.Run[slot]!.Level < 2 && s.Run.CanLevelUp(slot)) s.Run.LevelUp(slot);
		m = RestStopScreenModel.Create(s)!;
		int idx = m.Offers.ToList().FindIndex(o => o.Slot == slot);
		Assert.True(idx >= 0, "still offered at Lv 2");
		while (m.SelectedIndex != idx) m.SelectTrial(1);
		Assert.True(m.ChooseTrial().Ok, "taken");
		Assert.True(s.Dojo!.RecordAttempt(true).Ok, "landed");
		Assert.Equal(slot, s.Dojo.TrialSlot, "trial slot");
		Assert.True(m.TrialOffer is null || m.TrialOffer.Slot == slot, "card still shows the trained special, not a neighbour");
		Assert.Equal(2, m.TrialSpecialLevel, "level shown is the level before the trial");
	}

	// R7: selection is UI only and a refused move changes nothing.
	[Test]
	public static void SelectTrial_NeverTouchesStateHash_AndRefusedAfterLeave()
	{
		var s = DojoTrialTests.AtDojo(500);
		var m = RestStopScreenModel.Create(s)!;
		ulong h = s.StateHash();
		m.SelectTrial(1);
		Assert.Equal(h, s.StateHash(), "selecting does not change the run hash");
		Assert.True(m.TakeFree().Ok && m.Leave().Ok, "rest and leave");
		ulong after = s.StateHash(); int sel = m.SelectedIndex;
		Assert.True(!m.CanSelect && !m.SelectTrial(1), "refused after leaving");
		Assert.True(!m.ChooseTrial().Ok, "trial refused after leaving");
		Assert.Equal(after, s.StateHash(), "hash unchanged");
		Assert.Equal(sel, m.SelectedIndex, "index unchanged");
	}

	// R7: choosing the rest closes the selector.
	[Test]
	public static void SelectTrial_RefusedAfterRest()
	{
		var s = DojoTrialTests.AtDojo(500);
		var m = RestStopScreenModel.Create(s)!;
		Assert.True(m.TakeFree().Ok, "rest");
		int sel = m.SelectedIndex;
		Assert.True(!m.CanSelect && !m.SelectTrial(1) && m.SelectedIndex == sel, "locked");
		Assert.True(m.CopyRaisedTo() is null, "no copy line once the choice is made");
	}

	// Offers shrink under a selection on the last index: no exception, index in range, topped special never offered.
	[Test]
	public static void SelectedIndex_ClampsWhenOffersShrink()
	{
		var s = DojoTrialTests.AtDojo(500);
		var m = RestStopScreenModel.Create(s)!;
		m.SelectTrial(-1);
		var last = m.TrialOffer!.Slot;
		while (s.Run.CanLevelUp(last)) s.Run.LevelUp(last);
		Assert.True(m.SelectedIndex >= 0 && m.SelectedIndex < Math.Max(1, m.Offers.Count), "in range");
		Assert.True(m.TrialOffer is not null && m.TrialOffer.Slot != last, "never offers the topped special");
		Assert.True(m.ChooseTrial().Ok, "a take still works");
	}

	// R7: no trial: every selector member is inert.
	[Test]
	public static void NoTrial_AllSelectorMembersInert()
	{
		var s = DojoTrialTests.AtDojo(500);
		s.Trials = Array.Empty<TrialData>();
		var m = RestStopScreenModel.Create(s)!;
		ulong h = s.StateHash();
		Assert.Equal(0, m.Offers.Count, "no offers");
		Assert.True(!m.CanSelect && !m.SelectTrial(1) && !m.SelectTrial(-1), "no select");
		Assert.True(m.TrialOffer is null && m.TrialSpecialName is null && m.CopyRaisedTo() is null, "nulls");
		Assert.True(!m.ChooseTrial().Ok, "take refused");
		Assert.Equal(h, s.StateHash(), "hash unchanged");
		Assert.Equal(RestStopOption.NoTrial, m.Second, "card says no trial");
	}
}
