using System;
using YokaiFighters.Sim;
using YokaiFighters.Story;

namespace YokaiFighters.Ui;

/// <summary>One line of twist text as the UI shows it. <see cref="Placeholder"/> = the data had no text yet, this is a stand-in.</summary>
public readonly record struct TwistLine(string Text, bool Placeholder);

/// <summary>
/// YOK-139 (Y3): the one place the UI reads elder-twist wording. The text is story data (habit-writer, YOK-138), never code:
/// the intro card and the first-meeting callout are the story cards named by the twist's <c>intro_card</c> and
/// <c>callout_card</c> ids; the HUD reminder (and the map's detail line) is the story card for its <c>label_key</c>
/// (<c>twist.elder-oni-armour</c> is looked up as the card id <c>twist.elder-oni-armour</c> first, then as
/// <c>twist-elder-oni-armour</c>, because card ids are kebab-case), then the card <c>&lt;twist-id&gt;-label</c> (what habit-writer's YOK-138
/// ships: the schema has no label-text field, so that card names the key in its trigger). When the id is null or no such card exists the line
/// is a stand-in marked <see cref="PlaceholderMark"/>, so the copy drops in with no code change. Pure: no Godot.
/// </summary>
public static class TwistCopy
{
	public const string PlaceholderMark = "[PLACEHOLDER] ";

	/// <summary>Stand-in wording for each sim effect, shown only until the real text exists. Draft from the YOK-101 mockup, not approved copy.</summary>
	public static string StandIn(TwistEffect effect) => effect switch
	{
		TwistEffect.ReflectRepeatedSpecial => "Specials used twice in a row are reflected.",
		TwistEffect.SpecialArmour => "Its specials have armour.",
		TwistEffect.UnbreakableThrows => "Its throws can't be broken.",
		_ => "This elder fights with a twist.",
	};

	/// <summary>The HUD reminder / map detail line.</summary>
	public static TwistLine Label(ElderTwist t, StoryLibrary? story, string? yokaiName = null) =>
		Resolve(t, story, t.LabelKey, yokaiName) ?? Resolve(t, story, t.Id + "-label", yokaiName) ?? Stand(t);

	/// <summary>The intro card text (shown on entering the elder's node).</summary>
	public static TwistLine Intro(ElderTwist t, StoryLibrary? story, string? yokaiName = null) =>
		Resolve(t, story, t.IntroCard, yokaiName) ?? Stand(t);

	/// <summary>The first-meeting callout text.</summary>
	public static TwistLine Callout(ElderTwist t, StoryLibrary? story, string? yokaiName = null) =>
		Resolve(t, story, t.CalloutCard, yokaiName) ?? Stand(t);

	static TwistLine Stand(ElderTwist t) => new(PlaceholderMark + StandIn(t.Effect), true);

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
