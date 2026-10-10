using System;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-58 MOCKUP pause (Start) menu (<c>scenes/ui/pause_menu.tscn</c>): Resume, Move list, Settings, Restart run,
/// Quit to title. Opening it pauses the scene tree (when <see cref="FreezeTree"/>), so the fight, which advances in
/// <c>_Process</c>, stops; the menu and its sheets run while paused. Esc / pad Start resumes (or steps back out of a
/// sheet or the confirm box). Restart and Quit ask first, Cancel is focused. Reads Ryo's health and slots from a
/// <see cref="RunState"/>; the fight wiring (replacing F2 as the player's pause) is a follow-up ticket.
/// </summary>
public partial class PauseMenu : Control
{
	public event Action? ResumeRequested, RestartRequested, QuitToTitleRequested;

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
	private Label _confirmTitle = null!, _confirmText = null!, _runName = null!, _runSub = null!;
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
		_confirmYes = UiFind.Get<Button>(this, "ConfirmYes");
		_confirmNo = UiFind.Get<Button>(this, "ConfirmNo");
		_confirmTitle = UiFind.Get<Label>(this, "ConfirmTitle");
		_confirmText = UiFind.Get<Label>(this, "ConfirmText");
		_runName = UiFind.Get<Label>(this, "RunName");
		_runSub = UiFind.Get<Label>(this, "RunSub");
		_runBar = UiFind.Get<ProgressBar>(this, "RunBar");
		ResumeButton.Pressed += Resume;
		MoveListButton.Pressed += () => ShowSheet(MoveList);
		SettingsButton.Pressed += () => ShowSheet(Settings);
		RestartButton.Pressed += () => Ask(RestartButton, "Restart run?", "This run ends here and a new one begins.", "Restart", () => { Close(); RestartRequested?.Invoke(); });
		QuitButton.Pressed += () => Ask(QuitButton, "Quit to title?", "You can continue this run from the title screen.", "Quit", () => { Close(); QuitToTitleRequested?.Invoke(); });
		_confirmNo.Pressed += CloseConfirm;
		_confirmYes.Pressed += () => { var a = _confirmAction; CloseConfirm(); a?.Invoke(); };
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
		_runName.Text = $"RYO  ·  {run.Health} / {RunState.MaxHealth}";
		_runBar.MaxValue = RunState.MaxHealth; _runBar.Value = run.Health;
		_runSub.Text = $"Row {row} of {rows}  ·  vs {opponent}";
		MoveList.Bind(MoveListSource.Build(run, movesDir));
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

	public void OpenMoveList() => ShowSheet(MoveList);
	public void OpenSettings(SettingsTab tab = SettingsTab.Sound) { ShowSheet(Settings); Settings.ShowTab(tab); }

	public void Resume() { Close(); ResumeRequested?.Invoke(); }

	// A sheet is modal for focus: the five pause buttons cannot be reached until it closes, then the opener gets focus back.
	private void ShowSheet(Control sheet) { _focusLock.Engage(Mains); sheet.Visible = true; }
	private void CloseSheet(Control sheet, Button back) { sheet.Visible = false; _focusLock.Release(); back.GrabFocus(); }

	private Button? _askedFrom;
	private void Ask(Button from, string title, string text, string yes, Action action)
	{
		_askedFrom = from;
		_confirmTitle.Text = title; _confirmText.Text = text; _confirmYes.Text = yes; _confirmAction = action;
		_focusLock.Engage(Mains);
		ConfirmBox.Visible = true;
		_confirmNo.GrabFocus();
	}

	private void CloseConfirm() { ConfirmBox.Visible = false; _confirmAction = null; _focusLock.Release(); (_askedFrom ?? ResumeButton).GrabFocus(); }

	public override void _UnhandledInput(InputEvent e)
	{
		if (!Visible) return;
		if (MoveList.Visible || Settings.Visible) return; // the open sheet handles Back itself
		if (ConfirmBox.Visible) { if (MenuInput.Back(e)) { CloseConfirm(); GetViewport().SetInputAsHandled(); } return; }
		if (MenuInput.Pause(e)) { Resume(); GetViewport().SetInputAsHandled(); }
	}
}
