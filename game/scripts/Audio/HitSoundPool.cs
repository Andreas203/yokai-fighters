using System.Collections.Generic;
using Godot;
using YokaiFighters.Ui;

namespace YokaiFighters.Audio;

/// <summary>
/// YOK-93 (V6): the hit-sound players. A few pausable <see cref="AudioStreamPlayer"/>s on the Effects bus
/// (<see cref="MenuSettings.SfxBus"/>), used in turn, so a thud and its sting overlap and a new hit cuts only the
/// oldest sound. Samples load once and are cached; a path that does not load is warned about once and skipped.
/// Presentation only.
/// </summary>
public partial class HitSoundPool : Node, IHitSoundOut
{
	public const string NodeName = "HitSoundPool";
	public const int Size = 6;

	private readonly AudioStreamPlayer[] _players = new AudioStreamPlayer[Size];
	private readonly Dictionary<string, AudioStream?> _streams = new();
	private int _next;

	public IReadOnlyList<AudioStreamPlayer> Players => _players;

	public HitSoundPool()
	{
		Name = NodeName;
		ProcessMode = ProcessModeEnum.Pausable;
		for (int i = 0; i < Size; i++)
		{
			_players[i] = new AudioStreamPlayer
			{
				Name = $"HitCue{i + 1}",
				Bus = MenuSettings.SfxBus,
				ProcessMode = ProcessModeEnum.Pausable,
			};
			AddChild(_players[i]);
		}
	}

	public void Play(string sample, int volumeDb)
	{
		if (!_streams.TryGetValue(sample, out var stream))
		{
			stream = ResourceLoader.Exists(sample) ? ResourceLoader.Load<AudioStream>(sample) : null;
			if (stream == null) GD.PushWarning($"HitSoundPool: could not load hit sound {sample}");
			_streams[sample] = stream;
		}
		if (stream == null) return;
		var player = _players[_next++ % Size];
		player.Stop();
		player.Stream = stream;
		player.VolumeDb = volumeDb;
		player.Play();
	}
}
