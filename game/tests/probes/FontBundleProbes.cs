using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Ui;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Tests;

/// <summary>YOK-168 probes: the bundled font must be the only font, and the reserved labels must still hold the longest authored text.</summary>
public static class FontBundleProbes
{
	const string Bundled = "res://assets/fonts/NotoSerif-Bold.ttf";

	static IEnumerable<string> Files(string dir, params string[] exts)
	{
		foreach (var f in DirAccess.GetFilesAt(dir)) if (exts.Any(e => f.EndsWith(e))) yield return $"{dir}/{f}";
		foreach (var d in DirAccess.GetDirectoriesAt(dir)) foreach (var f in Files($"{dir}/{d}", exts)) yield return f;
	}

	// Intent (YOK-168): Linux and Windows measure alike, so no scene or theme may name an OS font.
	[Test]
	public static void NoSceneOrThemeNamesASystemFontOrOtherFontFile(Node host)
	{
		var bad = new List<string>();
		foreach (var path in Files("res://scenes", ".tscn", ".tres").Concat(Files("res://ui", ".tscn", ".tres")))
		{
			var text = FileAccess.GetFileAsString(path);
			if (text.Contains("SystemFont") || text.Contains("font_names")) bad.Add(path + " SystemFont");
			foreach (var line in text.Split('\n'))
				if ((line.Contains(".ttf") || line.Contains(".otf") || line.Contains(".woff")) && !line.Contains(Bundled)) bad.Add(path + " " + line.Trim());
		}
		Assert.True(bad.Count == 0, "non-bundled font: " + string.Join("; ", bad));
	}

	[Test]
	public static void ThemeDefaultAndHeadingFontsResolveToBundledFile(Node host)
	{
		var theme = GD.Load<Theme>("res://ui/talisman_theme.tres");
		Assert.True(theme.DefaultFont is FontFile ff && ff.ResourcePath == Bundled, "default font is bundled file");
		foreach (var t in new[] { "HeadingLabel", "HeadingLabelPaper" })
		{
			var fv = theme.GetFont("font", t) as FontVariation;
			Assert.True(fv != null, t + " is a FontVariation");
			Assert.True(fv!.BaseFont is FontFile b && b.ResourcePath == Bundled, t + " base font is bundled file");
		}
		foreach (var type in theme.GetFontTypeList())
			foreach (var name in theme.GetFontList(type))
			{
				var f = theme.GetFont(name, type);
				Font root = f is FontVariation v ? v.BaseFont : f;
				Assert.True(root is FontFile r && r.ResourcePath == Bundled, $"{type}/{name} resolves to bundled font");
			}
	}

	// tools/fonts/trim_line_height.py: ascent 917, descent 219, no line gap per 1000 em -> 1.136 em line.
	[Test]
	public static void BundledFontMetricsMatchTrimScript(Node host)
	{
		var f = GD.Load<FontFile>(Bundled);
		Assert.True(f != null, "font loads");
		foreach (int size in new[] { 1000, 26, 28 })
		{
			Assert.True(Mathf.Abs(f.GetAscent(size) - 0.917f * size) < 1f, $"ascent at {size}: {f.GetAscent(size)}");
			Assert.True(Mathf.Abs(f.GetDescent(size) - 0.219f * size) < 1f, $"descent at {size}: {f.GetDescent(size)}");
			Assert.True(Mathf.Abs(f.GetHeight(size) - 1.136f * size) < 1.5f, $"height at {size}: {f.GetHeight(size)}");
		}
	}

	static string DataDir()
	{
		var d = ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\');
		return System.IO.Path.GetFullPath(System.IO.Path.Combine(d, "..", "data"));
	}

	static IEnumerable<(string file, string text)> AuthoredTexts()
	{
		foreach (var file in System.IO.Directory.GetFiles(DataDir(), "*.json", System.IO.SearchOption.AllDirectories))
		{
			if (file.Contains("harness") || file.Contains("schema")) continue;
			System.Text.Json.JsonDocument? doc = null;
			try { doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(file)); } catch { continue; }
			using (doc)
			{
				var root = doc.RootElement;
				if (root.ValueKind != System.Text.Json.JsonValueKind.Object) continue;
				if (root.TryGetProperty("card", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.Object
					&& c.TryGetProperty("plain", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.String) yield return (file + "#plain", p.GetString()!);
				if (root.TryGetProperty("text", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String) yield return (file + "#text", t.GetString()!);
				if (root.TryGetProperty("name", out var n) && n.ValueKind == System.Text.Json.JsonValueKind.String) yield return (file + "#name", n.GetString()!);
			}
		}
	}

	// A missing glyph falls back to another font and changes metrics.
	[Test]
	public static void BundledFontCoversEveryCharacterInAuthoredText(Node host)
	{
		var f = GD.Load<FontFile>(Bundled);
		var missing = new HashSet<char>();
		foreach (var s in AuthoredTexts().Select(t => t.text))
			foreach (var ch in s) if (!char.IsWhiteSpace(ch) && !f.HasChar(ch)) missing.Add(ch);
		Assert.True(missing.Count == 0, "glyphs missing from bundled font: " + string.Join("", missing));
	}

	// Rule A12: plain language is shown first on the card; the Plain label must hold the longest authored plain text.
	[Test]
	public static void RewardCardPlainLabelHoldsLongestAuthoredPlainText(Node host)
	{
		var plains = AuthoredTexts().Where(t => t.file.EndsWith("#plain")).OrderByDescending(t => t.text.Length).Take(5).ToList();
		Assert.True(plains.Count > 0, "authored plain texts found");
		foreach (var (file, text) in plains)
			foreach (var kind in new[] { CardKind.NewMove, CardKind.Upgrade, CardKind.Modifier })
			{
				var card = GD.Load<PackedScene>("res://scenes/ui/reward_card.tscn").Instantiate<RewardCard>();
				host.AddChild(card);
				try
				{
					card.Bind(new CardView(kind, "x", "Longest Name", "kitsune", text, "frames", "TAG", "Pick a special", kind == CardKind.Upgrade ? 3 : 0));
					KitGallery.ForceLayout(card);
					var l = UiFind.Get<Label>(card, "Plain");
					float lineH = l.GetThemeFont("font").GetHeight(l.GetThemeFontSize("font_size"));
					float needed = l.GetLineCount() * lineH;
					Assert.True(needed <= l.Size.Y + 0.5f, $"{System.IO.Path.GetFileName(file)} {kind}: {l.GetLineCount()} lines need {needed}px, label {l.Size.Y}px ({text.Length} chars)");
					Assert.True(l.GetMinimumSize().X <= l.Size.X + 1, "no horizontal overflow (unbreakable word)");
				}
				finally { card.Free(); }
			}
	}

	[Test]
	public static void ClipTextOneLineLabelsKeepDescenders(Node host)
	{
		var short_ = new List<string>();
		foreach (var path in Files("res://scenes/ui", ".tscn"))
		{
			Node? root = null;
			try { root = GD.Load<PackedScene>(path).Instantiate(); } catch { continue; }
			host.AddChild(root);
			try
			{
				if (root is Control rc) { rc.Size = new Vector2(1920, 1080); KitGallery.ForceLayout(rc); }
				void Walk(Node n)
				{
					if (n is Label l && l.ClipText && l.Text.Length > 0 && l.AutowrapMode == TextServer.AutowrapMode.Off)
					{
						float h = l.GetThemeFont("font").GetHeight(l.GetThemeFontSize("font_size"));
						if (l.Size.Y > 0 && l.Size.Y < h - 0.5f) short_.Add($"{System.IO.Path.GetFileName(path)}/{l.Name} {l.Size.Y} < line {h}");
					}
					foreach (var c in n.GetChildren()) Walk(c);
				}
				Walk(root);
			}
			finally { root.Free(); }
		}
		Assert.True(short_.Count == 0, "clip_text labels shorter than their line: " + string.Join("; ", short_));
	}

	[Test]
	public static void ClipTextNameLabelsAreWideEnoughForLongestAuthoredNames(Node host)
	{
		var names = AuthoredTexts().Where(t => t.file.EndsWith("#name")).Select(t => t.text).Distinct().OrderByDescending(s => s.Length).Take(3).ToList();
		var tooNarrow = new List<string>();
		foreach (var path in new[] { "res://scenes/ui/map_node.tscn", "res://scenes/ui/hud.tscn", "res://scenes/ui/copy_chip.tscn" })
		{
			var root = GD.Load<PackedScene>(path).Instantiate();
			host.AddChild(root);
			try
			{
				if (root is Control rc) { rc.Size = new Vector2(1920, 1080); KitGallery.ForceLayout(rc); }
				void Walk(Node n)
				{
					if (n is Label l && l.ClipText && l.AutowrapMode == TextServer.AutowrapMode.Off && l.Name.ToString().Contains("Name"))
						foreach (var nm in names)
						{
							var w = l.GetThemeFont("font").GetStringSize(nm, HorizontalAlignment.Left, -1, l.GetThemeFontSize("font_size")).X;
							if (w > l.Size.X + 1) tooNarrow.Add($"{System.IO.Path.GetFileName(path)}/{l.Name}: '{nm}' {w}px > {l.Size.X}px");
						}
					foreach (var c in n.GetChildren()) Walk(c);
				}
				Walk(root);
			}
			finally { root.Free(); }
		}
		Assert.True(tooNarrow.Count == 0, string.Join("; ", tooNarrow));
	}
}
