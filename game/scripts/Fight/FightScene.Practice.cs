using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-197, GDD 8: practice mode. "Practice mode reuses the dojo scene with a training dummy, so it needs no new
/// screen": set <see cref="PracticeMode"/> before the scene enters the tree and this same fight scene and sim loop
/// play Ryo against a <see cref="PracticeDummy"/>, outside any run.
/// <list type="bullet">
/// <item>The dummy is the dojo's: the fighter a dojo node resolves to in data/map/node-scenes.json (<see cref="PracticeNode"/>,
/// the same lookup that gives a dojo trial its dummy, R7), on that node's stage. P2's AI is off; the dummy holds the
/// chosen <see cref="PracticeStance"/> (<see cref="PracticeStanceKey"/> cycles it).</item>
/// <item>Ryo has a fresh run's kit: the specials he owns outside a run are his starters (A1). PROVISIONAL (YOK-197): the
/// open question "whether practice mode unlocks trials for every special or only owned ones" stays open; no trial is
/// offered here and no unlock is read (rules.md, open questions).</item>
/// <item>A KO or <see cref="PracticeResetKey"/> / <see cref="PracticeResetButton"/> (or Restart on the pause menu) puts both
/// fighters back at their start, at full health, on the next tick boundary.</item>
/// <item>The YOK-22 overlay (F1 boxes, F2 pause, F3 one tick, F4 scheme) is kept in every build here, and the pause
/// menu's move list carries the frame-data toggle.</item>
/// <item>Nothing is saved: the scene's run session is its own and never handed to the boot flow, so no save point sees
/// it (M5, AM3), and no replay is recorded (a reset rewinds the match).</item>
/// </list>
/// </summary>
public partial class FightScene
{
	public const Key PracticeStanceKey = Key.F5, PracticeResetKey = Key.Backspace;
	public const JoyButton PracticeResetButton = JoyButton.Back;
	/// <summary>The id of the dojo node practice resolves (not on any map).</summary>
	public const string PracticeNodeId = "practice";
	public const string PracticeNodeType = "dojo";

	/// <summary>Set before the scene enters the tree: practice mode instead of a run's node.</summary>
	public bool PracticeMode { get; set; }
	/// <summary>The training dummy (practice mode only; null otherwise).</summary>
	public PracticeDummy? Dummy { get; private set; }
	/// <summary>How many times the fighters were put back (a KO or the reset button).</summary>
	public int PracticeResets { get; private set; }
	private Label? _practiceLabel;

	public bool InPractice => PracticeMode && !InTrial;

	/// <summary>A dojo node as the node-scene table resolves it: the dummy's fighter, tier and stage (R7's dojo).</summary>
	public static NodeContext PracticeNode() =>
		NodeSceneTable.Default(ContentPaths.Data("map")).Resolve(new MapNodeData(PracticeNodeId, 0, PracticeNodeType)).Context;

	/// <summary>Called by <see cref="SetUpRun"/> in practice mode: a fresh run of the scene's own, the dojo node, the dummy and its match.</summary>
	private void SetUpPracticeRun()
	{
		Pool = LoadAbilityPool(); // no unlocks: practice never reads the save (M5, AM3)
		RunSeed = FixedRunSeed ?? 0; // nothing in practice draws from it: no AI, no draft
		OwnsSession = true;
		Session = NewSession(DemoMapFile, RunSeed, Pool, RunState.NewRun(Pool)); // never entered, never completed
		Node = PracticeNode();
		Opponent = Node.Fighter;
		TutorialAllowed = false;
		P2AiEnabled = false; // the dummy holds its stance, never an AI (no profile needed)
		Dummy = new PracticeDummy();
		Match m = NewMatch(DemoRun); // Ryo's run kit vs the dojo dummy; no session node, so no tier scaling
		if (PlayerScheme is { } scheme) m.P1.Input.Scheme = scheme;
		Match = m;
		GD.Print($"FightScene: practice vs {Opponent} (dojo dummy, node-scenes.json), Ryo's starters");
	}

	/// <summary>Called at the end of setup in <c>_Ready</c>: no replay, and the greybox line naming the controls.</summary>
	private void SetUpPractice()
	{
		if (!InPractice) return;
		Recorder = null; // a reset rewinds the match: no replay
		_practiceLabel = new Label { Name = "PracticePrompt", HorizontalAlignment = HorizontalAlignment.Center, Position = new Vector2(0, 140), Size = new Vector2(1920, 80) };
		_practiceLabel.AddThemeFontSizeOverride("font_size", 26);
		_practiceLabel.AddThemeColorOverride("font_color", new Color(0.96f, 0.93f, 0.85f));
		_practiceLabel.AddThemeColorOverride("font_outline_color", new Color(0.08f, 0.06f, 0.1f));
		_practiceLabel.AddThemeConstantOverride("outline_size", 8);
		var layer = new CanvasLayer { Name = "PracticeLayer", Layer = 4 };
		layer.AddChild(_practiceLabel);
		AddChild(layer);
		UpdatePracticeLabel();
	}

	/// <summary>After each sim step in practice: a KO puts the fighters back.</summary>
	private void CheckPractice()
	{
		if (Match.Phase == MatchPhase.Over) ResetPractice();
	}

	/// <summary>Both fighters back at their start at full health, the dummy's stance kept. False outside practice.</summary>
	public bool ResetPractice()
	{
		if (!InPractice) return false;
		Match.Reset();
		_camMatch = null; // the camera snaps to the reset positions
		Shake.Stop();
		_clock.Restart();
		PracticeResets++;
		UpdatePracticeLabel();
		if (IsInsideTree()) Render();
		return true;
	}

	/// <summary>The dummy's next stance. Null outside practice.</summary>
	public PracticeStance? CyclePracticeStance()
	{
		if (Dummy is null || !InPractice) return null;
		var s = Dummy.Next();
		UpdatePracticeLabel();
		return s;
	}

	/// <summary>Practice keys, public so tests can press them without an input event. Ignored while paused or outside practice.</summary>
	public bool HandlePracticeKey(Key key)
	{
		if (!InPractice || Paused) return false;
		if (key == PracticeStanceKey) return CyclePracticeStance() is not null;
		if (key == PracticeResetKey) return ResetPractice();
		return false;
	}

	/// <summary>From <c>_UnhandledInput</c>: the practice keys and the pad's Back button.</summary>
	private bool HandlePracticeInput(InputEvent e)
	{
		if (!InPractice) return false;
		if (e is InputEventKey { Pressed: true, Echo: false } k) return HandlePracticeKey(k.PhysicalKeycode);
		if (e is InputEventJoypadButton { Pressed: true, ButtonIndex: PracticeResetButton } && !Paused) return ResetPractice();
		return false;
	}

	/// <summary>The pause panel's position line in practice.</summary>
	private string PracticeLine(string foe) => $"Practice · vs {foe} dummy · {PracticeDummy.Name(Dummy!.Stance)}";

	private void UpdatePracticeLabel()
	{
		if (_practiceLabel is null || Dummy is null) return;
		_practiceLabel.Text = $"PRACTICE · Dummy: {PracticeDummy.Name(Dummy.Stance)}\n"
			+ "F5 dummy stance · Backspace / pad Back reset · F1 boxes · F2 pause · F3 step · Esc move list and frame data";
	}
}
