namespace YokaiFighters.Tutorial;

/// <summary>
/// PLACEHOLDER prompt text for the guided apprentice fight (Y6). The final copy is story-card content owned by
/// habit-writer and must pass rules-lawyer; these strings only name the action so the fight is playable. Each one
/// starts with <see cref="Marker"/> so a screenshot or a test can tell placeholder copy from final copy.
/// </summary>
public static class TutorialPrompts
{
	public const string Marker = "[PLACEHOLDER] ";
	/// <summary>PLACEHOLDER: the skip hint under every prompt.</summary>
	public const string SkipHint = Marker + "Tab: skip this lesson";

	public static string For(TutorialLesson lesson) => lesson switch
	{
		TutorialLesson.Block => Marker + "Hold back to block the apprentice's attacks.", // C3
		TutorialLesson.Throw => Marker + "Get close and press both throw buttons to throw.", // C4
		TutorialLesson.ThrowBreak => Marker + "The apprentice will grab you: press throw as it does to break free.", // C4 break window
		TutorialLesson.Meter => Marker + "Land and block hits to fill one bar of meter.", // C5
		TutorialLesson.Burst => Marker + "Caught in a combo? Press three punches to burst free.", // C6
		TutorialLesson.Cancel => Marker + "Cancel a move into another before it ends.", // X1-X4
		_ => Marker,
	};
}
