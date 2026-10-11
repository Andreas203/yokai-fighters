using System;
using System.Collections.Generic;
using Godot;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-pause-flow-wiring: the in-fight pause. Esc / pad Start opens <c>scenes/ui/pause_menu.tscn</c> (the
/// <c>PauseLayer/Pause</c> instance in fight.tscn) and the scene stops stepping the sim: no tick runs, nothing is
/// recorded, and since animation is seeked from sim state (F3) the picture holds still. The scene tree is not paused;
/// the scene gates its own stepping (as the F9 debug menu does), so nothing global is left behind when the fight is freed.
/// <para>
/// Available only while a duel is being fought (<see cref="DemoStage.Fighting"/> and the match not over): not over the
/// binding card, reward, lose or demo-complete screens, and not while the F9 debug menu is up. Off under
/// <see cref="ExternalDrive"/> (tests, harness) unless <see cref="PauseEnabled"/> is set.
/// </para>
/// <para>
/// Resume: the clock restarts (no catch-up burst), and any attack button or Special that is down but was not held
/// continuously from the last sim tick through the whole pause is ignored until released, so the key that pressed
/// Resume (Space = Special, pad A = an attack) cannot fire. Buttons held right through, and directions, carry on as they were.
/// </para>
/// </summary>
public partial class FightScene
{
	/// <summary>Buttons that can be masked on resume. Directions are levels, not presses: a held guard stays held.</summary>
	public const InputBits PauseMaskable = InputBits.Attacks | InputBits.Special;

	/// <summary>null = pause is available unless <see cref="ExternalDrive"/>; a test sets true to ask for it (or false to switch it off).</summary>
	public bool? PauseEnabled { get; set; }
	/// <summary>The pause menu (null if fight.tscn has none).</summary>
	public PauseMenu? PauseMenu { get; private set; }
	public bool Paused => PauseMenu?.IsOpen == true;
	/// <summary>Test seam: replaces the live P1 device read (keyboard + pad 0).</summary>
	public Func<InputBits>? HeldBitsSource { get; set; }

	private InputBits _lastP1; // what the sim saw from P1 on its last tick
	private InputBits _heldThroughPause; // of those, the bits down on every frame since the pause opened

	public bool CanPause => PauseMenu != null && (PauseEnabled ?? !ExternalDrive) && !Paused && IsInsideTree() && !IsQueuedForDeletion()
		&& Stage == DemoStage.Fighting && Match.Phase != MatchPhase.Over && DebugMenu?.Visible != true;

	private void SetUpPause()
	{
		PauseMenu = GetNodeOrNull<PauseMenu>("PauseLayer/Pause");
		if (PauseMenu == null) return;
		PauseMenu.FreezeTree = false; // the scene gates its own stepping
		PauseMenu.ResumeRequested += EndPause;
		PauseMenu.RestartRequested += RestartRun; // masks everything held, like Restart on the end screens
		PauseMenu.QuitToTitleRequested += () =>
		{
			if (RequestTitle()) return;
			GD.Print("FightScene: Quit to title with no boot flow listening (scene opened on its own); resuming");
			EndPause();
		};
	}

	/// <summary>Opens the pause menu on the live run summary. False (and nothing happens) when pause is not available.</summary>
	public bool TryPause()
	{
		if (!CanPause) return false;
		_heldThroughPause = _lastP1;
		PauseMenu!.SetRunSummary(BuildPauseSummary());
		try { PauseMenu.MoveList.Bind(MoveListSource.Build(DemoRun, ContentPaths.Data("moves"), Match.Config)); }
		catch (Exception e) when (e is System.IO.IOException or FormatException or System.Text.Json.JsonException or KeyNotFoundException)
		{
			GD.PushWarning($"pause: move list not refreshed: {e.Message}");
		}
		PauseMenu.Open();
		return true;
	}

	/// <summary>
	/// What the pause panel shows, from live state: both fighters' health in this duel (the match, not the run's carried
	/// health), the position in the run, and Ryo's slots A-D with level and attached modifier (the run).
	/// </summary>
	public PauseRunSummary BuildPauseSummary()
	{
		string foe = Story.StoryLibrary.DisplayName(Opponent);
		var slots = new List<PauseSlotSummary>();
		foreach (var slot in new[] { SpecialSlot.A, SpecialSlot.B, SpecialSlot.C, SpecialSlot.D })
			slots.Add(DemoRun[slot] is { } owned
				? new(slot.ToString(), owned.Data.Name, owned.Level, owned.Modifier?.Name ?? "", owned.Data.Id)
				: new(slot.ToString(), "Empty", 0, ""));
		return new PauseRunSummary(
			Health(Story.StoryLibrary.DisplayName(Kit.Ryo).ToUpperInvariant(), Match.P1), Health(foe, Match.P2),
			$"Duel {Duel} of {DemoDuels} · vs {foe}", slots);

		static PauseFighterSummary Health(string name, Fighter f) => new(name, Math.Max(0, f.Health), f.MaxHealth);
	}

	/// <summary>A rendered frame while paused: no sim tick; only tracks which buttons are still held from before the pause.</summary>
	private void PausedFrame()
	{
		_heldThroughPause &= HeldBits();
		_clock.Restart();
		Render();
	}

	private void EndPause()
	{
		InputBits raw = HeldBits();
		_heldThroughPause &= raw;
		_suppress |= ResumeMask(_heldThroughPause, raw);
		_clock.Restart(); // continue from the same tick, no catch-up burst
		Render();
	}

	/// <summary>Buttons to ignore until released after a pause: down now, but not held the whole way through it.</summary>
	public static InputBits ResumeMask(InputBits heldThroughPause, InputBits heldNow) => heldNow & PauseMaskable & ~heldThroughPause;

	public override void _UnhandledInput(InputEvent e)
	{
		// The open menu (a child, so it is asked first) takes Esc / Start to resume; this only ever opens it.
		if (MenuInput.Pause(e) && TryPause()) GetViewport().SetInputAsHandled();
	}
}
