namespace YokaiFighters.Sim;

/// <summary>
/// Decouples the 60-tick simulation from the render rate (C1, F3). Fed an integer microsecond
/// timestamp each rendered frame, it returns how many sim ticks are due so that exactly one tick
/// runs per 1/60 s of wall time. Integer maths only: no float delta is ever accumulated.
/// If the game hitches for more than MaxCatchUp ticks, the surplus is dropped (the game slows
/// rather than spiralling); TicksDropped records it.
/// </summary>
public sealed class FixedStepClock
{
	public const long MicrosPerSecond = 1_000_000;

	public int MaxCatchUp { get; }
	public long TicksRun { get; private set; }
	public long TicksDropped { get; private set; }

	private long _startUsec = -1;

	public FixedStepClock(int maxCatchUp = 8) => MaxCatchUp = maxCatchUp;

	/// <summary>Ticks due at <paramref name="nowUsec"/>; the caller steps the sim that many times.</summary>
	public int Advance(long nowUsec)
	{
		if (_startUsec < 0) _startUsec = nowUsec;
		long target = (nowUsec - _startUsec) * SimConfig.TicksPerSecond / MicrosPerSecond;
		long due = target - TicksRun - TicksDropped;
		if (due <= 0) return 0;
		if (due > MaxCatchUp)
		{
			TicksDropped += due - MaxCatchUp;
			due = MaxCatchUp;
		}
		TicksRun += due;
		return (int)due;
	}

	public void Restart()
	{
		_startUsec = -1;
		TicksRun = 0;
		TicksDropped = 0;
	}
}
