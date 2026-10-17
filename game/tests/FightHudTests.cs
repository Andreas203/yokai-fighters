using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

public static class FightHudTests
{
 static FightHud Load(Node runner)
 {
  var hud = GD.Load<PackedScene>("res://scenes/ui/hud.tscn").Instantiate<FightHud>();
  runner.AddChild(hud);
  return hud;
 }
 static T Find<T>(Node n, string name) where T : Node => UiFind.Get<T>(n, name);

 [Test]
 public static void Health_FullDamagedZero_AndMeter_EmptyPartialFull_BurstReadySpent(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var match = new Match();
   foreach (int hp in new[] { 1000, 720, 0 })
   {
    match.P1.Health = hp; match.P2.Health = hp;
    hud.Refresh(match);
    for (int i = 1; i <= 2; i++)
    {
     Assert.Equal((double)hp, Find<ProgressBar>(hud, $"P{i}Health").Value, "live health fill");
     Assert.True(Find<Label>(hud, $"P{i}HealthText").Text.EndsWith($"{hp} / 1000"), "current / max health");
    }
   }
   foreach (int meter in new[] { 0, 40, 140, 300 })
   foreach (bool spent in new[] { false, true })
   {
    match.P1.Meter = meter; match.P1.BurstUsed = spent;
    hud.Refresh(match);
    for (int i = 0; i < 3; i++)
     Assert.Equal((double)System.Math.Clamp(meter - 100 * i, 0, 100), Find<ProgressBar>(hud, $"Meter{i+1}").Value, "segment fill");
    Assert.Equal($"METER {meter} / 300", Find<Label>(hud, "MeterText").Text, "live meter label");
    Assert.Equal(spent ? "×" : "B", Find<Label>(hud, "BurstLetter").Text, "burst has a non-colour state cue");
    Assert.Equal(spent ? "BurstSealSpent" : "BurstSeal", Find<Control>(hud, "BurstSeal").ThemeTypeVariation.ToString(), "burst style");
    Assert.Equal(meter, match.P1.Meter, "HUD does not spend meter");
    Assert.Equal(spent, match.P1.BurstUsed, "HUD does not spend burst");
   }
  }
  finally { hud.Free(); }
 }

 [Test]
 public static void OpponentIdentity_PortraitAndLongestNames(Node runner)
 {
  string original = FightScene.Opponent;
  var hud = Load(runner);
  try
  {
   foreach (string id in new[] { "kitsune", "nine-tailed-kitsune", "elder-kappa", "elder-oni", "oni", "kappa", "tanuki" })
   {
    FightScene.Opponent = id;
    hud.Refresh(new Match());
    var text = Find<Label>(hud, "P2HealthText");
    Assert.True(text.Text.StartsWith(Story.StoryLibrary.DisplayName(id).ToUpperInvariant()), "name follows opponent");
    Assert.True(text.GetThemeFont("font").GetStringSize(text.Text, fontSize: text.GetThemeFontSize("font_size")).X <= text.Size.X, "long name and health fit");
    var portrait = Find<TextureRect>(hud, "P2Portrait");
    Assert.Equal(id.Contains("kitsune"), portrait.Visible, "missing art never shows the wrong opponent or reveals the boss");
    if (portrait.Visible) Assert.Equal(FightHud.PortraitPath(id), portrait.Texture.ResourcePath, "per-opponent lookup");
   }
  }
  finally { FightScene.Opponent = original; hud.Free(); }
 }

 [Test]
 public static void JumpEnvelope_ClearsFloatingHud_AtBothCornersAndMidStage(Node runner)
 {
  var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
  scene.ExternalDrive = true;
  runner.AddChild(scene);
  try
  {
   var c = scene.Match.Config;
   int edge = c.StageHalfWidth - c.BodyWidth / 2;
   foreach (int x in new[] { -edge, 0, edge })
   {
    scene.Match.P1.X = x;
    scene.Match.P2.X = System.Math.Clamp(x + c.MaxSeparation, -edge, edge);
    for (int t = 0; t < c.JumpFrames; t++)
    {
     scene.Step(new FighterInput(InputBits.Up), new FighterInput(InputBits.Up));
     for (int i = 0; i < 2; i++)
     {
      var skeleton = scene.ModelOf(i)!.Skeleton;
      for (int bone = 0; bone < skeleton.GetBoneCount(); bone++)
      {
       // Skeleton plus 15cm for the visible hair/ears; inspect mesh silhouettes in captures too.
       Vector3 top = skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(bone).Origin + Vector3.Up * 0.15f;
       float y = scene.Camera.UnprojectPosition(top).Y * 1080 / scene.GetViewport().GetVisibleRect().Size.Y;
       Assert.True(y > 142, $"fighter {i}, corner {x}, jump {t}, bone {bone} clears HUD ({y})");
      }
     }
    }
   }
  }
  finally { scene.Free(); }
 }

 [Test]
 public static void FloatingHud_StaysInTopStrip_AndTextScalesTo720(Node runner)
 {
  var hud = Load(runner);
  try
  {
   hud.Refresh(new Match());
   Assert.True(hud.GetNode("Strip").GetNodeOrNull("P1Frame") == null
    && hud.GetNode("Strip").GetNodeOrNull("P2Frame") == null, "no paper backing");
   foreach (Node node in hud.GetNode("Strip").FindChildren("*", "Control", true, false))
   {
    if (node is not Control c) continue;
    var r = c.GetGlobalRect();
    bool bottom = r.Position.Y > 500; // YOK-74: the twist/Tanuki strip and the copy toast live in the bottom 100 px, never in the gameplay plane
    if (bottom) Assert.True(r.Position.Y >= 980 && r.End.Y <= 1080, $"{c.Name} stays in the bottom band");
    else Assert.True(r.Position.Y >= 0 && r.End.Y <= 138, $"{c.Name} stays in reserved strip");
    Assert.True(r.Position.X >= 0 && r.End.X <= 1920, $"{c.Name} fits design canvas");
    if (c is Label label)
    {
     Assert.True(label.GetThemeFontSize("font_size") * 720f / 1080 >= 17, $"{c.Name} readable at 720p");
     bool onPaper = false; // labels on the talisman or twist strip paper need no outline; everything else keeps the ink outline
    for (Node up = c.GetParent(); up != null && !onPaper; up = up.GetParent()) onPaper = up is CopyTalisman || up.Name == "TwistStrip";
    Assert.True(onPaper || label.GetThemeConstant("outline_size") >= 6, $"{c.Name}: ink outline over the stage (paper-backed labels need none)");
    }
   }
   var talisman = hud.CopyTarget;
   Assert.True(talisman.DrawnBounds.End.Y <= 138, $"talisman glow, seal and shadow end at {talisman.DrawnBounds.End.Y}, within the strip");
   Assert.True(talisman.DrawnBounds.Position.Y >= 0, "and start on screen");
   Assert.True(talisman.DrawnBounds.Position.X >= 884 && talisman.DrawnBounds.End.X <= 1160, "and stay in the top-centre gap between the panels");
  }
  finally { hud.Free(); }
 }
 [Test]
 public static void ResultCards_ClearEveryHudControl(Node runner)
 {
  var hud = Load(runner);
  try
  {
   foreach (string path in new[] { "lose_screen", "demo_complete" })
   {
    var card = GD.Load<PackedScene>($"res://scenes/ui/{path}.tscn").Instantiate<Control>();
    runner.AddChild(card);
    try
    {
     card.Size = new Vector2(1920, 1080);
     var panel = Find<Control>(card, "Panel");
     var mask = Find<Control>(card, "Mask");
     foreach (Node child in hud.GetNode("Strip").GetChildren())
      if (child is Control c)
      {
       Assert.True(!c.GetGlobalRect().Grow(4).Intersects(panel.GetGlobalRect()), $"{path} paper clears {c.Name}");
       Assert.True(!c.GetGlobalRect().Grow(4).Intersects(mask.GetGlobalRect()), $"{path} mask clears {c.Name}");
      }
    }
    finally { card.Free(); }
   }
  }
  finally { hud.Free(); }
 }


 // ---- YOK-74: copy-target talisman and elder-twist / Tanuki strip ----

 /// <summary>The slot shows the name in capitals; a name too long for it is hyphenated and ends in an ellipsis, so compare letters only.</summary>
 static bool IsNameOrItsFit(string full, string shown)
 {
  static string Letters(string t) { var sb = new System.Text.StringBuilder(); foreach (char ch in t) if (char.IsLetterOrDigit(ch)) sb.Append(char.ToUpperInvariant(ch)); return sb.ToString(); }
  if (!shown.Contains('…') && !shown.Contains('\n')) return shown == full.ToUpperInvariant(); // nothing was cut: the exact name
  string l = Letters(shown);
  return l.Length >= 4 && Letters(full).StartsWith(l);
 }

 sealed class FakeRunView : IHudRunView
 {
  public bool CopyLocked { get; set; }
  public CopyTarget? Copy { get; set; }
  public bool InTanukiFight { get; set; }
  public bool ElderFight { get; set; }
 }

 static string Root => System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");

 const string DenMap = """
 {"kind":"run_map","id":"hud-copy","schema_version":1,
  "nodes":[{"id":"a","row":1,"type":"duel","yokai":"kitsune"},{"id":"den","row":2,"type":"boss","yokai":"mujina"}],
  "routes":[{"from":"a","to":"den"}]}
 """;
 const string DenScenes = """
 {"kind":"node_scenes","id":"hud-copy-scenes","schema_version":1,"default_scene":"res://scenes/fight.tscn","fallback_fighter":"kitsune",
  "tiers":{"yokai":{"reaction_frames":22}},"types":{"duel":{"tier":"yokai"},"boss":{"tier":"yokai"}},"opponents":{"kitsune":{"fighter":"kitsune"}}}
 """;

 [Test]
 public static void CopyTarget_NoRun_IsHidden(Node runner)
 {
  var hud = Load(runner);
  try
  {
   hud.RunView = new SessionHudRunView(null);
   hud.Refresh(new Match());
   Assert.True(!hud.CopyTarget.Visible, "AM6, T6: no run, no talisman");
   Assert.True(!hud.TwistStripVisible, "no strip without a run");
  }
  finally { hud.Free(); }
 }

 [Test]
 public static void CopyTarget_DefinedLevelsUpAndChanges_FromTheRunState(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var run = RunState.NewRun(AbilityPool.LoadFixtures(Root));
   var view = new FakeRunView { InTanukiFight = true };
   hud.RunView = view;
   void Show() { view.Copy = CopyRule.Target(run); hud.Refresh(new Match()); }
   hud.RunView = new FakeRunView { InTanukiFight = true, Copy = new CopyTarget(SpecialSlot.A, Named("Spirit Wave"), 1) };
   hud.Refresh(new Match());
   Assert.Equal("SPIRIT WAVE", hud.CopyTarget.MoveName, "a short name is shown exactly");
   hud.RunView = view; hud.CopyTarget.Reset();
   Show();
   var t = hud.CopyTarget;
   Assert.Equal(CopyTalismanState.Defined, t.State, "defined from the start (T2/T5)");
   Assert.True(IsNameOrItsFit(CopyRule.Target(run)!.Data.Name, t.MoveName), "move name: " + t.MoveName);
   Assert.Equal("Lv 1", t.LevelText, "level");
   Assert.Equal("COPY TARGET", t.CaptionText, "caption");
   Assert.Equal("", t.LastEvent, "nothing animates on the first show");
   run.LevelUp(SpecialSlot.B); // another special overtakes it (T2)
   Show();
   Assert.Equal("changed", t.LastEvent, "target changed");
   Assert.True(t.ToastText.StartsWith("Copy target: ") && t.ToastText.EndsWith("Lv 2"), "toast names it: " + t.ToastText);
   run.LevelUp(SpecialSlot.B);
   Show();
   Assert.Equal("levelled", t.LastEvent, "same move gains a level");
   Assert.Equal("Lv 3", t.LevelText, "level ticks");
   Show();
   Assert.Equal("", t.LastEvent, "an unchanged target stays quiet");
   Assert.True(t.DrawnBounds.Position.Y >= 0 && t.DrawnBounds.End.Y <= 138 && t.DrawnBounds.Position.X >= 884 && t.DrawnBounds.End.X <= 1160, "talisman stays in the top-centre gap, above the gameplay plane");
  }
  finally { hud.Free(); }
 }

 [Test]
 public static void CopyTarget_LockedAtTheTanukiNode_AndTanukiFightLine_ReadFromTheSession(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var s = RunSession.Start(AbilityPool.LoadFixtures(Root), MapLoader.Parse(DenMap), NodeSceneTable.Parse(DenScenes), 74);
   hud.RunView = new SessionHudRunView(s);
   Assert.True(s.Enter("a").Ok, "enter a");
   s.Run.LevelUp(SpecialSlot.B); s.Run.LevelUp(SpecialSlot.B); // Lv 3: the copy runs the Lv 2 tuning only (T4)
   hud.Refresh(new Match());
   Assert.True(!hud.CopyTarget.Visible, "AM6: a yokai duel before the lock shows no talisman");
   Assert.True(!hud.TwistStripVisible, "a plain duel has no strip");
   s.WinFight(700); s.CompleteNode();
   Assert.True(s.Enter("den").Ok, "enter the Tanuki's node");
   hud.Refresh(new Match());
   var t = hud.CopyTarget;
   Assert.Equal(CopyTalismanState.Locked, t.State, "T1: locked");
   Assert.Equal("LOCKED", t.CaptionText, "caption");
   var locked = s.LockedCopy!;
   Assert.True(IsNameOrItsFit(locked.Data.Name, t.MoveName), "shows the locked copy: " + t.MoveName);
   Assert.True(hud.TwistStripVisible, "Tanuki fight line");
   Assert.Equal("THE TANUKI COPIES", hud.TwistKicker, "kicker");
   Assert.Equal(3, locked.Level, "the locked special is Lv 3");
   Assert.Equal($"{locked.Data.Name} at Lv 2, no modifiers", hud.TwistLine, "T4 line: never the evolution");
   Assert.Equal("Lv 3", t.LevelText, "the talisman shows Ryo's level");
   ulong before = s.StateHash();
   s.Run.LevelUp(SpecialSlot.A); s.Run.LevelUp(SpecialSlot.A); // Ryo keeps levelling: the lock does not move
   ulong afterChange = s.StateHash();
   hud.Refresh(new Match());
   Assert.True(IsNameOrItsFit(locked.Data.Name, t.MoveName), "locked copy unchanged by later level-ups");
   Assert.Equal("", t.LastEvent, "no change toast after the lock");
   Assert.Equal(afterChange, s.StateHash(), "the HUD reads only; it changes nothing");
   Assert.True(before != afterChange, "(the probe did change Ryo)");
  }
  finally { hud.Free(); }
 }

 [Test]
 public static void ElderStrip_ShowsOnlyInElderFights_WithPlaceholderText(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var view = new FakeRunView { ElderFight = true };
   hud.RunView = view;
   hud.Refresh(new Match());
   Assert.True(hud.TwistStripVisible, "elder fight shows the reminder");
   Assert.Equal("ELDER TWIST", hud.TwistKicker, "kicker");
   Assert.Equal(FightHud.TwistPlaceholder, hud.TwistLine, "placeholder until YOK-90");
   view.ElderFight = false;
   hud.Refresh(new Match());
   Assert.True(!hud.TwistStripVisible, "hidden in other fights");
  }
  finally { hud.Free(); }
 }

 static readonly string[] RuleNames = { "Spirit Wave", "Rising Talisman", "Foxfire", "Fox Mirage", "Iron-Club Charge", "Oni Quake", "River Grab", "Shell Spin" };

 /// <summary>A special that only carries a name: the fixture's Spirit Wave frame data under another label.</summary>
 static SpecialData Named(string name)
 {
  string fd = AbilityPool.LoadFixtures(Root).Special("test-spirit-wave")!.Level1Json;
  return SpecialLoader.Parse("{\"kind\":\"special\",\"id\":\"hud-name\",\"name\":\"" + name + "\",\"input\":{\"kata\":{\"slot\":\"A\"}},\"levels\":{\"1\":{\"frame_data\":" + fd + "}}}");
 }

 [Test]
 public static void CopyTarget_EveryAbilityName_FitsTwoLinesAtTheReadableSize(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var pool = AbilityPool.LoadRepo(Root);
   var names = new System.Collections.Generic.List<string>(RuleNames);
   foreach (var sp in pool.Specials) names.Add(sp.Name);
   names.AddRange(new[] { "Thunderstrike", "Wandering Thunderstrike", "Supercalifragilistic", "Tsukuyomi-Barrage" }); // longer than any shipped name
   var label = Find<Label>(hud, "MoveName");
   var font = label.GetThemeFont("font");
   foreach (string n in names)
   {
    hud.RunView = new FakeRunView { InTanukiFight = true, Copy = new CopyTarget(SpecialSlot.A, Named(n), 1) };
    hud.Refresh(new Match());
    Assert.Equal(CopyTalisman.NameSize, label.GetThemeFontSize("font_size"), $"{n}: kept at the 720p floor size");
    Assert.True(label.GetLineCount() <= 2, $"{n}: wraps to {label.GetLineCount()} lines");
    Assert.True(label.Text.Length > 0 && label.Text.Split('\n').Length <= CopyTalisman.NameMaxLines, $"{n}: at most two paragraphs");
    foreach (string w in label.Text.Replace("-", "- ").Split(new[] { ' ', '\n' }, System.StringSplitOptions.RemoveEmptyEntries))
     Assert.True(font.GetStringSize(w.TrimEnd(), fontSize: CopyTalisman.NameSize).X <= label.Size.X, $"{n}: '{w}' fits the slot width");
   }
  }
  finally { hud.Free(); }
 }

 [Test]
 public static void CopyTarget_YokaiFightLevelUpsAndRestart_StayHiddenWithNoToast(Node runner)
 {
  var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
  scene.ExternalDrive = true;
  runner.AddChild(scene);
  try
  {
   var hud = scene.GetNode<FightHud>("Hud");
   scene.Step(FighterInput.None, FighterInput.None);
   var t = hud.CopyTarget;
   Assert.True(!t.Visible, "AM6, T6: the default view over a yokai fight hides the talisman");
   scene.DemoRun.LevelUp(SpecialSlot.B);
   scene.Step(FighterInput.None, FighterInput.None);
   scene.DemoRun.LevelUp(SpecialSlot.B);
   scene.Step(FighterInput.None, FighterInput.None);
   Assert.True(!t.Visible, "still hidden after level-ups");
   Assert.Equal("", t.ToastText, "no 'copy target changed' toast in a yokai fight");
   Assert.Equal("", t.LastEvent, "nothing animates");
   scene.RestartRun();
   scene.Step(FighterInput.None, FighterInput.None);
   Assert.True(!t.Visible && t.ToastText == "", "a restart changes nothing");
  }
  finally { scene.Free(); }
 }

 // AM6, T6: the HUD's copy-target talisman shows only in the Tanuki fight.
 [Test]
 public static void CopyTarget_HiddenInAYokaiFight_EvenWithATarget(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var copy = CopyRule.Target(RunState.NewRun(AbilityPool.LoadFixtures(Root)));
   hud.RunView = new FakeRunView { Copy = copy };
   hud.Refresh(new Match());
   Assert.True(!hud.CopyTarget.Visible, "AM6: a yokai fight shows no copy target");
   Assert.True(!hud.TwistStripVisible, "and no strip");
  }
  finally { hud.Free(); }
 }

 [Test]
 public static void CopyTarget_HiddenInAnElderFight_EvenWithATarget(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var copy = CopyRule.Target(RunState.NewRun(AbilityPool.LoadFixtures(Root)));
   hud.RunView = new FakeRunView { ElderFight = true, Copy = copy };
   hud.Refresh(new Match());
   Assert.True(!hud.CopyTarget.Visible, "AM6: an elder fight shows no copy target");
   Assert.True(hud.TwistStripVisible && hud.TwistKicker == "ELDER TWIST", "the elder twist reminder (Y3) still shows");
  }
  finally { hud.Free(); }
 }

 [Test]
 public static void CopyTarget_ShownLockedInTheTanukiFight(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var copy = CopyRule.Target(RunState.NewRun(AbilityPool.LoadFixtures(Root)))!;
   hud.RunView = new FakeRunView { InTanukiFight = true, CopyLocked = true, Copy = copy };
   hud.Refresh(new Match());
   Assert.True(hud.CopyTarget.Visible, "AM6: shown in the Tanuki fight");
   Assert.Equal(CopyTalismanState.Locked, hud.CopyTarget.State, "T1: the locked copy");
   Assert.True(IsNameOrItsFit(copy.Data.Name, hud.CopyTarget.MoveName), "names the locked move: " + hud.CopyTarget.MoveName);
   Assert.Equal($"Lv {copy.Level}", hud.CopyTarget.LevelText, "its level");
   // leaving the Tanuki fight hides it again
   hud.RunView = new FakeRunView { Copy = copy };
   hud.Refresh(new Match());
   Assert.True(!hud.CopyTarget.Visible, "hidden once the fight is not the Tanuki's");
  }
  finally { hud.Free(); }
 }

 [Test]
 public static void CopyTarget_HiddenWithANullRun(Node runner)
 {
  var hud = Load(runner);
  try
  {
   hud.RunView = new SessionHudRunView(null);
   hud.Refresh(new Match());
   Assert.True(!hud.CopyTarget.Visible, "no run: no talisman, no placeholder");
   hud.RunView = null;
   hud.Refresh(new Match());
   Assert.True(!hud.CopyTarget.Visible, "no view at all: no talisman");
  }
  finally { hud.Free(); }
 }

 [Test]
 public static void CopyTarget_ToastIsClearedWhenTheLockArrives(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var run = RunState.NewRun(AbilityPool.LoadFixtures(Root));
   var view = new FakeRunView { InTanukiFight = true, Copy = CopyRule.Target(run) };
   hud.RunView = view;
   hud.Refresh(new Match());
   run.LevelUp(SpecialSlot.B); view.Copy = CopyRule.Target(run);
   hud.Refresh(new Match());
   Assert.True(hud.CopyTarget.ToastText.Length > 0, "toast up");
   view.CopyLocked = true;
   hud.Refresh(new Match());
   Assert.Equal("", hud.CopyTarget.ToastText, "locked: no stale toast");
  }
  finally { hud.Free(); }
 }

 [Test]
 public static void TanukiFight_NoLockedCopy_HidesTheStrip_AndTheTanukiOpponentHasNoPortrait(Node runner)
 {
  string original = FightScene.Opponent;
  var hud = Load(runner);
  try
  {
   FightScene.Opponent = "tanuki";
   var view = new FakeRunView { InTanukiFight = true, CopyLocked = true, Copy = null };
   hud.RunView = view;
   hud.Refresh(new Match());
   Assert.True(!hud.TwistStripVisible, "no locked copy: no empty 'copies' line");
   Assert.Equal(CopyTalismanState.None, hud.CopyTarget.State, "grey fallback");
   Assert.True(!Find<TextureRect>(hud, "P2Portrait").Visible, "no Tanuki portrait");
   view.Copy = CopyRule.Target(RunState.NewRun(AbilityPool.LoadFixtures(Root)));
   hud.Refresh(new Match());
   Assert.True(hud.TwistStripVisible, "locked copy: the strip says what it copies");
   Assert.True(!Find<TextureRect>(hud, "P2Portrait").Visible, "still no Tanuki portrait");
  }
  finally { FightScene.Opponent = original; hud.Free(); }
 }

 [Test]
 public static void ElderFight_ReadFromARealSession(Node runner)
 {
  const string map = """
  {"kind":"run_map","id":"hud-elder","schema_version":1,
   "nodes":[{"id":"e","row":1,"type":"elder","yokai":"kitsune","elder":true},{"id":"d","row":2,"type":"duel","yokai":"kitsune"}],
   "routes":[{"from":"e","to":"d"}]}
  """;
  const string table = """
  {"kind":"node_scenes","id":"hud-elder-scenes","schema_version":1,"default_scene":"res://scenes/fight.tscn","fallback_fighter":"kitsune",
   "tiers":{"yokai":{"reaction_frames":22}},"types":{"duel":{"tier":"yokai"},"elder":{"tier":"yokai"}},"opponents":{"kitsune":{"fighter":"kitsune"}}}
  """;
  var hud = Load(runner);
  try
  {
   var s = RunSession.Start(AbilityPool.LoadFixtures(Root), MapLoader.Parse(map), NodeSceneTable.Parse(table), 75);
   var view = new SessionHudRunView(s);
   Assert.True(!view.ElderFight, "no node entered");
   Assert.True(s.Enter("e").Ok, "enter the elder");
   Assert.True(view.ElderFight && !view.InTanukiFight, "elder node");
   hud.RunView = view;
   hud.Refresh(new Match());
   Assert.True(hud.TwistStripVisible, "strip from the real session");
   s.WinFight(700); s.CompleteNode(); s.Enter("d");
   hud.Refresh(new Match());
   Assert.True(!hud.TwistStripVisible, "a plain duel hides it");
  }
  finally { hud.Free(); }
 }

 // YOK-121: the bottom strip is a 10 s reminder (600 ticks), then it is gone.
 [Test]
 public static void TwistStrip_ShowsFor600Ticks_ThenHides_ForElderAndTanuki(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var copy = CopyRule.Target(RunState.NewRun(AbilityPool.LoadFixtures(Root)));
   foreach (var view in new FakeRunView[] { new() { ElderFight = true }, new() { InTanukiFight = true, Copy = copy } })
   {
    hud.RunView = view;
    var m = new Match();
    hud.Refresh(m);
    Assert.True(hud.TwistStripVisible && hud.TwistStripAlpha >= 1f, "visible and opaque at fight start");
    for (int i = 0; i < FightHud.TwistStripTicks - FightHud.TwistFadeTicks; i++) m.Step(FighterInput.None, FighterInput.None);
    hud.Refresh(m);
    Assert.True(hud.TwistStripVisible && hud.TwistStripAlpha >= 1f, "still opaque until the fade starts");
    for (int i = 0; i < FightHud.TwistFadeTicks / 2; i++) m.Step(FighterInput.None, FighterInput.None);
    hud.Refresh(m);
    Assert.True(hud.TwistStripVisible && hud.TwistStripAlpha > 0f && hud.TwistStripAlpha < 1f, "fading near the end of the window");
    for (int i = 0; i < FightHud.TwistFadeTicks / 2 - 1; i++) m.Step(FighterInput.None, FighterInput.None);
    hud.Refresh(m);
    Assert.Equal(FightHud.TwistStripTicks - 1, m.Tick, "tick 599");
    Assert.True(hud.TwistStripVisible, "last visible tick is 599");
    m.Step(FighterInput.None, FighterInput.None);
    hud.Refresh(m);
    Assert.True(!hud.TwistStripVisible, "hidden from tick 600 (10 s)");
    hud.Refresh(m);
    Assert.True(!hud.TwistStripVisible, "stays hidden");
    m.Reset();
    hud.Refresh(m);
    Assert.True(hud.TwistStripVisible && hud.TwistStripAlpha >= 1f, "a restart (tick 0) shows it for a fresh 10 s");
    Assert.True(hud.TwistLine != "", "text is set while shown");
   }
  }
  finally { hud.Free(); }
 }

 // YOK-121: the pause freezes the sim tick, so the 10 s window does not drain while paused; a restart shows it again.
 [Test]
 public static void TwistStrip_TimerPausesWithTheGamePause_AndRestartShowsItAgain(Node runner)
 {
  var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
  scene.PauseEnabled = true;
  scene.ExternalDrive = true;
  runner.AddChild(scene);
  try
  {
   var hud = scene.GetNode<FightHud>("Hud");
   hud.RunView = new FakeRunView { ElderFight = true };
   for (int i = 0; i < 100; i++) scene.Step(FighterInput.None, FighterInput.None);
   Assert.True(hud.TwistStripVisible, "visible mid-window");
   Assert.True(scene.TryPause(), "pause opens");
   int tick = scene.Match.Tick;
   for (int i = 0; i < 700; i++) scene.Step(FighterInput.None, FighterInput.None); // a paused scene ignores steps
   Assert.Equal(tick, scene.Match.Tick, "no tick passes while paused");
   Assert.True(hud.TwistStripVisible, "paused for longer than 10 s, the strip is still up");
   scene.PauseMenu!.Resume();
   for (int i = 0; i < FightHud.TwistStripTicks; i++) scene.Step(FighterInput.None, FighterInput.None);
   Assert.True(!hud.TwistStripVisible, "after 10 s of unpaused fight it is gone");
   scene.RestartRun();
   scene.Step(FighterInput.None, FighterInput.None);
   Assert.True(hud.TwistStripVisible, "restart shows it again");
  }
  finally { scene.Free(); }
 }

 [Test]
 public static void FitName_ZeroWidthLabel_Terminates(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var label = Find<Label>(hud, "MoveName");
   label.Size = Vector2.Zero;
   hud.RunView = new FakeRunView { InTanukiFight = true, Copy = new CopyTarget(SpecialSlot.A, Named("Thunderstrike Wave"), 1) };
   hud.Refresh(new Match()); // must return
   Assert.True(hud.CopyTarget.MoveName.Length > 0, "still shows a name");
  }
  finally { hud.Free(); }
 }
}
