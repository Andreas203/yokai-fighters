using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YokaiFighters.Story;

/// <summary>One kind <c>story_card</c> file (data/story/, schema story-card.schema.json). Text is habit-writer's, untouched.</summary>
public sealed record StoryCardData(string Id, string? Title, string Text, IReadOnlyList<string> Placeholders, string Speaker)
{
	static readonly Regex Slot = new(@"\{([^{}]*)\}");

	/// <summary>S4: fill {yokai}/{move} with the run's names. A placeholder the card uses but the caller didn't give throws.</summary>
	public string FillText(string? yokai = null, string? move = null) => Fill(Text, yokai, move);

	public string? FillTitle(string? yokai = null, string? move = null) => Title is null ? null : Fill(Title, yokai, move);

	string Fill(string s, string? yokai, string? move) => Slot.Replace(s, m => m.Groups[1].Value switch
	{
		"yokai" when yokai is not null => yokai,
		"move" when move is not null => move,
		var name => throw new InvalidOperationException($"story card '{Id}': no value for {{{name}}}"),
	});
}

/// <summary>
/// YOK-44: the story cards in <c>data/story/</c>, by id. Pure C# (System.Text.Json), no Godot. Implements the reward
/// screen's <see cref="Ui.IStorySource"/> (binding line, S3) and the lose screen's text.
/// </summary>
public sealed class StoryLibrary : Ui.IStorySource
{
	public const string BindingLineId = "binding-line", LoseScreenId = "lose-screen", DemoCompleteId = "demo-complete";

	readonly Dictionary<string, StoryCardData> _cards;

	public StoryLibrary(IEnumerable<StoryCardData> cards) => _cards = cards.ToDictionary(c => c.Id);

	public IReadOnlyCollection<StoryCardData> Cards => _cards.Values;

	public bool Has(string id) => _cards.ContainsKey(id);

	public StoryCardData this[string id] =>
		_cards.TryGetValue(id, out var c) ? c : throw new KeyNotFoundException($"no story card '{id}' in data/story/");

	/// <summary>S3: "Forgive me, {yokai}. I'll give it back." with the yokai's display name.</summary>
	public string BindingLine(string yokai) => this[BindingLineId].FillText(yokai: yokai);

	/// <summary>Lose screen title and text for the yokai that won.</summary>
	public (string? Title, string Text) LoseScreen(string yokai)
	{
		var c = this[LoseScreenId];
		return (c.FillTitle(yokai: yokai), c.FillText(yokai: yokai));
	}

	/// <summary>YOK-53 (for YOK-48): demo-complete card title and text, {yokai} = the yokai just bound.</summary>
	public (string? Title, string Text) DemoComplete(string yokai)
	{
		var c = this[DemoCompleteId];
		return (c.FillTitle(yokai: yokai), c.FillText(yokai: yokai));
	}

	/// <summary>Yokai id to the name the cards show ("kitsune" -> "Kitsune").</summary>
	public static string DisplayName(string id) =>
		string.Join(" ", id.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

	public static StoryCardData Parse(string json)
	{
		using var doc = JsonDocument.Parse(json);
		var r = doc.RootElement;
		string Str(string name) => r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s
			? s : throw new FormatException($"story card: missing '{name}'");
		if (Str("kind") != "story_card") throw new FormatException("story card: kind must be 'story_card'");
		string id = Str("id");
		string? title = r.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
		var ph = r.TryGetProperty("placeholders", out var p) && p.ValueKind == JsonValueKind.Array
			? p.EnumerateArray().Select(e => e.GetString() ?? "").ToArray() : Array.Empty<string>();
		foreach (var name in ph)
			if (name is not ("yokai" or "move")) throw new FormatException($"story card '{id}': unknown placeholder '{name}'");
		return new StoryCardData(id, title, Str("text"), ph, Str("speaker"));
	}

	public static StoryCardData LoadFile(string path) => Parse(File.ReadAllText(path));

	/// <summary>Every *.json in the folder (sorted); an absent folder gives an empty library.</summary>
	public static StoryLibrary LoadDirectory(string dir) =>
		new(Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal).Select(LoadFile) : Array.Empty<StoryCardData>());
}
