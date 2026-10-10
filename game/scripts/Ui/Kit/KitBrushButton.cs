using Godot;

namespace YokaiFighters.Ui.Kit;

public enum KitButtonKind { MenuRow, Red, Ghost }

/// <summary>
/// Menu row (red brush stroke behind it when focused), red brush button and outlined ghost button, one script.
/// The stroke is the child TextureRect "Stroke" (ink_to_alpha shader, tinted by modulate); fonts and frames come from the theme
/// variation named in the scene (MenuRow / RedButton / GhostButton). <see cref="Preview"/> pins a state for the gallery.
/// </summary>
[Tool]
public partial class KitBrushButton : Button
{
	public static readonly Color StrokeRed = new(0.710f, 0.200f, 0.169f);
	public static readonly Color StrokeRedFocus = new(0.80f, 0.25f, 0.19f);
	public static readonly Color StrokePressed = new(0.46f, 0.11f, 0.10f);
	public static readonly Color StrokeOff = new(0.60f, 0.58f, 0.55f);

	private KitButtonKind _kind = KitButtonKind.MenuRow;
	private KitState _preview = KitState.Auto;
	private TextureRect? _stroke;
	private (KitState, KitState) _applied = (KitState.Auto, KitState.Auto);

	[Export] public KitButtonKind Kind { get => _kind; set { _kind = value; Refresh(); } }
	[Export] public KitState Preview { get => _preview; set { _preview = value; Refresh(); } }

	/// <summary>Is the red stroke showing right now (tests read this).</summary>
	public bool StrokeVisible => _stroke is { Visible: true };
	public KitState Effective { get; private set; } = KitState.Normal;

	public override void _Ready()
	{
		_stroke = GetNodeOrNull<TextureRect>("Stroke");
		FocusEntered += Refresh; FocusExited += Refresh;
		MouseEntered += Refresh; MouseExited += Refresh;
		ButtonDown += Refresh; ButtonUp += Refresh;
		Refresh();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationDraw || what == NotificationEnabled || what == NotificationDisabled) Refresh();
	}

	public void Refresh()
	{
		_stroke ??= GetNodeOrNull<TextureRect>("Stroke");
		var s = _preview;
		if (s == KitState.Disabled && !Disabled) Disabled = true;
		if (s == KitState.Auto)
			s = Disabled ? KitState.Disabled : (IsPressed() || (GetDrawMode() is DrawMode.Pressed or DrawMode.HoverPressed)) ? KitState.Pressed
				: (HasFocus() || IsHovered()) ? KitState.Focused : KitState.Normal;
		Effective = s;
		ApplyPinned(s);
		if (_stroke == null) return;
		Color c = s switch { KitState.Disabled => StrokeOff, KitState.Pressed => StrokePressed, KitState.Focused => _kind == KitButtonKind.Red ? StrokeRedFocus : StrokeRed, _ => StrokeRed };
		_stroke.Modulate = c;
		_stroke.Visible = _kind switch { KitButtonKind.MenuRow => s is KitState.Focused or KitState.Pressed, KitButtonKind.Red => true, _ => false };
		_stroke.Scale = s == KitState.Pressed ? new Vector2(0.97f, 0.9f) : Vector2.One;
		_stroke.PivotOffset = _stroke.Size / 2f;
	}

	/// <summary>When a state is pinned, the theme's style and font colour for that state replace the live ones; Auto removes the overrides.</summary>
	private void ApplyPinned(KitState s)
	{
		if (!IsInsideTree() || _applied == (_preview, s)) return; // the theme variation is not resolved before the node is in the tree; overrides re-notify, so only act on a change
		_applied = (_preview, s);
		RemoveThemeStyleboxOverride("normal"); RemoveThemeColorOverride("font_color"); // read the theme's own values, not our last override
		if (_preview == KitState.Auto) return;
		string style = s switch { KitState.Focused => "focus", KitState.Disabled => "disabled", KitState.Pressed => "pressed", _ => "normal" };
		string color = s switch { KitState.Focused => "font_focus_color", KitState.Disabled => "font_disabled_color", KitState.Pressed => "font_pressed_color", _ => "font_color" };
		AddThemeStyleboxOverride("normal", GetThemeStylebox(style));
		AddThemeColorOverride("font_color", GetThemeColor(color));
	}
}
