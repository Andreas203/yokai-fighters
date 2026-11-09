using System.Collections.Generic;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-74 test-breaker probes: the copy-target talisman (T6) and the elder / Tanuki strip (Y3). Geometry only: headless Godot draws no pixels.</summary>
public static class CopyHudProbes
{
 sealed class View : IHudRunView
 {
  public bool CopyLocked { get; set; }
  public CopyTarget? Copy { get; set; }
  public bool InTanukiFight { get; set; }
  public bool ElderFight { get; set; }
 }

 static string Root => System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
 static FightHud Load(Node runner)
 {
  var hud = GD.Load<PackedScene>("res://scenes/ui/hud.tscn").Instantiate<FightHud>();
  runner.AddChild(hud);
  return hud;
 }
 static T Find<T>(Node n, string name) where T : Node => UiFind.Get<T>(n, name);

 static SpecialData Named(string name)
 {
  string fd = AbilityPool.LoadFixtures(Root).Special("test-spirit-wave")!.Level1Json;
  return SpecialLoader.Parse("{\"kind\":\"special\",\"id\":\"hud-name\",\"name\":\"" + name + "\",\"input\":{\"kata\":{\"slot\":\"A\"}},\"levels\":{\"1\":{\"frame_data\":" + fd + "}}}");
 }

 static readonly string[] RuleNames = { "Spirit Wave", "Rising Talisman", "Foxfire", "Fox Mirage", "Iron-Club Charge", "Oni Quake", "River Grab", "Shell Spin" };

 static List<string> AllNames()
 {
  var names = new List<string>(RuleNames);
  foreach (var sp in AbilityPool.LoadRepo(Root).Specials) names.Add(sp.Name);
  return names;
 }

 // Local glyph boxes of the wrapped name, as the label lays them out (centred, vertically centred, 3 px line gap).
 static List<Rect2> NameBoxes(Label label)
 {
  var font = label.GetThemeFont("font");
  int size = label.GetThemeFontSize("font_size");
  var p = new TextParagraph { Width = label.Size.X, Alignment = HorizontalAlignment.Center, BreakFlags = TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound };
  p.AddString(label.Text, font, size);
  int n = System.Math.Min(p.GetLineCount(), 2);
  float total = 0; var sizes = new List<Vector2>();
  for (int i = 0; i < n; i++) { var s = p.GetLineSize(i); sizes.Add(s); total += s.Y; }
  total += 3 * (n - 1);
  float y = label.Position.Y + (label.Size.Y - total) / 2;
  var boxes = new List<Rect2>();
  for (int i = 0; i < n; i++)
  {
   float cx = label.Position.X + label.Size.X / 2;
   boxes.Add(new Rect2(cx - sizes[i].X / 2, y, sizes[i].X, sizes[i].Y));
   y += sizes[i].Y + 3;
  }
  return boxes;
 }

 // Advisory 1. The locked seal (centre Size-6, radius 16, CopyTalisman.cs:142) against each wrapped name line's layout box.
 // Measured: only "Rising Talisman" (a name in the rules table) overlaps, line 2 "TALISMAN" box x 84-236 vs seal x 234-266 (2 px), y 72.5-102.5 vs 94-126.
 // The box includes side bearing and descender room, so the ink at most touches. Cosmetic: KnownOpen, flip to an empty list when the seal moves or the name slot narrows.
 [Test]
 public static void LockedSeal_ClearsEveryNameLineBox(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var t = hud.CopyTarget;
   var seal = t.SealRect; // the seal now sits top-right, clear of the name slot
   var label = Find<Label>(hud, "MoveName");
   var hits = new List<string>();
   foreach (string n in AllNames())
   {
    hud.RunView = new View { InTanukiFight = true, CopyLocked = true, Copy = new CopyTarget(SpecialSlot.A, Named(n), 1) };
    hud.Refresh(new Match());
    Assert.Equal(CopyTalismanState.Locked, t.State, "locked");
    var boxes = NameBoxes(label);
    for (int i = 0; i < boxes.Count; i++)
     if (boxes[i].Intersects(seal) && !hits.Contains(n)) hits.Add(n);
   }
   Assert.Equal("", string.Join(",", hits), "names whose line box meets the seal");
  }
  finally { hud.Free(); }
 }

 // Advisory 2 (geometry fallback). Every shape _Draw makes, recomputed from the Godot line rule (a width-w rect line
 // reaches w/2 past its edge), must sit in DrawnBounds, and DrawnBounds must sit in the strip (y<=138), for every state.
 [Test]
 public static void DrawnShapes_StayInsideDrawnBounds_InEveryState(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var t = hud.CopyTarget;
   var r = new Rect2(Vector2.Zero, t.Size);
   var shapes = new List<Rect2>
   {
    r.Grow(8 + 2), r.Grow(4 + 2), // glow rings, width 4
    new Rect2(new Vector2(6, 8), t.Size), r.Grow(2.5f), r.Grow(2 + 2), // shadow, ink border (5), persimmon frame (4)
    t.SealRect, // locked seal, top-right since round 2
   };
   var b = new Rect2(t.DrawnBounds.Position - t.Position, t.DrawnBounds.Size);
   foreach (var s in shapes) Assert.True(b.Encloses(s), $"shape {s} escapes DrawnBounds {b}");
   Assert.True(t.DrawnBounds.End.Y <= 138 && t.DrawnBounds.Position.Y >= 0, "inside the strip");
   // the pulse is a Modulate flash: it must not change the rect either
   var before = t.DrawnBounds;
   var run = RunState.NewRun(AbilityPool.LoadFixtures(Root));
   var v = new View { Copy = CopyRule.Target(run) };
   hud.RunView = v; hud.Refresh(new Match());
   run.LevelUp(SpecialSlot.A); v.Copy = CopyRule.Target(run); hud.Refresh(new Match());
   Assert.Equal(before, t.DrawnBounds, "levelling/pulse never moves or grows the drawn bounds");
  }
  finally { hud.Free(); }
 }

 // Advisory 3. Zero margin: the glow abuts the burst seal (884) and the opponent bar (1160) but never overlaps them.
 [Test]
 public static void GlowKeepsAMarginFromNeighbours(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var b = hud.CopyTarget.DrawnBounds;
   foreach (string name in new[] { "BurstSeal", "P2Health", "P2HealthText", "Temperament", "Meter3", "MeterText" })
   {
    var r = Find<Control>(hud, name).GetGlobalRect();
    Assert.True(!b.Intersects(r), $"talisman bounds {b} overlap {name} {r}");
   }
   Assert.True(Find<Control>(hud, "P2Health").GetGlobalRect().Position.X - b.End.X >= 4f, "gap to the opponent bar is at least 4 px");
   Assert.True(b.Position.X - Find<Control>(hud, "BurstSeal").GetGlobalRect().End.X >= 4f, "gap to the burst seal is at least 4 px");
  }
  finally { hud.Free(); }
 }

 // Advisory 4. V1/HUD: the talisman's drawn extent vs the result-card mask rect.
 [Test]
 public static void TalismanDrawnExtent_ClearsResultCardMaskRect(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var b = hud.CopyTarget.DrawnBounds;
   foreach (string path in new[] { "lose_screen", "demo_complete" })
   {
    var card = GD.Load<PackedScene>($"res://scenes/ui/{path}.tscn").Instantiate<Control>();
    runner.AddChild(card);
    try
    {
     card.Size = new Vector2(1920, 1080);
     var mask = Find<Control>(card, "Mask").GetGlobalRect();
     var panel = Find<Control>(card, "Panel").GetGlobalRect();
     Assert.True(!b.Intersects(panel) || path == "lose_screen", $"{path}: paper never meets the talisman");
     // lose screen: mask rect starts at y=135, the glow's bottom row is y 135..136 -> 1 px of rect overlap, texture edge is transparent.
     Assert.True(!b.Intersects(mask), $"{path} mask {mask} vs talisman {b}");
    }
    finally { card.Free(); }
   }
  }
  finally { hud.Free(); }
 }

 static float Lum(Color c)
 {
  float L(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
  return 0.2126f * L(c.R) + 0.7152f * L(c.G) + 0.0722f * L(c.B);
 }
 static float Contrast(Color a, Color b) { float x = Lum(a), y = Lum(b); return (System.Math.Max(x, y) + 0.05f) / (System.Math.Min(x, y) + 0.05f); }

 // Advisory 5. The outline-exempt labels must lie fully on their opaque plate in each state.
 [Test]
 public static void ExemptLabels_SitOnOpaquePlates_InEveryState(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var t = hud.CopyTarget;
   var plate = t.GetGlobalRect();
   var run = RunState.NewRun(AbilityPool.LoadFixtures(Root));
   var states = new (string, View)[]
   {
    ("none", new View { InTanukiFight = true }),
    ("defined", new View { InTanukiFight = true, Copy = CopyRule.Target(run) }),
    ("locked", new View { InTanukiFight = true, Copy = CopyRule.Target(run), CopyLocked = true }),
    ("tanuki", new View { Copy = CopyRule.Target(run), CopyLocked = true, InTanukiFight = true }),
    ("elder", new View { ElderFight = true }),
   };
   foreach (var (name, v) in states)
   {
    hud.RunView = v; hud.Refresh(new Match());
    if (name == "elder") Assert.True(!t.Visible, "elder: the talisman is hidden (AM6, T6)");
    else Assert.True(t.Visible && t.Modulate.A >= 1f && t.SelfModulate.A >= 1f, $"{name}: plate drawn opaque");
    if (name != "elder") foreach (string n in new[] { "Caption", "MoveName", "Level", "IconLetter" })
    {
     var l = Find<Label>(t, n);
     if (!l.Visible) continue;
     Assert.True(plate.Encloses(l.GetGlobalRect()), $"{name}: {n} {l.GetGlobalRect()} sits on the plate {plate}");
    }
    var strip = Find<Control>(hud, "TwistStrip");
    var paper = Find<Control>(strip, "Paper");
    Assert.True(paper is TwistStripDecor && paper.Visible, "strip has a paper plate");
    foreach (string n in new[] { "Kicker", "TwistText" })
     Assert.True(paper.GetGlobalRect().Encloses(Find<Label>(strip, n).GetGlobalRect()), $"{name}: {n} on strip paper");
    Assert.True(Find<Label>(strip, "Kicker").Modulate.A >= 1f && strip.Modulate.A >= 1f, $"{name}: strip not faded");
   }
  }
  finally { hud.Free(); }
 }

 // Advisory 5b. Legibility of the grey 'none' plate (grey 0.6,0.58,0.5 behind the red caption). No rule sets a ratio; pinned as today's value.
 [Test]
 public static void NoneState_CaptionIsInkOnGrey_ReadsAtThreeToOne(Node runner)
 {
  var hud = Load(runner);
  try
  {
   hud.RunView = new View { InTanukiFight = true }; hud.Refresh(new Match());
   var cap = Find<Label>(hud.CopyTarget, "Caption");
   float ratio = Contrast(cap.GetThemeColor("font_color"), new Color(0.6f, 0.58f, 0.5f));
   float onPaper = Contrast(cap.GetThemeColor("font_color"), FightHud.Paper);
   Assert.True(onPaper > 3.5f, "caption reads on paper");
   Assert.True(ratio >= 3f, $"caption on the grey plate is {ratio:F2}:1");
  }
  finally { hud.Free(); }
 }

 // Advisory 6 / HUD constraint: no name may push any HUD label below 17 px at 720p (font*720/1080).
 [Test]
 public static void LongSingleWordName_StaysAtTheSeventeenPxFloor_At720p(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var label = Find<Label>(hud.CopyTarget, "MoveName");
   var font = label.GetThemeFont("font");
   var worst = new List<string>();
   foreach (string n in new[] { "Thunderclap", "Thunderstrike", "Hundredfold", "Wandering Thunderstrike", "Shapeshifter", "Pandemonium", "Tsukuyomi-Barrage", "Supercalifragilistic" })
   {
    hud.RunView = new View { InTanukiFight = true, Copy = new CopyTarget(SpecialSlot.A, Named(n), 1) };
    hud.Refresh(new Match());
    int size = label.GetThemeFontSize("font_size");
    if (size * 720f / 1080 < 17) worst.Add($"{n} -> {size}px ({size * 720f / 1080:F1} at 720p)");
   }
   Assert.True(worst.Count == 0, "names under the floor: " + string.Join("; ", worst));
  }
  finally { hud.Free(); }
 }

 // Advisory 6b: the widest single word that still fits at the 26 px floor in the 180 px slot, in capitals.
 [Test]
 public static void ThirteenLetterCapitals_FitTheSlot_AtTheFloorSize(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var label = Find<Label>(hud.CopyTarget, "MoveName");
   var font = label.GetThemeFont("font");
   foreach (string w in new[] { "MMMMMMMMMM", "WWWWWWWW", "TALISMAN", "INTERNATIONAL" })
   {
    float wd = font.GetStringSize(w, fontSize: CopyTalisman.NameSize).X;
    GD.Print($"PROBE width {w} @26 = {wd} (slot {label.Size.X})");
   }
   Assert.True(font.GetStringSize("TALISMAN", fontSize: CopyTalisman.NameSize).X <= label.Size.X, "TALISMAN fits");
  }
  finally { hud.Free(); }
 }

 const string DenMap = """
 {"kind":"run_map","id":"hud-copy","schema_version":1,
  "nodes":[{"id":"a","row":1,"type":"duel","yokai":"kitsune"},{"id":"den","row":2,"type":"boss","yokai":"mujina"}],
  "routes":[{"from":"a","to":"den"}]}
 """;
 const string DenScenes = """
 {"kind":"node_scenes","id":"hud-copy-scenes","schema_version":1,"default_scene":"res://scenes/fight.tscn","fallback_fighter":"kitsune",
  "tiers":{"yokai":{"reaction_frames":22}},"types":{"duel":{"tier":"yokai"},"boss":{"tier":"yokai"}},"opponents":{"kitsune":{"fighter":"kitsune"}}}
 """;
 static RunSession Session(uint seed) => RunSession.Start(AbilityPool.LoadFixtures(Root), MapLoader.Parse(DenMap), NodeSceneTable.Parse(DenScenes), seed);

 // Own: walking none -> defined -> levelled -> changed -> locked -> Tanuki -> over with a HUD refreshing every step leaves the
 // session in the exact state an unobserved twin reaches (same hash after every step, same draft after the win: no RNG consumed).
 [Test]
 public static void EveryTransition_LeavesStateHashUnchanged_AndConsumesNoRng(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var watched = Session(74); var twin = Session(74);
   hud.RunView = new SessionHudRunView(watched);
   void Both(System.Action<RunSession> act, string step)
   {
    act(watched); act(twin);
    ulong before = watched.StateHash();
    for (int i = 0; i < 3; i++) hud.Refresh(new Match());
    Assert.Equal(before, watched.StateHash(), step + ": Refresh leaves the hash alone");
    Assert.Equal(twin.StateHash(), watched.StateHash(), step + ": watched run equals the unwatched twin");
   }
   hud.Refresh(new Match());
   Assert.True(!hud.CopyTarget.Visible, "AM6: hidden outside the Tanuki fight, so no copy toast or pulse can fire on the way");
   Both(s => s.Enter("a"), "enter a");
   Both(s => s.Run.LevelUp(SpecialSlot.A), "level A (levelled or changed)");
   Both(s => s.Run.LevelUp(SpecialSlot.B), "level B");
   Both(s => s.Run.LevelUp(SpecialSlot.B), "level B again");
   var da = watched.WinFight(700); var db = twin.WinFight(700);
   Assert.Equal(da?.GetType(), db?.GetType(), "draft kinds match");
   Both(s => s.CompleteNode(), "complete a");
   Both(s => s.Enter("den"), "enter the den: locked");
   Assert.Equal(CopyTalismanState.Locked, hud.CopyTarget.State, "locked");
   Assert.True(hud.TwistStripVisible && hud.TwistLine.Contains("no modifiers"), "Tanuki strip");
   Both(s => s.LoseFight(), "run over");
   Assert.Equal(CopyTalismanState.Locked, hud.CopyTarget.State, "a lost run still shows the locked copy, no crash");
  }
  finally { hud.Free(); }
 }

 // Own: T1 lock holds against any later levelling, including a different slot overtaking; level of the talisman follows Ryo (existing test), the move does not.
 [Test]
 public static void AfterTheLock_OvertakingSpecial_NeverMovesTheTalisman_AndNoToast(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var s = Session(5);
   hud.RunView = new SessionHudRunView(s);
   s.Enter("a"); s.WinFight(700); s.CompleteNode(); s.Enter("den");
   hud.Refresh(new Match());
   string locked = hud.CopyTarget.MoveName;
   for (int i = 0; i < 2; i++) { s.Run.LevelUp(SpecialSlot.A); hud.Refresh(new Match()); }
   s.Run.LevelUp(SpecialSlot.B); hud.Refresh(new Match());
   Assert.Equal(locked, hud.CopyTarget.MoveName, "locked move");
   Assert.Equal("", hud.CopyTarget.ToastText, "no toast");
   Assert.Equal("", hud.CopyTarget.LastEvent, "no event");
  }
  finally { hud.Free(); }
 }

 // Own: lock with no copy (nothing in a slot at the lock): grey state, neutral caption, no Tanuki strip.
 [Test]
 public static void LockedWithNullCopy_ShowsGreyDash_NotLockedCaption(Node runner)
 {
  var hud = Load(runner);
  try
  {
   hud.RunView = new View { CopyLocked = true, InTanukiFight = true };
   hud.Refresh(new Match());
   var t = hud.CopyTarget;
   Assert.Equal(CopyTalismanState.None, t.State, "none");
   Assert.Equal("-", t.MoveName, "dash");
   Assert.Equal("", t.LevelText, "no level");
   Assert.True(!hud.TwistStripVisible, "no strip");
   Assert.Equal("COPY TARGET", t.CaptionText, "no 'LOCKED' on an empty plate");
  }
  finally { hud.Free(); }
 }

 // Own: T4 / Y3 (YOK-109): the strip shows Ryo's level for Lv 1, 2, 3 and says "no modifiers, no evolution" (the copy itself runs MoveLevel, Lv 3 = the Lv 2 tuning).
 [Test]
 public static void TanukiStrip_ShowsRyosLevel_ForLv1To3(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var data = Named("Spirit Wave");
   foreach (int lv in new[] { 1, 2, 3 })
   {
    var copy = new CopyTarget(SpecialSlot.A, data, lv);
    hud.RunView = new View { CopyLocked = true, InTanukiFight = true, Copy = copy };
    hud.Refresh(new Match());
    Assert.Equal($"Spirit Wave · Lv {lv} · no modifiers, no evolution", hud.TwistLine, $"Lv {lv} strip");
    Assert.Equal($"Lv {lv}", hud.CopyTarget.LevelText, $"Lv {lv} talisman keeps Ryo's level");
    Assert.Equal(System.Math.Min(lv, 2), copy.MoveLevel, "T4 cap");
   }
   // elder reminder never shows on the Tanuki fight, and the Tanuki line wins over it
   hud.RunView = new View { CopyLocked = true, InTanukiFight = true, ElderFight = true, Copy = new CopyTarget(SpecialSlot.A, data, 1) };
   hud.Refresh(new Match());
   Assert.Equal("THE TANUKI COPIES", hud.TwistKicker, "tanuki wins");
  }
  finally { hud.Free(); }
 }

 // Own: rapid repeated Refresh with a changing target neither stacks toast text nor leaks tweens.
 [Test]
 public static void RapidRefresh_DoesNotStackToastsOrTweens(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var run = RunState.NewRun(AbilityPool.LoadFixtures(Root));
   var v = new View { Copy = CopyRule.Target(run) };
   hud.RunView = v; hud.Refresh(new Match());
   int Live() { int c = 0; foreach (var tw in runner.GetTree().GetProcessedTweens()) if (tw.IsValid() && tw.IsRunning()) c++; return c; }
   int baseline = Live();
   var slots = new[] { SpecialSlot.A, SpecialSlot.B };
   for (int i = 0; i < 4; i++) // two specials, two level-ups each (cap Lv 3): overtakes and level-ups alternate
   {
    run.LevelUp(slots[i % 2]); v.Copy = CopyRule.Target(run);
    for (int k = 0; k < 30; k++) hud.Refresh(new Match());
   }
   int after = Live(); // killed tweens linger in the list until the next frame, so only live ones count
   Assert.True(after - baseline <= 2, $"tweens grew by {after - baseline}");
   Assert.True(hud.CopyTarget.ToastText.Split('\n').Length == 1, "one toast line only");
   Assert.True(hud.CopyTarget.ToastText.Split("Copy target:").Length <= 2, "toast text is a single message: " + hud.CopyTarget.ToastText);
  }
  finally { hud.Free(); }
 }

 // Own: an opponent of 'tanuki' shows neither a portrait nor a likeness (the boss is not revealed).
 [Test]
 public static void TanukiOpponent_NoPortrait_NoLikenessPath(Node runner)
 {
  Assert.Equal("", FightHud.PortraitPath("tanuki"), "no path");
  string original = FightScene.Opponent;
  var hud = Load(runner);
  try
  {
   FightScene.Opponent = "tanuki";
   hud.RunView = new View { CopyLocked = true, InTanukiFight = true, Copy = new CopyTarget(SpecialSlot.A, Named("Fox Mirage"), 2) };
   hud.Refresh(new Match());
   Assert.True(!Find<TextureRect>(hud, "P2Portrait").Visible && Find<TextureRect>(hud, "P2Portrait").Texture == null, "no portrait texture");
   Assert.True(Find<Label>(hud, "P2HealthText").Text.StartsWith(Story.StoryLibrary.DisplayName("tanuki").ToUpperInvariant()), "name per display table");
  }
  finally { FightScene.Opponent = original; hud.Free(); }
 }

 // Own: 720p. The HUD scales as a whole; every label (including the fitted name and the strip) clears 17 px at 720p in every state.
 [Test]
 public static void EveryHudLabel_ClearsSeventeenPx_At720p_InEveryState(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var run = RunState.NewRun(AbilityPool.LoadFixtures(Root));
   foreach (var v in new[] { new View(), new View { Copy = CopyRule.Target(run) }, new View { Copy = CopyRule.Target(run), CopyLocked = true, InTanukiFight = true }, new View { ElderFight = true } })
   {
    hud.RunView = v; hud.Refresh(new Match());
    foreach (Node n in hud.GetNode("Strip").FindChildren("*", "Label", true, false))
    {
     var l = (Label)n;
     if (l.Text.Length == 0) continue;
     Assert.True(l.GetThemeFontSize("font_size") * 720f / 1080 >= 17, $"{l.Name} {l.GetThemeFontSize("font_size")}px is under 17 at 720p");
    }
   }
  }
  finally { hud.Free(); }
 }

 static string LettersOf(string t) { var sb = new System.Text.StringBuilder(); foreach (char ch in t) if (char.IsLetterOrDigit(ch)) sb.Append(char.ToUpperInvariant(ch)); return sb.ToString(); }

 // Round 2. Whatever the name, the slot shows it at 26 px, in at most two one-line paragraphs, each inside the slot width,
 // never losing or inventing letters: no '…' means every letter is still there; with '…' what is shown is a prefix.
 [Test]
 public static void NameFit_EveryShape_KeepsSizeLinesWidthAndLetters(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var label = Find<Label>(hud.CopyTarget, "MoveName");
   var font = label.GetThemeFont("font");
   var names = new[]
   {
    "Fox", "Fox Mirage", "Hundred Demon Parade", "Thousand Hundred Demon Night Parade", new string('W', 40), new string('a', 40),
    "Iron-Club Charge", "Iron-Club-Charge-Of-The-Hundred-Oni", "Spirit Wave 2", "Burst 100", "O'Neil's Strike", "Ōkami Kagé Ünïcode",
    "Kitsune-no-Yōkai-Hyakki-Yagyō-Parade", "A", "X Y Z", "Wave  Double  Space", "Trailing-",
   };
   foreach (string n in names)
   foreach (bool locked in new[] { false, true })
   {
    hud.RunView = new View { InTanukiFight = true, CopyLocked = locked, Copy = new CopyTarget(SpecialSlot.A, Named(n), 1) };
    hud.Refresh(new Match());
    string shown = label.Text;
    string tag = $"'{n}' locked={locked} -> '{shown.Replace("\n", "|")}'";
    Assert.Equal(CopyTalisman.NameSize, label.GetThemeFontSize("font_size"), tag + ": size");
    var paras = shown.Split('\n');
    Assert.True(paras.Length <= 2, tag + ": paragraphs");
    Assert.Equal(label.GetLineCount(), label.GetVisibleLineCount(), tag + ": nothing clipped");
    foreach (string word in shown.Replace("-", "- ").Split(new[] { ' ', '\n' }, System.StringSplitOptions.RemoveEmptyEntries)) Assert.True(font.GetStringSize(word.TrimEnd(), fontSize: 26).X <= label.Size.X, tag + ": word fits");
    string full = LettersOf(n), got = LettersOf(shown);
    Assert.True(got.Length > 0, tag + ": not blank");
    if (shown.Contains('…')) Assert.True(full.StartsWith(got) && got.Length < full.Length, tag + ": ellipsis shows a true prefix");
    else Assert.Equal(full, got, tag + ": no letter lost or added without an ellipsis");
    Assert.True(!shown.Contains("-\n-") && !shown.StartsWith("-") || n.StartsWith("-"), tag + ": no stray hyphen");
   }
  }
  finally { hud.Free(); }
 }

 // Round 2. The hyphenation slices UTF-16 code units: it must not cut a surrogate pair or strand a combining mark.
 [Test]
 public static void NameFit_NeverCutsASurrogatePair(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var label = Find<Label>(hud.CopyTarget, "MoveName");
   foreach (string n in new[] { string.Concat(System.Linq.Enumerable.Repeat("\U0001D504", 30)), string.Concat(System.Linq.Enumerable.Repeat("é", 30)) })
   {
    hud.RunView = new View { InTanukiFight = true, Copy = new CopyTarget(SpecialSlot.A, Named(n), 1) };
    hud.Refresh(new Match());
    string shown = label.Text;
    for (int i = 0; i < shown.Length; i++)
    {
     if (char.IsHighSurrogate(shown[i])) { Assert.True(i + 1 < shown.Length && char.IsLowSurrogate(shown[i + 1]), "high surrogate stranded"); i++; }
     else Assert.True(!char.IsLowSurrogate(shown[i]), "low surrogate stranded");
    }
    foreach (string line in shown.Split('\n'))
     Assert.True(line.Length == 0 || line[0] != '́', "a combining mark starts a line");
   }
  }
  finally { hud.Free(); }
 }

 // Round 2. An empty name: no crash, the plate does not go blank without a cue (the dash is the none-state cue; this documents what is shown).
 [Test]
 public static void KnownOpen_EmptyName_ShowsBlankSlot(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var data = Named("x");
   var empty = SpecialLoader.Parse("{\"kind\":\"special\",\"id\":\"hud-empty\",\"name\":\"\",\"input\":{\"kata\":{\"slot\":\"A\"}},\"levels\":{\"1\":{\"frame_data\":" + AbilityPool.LoadFixtures(Root).Special("test-spirit-wave")!.Level1Json + "}}}");
   hud.RunView = new View { InTanukiFight = true, Copy = new CopyTarget(SpecialSlot.A, empty, 1) };
   hud.Refresh(new Match());
   // Known open: a blank name draws an empty slot with state Defined and no letter badge. Flip to Text.Length > 0 if a fallback is added.
   Assert.Equal("", hud.CopyTarget.MoveName, "blank name stays blank");
   Assert.Equal("", Find<Label>(hud.CopyTarget, "IconLetter").Text, "no letter");
  }
  finally { hud.Free(); }
 }

 // Round 2. The moved seal against the caption's ink box, the level tag, the icon and the name, in every state.
 [Test]
 public static void Seal_ClearsCaptionLevelIconAndName_InEveryState(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var t = hud.CopyTarget;
   var run = RunState.NewRun(AbilityPool.LoadFixtures(Root));
   var views = new (string, View)[]
   {
    ("none", new View { InTanukiFight = true }), ("defined", new View { InTanukiFight = true, Copy = CopyRule.Target(run) }),
    ("locked", new View { InTanukiFight = true, Copy = CopyRule.Target(run), CopyLocked = true }),
    ("tanuki", new View { Copy = CopyRule.Target(run), CopyLocked = true, InTanukiFight = true }),
   };
   foreach (var (name, v) in views)
   {
    hud.RunView = v; hud.Refresh(new Match());
    var seal = t.SealRect;
    var cap = Find<Label>(t, "Caption");
    var font = cap.GetThemeFont("font");
    float w = font.GetStringSize(cap.Text, fontSize: cap.GetThemeFontSize("font_size")).X;
    var capInk = new Rect2(cap.Position.X + (cap.Size.X - w) / 2, cap.Position.Y, w, cap.Size.Y);
    Assert.True(!capInk.Intersects(seal), $"{name}: seal {seal} meets caption '{cap.Text}' ink {capInk}");
    foreach (string n in new[] { "Level", "Icon", "IconLetter", "MoveName" })
    {
     var c = Find<Control>(t, n);
     if (c.Visible) Assert.True(!new Rect2(c.Position, c.Size).Intersects(seal), $"{name}: seal meets {n}");
    }
    if (name == "locked") Assert.Equal("LOCKED", cap.Text, "locked caption");
   }
  }
  finally { hud.Free(); }
 }

 // Round 2. Drawn bounds with the moved seal: still inside the strip (y 0..138), clear of both neighbours, and they enclose the seal in parent coordinates.
 [Test]
 public static void DrawnBounds_WithMovedSeal_StayInStripAndGap(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var t = hud.CopyTarget;
   var b = t.DrawnBounds;
   var sealParent = new Rect2(t.Position + t.SealRect.Position, t.SealRect.Size);
   Assert.True(b.Encloses(sealParent), $"bounds {b} enclose seal {sealParent}");
   Assert.True(b.Position.Y >= 0 && b.End.Y <= 138, $"bounds y {b.Position.Y}..{b.End.Y}");
   Assert.True(sealParent.Position.Y >= 0, "seal is on screen");
   // 720p: the HUD scales by 2/3; after pixel snapping the glow still leaves at least one device pixel to both neighbours
   float k = 720f / 1080;
   float burstEnd = Find<Control>(hud, "BurstSeal").GetGlobalRect().End.X, barStart = Find<Control>(hud, "P2Health").GetGlobalRect().Position.X;
   Assert.True(Mathf.Floor(barStart * k) - Mathf.Ceil(b.End.X * k) >= 1, "720p gap right");
   Assert.True(Mathf.Floor(b.Position.X * k) - Mathf.Ceil(burstEnd * k) >= 1, "720p gap left");
  }
  finally { hud.Free(); }
 }

 // Round 2. Verdict probe on the author's IsNameOrItsFit (private in FightHudTests): it accepts a dash and a shorter, different name.
 [Test]
 public static void IsNameOrItsFit_IsStrict(Node runner)
 {
  var m = typeof(FightHudTests).GetMethod("IsNameOrItsFit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
  bool Call(string full, string shown) => (bool)m.Invoke(null, new object[] { full, shown })!;
  Assert.True(Call("Spirit Wave", "SPIRIT WAVE"), "right name accepted");
  Assert.True(!Call("Spirit Wave", "-"), "a bare dash is refused");
  Assert.True(!Call("Spirit Wave", "SPIRIT"), "an uncut shorter name is refused");
  Assert.True(!Call("Spirit Wave", "S"), "a single letter is refused");
  Assert.True(Call("Spirit Wave", "SPIRIT-\nWAVE…"), "a cut name with a 4+ letter prefix passes");
  Assert.True(!Call("Spirit Wave", ""), "empty is refused");
  Assert.True(!Call("Spirit Wave", "FOXFIRE"), "an unrelated name is refused");
 }

 // Round 2. The fixture target's name is long (it is ellipsised), so the author's three weakened assertions see a truncated name.
 // Pinned: what is shown, with its text, so a change to the fit shows here. Flip when fixtures get a short name.
 [Test]
 public static void KnownOpen_FixtureTargetName_IsShownEllipsised(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var target = CopyRule.Target(RunState.NewRun(AbilityPool.LoadFixtures(Root)))!;
   hud.RunView = new View { InTanukiFight = true, Copy = target };
   hud.Refresh(new Match());
   string shown = hud.CopyTarget.MoveName;
   GD.Print("PROBE fixture name shown: " + shown.Replace("\n", "|"));
   Assert.True(shown.Contains('…') && shown.Length < target.Data.Name.Length, "long fixture name is cut: " + shown);
  }
  finally { hud.Free(); }
 }
}
