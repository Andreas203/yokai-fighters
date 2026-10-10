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

	/// <summary>Wraps focus from the first/last of a vertical button list to the other end (list order = top to bottom).</summary>
	public static void WrapVertical(params Control[] list)
	{
		if (list.Length < 2) return;
		list[0].FocusNeighborTop = list[0].GetPathTo(list[^1]);
		list[^1].FocusNeighborBottom = list[^1].GetPathTo(list[0]);
	}
}
