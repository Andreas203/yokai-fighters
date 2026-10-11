using System;
using Godot;
using YokaiFighters.Settings;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

/// <summary>
/// The live settings shared by every menu (title, control select, settings sheet, pause, move list): the sound levels
/// drive the audio buses, the scheme is the one control select shows and the boot flow hands to the fight.
/// <para>
/// Saved only when a <see cref="SettingsStore"/> is attached (<see cref="Attach"/>), which the boot scene does when it
/// is the running game. Nothing else attaches one: tests, captures, the harness and a fight scene opened on its own
/// run on <see cref="SettingsData.Defaults"/> and never read or write the player's file. The sim never reads this
/// class; the fight gets its scheme as an argument (<c>FightScene.PlayerScheme</c>).
/// </para>
/// </summary>
public static class MenuSettings
{
	/// <summary>Bus names of <c>res://default_bus_layout.tres</c> (Master is bus 0).</summary>
	public const string MasterBus = "Master", MusicBus = "Music", SfxBus = "Effects";

	private static SettingsData _data = SettingsData.Defaults;

	public static int Master => _data.Master;
	public static int Music => _data.Music;
	public static int Sfx => _data.Effects;
	public static bool Muted => _data.Muted;
	/// <summary>E18: Kihon unless the player chose otherwise.</summary>
	public static ControlScheme Scheme => _data.Scheme;
	/// <summary>The current values as plain data.</summary>
	public static SettingsData Current => _data;

	/// <summary>Where changes are saved; null (the default) = in memory only.</summary>
	public static SettingsStore? Store { get; private set; }

	public static event Action? Changed;

	public static void SetMaster(int v) => Set(_data with { Master = SettingsData.ClampLevel(v) });
	public static void SetMusic(int v) => Set(_data with { Music = SettingsData.ClampLevel(v) });
	public static void SetSfx(int v) => Set(_data with { Effects = SettingsData.ClampLevel(v) });
	public static void SetMuted(bool m) => Set(_data with { Muted = m });
	public static void SetScheme(ControlScheme s) => Set(_data with { Scheme = s });

	/// <summary>
	/// Loads the store's file into the live settings (buses included) and saves every later change to it. Never
	/// throws: a missing, corrupt or older file gives defaults for whatever could not be read (see the result).
	/// </summary>
	public static SettingsLoad Attach(SettingsStore store)
	{
		Store = store;
		SettingsLoad load = store.Load();
		_data = load.Data;
		Apply();
		return load;
	}

	/// <summary>Stops saving; the values stay.</summary>
	public static void Detach() => Store = null;

	/// <summary>Test hook: back to the defaults, with no store attached (so a test can never write a settings file by accident).</summary>
	public static void Reset()
	{
		Store = null;
		_data = SettingsData.Defaults;
		Apply();
	}

	private static void Set(SettingsData next)
	{
		bool changed = next != _data;
		_data = next;
		Apply();
		if (changed && Store is { } store && !store.Save(_data))
			GD.PushWarning($"MenuSettings: could not save {store.Path}: {store.LastSaveError}");
	}

	/// <summary>The Music / Effects bus indices; the buses come from the bus layout and are created here only if it did not load.</summary>
	public static (int Music, int Sfx) EnsureBuses()
	{
		return (Ensure(MusicBus), Ensure(SfxBus));
		static int Ensure(string name)
		{
			int i = AudioServer.GetBusIndex(name);
			if (i >= 0) return i;
			AudioServer.AddBus();
			i = AudioServer.BusCount - 1;
			AudioServer.SetBusName(i, name);
			AudioServer.SetBusSend(i, MasterBus);
			return i;
		}
	}

	/// <summary>Levels to buses: <see cref="AudioLevels.ToDb"/>, and a bus at 0 is muted. "Mute all" mutes Master and leaves the levels alone.</summary>
	private static void Apply()
	{
		var (m, s) = EnsureBuses();
		SetBus(0, Master, Muted);
		SetBus(m, Music, false);
		SetBus(s, Sfx, false);
		Changed?.Invoke();

		static void SetBus(int bus, int level, bool muted)
		{
			AudioServer.SetBusVolumeDb(bus, AudioLevels.ToDb(level));
			AudioServer.SetBusMute(bus, muted || AudioLevels.IsMute(level));
		}
	}

	/// <summary>Placeholder preview cues, synthesised (no sound asset yet; the sourced pack replaces them).</summary>
	public enum Cue { Bell, Hit, Pad }

	public static AudioStreamWav MakeCue(Cue cue)
	{
		const int rate = 22050;
		double seconds = cue switch { Cue.Bell => 0.9, Cue.Hit => 0.25, _ => 1.4 };
		int n = (int)(rate * seconds);
		var data = new byte[n * 2];
		var rng = new Random(7);
		for (int i = 0; i < n; i++)
		{
			double t = i / (double)rate;
			double v = cue switch
			{
				Cue.Bell => Math.Sin(2 * Math.PI * 660 * t) * Math.Exp(-5 * t) + 0.4 * Math.Sin(2 * Math.PI * 1320 * t) * Math.Exp(-8 * t),
				Cue.Hit => (rng.NextDouble() * 2 - 1) * Math.Exp(-30 * t) * 0.8 + Math.Sin(2 * Math.PI * 110 * t) * Math.Exp(-14 * t),
				_ => (Math.Sin(2 * Math.PI * 220 * t) + 0.7 * Math.Sin(2 * Math.PI * 277.2 * t) + 0.6 * Math.Sin(2 * Math.PI * 330 * t)) / 2.3 * Math.Min(1, t * 6) * Math.Min(1, (seconds - t) * 4),
			};
			short s = (short)(Math.Clamp(v, -1, 1) * 0.6 * short.MaxValue);
			data[i * 2] = (byte)(s & 0xff);
			data[i * 2 + 1] = (byte)((s >> 8) & 0xff);
		}
		return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Stereo = false, Data = data };
	}
}
