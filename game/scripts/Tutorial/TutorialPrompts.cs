using YokaiFighters.Story;

namespace YokaiFighters.Tutorial;

/// <summary>
/// Prompt text for the guided apprentice fight (Y6), read by id from the story cards in data/story/ (habit-writer's
/// <c>tutorial-*</c> cards); nothing is hard-coded. A card missing from the library gives an empty string, never a tag.
/// </summary>
public static class TutorialPrompts
{
	public const string SkipId = "tutorial-skip";

	/// <summary>The cards to read; the scene sets it (<c>FightScene.LoadStory</c>) before the first prompt shows.</summary>
	public static StoryLibrary? Library { get; set; }

	/// <summary>The live control names for {throw_keys} and {burst_keys} (the scene fills it from the bindings); a token without one reads as plain words.</summary>
	public static System.Collections.Generic.IReadOnlyDictionary<string, string> Keys { get; set; } = new System.Collections.Generic.Dictionary<string, string>();

	/// <summary>The key the skip hint names in place of the card's own "Tab" (a pad's Back button); null = the card as written.</summary>
	public static string? SkipKey { get; set; }

	/// <summary>The skip hint under every prompt.</summary>
	public static string SkipHint
	{
		get
		{
			string t = Text(SkipId);
			return SkipKey is { Length: > 0 } k && t.StartsWith("Tab", System.StringComparison.Ordinal) ? k + t[3..] : t;
		}
	}

	/// <summary>Story card id of a lesson's prompt.</summary>
	public static string IdOf(TutorialLesson lesson) => lesson switch
	{
		TutorialLesson.Block => "tutorial-block", // C3
		TutorialLesson.Throw => "tutorial-throw", // C4
		TutorialLesson.ThrowBreak => "tutorial-throw-break", // C4 break window
		TutorialLesson.Meter => "tutorial-meter", // C5
		TutorialLesson.Burst => "tutorial-burst", // C6
		TutorialLesson.Cancel => "tutorial-cancel", // X1-X4
		_ => "",
	};

	public static string For(TutorialLesson lesson) => Text(IdOf(lesson));

	static string Text(string id) => Library is { } l && l.Has(id) ? l[id].FillText(WithFallbacks(l[id])) : "";

	// The card's own fallback wording when the scene gave no binding names.
	static System.Collections.Generic.IReadOnlyDictionary<string, string> WithFallbacks(StoryCardData c)
	{
		var d = new System.Collections.Generic.Dictionary<string, string>
		{
			["throw_keys"] = "both throw buttons",
			["burst_keys"] = "all three punches",
		};
		foreach (var kv in Keys) d[kv.Key] = kv.Value;
		return d;
	}
}
