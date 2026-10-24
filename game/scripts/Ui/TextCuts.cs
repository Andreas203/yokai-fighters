using System.Globalization;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-205: where a display name may be cut. Cutting at a UTF-16 index (<c>name[..1]</c>) can hand Godot 4.7.2 half a
/// surrogate pair, which it decodes into garbage code points and which can crash it (YOK-162). Every cut of a name on
/// screen goes through here.
/// </summary>
public static class TextCuts
{
	/// <summary>The offsets a name may be cut at: 0, the start of every text element (grapheme cluster) and <c>s.Length</c>.</summary>
	public static int[] Cuts(string s)
	{
		var cuts = new System.Collections.Generic.List<int> { 0 };
		for (int i = 0; i < s.Length;) cuts.Add(i += StringInfo.GetNextTextElementLength(s, i));
		return cuts.ToArray();
	}

	/// <summary>The whole first text element of <paramref name="name"/>, upper-cased; <paramref name="empty"/> when there is none.</summary>
	public static string Badge(string? name, string empty = "?")
	{
		if (string.IsNullOrEmpty(name)) return empty;
		string first = name[..Cuts(name)[1]];
		// StringInfo hands a lone surrogate back as its own element; Godot 4.7.2 mis-decodes it (YOK-162), so show the fallback.
		if (first.Length == 1 && char.IsSurrogate(first[0])) return empty;
		return first.ToUpperInvariant();
	}
}
