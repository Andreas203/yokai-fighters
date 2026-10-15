using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace YokaiFighters.Dev;

/// <summary>Whether dev mode can be switched on (<see cref="Available"/>) and whether it is (<see cref="On"/>).</summary>
public readonly record struct DevModeState(bool Available, bool On);

/// <summary>
/// YOK-123 dev mode flag (YOK-122). A development tool only: it never ships switched on.
/// <list type="bullet">
/// <item>Debug builds (editor, debug exports): available, off at launch; the dev menu's toggle switches it on (title screen only).</item>
/// <item>Any build launched with <c>--dev</c> (<c>godot --path game -- --dev</c>, or an export run with <c>--dev</c>): on at launch.</item>
/// <item>A release build without <c>--dev</c>: unavailable; <see cref="SetOn"/> refuses, so nothing can reach it.</item>
/// </list>
/// While on: the DEV badge shows on every screen (<see cref="DevShell"/>) and the boot flow saves to the dev slot
/// (<see cref="SavePathFor"/>), never the player's save. Nothing in <c>Sim/</c>, <c>Ai/</c> or <c>Replay/</c> reads it.
/// </summary>
public static class DevMode
{
	public const string Arg = "--dev";
	/// <summary>Opens the dev menu on the title, the map and the pause menu. F1-F10 and R are the fight's debug keys.</summary>
	public const Key MenuKey = Key.F11;
	/// <summary>Inserted before the save file's extension for the dev slot: <c>save.json</c> → <c>save.dev.json</c>.</summary>
	public const string SaveSuffix = ".dev";

	private static DevModeState _state = Launch();

	public static bool Available => _state.Available;
	public static bool On => _state.On;
	public static DevModeState State => _state;
	/// <summary>Raised after <see cref="On"/> changes.</summary>
	public static event Action? Changed;

	/// <summary>Pure rule: available in a debug build or with <c>--dev</c>; on at launch only with <c>--dev</c>.</summary>
	public static DevModeState Evaluate(bool debugBuild, IEnumerable<string> args)
	{
		bool arg = args.Any(a => a == Arg);
		return new DevModeState(debugBuild || arg, arg);
	}

	/// <summary>This process's state at launch: <see cref="OS.IsDebugBuild"/> and the user and engine command-line args.</summary>
	public static DevModeState Launch() => Evaluate(OS.IsDebugBuild(), OS.GetCmdlineUserArgs().Concat(OS.GetCmdlineArgs()));

	/// <summary>Switch dev mode on or off. Turning it on is refused (false, no change) when it is not <see cref="Available"/>.</summary>
	public static bool SetOn(bool on)
	{
		if (on && !_state.Available) return false;
		if (_state.On == on) return true;
		_state = _state with { On = on };
		Changed?.Invoke();
		return true;
	}

	/// <summary>Test hook: replace the state (as if launched with it; On is dropped when not Available). Raises <see cref="Changed"/> when <see cref="On"/> changes.</summary>
	public static void Configure(DevModeState state)
	{
		var next = state with { On = state.On && state.Available };
		bool changed = next.On != _state.On;
		_state = next;
		if (changed) Changed?.Invoke();
	}

	/// <summary>Test hook: back to <see cref="Launch"/>.</summary>
	public static void ResetToLaunch() => Configure(Launch());

	/// <summary>The dev slot for a save file path: same directory, <see cref="SaveSuffix"/> before the extension. Never equal to <paramref name="path"/>.</summary>
	public static string SavePathFor(string path)
	{
		string dir = Path.GetDirectoryName(path) ?? "";
		string name = Path.GetFileNameWithoutExtension(path) + SaveSuffix + Path.GetExtension(path);
		return dir.Length == 0 ? name : Path.Combine(dir, name);
	}

	/// <summary>The save file a boot should use: the dev slot while <see cref="On"/>, else <paramref name="playerPath"/>.</summary>
	public static string SavePath(string playerPath) => On ? SavePathFor(playerPath) : playerPath;
}
