using Godot;
using YokaiFighters.Ui;
using YokaiFighters.Ui.Kit;
using YokaiFighters.Sim;
namespace YokaiFighters.Tests;
public static class SettingsLayoutTests
{
 [Test]
 public static void Settings_InteractiveRectsStayInsidePaperSafeArea(Node host)
 {
  var s = GD.Load<PackedScene>("res://scenes/ui/settings_panel.tscn").Instantiate<SettingsPanel>();
  host.AddChild(s); s.Size = new Vector2(1920,1080); s.Show();
  try {
   foreach (var scale in new[] {1f, 2f/3})
   foreach (var scheme in new[] {ControlScheme.Kata,ControlScheme.Kihon})
   foreach (var tab in new[] {SettingsTab.Sound,SettingsTab.Controls}) {
    s.Scale = Vector2.One * scale; MenuSettings.SetScheme(scheme); s.ShowTab(tab);
    for(int i=0;i<4;i++) KitGallery.ForceLayout(s);
    // The canvas stretches uniformly; apply the same transform to its safe inset.
    var paper = UiFind.Get<Control>(s,"Panel").GetGlobalRect();
    var safe = s.SheetSafeArea;
    void Walk(Node n) {
     if(n is Control c && !c.IsVisibleInTree()) return;
     if(n is Control c2 && (c2 is BaseButton || c2 is Range || c2.FocusMode != Control.FocusModeEnum.None))
      Assert.True(safe.Grow(1).Encloses(c2.GetGlobalRect()), $"{tab}/{scheme}/{scale}: {c2.Name} inside safe paper {c2.GetGlobalRect()} / {safe}");
     foreach(var child in n.GetChildren()) Walk(child);
    }
    Walk(s);
    var footer = UiFind.Get<Control>(s, "Footer").GetGlobalRect();
    Assert.True(footer.Position.Y >= paper.End.Y + 16 * scale, "footer clears the torn bottom edge");
    Assert.True(footer.End.Y <= 1080 * scale, "footer stays on screen");
    var scroll = UiFind.Get<ScrollContainer>(s,"BindingsScroll");
    Assert.True(scroll.Size.Y >= 360, "bindings have room for five rows plus heading");
    if (tab == SettingsTab.Controls)
     Assert.True(UiFind.Get<Control>(s, "Tables").Size.Y > scroll.Size.Y,"bindings scroll instead of shrink");
   }
  } finally { s.Free(); MenuSettings.Reset(); }
 }
}
