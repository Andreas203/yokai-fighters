using System;
using Godot;

namespace YokaiFighters.Ui.Kit;

public enum InputDevice { Keyboard, Pad }

/// <summary>
/// Which device the player touched last, and the text on a key chip for each menu action on that device.
/// The game has no such tracker elsewhere (<c>InputDevices</c> reads the fight, <c>MenuInput</c> answers menu keys), so the kit owns one:
/// any <see cref="KeyChipFooter"/> in the tree feeds it from <c>_Input</c>; mouse counts as keyboard. Pad letters are Xbox layout.
/// </summary>
public static class InputGlyphs
{
	public const float StickDeadzone = 0.5f;
	public static InputDevice Last { get; private set; } = InputDevice.Keyboard;
	public static event Action<InputDevice>? Changed;

	public static void Set(InputDevice d)
	{
		if (d == Last) return;
		Last = d;
		Changed?.Invoke(d);
	}

	/// <summary>Updates <see cref="Last"/> from an input event. Returns the device it indicates, or null when the event says nothing (stick drift, mouse motion, key release).</summary>
	public static InputDevice? Observe(InputEvent e)
	{
		InputDevice? d = e switch
		{
			InputEventKey { Pressed: true } => InputDevice.Keyboard,
			InputEventMouseButton { Pressed: true } => InputDevice.Keyboard,
			InputEventJoypadButton { Pressed: true } => InputDevice.Pad,
			InputEventJoypadMotion m when Mathf.Abs(m.AxisValue) >= StickDeadzone => InputDevice.Pad,
			_ => null,
		};
		if (d is { } dev) Set(dev);
		return d;
	}

	/// <summary>Chip text for a menu action. Unknown actions come back upper-cased so a typo is visible rather than blank.</summary>
	public static string Glyph(string action, InputDevice device) => (action, device) switch
	{
		("confirm", InputDevice.Keyboard) => "ENTER",
		("confirm", InputDevice.Pad) => "A",
		("back", InputDevice.Keyboard) => "ESC",
		("back", InputDevice.Pad) => "B",
		("pause", InputDevice.Keyboard) => "ESC",
		("pause", InputDevice.Pad) => "START",
		("frames", InputDevice.Keyboard) => "TAB",
		("frames", InputDevice.Pad) => "Y",
		("scheme", InputDevice.Keyboard) => "Q",
		("scheme", InputDevice.Pad) => "X",
		("choose", InputDevice.Keyboard) => "LEFT / RIGHT",
		("choose", InputDevice.Pad) => "D-PAD",
		("move", InputDevice.Keyboard) => "UP / DOWN",
		("move", InputDevice.Pad) => "D-PAD",
		("tab", InputDevice.Keyboard) => "Q / E",
		("tab", InputDevice.Pad) => "LB / RB",
		_ => action.ToUpperInvariant(),
	};
}
