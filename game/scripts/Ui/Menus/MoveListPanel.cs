using System.Collections.Generic;
using Godot;
using YokaiFighters.Ui.Kit;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-58 MOCKUP move list (<c>scenes/ui/move_list_panel.tscn</c>): Ryo's base kit (walk, dash, jump, block, six
/// normals, throw, burst) on the left, specials in slots A-D with their level on the right. Inputs follow the active
/// scheme (<see cref="MenuSettings.Scheme"/>: Kata motions with the +10% note, Kihon Special + direction). Plain
/// language first (A7/A12); frame data behind a toggle (Tab / pad Y). Content comes from <see cref="MoveListSource"/>.
/// Esc / pad B raises <see cref="BackRequested"/>; the host hides the panel.
/// </summary>
public partial class MoveListPanel : Control
{
	public event System.Action? BackRequested;

	public bool FrameDataVisible { get; private set; }
	public MoveListView? View { get; private set; }

	private VBoxContainer _baseRows = null!, _cards = null!;
	private Button _frameToggle = null!, _schemeToggle = null!;
	private Label _subtitle = null!, _legend = null!;
	private readonly List<(Label Input, Label Frames, MoveRow Row)> _rows = new();
	private readonly List<MoveSlotCard> _slotCards = new();
	private PackedScene? _cardScene;

	public int BaseRowCount => _rows.Count;
	public IReadOnlyList<MoveSlotCard> SlotCards => _slotCards;
	public string InputOf(int row) => _rows[row].Input.Text;
	public bool FramesShownOn(int row) => _rows[row].Frames.Visible;

	public override void _Ready()
	{
		MenuInput.EnsurePadConfirm();
		UiFind.Get<KeyChipFooter>(this, "Footer").SetHints(new("back", "Back"), new("frames", "Frame data"), new("scheme", "Inputs"));
		_baseRows = UiFind.Get<VBoxContainer>(this, "BaseRows");
		_cards = UiFind.Get<VBoxContainer>(this, "SpecialCards");
		_frameToggle = UiFind.Get<Button>(this, "FrameToggle");
		_schemeToggle = UiFind.Get<Button>(this, "SchemeToggle");
		_subtitle = UiFind.Get<Label>(this, "Subtitle");
		_legend = UiFind.Get<Label>(this, "Legend");
		_frameToggle.Pressed += ToggleFrameData;
		_schemeToggle.Pressed += ToggleScheme;
		void Link(Button from, Button right, Button left) { from.FocusNeighborRight = from.GetPathTo(right); from.FocusNeighborLeft = from.GetPathTo(left); from.FocusNeighborTop = from.GetPathTo(from); from.FocusNeighborBottom = from.GetPathTo(from); }
		Link(_schemeToggle, _frameToggle, _schemeToggle);
		Link(_frameToggle, _frameToggle, _schemeToggle);
		var baseScroll = UiFind.Get<ScrollContainer>(this, "BaseRowsScroll");
		var specialScroll = UiFind.Get<ScrollContainer>(this, "SpecialCardsScroll");
		MenuInput.Neighbours(_schemeToggle, null, baseScroll, null, _frameToggle);
		MenuInput.Neighbours(_frameToggle, null, specialScroll, _schemeToggle, null);
		MenuInput.Neighbours(baseScroll, _schemeToggle, null, null, specialScroll);
		MenuInput.Neighbours(specialScroll, _frameToggle, null, baseScroll, null);
		_cardScene = GD.Load<PackedScene>("res://scenes/ui/move_slot_card.tscn");
		MenuSettings.Changed += Refresh;
		TreeExiting += () => MenuSettings.Changed -= Refresh;
		VisibilityChanged += () => { if (Visible) _frameToggle.GrabFocus(); };
		Visible = false;
	}

	public void Bind(MoveListView view)
	{
		View = view;
		foreach (Node c in _baseRows.GetChildren()) { _baseRows.RemoveChild(c); c.QueueFree(); }
		foreach (Node c in _cards.GetChildren()) { _cards.RemoveChild(c); c.QueueFree(); }
		_rows.Clear(); _slotCards.Clear();
		for (int i = 0; i < view.Base.Count; i++)
		{
			_baseRows.AddChild(BuildRow(view.Base[i]));
			if (System.Array.IndexOf(MoveListSource.GroupBreaksAfter, i + 1) >= 0)
			{
				var gap = new BrushStroke { Thickness = 3, Ink = new Color(FightHud.Ink, 0.5f), CustomMinimumSize = new Vector2(0, 14), MouseFilter = MouseFilterEnum.Ignore };
				_baseRows.AddChild(gap);
			}
		}
		foreach (var slot in view.Slots)
		{
			var card = _cardScene!.Instantiate<MoveSlotCard>();
			_cards.AddChild(card);
			_slotCards.Add(card);
		}
		Refresh();
	}

	private Control BuildRow(MoveRow row)
	{
		var box = new HBoxContainer { CustomMinimumSize = new Vector2(0, 80), MouseFilter = MouseFilterEnum.Ignore };
		box.AddThemeConstantOverride("separation", 0);
		var name = new Label { Text = row.Name, CustomMinimumSize = new Vector2(205, 0), VerticalAlignment = VerticalAlignment.Center, ThemeTypeVariation = "PineLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart };
		name.AddThemeFontSizeOverride("font_size", 26);
		var input = new Label { Text = row.InputFor(MenuSettings.Scheme == ControlScheme.Kata), CustomMinimumSize = new Vector2(280, 0), VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		input.AddThemeFontSizeOverride("font_size", 26);
		var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(100, 0), Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
		right.AddThemeConstantOverride("separation", 0);
		var plain = new Label { Text = row.Plain, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(100, 0) };
		plain.AddThemeFontSizeOverride("font_size", 26);
		var frames = new Label { Text = row.Frames, ThemeTypeVariation = "PersimmonLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false, CustomMinimumSize = new Vector2(100, 0) };
		frames.AddThemeFontSizeOverride("font_size", 26);
		right.AddChild(plain); right.AddChild(frames);
		box.AddChild(name); box.AddChild(input); box.AddChild(right);
		_rows.Add((input, frames, row));
		return box;
	}

	public void ToggleFrameData() { FrameDataVisible = !FrameDataVisible; Refresh(); }
	public void ToggleScheme() => MenuSettings.SetScheme(MenuSettings.Scheme == ControlScheme.Kata ? ControlScheme.Kihon : ControlScheme.Kata);

	private void Refresh()
	{
		if (View == null || _frameToggle == null) return;
		bool kata = MenuSettings.Scheme == ControlScheme.Kata;
		_frameToggle.Text = FrameDataVisible ? "Frame data: ON" : "Frame data: off";
		_frameToggle.ThemeTypeVariation = "GhostButton";
		_schemeToggle.Text = kata ? "Inputs: Kata" : "Inputs: Kihon";
		_subtitle.Text = kata ? "Ryo's kit. Kata: motion specials deal +10% (precision)." : "Ryo's kit. Kihon: Special + direction, 100% damage.";
		foreach (var (input, frames, row) in _rows) { frames.Visible = FrameDataVisible; frames.GetParent<Control>().GetChild<Label>(0).Visible = !FrameDataVisible; input.Text = row.InputFor(kata); } // frame line replaces the plain line, so rows keep their height
		for (int i = 0; i < _slotCards.Count; i++) _slotCards[i].Bind(View.Slots[i], kata, FrameDataVisible);
		_legend.Text = "Keys and pad: Settings, Controls."; // the full bindings live there (no duplicate table here)
	}

	// _Input, not _UnhandledInput: Tab is also Godot's focus-next, and the GUI would swallow it before the frame-data shortcut ran.
	public override void _Input(InputEvent e)
	{
		if (!Visible) return;
		if (GetViewport().GuiGetFocusOwner() is ScrollContainer scroll &&
			(e.IsActionPressed("ui_down") || e.IsActionPressed("ui_up")))
		{
			scroll.ScrollVertical += e.IsActionPressed("ui_down") ? 100 : -100;
			GetViewport().SetInputAsHandled(); return;
		}
		if (MenuInput.Back(e)) BackRequested?.Invoke();
		else if (MenuInput.FrameData(e)) ToggleFrameData();
		else if (MenuInput.Scheme(e)) ToggleScheme();
		else return;
		GetViewport().SetInputAsHandled();
	}
}
