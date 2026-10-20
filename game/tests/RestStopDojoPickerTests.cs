using System;
using System.Linq;
using Godot;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-109 / YOK-163: the dojo's trial card. The special selector (skips specials with no authored trial or at their
/// top level, wraps, locks once the trial is taken), the attempts pips, the disabled card with its reason, the
/// display name (never the special's id) and the dojo-only "raises this copy" forecast line (T2, A4, R7).
/// </summary>
public static class RestStopDojoPickerTests
{
	static RestStopScreen Open(Node runner, RunSession s)
	{
		var screen = GD.Load<PackedScene>("res://scenes/ui/rest_stop.tscn").Instantiate<RestStopScreen>();
		screen.LineSource = _ => "A line.";
		screen.BindRun(s, ControlScheme.Kihon);
		runner.AddChild(screen);
		return screen;
	}

	static Label L(Node n, string path) => n.GetNode<Label>(path);
	static InputEventKey Key(Godot.Key k) => new() { Pressed = true, Keycode = k };

	[Test]
	public static void Model_SelectorWalksTheOfferedSpecials_WrapsAndTakesTheSelectedOne()
	{
		var s = DojoTrialTests.AtDojo(500);
		var m = RestStopScreenModel.Create(s)!;
		int n = m.Offers.Count;
		Assert.True(n >= 2, "the starters have several trials");
		var first = m.TrialOffer!.Slot;
		Assert.True(m.CanSelect && m.SelectTrial(1), "moves right");
		Assert.True(m.TrialOffer!.Slot != first, "the card now offers another special");
		Assert.True(m.SelectTrial(-1) && m.TrialOffer!.Slot == first, "and back");
		Assert.True(m.SelectTrial(-1) && m.SelectedIndex == n - 1, "left of the first wraps to the last");
		var chosen = m.TrialOffer!;
		Assert.True(m.ChooseTrial().Ok, "taken");
		Assert.Equal(chosen.Slot, s.Dojo!.TrialSlot, "the trial is the selected special's");
		Assert.True(!m.CanSelect && !m.SelectTrial(1), "locked once a choice is made");
	}

	[Test]
	public static void Model_SkipsSpecialsAtTheirTopLevel_AndWithNoAuthoredTrial()
	{
		var s = DojoTrialTests.AtDojo(500);
		var all = RestStopScreenModel.Create(s)!.Offers.ToList();
		var top = all[0].Slot;
		while (s.Run.CanLevelUp(top)) s.Run.LevelUp(top);
		var m = RestStopScreenModel.Create(s)!;
		Assert.True(m.Offers.All(o => o.Slot != top), "a top-level special is skipped");
		Assert.Equal(all.Count - 1, m.Offers.Count, "only that one dropped");
		var one = DojoTrialTests.AtDojo(500);
		one.Trials = new[] { one.Trials[0] };
		var m1 = RestStopScreenModel.Create(one)!;
		Assert.Equal(1, m1.Offers.Count, "specials with no trial are skipped");
		Assert.True(!m1.CanSelect && !m1.SelectTrial(1), "one offer: nothing to select");
	}

	[Test]
	public static void Scene_NoTrialLeft_DisablesTheCardAndSaysWhy(Node runner)
	{
		var s = DojoTrialTests.AtDojo(500);
		s.Trials = Array.Empty<TrialData>();
		var screen = Open(runner, s);
		try
		{
			Assert.True(screen.SecondCard.CardDisabled, "disabled");
			Assert.True(!screen.OpenSecond(), "a take is refused");
			Assert.True(!s.Dojo!.TrialRunning && s.Dojo.Choice == DojoChoice.None, "and changes nothing");
			Assert.True(L(screen, "SecondCard/Box/Coming").Text.Contains("unavailable"), "the reason shows");
			Assert.True(!screen.GetNode<Control>("SecondCard/Box/Picker").Visible, "no selector with nothing to select");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	}

	[Test]
	public static void Scene_ShowsPips_NameNotId_AndLeftRightChangesTheSpecial(Node runner)
	{
		var s = DojoTrialTests.AtDojo(500);
		var screen = Open(runner, s);
		try
		{
			string sp0 = L(screen, "SecondCard/Box/Picker/Special").Text;
			Assert.True(!L(screen, "SecondCard/Box/Desc").Text.Contains("rising-talisman"), "the display name, not the special id (YOK-163)");
			Assert.Equal("Tries ○ ○ ○", L(screen, "SecondCard/Box/Picker/Pips").Text, "0 of 3 used");
			screen._Input(Key(Godot.Key.D));
			Assert.True(L(screen, "SecondCard/Box/Picker/Special").Text != sp0, "D moves the selector");
			screen._Input(Key(Godot.Key.A));
			Assert.Equal(sp0, L(screen, "SecondCard/Box/Picker/Special").Text, "A moves it back");
			screen._Input(Key(Godot.Key.Right));
			screen._Input(Key(Godot.Key.Left));
			Assert.Equal(sp0, L(screen, "SecondCard/Box/Picker/Special").Text, "arrows too");
			Assert.Equal(1, RestStopScreen.SelectorStep(new InputEventJoypadButton { Pressed = true, ButtonIndex = JoyButton.RightShoulder }), "bumper");
			Assert.Equal(-1, RestStopScreen.SelectorStep(new InputEventJoypadButton { Pressed = true, ButtonIndex = JoyButton.DpadLeft }), "d-pad");
			Assert.True(screen.OpenSecond(), "take the trial");
			Assert.True(s.Dojo!.RecordAttempt(false).Ok, "one miss");
			screen.Refresh();
			Assert.Equal("Tries ● ○ ○", L(screen, "SecondCard/Box/Picker/Pips").Text, "1 of 3 used");
			screen._Input(Key(Godot.Key.D));
			Assert.Equal("Tries ● ○ ○", L(screen, "SecondCard/Box/Picker/Pips").Text, "still the same trial after the take");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	}

	[Test]
	public static void Forecast_DojoLine_ShowsOnlyWhenTheSelectedSpecialIsTheCopyTarget_AndItRaisesTheCopy()
	{
		var s = DojoTrialTests.AtDojo(500);
		var m = RestStopScreenModel.Create(s)!;
		var target = m.Forecast!.Slot;
		int hits = 0;
		for (int i = 0; i < m.Offers.Count; i++)
		{
			if (m.TrialOffer!.Slot == target) { hits++; Assert.Equal(m.Forecast.Level + 1, m.CopyRaisedTo(), "Lv 1 to 2 raises the copy"); }
			else Assert.True(m.CopyRaisedTo() is null, "another special: no line");
			m.SelectTrial(1);
		}
		Assert.True(hits <= 1, "at most one selection is the target");
		// T4: the copy never passes Lv 2, so training the Lv 2 target to its Lv 3 evolution does not raise it
		var up = DojoTrialTests.AtDojo(500);
		var tslot = RestStopScreenModel.Create(up)!.Forecast!.Slot;
		while (up.Run[tslot]!.Level < 2 && up.Run.CanLevelUp(tslot)) up.Run.LevelUp(tslot);
		var um = RestStopScreenModel.Create(up)!;
		for (int i = 0; i < um.Offers.Count; i++)
		{
			if (um.TrialOffer!.Slot == tslot) Assert.True(um.CopyRaisedTo() is null || um.TrialSpecialLevel < 2, "no line for the evolution");
			um.SelectTrial(1);
		}
	}
}
