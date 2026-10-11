using Godot;

namespace YokaiFighters.Ui.Kit;

/// <summary>
/// Confirm modal on paper: dimmed backdrop, centred sheet, title + brush underline, one line, red confirm button and ghost cancel button.
/// <see cref="Open"/> shows it and focuses Confirm; Esc / pad B (or Cancel) raise <see cref="Cancelled"/>, Confirm raises <see cref="Confirmed"/>;
/// both close it. Focus cannot leave the two buttons while it is open. <see cref="ShowDim"/> off lets the gallery show it inline.
/// </summary>
[Tool]
public partial class PaperDialog : Control
{
	[Signal] public delegate void ConfirmedEventHandler();
	[Signal] public delegate void CancelledEventHandler();

	private bool _dim = true;
	[Export] public bool ShowDim { get => _dim; set { _dim = value; if (GetNodeOrNull<Control>("Dim") is { } d) d.Visible = value; } }

	public KitBrushButton? ConfirmButton => GetNodeOrNull<KitBrushButton>("%Confirm");
	public KitBrushButton? CancelButton => GetNodeOrNull<KitBrushButton>("%Cancel");

	public override void _Ready()
	{
		MenuInput.EnsurePadConfirm();
		if (ConfirmButton is { } c && CancelButton is { } x)
		{
			c.Pressed += () => Close(true);
			x.Pressed += () => Close(false);
			MenuInput.Neighbours(c, null, null, x, x);
			MenuInput.Neighbours(x, null, null, c, c);
			MenuInput.Ring(c, x);
		}
		if (GetNodeOrNull<Control>("Dim") is { } d) d.Visible = _dim;
	}

	public void Open(string title, string body, string confirm = "Restart", string cancel = "Cancel")
	{
		GetNode<Label>("%Title").Text = title;
		GetNode<Label>("%Body").Text = body;
		ConfirmButton!.Text = confirm;
		CancelButton!.Text = cancel;
		Visible = true;
		ConfirmButton.GrabFocus();
	}

	private void Close(bool ok)
	{
		Visible = false;
		EmitSignal(ok ? SignalName.Confirmed : SignalName.Cancelled);
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (!Visible || Engine.IsEditorHint()) return;
		if (MenuInput.Back(e)) { Close(false); GetViewport().SetInputAsHandled(); }
	}
}
