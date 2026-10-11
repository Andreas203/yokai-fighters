using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-58: title, pause, move list and settings mockups (scenes build, real data, scheme text, freeze, confirm, focus).</summary>
public static class MenuMockupTests
{
	static string MovesDir => ContentPaths.Data("moves");
	static RunState NewRun() => RunState.NewRun(FightScene.LoadAbilityPool());

	// ---- move list content (data, not copy) ----

	[Test]
	public static void PauseSummary_UsesBothFightersPositionAndAllSlots(Node host)
	{
		var p = GD.Load<PackedScene>("res://scenes/ui/pause_menu.tscn").Instantiate<PauseMenu>();
		host.AddChild(p); p.FreezeTree = false;
		var view = MoveListSource.Build(NewRun(), MovesDir);
		p.SetRunSummary(new(new("RYO", 321, 1000), new("Kitsune", 456, 1000), "Duel 2 of 2",
			view.Slots.Select(x => new PauseSlotSummary(x.Slot, x.Empty ? "Empty" : x.Name, x.Empty ? 0 : x.Level, x.Slot == "A" ? "Will-o'-Wisp" : "")).ToArray()));
		Assert.Equal("Duel 2 of 2", UiFind.Get<Label>(p, "RunSub").Text, "caller owns position");
		Assert.Equal(321.0, UiFind.Get<ProgressBar>(p, "RunBar").Value, "player health");
		Assert.Equal(456.0, UiFind.Get<ProgressBar>(p, "OpponentBar").Value, "opponent health");
		var slots = UiFind.Get<VBoxContainer>(p, "Slots");
		Assert.Equal(4, slots.GetChildCount(), "all four slots including empty");
		Assert.True(slots.GetChild(0).GetChild<Label>(0).Text.Contains("Will-o'-Wisp"), "modifier rendered");
		int moves = 0, settings = 0;
		p.MoveListRequested += () => moves++; p.SettingsRequested += () => settings++;
		p.Open(); p.MoveListButton.EmitSignal(BaseButton.SignalName.Pressed);
		p.Close(); p.Open(); p.SettingsButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Equal((1, 1), (moves, settings), "sheet request interface");
		p.Close(); p.QueueFree();
	}

	[Test]
	public static void RebuiltMenus_KitFootersPercentagesBackAndReadableCards(Node host)
	{
		MenuSettings.Reset();
		var s = GD.Load<PackedScene>("res://scenes/ui/settings_panel.tscn").Instantiate<SettingsPanel>();
		host.AddChild(s);
		Assert.Equal("70%", UiFind.Get<Label>(s, "MusicValue").Text, "real default, not concept 60%");
		var footer = UiFind.Get<YokaiFighters.Ui.Kit.KeyChipFooter>(s, "Footer");
		footer.Pinned = YokaiFighters.Ui.Kit.FooterDevice.Pad;
		Assert.Equal("D-PAD|LB / RB|B", string.Join("|", footer.ChipTexts()), "pad hints");
		int backs = 0; s.BackRequested += () => backs++;
		UiFind.Get<Button>(s, "Back").EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Equal(1, backs, "visible back button uses existing event");
		var p = MakeMoveList(host);
		foreach (var card in p.SlotCards)
		{
			Assert.True(card.IsClass("PanelContainer"), "kit paper container grows to fit copy");
			Assert.Equal(26, UiFind.Get<Label>(card, "Input").GetThemeFontSize("font_size"), "readable input");
			Assert.True(UiFind.Get<Label>(card, "Input").AutowrapMode != TextServer.AutowrapMode.Off, "long motions wrap");
		}
		Assert.Equal(Control.FocusModeEnum.All, UiFind.Get<ScrollContainer>(p, "SpecialCardsScroll").FocusMode, "pad can reach scroll column");
		s.QueueFree(); p.QueueFree(); MenuSettings.Reset();
	}

	[Test]
	public static void MoveList_BaseKit_HasEveryRowAndRealNames()
	{
		var v = MoveListSource.Build(NewRun(), MovesDir);
		string[] expected = { "Walk", "Dash", "Jump", "Air punch", "Air kick", "Block", "Light punch", "Medium punch", "Heavy punch", "Light kick", "Medium kick", "Heavy kick", "Throw", "Burst", "EX special" };
		Assert.Equal(string.Join("|", expected), string.Join("|", v.Base.Select(r => r.Name)), "base kit order");
		// names and frame data come from the data files
		var lp = MoveLoader.LoadFile(Path.Combine(MovesDir, "ryo-light-punch.json"));
		var row = v.Base.First(r => r.Id == "ryo-light-punch");
		Assert.True(row.Frames.StartsWith($"{lp.Startup} / {lp.Active} / {lp.Recovery} frames, {lp.Damage} damage"), "LP frames from data: " + row.Frames);
		Assert.Equal("LP + LK", v.Base.First(r => r.Id == MoveListSource.ThrowId).Input, "throw input from data");
		Assert.True(v.Base.All(r => r.Plain.Length > 0), "every row has a plain line (A7)");
	}

	[Test]
	public static void MoveList_Specials_SlotsAndLevels()
	{
		var run = NewRun();
		var v = MoveListSource.Build(run, MovesDir);
		Assert.Equal(4, v.Slots.Count, "slots A-D");
		Assert.Equal("Spirit Wave", v.Slots[0].Name, "A is Spirit Wave");
		Assert.Equal(1, v.Slots[0].Level, "Lv 1");
		Assert.True(v.Slots[2].Empty && v.Slots[3].Empty, "C and D empty for a new run");
		run.LevelUp(SpecialSlot.B);
		Assert.Equal(2, MoveListSource.Build(run, MovesDir).Slots[1].Level, "Rising Talisman Lv 2");
		Assert.True(v.Slots[0].KataInput.StartsWith("Down, down-forward, forward"), "K1 slot A motion");
		Assert.Equal("Special + neutral", v.Slots[0].KihonInput, "K2 slot A");
		Assert.Equal("Special + forward", v.Slots[1].KihonInput, "K2 slot B");
	}

	// ---- panels ----

	static MoveListPanel MakeMoveList(Node host)
	{
		MenuSettings.Reset();
		var p = GD.Load<PackedScene>("res://scenes/ui/move_list_panel.tscn").Instantiate<MoveListPanel>();
		host.AddChild(p);
		p.Bind(MoveListSource.Build(NewRun(), MovesDir));
		return p;
	}

	[Test]
	public static void MoveListPanel_InputsFollowScheme_FramesBehindToggle(Node host)
	{
		var p = MakeMoveList(host);
		Assert.Equal(15, p.BaseRowCount, "15 base rows");
		Assert.Equal(4, p.SlotCards.Count, "4 slot cards");
		var input = UiFind.Get<Label>(p.SlotCards[0], "Input");
		Assert.True(input.Text.StartsWith("Kihon: Special + neutral"), "Kihon default (E18): " + input.Text);
		MenuSettings.SetScheme(ControlScheme.Kata);
		Assert.True(input.Text.StartsWith("Kata: Down"), "Kata motion: " + input.Text);
		Assert.True(!p.FramesShownOn(6) && !p.FrameDataVisible, "frame data hidden by default");
		Assert.True(!UiFind.Get<Label>(p.SlotCards[0], "Frames").Visible, "card frames hidden");
		p.ToggleFrameData();
		Assert.True(p.FramesShownOn(6), "row frames shown after toggle");
		Assert.True(UiFind.Get<Label>(p.SlotCards[0], "Frames").Visible, "card frames shown after toggle");
		MenuSettings.Reset();
		p.QueueFree();
	}

	[Test]
	public static void SettingsPanel_Sliders_Mute_SchemeLine(Node host)
	{
		MenuSettings.Reset();
		var s = GD.Load<PackedScene>("res://scenes/ui/settings_panel.tscn").Instantiate<SettingsPanel>();
		host.AddChild(s);
		s.SliderOf(1).Value = 35;
		Assert.Equal(35, MenuSettings.Music, "music slider drives the setting");
		s.SliderOf(2).Value = 0;
		Assert.Equal(0, MenuSettings.Sfx, "sfx can reach 0");
		s.MuteButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.True(MenuSettings.Muted && AudioServer.IsBusMute(0), "mute reaches the master bus");
		Assert.Equal(SettingsPanel.KihonLine, s.SchemeLineLabel.Text, "Kihon line (K2)");
		s.KataButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Equal(ControlScheme.Kata, MenuSettings.Scheme, "Kata toggle");
		Assert.Equal(SettingsPanel.KataLine, s.SchemeLineLabel.Text, "Kata line (K1)");
		Assert.True(SettingsPanel.KataLine.Contains("+10%"), "K1 states the +10% bonus");
		Assert.True(SettingsPanel.KihonLine.Contains("100%"), "K2 states 100% damage");
		Assert.True(s.PlayerOf(0).Stream != null && s.PlayerOf(2).Bus == MenuSettings.SfxBus, "preview cues exist on the right buses");
		Assert.Equal(10, UiFind.Get<GridContainer>(s, "KeyboardGrid").GetChildCount() / 2, "bindings rows (keyboard)");
		MenuSettings.Reset();
		s.QueueFree();
	}

	[Test]
	public static void TitleMenu_ContinueGreyedWithoutSave_FocusSkipsIt(Node host)
	{
		var t = GD.Load<PackedScene>("res://scenes/ui/title_menu.tscn").Instantiate<TitleMenu>();
		host.AddChild(t);
		Assert.True(t.ContinueButton.Disabled && !t.HasSavedRun, "Continue disabled with no saved run");
		Assert.True(t.ContinueButton.FocusMode == Control.FocusModeEnum.None, "Continue not focusable");
		string[] order = { t.StartButton.Text, t.ContinueButton.Text, t.PracticeButton.Text, t.SettingsButton.Text, t.QuitButton.Text };
		Assert.Equal("Start|Continue|Practice|Settings|Quit", string.Join("|", order), "entries");
		t.SetSavedRun(new SavedRunSummary(4, 8, 720, 1000, "Spirit Wave Lv 1"));
		Assert.True(!t.ContinueButton.Disabled, "Continue enabled with a saved run");
		Assert.True(t.DetailBodyLabel.Text.Length > 0, "detail card text");
		int started = 0, practice = 0;
		t.StartRequested += () => started++;
		t.PracticeRequested += () => practice++;
		t.StartButton.EmitSignal(BaseButton.SignalName.Pressed);
		t.PracticeButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Equal((1, 1), (started, practice), "events");
		t.SettingsButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.True(t.Settings!.Visible, "Settings sheet opens from the title");
		t.QueueFree();
	}

	[Test]
	public static void TitleMenu_MasksLightWithFocus_FallBackToDrawnArt(Node host)
	{
		var t = GD.Load<PackedScene>("res://scenes/ui/title_menu.tscn").Instantiate<TitleMenu>();
		host.AddChild(t);
		var fox = UiFind.Get<YokaiMask>(t, "MaskKitsune"); var oni = UiFind.Get<YokaiMask>(t, "MaskOni"); var kappa = UiFind.Get<YokaiMask>(t, "MaskKappa");
		Assert.True(fox.Lit && !oni.Lit && !kappa.Lit, "Start lights the Kitsune mask");
		t.SetSavedRun(new SavedRunSummary(4, 8, 720, 1000, "Spirit Wave Lv 1"));
		t.PracticeButton.GrabFocus();
		Assert.True(kappa.Lit && !fox.Lit && !oni.Lit, "Practice lights the Kappa mask");
		t.ContinueButton.GrabFocus();
		Assert.True(oni.Lit && !kappa.Lit, "Continue lights the Oni mask");
		Assert.True(fox.ArtPath.EndsWith("title/mask-kitsune.png"), "art slot path");
		Assert.True(fox.UsesGeneratedArt == ResourceLoader.Exists(fox.ArtPath), "generated art is used only when the PNG exists");
		Assert.True(t.FindChild("MaskTanuki", true, false) == null, "no Tanuki mask on the title (story reveal)");
		t.QueueFree();
	}

	[Test]
	public static void PauseMenu_FreezesTree_ConfirmGuardsDestructiveEntries(Node host)
	{
		var m = GD.Load<PackedScene>("res://scenes/ui/pause_menu.tscn").Instantiate<PauseMenu>();
		host.AddChild(m);
		m.Bind(NewRun(), 3, 8, "Kitsune", MovesDir);
		Assert.Equal("Resume|Move list|Settings|Restart run|Quit to title",
			string.Join("|", new[] { m.ResumeButton, m.MoveListButton, m.SettingsButton, m.RestartButton, m.QuitButton }.Select(b => b.Text)), "entries");
		int restarts = 0, quits = 0, resumes = 0;
		m.RestartRequested += () => restarts++; m.QuitToTitleRequested += () => quits++; m.ResumeRequested += () => resumes++;
		var tree = host.GetTree();
		m.Open();
		Assert.True(tree.Paused && m.IsOpen, "opening freezes the tree");
		m.RestartButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.True(m.ConfirmBox.Visible && restarts == 0, "Restart asks first");
		((YokaiFighters.Ui.Kit.PaperDialog)m.ConfirmBox).CancelButton!.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.True(!m.ConfirmBox.Visible && restarts == 0 && m.IsOpen, "Cancel keeps the run");
		m.RestartButton.EmitSignal(BaseButton.SignalName.Pressed);
		((YokaiFighters.Ui.Kit.PaperDialog)m.ConfirmBox).ConfirmButton!.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.True(restarts == 1 && !m.IsOpen && !tree.Paused, "confirmed restart closes and unfreezes");
		m.Open();
		m.QuitButton.EmitSignal(BaseButton.SignalName.Pressed);
		((YokaiFighters.Ui.Kit.PaperDialog)m.ConfirmBox).ConfirmButton!.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Equal(1, quits, "quit to title after confirm");
		m.Open();
		m.Resume();
		Assert.True(resumes == 1 && !tree.Paused, "Resume unfreezes");
		m.MoveListButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.True(m.MoveList.Visible && m.MoveList.BaseRowCount == 15, "move list opens with the base kit");
		tree.Paused = false;
		m.QueueFree();
	}

	[Test]
	public static void Mockups_EveryScreenOpens(Node host)
	{
		var h = GD.Load<PackedScene>("res://scenes/ui/menu_mockups.tscn").Instantiate<MenuMockups>();
		h.WithFight = false;
		host.AddChild(h);
		foreach (var s in System.Enum.GetValues<MockScreen>()) h.Show(s);
		h.Show(MockScreen.Controls);
		Assert.True(h.Pause.Settings.Visible && h.Pause.Settings.Tab == SettingsTab.Controls, "controls sheet");
		h.Show(MockScreen.TitleNoSave);
		Assert.True(h.Title.Visible && !h.Title.HasSavedRun, "title without a saved run");
		host.GetTree().Paused = false;
		h.QueueFree();
	}
}
