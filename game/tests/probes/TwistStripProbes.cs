using System.Collections.Generic;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-121 test-breaker probes: the 10 s scroll strip (Y3, T6, AM6).</summary>
public static class TwistStripProbes
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

 // Y3/T6: alpha stays in [0,1] and never rises as ticks pass, from 0 to well past 600; visible iff tick < 600.
 [Test]
 public static void Alpha_InRangeAndMonotonic_AcrossEveryTick(Node runner)
 {
  var hud = Load(runner);
  try
  {
   hud.RunView = new View { ElderFight = true };
   var m = new Match();
   float prev = 2f;
   for (int i = 0; i < 700; i++)
   {
    hud.Refresh(m);
    float a = hud.TwistStripAlpha;
    Assert.True(a >= 0f && a <= 1f, $"alpha {a} at tick {m.Tick}");
    Assert.True(a <= prev, $"alpha rose at tick {m.Tick}");
    Assert.True(hud.TwistStripVisible == (m.Tick < 600), $"visibility at tick {m.Tick}");
    prev = a;
    m.Step(FighterInput.None, FighterInput.None);
   }
  }
  finally { hud.Free(); }
 }

 // AM6/T6: the top talisman stays up in the Tanuki fight after the strip is gone; strip never shows in a yokai fight or with no run.
 [Test]
 public static void Talisman_UnaffectedByStripTimer_AndNoStripInOtherFights(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var copy = CopyRule.Target(RunState.NewRun(AbilityPool.LoadFixtures(Root)));
   hud.RunView = new View { InTanukiFight = true, Copy = copy };
   var m = new Match();
   for (int i = 0; i < 650; i++) m.Step(FighterInput.None, FighterInput.None);
   hud.Refresh(m);
   Assert.True(!hud.TwistStripVisible, "strip gone at 650");
   Assert.True(hud.CopyTarget.Visible, "talisman still shown at 650");
   Assert.True(hud.CopyTarget.Modulate.A >= 1f, "talisman not faded by strip timer");
   foreach (IHudRunView? v in new IHudRunView?[] { null, new View(), new View { Copy = copy } })
   {
    hud.RunView = v;
    hud.Refresh(new Match());
    Assert.True(!hud.TwistStripVisible, "no strip in yokai fight / no run / tanuki with nothing locked");
   }
  }
  finally { hud.Free(); }
 }

 // Determinism: the HUD reads the sim, never writes it. Same inputs with and without a HUD refresh give the same hash.
 [Test]
 public static void StateHash_SameWithAndWithoutHud(Node runner)
 {
  var hud = Load(runner);
  try
  {
   hud.RunView = new View { ElderFight = true };
   var a = new Match(); var b = new Match();
   for (int i = 0; i < 640; i++)
   {
    a.Step(FighterInput.None, FighterInput.None);
    b.Step(FighterInput.None, FighterInput.None);
    hud.Refresh(b);
   }
   Assert.Equal(a.StateHash(), b.StateHash(), "hash");
  }
  finally { hud.Free(); }
 }

 // Swapping to a fresh Match mid-way (new fight) shows the strip again at tick 0 with the same HUD node.
 [Test]
 public static void NewMatchObject_AfterLateTick_ShowsStripAgain(Node runner)
 {
  var hud = Load(runner);
  try
  {
   hud.RunView = new View { ElderFight = true };
   var m = new Match();
   for (int i = 0; i < 620; i++) m.Step(FighterInput.None, FighterInput.None);
   hud.Refresh(m);
   Assert.True(!hud.TwistStripVisible, "hidden");
   hud.Refresh(new Match());
   Assert.True(hud.TwistStripVisible && hud.TwistStripAlpha >= 1f, "fresh match shows");
  }
  finally { hud.Free(); }
 }

 // Text fits the scroll: every special name in data, as the Tanuki line at Lv 3, laid out at the label font
 // must fit the label's width (830) and the label must sit inside the strip.
 [Test]
 public static void TanukiLine_LongestDataName_FitsTheScroll(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var strip = UiFind.Get<Control>(hud, "TwistStrip");
   var text = UiFind.Get<Label>(hud, "TwistText");
   var kick = UiFind.Get<Label>(hud, "Kicker");
   Assert.True(text.Position.X + text.Size.X <= strip.Size.X - 18 && text.Position.Y + text.Size.Y <= strip.Size.Y, "label inside strip");
   Assert.True(kick.Position.Y + kick.Size.Y <= text.Position.Y + 2, "kicker above text");
   var font = text.GetThemeFont("font"); int size = text.GetThemeFontSize("font_size");
   var worst = new List<string>();
   foreach (var sp in AbilityPool.LoadRepo(Root).Specials)
   {
    string line = $"{sp.Name} at Lv 3, no modifiers";
    float w = font.GetStringSize(line, HorizontalAlignment.Left, -1, size).X;
    if (w > text.Size.X) worst.Add($"{sp.Name}={w}");
   }
   Assert.Equal("", string.Join(",", worst), "lines wider than the label (clipped)");
  }
  finally { hud.Free(); }
 }
}
