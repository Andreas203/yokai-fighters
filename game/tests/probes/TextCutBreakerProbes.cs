using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-161 test-breaker probes: the copy talisman cuts names only at text-element boundaries (T6 name fit, YOK-162).</summary>
public static class TextCutBreakerProbes
{
 sealed class View : IHudRunView
 {
  public bool CopyLocked { get; set; }
  public CopyTarget? Copy { get; set; }
  public bool InTanukiFight { get; set; }
  public bool ElderFight { get; set; }
 }

 static string Root => System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");

 static SpecialData Named(string jsonEscapedName)
 {
  string fd = AbilityPool.LoadFixtures(Root).Special("test-spirit-wave")!.Level1Json;
  return SpecialLoader.Parse("{\"kind\":\"special\",\"id\":\"tb-name\",\"name\":\"" + jsonEscapedName + "\",\"input\":{\"kata\":{\"slot\":\"A\"}},\"levels\":{\"1\":{\"frame_data\":" + fd + "}}}");
 }
 static string Esc(string s) => System.Text.Json.JsonSerializer.Serialize(s)[1..^1];

 static (string name, string letter) Show(FightHud hud, SpecialData d)
 {
  hud.RunView = new View { InTanukiFight = true, Copy = new CopyTarget(SpecialSlot.A, d, 1) };
  hud.Refresh(new Match());
  return (hud.CopyTarget.MoveName, UiFind.Get<Label>(hud.CopyTarget, "IconLetter").Text);
 }

 static FightHud Load(Node runner)
 {
  var hud = GD.Load<PackedScene>("res://scenes/ui/hud.tscn").Instantiate<FightHud>();
  runner.AddChild(hud);
  return hud;
 }

 static bool WellFormed(string s)
 {
  for (int i = 0; i < s.Length; i++)
  {
   if (char.IsHighSurrogate(s[i])) { if (i + 1 >= s.Length || !char.IsLowSurrogate(s[i + 1])) return false; i++; }
   else if (char.IsLowSurrogate(s[i])) return false;
  }
  return true;
 }

 // A line may not start with something that only makes sense glued to the previous character.
 static bool StartsClean(string line)
 {
  if (line.Length == 0) return true;
  var cat = CharUnicodeInfo.GetUnicodeCategory(line, 0);
  return cat != UnicodeCategory.NonSpacingMark && cat != UnicodeCategory.SpacingCombiningMark && cat != UnicodeCategory.EnclosingMark
   && cat != UnicodeCategory.Format && line[0] != '‍' && !(line[0] >= '︀' && line[0] <= '️') && !char.IsLowSurrogate(line[0]);
 }

 static readonly string Fam = "\U0001F468‍\U0001F469‍\U0001F467‍\U0001F466";
 static readonly string Flag = "\U0001F1EF\U0001F1F5";
 static readonly string Heart = "❤️";
 static readonly string Eacute = "é";
 static readonly string Astral = "\U0001D504";
 static readonly string Thumb = "\U0001F44D\U0001F3FD";

 static IEnumerable<string> Cluster()
 {
  foreach (string g in new[] { Fam, Flag, Heart, Eacute, Astral, Thumb, "\r\n", "क्ष" })
  {
   yield return g;
   yield return g + g + g;
   yield return string.Concat(Enumerable.Repeat(g, 40));
   yield return "A" + g + "B";
   yield return "Wandering " + string.Concat(Enumerable.Repeat(g, 12)) + "-Strike " + string.Concat(Enumerable.Repeat(g, 12));
   yield return string.Concat(Enumerable.Repeat(g + "-", 30));
  }
  yield return Fam + Fam + Fam + Fam + Fam + Fam;
 }

 // T6 / YOK-162: Cuts invariants over many strings, including lone surrogate inputs.
 [Test]
 public static void Cuts_AreStrictlyIncreasing_ZeroToLength_AndRealBoundaries(Node runner)
 {
  var inputs = Cluster().Concat(new[] { "", "a", "\uD800", "x\uD800y", "\uDC00\uD800", "ab\uDC00" }).ToList();
  var rng = new System.Random(161);
  for (int i = 0; i < 200; i++) inputs.Add(new string(Enumerable.Range(0, rng.Next(0, 40)).Select(_ => (char)rng.Next(0x20, 0x7F)).ToArray()));
  foreach (string s in inputs)
  {
   int[] c = CopyTalisman.Cuts(s);
   string tag = $"'{s.Length}:{string.Join(",", s.Take(6).Select(ch => ((int)ch).ToString("X")))}'";
   Assert.Equal(0, c[0], "starts at 0 " + tag);
   Assert.Equal(s.Length, c[^1], "ends at Length " + tag);
   for (int k = 1; k < c.Length; k++) Assert.True(c[k] > c[k - 1], "strictly increasing " + tag);
   var valid = new HashSet<int>(StringInfo.ParseCombiningCharacters(s)) { s.Length };
   foreach (int x in c) Assert.True(valid.Contains(x), $"cut {x} is a StringInfo boundary {tag}");
  }
 }

 // T6: FitName/badge never split a grapheme, for clusters in every position, at lengths forcing hyphen break and ellipsis.
 [Test]
 public static void FitName_NeverSplitsAGrapheme_OrEmitsMalformedUtf16(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var bad = new List<string>();
   foreach (string raw in Cluster())
   {
    var (name, letter) = Show(hud, Named(Esc(raw)));
    string up = raw.ToUpperInvariant();
    string tag = $"[{string.Join(",", raw.Take(8).Select(ch => ((int)ch).ToString("X")))}..len{raw.Length}]";
    if (!WellFormed(name)) bad.Add("malformed name " + tag);
    if (!WellFormed(letter)) bad.Add("malformed letter " + tag);
    foreach (string line in name.Split('\n')) if (!StartsClean(line)) bad.Add("line starts mid-grapheme " + tag);
    string first = up.Length == 0 ? "" : up[..StringInfo.GetNextTextElementLength(up, 0)];
    if (letter != first) bad.Add($"badge {tag}: expected first element of length {first.Length}, got length {letter.Length}");
    var inElems = new HashSet<string>();
    for (var e = StringInfo.GetTextElementEnumerator(up); e.MoveNext();) inElems.Add((string)e.Current);
    for (var e = StringInfo.GetTextElementEnumerator(name); e.MoveNext();)
    {
     string el = (string)e.Current;
     if (el != "-" && el != "\n" && el != "…" && !inElems.Contains(el)) { bad.Add($"foreign element {el.Length} chars {tag}"); break; }
    }
   }
   Assert.True(bad.Count == 0, string.Join("; ", bad.Take(8)));
  }
  finally { hud.Free(); }
 }

 // Single element wider than the slot, alone: kept whole (nothing to cut).
 [Test]
 public static void FitName_SingleHugeGrapheme_StaysOneElement(Node runner)
 {
  var hud = Load(runner);
  try
  {
   string huge = "e" + string.Concat(Enumerable.Repeat("́", 40));
   Assert.Equal(1, new StringInfo(huge).LengthInTextElements, "premise: one element");
   var (name, letter) = Show(hud, Named(Esc(huge)));
   Assert.True(WellFormed(name) && name[0] == (char)69, "name starts with the letter: " + string.Join(",", name.Take(6).Select(ch => ((int)ch).ToString("X"))) + " len " + name.Length);
   Assert.True(StartsClean(name), "no leading bare mark");
   Assert.True(letter[0] == (char)69 && new StringInfo(letter).LengthInTextElements == 1, "badge is one element");
  }
  finally { hud.Free(); }
 }

 [Test]
 public static void FitName_EmptyName_IsEmptyAndDoesNotThrow(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var (name, letter) = Show(hud, Named(""));
   Assert.Equal("", name, "empty name");
   Assert.Equal("", letter, "empty badge");
  }
  finally { hud.Free(); }
 }

 // Reference: main's FitName for ASCII, inlined from main's CopyTalisman.cs, run against the same label and font.
 static string OldFit(Label label, string text)
 {
  var font = label.GetThemeFont("font");
  float width = label.Size.X;
  const int NameSize = CopyTalisman.NameSize, NameMaxLines = CopyTalisman.NameMaxLines;
  float W(string t) => font.GetStringSize(t, fontSize: NameSize).X;
  var tokens = text.Replace("-", "- ").Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
  var lines = new List<string>();
  string line = "";
  bool rewrite = false;
  void Flush() { if (line.Length > 0) lines.Add(line); line = ""; }
  foreach (string tok in tokens)
  {
   string t = tok;
   string joined = line.Length == 0 ? t : (line.EndsWith('-') ? line + t : line + " " + t);
   if (W(joined) <= width) { line = joined; continue; }
   Flush();
   while (width > 0 && t.Length > 1 && W(t) > width)
   {
    rewrite = true;
    int n = t.Length - 1;
    while (n > 2 && W(t[..n] + "-") > width) n--;
    n = System.Math.Max(1, n);
    lines.Add(t[..n] + "-");
    t = t[n..];
   }
   line = t;
  }
  Flush();
  if (lines.Count > NameMaxLines)
  {
   rewrite = true;
   string last = lines[NameMaxLines - 1].TrimEnd('-');
   while (last.Length > 1 && W(last + "…") > width) last = last[..^1];
   lines = lines.GetRange(0, NameMaxLines - 1);
   lines.Add(last + "…");
  }
  return rewrite ? string.Join("\n", lines) : text;
 }

 // Regression: for ASCII, output equals main's algorithm. Authored names plus random names of 1-40 chars.
 [Test]
 public static void FitName_Ascii_MatchesMainsAlgorithm_ForAuthoredAndRandomNames(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var label = UiFind.Get<Label>(hud.CopyTarget, "MoveName");
   var names = new List<string>();
   foreach (var sp in AbilityPool.LoadRepo(Root).Specials) names.Add(sp.Name);
   foreach (string f in System.IO.Directory.GetFiles(System.IO.Path.Combine(Root, "data"), "*.json", System.IO.SearchOption.AllDirectories))
   {
    using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(f));
    if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && doc.RootElement.TryGetProperty("name", out var n) && n.ValueKind == System.Text.Json.JsonValueKind.String) names.Add(n.GetString()!);
   }
   Assert.True(names.Count > 5, "authored names found: " + names.Count);
   var rng = new System.Random(1612);
   const string alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz  --WWWWiiii";
   for (int i = 0; i < 400; i++) names.Add(new string(Enumerable.Range(0, rng.Next(1, 41)).Select(_ => alpha[rng.Next(alpha.Length)]).ToArray()));
   var diffs = new List<string>();
   foreach (string raw in names)
   {
    if (raw.Any(ch => ch > 0x7E || ch < 0x20)) continue;
    var (shown, _) = Show(hud, Named(Esc(raw)));
    string want = OldFit(label, raw.ToUpperInvariant());
    if (shown != want) diffs.Add($"'{raw}' -> '{shown.Replace("\n", "|")}' want '{want.Replace("\n", "|")}'");
   }
   Assert.True(diffs.Count == 0, diffs.Count + " differ: " + string.Join("; ", diffs.Take(5)));
  }
  finally { hud.Free(); }
 }

 // Same name twice gives the same fit, with another name shown between (no state leaks across Apply).
 [Test]
 public static void FitName_SameNameTwice_SameText_AfterAnotherNameBetween(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var a = Named(Esc("Wandering " + Fam + Fam + Fam + Fam + "-Strike Supercalifragilistic"));
   var (first, _) = Show(hud, a);
   Show(hud, Named("Foxfire"));
   var (second, _) = Show(hud, a);
   Assert.Equal(first, second, "same name, same fit");
  }
  finally { hud.Free(); }
 }
}
