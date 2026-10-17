using System;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Sim;
using YokaiFighters.Tutorial;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-92, Y6: the guided apprentice fight. Runs only when the host allows it (<see cref="TutorialAllowed"/>,
/// GameFlow: the save has no <c>apprentice-tutorial</c> story flag, i.e. the first run) and the node's tier is
/// "apprentice" (row 1, R1). The sim side is <see cref="ApprenticeTutorial"/> (lessons) and
/// <see cref="TutorialCoach"/> (the apprentice's half of each lesson, which overrides the profile AI while a
/// lesson is on screen). Tab / pad Back skips the lesson on screen. The prompt is a PLACEHOLDER label on the HUD
/// layer (ui-designer styles it; habit-writer writes the copy).
/// </summary>
public partial class FightScene
{
	public const string ApprenticeTier = "apprentice";
	public const Key SkipLessonKey = Key.Tab;
	public const JoyButton SkipLessonButton = JoyButton.Back;

	/// <summary>Set by the host before the scene enters the tree. False (the default) = never guided.</summary>
	public bool TutorialAllowed { get; set; }
	public ApprenticeTutorial? Tutorial { get; private set; }
	public TutorialCoach? Coach { get; private set; }
	/// <summary>Tunables for the guided fight (all PROVISIONAL, see <see cref="TutorialOptions"/>).</summary>
	public TutorialOptions TutorialOptions { get; set; } = new();
	/// <summary>Raised once when this fight ran a tutorial and it finished (every lesson done, skipped or unavailable).</summary>
	public event Action<ApprenticeTutorial>? TutorialFinished;
	/// <summary>The on-screen prompt (empty when no lesson is shown).</summary>
	public string TutorialPromptText => _tutorialLabel?.Visible == true ? _tutorialLabel.Text : "";

	private Label? _tutorialLabel;
	private bool _skipHeld;

	/// <summary>True when this node is a guided fight: allowed by the host and an apprentice-tier node.</summary>
	public static bool IsGuided(bool allowed, NodeContext? node) => allowed && node?.Tier == ApprenticeTier;

	private void SetUpTutorial()
	{
		Tutorial?.Detach();
		Tutorial = null;
		Coach = null;
		if (!IsGuided(TutorialAllowed, Node)) { RenderTutorial(); return; }
		Tutorial = new ApprenticeTutorial(Match, TutorialOptions, 0);
		Tutorial.Completed += t => TutorialFinished?.Invoke(t);
		Coach = new TutorialCoach(Match.P2, 1, Match.Config, TutorialOptions, Node?.ReactionFrames ?? 30); // Y4: apprentice 30f
		GD.Print($"FightScene: guided apprentice fight (Y6), {ApprenticeTutorial.Order.Count} lessons");
		RenderTutorial();
	}

	/// <summary>P2's input: the coach while a lesson needs the apprentice, else the profile AI (fed every tick either way).</summary>
	private FighterInput CoachedP2(AiView view, FighterInput fromAi) =>
		Coach is { } c && Tutorial is { } t ? c.Next(view, t.Current) ?? fromAi : fromAi;

	/// <summary>After each sim tick: the lessons watch the match.</summary>
	private void ObserveTutorial()
	{
		if (Tutorial is null) return;
		Tutorial.Observe();
		if (Match.Phase == MatchPhase.Over) Tutorial.Detach(); // a KO ends the lessons; any left stay Pending
	}

	/// <summary>Skips the lesson on screen (also what the skip key does).</summary>
	public bool SkipTutorialLesson()
	{
		bool skipped = Tutorial?.SkipLesson() ?? false;
		RenderTutorial();
		return skipped;
	}

	private void PollTutorialKeys()
	{
		if (Tutorial is null) return;
		bool down = Input.IsPhysicalKeyPressed(SkipLessonKey) || IsAnyPadPressed(SkipLessonButton);
		if (down && !_skipHeld) SkipTutorialLesson();
		_skipHeld = down;
	}

	private static bool IsAnyPadPressed(JoyButton b)
	{
		foreach (int pad in Input.GetConnectedJoypads()) if (Input.IsJoyButtonPressed(pad, b)) return true;
		return false;
	}

	private void RenderTutorial()
	{
		string text = Tutorial is { Finished: false } t && Match.Phase != MatchPhase.Over ? t.Prompt : "";
		if (_tutorialLabel is null)
		{
			if (text.Length == 0 || _hud is null) return;
			_tutorialLabel = new Label
			{
				Name = "TutorialPrompt",
				HorizontalAlignment = HorizontalAlignment.Center,
				AnchorLeft = 0.2f, AnchorRight = 0.8f, AnchorTop = 0.18f, AnchorBottom = 0.18f,
			};
			_tutorialLabel.AddThemeColorOverride("font_color", FightHud.Ink);
			_tutorialLabel.AddThemeFontSizeOverride("font_size", 28);
			_hud.AddChild(_tutorialLabel);
		}
		_tutorialLabel.Text = text.Length == 0 ? "" : text + "\n" + TutorialPrompts.SkipHint;
		_tutorialLabel.Visible = text.Length > 0;
	}
}
