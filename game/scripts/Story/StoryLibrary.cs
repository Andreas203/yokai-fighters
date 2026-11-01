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
	/// <summary>A {name} slot in title or text.</summary>
	internal static readonly Regex Slot = new(@"\{([^{}]*)\}");

	/// <summary>YOK-212, S3: the card's <c>later_loss</c> (a later-loss wake-up card), or null for every other card.</summary>
	public LaterLoss? LaterLoss { get; init; }

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
/// YOK-212, S3 "later losses: variants by row reached and winning yokai": a card's <c>later_loss</c> field. The card
/// shows for a loss on a map row from <see cref="RowMin"/> to <see cref="RowMax"/> (both included) to <see cref="Yokai"/>
/// (an elder counts as its yokai); a null yokai = any yokai, the row band's card.
/// </summary>
public sealed record LaterLoss(string? Yokai, int RowMin, int RowMax)
{
	/// <summary>
	/// The yokai a loss can be to: every <c>NodeContext.DraftYokai</c> the shipped maps' fight nodes resolve to (an elder,
	/// the apprentice and the Nine-Tails as their yokai). Same list as the schema's <c>later_loss.yokai</c> enum; both are
	/// checked against the maps by <c>LoseCardTests</c>.
	/// </summary>
	public static readonly IReadOnlyList<string> Winners = new[] { "kitsune", "oni", "kappa", "tanuki" };

	public bool Covers(int row) => row >= RowMin && row <= RowMax;
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

	/// <summary>S3: "Forgive me, {yokai}. I'll set your spirit free." with the yokai's display name.</summary>
	public string BindingLine(string yokai) => this[BindingLineId].FillText(yokai: yokai);

	/// <summary>Lose screen title and text for the yokai that won, off the map (no row): the <c>lose-screen</c> card.</summary>
	public (string? Title, string Text) LoseScreen(string yokai) => LoseScreen(yokai, null, null);

	/// <summary>YOK-212: the <see cref="LaterLossCard"/> for <paramref name="winner"/> on <paramref name="row"/>, {yokai} = <paramref name="name"/> (the name shown, e.g. "Elder Oni").</summary>
	public (string? Title, string Text) LoseScreen(string name, string? winner, int? row)
	{
		var c = LaterLossCard(winner, row);
		return (c.FillTitle(yokai: name), c.FillText(yokai: name));
	}

	/// <summary>
	/// YOK-212, S3: the wake-up card for a loss to <paramref name="winner"/> (a yokai id; an elder already as its yokai,
	/// <c>NodeContext.DraftYokai</c>) on map row <paramref name="row"/>, read from each card's <c>later_loss</c>. First a
	/// card naming that yokai whose rows cover the row; else a card naming no yokai (the row band) that covers it; else
	/// <c>lose-screen</c> (also with no row: a fight off the map). Tie-break when several cards match in one step: the
	/// narrower row range, then the lower id (ordinal). Pure: no RNG, the same cards and loss always give the same card.
	/// </summary>
	public StoryCardData LaterLossCard(string? winner, int? row)
	{
		if (row is int r)
		{
			var covering = _cards.Values.Where(c => c.LaterLoss?.Covers(r) == true).ToList();
			var pick = (winner is null ? null : Narrowest(covering.Where(c => c.LaterLoss!.Yokai == winner)))
				?? Narrowest(covering.Where(c => c.LaterLoss!.Yokai is null));
			if (pick is not null) return pick;
		}
		return this[LoseScreenId];
	}

	static StoryCardData? Narrowest(IEnumerable<StoryCardData> cards) => cards
		.OrderBy(c => c.LaterLoss!.RowMax - c.LaterLoss.RowMin).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();

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
		string text = Str("text");
		var later = ParseLaterLoss(r, id);
		// YOK-212: the lose screen fills only {yokai}; any other name would load here and then throw at the loss.
		if (later is not null)
		{
			var used = new[] { title ?? "", text }.SelectMany(x => StoryCardData.Slot.Matches(x).Select(m => m.Groups[1].Value));
			if (ph.Concat(used).FirstOrDefault(n => n != "yokai") is { } other)
				throw new FormatException($"story card '{id}': a later-loss card fills only {{yokai}}, never {{{other}}}");
		}
		return new StoryCardData(id, title, text, ph, Str("speaker")) { LaterLoss = later };
	}

	/// <summary>YOK-212: <c>later_loss</c> = { yokai? (one of <see cref="LaterLoss.Winners"/>), row_min, row_max } (whole Int32 rows from 1, written
	/// without a decimal point or exponent; min no higher than max); absent = null. tools/validate_data.py refuses the same files.</summary>
	static LaterLoss? ParseLaterLoss(JsonElement card, string id)
	{
		if (!card.TryGetProperty("later_loss", out var t)) return null;
		if (t.ValueKind != JsonValueKind.Object) throw new FormatException($"story card '{id}': later_loss must be an object");
		int Row(string name) => t.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) && n >= 1
			? n : throw new FormatException($"story card '{id}': later_loss.{name} must be a whole row number from 1 to 2147483647, with no decimal point or exponent");
		string? yokai = !t.TryGetProperty("yokai", out var y) ? null
			: y.ValueKind == JsonValueKind.String && y.GetString() is { } s && LaterLoss.Winners.Contains(s) ? s
			: throw new FormatException($"story card '{id}': later_loss.yokai {y.GetRawText()}: a later_loss yokai is the plain yokai id; an elder counts as its yokai (one of {string.Join(", ", LaterLoss.Winners)})");
		int min = Row("row_min"), max = Row("row_max");
		if (min > max) throw new FormatException($"story card '{id}': later_loss.row_min {min} is above row_max {max}");
		return new LaterLoss(yokai, min, max);
	}

	public static StoryCardData LoadFile(string path) => Parse(File.ReadAllText(path));

	/// <summary>Every *.json in the folder (sorted); an absent folder gives an empty library.</summary>
	public static StoryLibrary LoadDirectory(string dir) =>
		new(Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal).Select(LoadFile) : Array.Empty<StoryCardData>());
}
