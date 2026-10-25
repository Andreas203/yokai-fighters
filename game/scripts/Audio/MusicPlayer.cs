using Godot;

namespace YokaiFighters.Audio;

/// <summary>
/// The single, pause-aware background music player. Registered as an autoload so scene transitions cannot restart it.
/// </summary>
public partial class MusicPlayer : Node
{
	public const string MenuTrackPath = "res://sound/Dusk_Over_Yokai_Village_2026-10-20T172847.wav";
	public const string BattleTrackPath = "res://sound/Spirit_Duel_Loop_2026-10-20T182218.wav";
	private const int TrackLoopEnd = 2_880_000;

	public static MusicPlayer? Instance { get; private set; }

	private AudioStreamPlayer? _player;
	private string? _currentTrackPath;

	public override void _EnterTree()
	{
		if (Instance != null && Instance != this)
		{
			GD.PushWarning("MusicPlayer: duplicate singleton discarded.");
			QueueFree();
			return;
		}
		Instance = this;
	}

	public override void _Ready()
	{
		_player = GetNodeOrNull<AudioStreamPlayer>("BackgroundMusic");
		if (_player == null)
		{
			_player = new AudioStreamPlayer { Name = "BackgroundMusic" };
			AddChild(_player);
		}

		_player.ProcessMode = ProcessModeEnum.Pausable;
		_player.Bus = Ui.MenuSettings.MusicBus;
		PlayMenuMusic();
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
	}

	public void PlayMenuMusic() => PlayTrack(MenuTrackPath);

	public void PlayBattleMusic() => PlayTrack(BattleTrackPath);

	private void PlayTrack(string path)
	{
		if (_player == null) return;
		if (_currentTrackPath == path && _player.Playing) return;

		AudioStreamWav? track = ResourceLoader.Load<AudioStreamWav>(path);
		if (track == null)
		{
			GD.PushError($"MusicPlayer: could not load {path}");
			return;
		}

		// Configure the imported resource itself: the complete 60-second, 48 kHz track loops forward.
		track.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
		track.LoopBegin = 0;
		track.LoopEnd = TrackLoopEnd;

		_player.Stop();
		_player.Stream = track;
		_currentTrackPath = path;
		_player.Play();
	}
}
