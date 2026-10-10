using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-58 review fixes: the menus driven through the viewport's real input pipeline (key and joypad events, GUI focus
/// navigation, shortcuts), not by emitting <c>Pressed</c>. Covers focus traversal incl. the disabled Continue, modal
/// containment of the confirm box and the settings sheet, Kata / Kihon EX text, and modifier-aware move data.
/// </summary>
public static class MenuInputPathTests
{
	static string MovesDir => ContentPaths.Data("moves");
	static RunState NewRun() => RunState.NewRun(FightScene.LoadAbilityPool());

	// ---- input helpers: events go through Viewport.PushInput, the same path as the keyboard and pad ----

	static void Push(Node n, InputEvent e) => n.GetViewport().PushInput(e);
	static void Tap(Node n, Key k, bool shift = false)
	{
		Push(n, new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true, ShiftPressed = shift });
		Push(n, new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false, ShiftPressed = shift });
	}
	static void Pad(Node n, JoyButton b)
	{
		Push(n, new InputEventJoypadButton { ButtonIndex = b, Pressed = true, Pressure = 1f });
		Push(n, new InputEventJoypadButton { ButtonIndex = b, Pressed = false });
	}
	static Control? Focus(Node n) => n.GetViewport().GuiGetFocusOwner();
	static bool Inside(Control? f, Control root) => f != null && (f == root || root.IsAncestorOf(f));

	/// <summary>Every way focus can move: arrows, Tab, Shift+Tab, and the pad d-pad. Asserts focus stays inside <paramref name="root"/> after each.</summary>
	static void MashFocusKeys(Node host, Control root, string what)
	{
		for (int round = 0; round < 12; round++)
		{
			foreach (var k in new[] { Key.Down, Key.Up, Key.Right, Key.Left, Key.Tab })
			{
				Tap(host, k);
				Assert.True(Inside(Focus(host), root), $"{what}: focus left the overlay after {k} (now {Focus(host)?.Name})");
			}
			Tap(host, Key.Tab, shift: true);
			Assert.True(Inside(Focus(host), root), $"{what}: focus left the overlay after Shift+Tab (now {Focus(host)?.Name})");
			foreach (var b in new[] { JoyButton.DpadDown, JoyButton.DpadUp, JoyButton.DpadRight, JoyButton.DpadLeft })
			{
				Pad(host, b);
				Assert.True(Inside(Focus(host), root), $"{what}: focus left the overlay after pad {b} (now {Focus(host)?.Name})");
			}
		}
	}

	// ---- title ----

	[Test]
	public static void Title_Navigation_SkipsDisabledContinue_AndWraps(Node host)
	{
		MenuSettings.Reset();
		var t = GD.Load<PackedScene>("res://scenes/ui/title_menu.tscn").Instantiate<TitleMenu>();
		host.AddChild(t);
		Assert.True(Focus(host) == t.StartButton, "Start has focus on open");
		Tap(host, Key.Down);
		Assert.True(Focus(host) == t.PracticeButton, "Down from Start skips the greyed Continue (keyboard): " + Focus(host)?.Name);
		Tap(host, Key.Up);
		Assert.True(Focus(host) == t.StartButton, "Up from Practice skips Continue again");
		Pad(host, JoyButton.DpadDown);
		Assert.True(Focus(host) == t.PracticeButton, "d-pad Down skips Continue");
		Pad(host, JoyButton.DpadUp); Pad(host, JoyButton.DpadUp);
		Assert.True(Focus(host) == t.QuitButton, "Up from the first entry wraps to Quit");
		Tap(host, Key.Down);
		Assert.True(Focus(host) == t.StartButton, "Down from Quit wraps to Start");
		Tap(host, Key.Tab);
		Assert.True(Focus(host) == t.PracticeButton || Focus(host) == t.SettingsButton, "Tab never lands on Continue: " + Focus(host)?.Name);
		t.SetSavedRun(new SavedRunSummary(4, 8, 720, 1000, "Spirit Wave Lv 1"));
		t.StartButton.GrabFocus();
		Tap(host, Key.Down);
		Assert.True(Focus(host) == t.ContinueButton, "with a saved run Down reaches Continue");
		t.QueueFree();
	}

	[Test]
	public static void Title_SettingsSheet_IsModalForFocus(Node host)
	{
		MenuSettings.Reset();
		var t = GD.Load<PackedScene>("res://scenes/ui/title_menu.tscn").Instantiate<TitleMenu>();
		host.AddChild(t);
		t.SettingsButton.GrabFocus();
		Tap(host, Key.Enter);
		Assert.True(t.Settings!.Visible, "Enter on Settings opens the sheet");
		Assert.True(Inside(Focus(host), t.Settings), "focus moves into the sheet");
		Assert.True(new[] { t.StartButton, t.PracticeButton, t.SettingsButton, t.QuitButton }.All(b => b.FocusMode == Control.FocusModeEnum.None), "title buttons cannot take focus under the sheet");
		MashFocusKeys(host, t.Settings, "title settings");
		Tap(host, Key.Escape);
		Assert.True(!t.Settings.Visible, "Esc closes the sheet");
		Assert.True(Focus(host) == t.SettingsButton, "focus returns to the Settings button: " + Focus(host)?.Name);
		Assert.True(t.ContinueButton.FocusMode == Control.FocusModeEnum.None, "greyed Continue stays unfocusable after the sheet closes");
		Tap(host, Key.Down);
		Assert.True(Focus(host) == t.QuitButton, "title navigation works again");
		MenuSettings.Reset();
		t.QueueFree();
	}

	// ---- pause ----

	static PauseMenu NewPause(Node host)
	{
		MenuSettings.Reset();
		var m = GD.Load<PackedScene>("res://scenes/ui/pause_menu.tscn").Instantiate<PauseMenu>();
		m.FreezeTree = false;
		host.AddChild(m);
		m.Bind(NewRun(), 3, 8, "Kitsune", MovesDir);
		m.Open();
		return m;
	}

	[Test]
	public static void Pause_Confirm_IsModalForFocus_RestoresOpener(Node host)
	{
		var m = NewPause(host);
		Assert.True(Focus(host) == m.ResumeButton, "Resume has focus on open");
		Tap(host, Key.Down); Tap(host, Key.Down); Tap(host, Key.Down);
		Assert.True(Focus(host) == m.RestartButton, "Down x3 reaches Restart run: " + Focus(host)?.Name);
		Tap(host, Key.Enter);
		Assert.True(m.ConfirmBox.Visible, "Enter on Restart opens the confirm box");
		var no = UiFind.Get<Button>(m, "ConfirmNo"); var yes = UiFind.Get<Button>(m, "ConfirmYes");
		Assert.True(Focus(host) == no, "Cancel is focused first");
		Assert.True(new[] { m.ResumeButton, m.MoveListButton, m.SettingsButton, m.RestartButton, m.QuitButton }.All(b => b.FocusMode == Control.FocusModeEnum.None), "the pause buttons underneath cannot take focus while the box is open");
		for (int round = 0; round < 12; round++)
		{
			foreach (var k in new[] { Key.Down, Key.Up, Key.Right, Key.Left, Key.Tab })
			{
				Tap(host, k);
				Assert.True(Focus(host) == no || Focus(host) == yes, $"confirm: focus escaped after {k}: {Focus(host)?.Name}");
			}
			Tap(host, Key.Tab, shift: true);
			Assert.True(Focus(host) == no || Focus(host) == yes, "confirm: focus escaped after Shift+Tab: " + Focus(host)?.Name);
			foreach (var b in new[] { JoyButton.DpadDown, JoyButton.DpadUp, JoyButton.DpadLeft, JoyButton.DpadRight })
			{
				Pad(host, b);
				Assert.True(Focus(host) == no || Focus(host) == yes, $"confirm: focus escaped after pad {b}: {Focus(host)?.Name}");
			}
		}
		int restarts = 0; m.RestartRequested += () => restarts++;
		no.GrabFocus();
		Tap(host, Key.Escape);
		Assert.True(!m.ConfirmBox.Visible && m.IsOpen && restarts == 0, "Esc cancels, the run is kept");
		Assert.True(m.ResumeButton.FocusMode == Control.FocusModeEnum.All, "pause buttons focusable again after cancel");
		Assert.True(Focus(host) == m.RestartButton, "cancel restores focus to the opener: " + Focus(host)?.Name);
		Tap(host, Key.Down);
		Assert.True(Focus(host) == m.QuitButton, "pause navigation works again after cancel");
		Pad(host, JoyButton.A);
		Assert.True(m.ConfirmBox.Visible && Focus(host) == no, $"pad A on Quit opens its confirm, Cancel focused (box {m.ConfirmBox.Visible}, focus {Focus(host)?.Name})");
		Pad(host, JoyButton.B);
		Assert.True(!m.ConfirmBox.Visible && Focus(host) == m.QuitButton, "pad B cancels and restores Quit");
		m.QueueFree();
	}

	[Test]
	public static void Pause_Sheets_AreModalForFocus_RestoreOpener(Node host)
	{
		var m = NewPause(host);
		m.SettingsButton.GrabFocus();
		Tap(host, Key.Enter);
		Assert.True(m.Settings.Visible && Inside(Focus(host), m.Settings), "Settings sheet opens with focus inside");
		MashFocusKeys(host, m.Settings, "pause settings");
		Tap(host, Key.Escape);
		Assert.True(!m.Settings.Visible && Focus(host) == m.SettingsButton, "Esc closes Settings, focus back on its button: " + Focus(host)?.Name);
		m.MoveListButton.GrabFocus();
		Tap(host, Key.Enter);
		Assert.True(m.MoveList.Visible && Inside(Focus(host), m.MoveList), "Move list opens with focus inside");
		MashFocusKeys(host, m.MoveList, "move list");
		Pad(host, JoyButton.B);
		Assert.True(!m.MoveList.Visible && Focus(host) == m.MoveListButton, "pad B closes the move list, focus back on its button: " + Focus(host)?.Name);
		Assert.True(m.ResumeButton.FocusMode == Control.FocusModeEnum.All && m.QuitButton.FocusMode == Control.FocusModeEnum.All, "pause buttons are focusable again");
		MenuSettings.Reset();
		m.QueueFree();
	}

	// ---- settings: traversal and scheme-specific controls ----

	static string Binding(SettingsPanel s, string grid, string actionStart)
	{
		var labels = UiFind.Get<GridContainer>(s, grid).GetChildren().OfType<Label>().ToList();
		for (int i = 0; i + 1 < labels.Count; i += 2) if (labels[i].Text.StartsWith(actionStart)) return labels[i + 1].Text;
		return "<missing " + actionStart + ">";
	}

	[Test]
	public static void Settings_FocusGraph_And_SchemeRows_ByRealInput(Node host)
	{
		MenuSettings.Reset();
		var s = GD.Load<PackedScene>("res://scenes/ui/settings_panel.tscn").Instantiate<SettingsPanel>();
		host.AddChild(s);
		s.Visible = true;
		Assert.True(Focus(host) == s.SliderOf(0), "Sound tab focuses Master first");
		Tap(host, Key.Right);
		Assert.True(Focus(host) == s.SliderOf(0) || Focus(host) == s.PreviewOf(0), "Right on a slider changes the value or steps to Preview");
		s.SliderOf(0).GrabFocus(); Tap(host, Key.Down);
		Assert.True(Focus(host) == s.SliderOf(1), "Down: Master -> Music slider");
		Tap(host, Key.Down); Tap(host, Key.Down);
		Assert.True(Focus(host) == s.MuteButton, "Down: Sfx -> Mute");
		for (int i = 0; i < 4; i++) Tap(host, Key.Up);
		Assert.True(Focus(host)?.Name == "TabSound", "Up from Master reaches the tabs: " + Focus(host)?.Name);
		Tap(host, Key.Right);
		Assert.True(Focus(host)?.Name == "TabControls", "Right between tabs");
		Tap(host, Key.Enter);
		Assert.True(s.Tab == SettingsTab.Controls, "Enter on the Controls tab switches page");
		Assert.True(Focus(host) == s.KihonButton, "Controls page focuses the active scheme (Kihon)");
		Assert.True(Binding(s, "KeyboardGrid", "EX").Contains("direction"), "Kihon EX row: Special + direction: " + Binding(s, "KeyboardGrid", "EX"));
		Tap(host, Key.Left);
		Assert.True(Focus(host) == s.KataButton, "Left: Kihon -> Kata");
		Tap(host, Key.Enter);
		Assert.Equal(ControlScheme.Kata, MenuSettings.Scheme, "Enter on Kata selects it");
		Assert.True(Binding(s, "KeyboardGrid", "EX").Contains("2 punches or 2 kicks"), "Kata EX row (E16): " + Binding(s, "KeyboardGrid", "EX"));
		Assert.True(Binding(s, "PadGrid", "EX").Contains("2 punches or 2 kicks"), "Kata EX row on the pad table: " + Binding(s, "PadGrid", "EX"));
		Assert.True(!Binding(s, "KeyboardGrid", "Special").Contains("+"), "Kata Special row has no Special button: " + Binding(s, "KeyboardGrid", "Special"));
		Assert.True(Binding(s, "KeyboardGrid", "Throw").Length > 0 && StartScreen.Rows(ControlScheme.Kata).Any(r => r.Action.Contains("break")), "Throw row names the break (E12)");
		Tap(host, Key.Right); Tap(host, Key.Enter);
		Assert.Equal(ControlScheme.Kihon, MenuSettings.Scheme, "back to Kihon");
		Assert.True(Binding(s, "KeyboardGrid", "EX").Contains("direction"), "rows refresh back to Kihon (E18): " + Binding(s, "KeyboardGrid", "EX"));
		MenuSettings.Reset();
		s.QueueFree();
	}

	// ---- move list ----

	[Test]
	public static void MoveList_Shortcuts_ThroughTheGuiPipeline(Node host)
	{
		MenuSettings.Reset();
		var p = GD.Load<PackedScene>("res://scenes/ui/move_list_panel.tscn").Instantiate<MoveListPanel>();
		host.AddChild(p);
		p.Bind(MoveListSource.Build(NewRun(), MovesDir));
		p.Visible = true;
		Assert.True(!p.FrameDataVisible, "frame data starts hidden");
		Tap(host, Key.Tab);
		Assert.True(p.FrameDataVisible, "Tab toggles frame data even with a button focused");
		Pad(host, JoyButton.Y);
		Assert.True(!p.FrameDataVisible, "pad Y toggles it back");
		Tap(host, Key.Q);
		Assert.Equal(ControlScheme.Kata, MenuSettings.Scheme, "Q switches the displayed scheme");
		Assert.True(p.InputOf(p.BaseRowCount - 1).Contains("2 punches"), "Kata EX input on the move list: " + p.InputOf(p.BaseRowCount - 1));
		Pad(host, JoyButton.X);
		Assert.Equal(ControlScheme.Kihon, MenuSettings.Scheme, "pad X switches back");
		Assert.True(p.InputOf(p.BaseRowCount - 1).Contains("Special"), "Kihon EX input: " + p.InputOf(p.BaseRowCount - 1));
		MenuSettings.Reset();
		p.QueueFree();
	}

	// ---- move list content ----

	[Test]
	public static void MoveList_BaseKit_HasAirNormals_ThrowBreak_AndEx()
	{
		var v = MoveListSource.Build(NewRun(), MovesDir);
		string[] ids = v.Base.Select(r => r.Id).ToArray();
		Assert.True(ids.Contains(MoveListSource.AirPunchId) && ids.Contains(MoveListSource.AirKickId), "air punch and air kick rows (E11, E19)");
		var ap = MoveLoader.LoadFile(System.IO.Path.Combine(MovesDir, "ryo-air-punch.json"));
		var row = v.Base.First(r => r.Id == MoveListSource.AirPunchId);
		Assert.True(row.Frames.StartsWith($"{ap.Startup} / {ap.Active} / {ap.Recovery} frames"), "air punch frames from data: " + row.Frames);
		Assert.True(row.Input.Contains("Jump"), "air input says jump: " + row.Input);
		var thr = v.Base.First(r => r.Id == MoveListSource.ThrowId);
		Assert.True(thr.Plain.Contains("reak") && thr.Plain.Contains("LP + LK"), "throw row says how to break it (E12): " + thr.Plain);
		Assert.True(thr.Frames.Contains("breaks in"), "break window shown from data: " + thr.Frames);
		Assert.True(v.Base.First(r => r.Id == "burst").Plain.Contains("throw"), "burst row says it cannot be used out of a throw (E13)");
		var ex = v.Base.First(r => r.Id == "ex");
		Assert.True(ex.InputFor(true).Contains("2 punches") && ex.InputFor(false).Contains("Special"), $"EX per scheme (E16 / E18): {ex.InputFor(true)} | {ex.InputFor(false)}");
		Assert.True(v.Base.All(r => r.Plain.Length > 0), "every row has a plain line (A7)");
	}

	[Test]
	public static void MoveList_SlotFrames_UseTheEquippedModifier()
	{
		var run = NewRun();
		var plain = MoveListSource.Build(run, MovesDir).Slots[0];
		Assert.Equal("", plain.ModifierName, "no modifier by default");
		var mod = ModifierLoader.Parse("{\"kind\":\"modifier\",\"id\":\"test-quick\",\"name\":\"Quick Hands\",\"source\":\"kitsune\",\"applies_to\":{\"requires\":[]}," +
			"\"effects\":[{\"target\":\"recovery\",\"op\":\"add\",\"value\":-5}],\"card\":{\"plain\":\"Recovers 5 frames faster.\",\"frames\":\"Recovery -5\"}}");
		run.Attach(SpecialSlot.A, mod);
		var owned = run[SpecialSlot.A]!;
		var expected = new EquippedSpecial(owned.Data, owned.Level, mod, run.MatchConfig()).Move;
		var unmodified = owned.Data.Build(owned.Level, false);
		Assert.Equal(unmodified.Recovery - 5, expected.Recovery, "sim applies the modifier");
		var shown = MoveListSource.Build(run, MovesDir).Slots[0];
		Assert.True(shown.Frames.StartsWith(MoveListSource.FrameLine(expected)), $"list shows the modified frame data: {shown.Frames}");
		Assert.True(!shown.Frames.StartsWith(MoveListSource.FrameLine(unmodified)), "and not the unmodified line");
		Assert.Equal("Quick Hands", shown.ModifierName, "modifier name");
		Assert.Equal("Recovers 5 frames faster.", shown.ModifierPlain, "modifier plain effect");
		Assert.True(shown.Frames.Contains("Quick Hands: Recovery -5"), "frame text carries the modifier's frame effect");
	}

	[Test]
	public static void MoveSlotCard_ShowsModifierName_AndMovesItBehindTheToggle(Node host)
	{
		var run = NewRun();
		var mod = ModifierLoader.Parse("{\"kind\":\"modifier\",\"id\":\"test-quick\",\"name\":\"Quick Hands\",\"source\":\"kitsune\",\"applies_to\":{\"requires\":[]}," +
			"\"effects\":[{\"target\":\"recovery\",\"op\":\"add\",\"value\":-5}],\"card\":{\"plain\":\"Recovers 5 frames faster.\",\"frames\":\"Recovery -5\"}}");
		run.Attach(SpecialSlot.A, mod);
		MenuSettings.Reset();
		var p = GD.Load<PackedScene>("res://scenes/ui/move_list_panel.tscn").Instantiate<MoveListPanel>();
		host.AddChild(p);
		p.Bind(MoveListSource.Build(run, MovesDir));
		var modLabel = UiFind.Get<Label>(p.SlotCards[0], "Mod");
		Assert.True(modLabel.Visible && modLabel.Text.Contains("Quick Hands") && modLabel.Text.Contains("5 frames faster"), "modifier name + effect on the card: " + modLabel.Text);
		Assert.True(!UiFind.Get<Label>(p.SlotCards[1], "Mod").Visible, "cards without a modifier show no line");
		p.ToggleFrameData();
		Assert.True(!modLabel.Visible && UiFind.Get<Label>(p.SlotCards[0], "Frames").Text.Contains("Quick Hands"), "with frame data on, the modifier moves into the frame text");
		MenuSettings.Reset();
		p.QueueFree();
	}
}
