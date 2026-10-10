using Godot;

namespace YokaiFighters.Ui.Kit;

/// <summary>
/// A selectable paper card (reward cards, special slots). Focus or <see cref="Selected"/> shows a red/persimmon nine-slice brush
/// frame around the card; disabled greys it; pressed pushes it in. Enter / pad A / click raise <see cref="Activated"/>.
/// </summary>
[Tool]
public partial class KitCard : PanelContainer
{
	[Signal] public delegate void ActivatedEventHandler();

	private KitState _preview = KitState.Auto;
	private bool _selected, _disabled, _down, _hover;
	private NinePatchRect? _stroke;
	[Export] public Texture2D? SelectionArt { get => GetNodeOrNull<NinePatchRect>("Stroke")?.Texture; set { if (GetNodeOrNull<NinePatchRect>("Stroke") is { } frame) frame.Texture = value; } }

	[Export] public KitState Preview { get => _preview; set { _preview = value; Refresh(); } }
	[Export] public bool Selected { get => _selected; set { _selected = value; Refresh(); } }
	[Export] public bool CardDisabled { get => _disabled; set { _disabled = value; FocusMode = value ? FocusModeEnum.None : FocusModeEnum.All; Refresh(); } }
	public KitState Effective { get; private set; } = KitState.Normal;
	public bool StrokeVisible => _stroke is { Visible: true };

	public override void _Ready()
	{
		if (!_disabled) FocusMode = FocusModeEnum.All;
		_stroke = GetNodeOrNull<NinePatchRect>("Stroke");
		SetNotifyTransform(true);
		FocusEntered += Refresh; FocusExited += Refresh;
		MouseEntered += () => { _hover = true; Refresh(); }; MouseExited += () => { _hover = false; Refresh(); };
		Refresh();
	}

	public override void _GuiInput(InputEvent e)
	{
		if (Effective == KitState.Disabled) return;
		if (e.IsActionPressed("ui_accept") || e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			_down = true; Refresh();
			EmitSignal(SignalName.Activated);
			AcceptEvent();
		}
		else if (e.IsActionReleased("ui_accept") || e is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left })
		{
			_down = false; Refresh();
		}
	}

	private void PlaceStroke()
	{
		if (_stroke == null) return;
		_stroke.GlobalPosition = GlobalPosition + new Vector2(-8f, -8f);
		_stroke.Size = Size + new Vector2(16f, 16f);
	}

	public void Refresh()
	{
		_stroke ??= GetNodeOrNull<NinePatchRect>("Stroke");
		var s = _preview;
		if (s == KitState.Auto)
			s = _disabled ? KitState.Disabled : _down ? KitState.Pressed : (HasFocus() || _hover) ? KitState.Focused : KitState.Normal;
		Effective = s;
		bool lit = s is KitState.Focused or KitState.Pressed || (_selected && s != KitState.Disabled);
		if (_stroke != null)
		{
			_stroke.Visible = lit;
			PlaceStroke();
			_stroke.Modulate = s == KitState.Pressed ? new Color(0.75f, 0.75f, 0.75f) : Colors.White;
		}
		SelfModulate = s == KitState.Disabled ? new Color(0.8f, 0.8f, 0.82f) : Colors.White;
		Modulate = s == KitState.Disabled ? new Color(0.86f, 0.86f, 0.88f) : Colors.White;
		PivotOffset = Size / 2f;
		Scale = s == KitState.Pressed ? new Vector2(0.975f, 0.975f) : Vector2.One;
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized) PivotOffset = Size / 2f;
		if (what == NotificationResized || what == NotificationDraw || what == NotificationTransformChanged) PlaceStroke();
	}
}
