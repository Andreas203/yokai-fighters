using System;
using YokaiFighters.Sim;

namespace YokaiFighters.Settings;

/// <summary>
/// The player's settings as plain values (no Godot): the three sound levels of the settings sheet (0-100), mute, and
/// the control scheme (K1 / K2). <see cref="Defaults"/> is what a first launch, a test and the harness get. Never
/// read by the sim: the fight takes its scheme as an explicit argument.
/// </summary>
public sealed record SettingsData
{
	public const int MinLevel = 0, MaxLevel = 100;

	public int Master { get; init; } = 80;
	public int Music { get; init; } = 70;
	public int Effects { get; init; } = 80;
	public bool Muted { get; init; }
	/// <summary>E18: Kihon is the default scheme.</summary>
	public ControlScheme Scheme { get; init; } = ControlScheme.Kihon;

	public static SettingsData Defaults => new();

	public static int ClampLevel(int v) => Math.Clamp(v, MinLevel, MaxLevel);
}

/// <summary>Slider position to bus volume. Pure, so the curve is testable without an audio server.</summary>
public static class AudioLevels
{
	/// <summary>Volume set alongside the bus mute at level 0.</summary>
	public const float SilentDb = -80f;

	/// <summary>True when the level is silence: the bus is muted rather than only turned down.</summary>
	public static bool IsMute(int level) => level <= 0;

	/// <summary>
	/// Squared taper: amplitude = (level / 100)^2, so 100 = 0 dB, 50 = -12 dB, 25 = -24 dB, 10 = -40 dB. A straight
	/// amplitude slider (50 = -6 dB) sounds as if nothing happens over its top half; squaring spreads the audible
	/// change along the travel. Never above 0 dB; 0 = <see cref="SilentDb"/> (and the bus is muted).
	/// </summary>
	public static float ToDb(int level)
	{
		level = SettingsData.ClampLevel(level);
		if (IsMute(level)) return SilentDb;
		return (float)Math.Max(SilentDb, 40.0 * Math.Log10(level / 100.0));
	}
}
