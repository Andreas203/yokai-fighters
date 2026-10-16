using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// V3 screen shake, presentation only (YOK-20; pure C#, no Godot). Started by a sim
/// <see cref="ImpactEvent"/> with Shake set (heavy hits and EX, never lights) and stepped once per
/// sim tick, so it lasts exactly ShakeFrames ticks at any render rate. Offsets are a fixed
/// pattern of 2-4 screen px per axis on the 1920-px canvas; FightScene.ShakeOffsetMeters converts px to
/// gameplay units through the view width (1 px = 0.75 units at AM8's 1,440-unit screen); no RNG.
/// </summary>
public sealed class ScreenShake
{
	private static readonly (int x, int y)[] Pattern =
		{ (4, -2), (-3, 2), (3, -2), (-2, 3), (2, -2), (-2, 2) };

	private readonly int _frames;
	private int _left, _index;

	public ScreenShake(int frames = 6) { _frames = frames; }

	public bool Active => _left > 0;

	/// <summary>Restart the shake; the tick that raised the impact shows the first offset.</summary>
	public void Start()
	{
		_left = _frames;
		_index = 0;
	}

	public void OnImpact(ImpactEvent e)
	{
		if (e.Shake) Start();
	}

	/// <summary>Call once before each sim tick.</summary>
	public void Advance()
	{
		if (_left == 0) return;
		_left--;
		_index++;
	}

	/// <summary>Current camera offset in screen px (1920-px canvas); (0, 0) when idle.</summary>
	public (int x, int y) OffsetPx => _left > 0 ? Pattern[_index % Pattern.Length] : (0, 0);

	public void Stop() => _left = 0;
}
