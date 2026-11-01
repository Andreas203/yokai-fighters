using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;
using YokaiFighters.Ui.Kit;
using CardKind = YokaiFighters.Ui.CardKind;
using CardView = YokaiFighters.Ui.CardView;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-70 layout probes (test-breaker). Presentation only: the expectations are geometric claims from the ticket
/// (no rule ID applies; A12 plain-language card text must stay readable). Real card text from data/ is bound on purpose,
/// because the claim is about the longest real text, not about fixtures.
/// </summary>
public static class LayoutFixProbes
{
	static AbilityPool Pool => FightScene.LoadAbilityPool();

	static void Lay(Control c) { for (int i = 0; i < 4; i++) KitGallery.ForceLayout(c); }

	static Rect2 Shrink(Rect2 r, float l, float t, float rt, float b) => new(r.Position + new Vector2(l, t), r.Size - new Vector2(l + rt, t + b));

	// Real run: every special owned at level 3, the longest-text modifier on every slot that accepts it.
	static RunState FullRun(AbilityPool pool)
	{
		var run = RunState.NewRun(pool);
		foreach (var s in pool.Specials) if (run.SpecialCount < 4 && !run.Owns(s.Id)) run.Equip(s);
		foreach (var (slot, _) in run.Owned.ToList()) while (run.CanLevelUp(slot)) run.LevelUp(slot);
		var mod = pool.Modifiers.OrderByDescending(m => m.CardPlain.Length + m.Name.Length).First();
		foreach (var (slot, _) in run.Owned.ToList()) if (run.CanAttach(slot, mod)) run.Attach(slot, mod);
		return run;
	}

	// ---- move list ----

	// Claim: card content margins 44/36/44/44 keep the copy inside the frame (rice-card texture margin 28).
	[Test]
	public static void MoveCard_RealLongestText_StaysInsideContentMargins(Node host)
	{
		MenuSettings.Reset();
		var pool = Pool;
		var view = MoveListSource.Build(FullRun(pool), ContentPaths.Data("moves"));
		var longest = new SlotRow("A", false, pool.Specials.OrderByDescending(s => s.Name.Length).First().Name, 3, 3,
			"Down, down-forward, forward", "Special + neutral",
			string.Join(" ", pool.Specials.OrderByDescending(s => s.CardPlain.Length).Take(2).Select(s => s.CardPlain)),
			string.Join("\n", view.Slots.Select(s => s.Frames)),
			pool.Modifiers.OrderByDescending(m => m.Name.Length).First().Name,
			string.Join(" ", pool.Modifiers.Select(m => m.CardPlain)));
		var p = GD.Load<PackedScene>("res://scenes/ui/move_list_panel.tscn").Instantiate<MoveListPanel>();
		host.AddChild(p); p.Size = new(1920, 1080); p.Visible = true;
		foreach (bool frames in new[] { false, true })
		{
			p.Bind(new MoveListView(view.Base, view.Slots.Append(longest).ToArray()));
			if (p.FrameDataVisible != frames) p.ToggleFrameData();
			Lay(p);
			for (int i = 0; i < p.SlotCards.Count; i++)
			{
				var card = p.SlotCards[i];
				var inner = Shrink(card.GetGlobalRect(), 44, 36, 44, 44);
				foreach (var l in card.GetNode("Box").GetChildren().OfType<Label>().Where(l => l.Visible && l.Text.Length > 0))
					Assert.True(inner.Encloses(l.GetGlobalRect()),
						$"card {i} frames={frames} label {l.Name} {l.GetGlobalRect()} outside content area {inner}");
				var box = (Control)card.GetNode("Box");
				Assert.True(card.Size.Y >= card.GetCombinedMinimumSize().Y - 0.01f, $"card {i} frames={frames} size {card.Size.Y} < its min {card.GetCombinedMinimumSize().Y}");
				var vis = box.GetChildren().OfType<Control>().Where(c => c.Visible).ToList();
				float need = vis.Sum(c => c is Label l ? l.GetMinimumSize().Y : c.GetCombinedMinimumSize().Y) + 10 * (vis.Count - 1);
				Assert.True(need <= card.Size.Y - 80 + 0.01f, $"card {i} frames={frames} content needs {need} but card gives {card.Size.Y - 80} inside 36/44 margins");
			}
		}
		MenuSettings.Reset(); p.QueueFree();
	}

	// Claim: both scroll areas end at 915, above the footer (947).
	[Test]
	public static void MoveList_ScrollAreasEndAboveFooter(Node host)
	{
		var p = GD.Load<PackedScene>("res://scenes/ui/move_list_panel.tscn").Instantiate<MoveListPanel>();
		host.AddChild(p); p.Size = new(1920, 1080); p.Visible = true;
		p.Bind(MoveListSource.Build(FullRun(Pool), ContentPaths.Data("moves")));
		Lay(p);
		float footerTop = UiFind.Get<Control>(p, "Footer").GetGlobalRect().Position.Y;
		foreach (string n in new[] { "BaseRowsScroll", "SpecialCardsScroll" })
		{
			var s = UiFind.Get<Control>(p, n).GetGlobalRect();
			Assert.True(s.End.Y <= footerTop, $"{n} bottom {s.End.Y} reaches footer top {footerTop}");
			Assert.True(s.End.Y <= UiFind.Get<Control>(p, "Panel").GetGlobalRect().Position.Y + 915 + 0.01f, $"{n} bottom {s.End.Y} below panel y 915");
		}
		p.QueueFree();
	}

	// ---- pause ----

	// Claim: four 124 px plates with an 8 px gap fit above the footer at 996, modifier line inside each plate.
	[Test]
	public static void Pause_FourPlatesWithRealModifierLines_ClearFooter(Node host)
	{
		var pool = Pool;
		var run = FullRun(pool);
		var p = GD.Load<PackedScene>("res://scenes/ui/pause_menu.tscn").Instantiate<PauseMenu>();
		host.AddChild(p); p.FreezeTree = false; p.Size = new(1920, 1080);
		string longName = pool.Specials.OrderByDescending(s => s.Name.Length).First().Name;
		string longMod = pool.Modifiers.OrderByDescending(m => m.Name.Length).First().Name;
		p.SetRunSummary(new(new("RYO", 500, 1000), new("Kitsune", 500, 1000), "Duel 1 of 2 · vs Kitsune",
			new[] { "A", "B", "C", "D" }.Select(l => new PauseSlotSummary(l, longName, 3, longMod)).ToArray()));
		p.Open(); Lay(p);
		var slots = UiFind.Get<VBoxContainer>(p, "Slots");
		float footerTop = UiFind.Get<Control>(p, "Footer").GetGlobalRect().Position.Y;
		Assert.Equal(8, slots.GetThemeConstant("separation"), "gap");
		for (int i = 0; i < 4; i++)
		{
			var card = slots.GetChild<Control>(i);
			Assert.Equal(124f, card.Size.Y, $"plate {i} height");
			if (i > 0) Assert.Equal(8f, card.GetGlobalRect().Position.Y - slots.GetChild<Control>(i - 1).GetGlobalRect().End.Y, "gap between plates");
			var name = UiFind.Get<Label>(card, "MoveName"); var mod = UiFind.Get<Label>(card, "Modifier");
			Assert.True(mod.Visible, "modifier line visible");
			Assert.True(card.GetGlobalRect().Encloses(mod.GetGlobalRect()), $"plate {i} modifier line outside plate");
			Assert.True(name.GetMinimumSize().Y <= name.Size.Y, $"plate {i} name '{name.Text}' wraps to {name.GetMinimumSize().Y} > {name.Size.Y}, runs into modifier line");
			Assert.True(mod.GetMinimumSize().Y <= mod.Size.Y && mod.GetMinimumSize().X <= mod.Size.X, $"plate {i} modifier '{mod.Text}' min {mod.GetMinimumSize()} > {mod.Size}");
			Assert.True(name.GetGlobalRect().End.Y <= mod.GetGlobalRect().Position.Y, $"plate {i} name overlaps modifier line");
		}
		Assert.True(slots.GetChild<Control>(3).GetGlobalRect().End.Y <= footerTop, $"4th plate bottom {slots.GetChild<Control>(3).GetGlobalRect().End.Y} overlaps footer {footerTop}");
		p.Close(); p.QueueFree();
	}

	// ---- lose ----

	// Claim: lose panel top (y 165) clears the HUD plates (y 130), including the mask that sticks out of it.
	[Test]
	public static void Lose_PanelAndMask_ClearHudPlates(Node host)
	{
		var hud = GD.Load<PackedScene>("res://scenes/ui/hud.tscn").Instantiate();
		host.AddChild(hud);
		var lose = UiFind.Get<LoseScreen>(hud, "LoseScreen");
		var (title, text) = FightScene.LoadStory().LoseScreen(YokaiFighters.Story.StoryLibrary.DisplayName(FightScene.Opponent));
		lose.SetText(title, text); lose.Open();
		Assert.Equal(new Vector2(1920, 1080), hud.GetViewport().GetVisibleRect().Size, "design viewport");
		Lay(lose);
		float plateBottom = new[] { "P1Portrait", "P2Portrait", "P1Health", "P2Health", "P1HealthText", "P2HealthText", "Meter1", "Temperament" }
			.Select(n => UiFind.Get<Control>(hud, n)).Where(c => c.IsVisibleInTree()).Max(c => c.GetGlobalRect().End.Y);
		var panel = UiFind.Get<Control>(lose, "Panel");
		Assert.True(panel.GetGlobalRect().Position.Y >= 130f, $"lose panel top {panel.GetGlobalRect().Position.Y} above HUD plate bottom 130");
		var mask = UiFind.Get<Control>(lose, "Mask");
		Assert.True(mask.GetGlobalRect().Position.Y >= UiFind.Get<Control>(hud, "P1Portrait").GetGlobalRect().End.Y,
			$"mask top {mask.GetGlobalRect().Position.Y} overlaps HUD portrait bottom {UiFind.Get<Control>(hud, "P1Portrait").GetGlobalRect().End.Y}");
		Assert.True(panel.GetGlobalRect().Position.Y >= plateBottom - 0.01f || plateBottom <= 130.5f, $"HUD content reaches {plateBottom}");
		var sub = UiFind.Get<Label>(lose, "Sub");
		Assert.True(panel.GetGlobalRect().Encloses(sub.GetGlobalRect()), $"story text {sub.GetGlobalRect()} outside panel {panel.GetGlobalRect()}");
		Assert.True(sub.GetMinimumSize().Y <= sub.Size.Y + 0.01f, $"real lose text needs {sub.GetMinimumSize().Y}, box {sub.Size.Y}");
		hud.QueueFree();
	}

	// ---- reward card ----

	static CardView Longest(AbilityPool pool, CardKind kind)
	{
		string plain = kind == CardKind.Modifier ? pool.Modifiers.OrderByDescending(m => m.CardPlain.Length).First().CardPlain
			: pool.Specials.OrderByDescending(s => s.CardPlain.Length).First().CardPlain;
		string name = kind == CardKind.Modifier ? pool.Modifiers.OrderByDescending(m => m.Name.Length).First().Name
			: pool.Specials.OrderByDescending(s => s.Name.Length).First().Name;
		return new CardView(kind, kind == CardKind.Modifier ? "fox-patience" : "foxfire", name, "Kitsune - " + kind, plain, "frames", "TAG",
			kind == CardKind.Modifier ? "Pick the special to carry it" : null);
	}

	// Claim: selected cards don't lift; names share one baseline.
	[Test]
	public static void RewardCard_SelectedAndUnselected_ShareNameBaseline(Node host)
	{
		var pool = Pool;
		foreach (var kind in new[] { CardKind.NewMove, CardKind.Upgrade, CardKind.Modifier })
			foreach (bool frames in new[] { false, true })
			{
				var a = GD.Load<PackedScene>("res://scenes/ui/reward_card.tscn").Instantiate<RewardCard>();
				var b = GD.Load<PackedScene>("res://scenes/ui/reward_card.tscn").Instantiate<RewardCard>();
				host.AddChild(a); host.AddChild(b);
				a.Bind(Longest(pool, kind)); b.Bind(Longest(pool, kind));
				a.SetState(false, frames); b.SetState(true, frames);
				Lay(a); Lay(b);
				Assert.Equal(UiFind.Get<Label>(a, "Name").GetGlobalRect().Position.Y, UiFind.Get<Label>(b, "Name").GetGlobalRect().Position.Y, $"{kind} frames={frames} name y");
				Assert.Equal(UiFind.Get<Control>(a, "Body").GetGlobalRect().Position.Y, UiFind.Get<Control>(b, "Body").GetGlobalRect().Position.Y, $"{kind} frames={frames} body y");
				a.QueueFree(); b.QueueFree();
			}
	}

	// Claim: text runs x 40-410 inside the card (selection frame patch margin 32), real longest text fits its box.
	[Test]
	public static void RewardCard_RealLongestPlain_InsideFrameAndBody(Node host)
	{
		var pool = Pool;
		foreach (var kind in new[] { CardKind.NewMove, CardKind.Modifier })
		{
			var c = GD.Load<PackedScene>("res://scenes/ui/reward_card.tscn").Instantiate<RewardCard>();
			host.AddChild(c); c.Bind(Longest(pool, kind)); c.SetState(true, false); Lay(c);
			var body = UiFind.Get<Control>(c, "Body").GetGlobalRect();
			var inner = Shrink(body, 32, 32, 32, 32);
			var plain = UiFind.Get<Label>(c, "Plain"); var prompt = UiFind.Get<Label>(c, "TargetPrompt");
			Assert.True(inner.Encloses(plain.GetGlobalRect()), $"{kind} Plain {plain.GetGlobalRect()} touches selection frame {inner}");
			Assert.True(plain.GetMinimumSize().Y <= plain.Size.Y + 0.01f, $"{kind} plain text needs {plain.GetMinimumSize().Y}, box {plain.Size.Y}");
			if (prompt.Visible)
			{
				Assert.True(prompt.GetGlobalRect().Position.X >= inner.Position.X && prompt.GetGlobalRect().End.X <= inner.End.X && body.Encloses(prompt.GetGlobalRect()), $"{kind} prompt {prompt.GetGlobalRect()} outside {inner}");
				Assert.True(prompt.GetMinimumSize().Y <= prompt.Size.Y + 0.01f, $"{kind} prompt needs {prompt.GetMinimumSize().Y}, box {prompt.Size.Y}");
				Assert.True(plain.GetGlobalRect().End.Y <= prompt.GetGlobalRect().Position.Y, $"{kind} plain overlaps prompt");
			}
			var name = UiFind.Get<Label>(c, "Name");
			Assert.True(name.GetGlobalRect().Position.X >= inner.Position.X && name.GetGlobalRect().Position.X + name.GetMinimumSize().X <= inner.End.X && name.GetMinimumSize().Y <= name.Size.Y + 0.01f, $"{kind} name '{name.Text}' needs {name.GetMinimumSize().Y} in {name.Size.Y}");
			c.QueueFree();
		}
	}

	// ---- control select ----

	// Claim: descriptions end at 1250; the selection frame sits 6 px inside the card.
	[Test]
	public static void ControlSelect_DescriptionsAndFrameInsideCards(Node host)
	{
		var s = GD.Load<PackedScene>("res://scenes/ui/start_screen.tscn").Instantiate<StartScreen>();
		host.AddChild(s); s.Size = new(1920, 1080); s.Open(); Lay(s);
		foreach (var (btn, desc) in new[] { ("Kata", "KataDescription"), ("Kihon", "KihonDescription") })
		{
			var card = UiFind.Get<Control>(s, btn).GetGlobalRect();
			var frame = UiFind.Get<Control>(s, btn).GetNode<Control>("Selected").GetGlobalRect();
			var d = UiFind.Get<Label>(s, desc);
			Assert.True(Shrink(card, 6, 6, 6, 6).Encloses(frame), $"{btn} frame {frame} not 6 px inside card {card}");
			Assert.True(d.GetGlobalRect().End.X <= card.Position.X + 1250f - 285f + 0.01f || d.GetGlobalRect().End.X <= card.End.X - 100f, $"{btn} description ends at {d.GetGlobalRect().End.X}");
			Assert.True(Shrink(frame, 32, 32, 32, 32).Encloses(d.GetGlobalRect()), $"{btn} description {d.GetGlobalRect()} touches frame patch {frame}");
			Assert.True(d.GetMinimumSize().Y <= d.Size.Y + 0.01f, $"{btn} description '{d.Text}' needs {d.GetMinimumSize().Y}, box {d.Size.Y}");
		}
		s.QueueFree();
	}
}
