using System;
using System.Collections.Generic;
using YokaiFighters.Sim;

namespace YokaiFighters.Flow;

/// <summary>The top-level screens of the game. Boot enters <see cref="Title"/>.</summary>
public enum Screen { Title, ControlSelect, Settings, Fight }

/// <summary>The title menu's entries, top to bottom (docs/design/screens/title.png).</summary>
public enum TitleEntry { Start, Continue, Practice, Settings, Quit }

/// <summary>One title entry as the UI should show it: <see cref="Reason"/> says why a disabled entry is disabled ("" when enabled).</summary>
public readonly record struct TitleEntryState(TitleEntry Entry, bool Enabled, string Reason);

/// <summary>
/// The top-level screen state of the game: Title → ControlSelect → Fight, Title ↔ Settings, Fight → Title, plus Quit.
/// Pure C# (no Godot), so every transition is tested headless; <see cref="GameFlow"/> is the thin node that shows and
/// hides scenes when <see cref="Changed"/> fires. It never touches the sim: the fight scene only exists while
/// <see cref="Current"/> is <see cref="Screen.Fight"/>.
/// <para>
/// Continue and Practice have no backing system yet (save system, practice mode). They start disabled with a reason;
/// the ticket that builds one calls <see cref="Enable"/> and listens to <see cref="Activated"/> — that is the whole seam.
/// </para>
/// </summary>
public sealed class ScreenRouter
{
	public const string NoSaveReason = "No saved run yet: saving is not built.";
	public const string NoPracticeReason = "Practice mode is not built yet.";

	private readonly Dictionary<TitleEntry, string> _disabled = new()
	{
		[TitleEntry.Continue] = NoSaveReason,
		[TitleEntry.Practice] = NoPracticeReason,
	};

	public Screen Current { get; private set; } = Screen.Title;
	/// <summary>The scheme the next fight starts with (K1 Kata / K2 Kihon). Kihon by default (E18).</summary>
	public ControlScheme Scheme { get; private set; } = ControlScheme.Kihon;
	/// <summary>True once Quit was selected (the node quits the tree; tests read this).</summary>
	public bool QuitRequested { get; private set; }

	/// <summary>Raised after every screen change (from, to).</summary>
	public event Action<Screen, Screen>? Changed;
	/// <summary>Raised when <see cref="Scheme"/> changes.</summary>
	public event Action<ControlScheme>? SchemeChanged;
	/// <summary>Raised when an entry is enabled or disabled, so the title can redraw it.</summary>
	public event Action<TitleEntryState>? EntryChanged;
	/// <summary>Raised when an enabled Continue or Practice is selected. Nothing listens yet (no save system, no practice mode).</summary>
	public event Action<TitleEntry>? Activated;
	public event Action? Quit;

	public TitleEntryState Entry(TitleEntry entry) =>
		_disabled.TryGetValue(entry, out string? why) ? new(entry, false, why) : new(entry, true, "");

	/// <summary>All five entries in menu order.</summary>
	public IReadOnlyList<TitleEntryState> Entries
	{
		get
		{
			var list = new List<TitleEntryState>();
			foreach (TitleEntry e in Enum.GetValues<TitleEntry>()) list.Add(Entry(e));
			return list;
		}
	}

	public void Enable(TitleEntry entry)
	{
		if (_disabled.Remove(entry)) EntryChanged?.Invoke(Entry(entry));
	}

	public void Disable(TitleEntry entry, string reason)
	{
		if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("a disabled entry needs a reason the UI can show", nameof(reason));
		_disabled[entry] = reason;
		EntryChanged?.Invoke(Entry(entry));
	}

	/// <summary>A title entry was confirmed. False (and nothing changes) off the title screen or on a disabled entry.</summary>
	public bool Select(TitleEntry entry)
	{
		if (Current != Screen.Title || !Entry(entry).Enabled) return false;
		switch (entry)
		{
			case TitleEntry.Start: Go(Screen.ControlSelect); break;
			case TitleEntry.Settings: Go(Screen.Settings); break;
			case TitleEntry.Quit: QuitRequested = true; Quit?.Invoke(); break;
			default: Activated?.Invoke(entry); break;
		}
		return true;
	}

	/// <summary>Back out of Settings or ControlSelect to the title. False anywhere else.</summary>
	public bool Back()
	{
		if (Current is not (Screen.Settings or Screen.ControlSelect)) return false;
		Go(Screen.Title);
		return true;
	}

	/// <summary>Kata or Kihon for the next fight (control select, or the settings sheet). Refused once the fight is on.</summary>
	public bool ChooseScheme(ControlScheme scheme)
	{
		if (Current == Screen.Fight) return false;
		if (scheme == Scheme) return true;
		Scheme = scheme;
		SchemeChanged?.Invoke(scheme);
		return true;
	}

	/// <summary>Start Game on control select: the fight begins with <see cref="Scheme"/>.</summary>
	public bool Confirm()
	{
		if (Current != Screen.ControlSelect) return false;
		Go(Screen.Fight);
		return true;
	}

	/// <summary>The explicit bypass (bot, harness, <c>--fight</c>): from the title straight into a fight with <paramref name="scheme"/>.</summary>
	public bool StartFightDirect(ControlScheme scheme)
	{
		if (Current != Screen.Title) return false;
		ChooseScheme(scheme);
		Go(Screen.Fight);
		return true;
	}

	/// <summary>Leave the fight for the title (pause menu "Quit to title", later). False when not in a fight.</summary>
	public bool ReturnToTitle()
	{
		if (Current != Screen.Fight) return false;
		Go(Screen.Title);
		return true;
	}

	private void Go(Screen to)
	{
		Screen from = Current;
		Current = to;
		Changed?.Invoke(from, to);
	}
}
