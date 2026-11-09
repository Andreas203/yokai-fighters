using System.Collections.Generic;
using YokaiFighters.Fight;

namespace YokaiFighters.Audio;

/// <summary>Where a chosen hit sample goes: <see cref="HitSoundPool"/> in the game, a recorder in tests.</summary>
public interface IHitSoundOut
{
	void Play(string sample, int volumeDb);
}

/// <summary>
/// YOK-93 (V6): the data-driven <see cref="IHitCueSink"/>. Each <see cref="HitCue"/> resolves through the
/// <see cref="HitCueLibrary"/> to a thud and an optional yokai sting; offset 0 plays at once (the impact tick), a later
/// one waits until <see cref="Advance"/> reaches its tick. Pure C# and presentation only: it is timed by the match's
/// own tick (which the scene passes in after every sim step), so it freezes with pause and frame step and never feeds
/// back into the sim, the AI, replays or the harness. A tick earlier than the last one seen (a reset or a new match)
/// drops whatever was still waiting.
/// </summary>
public sealed class DataHitCueSink : IHitCueSink
{
	/// <summary>Sounds kept waiting at once; the oldest goes first (more than a fight ever stacks inside 30 ticks).</summary>
	public const int MaxPending = 16;

	private readonly List<HitSound> _pending = new();
	private int _lastTick = -1;

	public HitCueLibrary Library { get; }
	public IHitSoundOut Out { get; }

	/// <summary>Sounds resolved but not yet started, oldest first.</summary>
	public IReadOnlyList<HitSound> Pending => _pending;

	public DataHitCueSink(HitCueLibrary library, IHitSoundOut output)
	{
		Library = library;
		Out = output;
	}

	public void Play(in HitCue cue)
	{
		Sync(cue.Tick);
		if (Library.Resolve(cue) is not { } s) return;
		if (s.StartTick <= cue.Tick) { Emit(s); return; }
		if (_pending.Count >= MaxPending) _pending.RemoveAt(0);
		_pending.Add(s);
	}

	/// <summary>The match has stepped to <paramref name="tick"/>: start every waiting sound due by then, in order.</summary>
	public void Advance(int tick)
	{
		Sync(tick);
		for (int i = 0; i < _pending.Count;)
		{
			if (_pending[i].StartTick <= tick) { var s = _pending[i]; _pending.RemoveAt(i); Emit(s); }
			else i++;
		}
	}

	/// <summary>Drops the waiting sounds (a replaced match).</summary>
	public void Clear()
	{
		_pending.Clear();
		_lastTick = -1;
	}

	private void Sync(int tick)
	{
		if (tick < _lastTick) _pending.Clear();
		_lastTick = tick;
	}

	private void Emit(in HitSound s)
	{
		Out.Play(s.Thud, s.VolumeDb);
		if (s.Sting != null) Out.Play(s.Sting, s.VolumeDb);
	}
}
