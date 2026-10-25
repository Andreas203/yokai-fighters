using Godot;
using YokaiFighters.Ui;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Tests;

public static class PauseLayoutTests
{
	[Test]
	public static void PauseCards_IdentityFallbackEmptyAndModifierFit(Node host)
	{
		var p = GD.Load<PackedScene>("res://scenes/ui/pause_menu.tscn").Instantiate<PauseMenu>();
		host.AddChild(p);
		p.FreezeTree = false;
		p.Size = new(1920, 1080);
		p.SetRunSummary(new(new("RYO", 720, 1000), new("Kitsune", 600, 1000), "Duel 1 of 2 · vs Kitsune",
			new PauseSlotSummary[] {
				new("A", "Foxfire", 1, "Will-o'-wisp", "foxfire"),
				new("B", "Spirit Wave", 3, "Fox's Patience", "spirit-wave"),
				new("C", "Rising Talisman", 2, "", "rising-talisman"),
				new("D", "Empty", 0, "") }));
		p.Open();
		for (int i = 0; i < 3; i++) KitGallery.ForceLayout(p);
		var slots = UiFind.Get<VBoxContainer>(p, "Slots");
		Assert.Equal("72%", UiFind.Get<Label>(p, "RunPercent").Text, "health percentage");
		Assert.True(p.GetNode("Backdrop") is ColorRect { Color.A: > 0 and < 1 }, "live fight remains visible under dim");
		Assert.True(p.FindChild("OpponentBar", true, false) == null, "concept shows only Ryo");
		for (int i = 0; i < 4; i++)
		{
			var card = slots.GetChild<Control>(i);
			var icon = UiFind.Get<TextureRect>(card, "MoveIcon");
			Assert.Equal(i < 3, icon.Visible, "empty slot has no false move icon");
			Assert.True(new Rect2(0, 0, 1920, 980).Encloses(card.GetGlobalRect()), "all four cards clear footer");
			foreach (string label in new[] { "MoveName", "Modifier" })
			{
				var text = UiFind.Get<Label>(card, label);
				if (!text.Visible) continue;
				Assert.True(card.GetGlobalRect().Encloses(text.GetGlobalRect()), "copy stays on paper");
				Assert.True(text.GetMinimumSize().Y <= text.Size.Y && text.GetMinimumSize().X <= text.Size.X, "copy fits");
				Assert.True(text.GetThemeFontSize("font_size") * 2f / 3 >= 17, "720p text size");
			}
		}
		Assert.Equal(AbilityArt.ForId("foxfire"), UiFind.Get<TextureRect>(slots.GetChild(0), "MoveIcon").Texture, "A can show Foxfire");
		Assert.Equal(AbilityArt.ForId("spirit-wave"), UiFind.Get<TextureRect>(slots.GetChild(1), "MoveIcon").Texture, "B can show Spirit Wave");
		Assert.Equal(AbilityArt.ForId(null), AbilityArt.ForId("unmapped"), "unknown identity fallback");
		p.SetRunSummary(new(new("RYO", 0, 0), new("Other", 1, 1), "Caller position",
			new[] { new PauseSlotSummary("A", "Unknown move", 1, "") }));
		Assert.Equal("—", UiFind.Get<Label>(p, "RunPercent").Text, "zero max has no invalid percentage");
		Assert.Equal(1, slots.GetChildCount(), "rebind removes old cards");
		Assert.Equal(AbilityArt.ForId(null), UiFind.Get<TextureRect>(slots.GetChild(0), "MoveIcon").Texture, "legacy caller gets generic icon");
		p.Close(); p.QueueFree();
	}
}
