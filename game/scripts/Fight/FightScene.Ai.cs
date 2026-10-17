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

	/// <summary>Y4 tier name of the row-1 apprentice (data/map/node-scenes.json).</summary>
	public const string ApprenticeTier = "apprentice";

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
		Profiles = LoadProfiles(Opponent);
		if (Node?.Profile is not { } id) return;
		int i = Array.FindIndex(Profiles, p => p.Id == id);
		if (i >= 0) _profileIndex = i;
		else GD.PushWarning($"FightScene: node '{Node.NodeId}' names profile '{id}', which {Opponent} does not have; using {Profiles[_profileIndex % Profiles.Length].Id}");
	}

	/// <summary>New AI over the current match (fight start, reset, temperament swap): empty delay buffer.</summary>
	public void RebuildAi()
	{
		if (P2AiEnabled && Profiles.Length > 0)
		{
			var profile = Profiles[_profileIndex % Profiles.Length];
			var pressure = Pressure;
			int reaction = pressure.ReactionFrames(Node?.ReactionFrames ?? profile.ReactionFrames); // Y4: the node's row, Y5: minus Tanuki wins
			P2Ai = new ProfileAi(profile, 1, AiLoadout.From(Match.P2), AiSeed, reaction, TierOf(Node), pressure.AggressionBonus);
		}
		else P2Ai = null;
		Recorder?.OnAi(P2Ai, AiSeed); // YOK-49
	}

	/// <summary>
	/// YOK-57, E22: the fight the AI is in. The boss node (the copy-lock node, <see cref="RunSession.IsCopyLockNode"/>)
	/// or a "tanuki" tier = Tanuki; an elder node or "elder" tier = Elder; anything else, or no node, = Standard
	/// (no EX, no burst whatever the profile says).
	/// </summary>
	public static AiFightTier TierOf(NodeContext? node) =>
		node is null ? AiFightTier.Standard
		: RunSession.IsCopyLockNode(node) || node.Tier == "tanuki" ? AiFightTier.Tanuki
		: node.Elder || node.Tier == "elder" ? AiFightTier.Elder
		: AiFightTier.Standard;

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
