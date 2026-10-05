using System;

namespace YokaiFighters.Sim;

/// <summary>
/// Ring buffer of one fighter's raw per-tick input (absolute directions, buttons). Age 0 is the
/// newest tick. Shared by Kata and Kihon (K3): only the parser on top differs.
/// </summary>
public sealed class InputBuffer
{
	readonly InputBits[] _ring;
	int _head = -1;

	public InputBuffer(int capacity) { _ring = new InputBits[Math.Max(2, capacity)]; }

	public int Capacity => _ring.Length;
	/// <summary>Ticks recorded so far (saturates at Capacity).</summary>
	public int Count { get; private set; }

	public void Push(FighterInput input)
	{
		_head = (_head + 1) % _ring.Length;
		_ring[_head] = input.Bits;
		if (Count < _ring.Length) Count++;
	}

	public void Clear() { _head = -1; Count = 0; }

	/// <summary>Input <paramref name="age"/> ticks ago; None beyond what was recorded.</summary>
	public InputBits At(int age)
	{
		if (age < 0 || age >= Count) return InputBits.None;
		return _ring[(_head - age + _ring.Length) % _ring.Length];
	}

	public int Dir(int age, int facing) => Numpad.From(At(age), facing);

	/// <summary>Buttons that went down on that tick (edge: held now, not the tick before).</summary>
	public InputBits PressedAt(int age) => At(age) & ~At(age + 1) & ~InputBits.Directions;

	/// <summary>Consecutive ticks (ending now) that all of <paramref name="b"/> have been held.</summary>
	public int HeldTicks(InputBits b)
	{
		int n = 0;
		while (n < Count && (At(n) & b) == b) n++;
		return n;
	}
}
