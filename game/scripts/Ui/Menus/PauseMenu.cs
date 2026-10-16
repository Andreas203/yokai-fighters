using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Ui.Kit;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

/// <summary>
/// Pause (Start) menu (<c>scenes/ui/pause_menu.tscn</c>): Resume, Move list, Settings, Restart run,
/// Quit to title. Opening it pauses the scene tree (when <see cref="FreezeTree"/>), so the fight, which advances in
/// <c>_Process</c>, stops; the menu and its sheets run while paused. Esc / pad Start resumes (or steps back out of a
/// sheet or the confirm box). Restart and Quit ask first, Cancel is focused. Reads caller-owned health, position and slots; FightScene.Pause supplies the live summary.
/// </summary>
public sealed record PauseFighterSummary(string Name, int Health, int MaxHealth);
public sealed record PauseSlotSummary(string Slot, string Move, int Level, string Modifier, string? MoveId = null);
/// <summary>Presentation only: the caller supplies position text, both health values and slots A-D.</summary>
public sealed record PauseRunSummary(PauseFighterSummary Player, PauseFighterSummary Opponent,
 string Position, IReadOnlyList<PauseSlotSummary> Slots);

public partial class PauseMenu : Control
{
	public event Action? ResumeRequested, RestartRequested, QuitToTitleRequested, MoveListRequested, SettingsRequested;
	/// <summary>YOK-123: the Dev menu button or F11 while <see cref="SetDevMenuEntry"/> is on.</summary>
	public event Action? DevMenuRequested;
	/// <summary>YOK-123: small "Dev menu (F11)" button, shown only in dev mode; null until first shown.</summary>
	public Button? DevButton { get; private set; }
	private bool _devEntry;

	/// <summary>Pause the scene tree while open (the real fight). Tests and the capture tool may turn it off.</summary>
	public bool FreezeTree { get; set; } = true;
	public bool IsOpen => Visible;

	public Button ResumeButton { get; private set; } = null!;
	public Button MoveListButton { get; private set; } = null!;
	public Button SettingsButton { get; private set; } = null!;
	public Button RestartButton { get; private set; } = null!;
	public Button QuitButton { get; private set; } = null!;
	public MoveListPanel MoveList { get; private set; } = null!;
	public SettingsPanel Settings { get; private set; } = null!;
	public Control ConfirmBox { get; private set; } = null!;

	private Button _confirmYes = null!, _confirmNo = null!;
	private Label _runName = null!, _runSub = null!;
	private PaperDialog _dialog = null!;
	private ProgressBar _runBar = null!;
	private Action? _confirmAction;
	private readonly FocusLock _focusLock = new();
	private Button[] Mains => new[] { ResumeButton, MoveListButton, SettingsButton, RestartButton, QuitButton };
	private string _movesDir = "";

	public override void _Ready()
	{
		MenuInput.EnsurePadConfirm();
		ProcessMode = ProcessModeEnum.Always;
		ResumeButton = UiFind.Get<Button>(this, "Resume");
		MoveListButton = UiFind.Get<Button>(this, "MoveList");
		SettingsButton = UiFind.Get<Button>(this, "Settings");
		RestartButton = UiFind.Get<Button>(this, "Restart");
		QuitButton = UiFind.Get<Button>(this, "QuitToTitle");
		ConfirmBox = UiFind.Get<Control>(this, "Confirm");
		_dialog = (PaperDialog)ConfirmBox;
		_confirmYes = _dialog.ConfirmButton!;
		_confirmNo = _dialog.CancelButton!;
		UiFind.Get<KeyChipFooter>(this, "Footer").SetHints(new("confirm", "Confirm"), new("pause", "Resume"));
		_runName = UiFind.Get<Label>(this, "RunName");
		_runSub = UiFind.Get<Label>(this, "RunSub");
		_runBar = UiFind.Get<ProgressBar>(this, "RunBar");
		ResumeButton.Pressed += Resume;
		MoveListButton.Pressed += () => { MoveListRequested?.Invoke(); ShowSheet(MoveList); };
		SettingsButton.Pressed += () => { SettingsRequested?.Invoke(); ShowSheet(Settings); };
		RestartButton.Pressed += () => Ask(RestartButton, "Restart run?", "This run ends here and a new one begins.", "Restart", () => { Close(); RestartRequested?.Invoke(); });
		QuitButton.Pressed += () => Ask(QuitButton, "Quit to title?", "Leave this run and return to the title screen?", "Quit", () => { Close(); QuitToTitleRequested?.Invoke(); });
		_dialog.Cancelled += CloseConfirm;
		_dialog.Confirmed += () => { var a = _confirmAction; CloseConfirm(); a?.Invoke(); };
		MenuInput.WrapVertical(Mains);
		// The confirm box is modal for focus: Cancel and the action button only reach each other (the buttons underneath are locked while it is open).
		MenuInput.Neighbours(_confirmNo, null, null, _confirmYes, _confirmYes);
		MenuInput.Neighbours(_confirmYes, null, null, _confirmNo, _confirmNo);
		MenuInput.Ring(_confirmNo, _confirmYes);

		MoveList = GD.Load<PackedScene>("res://scenes/ui/move_list_panel.tscn").Instantiate<MoveListPanel>();
		Settings = GD.Load<PackedScene>("res://scenes/ui/settings_panel.tscn").Instantiate<SettingsPanel>();
		AddChild(MoveList); AddChild(Settings);
		MoveList.BackRequested += () => CloseSheet(MoveList, MoveListButton);
		Settings.BackRequested += () => CloseSheet(Settings, SettingsButton);
		Visible = false;
	}

	/// <summary>Fills the run strip and the move list from the run state. <paramref name="movesDir"/> = data/moves.</summary>
	public void Bind(RunState run, int row, int rows, string opponent, string movesDir)
	{
		_movesDir = movesDir;
		var view = MoveListSource.Build(run, movesDir);
		SetRunSummary(new(new("RYO", run.Health, RunState.MaxHealth), new(opponent, 0, 0),
			$"Row {row} of {rows} · vs {opponent}", view.Slots.Select(x => new PauseSlotSummary(x.Slot, x.Empty ? "Empty" : x.Name, x.Level, x.ModifierName, run[Enum.Parse<SpecialSlot>(x.Slot)]?.Data.Id)).ToArray()));
		MoveList.Bind(MoveListSource.Build(run, movesDir));
	}

	/// <summary>Single presentation seam for fight wiring; no simulation, fixture values or run rules here.</summary>
	public void SetRunSummary(PauseRunSummary summary)
	{
		_runName.Text = summary.Player.Name;
		UiFind.Get<Label>(this, "RunHealth").Text = $"{summary.Player.Health} / {summary.Player.MaxHealth}";
		UiFind.Get<Label>(this, "RunPercent").Text = summary.Player.MaxHealth > 0
			? $"{Math.Clamp(100.0 * summary.Player.Health / summary.Player.MaxHealth, 0, 100):0}%" : "—";
		_runBar.MaxValue = Math.Max(1, summary.Player.MaxHealth); _runBar.Value = summary.Player.Health;
		_runSub.Text = summary.Position;
		var slots = UiFind.Get<VBoxContainer>(this, "Slots");
		foreach (Node child in slots.GetChildren()) { slots.RemoveChild(child); child.QueueFree(); }
		foreach (var slot in summary.Slots)
		{
			bool empty = slot.Level <= 0;
			var card = new PanelContainer { Name = $"Slot{slot.Slot}", CustomMinimumSize = new(680, 116),
				MouseFilter = MouseFilterEnum.Ignore };
			var paper = (StyleBoxTexture)GD.Load<StyleBoxTexture>("res://ui/kit/paper_card_style.tres").Duplicate();
			paper.ContentMarginLeft = paper.ContentMarginRight = 20;
			paper.ContentMarginTop = paper.ContentMarginBottom = 12;
			card.AddThemeStyleboxOverride("panel", paper);
			var content = new Control { CustomMinimumSize = new(640, 92), MouseFilter = MouseFilterEnum.Ignore };
			card.AddChild(content);
			var texture = AbilityArt.ForId(slot.MoveId);
			// ExpandMode first: with the default mode the texture's own size is the minimum and Size would be clamped up to it.
			var flourish = new TextureRect { Name = "Flourish", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Texture = texture,
				Position = new(536, 0), Size = new(104, 92),
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Modulate = new Color(1, 1, 1, .12f),
				Visible = !empty, MouseFilter = MouseFilterEnum.Ignore };
			content.AddChild(flourish);
			content.AddChild(new TextureRect { Name = "MoveIcon", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Texture = texture,
				Position = new(0, 0), Size = new(92, 92),
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Visible = !empty,
				MouseFilter = MouseFilterEnum.Ignore });
			var name = new Label { Name = "MoveName", Text = empty ? $"{slot.Slot}   Empty" : $"{slot.Move} · Lv {slot.Level}",
				Position = new(112, string.IsNullOrEmpty(slot.Modifier) ? 24 : 6), Size = new(516, 40),
				AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
			name.AddThemeFontSizeOverride("font_size", 28);
			content.AddChild(name);
			var modifier = new Label { Name = "Modifier", Text = slot.Modifier, Visible = !empty && !string.IsNullOrEmpty(slot.Modifier),
				Position = new(112, 50), Size = new(516, 36), MouseFilter = MouseFilterEnum.Ignore };
			modifier.AddThemeFontSizeOverride("font_size", 26);
			content.AddChild(modifier);
			slots.AddChild(card);
		}
	}

	public void Open()
	{
		if (Visible) return;
		Visible = true;
		MoveList.Visible = false; Settings.Visible = false; ConfirmBox.Visible = false;
		if (FreezeTree && IsInsideTree()) GetTree().Paused = true;
		ResumeButton.GrabFocus();
	}

	public void Close()
	{
		_focusLock.Release();
		Visible = false;
		MoveList.Visible = false; Settings.Visible = false; ConfirmBox.Visible = false;
		if (FreezeTree && IsInsideTree()) GetTree().Paused = false;
	}

	public void OpenMoveList() { MoveListRequested?.Invoke(); ShowSheet(MoveList); }
	public void OpenSettings(SettingsTab tab = SettingsTab.Sound) { SettingsRequested?.Invoke(); ShowSheet(Settings); Settings.ShowTab(tab); }

	public void Resume() { Close(); ResumeRequested?.Invoke(); }

	/// <summary>YOK-123: shows or hides the dev menu entry (button + F11). The fight sets it on each pause from dev mode.</summary>
	public void SetDevMenuEntry(bool on)
	{
		_devEntry = on;
		if (on && DevButton == null)
		{
			DevButton = new Button { Name = "DevMenu", Text = "Dev menu (F11)", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(220, 44) };
			DevButton.AddThemeFontSizeOverride("font_size", 22);
			DevButton.Pressed += RequestDevMenu;
			AddChild(DevButton);
			DevButton.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft, LayoutPresetMode.Minsize, 16);
		}
		if (DevButton != null) DevButton.Visible = on;
	}

	/// <summary>While the dev menu is over the pause menu: the pause buttons cannot take focus. <see cref="ReleaseOverlay"/> gives Resume focus back.</summary>
	public void LockForOverlay() => _focusLock.Engage(Mains);
	public void ReleaseOverlay() { _focusLock.Release(); if (Visible) ResumeButton.GrabFocus(); }

	private void RequestDevMenu()
	{
		if (_devEntry && Visible && !MoveList.Visible && !Settings.Visible && !ConfirmBox.Visible) DevMenuRequested?.Invoke();
	}

	// A sheet is modal for focus: the five pause buttons cannot be reached until it closes, then the opener gets focus back.
	private void ShowSheet(Control sheet) { _focusLock.Engage(Mains); sheet.Visible = true; }
	private void CloseSheet(Control sheet, Button back) { sheet.Visible = false; _focusLock.Release(); back.GrabFocus(); }

	private Button? _askedFrom;
	private void Ask(Button from, string title, string text, string yes, Action action)
	{
		_askedFrom = from;
		_confirmAction = action;
		_focusLock.Engage(Mains);
		_dialog.Open(title, text, yes);
		_confirmNo.GrabFocus();
	}

	private void CloseConfirm() { ConfirmBox.Visible = false; _confirmAction = null; _focusLock.Release(); (_askedFrom ?? ResumeButton).GrabFocus(); }

	public override void _UnhandledInput(InputEvent e)
	{
		if (!Visible) return;
		if (MoveList.Visible || Settings.Visible) return; // the open sheet handles Back itself
		if (ConfirmBox.Visible) { if (MenuInput.Back(e)) { CloseConfirm(); GetViewport().SetInputAsHandled(); } return; }
		if (_devEntry && e is InputEventKey { Pressed: true, Echo: false } k && k.PhysicalKeycode == Dev.DevMode.MenuKey) { RequestDevMenu(); GetViewport().SetInputAsHandled(); return; }
		if (MenuInput.Pause(e)) { Resume(); GetViewport().SetInputAsHandled(); }
	}
}
