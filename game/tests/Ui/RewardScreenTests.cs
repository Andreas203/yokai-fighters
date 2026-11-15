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
		Assert.True(UiFind.Get<TextureRect>(card, "Illustration").Texture == AbilityArt.ForId("foxfire"), "id supplies art");
		card.Bind(new CardView(CardKind.Modifier, "fox-patience", "Fox's Patience", "Kitsune - modifier", "From source", "Live frames", "MODIFIER"));
		card.SetState(true, false);
		var modArt = UiFind.Get<TextureRect>(card, "Illustration").Texture;
		Assert.True(modArt == AbilityArt.ForId("fox-patience"), "modifier card gets its own art (its icon, or the generic diamond when no file), not previous art");
		Assert.True(modArt != AbilityArt.ForId("foxfire"), "not the previous card's art");
		Assert.Equal("F", UiFind.Get<SealStamp>(card, "Fallback").Glyph, "live initial");
		s.ToggleFrameData();
		Assert.True(UiFind.Get<Control>(card, "FrameBox").Visible && !UiFind.Get<Control>(card, "Illustration").Visible, "frames replace illustration");
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

	/// <summary>A source whose new special would replace Rising Talisman (slot B): the designer's 2026-11-15 confirm step.</summary>
	sealed class ReplacingSource : IRewardSource
	{
		public (int Card, string? Target)? LastPick { get; private set; }
		public RewardOffer Offer { get; } = new("Oni", new[]
		{
			new CardView(CardKind.NewMove, "iron-club-charge", "Iron-Club Charge", "Oni - special", "Charge forward club first.", "12/6/14", "NEW - Lv 1")
				{ Replaces = new TargetView("rising-talisman", "B", "Rising Talisman", 2, null, true) { Note = "Loses its levels" } },
			new CardView(CardKind.NewMove, "foxfire", "Foxfire", "Kitsune - special", "A slow flame.", "15/4/30", "NEW - Lv 1"),
		}, new[] { new TargetView("spirit-wave", "A", "Spirit Wave", 1, null, true) });
		public void Pick(int cardIndex, string? targetId) => LastPick = (cardIndex, targetId);
		public void Reset() => LastPick = null;
	}

	[Test]
	public static void ReplacingSpecial_AsksFirst_BackKeepsTheOldMove_ConfirmReplaces(Node host)
	{
		var src = new ReplacingSource();
		var s = GD.Load<PackedScene>("res://scenes/ui/reward_screen.tscn").Instantiate<RewardScreen>();
		host.AddChild(s);
		try
		{
			s.Present(src);
			var card = s.GetNode<RewardCard>("Cards/Card1");
			var prompt = UiFind.Get<Label>(card, "TargetPrompt");
			Assert.True(prompt.Visible && prompt.Text == "Replaces Rising Talisman (slot B)", $"the card warns before the pick: '{prompt.Text}'");
			Assert.True(!UiFind.Get<Label>(s.GetNode<RewardCard>("Cards/Card2"), "TargetPrompt").Visible, "a new special for an empty slot has no warning");
			s.Confirm();
			Assert.True(src.LastPick is null && s.Showing, "the first press does not replace anything");
			Assert.Equal(RewardStep.Replace, s.Step, "it opens the replace step");
			Assert.Equal("Replace Rising Talisman?", UiFind.Get<Label>(s, "Title").Text, "the question");
			Assert.True(UiFind.Get<Label>(s, "TargetHeading").Text.Contains("levels are lost", System.StringComparison.Ordinal), "the cost is spelled out (A6)");
			var target = s.GetNode<TargetCard>("Targets/Target1");
			Assert.True(target.Visible && !s.GetNode<TargetCard>("Targets/Target2").Visible, "only the special being replaced shows");
			Assert.Equal("Rising Talisman", UiFind.Get<Label>(target, "Name").Text, "its name");
			Assert.Equal("Loses its levels", UiFind.Get<Label>(target, "Note").Text, "its note");
			s.Move(1);
			Assert.Equal(RewardStep.Replace, s.Step, "left/right does nothing here");
			s.Back();
			Assert.True(s.Step == RewardStep.Cards && s.Showing && src.LastPick is null, "Back returns to the cards with nothing replaced");
			s.Move(1); s.Confirm();
			Assert.True(src.LastPick == (1, null), "an empty-slot special is taken on the first press, as before");
			s.Present(src);
			s.Confirm(); s.Confirm();
			Assert.True(src.LastPick == (0, null) && !s.Showing, "confirming the replace step takes the card");
			// Mouse (code-critic 2026-11-15): a right-click backs out, a click on the shown special replaces it.
			src.Reset();
			s.Present(src);
			s.Confirm();
			s._UnhandledInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Right });
			Assert.True(s.Step == RewardStep.Cards && s.Showing && src.LastPick is null, "right-click backs out of the replace step");
			s.Confirm();
			var click = new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left };
			s.GetNode<TargetCard>("Targets/Target1").EmitSignal(Control.SignalName.GuiInput, click);
			Assert.True(src.LastPick == (0, null) && !s.Showing, "clicking the replaced special's card replaces it");
		}
		finally { s.Free(); }
	}

	[Test]
	public static void Adapter_NamesTheSpecialANewCardWouldReplace()
	{
		string root = Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
		var pool = YokaiFighters.Sim.AbilityPool.LoadRepo(root);
		int replacing = 0, empty = 0;
		foreach (string yokai in new[] { "kitsune", "oni", "kappa" })
			for (uint seed = 1; seed <= 12; seed++)
			{
				var run = YokaiFighters.Sim.RunState.NewRun(pool);
				if (seed % 2 == 0) run.Equip(pool.Special("foxfire")!); // half the runs hold a drafted slot-C special, as after a Kitsune duel
				var draft = YokaiFighters.Sim.RewardDraft.Build(run, pool, yokai, seed);
				var src = new DraftRewardSource(draft, run);
				for (int i = 0; i < draft.Offer.Cards.Count; i++)
				{
					var sim = draft.Offer.Cards[i];
					if (sim.Kind != YokaiFighters.Sim.CardKind.NewSpecial) { Assert.True(src.Offer.Cards[i].Replaces is null, $"{sim.Name}: only a new special can replace"); continue; }
					var owned = run[sim.Targets[0].Slot];
					var r = src.Offer.Cards[i].Replaces;
					if (owned is null) { empty++; Assert.True(r is null, $"{sim.Name}: its slot is empty, nothing replaced"); }
					else { replacing++; Assert.True(r is not null && r.Id == owned.Data.Id && r.Level == owned.Level, $"{sim.Name}: replaces {owned.Data.Name}"); }
				}
			}
		Assert.True(replacing > 0 && empty > 0, $"offers both replace a held special and fill an empty slot ({replacing} / {empty})");
	}
}
