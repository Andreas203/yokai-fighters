using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-205 Test Breaker probes for TextCuts.Badge. No rule ID applies; expectations come from the ticket (whole first text element, upper-cased, no lone surrogate, "?" when empty).</summary>
public static class TextElementBadgeBreakerProbes
{
	static bool HasLone(string s)
	{
		for (int i = 0; i < s.Length; i++)
		{
			if (char.IsHighSurrogate(s[i]) && !(i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))) return true;
			if (char.IsLowSurrogate(s[i]) && !(i > 0 && char.IsHighSurrogate(s[i - 1]))) return true;
		}
		return false;
	}

	static void Names(JsonElement e, List<string> into)
	{
		if (e.ValueKind == JsonValueKind.Object)
			foreach (var p in e.EnumerateObject())
			{
				if ((p.Name == "name" || p.Name == "display_name" || p.Name == "displayName") && p.Value.ValueKind == JsonValueKind.String) into.Add(p.Value.GetString()!);
				Names(p.Value, into);
			}
		else if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) Names(x, into);
	}

	// Ticket: every authored name keeps the badge the old name[..1].ToUpperInvariant() gave.
	[Test]
	public static void AuthoredNames_SameBadgeAsOldLogic()
	{
		string root = ProjectSettings.GlobalizePath("res://") + "../data/";
		var names = new List<string>();
		foreach (var f in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
		{
			try { using var d = JsonDocument.Parse(File.ReadAllText(f)); Names(d.RootElement, names); } catch (JsonException) { }
		}
		Assert.True(names.Count > 10, $"found authored names ({names.Count})");
		foreach (var n in names)
		{
			string old = n.Length > 0 ? n[..1].ToUpperInvariant() : "?";
			Assert.Equal(old, TextCuts.Badge(n), $"badge of authored name '{n}'");
		}
	}

	[Test]
	public static void Flag_IsOneElement()
	{
		string flag = "\U0001F1EF\U0001F1F5"; // JP regional indicators, 4 code units
		Assert.Equal(flag, TextCuts.Badge(flag + "x"), "flag badge");
	}

	[Test]
	public static void Vs16_StaysWithBase()
	{
		string heart = "❤️";
		Assert.Equal(heart, TextCuts.Badge(heart + "x"), "VS16 badge");
	}

	[Test]
	public static void Crlf_IsOneElement()
	{
		Assert.Equal("\r\n", TextCuts.Badge("\r\nabc"), "CRLF is a single grapheme");
	}

	[Test]
	public static void LeadingSpace_IsTheBadge_LikeOldLogic()
	{
		Assert.Equal(" ", TextCuts.Badge(" fox"), "leading space");
	}

	[Test]
	public static void LeadingCombiningMark_AndComposedPair()
	{
		Assert.Equal("́", TextCuts.Badge("́e"), "lone combining mark first element");
		Assert.Equal("é".ToUpperInvariant(), TextCuts.Badge("é"), "e + acute");
	}

	[Test]
	public static void Eszett_UpperInvariantKeepsLength()
	{
		string b = TextCuts.Badge("ßa");
		Assert.True(b.Length >= 1 && !HasLone(b), "eszett badge is well formed");
		Assert.Equal("ß".ToUpperInvariant(), b, "eszett uppercase per invariant culture");
	}

	[Test]
	public static void TurkishDotlessI_UsesInvariantCulture()
	{
		var old = System.Globalization.CultureInfo.CurrentCulture;
		try
		{
			System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
			string dl = ((char)0x131).ToString(); Assert.Equal(dl, TextCuts.Badge(dl + "ris"), "dotless i unchanged by ToUpperInvariant, as the old logic");
			Assert.Equal("I", TextCuts.Badge("iris"), "i -> I (not dotted capital) under tr-TR");
		}
		finally { System.Globalization.CultureInfo.CurrentCulture = old; }
	}

	// Ticket: no badge ever contains a lone surrogate, even from malformed input.
	[Test]
	public static void LoneSurrogate_NeverInBadge()
	{
		foreach (var s in new[] { "\uD800abc", "\uDC00abc", "\uD83D", "\uDE00", "a\uD800" })
		{
			string b = TextCuts.Badge(s);
			Assert.True(!HasLone(b), $"badge of malformed '{System.Convert.ToHexString(System.Text.Encoding.Unicode.GetBytes(s))}' has a lone surrogate");
		}
	}

	[Test]
	public static void NullAndEmpty_UseMark()
	{
		Assert.Equal("?", TextCuts.Badge(null), "null");
		Assert.Equal("?", TextCuts.Badge(""), "empty");
		Assert.Equal("-", TextCuts.Badge(null, "-"), "null custom");
		Assert.Equal("", TextCuts.Badge("", ""), "empty with empty mark");
	}

	[Test]
	public static void Cuts_OfEmpty_IsJustZero()
	{
		var c = TextCuts.Cuts("");
		Assert.Equal(1, c.Length, "cuts of empty");
		Assert.Equal(0, c[0], "cut 0");
	}
}
