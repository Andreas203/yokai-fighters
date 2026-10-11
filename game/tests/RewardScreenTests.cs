using Godot;
using System.IO;
using System.Text.Json;
using YokaiFighters.Ui.Kit;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-52: reward screen against the stub source: cards from data, picks reach the source, toggle, modifier target step.</summary>
public static class RewardScreenTests
{
	static (RewardScreen, StubRewardSource) Make(Node host)
	{
		var src = new StubRewardSource();
		var s = GD.Load<PackedScene>("res://scenes/ui/reward_screen.tscn").Instantiate<RewardScreen>();
		host.AddChild(s);
		s.Present(src);
		return (s, src);
	}

	[Test]
	public static void KitArtAndDeviceChipsFollowData(Node host)
	{
		var (s, _) = Make(host);
		var card = s.GetNode<RewardCard>("Cards/Card1");
		Assert.True(UiFind.Get<TextureRect>(card, "Illustration").Texture == RewardArt.ForId("foxfire"), "id supplies art");
		card.Bind(new CardView(CardKind.Modifier, "fox-patience", "Fox's Patience", "Kitsune - modifier", "From source", "Live frames", "MODIFIER"));
		card.SetState(true, false);
		Assert.True(UiFind.Get<SealStamp>(card, "Fallback").Visible, "unmapped id gets letter, not previous art");
		Assert.Equal("F", UiFind.Get<SealStamp>(card, "Fallback").Glyph, "live initial");
		s.ToggleFrameData();
		Assert.True(UiFind.Get<Control>(card, "FrameBox").Visible && !UiFind.Get<Control>(card, "Fallback").Visible, "frames replace illustration");
		Assert.Equal("Live frames", UiFind.Get<Label>(card, "FrameText").Text, "source frame text");
		InputGlyphs.Set(InputDevice.Keyboard);
		Assert.Equal("TAB", UiFind.Get<KeyChipFooter>(s, "FrameHint").ChipTexts()[0], "keyboard toggle chip");
		Assert.Equal(Control.MouseFilterEnum.Ignore, UiFind.Get<KeyChipFooter>(s, "FrameHint").GetNode<Control>("Chip0").GetMouseFilterWithOverride(), "chip lets click reach frame button");
		InputGlyphs.Set(InputDevice.Pad);
		Assert.Equal("Y", UiFind.Get<KeyChipFooter>(s, "FrameHint").ChipTexts()[0], "pad toggle chip");
		s.Move(2); s.Confirm();
		Assert.Equal("D-PAD|A|B", string.Join("|", UiFind.Get<KeyChipFooter>(s, "HintTarget").ChipTexts()), "target chips");
		var target = s.GetNode<TargetCard>("Targets/Target2");
		Assert.Equal("Needs a projectile", UiFind.Get<Label>(target, "Note").Text, "source rejection reason");
		Assert.True(UiFind.Get<Panel>(target, "Body").SelfModulate.R < 1, "ineligible slot greyed");
		Assert.Equal("+", UiFind.Get<SealStamp>(s.GetNode<TargetCard>("Targets/Target3"), "Fallback").Glyph, "empty slot mark");
		InputGlyphs.Set(InputDevice.Keyboard);
		s.Free();
	}

	[Test]
	public static void AllAuthoredCardCopyFitsReservedAreas(Node host)
	{
		var (s, _) = Make(host);
		var card = s.GetNode<RewardCard>("Cards/Card1");
		string root = Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
		int count = 0;
		void Check(JsonElement value, string id, string name)
		{
			if (value.ValueKind == JsonValueKind.Object)
			{
				if (value.TryGetProperty("plain", out var plain))
				{
					card.Bind(new CardView(CardKind.Modifier, id, name, "Kitsune - modifier", plain.GetString()!, "", "UPGRADE - Lv 2 to 3", "Pick the special to carry it"));
					foreach (var area in new[] { ("Name", 89f), ("Source", 44f), ("Tag", 48f), ("Plain", 152f), ("TargetPrompt", 48f) })
					{
						var label = UiFind.Get<Label>(card, area.Item1);
						float height = label.GetLineCount() * label.GetLineHeight() + (label.GetLineCount() - 1) * label.GetThemeConstant("line_spacing");
						Assert.True(height <= area.Item2, $"{id} {area.Item1} needs {height}px, reserved {area.Item2}");
						Assert.True(label.GetThemeFontSize("font_size") * 2f / 3 >= 17, "720p minimum text");
					}
					count++;
				}
				foreach (var prop in value.EnumerateObject()) Check(prop.Value, id, name);
			}
			else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) Check(item, id, name);
		}
		foreach (string dir in new[] { "moves", "modifiers", "cards" })
		{
			string path = Path.Combine(root, "data", dir);
			if (!Directory.Exists(path)) continue;
			foreach (string file in Directory.GetFiles(path, "*.json", SearchOption.AllDirectories))
			{
				using var doc = JsonDocument.Parse(File.ReadAllText(file));
				Check(doc.RootElement, doc.RootElement.GetProperty("id").GetString()!, doc.RootElement.GetProperty("name").GetString()!);
			}
		}
		Assert.True(count >= 11, "base, level and modifier copy checked");
		var pool = Sim.AbilityPool.LoadRepo(root);
		var run = Sim.RunState.NewRun(pool);
		for (uint seed = 0; seed < 16; seed++)
		{
			var source = new DraftRewardSource(Sim.RewardDraft.Build(run, pool, "kitsune", seed), run);
			foreach (var view in source.Offer.Cards)
			{
				card.Bind(view); card.SetState(true, true);
				var frames = UiFind.Get<Label>(card, "FrameText");
				float height = frames.GetLineCount() * frames.GetLineHeight() + (frames.GetLineCount() - 1) * frames.GetThemeConstant("line_spacing");
				Assert.True(height <= 164, $"{view.Id} actual draft frame text fits without scrolling ({height})");
			}
		}
		s.Free();
	}

	[Test]
	public static void NewCardPickReturnsIndex(Node host)
	{
		var (s, src) = Make(host);
		(int, string?)? got = null;
		s.Picked += (c, t) => got = (c, t);
		s.Confirm();
		Assert.Equal((0, (string?)null), src.LastPick!.Value, "source got pick");
		Assert.Equal((0, (string?)null), got!.Value, "signal");
		Assert.True(!s.Showing, "hidden after pick");
		s.QueueFree();
	}

	[Test]
	public static void ModifierNeedsEligibleTarget(Node host)
	{
		var (s, src) = Make(host);
		s.Move(2); s.Confirm();
		Assert.Equal(RewardStep.Target, s.Step, "opens target step");
		Assert.True(src.LastPick == null, "no pick yet");
		s.Move(1); s.Confirm(); // slot B is not eligible
		Assert.True(src.LastPick == null, "ineligible refused");
		s.Back();
		Assert.Equal(RewardStep.Cards, s.Step, "back");
		s.Confirm(); s.Confirm();
		Assert.Equal((2, (string?)"spirit-wave"), src.LastPick!.Value, "modifier + target");
		s.QueueFree();
	}

	[Test]
	public static void FrameDataToggle(Node host)
	{
		var (s, _) = Make(host);
		Assert.True(!s.FrameDataVisible, "off by default");
		s.ToggleFrameData();
		Assert.True(s.FrameDataVisible, "on");
		s.ToggleFrameData();
		Assert.True(!s.FrameDataVisible, "off");
		s.QueueFree();
	}

	[Test]
	public static void BindingLineTemplate()
	{
		Assert.Equal("Forgive me, Kitsune. I'll set your spirit free.", new StubRewardSource().BindingLine("Kitsune"), "S3 line");
	}
}
