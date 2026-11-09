using System;
using YokaiFighters.Sim;
using YokaiFighters.Story;

namespace YokaiFighters.Ui;

/// <summary>One line of twist text as the UI shows it. <see cref="Placeholder"/> = the story data has no card for it: <see cref="Text"/> is empty and the caller shows nothing (no invented wording).</summary>
public readonly record struct TwistLine(string Text, bool Placeholder);

/// <summary>
/// YOK-139 (Y3): the one place the UI reads elder-twist wording. The text is story data (habit-writer, YOK-138), never code:
/// the intro card and the first-meeting callout are the story cards named by the twist's <c>intro_card</c> and
/// <c>callout_card</c> ids; the HUD reminder (and the map's detail line) is the story card for its <c>label_key</c>
/// (<c>twist.elder-oni-armour</c> is looked up as the card id <c>twist.elder-oni-armour</c> first, then as
/// <c>twist-elder-oni-armour</c>, because card ids are kebab-case), then the card <c>&lt;twist-id&gt;-label</c> (what habit-writer's YOK-138
/// ships: the schema has no label-text field, so that card names the key in its trigger). When the id is null or no such card exists the line
/// is <see cref="Missing"/> (empty, <see cref="TwistLine.Placeholder"/> set): the screens show nothing rather than a stand-in, and the
/// data-completeness test fails instead. Pure: no Godot.
/// </summary>
public static class TwistCopy
{
	/// <summary>No story card for this line: empty text, flagged. The UI shows nothing for it.</summary>
	public static readonly TwistLine Missing = new("", true);

	/// <summary>The HUD reminder / map detail line.</summary>
	public static TwistLine Label(ElderTwist t, StoryLibrary? story, string? yokaiName = null) =>
		Resolve(t, story, t.LabelKey, yokaiName) ?? Resolve(t, story, t.Id + "-label", yokaiName) ?? Missing;

	/// <summary>The intro card text (shown on entering the elder's node).</summary>
	public static TwistLine Intro(ElderTwist t, StoryLibrary? story, string? yokaiName = null) =>
		Resolve(t, story, t.IntroCard, yokaiName) ?? Missing;

	/// <summary>The first-meeting callout text.</summary>
	public static TwistLine Callout(ElderTwist t, StoryLibrary? story, string? yokaiName = null) =>
		Resolve(t, story, t.CalloutCard, yokaiName) ?? Missing;

	/// <summary>The intro card's speaker ("Nine-Tailed Kitsune", "Elder Oni"): the elder's name for the scroll title, or null when the card is missing or names nobody.</summary>
	public static string? IntroSpeaker(ElderTwist t, StoryLibrary? story)
	{
		if (story == null || string.IsNullOrWhiteSpace(t.IntroCard)) return null;
		foreach (string key in new[] { t.IntroCard, t.IntroCard.Replace('.', '-') })
			if (story.Has(key) && story[key].Speaker is { Length: > 0 } who && !who.Equals("Narrator", StringComparison.OrdinalIgnoreCase)) return who;
		return null;
	}

	static TwistLine? Resolve(ElderTwist t, StoryLibrary? story, string? id, string? yokaiName)
	{
		if (story == null || string.IsNullOrWhiteSpace(id)) return null;
		foreach (string key in new[] { id, id.Replace('.', '-') })
		{
			if (!story.Has(key)) continue;
			try { return new TwistLine(story[key].FillText(yokai: yokaiName ?? StoryLibrary.DisplayName(t.Elder)), false); }
			catch (InvalidOperationException) { return null; } // a card that needs a name we do not have: stand in rather than crash
		}
		return null;
	}

	/// <summary>The story library the screens read; loaded from data/story/ on first use, a test may set its own.</summary>
	public static StoryLibrary? Story
	{
		get => _story ??= Load();
		set => _story = value;
	}

	static StoryLibrary? _story;

	static StoryLibrary? Load()
	{
		try { return Fight.FightScene.LoadStory(); }
		catch (Exception e) when (e is System.IO.IOException or FormatException or System.Text.Json.JsonException) { return null; }
	}
}
