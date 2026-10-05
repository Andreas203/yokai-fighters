using System;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// Minimal keyboard and gamepad mapping into raw <see cref="FighterInput"/> (both schemes need
/// keyboard and pad, K2). Fixed default layouts only; rebinding and scheme select are YOK-25.
/// Reading devices is the only non-deterministic step: the sim sees just the resulting bits,
/// which are what a replay records.
/// Keyboard P1: WASD move, U/I/O = light/medium/heavy punch, J/K/L = light/medium/heavy kick,
/// Space = Special. Keyboard P2: arrows move, numpad 4/5/6 punches, 1/2/3 kicks, numpad 0 Special.
/// Pad (Xbox names): d-pad or left stick move; X/Y/RB punches, A/B/RT kicks, LB Special.
/// </summary>
public static class InputDevices
{
	public sealed record KeyLayout(Key Left, Key Right, Key Up, Key Down,
		Key LP, Key MP, Key HP, Key LK, Key MK, Key HK, Key Special);

	public static readonly KeyLayout P1Keys = new(Key.A, Key.D, Key.W, Key.S,
		Key.U, Key.I, Key.O, Key.J, Key.K, Key.L, Key.Space);

	public static readonly KeyLayout P2Keys = new(Key.Left, Key.Right, Key.Up, Key.Down,
		Key.Kp4, Key.Kp5, Key.Kp6, Key.Kp1, Key.Kp2, Key.Kp3, Key.Kp0);

	/// <summary>Stick deflection (0..1) that counts as a direction; 8-way by axis threshold.</summary>
	public const float StickDeadzone = 0.5f;
	public const float TriggerThreshold = 0.5f;

	public static InputBits FromKeys(KeyLayout k, Func<Key, bool> down)
	{
		InputBits b = InputBits.None;
		if (down(k.Left)) b |= InputBits.Left;
		if (down(k.Right)) b |= InputBits.Right;
		if (down(k.Up)) b |= InputBits.Up;
		if (down(k.Down)) b |= InputBits.Down;
		if (down(k.LP)) b |= InputBits.LightPunch;
		if (down(k.MP)) b |= InputBits.MediumPunch;
		if (down(k.HP)) b |= InputBits.HeavyPunch;
		if (down(k.LK)) b |= InputBits.LightKick;
		if (down(k.MK)) b |= InputBits.MediumKick;
		if (down(k.HK)) b |= InputBits.HeavyKick;
		if (down(k.Special)) b |= InputBits.Special;
		return b;
	}

	/// <summary>Pad state to bits. Stick y is Godot's convention: negative = up.</summary>
	public static InputBits FromPad(Func<JoyButton, bool> button, float stickX, float stickY, float rightTrigger)
	{
		InputBits b = InputBits.None;
		if (button(JoyButton.DpadLeft) || stickX <= -StickDeadzone) b |= InputBits.Left;
		if (button(JoyButton.DpadRight) || stickX >= StickDeadzone) b |= InputBits.Right;
		if (button(JoyButton.DpadUp) || stickY <= -StickDeadzone) b |= InputBits.Up;
		if (button(JoyButton.DpadDown) || stickY >= StickDeadzone) b |= InputBits.Down;
		if (button(JoyButton.X)) b |= InputBits.LightPunch;
		if (button(JoyButton.Y)) b |= InputBits.MediumPunch;
		if (button(JoyButton.RightShoulder)) b |= InputBits.HeavyPunch;
		if (button(JoyButton.A)) b |= InputBits.LightKick;
		if (button(JoyButton.B)) b |= InputBits.MediumKick;
		if (rightTrigger >= TriggerThreshold) b |= InputBits.HeavyKick;
		if (button(JoyButton.LeftShoulder)) b |= InputBits.Special;
		return b;
	}

	/// <summary>
	/// Live read for one player: their keyboard layout OR'd with pad <paramref name="player"/>
	/// (pad 0 for P1, pad 1 for P2) if connected.
	/// </summary>
	public static FighterInput Read(int player)
	{
		var layout = player == 0 ? P1Keys : P2Keys;
		InputBits b = FromKeys(layout, Input.IsPhysicalKeyPressed);
		if (Input.GetConnectedJoypads().Contains(player))
			b |= FromPad(btn => Input.IsJoyButtonPressed(player, btn),
				Input.GetJoyAxis(player, JoyAxis.LeftX), Input.GetJoyAxis(player, JoyAxis.LeftY),
				Input.GetJoyAxis(player, JoyAxis.TriggerRight));
		return new FighterInput(b);
	}
}
