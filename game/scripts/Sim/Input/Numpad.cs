namespace YokaiFighters.Sim;

/// <summary>
/// Numpad notation relative to facing: 6 = forward (toward the opponent), 4 = back, 2 = down,
/// 8 = up, 5 = neutral; diagonals 1/3/7/9. Facing +1 means Right is forward, -1 means Left is.
/// Opposing directions cancel (left+right = no horizontal, up+down = no vertical; SOCD neutral).
/// </summary>
public static class Numpad
{
	public static int From(InputBits bits, int facing)
	{
		bool l = (bits & InputBits.Left) != 0, r = (bits & InputBits.Right) != 0;
		bool u = (bits & InputBits.Up) != 0, d = (bits & InputBits.Down) != 0;
		int h = (r && !l ? 1 : 0) - (l && !r ? 1 : 0); // +1 screen-right
		h *= facing >= 0 ? 1 : -1;                     // +1 forward
		int v = (u && !d ? 1 : 0) - (d && !u ? 1 : 0);  // +1 up
		return 5 + h + 3 * v;
	}

	/// <summary>Absolute direction bits for a numpad direction under a facing (tests, AI, replays).</summary>
	public static InputBits ToBits(int numpad, int facing)
	{
		int h = (numpad - 1) % 3 - 1, v = (numpad - 1) / 3 - 1;
		h *= facing >= 0 ? 1 : -1;
		InputBits b = InputBits.None;
		if (h > 0) b |= InputBits.Right;
		if (h < 0) b |= InputBits.Left;
		if (v > 0) b |= InputBits.Up;
		if (v < 0) b |= InputBits.Down;
		return b;
	}

	public static bool IsDown(int n) => n <= 3;
	public static bool IsUp(int n) => n >= 7;
	public static bool IsForward(int n) => n % 3 == 0;
	public static bool IsBack(int n) => n % 3 == 1;
}
