using System;
using System.Collections.Generic;
using YokaiFighters.Sim;

namespace YokaiFighters.Flow;

/// <summary>The top-level screens of the game. Boot enters <see cref="Title"/>.</summary>
public enum Screen { Title, ControlSelect, Settings, Fight, Map }

/// <summary>The title menu's entries, top to bottom (docs/design/screens/title.png).</summary>
public enum TitleEntry { Start, Continue, Practice, Settings, Quit }

/// <summary>One title entry as the UI should show it: <see cref="Reason"/> says why a disabled entry is disabled ("" when enabled).</summary>
public readonly record struct TitleEntryState(TitleEntry Entry, bool Enabled, string Reason);

/// <summary>
/// The top-level screen state of the game: Title → ControlSelect → Map ⇄ Fight (YOK-77: a run is map, node, map...),
/// Title ↔ Settings, Map or Fight → Title, plus Quit.
/// Pure C# (no Godot), so every transition is tested headless; <see cref="GameFlow"/> is the thin node that shows and
/// hides scenes when <see cref="Changed"/> fires. It never touches the sim: the fight scene only exists while
/// <see cref="Current"/> is <see cref="Screen.Fight"/>.
/// <para>
/// Continue and Practice start disabled with a reason. Continue is enabled by the boot flow while the save file holds
/// a run (YOK-87, <see cref="ContinueRun"/>); practice mode is not built: its ticket calls <see cref="Enable"/> and
/// listens to <see cref="Activated"/>.
/// </para>
/// </summary>
public sealed class ScreenRouter
{
	public const string NoSaveReason = "No saved run yet.";
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
	/// <summary>Raised when an enabled Continue or Practice is selected (Continue: the boot flow restores the saved run).</summary>
	public event Action<TitleEntry>? Activated;
	public event Action? Quit;
	/// <summary>YOK-104: true while the merchant's shop screen is open over the rest stop (a sub-screen of the node, not a top-level screen).</summary>
	public bool ShopOpen { get; private set; }

	/// <summary>Merchant stop to shop. Only while a node scene runs (<see cref="Screen.Fight"/>); False, changing nothing, anywhere else or when already open.</summary>
	public bool OpenShop()
	{
		if (Current != Screen.Fight || ShopOpen) return false;
		ShopOpen = true;
		return true;
	}

	/// <summary>Shop back to the rest stop. False when it was not open.</summary>
	public bool CloseShop()
	{
		if (!ShopOpen) return false;
		ShopOpen = false;
		return true;
	}

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

	/// <summary>Kata or Kihon for the next run (control select, or the settings sheet). Refused once the run is on (map or fight).</summary>
	public bool ChooseScheme(ControlScheme scheme)
	{
		if (Current is Screen.Fight or Screen.Map) return false;
		if (scheme == Scheme) return true;
		Scheme = scheme;
		SchemeChanged?.Invoke(scheme);
		return true;
	}

	/// <summary>Start Game on control select: a new run opens on its map, to be played with <see cref="Scheme"/>.</summary>
	public bool Confirm()
	{
		if (Current != Screen.ControlSelect) return false;
		Go(Screen.Map);
		return true;
	}

	/// <summary>YOK-87: Continue: from the title straight to the saved run's map (no control select: the scheme is the saved setting).</summary>
	public bool ContinueRun()
	{
		if (Current != Screen.Title) return false;
		Go(Screen.Map);
		return true;
	}

	/// <summary>YOK-77: a node was picked on the map: its scene (the fight) runs.</summary>
	public bool EnterNode()
	{
		if (Current != Screen.Map) return false;
		Go(Screen.Fight);
		return true;
	}

	/// <summary>YOK-77: the node is done (or a new run was asked for from the fight): back to the map.</summary>
	public bool ReturnToMap()
	{
		if (Current != Screen.Fight) return false;
		Go(Screen.Map);
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

	/// <summary>Leave the fight or the map for the title (pause menu "Quit to title"). False anywhere else.</summary>
	public bool ReturnToTitle()
	{
		if (Current is not (Screen.Fight or Screen.Map)) return false;
		Go(Screen.Title);
		return true;
	}

	private void Go(Screen to)
	{
		Screen from = Current;
		Current = to;
		ShopOpen = false; // a shop never outlives its node scene
		Changed?.Invoke(from, to);
	}
}
