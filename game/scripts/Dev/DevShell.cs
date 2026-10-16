using System;
using Godot;

namespace YokaiFighters.Dev;

/// <summary>
/// YOK-123: the dev mode overlay, autoloaded as <c>DevShell</c> (<c>scenes/dev/dev_shell.tscn</c>, CanvasLayer 120 over
/// every screen): the "DEV" <see cref="Badge"/>, shown whenever <see cref="DevMode.On"/>, and the <see cref="Menu"/>.
/// Hosts open the menu through <see cref="OpenMenu"/> with their <see cref="DevContext"/>; nothing opens it on its own.
/// Inert while dev mode is off and unavailable.
/// </summary>
public partial class DevShell : CanvasLayer
{
	/// <summary>The autoload instance (null before it enters the tree).</summary>
	public static DevShell? Instance { get; private set; }

	public Control Badge { get; private set; } = null!;
	public DevMenu Menu { get; private set; } = null!;
	private Action? _onClosed;

	public override void _EnterTree() => Instance ??= this;

	public override void _ExitTree()
	{
		DevMode.Changed -= RefreshBadge;
		if (Instance == this) Instance = null;
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Badge = GetNode<Control>("Badge");
		Menu = GetNode<DevMenu>("Menu");
		Menu.Closed += () => { var done = _onClosed; _onClosed = null; done?.Invoke(); };
		DevMode.Changed += RefreshBadge;
		RefreshBadge();
	}

	private void RefreshBadge() => Badge.Visible = DevMode.On;

	/// <summary>
	/// Opens the dev menu on <paramref name="context"/>; <paramref name="onClosed"/> runs once when it closes. False
	/// (nothing opens) when dev mode is unavailable, when it is off and not on the title (only the title can switch it
	/// on), or when the menu is already open.
	/// </summary>
	public bool OpenMenu(DevContext context, Action? onClosed = null)
	{
		if (!CanOpen(context.Place) || Menu.IsOpen) return false;
		_onClosed = onClosed;
		Menu.Open(context);
		return true;
	}

	/// <summary>The title reaches the menu whenever dev mode is available (to switch it on); the map and pause only while it is on.</summary>
	public static bool CanOpen(DevPlace place) => DevMode.Available && (DevMode.On || place == DevPlace.Title);
}
