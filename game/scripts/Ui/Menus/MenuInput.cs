using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// Shared menu input mapping, so title, pause, settings and move list answer the same keys and pad buttons:
/// Esc / pad B = back, Tab / pad Y = frame data, Q / pad X = switch scheme, Q or E / LB or RB = switch tab,
/// Esc / pad Start = pause. None of these are fight keys (WASD, UIOJKL, Space) and menus only read input while shown.
/// Up / Down / Left / Right / Enter / A use Godot's built-in ui_* actions (arrows, d-pad, left stick).
/// </summary>
public static class MenuInput
{
	public static bool Back(InputEvent e) => e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } or InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.B };
	public static bool Pause(InputEvent e) => e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } or InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Start };
	public static bool FrameData(InputEvent e) => e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Tab } or InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Y };
	public static bool Scheme(InputEvent e) => e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Q } or InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.X };
	public static int Tab(InputEvent e) => e switch
	{
		InputEventKey { Pressed: true, Echo: false, Keycode: Key.Q } => -1,
		InputEventKey { Pressed: true, Echo: false, Keycode: Key.E } => 1,
		InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.LeftShoulder } => -1,
		InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.RightShoulder } => 1,
		_ => 0,
	};

	/// <summary>
	/// Godot 4.7's default <c>ui_accept</c> has no pad button (Enter, Keypad Enter, Space only), so a controller could move focus but never
	/// press a menu button. Adds pad A to it (once). Menus call this from <c>_Ready</c>; the fight reads the pad itself and is unaffected.
	/// </summary>
	public static void EnsurePadConfirm()
	{
		foreach (var ev in InputMap.ActionGetEvents("ui_accept")) if (ev is InputEventJoypadButton { ButtonIndex: JoyButton.A }) return;
		InputMap.ActionAddEvent("ui_accept", new InputEventJoypadButton { ButtonIndex = JoyButton.A });
	}

	/// <summary>
	/// Explicit up / down chain through a vertical button list (top to bottom), wrapping from the last to the first and back.
	/// Left / right stay on the button. Nothing depends on layout geometry; a disabled entry (FocusMode None) is passed over by Godot's focus search.
	/// </summary>
	public static void WrapVertical(params Control[] list)
	{
		if (list.Length < 2) return;
		for (int i = 0; i < list.Length; i++)
			Neighbours(list[i], list[(i + list.Length - 1) % list.Length], list[(i + 1) % list.Length], null, null);
		Ring(list);
	}

	/// <summary>Sets a control's four directional neighbours (null = stay on itself, so focus cannot escape that way).</summary>
	public static void Neighbours(Control c, Control? up, Control? down, Control? left, Control? right)
	{
		c.FocusNeighborTop = c.GetPathTo(up ?? c);
		c.FocusNeighborBottom = c.GetPathTo(down ?? c);
		c.FocusNeighborLeft = c.GetPathTo(left ?? c);
		c.FocusNeighborRight = c.GetPathTo(right ?? c);
	}

	/// <summary>Tab / Shift+Tab order: a closed ring through <paramref name="list"/> (so next / previous cannot leave it either).</summary>
	public static void Ring(params Control[] list)
	{
		for (int i = 0; i < list.Length; i++)
		{
			list[i].FocusNext = list[i].GetPathTo(list[(i + 1) % list.Length]);
			list[i].FocusPrevious = list[i].GetPathTo(list[(i + list.Length - 1) % list.Length]);
		}
	}
}

/// <summary>
/// Modal focus for an overlay (confirm box, settings sheet): while engaged, the controls underneath cannot take
/// keyboard / controller focus at all (FocusMode None), so no directional, Tab or hover move can reach them.
/// <see cref="Release"/> puts each control's previous focus mode back (a greyed Continue stays unfocusable).
/// </summary>
public sealed class FocusLock
{
	private readonly System.Collections.Generic.Dictionary<Control, Control.FocusModeEnum> _saved = new();
	public bool Active => _saved.Count > 0;

	public void Engage(System.Collections.Generic.IEnumerable<Control> underneath)
	{
		if (Active) return;
		foreach (var c in underneath) { _saved[c] = c.FocusMode; c.FocusMode = Control.FocusModeEnum.None; }
	}

	public void Release()
	{
		foreach (var (c, mode) in _saved) if (GodotObject.IsInstanceValid(c)) c.FocusMode = mode;
		_saved.Clear();
	}
}
