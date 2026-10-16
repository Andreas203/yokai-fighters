using System;
using Godot;
using YokaiFighters.Ui;

namespace YokaiFighters.Dev;

/// <summary>
/// YOK-125: the dev viewer. Holds one screen scene on its own (reward draft, story card, lose screen, move list,
/// control select...) over an opaque backdrop, with a "DEV VIEW" bar and a Close button. Opened by
/// <c>GameFlow.OpenDevScreen</c>, which hides the title or map underneath. It swallows every unhandled input event, so
/// nothing underneath reacts; Esc / pad B that the screen leaves unhandled closes it, as does <see cref="Finish"/>
/// (wired to the screen's own done event: a pick, Continue, Restart, Back).
/// </summary>
public partial class DevScreenHost : CanvasLayer
{
	public const int LayerIndex = 110; // under the DevShell (120), so the DEV badge stays on top

	public event Action? Closed;
	public string Title { get; }
	public Control Screen { get; }
	public Button CloseButton { get; }
	public bool IsClosed { get; private set; }

	public DevScreenHost(string title, Control screen)
	{
		Title = title;
		Screen = screen;
		Layer = LayerIndex;
		ProcessMode = ProcessModeEnum.Always;
		var back = new ColorRect { Name = "Backdrop", Color = new Color(0.08f, 0.07f, 0.06f), MouseFilter = Control.MouseFilterEnum.Stop };
		back.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(back);
		AddChild(screen);
		var bar = new HBoxContainer { Name = "Bar", AnchorTop = 1, AnchorBottom = 1, OffsetLeft = 16, OffsetTop = -64, OffsetBottom = -12 };
		bar.AddThemeConstantOverride("separation", 16);
		CloseButton = new Button { Name = "Close", Text = "Close dev view (Esc)" };
		CloseButton.AddThemeFontSizeOverride("font_size", 20);
		CloseButton.Pressed += Finish;
		bar.AddChild(CloseButton);
		bar.AddChild(DevMenu.PageLabel("DEV VIEW · " + title, 20, "ViewTitle"));
		AddChild(bar);
	}

	/// <summary>Closes the viewer and frees the screen (once).</summary>
	public void Finish()
	{
		if (IsClosed) return;
		IsClosed = true;
		if (GetParent() is { } p) p.RemoveChild(this);
		QueueFree();
		Closed?.Invoke();
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (IsClosed) return;
		var viewport = GetViewport();
		if (MenuInput.Back(e)) Finish();
		viewport?.SetInputAsHandled();
	}
}
