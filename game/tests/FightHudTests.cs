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
 public static void JumpEnvelope_ClearsPlates_AtBothCornersAndMidStage(Node runner)
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
       Assert.True(y > 238, $"fighter {i}, corner {x}, jump {t}, bone {bone} clears HUD ({y})");
      }
     }
    }
   }
  }
  finally { scene.Free(); }
 }

 [Test]
 public static void Plates_StayInTopStrip_AndTextScalesTo720(Node runner)
 {
  var hud = Load(runner);
  try
  {
   hud.Refresh(new Match());
   foreach (Node child in hud.GetNode("Strip").GetChildren())
   {
    if (child is not Control c) continue;
    Assert.True(c.Position.Y >= 0 && c.Position.Y + c.Size.Y <= 238, $"{c.Name} stays in reserved strip");
    Assert.True(c.Position.X >= 0 && c.Position.X + c.Size.X <= 1920, $"{c.Name} fits design canvas");
    if (c is Label label) Assert.True(label.GetThemeFontSize("font_size") * 720f / 1080 >= 17, "readable at 720p");
   }
  }
  finally { hud.Free(); }
 }
}
