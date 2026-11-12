using System;
using Godot;
using YokaiFighters.Sim;
using YokaiFighters.Ui;
using YokaiFighters.SaveGame;
using YokaiFighters.Story;

namespace YokaiFighters.Fight;

/// <summary>
/// S3 story beats a hosted run shows around its fights, read from data/story/ and the save's story flags (M4): the intro
/// (<c>intro-soen</c>, first run only, before row 1), the boss reveal (<c>boss-reveal</c>, on entering the Tanuki's node) and the
/// win cards (<c>win-1</c>, <c>win-2</c>, <c>win-3</c>, the first one not yet seen, after beating the Tanuki). The scene only
/// shows the card and raises <see cref="StoryShown"/>; the host writes the flag. Which beat shows is decided here from the
/// flags the host hands in, never from game rules.
/// </summary>
public partial class FightScene
{
	public const string IntroId = "intro-soen", RevealId = "boss-reveal";
	public static readonly string[] WinIds = { "win-1", "win-2", "win-3" };

	/// <summary>Has the save seen this story flag (<see cref="StoryProgress"/>)? Null = nothing seen (a run with no save).</summary>
	public Func<string, bool>? StoryHas { get; set; }
	/// <summary>A story beat was put on screen: the flag to record (<see cref="StoryProgress.Intro"/>, <c>reveal</c>, <c>win-N</c>).</summary>
	public event Action<string>? StoryShown;
	/// <summary>The story card on screen before the fight (intro or reveal), or "" when none.</summary>
	public string StoryBeatId { get; private set; } = "";
	/// <summary>The win card that was chosen for the end screen, or "" before a win.</summary>
	public string WinCardId { get; private set; } = "";

	bool Seen(string flag) => StoryHas?.Invoke(flag) == true;

	/// <summary>The id of the card a run-start or boss-node entry shows (null = none): intro on the first run's row 1, the reveal on the Tanuki's node.</summary>
	public static string? BeatFor(NodeContext node, bool introSeen) =>
		RunSession.IsCopyLockNode(node) ? RevealId : node.Row == 1 && !introSeen ? IntroId : null;

	/// <summary>The win card: the first of win-1..3 not yet seen; once all three were seen, the last.</summary>
	public static string WinCardFor(Func<string, bool> seen)
	{
		foreach (string id in WinIds) if (!seen(id)) return id;
		return WinIds[^1];
	}

	void ShowStoryBeat()
	{
		if (StoryHas is null || TwistCard == null || OwnsSession || InPractice || Node is null || TwistCard.Showing) return;
		if (BeatFor(Node, Seen(StoryProgress.Intro)) is not { } id) return;
		try
		{
			var story = LoadStory();
			if (!story.Has(id)) return;
			StoryBeatId = id;
			TwistCard.Display(story[id].FillText(yokai: OpponentName));
			StoryShown?.Invoke(id == IntroId ? StoryProgress.Intro : StoryProgress.Reveal);
		}
		catch (Exception e) when (e is System.IO.IOException or FormatException or System.Text.Json.JsonException or System.Collections.Generic.KeyNotFoundException or InvalidOperationException)
		{
			GD.PushWarning($"story beat '{id}' skipped: {e.Message}");
		}
	}

	/// <summary>The end card of a won run: the win card (S3), else the demo-complete card; then the screen.</summary>
	void ShowRunWon()
	{
		if (OwnsSession) { CompleteScreen.Show(); return; }
		try
		{
			var story = LoadStory();
			string id = WinCardFor(Seen);
			if (story.Has(id))
			{
				var c = story[id];
				CompleteScreen.SetText(c.FillTitle(yokai: OpponentName), c.FillText(yokai: OpponentName));
				WinCardId = id;
				StoryShown?.Invoke(id);
			}
			else ApplyHostedDemoComplete(story);
		}
		catch (Exception e) when (e is System.IO.IOException or FormatException or System.Text.Json.JsonException or System.Collections.Generic.KeyNotFoundException or InvalidOperationException)
		{
			GD.PushWarning($"win card skipped: {e.Message}");
		}
		CompleteScreen.SetPortrait(OpponentPortrait);
		CompleteScreen.Show();
	}

	void ApplyHostedDemoComplete(StoryLibrary story)
	{
		var (title, text) = story.DemoComplete(OpponentName);
		CompleteScreen.SetText(title, text);
	}
}
