using System.Collections.Generic;
using Godot;
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
		_cardScene = GD.Load<PackedScene>("res://scenes/ui/move_slot_card.tscn");
		MenuSettings.Changed += Refresh;
		TreeExiting += () => MenuSettings.Changed -= Refresh;
		VisibilityChanged += () => { if (Visible) _frameToggle.GrabFocus(); };
		Visible = false;
	}

	public void Bind(MoveListView view)
	{
		View = view;
		foreach (Node c in _baseRows.GetChildren()) c.QueueFree();
		foreach (Node c in _cards.GetChildren()) c.QueueFree();
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
		var box = new HBoxContainer { CustomMinimumSize = new Vector2(0, 52), MouseFilter = MouseFilterEnum.Ignore };
		box.AddThemeConstantOverride("separation", 0);
		var name = new Label { Text = row.Name, CustomMinimumSize = new Vector2(190, 0), VerticalAlignment = VerticalAlignment.Center, ThemeTypeVariation = "PineLabel", ClipText = true };
		name.AddThemeFontSizeOverride("font_size", 24);
		var input = new Label { Text = row.Input, CustomMinimumSize = new Vector2(300, 0), VerticalAlignment = VerticalAlignment.Center, ClipText = true };
		input.AddThemeFontSizeOverride("font_size", 21);
		var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(100, 0), Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
		right.AddThemeConstantOverride("separation", 0);
		var plain = new Label { Text = row.Plain, ClipText = true, CustomMinimumSize = new Vector2(100, 0) };
		plain.AddThemeFontSizeOverride("font_size", 20);
		var frames = new Label { Text = row.Frames, ThemeTypeVariation = "PersimmonLabel", ClipText = true, Visible = false, CustomMinimumSize = new Vector2(100, 0) };
		frames.AddThemeFontSizeOverride("font_size", 18);
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
		_frameToggle.Text = FrameDataVisible ? "Frame data: ON  (Tab / Y)" : "Frame data: off  (Tab / Y)";
		_frameToggle.ThemeTypeVariation = FrameDataVisible ? "ToggleButtonOn" : "ToggleButton";
		_schemeToggle.Text = kata ? "Inputs: Kata  (Q / X)" : "Inputs: Kihon  (Q / X)";
		_subtitle.Text = kata ? "Ryo's kit. Kata: motion specials deal +10% (precision)." : "Ryo's kit. Kihon: Special + direction, 100% damage.";
		foreach (var (_, frames, _) in _rows) frames.Visible = FrameDataVisible;
		for (int i = 0; i < _slotCards.Count; i++) _slotCards[i].Bind(View.Slots[i], kata, FrameDataVisible);
		_legend.Text = "Button keys and pad layout: Settings, Controls."; // the full bindings live there (no duplicate table here)
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (!Visible) return;
		if (MenuInput.Back(e)) BackRequested?.Invoke();
		else if (MenuInput.FrameData(e)) ToggleFrameData();
		else if (MenuInput.Scheme(e)) ToggleScheme();
		else return;
		GetViewport().SetInputAsHandled();
	}
}
