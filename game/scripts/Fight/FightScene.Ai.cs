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
	/// <summary>
	/// YOK-99, Y5: Tanuki wins before this run (the host sets it from the save before the scene enters the tree;
	/// 0 on its own). Every AI opponent but the apprentice reacts one frame faster per win and leans aggressive
	/// (<see cref="TanukiPressure"/>, <see cref="PressureFor"/>).
	/// </summary>
	public int TanukiWins { get; set; }
	/// <summary>The pressure this fight's AI gets (<see cref="PressureFor"/>).</summary>
	public TanukiPressure Pressure => PressureFor(Node, TanukiWins);

	// ApprenticeTier (Y4 tier name of the row-1 apprentice, data/map/node-scenes.json) is defined in FightScene.Tutorial.cs (YOK-92).

	/// <summary>
	/// YOK-99, Y5 with Y4/Y6: the Tanuki pressure for a node. The apprentice (Y4 tier "apprentice", row 1) is
	/// exempt: Y4 fixes it at 30f and Y6 makes it the guided tutorial fight. Every other node, elders and the
	/// Tanuki included, gets <paramref name="tanukiWins"/> in full (that reading is flagged to the designer).
	/// </summary>
	public static TanukiPressure PressureFor(NodeContext? node, int tanukiWins) =>
		node?.Tier == ApprenticeTier ? TanukiPressure.None : new TanukiPressure(tanukiWins);
	public BehaviourProfile[] Profiles { get; private set; } = Array.Empty<BehaviourProfile>();
	private int _profileIndex;
	private bool _swapHeld, _toggleHeld;

	/// <summary>A yokai's profiles from data/profiles/ (default Kitsune), else the Kitsune test fixtures. Aggressive first.</summary>
	public static BehaviourProfile[] LoadProfiles(string yokai = "kitsune")
	{
		var all = BehaviourProfile.LoadDirectory(ContentPaths.Data("profiles"));
		var own = Array.FindAll(all, p => p.Yokai == yokai);
		if (own.Length == 0)
		{
			GD.Print($"FightScene: data/profiles/ has no {yokai} profile, using TEST FIXTURE profiles");
			own = BehaviourProfile.LoadDirectory(ProjectSettings.GlobalizePath(FixtureProfilesDir));
		}
		Array.Sort(own, (a, b) => string.CompareOrdinal(a.Temperament, b.Temperament));
		return own;
	}

	private void SetUpAi()
	{
		LoadNodeProfiles();
		RebuildAi();
	}

	/// <summary>YOK-77: the node's fighter's profiles, starting on the node's profile when it names one.</summary>
	private void LoadNodeProfiles()
	{
		Profiles = LoadProfiles(Node is { } n && n.Fighter == Opponent ? n.MovesAndProfilesOf : Opponent); // YOK-174: the yokai's own profiles
		if (Node is null || Profiles.Length == 0) return;
		if (Node.Profile is null && DebugTemperament is { } t && Array.FindIndex(Profiles, p => p.Temperament == t) is var d and >= 0)
		{
			_profileIndex = d; // E23: the temperament chosen in the debug menu holds over the roll
			return;
		}
		// YOK-174, Y1/Y2: the node's named profile, else its seeded 50/50 temperament (same seed, same temperament; M5).
		int i = NodeTemperament.Pick(Profiles, Node, Session?.Seed ?? 0);
		if (i >= 0) _profileIndex = i;
		else GD.PushWarning($"FightScene: node '{Node.NodeId}' names profile '{Node.Profile}', which {Opponent} does not have; using {Profiles[_profileIndex % Profiles.Length].Id}");
	}

	/// <summary>YOK-174: why the AI could not be built (a profile names a move P2's kit lacks); null when it was. The fight does not run then.</summary>
	public string? AiSetupError { get; private set; }

	/// <summary>E23 debug: the temperament last chosen in the debug menu (F6/F9, --temperament=); null = each node rolls its own (YOK-174).</summary>
	public string? DebugTemperament { get; private set; }

	/// <summary>New AI over the current match (fight start, reset, temperament swap): empty delay buffer.</summary>
	public void RebuildAi()
	{
		if (P2AiEnabled && Profiles.Length > 0)
		{
			var profile = Profiles[_profileIndex % Profiles.Length];
			var pressure = Pressure;
			int reaction = pressure.ReactionFrames(Node?.ReactionFrames ?? profile.ReactionFrames); // Y4: the node's row, Y5: minus Tanuki wins
			var loadout = AiLoadout.From(Match.P2);
			// YOK-174: on the real content a named move the kit lacks is an error, not a silent skip. TEST FIXTURE kits
			// (Sources.FixturesOnly, MovesDir empty) never match the real profiles' move ids, so they are not checked.
			if (!string.IsNullOrEmpty(Sources.MovesDir) && loadout.Missing(profile) is { Count: > 0 } missing)
			{
				// A throw here would be swallowed by Godot's callback and leave the scene half set up: refuse the fight
				// instead. P2 has no AI, the clock stops, and the boot flow sends the player back to the title.
				AiSetupError = $"FightScene: profile '{profile.Id}' names {string.Join(", ", missing)}, which P2's kit does not have";
				GD.PushError(AiSetupError);
				P2Ai = null;
				SetProcess(false);
				Recorder?.OnAi(null, AiSeed);
				return;
			}
			AiSetupError = null;
			P2Ai = new ProfileAi(profile, 1, loadout, AiSeed, reaction, TierOf(Node), pressure.AggressionBonus);
		}
		else P2Ai = null;
		Recorder?.OnAi(P2Ai, AiSeed); // YOK-49
	}

	/// <summary>
	/// YOK-57, E22: the fight the AI is in; see <see cref="AiFightTiers.Of"/> (shared with the YOK-26 harness).
	/// </summary>
	public static AiFightTier TierOf(NodeContext? node) => AiFightTiers.Of(node);

	public void SwapTemperament()
	{
		if (Profiles.Length == 0) return;
		_profileIndex = (_profileIndex + 1) % Profiles.Length;
		DebugTemperament = Profiles[_profileIndex].Temperament;
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
	private FighterInput P2Input()
	{
		if (P2Ai is not { } ai) return InputDevices.Read(1);
		AiView view = AiView.Capture(Match);
		return CoachedP2(view, ai.Next(view)); // YOK-92: the tutorial coach overrides while a lesson needs the apprentice
	}

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
