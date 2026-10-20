using System;
using System.Linq;
using Godot;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes (second pass) for the YOK-109 dojo picker. Expected values from R7, T2, T3, T4, A4.</summary>
public static class DojoPickerProbes
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

	// R7: the picker is UI only. Different selector moves before the same take -> same hash;
	// a different special taken -> a different hash.
	[Test]
	public static void SelectorMoves_DoNotChangeHash_ButTheTakenSpecialDoes()
	{
		var a = DojoTrialTests.AtDojo(500); var b = DojoTrialTests.AtDojo(500); var c = DojoTrialTests.AtDojo(500);
		var ma = RestStopScreenModel.Create(a)!; var mb = RestStopScreenModel.Create(b)!; var mc = RestStopScreenModel.Create(c)!;
		for (int i = 0; i < 7; i++) mb.SelectTrial(1);
		while (mb.SelectedIndex != 0) mb.SelectTrial(1);
		Assert.Equal(a.StateHash(), b.StateHash(), "moving never touches the hash");
		Assert.True(ma.ChooseTrial().Ok && mb.ChooseTrial().Ok, "taken");
		Assert.Equal(a.StateHash(), b.StateHash(), "same special taken: same hash whatever the selector did");
		mc.SelectTrial(1);
		Assert.True(mc.ChooseTrial().Ok, "taken");
		Assert.True(a.StateHash() != c.StateHash(), "a different special taken hashes differently");
	}

	// R7: every special at its top level -> no offers; the card is disabled and every action refused, hash untouched.
	[Test]
	public static void AllSpecialsAtTopLevel_NoOffers_CardInert(Node runner)
	{
		var s = DojoTrialTests.AtDojo(500);
		foreach (var (slot, _) in s.Run.Owned.ToList()) while (s.Run.CanLevelUp(slot)) s.Run.LevelUp(slot);
		var m = RestStopScreenModel.Create(s)!;
		ulong h = s.StateHash();
		Assert.Equal(0, m.Offers.Count, "no offers");
		Assert.Equal(RestStopOption.NoTrial, m.Second, "card says no trial");
		Assert.True(!m.CanSelect && !m.SelectTrial(1) && !m.ChooseTrial().Ok && m.CopyRaisedTo() is null, "inert");
		Assert.Equal(h, s.StateHash(), "hash unchanged");
		var screen = Open(runner, s);
		try
		{
			Assert.True(screen.SecondCard.CardDisabled, "card disabled");
			screen._Input(Key(Godot.Key.D));
			Assert.Equal("", screen.GetNode<Label>("Forecast/TrainNote").Text, "no forecast line");
			Assert.Equal(h, s.StateHash(), "keys changed nothing");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	}

	// R7: a landed trial that takes the only offered special to its top level empties the offers; the card still
	// reads as the taken trial with the display name and the level before the trial.
	[Test]
	public static void LandedTrial_OnTheOnlyOffer_CardStillShowsTheTakenTrial()
	{
		var s = DojoTrialTests.AtDojo(500);
		s.Trials = new[] { s.Trials[0] };
		var m = RestStopScreenModel.Create(s)!;
		Assert.Equal(1, m.Offers.Count, "single offer");
		var slot = m.TrialOffer!.Slot;
		while (s.Run[slot]!.Level < 2) s.Run.LevelUp(slot);
		m = RestStopScreenModel.Create(s)!;
		Assert.Equal(1, m.Offers.Count, "still offered at Lv 2");
		Assert.True(!m.CanSelect, "one offer: no selector");
		Assert.True(m.ChooseTrial().Ok && s.Dojo!.RecordAttempt(true).Ok, "taken and landed");
		Assert.Equal(0, m.Offers.Count, "now at Lv 3: not offered");
		Assert.Equal(RestStopOption.Taken, m.Second, "card reads Taken");
		Assert.True(m.ChosenTrial is not null, "the chosen trial data is still there");
		Assert.True(!string.IsNullOrEmpty(m.TrialSpecialName), "display name survives");
		Assert.Equal(2, m.TrialSpecialLevel, "level before the trial");
		Assert.Equal(1, m.AttemptsUsed, "one attempt used");
		Assert.True(m.CopyRaisedTo() is null, "no forecast once chosen");
	}

	// Pips: follow Dojo.Attempts through three misses.
	[Test]
	public static void Pips_FollowAttempts_ThroughThreeMisses(Node runner)
	{
		var s = DojoTrialTests.AtDojo(500);
		var screen = Open(runner, s);
		try
		{
			Assert.True(screen.OpenSecond(), "take");
			for (int i = 1; i <= RestStopScreenModel.MaxAttempts; i++)
			{
				Assert.True(s.Dojo!.RecordAttempt(false).Ok, $"miss {i}");
				screen.Refresh();
				string expect = "Tries " + string.Join(" ", Enumerable.Range(0, RestStopScreenModel.MaxAttempts).Select(k => k < i ? "●" : "○"));
				Assert.Equal(expect, screen.GetNode<Label>("SecondCard/Box/Picker/Pips").Text, $"{i} used");
			}
			Assert.True(!s.Dojo!.RecordAttempt(false).Ok, "a fourth attempt is refused");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	}

	// R7: after the rest is taken, selector keys neither move the pick nor write a forecast line.
	[Test]
	public static void KeysAfterRest_ChangeNothing(Node runner)
	{
		var s = DojoTrialTests.AtDojo(500);
		var screen = Open(runner, s);
		try
		{
			var m = screen.Model!;
			int idx = m.SelectedIndex;
			Assert.True(m.TakeFree().Ok, "rest");
			screen.Refresh();
			ulong h = s.StateHash();
			foreach (var k in new[] { Godot.Key.D, Godot.Key.A, Godot.Key.Left, Godot.Key.Right }) screen._Input(Key(k));
			Assert.Equal(idx, m.SelectedIndex, "pick unchanged");
			Assert.Equal(h, s.StateHash(), "hash unchanged");
			Assert.Equal("", screen.GetNode<Label>("Forecast/TrainNote").Text, "no forecast line after the choice");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	}

	// A held key (echo) or a release must not step the selector.
	[Test]
	public static void HeldKey_EchoAndReleaseDoNotStep()
	{
		Assert.Equal(0, RestStopScreen.SelectorStep(new InputEventKey { Pressed = true, Echo = true, Keycode = Godot.Key.D }), "echo ignored");
		Assert.Equal(0, RestStopScreen.SelectorStep(new InputEventKey { Pressed = false, Keycode = Godot.Key.D }), "release ignored");
	}
}
