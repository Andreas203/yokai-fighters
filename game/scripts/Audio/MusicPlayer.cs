using Godot;

namespace YokaiFighters.Audio;

/// <summary>
/// The single, pause-aware background music player. Registered as an autoload so scene transitions cannot restart it.
/// YOK-54 (V9, AM23): each stage has its own battle track (<see cref="StageTrack"/>), one for the whole fight; the tea
/// house plays one track throughout, not one per season (designer 2026-11-12). A track that is missing falls back to
/// <see cref="FallbackBattleTrackPath"/>. Presentation only: the sim never reads it.
/// </summary>
public partial class MusicPlayer : Node
{
	public const string MenuTrackPath = "res://sound/Dusk_Over_Yokai_Village_2026-10-20T172847.wav";
	/// <summary>The pre-AM23 battle loop: the fallback when a stage's own track is missing.</summary>
	public const string FallbackBattleTrackPath = "res://sound/Spirit_Duel_Loop_2026-10-20T182218.wav";
	public const string MusicDir = "res://sound/music/";
	/// <summary>The bamboo grove's theme, also the battle music before a stage is known.</summary>
	public const string BattleTrackPath = MusicDir + "battle_grove_t1.mp3";

	/// <summary>
	/// Pure: the battle track for a stage (<c>FightStage.Kind</c>). The grove, the placeholder and any unknown stage get
	/// the grove's theme.
	/// </summary>
	public static string StageTrack(string? stageKind) => stageKind switch
	{
		"shrine-gate" => MusicDir + "battle_shrine_t1.mp3",
		"riverbank" => MusicDir + "battle_swamp_t1.mp3", // the night swamp (AM21); the stage id keeps its old name
		"tea-house" => MusicDir + "tanuki_spring_t1.mp3",
		_ => BattleTrackPath,
	};

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

	/// <summary>Battle music before the stage is known (the grove's theme); the fight scene then asks for its stage's.</summary>
	public void PlayBattleMusic() => PlayTrack(BattleTrackPath);

	/// <summary>The stage's own track.</summary>
	public void PlayStageMusic(string? stageKind) => PlayTrack(StageTrack(stageKind));

	private void PlayTrack(string path)
	{
		if (_player == null) return;
		if (_currentTrackPath == path && _player.Playing) return;

		AudioStream? stream = Load(path);
		if (stream == null && path != MenuTrackPath && path != FallbackBattleTrackPath)
		{
			GD.PushWarning($"MusicPlayer: {path} missing; playing {FallbackBattleTrackPath}");
			stream = Load(FallbackBattleTrackPath);
		}
		if (stream == null)
		{
			GD.PushError($"MusicPlayer: could not load {path}");
			return;
		}

		_player.Stop();
		_player.Stream = stream;
		_currentTrackPath = path;
		_player.Play();
	}

	/// <summary>Loads a WAV or MP3 track set to loop over its whole length; null when missing.</summary>
	private static AudioStream? Load(string path)
	{
		if (!ResourceLoader.Exists(path)) return null;
		switch (ResourceLoader.Load(path))
		{
			case AudioStreamMP3 mp3:
				mp3.Loop = true;
				return mp3;
			case AudioStreamWav wav:
				// Configure the imported resource itself: the whole track loops forward, whatever its length.
				wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
				wav.LoopBegin = 0;
				wav.LoopEnd = (int)System.Math.Round(wav.GetLength() * wav.MixRate);
				return wav;
			default:
				return null;
		}
	}
}
