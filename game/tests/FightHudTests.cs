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
   foreach (Node child in hud.GetNode("Strip").GetChildren())
   {
    if (child is not Control c) continue;
    // YOK-74: the twist/Tanuki strip and the copy toast live in the bottom 100 px, never in the gameplay plane.
    if (c.Name == "TwistStrip" || c.Name == "Toast") { Assert.True(c.Position.Y >= 980 && c.Position.Y + c.Size.Y <= 1080, $"{c.Name} stays in the bottom band"); continue; }
    Assert.True(c.Position.Y >= 0 && c.Position.Y + c.Size.Y <= 138, $"{c.Name} stays in reserved strip");
    Assert.True(c.Position.X >= 0 && c.Position.X + c.Size.X <= 1920, $"{c.Name} fits design canvas");
    if (c is Label label)
    {
     Assert.True(label.GetThemeFontSize("font_size") * 720f / 1080 >= 17, "readable at 720p");
     Assert.True(label.GetThemeConstant("outline_size") >= 6, "ink outline over the stage");
    }
   }
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
 public static void CopyTarget_NoRun_ShowsTheGreyFallback(Node runner)
 {
  var hud = Load(runner);
  try
  {
   hud.RunView = new SessionHudRunView(null);
   hud.Refresh(new Match());
   Assert.Equal(CopyTalismanState.None, hud.CopyTarget.State, "no run, no target");
   Assert.Equal("-", hud.CopyTarget.MoveName, "dash");
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
   var view = new FakeRunView();
   hud.RunView = view;
   void Show() { view.Copy = CopyRule.Target(run); hud.Refresh(new Match()); }
   Show();
   var t = hud.CopyTarget;
   Assert.Equal(CopyTalismanState.Defined, t.State, "defined from the start (T2/T5)");
   Assert.Equal(CopyRule.Target(run)!.Data.Name.ToUpperInvariant(), t.MoveName, "move name");
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
   Assert.True(t.Position.Y + t.Size.Y + 4 <= 135 && t.Position.X >= 884 && t.Position.X + t.Size.X <= 1100, "talisman stays in the top-centre gap, above the gameplay plane");
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
   s.Run.LevelUp(SpecialSlot.B);
   hud.Refresh(new Match());
   Assert.Equal(CopyTalismanState.Defined, hud.CopyTarget.State, "before the lock");
   Assert.True(!hud.TwistStripVisible, "a plain duel has no strip");
   s.WinFight(700); s.CompleteNode();
   Assert.True(s.Enter("den").Ok, "enter the Tanuki's node");
   hud.Refresh(new Match());
   var t = hud.CopyTarget;
   Assert.Equal(CopyTalismanState.Locked, t.State, "T1: locked");
   Assert.Equal("LOCKED", t.CaptionText, "caption");
   var locked = s.LockedCopy!;
   Assert.Equal(locked.Data.Name.ToUpperInvariant(), t.MoveName, "shows the locked copy");
   Assert.True(hud.TwistStripVisible, "Tanuki fight line");
   Assert.Equal("THE TANUKI COPIES", hud.TwistKicker, "kicker");
   Assert.Equal($"{locked.Data.Name} at Lv {locked.MoveLevel}, no modifiers", hud.TwistLine, "T4 line");
   ulong before = s.StateHash();
   s.Run.LevelUp(SpecialSlot.A); s.Run.LevelUp(SpecialSlot.A); // Ryo keeps levelling: the lock does not move
   ulong afterChange = s.StateHash();
   hud.Refresh(new Match());
   Assert.Equal(locked.Data.Name.ToUpperInvariant(), t.MoveName, "locked copy unchanged by later level-ups");
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
}
