using System;
using System.IO;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Settings;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-settings-persistence: the pure <see cref="SettingsStore"/> (round trip, missing / corrupt / older / newer /
/// partial files, a disk that refuses), the slider curve and the audio buses, <see cref="MenuSettings"/> saving on
/// change, the boot scene loading the file before the title and carrying the scheme to control select and the fight,
/// and isolation: nothing here, and nothing a test or the harness instances, touches the player's file. Every file
/// is under a temp directory.
/// </summary>
public static class SettingsTests
{
	static void WithTempFile(Action<string> body)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-settings-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try { body(Path.Combine(dir, SettingsStore.FileName)); }
		finally
		{
			MenuSettings.Reset();
			try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
		}
	}

	static readonly SettingsData Sample = new() { Master = 35, Music = 0, Effects = 100, Muted = true, Scheme = ControlScheme.Kata };

	// ---- the store (no Godot) ----

	[Test]
	public static void Store_RoundTrip() => WithTempFile(path =>
	{
		var store = new SettingsStore(path);
		Assert.True(store.Save(Sample), "saved");
		Assert.True(!File.Exists(path + ".tmp"), "no temp file left behind");
		SettingsLoad load = new SettingsStore(path).Load();
		Assert.Equal(SettingsLoadStatus.Loaded, load.Status, "status");
		Assert.Equal(0, load.Problems.Count, "no problems");
		Assert.True(load.Data == Sample, $"same values back: {load.Data}");
		string text = File.ReadAllText(path);
		Assert.True(text.Contains("\"version\": 1") && text.Contains("\"scheme\": \"kata\"") && text.Contains("\"effects\": 100"), "versioned, readable file: " + text);
		Assert.True(store.Save(SettingsData.Defaults) && new SettingsStore(path).Load().Data == SettingsData.Defaults, "overwrites");
	});

	[Test]
	public static void Store_MissingFile_IsDefaults_AndLoadWritesNothing() => WithTempFile(path =>
	{
		SettingsLoad load = new SettingsStore(path).Load();
		Assert.Equal(SettingsLoadStatus.Missing, load.Status, "status");
		Assert.True(load.Data == SettingsData.Defaults, "defaults");
		Assert.True(load.Data is { Master: 80, Music: 70, Effects: 80, Muted: false, Scheme: ControlScheme.Kihon }, "the defaults are 80 / 70 / 80, unmuted, Kihon (E18)");
		Assert.True(!File.Exists(path), "loading does not create the file");
		string nested = Path.Combine(Path.GetDirectoryName(path)!, "not", "there", "settings.json");
		Assert.Equal(SettingsLoadStatus.Missing, new SettingsStore(nested).Load().Status, "missing directory");
		Assert.True(new SettingsStore(nested).Save(Sample) && new SettingsStore(nested).Load().Data == Sample, "save creates the directory");
	});

	[Test]
	public static void Store_CorruptFile_IsDefaults_AndTheNextSaveRepairsIt() => WithTempFile(path =>
	{
		string[] corrupt =
		{
			"", "   ", "not json at all", "{ \"version\": 1, \"master\": 4", "[1, 2, 3]", "42", "null", "\"kata\"",
			"{ \"version\": 1, \"master\": 40 } trailing", "\0\0\0\u0001\u0002", "{{{{",
		};
		foreach (string text in corrupt)
		{
			File.WriteAllText(path, text);
			SettingsLoad load = new SettingsStore(path).Load();
			Assert.Equal(SettingsLoadStatus.Corrupt, load.Status, $"status for '{text}'");
			Assert.True(load.Data == SettingsData.Defaults && load.Problems.Count == 1, $"defaults with a reason for '{text}'");
		}
		File.WriteAllBytes(path, new byte[] { 0xff, 0xfe, 0x00, 0x80, 0x7b });
		Assert.Equal(SettingsLoadStatus.Corrupt, new SettingsStore(path).Load().Status, "binary junk");
		Assert.True(new SettingsStore(path).Save(Sample), "saving over a corrupt file");
		Assert.True(new SettingsStore(path).Load() is { Status: SettingsLoadStatus.Loaded } ok && ok.Data == Sample, "the file is good again");
	});

	[Test]
	public static void Store_OlderVersion_KeepsValidFields_AndIsRewrittenAsCurrent() => WithTempFile(path =>
	{
		File.WriteAllText(path, "{ \"version\": 0, \"master\": 20, \"music\": 55, \"effects\": 5, \"muted\": true, \"scheme\": \"Kata\" }");
		SettingsLoad load = new SettingsStore(path).Load();
		Assert.Equal(SettingsLoadStatus.OlderVersion, load.Status, "version 0");
		Assert.True(load.Data is { Master: 20, Music: 55, Effects: 5, Muted: true, Scheme: ControlScheme.Kata }, $"every valid field kept: {load.Data}");

		File.WriteAllText(path, "{ \"music\": 15, \"scheme\": \"kata\" }"); // no version at all, and only some fields
		load = new SettingsStore(path).Load();
		Assert.Equal(SettingsLoadStatus.OlderVersion, load.Status, "no version");
		Assert.True(load.Data is { Master: 80, Music: 15, Effects: 80, Muted: false, Scheme: ControlScheme.Kata }, $"valid fields kept, the rest default: {load.Data}");

		File.WriteAllText(path, "{ \"version\": \"one\", \"master\": 10 }");
		load = new SettingsStore(path).Load();
		Assert.True(load.Status == SettingsLoadStatus.OlderVersion && load.Data.Master == 10, "an unreadable version is treated as older, fields still kept");

		Assert.True(new SettingsStore(path).Save(load.Data), "save");
		load = new SettingsStore(path).Load();
		Assert.True(load.Status == SettingsLoadStatus.Loaded && load.Data.Master == 10, "rewritten as the current version");
	});

	[Test]
	public static void Store_NewerVersion_KeepsTheFieldsThisBuildKnows() => WithTempFile(path =>
	{
		File.WriteAllText(path, "{ \"version\": 7, \"master\": 45, \"music\": 30, \"effects\": 60, \"muted\": false, \"scheme\": \"kata\", \"voice\": 90, \"display\": { \"fullscreen\": true } }");
		SettingsLoad load = new SettingsStore(path).Load();
		Assert.Equal(SettingsLoadStatus.NewerVersion, load.Status, "status");
		Assert.True(load.Data is { Master: 45, Music: 30, Effects: 60, Scheme: ControlScheme.Kata }, $"known fields kept: {load.Data}");
	});

	[Test]
	public static void Store_PartialFile_KeepsValidFields_DefaultsTheRest() => WithTempFile(path =>
	{
		var d = SettingsData.Defaults;
		SettingsLoad Read(string json) { File.WriteAllText(path, json); return new SettingsStore(path).Load(); }

		SettingsLoad load = Read("{ \"version\": 1, \"music\": 25 }");
		Assert.Equal(SettingsLoadStatus.Partial, load.Status, "missing fields");
		Assert.True(load.Data == d with { Music = 25 }, $"music kept, others default: {load.Data}");
		Assert.Equal(4, load.Problems.Count, "one note per missing field");

		load = Read("{ \"version\": 1, \"master\": \"loud\", \"music\": 40, \"effects\": null, \"muted\": 1, \"scheme\": \"kata\" }");
		Assert.Equal(SettingsLoadStatus.Partial, load.Status, "wrong types");
		Assert.True(load.Data == d with { Music = 40, Scheme = ControlScheme.Kata }, $"wrong-typed fields default, valid ones kept: {load.Data}");

		load = Read("{ \"version\": 1, \"master\": 250, \"music\": -3, \"effects\": 42.6, \"muted\": true, \"scheme\": \"KIHON\" }");
		Assert.True(load.Data is { Master: 100, Music: 0, Effects: 43, Muted: true, Scheme: ControlScheme.Kihon }, $"levels clamped to 0-100 and rounded: {load.Data}");
		Assert.Equal(SettingsLoadStatus.Partial, load.Status, "clamping is reported");

		load = Read("{ \"version\": 1, \"master\": 1e999, \"music\": 60, \"effects\": 80, \"muted\": false, \"scheme\": \"kihon\" }");
		Assert.True(load.Data.Music == 60 && load.Data.Master is >= 0 and <= 100, $"an absurd number cannot break the rest: {load.Data}");

		foreach (string scheme in new[] { "\"tekken\"", "\"\"", "0", "1", "\"1\"", "true", "[\"kata\"]" })
		{
			load = Read("{ \"version\": 1, \"master\": 30, \"music\": 30, \"effects\": 30, \"muted\": false, \"scheme\": " + scheme + " }");
			Assert.True(load.Status == SettingsLoadStatus.Partial && load.Data == d with { Master = 30, Music = 30, Effects = 30 }, $"scheme {scheme} is not a scheme: Kihon, levels kept");
		}

		load = Read("{ \"version\": 1, \"master\": 30, \"music\": 30, \"effects\": 30, \"muted\": false, \"scheme\": \"kata\", \"extra\": [1, 2], // a comment\n }");
		Assert.True(load.Status == SettingsLoadStatus.Loaded && load.Data.Scheme == ControlScheme.Kata, "unknown fields, comments and a trailing comma are tolerated");
	});

	[Test]
	public static void Store_SaveThatCannotWrite_ReturnsFalse_NoThrow() => WithTempFile(path =>
	{
		File.WriteAllText(path, "{}");
		var blocked = new SettingsStore(Path.Combine(path, "inside-a-file", SettingsStore.FileName)); // its "directory" is a file
		Assert.True(!blocked.Save(Sample), "refused");
		Assert.True(!string.IsNullOrEmpty(blocked.LastSaveError), "with a reason");
		Assert.True(blocked.Load().Data == SettingsData.Defaults, "and loading from there is defaults, not a crash");
	});

	// ---- sound levels ----

	[Test]
	public static void AudioLevels_Curve_FullIsZeroDb_ZeroIsMute_Monotonic()
	{
		Assert.True(MathF.Abs(AudioLevels.ToDb(100)) < 0.001f, "100 = 0 dB (never boosted)");
		Assert.True(MathF.Abs(AudioLevels.ToDb(50) + 12.04f) < 0.01f, $"50 = -12 dB, got {AudioLevels.ToDb(50)}");
		Assert.True(MathF.Abs(AudioLevels.ToDb(10) + 40f) < 0.01f, $"10 = -40 dB, got {AudioLevels.ToDb(10)}");
		Assert.True(AudioLevels.IsMute(0) && AudioLevels.ToDb(0) == AudioLevels.SilentDb, "0 = mute");
		Assert.True(!AudioLevels.IsMute(1) && AudioLevels.ToDb(1) >= AudioLevels.SilentDb, "1 is quiet, not muted");
		Assert.True(AudioLevels.ToDb(-20) == AudioLevels.SilentDb && AudioLevels.ToDb(400) == AudioLevels.ToDb(100), "out of range is clamped");
		Assert.True(MathF.Abs(AudioLevels.ToDb(5) + 52.04f) < 0.01f, $"the slider's lowest notch (5) = -52 dB, got {AudioLevels.ToDb(5)}");
		for (int level = 2; level <= 100; level++) // level 1 sits on the -80 dB floor
			Assert.True(AudioLevels.ToDb(level) > AudioLevels.ToDb(level - 1) && AudioLevels.ToDb(level) <= 0f, $"louder at {level} than {level - 1}, never above 0 dB");
	}

	[Test]
	public static void BusLayout_HasMasterMusicEffects()
	{
		Assert.Equal("res://default_bus_layout.tres", ProjectSettings.GetSetting("audio/buses/default_bus_layout").AsString(), "the project's bus layout path");
		var layout = GD.Load<AudioBusLayout>("res://default_bus_layout.tres");
		Assert.True(layout != null, "bus layout loads");
		AudioServer.SetBusLayout(layout); // exactly the file's buses, whatever earlier tests created
		try
		{
			Assert.Equal(3, AudioServer.BusCount, "three buses");
			Assert.True(AudioServer.GetBusName(0) == MenuSettings.MasterBus && AudioServer.GetBusName(1) == MenuSettings.MusicBus && AudioServer.GetBusName(2) == MenuSettings.SfxBus, "Master, Music, Effects");
			Assert.True(MenuSettings.SfxBus == "Effects", "the effects bus is named as on the settings sheet");
			Assert.True(AudioServer.GetBusSend(1) == "Master" && AudioServer.GetBusSend(2) == "Master", "both feed Master");
			Assert.Equal((1, 2), MenuSettings.EnsureBuses(), "the settings use the layout's buses and add none");
			Assert.Equal(3, AudioServer.BusCount, "still three");
		}
		finally { MenuSettings.Reset(); }
	}

	[Test]
	public static void Levels_DriveTheBuses_MuteAtZero()
	{
		MenuSettings.Reset();
		try
		{
			int master = 0, music = AudioServer.GetBusIndex(MenuSettings.MusicBus), effects = AudioServer.GetBusIndex(MenuSettings.SfxBus);
			Assert.True(music > 0 && effects > 0 && music != effects, "buses exist");
			static bool Near(float a, float b) => MathF.Abs(a - b) < 0.01f;
			Assert.True(Near(AudioServer.GetBusVolumeDb(master), AudioLevels.ToDb(80)) && Near(AudioServer.GetBusVolumeDb(music), AudioLevels.ToDb(70)) && Near(AudioServer.GetBusVolumeDb(effects), AudioLevels.ToDb(80)), "defaults applied");

			MenuSettings.SetMaster(50); MenuSettings.SetMusic(25); MenuSettings.SetSfx(100);
			Assert.True(Near(AudioServer.GetBusVolumeDb(master), -12.04f), $"master 50 = -12 dB, got {AudioServer.GetBusVolumeDb(master)}");
			Assert.True(Near(AudioServer.GetBusVolumeDb(music), -24.08f), $"music 25 = -24 dB, got {AudioServer.GetBusVolumeDb(music)}");
			Assert.True(Near(AudioServer.GetBusVolumeDb(effects), 0f), "effects 100 = 0 dB");
			Assert.True(!AudioServer.IsBusMute(master) && !AudioServer.IsBusMute(music) && !AudioServer.IsBusMute(effects), "nothing muted above zero");

			MenuSettings.SetMusic(0);
			Assert.True(AudioServer.IsBusMute(music) && !AudioServer.IsBusMute(effects) && !AudioServer.IsBusMute(master), "music 0 mutes the music bus only");
			MenuSettings.SetSfx(0);
			Assert.True(AudioServer.IsBusMute(effects), "effects 0 mutes the effects bus");
			MenuSettings.SetMusic(5);
			Assert.True(!AudioServer.IsBusMute(music) && Near(AudioServer.GetBusVolumeDb(music), AudioLevels.ToDb(5)), "and back up unmutes it");
			MenuSettings.SetMaster(0);
			Assert.True(AudioServer.IsBusMute(master), "master 0 mutes everything");
			MenuSettings.SetMaster(60);
			Assert.True(!AudioServer.IsBusMute(master), "master back");
			MenuSettings.SetMuted(true);
			Assert.True(AudioServer.IsBusMute(master) && MenuSettings.Master == 60 && Near(AudioServer.GetBusVolumeDb(master), AudioLevels.ToDb(60)), "Mute all mutes Master and keeps the level");
			MenuSettings.SetMuted(false);
			Assert.True(!AudioServer.IsBusMute(master), "unmuted");
			MenuSettings.SetMusic(900); MenuSettings.SetSfx(-4);
			Assert.True(MenuSettings.Music == 100 && MenuSettings.Sfx == 0, "setters clamp");
		}
		finally { MenuSettings.Reset(); }
	}

	// ---- saving on change ----

	[Test]
	public static void MenuSettings_Attach_LoadsAndAppliesTheFile_ThenSavesEachChange(Node host) => WithTempFile(path =>
	{
		new SettingsStore(path).Save(Sample);
		int changes = 0;
		void Count() => changes++;
		MenuSettings.Changed += Count;
		try
		{
			SettingsLoad load = MenuSettings.Attach(new SettingsStore(path));
			Assert.True(load.Status == SettingsLoadStatus.Loaded && MenuSettings.Current == Sample, "the file's values are live");
			Assert.True(changes == 1, "listeners told once");
			Assert.True(AudioServer.IsBusMute(0) && AudioServer.IsBusMute(AudioServer.GetBusIndex(MenuSettings.MusicBus)), "and on the buses (muted, music 0)");
			Assert.True(MathF.Abs(AudioServer.GetBusVolumeDb(0) - AudioLevels.ToDb(35)) < 0.01f, "master 35 applied");

			DateTime stamp = File.GetLastWriteTimeUtc(path);
			string before = File.ReadAllText(path);
			MenuSettings.SetMaster(35); MenuSettings.SetScheme(ControlScheme.Kata); // no change
			Assert.True(File.ReadAllText(path) == before && File.GetLastWriteTimeUtc(path) == stamp, "setting the same value does not rewrite the file");

			MenuSettings.SetMusic(45);
			Assert.Equal(45, new SettingsStore(path).Load().Data.Music, "a change is on disk at once");
			MenuSettings.SetScheme(ControlScheme.Kihon);
			MenuSettings.SetMuted(false);
			Assert.True(new SettingsStore(path).Load().Data == Sample with { Music = 45, Scheme = ControlScheme.Kihon, Muted = false }, "every change saved, nothing else disturbed");

			// The sheet's own controls write the same file.
			var panel = GD.Load<PackedScene>("res://scenes/ui/settings_panel.tscn").Instantiate<SettingsPanel>();
			host.AddChild(panel);
			try
			{
				Assert.True((int)panel.SliderOf(0).Value == 35 && (int)panel.SliderOf(1).Value == 45 && (int)panel.SliderOf(2).Value == 100, "the sheet opens on the saved levels");
				panel.SliderOf(2).Value = 15;
				panel.KataButton.EmitSignal(BaseButton.SignalName.Pressed);
				panel.MuteButton.EmitSignal(BaseButton.SignalName.Pressed);
				Assert.True(new SettingsStore(path).Load().Data == new SettingsData { Master = 35, Music = 45, Effects = 15, Muted = true, Scheme = ControlScheme.Kata }, "slider, scheme and mute from the sheet are saved");
			}
			finally { host.RemoveChild(panel); panel.QueueFree(); }

			MenuSettings.Detach();
			MenuSettings.SetMusic(5);
			Assert.Equal(45, new SettingsStore(path).Load().Data.Music, "detached: no more saves");
			MenuSettings.Attach(new SettingsStore(path));
			MenuSettings.Reset();
			Assert.True(MenuSettings.Store == null && new SettingsStore(path).Load().Data.Music == 45, "Reset (the test hook) detaches and does not write defaults over the file");
		}
		finally { MenuSettings.Changed -= Count; }
	});

	[Test]
	public static void MenuSettings_Attach_BadFiles_NeverThrow_AndASaveFailureKeepsTheSession() => WithTempFile(path =>
	{
		File.WriteAllText(path, "{ broken");
		SettingsLoad load = MenuSettings.Attach(new SettingsStore(path));
		Assert.True(load.Status == SettingsLoadStatus.Corrupt && MenuSettings.Current == SettingsData.Defaults, "corrupt file = defaults, live");
		Assert.True(File.ReadAllText(path) == "{ broken", "the bad file is left alone until the player changes something");
		MenuSettings.SetScheme(ControlScheme.Kata);
		Assert.True(new SettingsStore(path).Load() is { Status: SettingsLoadStatus.Loaded } fixedUp && fixedUp.Data.Scheme == ControlScheme.Kata, "the first change writes a good file");

		MenuSettings.Attach(new SettingsStore(Path.Combine(path, "under-a-file", "settings.json"))); // cannot be written
		MenuSettings.SetMusic(10); // pushes a warning, must not throw
		Assert.Equal(10, MenuSettings.Music, "the value still applies for this session when the disk refuses");
	});

	// ---- the boot scene ----

	static void WithBoot(Node runner, string? settingsFile, Action<GameFlow> body)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.SettingsFile = settingsFile;
		flow.ConfigureFight = f => f.ExternalDrive = true;
		try { runner.AddChild(flow); body(flow); }
		finally
		{
			if (flow.GetParent() == runner) runner.RemoveChild(flow);
			flow.QueueFree();
			MenuSettings.Reset();
			FightScene.Sources = saved;
			FightScene.Run = savedRun;
		}
	}

	static void Press(Button b) => b.EmitSignal(BaseButton.SignalName.Pressed);

	[Test]
	public static void Boot_LoadsTheFileBeforeTheTitle_SchemeReachesControlSelectAndTheFight(Node runner) => WithTempFile(path =>
	{
		new SettingsStore(path).Save(new SettingsData { Master = 40, Music = 0, Effects = 65, Scheme = ControlScheme.Kata });
		WithBoot(runner, path, flow =>
		{
			Assert.True(flow.SettingsLoaded is { Status: SettingsLoadStatus.Loaded }, "the boot read the file");
			Assert.Equal(Screen.Title, flow.Router.Current, "still boots to the title");
			Assert.True(MenuSettings.Scheme == ControlScheme.Kata && flow.Router.Scheme == ControlScheme.Kata, "saved scheme is the menu scheme and the router's");
			Assert.True(MathF.Abs(AudioServer.GetBusVolumeDb(0) - AudioLevels.ToDb(40)) < 0.01f && AudioServer.IsBusMute(AudioServer.GetBusIndex(MenuSettings.MusicBus)), "saved levels are on the buses at the title");

			Press(flow.Title.SettingsButton);
			Assert.True((int)flow.Title.Settings!.SliderOf(0).Value == 40 && (int)flow.Title.Settings!.SliderOf(2).Value == 65, "the settings sheet shows the saved levels");
			flow.Router.Back();

			Press(flow.Title.StartButton);
			Assert.True(flow.ControlSelect.KataButton.Text.StartsWith("●") && !flow.ControlSelect.KihonButton.Text.StartsWith("●"), "control select opens on the saved scheme");
			Press(flow.ControlSelect.Start);
			Assert.Equal(ControlScheme.Kata, flow.Fight!.SchemeOf(0), "and the fight uses it");
			Assert.Equal(ControlScheme.Kata, new SettingsStore(path).Load().Data.Scheme, "file unchanged by starting");
		});
		Assert.True(MenuSettings.Store == null, "leaving the boot scene detaches the file");
	});

	[Test]
	public static void Boot_SchemeChosenOnControlSelect_IsSaved_AndIsTheNextLaunchDefault(Node runner) => WithTempFile(path =>
	{
		WithBoot(runner, path, flow =>
		{
			Assert.True(flow.SettingsLoaded is { Status: SettingsLoadStatus.Missing } && MenuSettings.Scheme == ControlScheme.Kihon, "first launch: no file, Kihon (E18)");
			Assert.True(!File.Exists(path), "a first launch writes nothing until something changes");
			Press(flow.Title.StartButton);
			Press(flow.ControlSelect.KataButton);
			Assert.Equal(ControlScheme.Kata, new SettingsStore(path).Load().Data.Scheme, "the control-select choice is saved");
			Press(flow.ControlSelect.Start);
			Assert.Equal(ControlScheme.Kata, flow.Fight!.SchemeOf(0), "fight uses it");
		});
		WithBoot(runner, path, flow => // the next launch
		{
			Assert.Equal(ControlScheme.Kata, flow.Router.Scheme, "next launch starts on Kata");
			Press(flow.Title.SettingsButton);
			Press(flow.Title.Settings!.KihonButton); // changed in settings this time
			flow.Title.Settings!.SliderOf(1).Value = 20;
			flow.Router.Back();
			Assert.True(new SettingsStore(path).Load().Data is { Scheme: ControlScheme.Kihon, Music: 20 }, "settings-sheet changes are saved to the same file");
			Press(flow.Title.StartButton);
			Assert.True(flow.ControlSelect.KihonButton.Text.StartsWith("●"), "one value: control select follows the sheet");
		});
		WithBoot(runner, path, flow =>
		{
			Assert.True(flow.Router.Scheme == ControlScheme.Kihon && MenuSettings.Music == 20, "third launch has both");
		});
	});

	[Test]
	public static void Boot_CorruptFile_StillBootsToTheTitleOnDefaults(Node runner) => WithTempFile(path =>
	{
		File.WriteAllText(path, "\u0000garbage{{");
		WithBoot(runner, path, flow =>
		{
			Assert.True(flow.SettingsLoaded is { Status: SettingsLoadStatus.Corrupt }, "noticed");
			Assert.True(flow.Router.Current == Screen.Title && flow.Title.Visible && MenuSettings.Current == SettingsData.Defaults, "title on defaults");
		});
	});

	[Test]
	public static void Boot_DirectFightArg_DoesNotOverwriteTheSavedScheme(Node runner) => WithTempFile(path =>
	{
		new SettingsStore(path).Save(SettingsData.Defaults with { Scheme = ControlScheme.Kihon });
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.SettingsFile = path;
		flow.DirectFight = ControlScheme.Kata; // what --fight=kata does
		flow.ConfigureFight = f => f.ExternalDrive = true;
		try
		{
			runner.AddChild(flow);
			Assert.Equal(ControlScheme.Kata, flow.Fight!.SchemeOf(0), "the bypass scheme is used for that fight");
			Assert.Equal(ControlScheme.Kihon, new SettingsStore(path).Load().Data.Scheme, "but the player's saved choice is untouched");
		}
		finally { runner.RemoveChild(flow); flow.QueueFree(); MenuSettings.Reset(); FightScene.Sources = saved; }
	});

	// ---- isolation: tests, the harness and ExternalDrive fights never use the player's file ----

	[Test]
	public static void Isolation_InstancedBootAndStandaloneFight_NeverTouchTheUserFile(Node runner)
	{
		string user = GameFlow.UserSettingsPath;
		Assert.True(user.EndsWith(SettingsStore.FileName) && Path.IsPathRooted(user), "user path resolves: " + user);
		bool existed = File.Exists(user);
		DateTime stamp = existed ? File.GetLastWriteTimeUtc(user) : default;

		// A boot scene that is not the running game's main scene (every test, every capture) gets no file.
		WithBoot(runner, null, flow =>
		{
			Assert.True(flow.GetTree().CurrentScene != flow, "the test runner, not the boot scene, is the main scene here");
			Assert.True(flow.SettingsLoaded == null && MenuSettings.Store == null, "no store attached");
			Assert.True(MenuSettings.Current == SettingsData.Defaults, "explicit defaults");
			MenuSettings.SetMusic(5);
			Press(flow.Title.StartButton);
			Press(flow.ControlSelect.KataButton);
			Press(flow.ControlSelect.Start);
			Assert.True(flow.Fight != null, "fight started");
		});

		// A fight scene on its own (tests, harness, ExternalDrive) takes no scheme from the menu settings at all.
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		MenuSettings.Reset();
		MenuSettings.SetScheme(ControlScheme.Kata);
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		try
		{
			runner.AddChild(scene);
			Assert.True(scene.PlayerScheme == null, "no scheme handed in");
			Assert.Equal(FightScene.DefaultScheme, scene.SchemeOf(0), "P1 = the sim's own default, not the menu's Kata");
			Assert.True(MenuSettings.Store == null, "and a fight scene attaches no store");
		}
		finally { runner.RemoveChild(scene); scene.QueueFree(); MenuSettings.Reset(); FightScene.Sources = saved; }

		Assert.True(File.Exists(user) == existed && (!existed || File.GetLastWriteTimeUtc(user) == stamp), "the player's settings file was neither created nor rewritten");
	}

	[Test]
	public static void Isolation_SimAiAndReplayCode_DoNotReferenceSettings()
	{
		string scripts = ProjectSettings.GlobalizePath("res://scripts");
		int files = 0;
		foreach (string dir in new[] { "Sim", "Ai", "Replay" })
			foreach (string file in Directory.GetFiles(Path.Combine(scripts, dir), "*.cs", SearchOption.AllDirectories))
			{
				files++;
				string text = File.ReadAllText(file);
				Assert.True(!text.Contains("MenuSettings") && !text.Contains("SettingsStore") && !text.Contains("YokaiFighters.Settings") && !text.Contains("user://"),
					$"{Path.GetFileName(file)} must not read settings or user files");
			}
		Assert.True(files > 20, $"scanned the sim sources ({files} files)");
	}
}
