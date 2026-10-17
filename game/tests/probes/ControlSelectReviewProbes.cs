using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Tests;

public static class ControlSelectReviewProbes
{
    [Test]
    public static void SelectionBrushAndRadioSurviveFocusAndRepeatedSchemeChanges(Node runner)
    {
        // V1: the selected scheme uses its paper/brush presentation; K1/K2: the choice identifies the active scheme.
        MenuSettings.Reset();
        var screen = GD.Load<PackedScene>("res://scenes/ui/start_screen.tscn").Instantiate<StartScreen>();
        runner.AddChild(screen);
        try
        {
            foreach (var scheme in new[] { ControlScheme.Kata, ControlScheme.Kihon, ControlScheme.Kata, ControlScheme.Kihon })
            {
                MenuSettings.SetScheme(scheme);
                var selected = scheme == ControlScheme.Kata ? screen.KataButton : screen.KihonButton;
                var other = scheme == ControlScheme.Kata ? screen.KihonButton : screen.KataButton;
                var selectedStyle = selected.GetThemeStylebox("normal");
                foreach (var focus in new[] { selected, other, screen.Start, selected })
                {
                    focus.GrabFocus();
                    ((KitBrushButton)selected).Refresh();
                    ((KitBrushButton)other).Refresh();
                    Assert.True(selected.GetNode<Control>("Selected").Visible, "brush remains on selected scheme");
                    Assert.True(!other.GetNode<Control>("Selected").Visible, "focus does not mark the other scheme selected");
                    Assert.True(selected.GetNode<Control>("Radio/Dot").Visible && !other.GetNode<Control>("Radio/Dot").Visible, "radio follows scheme, not focus");
                    Assert.True(selected.GetThemeStylebox("normal") == selectedStyle, "focus refresh retains screen-owned selected style");
                    Assert.Equal(100f, selected.GetThemeStylebox("normal").ContentMarginLeft, "selected text inset survives focus");
                }
            }
        }
        finally { screen.Free(); MenuSettings.Reset(); }
    }

    [Test]
    public static void ReferenceRepeatedOpenCloseRestoresActionsAndPreservesHiddenSchemeChoice(Node runner)
    {
        // V1: paper controls remain usable around the reference sheet; K1/K2: reference reflects the chosen scheme.
        MenuSettings.Reset();
        var screen = GD.Load<PackedScene>("res://scenes/ui/start_screen.tscn").Instantiate<StartScreen>();
        runner.AddChild(screen);
        try
        {
            var reference = UiFind.Get<Control>(screen, "Reference");
            var toggle = UiFind.Get<Button>(screen, "ReferenceToggle");
            var startPosition = screen.Start.Position;
            var togglePosition = toggle.Position;
            for (int i = 0; i < 3; i++)
            {
                screen.SetReferenceVisible(true);
                screen.SetReferenceVisible(true);
                Assert.True(screen.ReferenceVisible, "reference opens idempotently");
                Assert.True(screen.Start.Position.Y >= reference.Position.Y + reference.Size.Y, "Start stays below reference sheet");
                Assert.True(toggle.Position.Y >= reference.Position.Y + reference.Size.Y, "reference toggle stays below sheet");
                Assert.True(!screen.KataButton.Visible && !screen.KihonButton.Visible, "sheet hides scheme cards");
                MenuSettings.SetScheme(ControlScheme.Kata);
                Assert.Equal(StartScreen.KataDescription, UiFind.Get<Label>(screen, "SpecialsLine").Text, "hidden-card choice redraws Kata reference");
                screen.SetReferenceVisible(false);
                screen.SetReferenceVisible(false);
                Assert.Equal(startPosition, screen.Start.Position, "Start restores original position without drift");
                Assert.Equal(togglePosition, toggle.Position, "toggle restores original position without drift");
                Assert.True(screen.KataButton.Visible && screen.KihonButton.Visible, "both cards restored");
                Assert.True(screen.KataButton.GetNode<Control>("Radio/Dot").Visible, "scheme choice persists across reference close");
                MenuSettings.SetScheme(ControlScheme.Kihon);
            }
        }
        finally { screen.Free(); MenuSettings.Reset(); }
    }
}
