using System;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-27 hook: P2 is driven by <see cref="ProfileAi"/> by default (demo), from kitsune profiles in
/// data/profiles/ or, until habit-writer's land (YOK-44), the TEST FIXTURE profiles in
/// tests/fixtures/profiles/. Debug builds: F6 swaps temperament (aggressive/patient), F7 toggles P2 AI/keyboard.
/// </summary>
public partial class FightScene
{
	public const string FixtureProfilesDir = "res://tests/fixtures/profiles";
	public const Key SwapTemperamentKey = Key.F6, ToggleAiKey = Key.F7;

	/// <summary>Seed for the AI's rolls; the same seed and inputs replay the same fight.</summary>
	public uint AiSeed { get; set; } = 27;
	public bool P2AiEnabled { get; private set; } = true;
	public ProfileAi? P2Ai { get; private set; }
	public BehaviourProfile[] Profiles { get; private set; } = Array.Empty<BehaviourProfile>();
	private int _profileIndex;
	private bool _swapHeld, _toggleHeld;

	/// <summary>Kitsune profiles from data/profiles/, else the test fixtures. Aggressive first.</summary>
	public static BehaviourProfile[] LoadProfiles()
	{
		var all = BehaviourProfile.LoadDirectory(ContentPaths.Data("profiles"));
		var kitsune = Array.FindAll(all, p => p.Yokai == "kitsune");
		if (kitsune.Length == 0)
		{
			GD.Print("FightScene: data/profiles/ has no kitsune profile, using TEST FIXTURE profiles");
			kitsune = BehaviourProfile.LoadDirectory(ProjectSettings.GlobalizePath(FixtureProfilesDir));
		}
		Array.Sort(kitsune, (a, b) => string.CompareOrdinal(a.Temperament, b.Temperament));
		return kitsune;
	}

	private void SetUpAi()
	{
		Profiles = LoadProfiles();
		RebuildAi();
	}

	/// <summary>New AI over the current match (fight start, reset, temperament swap): empty delay buffer.</summary>
	public void RebuildAi()
	{
		P2Ai = P2AiEnabled && Profiles.Length > 0
			? new ProfileAi(Profiles[_profileIndex % Profiles.Length], 1, AiLoadout.From(Match.P2), AiSeed)
			: null;
		Recorder?.OnAi(P2Ai, AiSeed); // YOK-49
	}

	public void SwapTemperament()
	{
		if (Profiles.Length == 0) return;
		_profileIndex = (_profileIndex + 1) % Profiles.Length;
		RebuildAi();
		GD.Print($"FightScene: P2 AI profile {Profiles[_profileIndex].Id} ({Profiles[_profileIndex].Temperament})");
	}

	public void ToggleP2Ai()
	{
		P2AiEnabled = !P2AiEnabled;
		RebuildAi();
		GD.Print($"FightScene: P2 {(P2AiEnabled ? "AI" : "keyboard/pad")}");
	}

	/// <summary>P2's input for the next tick: the AI from the screen as it is now (it delays itself), else devices.</summary>
	private FighterInput P2Input() => P2Ai is { } ai ? ai.Next(AiView.Capture(Match)) : InputDevices.Read(1);

	/// <summary>ExternalDrive: one tick with P1's input and P2 from the AI (or devices when it is off).</summary>
	public void StepWithAi(FighterInput p1) => Step(p1, P2Input());

	private void PollAiKeys()
	{
		if (!OS.IsDebugBuild()) return;
		bool swap = Input.IsPhysicalKeyPressed(SwapTemperamentKey), toggle = Input.IsPhysicalKeyPressed(ToggleAiKey);
		if (swap && !_swapHeld) SwapTemperament();
		if (toggle && !_toggleHeld) ToggleP2Ai();
		_swapHeld = swap;
		_toggleHeld = toggle;
	}
}
