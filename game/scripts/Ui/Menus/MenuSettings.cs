using System;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-58 MOCKUP: in-memory sound and control settings shared by the title and pause menus. Nothing is saved to
/// disk and the fight's scheme is not touched (that wiring is a follow-up ticket); sound does drive the real
/// audio buses so the sliders can be heard. Buses "Music" and "SFX" are created on first use, sent to Master.
/// </summary>
public static class MenuSettings
{
	public const string MusicBus = "Music", SfxBus = "SFX";

	public static int Master { get; private set; } = 80;
	public static int Music { get; private set; } = 70;
	public static int Sfx { get; private set; } = 80;
	public static bool Muted { get; private set; }
	/// <summary>E18: both players default to Kihon in the demo.</summary>
	public static ControlScheme Scheme { get; private set; } = ControlScheme.Kihon;

	public static event Action? Changed;

	public static void SetMaster(int v) { Master = Math.Clamp(v, 0, 100); Apply(); }
	public static void SetMusic(int v) { Music = Math.Clamp(v, 0, 100); Apply(); }
	public static void SetSfx(int v) { Sfx = Math.Clamp(v, 0, 100); Apply(); }
	public static void SetMuted(bool m) { Muted = m; Apply(); }
	public static void SetScheme(ControlScheme s) { Scheme = s; Changed?.Invoke(); }

	/// <summary>Test hook: back to the defaults.</summary>
	public static void Reset() { Master = 80; Music = 70; Sfx = 80; Muted = false; Scheme = ControlScheme.Kihon; Apply(); }

	/// <summary>Creates the Music / SFX buses if missing and returns their indices.</summary>
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
			AudioServer.SetBusSend(i, "Master");
			return i;
		}
	}

	private static void Apply()
	{
		var (m, s) = EnsureBuses();
		AudioServer.SetBusVolumeDb(0, Db(Master));
		AudioServer.SetBusMute(0, Muted);
		AudioServer.SetBusVolumeDb(m, Db(Music));
		AudioServer.SetBusVolumeDb(s, Db(Sfx));
		Changed?.Invoke();
	}

	private static float Db(int percent) => percent <= 0 ? -80f : Mathf.LinearToDb(percent / 100f);

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
