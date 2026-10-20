using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YokaiFighters.Story;

/// <summary>
/// YOK-159 (S1, S2, R6, A12): the merchant's speech-bubble lines for the shop, from
/// <c>data/story/shop/merchant-shop-lines.json</c> (kind <c>merchant_shop_lines</c>, schema
/// <c>merchant-shop-lines.schema.json</c>). Pure C#, no Godot, no RNG: the caller passes a pick number, so a screen can
/// stay deterministic. Event lines come from the data untouched. The focused-item line is a lead-in fragment joined to
/// the item's plain card text, inserted verbatim (A12: frame data is never spoken); when that would be more than two
/// sentences (S2) the card text alone is spoken.
/// </summary>
public sealed class MerchantShopLines
{
	public const string FileName = "merchant-shop-lines.json";
	public const string ItemFocused = "item_focused";
	/// <summary>S2: a bubble is one or two sentences.</summary>
	public const int MaxSentences = 2;

	readonly Dictionary<string, string[]> _variants;
	readonly string[] _leadIns;

	MerchantShopLines(Dictionary<string, string[]> variants, string[] leadIns) { _variants = variants; _leadIns = leadIns; }

	/// <summary>No lines at all (the data is missing): every event is silent, a focused item speaks its card text alone.</summary>
	public static MerchantShopLines Empty { get; } = new(new Dictionary<string, string[]>(), Array.Empty<string>());

	public IReadOnlyCollection<string> Events => _variants.Keys;
	public IReadOnlyList<string> LeadIns => _leadIns;

	public IReadOnlyList<string> Variants(string eventKey) =>
		_variants.TryGetValue(eventKey, out var v) ? v : Array.Empty<string>();

	/// <summary>The line for an event; <paramref name="pick"/> chooses among its variants (wraps). Empty when the data has none.</summary>
	public string Event(string eventKey, int pick)
	{
		var v = Variants(eventKey);
		return v.Count == 0 ? "" : v[Mod(pick, v.Count)];
	}

	/// <summary>
	/// What the merchant says when an item is hovered or focused: the picked lead-in with the item's name and plain
	/// card text, or the card text alone when the joined line would be more than <see cref="MaxSentences"/> sentences
	/// (or there is no lead-in).
	/// </summary>
	public string ForItem(string name, string cardText, int pick)
	{
		if (_leadIns.Length == 0) return cardText;
		string joined = _leadIns[Mod(pick, _leadIns.Length)].Replace("{name}", name).Replace("{card_text}", cardText);
		return Sentences(joined) <= MaxSentences ? joined : cardText;
	}

	static int Mod(int a, int n) => ((a % n) + n) % n;

	static readonly Regex SentenceBreak = new(@"(?<=[.!?])\s+");

	/// <summary>Sentences in a line: pieces split after . ! ? and a space; a trailing piece with no stop counts too.</summary>
	public static int Sentences(string text) => SentenceBreak.Split(text.Trim()).Count(s => s.Length > 0);

	public static MerchantShopLines Parse(string json)
	{
		using var doc = JsonDocument.Parse(json);
		var r = doc.RootElement;
		if (!r.TryGetProperty("kind", out var k) || k.GetString() != "merchant_shop_lines") throw new FormatException("merchant shop lines: kind must be 'merchant_shop_lines'");
		if (!r.TryGetProperty("events", out var ev) || ev.ValueKind != JsonValueKind.Object) throw new FormatException("merchant shop lines: missing 'events'");
		var variants = new Dictionary<string, string[]>(StringComparer.Ordinal);
		string[] leadIns = Array.Empty<string>();
		foreach (var e in ev.EnumerateObject())
		{
			if (e.Name == ItemFocused)
			{
				leadIns = Strings(e.Value, "lead_ins", e.Name);
				foreach (string l in leadIns)
					if (!l.Contains("{name}") || !l.Contains("{card_text}")) throw new FormatException($"merchant shop lines: lead-in '{l}' needs {{name}} and {{card_text}}");
			}
			else
			{
				var v = Strings(e.Value, "variants", e.Name);
				foreach (string line in v)
				{
					if (line.Contains('{')) throw new FormatException($"merchant shop lines: '{e.Name}' line '{line}' has a placeholder");
					if (Sentences(line) > MaxSentences) throw new FormatException($"merchant shop lines: '{e.Name}' line '{line}' is more than {MaxSentences} sentences (S2)");
				}
				variants[e.Name] = v;
			}
		}
		return new MerchantShopLines(variants, leadIns);
	}

	static string[] Strings(JsonElement obj, string prop, string eventKey)
	{
		if (!obj.TryGetProperty(prop, out var a) || a.ValueKind != JsonValueKind.Array) throw new FormatException($"merchant shop lines: '{eventKey}' has no '{prop}'");
		var list = a.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String && x.GetString() is { Length: > 0 } s ? s : throw new FormatException($"merchant shop lines: '{eventKey}' has a blank line")).ToArray();
		if (list.Length == 0) throw new FormatException($"merchant shop lines: '{eventKey}' has no lines");
		return list;
	}

	public static MerchantShopLines LoadFile(string path) => Parse(File.ReadAllText(path));

	/// <summary>The lines in <paramref name="storyDir"/><c>/shop/merchant-shop-lines.json</c>; <see cref="Empty"/> when the file is absent.</summary>
	public static MerchantShopLines LoadDirectory(string storyDir)
	{
		string path = Path.Combine(storyDir, "shop", FileName);
		return File.Exists(path) ? LoadFile(path) : Empty;
	}
}
