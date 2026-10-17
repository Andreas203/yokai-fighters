using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-118 test-breaker probes: AM6 / T6, the copy talisman only in the Tanuki fight.</summary>
public static class CopyTalismanAm6Probes
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
 static void Quiet(FightHud hud, string why)
 {
  var t = hud.CopyTarget;
  Assert.True(!t.Visible, why + ": hidden");
  Assert.Equal("", t.ToastText, why + ": no toast text");
  Assert.Equal("", t.LastEvent, why + ": no event");
  Assert.True(t.Modulate == Colors.White, why + ": no pulse tint");
  Assert.True(UiFind.Get<Label>(hud, "Toast").Modulate.A == 0f, why + ": toast invisible");
 }

 // AM6/T6: leaving the Tanuki fight leaves nothing behind; coming back is a first show (no toast, no pulse).
 [Test]
 public static void ViewCycle_YokaiTanukiYokaiTanuki_LeavesNoToastOrPulse(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var run = RunState.NewRun(AbilityPool.LoadFixtures(Root));
   var view = new View { Copy = CopyRule.Target(run) };
   hud.RunView = view; hud.Refresh(new Match());
   Quiet(hud, "yokai");
   for (int round = 0; round < 2; round++)
   {
    run = RunState.NewRun(AbilityPool.LoadFixtures(Root)); view.Copy = CopyRule.Target(run);
    view.InTanukiFight = true; hud.Refresh(new Match());
    Assert.True(hud.CopyTarget.Visible, "Tanuki fight shows it");
    Assert.Equal("", hud.CopyTarget.ToastText, "re-entry is a first show: no toast");
    Assert.True(hud.CopyTarget.Modulate == Colors.White, "re-entry: no pulse");
    run.LevelUp(SpecialSlot.B); view.Copy = CopyRule.Target(run); hud.Refresh(new Match());
    run.LevelUp(SpecialSlot.B); view.Copy = CopyRule.Target(run); hud.Refresh(new Match());
    view.InTanukiFight = false; hud.Refresh(new Match());
    Quiet(hud, "back to yokai, round " + round);
   }
  }
  finally { hud.Free(); }
 }

 // AM6: a toast in flight (Defined, copy changed) when the view turns non-Tanuki is gone at once.
 [Test]
 public static void ToastInFlight_ThenNonTanuki_IsCleared(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var run = RunState.NewRun(AbilityPool.LoadFixtures(Root));
   var view = new View { InTanukiFight = true, Copy = CopyRule.Target(run) };
   hud.RunView = view; hud.Refresh(new Match());
   run.LevelUp(SpecialSlot.B); view.Copy = CopyRule.Target(run); hud.Refresh(new Match());
   Assert.True(hud.CopyTarget.ToastText.Length > 0, "setup: toast up");
   view.InTanukiFight = false; view.ElderFight = true; hud.Refresh(new Match());
   Quiet(hud, "elder after toast");
  }
  finally { hud.Free(); }
 }

 // AM6: a pulse in flight is cancelled the same way.
 [Test]
 public static void PulseInFlight_ThenNonTanuki_IsCleared(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var run = RunState.NewRun(AbilityPool.LoadFixtures(Root));
   var view = new View { InTanukiFight = true, Copy = CopyRule.Target(run) };
   hud.RunView = view; hud.Refresh(new Match());
   run.LevelUp(SpecialSlot.B); view.Copy = CopyRule.Target(run); hud.Refresh(new Match());
   run.LevelUp(SpecialSlot.B); view.Copy = CopyRule.Target(run); hud.Refresh(new Match());
   Assert.Equal("levelled", hud.CopyTarget.LastEvent, "setup: pulse running");
   view.InTanukiFight = false; hud.Refresh(new Match());
   Quiet(hud, "pulse then yokai");
  }
  finally { hud.Free(); }
 }

 // T6/Y3: InTanukiFight wins over ElderFight on a boss node; elder alone hides it.
 [Test]
 public static void ElderFlagWithTanukiFlag_ShowsTalisman_ElderAlone_Hides(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var run = RunState.NewRun(AbilityPool.LoadFixtures(Root));
   var view = new View { InTanukiFight = true, ElderFight = true, CopyLocked = true, Copy = CopyRule.Target(run) };
   hud.RunView = view; hud.Refresh(new Match());
   Assert.True(hud.CopyTarget.Visible, "boss node: shown");
   Assert.Equal(CopyTalismanState.Locked, hud.CopyTarget.State, "boss node: locked copy");
   Assert.True(hud.TwistStripVisible, "boss node: strip");
   view.InTanukiFight = false; hud.Refresh(new Match());
   Assert.True(!hud.CopyTarget.Visible, "elder only: hidden");
   Assert.True(hud.TwistStripVisible, "elder only: elder strip");
  }
  finally { hud.Free(); }
 }

 // T6: a Tanuki fight with no copy (null) must not throw or show a stale name from the previous fight.
 [Test]
 public static void TanukiFightWithNullCopy_ShowsNoStaleMove(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var run = RunState.NewRun(AbilityPool.LoadFixtures(Root));
   var view = new View { InTanukiFight = true, CopyLocked = true, Copy = CopyRule.Target(run) };
   hud.RunView = view; hud.Refresh(new Match());
   string first = hud.CopyTarget.MoveName;
   view.InTanukiFight = false; hud.Refresh(new Match());
   view.Copy = null; view.InTanukiFight = true; hud.Refresh(new Match());
   Assert.Equal(CopyTalismanState.None, hud.CopyTarget.State, "no copy: empty state, not the old locked one");
   Assert.True(hud.CopyTarget.MoveName != first || first == "-", "old move name not carried over");
   Assert.True(!hud.TwistStripVisible, "no copy: no Tanuki strip");
  }
  finally { hud.Free(); }
 }

 // Visible-but-unlocked boss state (Critic advisory a): Locked state requires CopyLocked (T1).
 [Test]
 public static void TanukiFightUnlocked_IsNotShownAsLocked(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var run = RunState.NewRun(AbilityPool.LoadFixtures(Root));
   hud.RunView = new View { InTanukiFight = true, CopyLocked = false, Copy = CopyRule.Target(run) };
   hud.Refresh(new Match());
   Assert.True(hud.CopyTarget.State != CopyTalismanState.Locked, "T1: only a locked copy gets the LOCKED stamp");
  }
  finally { hud.Free(); }
 }

 // Determinism: the HUD reads only; hash unchanged across view switches.
 [Test]
 public static void ViewSwitches_DoNotTouchStateHashOrRunState(Node runner)
 {
  var hud = Load(runner);
  try
  {
   var run = RunState.NewRun(AbilityPool.LoadFixtures(Root));
   var m = new Match();
   ulong h = m.StateHash();
   var copy = CopyRule.Target(run);
   var view = new View { Copy = copy };
   hud.RunView = view;
   foreach (bool tan in new[] { false, true, false, true })
   foreach (bool locked in new[] { false, true })
   { view.InTanukiFight = tan; view.CopyLocked = locked; hud.Refresh(m); }
   Assert.True(h == m.StateHash(), "StateHash unchanged");
   Assert.True(copy == CopyRule.Target(run), "run copy unchanged");
  }
  finally { hud.Free(); }
 }
}
