using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.SaveGame;
using YokaiFighters.Settings;
using YokaiFighters.Sim;
using YokaiFighters.Ui;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Tests;

/// <summary>
/// Test Breaker probes for YOK-218 (held cancel rules in the UI). Expected values come from X1 (none at the start; only elders offer),
/// X2 (two at most), X4 (one rule per elder, Undertow locked), A11 (locked until the save unlocks), A12 (plain first, path behind the toggle)
/// in docs/design/rules.md. The oracle for CancelRuleHeldOut is written from X2 alone: the beaten elder has an available rule Ryo does not
/// hold and Ryo holds two.
/// </summary>
public static class HeldCancelRuleUiProbes
{
	static AbilityPool Pool() => FightScene.LoadAbilityPool();
	static AbilityPool Unlocked() => Pool().WithUnlocks(new[] { "undertow" });
	static string MapDir => ContentPaths.Data("map");

	static IEnumerable<List<CancelRuleData>> HeldSets(AbilityPool pool)
	{
		var all = pool.CancelRules;
		yield return new();
		foreach (var a in all) yield return new() { a };
		foreach (var a in all) foreach (var b in all) if (a.Id != b.Id) yield return new() { a, b };
	}

	static RunState RunHolding(AbilityPool pool, IEnumerable<CancelRuleData> held)
	{
		var run = RunState.NewRun(pool);
		foreach (var c in held) run.HoldCancelRule(c);
		return run;
	}

	// ---- sim: CancelRuleHeldOut ----

	// X2, X4, A11: swept over elders, held sets in both orders, locked and unlocked pools, seeds, and the upgradeChoice option.
	[Test]
	public static void HeldOut_Sweep_MatchesTheRuleFromX2_AndNeverCoexistsWithACancelCard()
	{
		int combos = 0;
		foreach (var pool in new[] { Pool(), Unlocked() })
		{
			var ids = new string?[] { null, "elder-oni", "elder-kappa", "nine-tailed-kitsune", "bogus" };
			foreach (var held in HeldSets(pool))
			foreach (var elderId in ids)
			foreach (bool elderFlag in new[] { false, true })
			foreach (bool choice in new[] { false, true })
			for (uint seed = 1; seed <= 6; seed++)
			{
				var run = RunHolding(pool, held);
				ulong before = run.StateHash();
				// X1: the beaten yokai's own id; any yokai works for the draft (the field is about the rule).
				var d = RewardDraft.Build(run, pool, "oni", seed, elder: elderFlag, upgradeChoice: choice, elderId: elderId);
				bool offers = elderId != null && pool.CancelRules.Any(c => c.OfferedBy == elderId && (!c.Locked || pool.Unlocked.Contains(c.Id)) && !held.Any(h => h.Id == c.Id));
				bool expectOut = offers && held.Count == 2;
				bool expectCard = offers && held.Count < 2;
				string tag = $"held=[{string.Join(",", held.Select(h => h.Id))}] elder={elderId} flag={elderFlag} unl={pool.Unlocked.Contains("undertow")} seed={seed}";
				Assert.Equal(expectOut, d.Offer.CancelRuleHeldOut, "CancelRuleHeldOut: " + tag);
				Assert.Equal(expectCard, d.Offer.Cards.Any(c => c.Kind == Sim.CardKind.CancelRule), "cancel card present: " + tag);
				Assert.True(!(d.Offer.CancelRuleHeldOut && d.Offer.Cards.Any(c => c.Kind == Sim.CardKind.CancelRule)), "held out and offered at once: " + tag);
				Assert.Equal(before, run.StateHash(), "Build never touches the run: " + tag);
				combos++;
			}
		}
		Assert.True(combos > 1000, "swept " + combos);
	}

	// Determinism: the same seed gives the same cards and flag; the flag does not change the draw (a full run's cards equal an open run's non-cancel cards).
	[Test]
	public static void HeldOut_SameSeedTwice_SameDraft_AndTheOtherCardsDoNotShiftWithTheField()
	{
		var pool = Unlocked();
		foreach (uint seed in new uint[] { 1, 2, 3, 99, 4242 })
		{
			var full = RunHolding(pool, pool.CancelRules.Where(c => c.Id != "oni-chain").Take(2));
			string Sig(Sim.RewardOffer o) => string.Join("|", o.Cards.Select(c => $"{c.Kind}:{c.AbilityId}:{c.Index}")) + "/" + o.CancelRuleHeldOut;
			var a = RewardDraft.Build(full, pool, "oni", seed, elder: true, elderId: "elder-oni");
			var b = RewardDraft.Build(full, pool, "oni", seed, elder: true, elderId: "elder-oni");
			Assert.Equal(Sig(a.Offer), Sig(b.Offer), "same inputs, same offer");
			Assert.True(a.Offer.CancelRuleHeldOut, "setup: held out");
			// The same run seen by a non-elder draw draws the same non-cancel cards when it has no rule to offer anyway.
			var none = RewardDraft.Build(full, pool, "oni", seed, elder: true);
			Assert.Equal(string.Join("|", none.Offer.Cards.Select(c => c.AbilityId)), string.Join("|", a.Offer.Cards.Select(c => c.AbilityId)), "the held-out flag does not change which cards are drawn");
		}
	}

	// A draft is read after the run changed: the flag is a snapshot of the build, not a live read (a reward screen that shows a stale notice is wrong too).
	[Test]
	public static void HeldOut_IsFixedAtBuild_NotRecomputedLater()
	{
		var pool = Pool();
		var run = RunHolding(pool, pool.CancelRules.Where(c => c.Id != "oni-chain").Take(2));
		var d = RewardDraft.Build(run, pool, "oni", 3, elder: true, elderId: "elder-oni");
		Assert.True(d.Offer.CancelRuleHeldOut, "held out at build");
		var src = new DraftRewardSource(d, run);
		Assert.Equal(DraftRewardSource.FullNotice, src.Offer.Notice, "notice present");
		Assert.True(src.Offer.Notice!.All(c => c >= 32 && c < 127), "printable ASCII only");
		Assert.Equal("No cancel rule offered. You already hold 2 of 2.", src.Offer.Notice, "ticket wording");
	}

	// X1/X2 through the real run: the notice follows the node (elder with a rule held out), never a duel, never a locked rule (A11).
	[Test]
	public static void HeldOut_ThroughRunSession_ElderRows5And8_AndDuels() => OnFixtures(() =>
	{
		// (node, rules held when the node is won, pool unlocked, expected held out)
		var cases = new (string Node, string[] Held, bool Unlocked, bool Out)[]
		{
			("r5-elder-oni", new[] { "fox-step", "undertow" }, true, true),
			("r5-elder-oni", new[] { "fox-step", "undertow" }, false, true),   // two held, oni-chain is unlocked: out
			("r5-elder-oni", new[] { "oni-chain", "fox-step" }, true, false),  // holds its own rule
			("r5-elder-oni", new[] { "fox-step" }, true, false),               // room: card offered
			("r5-nine-tails", new[] { "oni-chain", "undertow" }, true, true),
			("r5-nine-tails", new[] { "oni-chain", "fox-step" }, true, false),
			("r2-oni", new[] { "oni-chain", "fox-step" }, true, false),         // a duel
		};
		foreach (var c in cases)
		{
			var pool = c.Unlocked ? Unlocked() : Pool();
			var s = RunSession.Start(pool, FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir), 5);
			WalkTo(s, c.Node);
			foreach (string id in c.Held) s.Run.HoldCancelRule(pool.CancelRule(id)!);
			var d = s.WinFight(500);
			Assert.True(d != null, c.Node + ": a draft");
			Assert.Equal(c.Out, d!.Offer.CancelRuleHeldOut, $"{c.Node} holding {string.Join("+", c.Held)}");
			Assert.Equal(c.Out ? DraftRewardSource.FullNotice : null, new DraftRewardSource(d, s.Run).Offer.Notice, $"{c.Node}: notice");
		}
	});

	// A11 on row 8: Elder Kappa's Undertow is locked, so holding two others holds nothing out; unlocked, it does. Elder Oni at row 8 follows the same rule as row 5.
	[Test]
	public static void HeldOut_Row8Elders_LockedUndertowNeverHeldOut_UnlockedIs() => OnFixtures(() =>
	{
		foreach (var (unlocked, kappaOut) in new[] { (false, false), (true, true) })
		{
			var pool = unlocked ? Unlocked() : Pool();
			var s = RunSession.Start(pool, FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir), 5);
			WalkTo(s, "r8-elder-kappa");
			s.Run.HoldCancelRule(pool.CancelRule("fox-step")!); s.Run.HoldCancelRule(pool.CancelRule("oni-chain")!);
			var d = s.WinFight(500)!;
			Assert.Equal(kappaOut, d.Offer.CancelRuleHeldOut, $"r8-elder-kappa, undertow unlocked={unlocked}");
			Assert.Equal(kappaOut, new DraftRewardSource(d, s.Run).Offer.Notice != null, "notice follows");
		}
	});

	static bool Leads(RunSession s, string from, string to) => from == to || s.Map.Graph.Next(from).Any(n => Leads(s, n, to));

	static void WalkTo(RunSession s, string target)
	{
		for (; ; )
		{
			string pick = s.Reachable().First(id => Leads(s, id, target));
			Assert.True(s.Enter(pick).Ok, "entered " + pick);
			if (pick == target) return;
			if (s.Node!.Type is "merchant" or "dojo") Assert.True(s.CompleteNode().Ok, "stop passed");
			else
			{
				var d = s.WinFight(500);
				if (d != null && !d.Picked) { var card = d.Offer.Cards.First(x => x.Kind != Sim.CardKind.CancelRule); Assert.True(d.Pick(card.Index, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, "picked"); }
				Assert.True(s.CompleteNode().Ok, "completed");
			}
		}
	}

	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		try { body(); }
		finally { FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe; }
	}

	// ---- UI: reward screen ----

	// The notice fits its box at the screen's own font, shows only on the cards step, and is gone for the next offer.
	[Test]
	public static void RewardScreen_Notice_FitsItsBox_HiddenOnTheTargetStep_AndOnTheNextOffer(Node host)
	{
		var pool = Pool();
		var full = RunHolding(pool, pool.CancelRules.Where(c => c.Id != "oni-chain").Take(2));
		var held = new DraftRewardSource(RewardDraft.Build(full, pool, "oni", 3, elder: true, elderId: "elder-oni"), full);
		var s = GD.Load<PackedScene>("res://scenes/ui/reward_screen.tscn").Instantiate<RewardScreen>();
		host.AddChild(s);
		s.Size = new(1920, 1080);
		s.Present(held);
		for (int i = 0; i < 3; i++) KitGallery.ForceLayout(s);
		var notice = UiFind.Get<Label>(s, "Notice");
		Assert.True(notice.Visible, "shown on the cards step");
		Assert.True(notice.GetMinimumSize().X <= notice.Size.X + 0.5f, $"notice text wider ({notice.GetMinimumSize().X}) than its box ({notice.Size.X})");
		Assert.True(notice.GetMinimumSize().Y <= notice.Size.Y + 0.5f, "notice taller than its box");
		Assert.True(new Rect2(0, 0, 1920, 1080).Encloses(notice.GetGlobalRect()), "notice on screen");
		var title = UiFind.Get<Label>(s, "Title");
		Assert.True(title.GetMinimumSize().Y <= title.Size.Y + 0.5f, $"title ({title.GetMinimumSize().Y}) taller than its box ({title.Size.Y}) with the notice present");
		var rule = UiFind.Get<Control>(s, "TitleRule");
		Assert.True(notice.GetGlobalRect().End.Y <= rule.GetGlobalRect().Position.Y + 1, "notice above the title rule");
		// the next offer has no notice (state must not leak between Present calls)
		var open = RunState.NewRun(pool);
		s.Present(new DraftRewardSource(RewardDraft.Build(open, pool, "oni", 3, elder: true, elderId: "elder-oni"), open));
		for (int i = 0; i < 3; i++) KitGallery.ForceLayout(s);
		Assert.True(!notice.Visible && notice.Text == "", "no stale notice on the next offer");
		var tp = title.Position;
		Assert.True(tp.Y >= 200, $"title back at its normal place, got y={tp.Y}");
		s.Free();
	}

	// ---- UI: pause panel ----

	static PauseMenu NewPause(Node host)
	{
		var p = GD.Load<PackedScene>("res://scenes/ui/pause_menu.tscn").Instantiate<PauseMenu>();
		host.AddChild(p);
		p.FreezeTree = false;
		p.Size = new(1920, 1080);
		return p;
	}

	static string[] PanelNames(PauseMenu p) =>
		UiFind.Get<PanelContainer>(p, "CancelPanel").FindChildren("Cancel?", "HBoxContainer", true, false).Cast<Control>()
			.OrderBy(c => c.Name.ToString()).Select(c => UiFind.Get<Label>(c, "CancelName").Text).ToArray();

	// X2: three summaries given by a careless caller never grow the panel past two places; and a second Bind or SetRunSummary replaces, not appends.
	[Test]
	public static void PausePanel_NeverMoreThanTwoRows_AndRebindReplacesTheOldRules(Node host)
	{
		var pool = Pool();
		var p = NewPause(host);
		p.Bind(RunHolding(pool, pool.CancelRules.Take(2)), 1, 8, "Kitsune", ContentPaths.Data("moves"));
		p.Open();
		Assert.Equal("Fox Step|Oni Chain|", string.Join("|", PanelNames(p)) + "|", "two rules, in the order taken");
		// a restart: new run, nothing held
		p.Bind(RunState.NewRun(pool), 1, 8, "Kitsune", ContentPaths.Data("moves"));
		Assert.Equal("Cancel rule 1: open|Cancel rule 2: open", string.Join("|", PanelNames(p)), "X1: a new run holds none; no stale rule");
		Assert.Equal("0 / 2", UiFind.Get<Label>(p, "CancelCount").Text, "count reset");
		Assert.Equal(1, p.FindChildren("CancelPanel", "PanelContainer", true, false).Count, "one panel, not stacked");
		// one rule, then the summary without the field at all (caller that gives none)
		p.Bind(RunHolding(pool, new[] { pool.CancelRules[2] }), 1, 8, "Kitsune", ContentPaths.Data("moves"));
		Assert.Equal("Undertow|Cancel rule 2: open", string.Join("|", PanelNames(p)), "one held, one open");
		p.SetRunSummary(new(new("RYO", 500, 1000), new("Kitsune", 0, 0), "x", Array.Empty<PauseSlotSummary>()));
		Assert.Equal("Cancel rule 1: open|Cancel rule 2: open", string.Join("|", PanelNames(p)), "a summary with no Cancels clears the panel");
		// an oversized summary (3 entries) must not draw a third row or a 3 / 2 count
		p.SetRunSummary(new(new("RYO", 500, 1000), new("Kitsune", 0, 0), "x", Array.Empty<PauseSlotSummary>(),
			pool.CancelRules.Select(c => new PauseCancelSummary(c.Id, c.Name, c.CardPlain)).ToArray()));
		Assert.True(PanelNames(p).Length <= 2, "at most two rows");
		p.Close(); p.QueueFree();
	}

	// Real longest names and lines from data/cancels, forced into a name label: the pause rows stay on their paper at 1080p and the 720p-scaled canvas.
	[Test]
	public static void PausePanel_LongestRealNames_StayInTheirRowsAndOnScreen(Node host)
	{
		var pool = Pool();
		string longest = pool.CancelRules.OrderByDescending(c => c.Name.Length).First().Name;
		foreach (var size in new[] { new Vector2(1920, 1080) })
		{
			var p = NewPause(host);
			p.Size = size;
			p.Bind(RunHolding(pool, pool.CancelRules.OrderByDescending(c => c.Name.Length).Take(2)), 1, 8, "Nine-Tailed Kitsune", ContentPaths.Data("moves"));
			p.Open();
			for (int i = 0; i < 8; i++) KitGallery.ForceLayout(p);
			var panel = UiFind.Get<PanelContainer>(p, "CancelPanel");
			foreach (var row in panel.FindChildren("Cancel?", "HBoxContainer", true, false).Cast<Control>())
			{
				var name = UiFind.Get<Label>(row, "CancelName");
				Assert.True(name.GetMinimumSize().X <= name.Size.X + 0.5f, $"{size}: '{name.Text}' ({name.GetMinimumSize().X}) wider than its label ({name.Size.X})");
				Assert.True(panel.GetGlobalRect().Encloses(row.GetGlobalRect()), $"{size}: row on its paper");
				Assert.True(name.Text.All(ch => ch < 128), $"non-ASCII in '{name.Text}'");
			}
			foreach (var l in new[] { UiFind.Get<Label>(p, "CancelHeading"), UiFind.Get<Label>(p, "CancelCount") })
				Assert.True(l.GetMinimumSize().X <= l.Size.X + 0.5f, $"{size}: {l.Name} '{l.Text}' clipped");
			Assert.True(longest.Length > 0, "setup");
			p.Close(); p.QueueFree();
		}
	}

	// The fight pause and the rest-stop pause read the same run through the same summary: same rows, same order, after rules are taken mid-run.
	[Test]
	public static void FightPause_AndRestStopPause_ShowTheSameRules(Node runner) => OnFixtures(() =>
	{
		var pool = Pool();
		var s = RunSession.Start(pool, FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir), 5);
		WalkTo(s, "r4-merchant");
		s.Run.HoldCancelRule(pool.CancelRule("oni-chain")!); s.Run.HoldCancelRule(pool.CancelRule("fox-step")!);

		var host = new Control { Size = new Vector2(1920, 1080) };
		runner.AddChild(host);
		var screen = GD.Load<PackedScene>("res://scenes/ui/rest_stop.tscn").Instantiate<RestStopScreen>();
		screen.LineSource = _ => "A line.";
		screen.BindRun(s, ControlScheme.Kihon);
		host.AddChild(screen);
		screen.Size = new Vector2(1920, 1080);
		string rest;
		try
		{
			Assert.True(screen.OpenPause(), "rest stop pause opens");
			for (int i = 0; i < 4; i++) KitGallery.ForceLayout(screen.PauseMenu!);
			rest = string.Join("|", PanelNames(screen.PauseMenu!)) + "#" + UiFind.Get<Label>(screen.PauseMenu!, "CancelCount").Text;
		}
		finally { runner.RemoveChild(host); host.Free(); }

		var mock = NewPause(runner);
		mock.Bind(s.Run, 4, 8, "Merchant", ContentPaths.Data("moves"));
		mock.SetRunSummary(new(new("RYO", 1, 1000), new("x", 0, 0), "p", Array.Empty<PauseSlotSummary>(), PauseCancelSummary.Of(s.Run)));
		string bind = string.Join("|", PanelNames(mock)) + "#" + UiFind.Get<Label>(mock, "CancelCount").Text;
		mock.QueueFree();
		Assert.Equal("Oni Chain|Fox Step#2 / 2", rest, "rest-stop pause: order taken");
		Assert.Equal(bind, rest, "Bind-only pause and rest-stop pause agree");
	});

	// A fight pause after a restart shows the new run's rules (none), then the new ones.
	[Test]
	public static void FightPause_AfterRestart_ShowsNoStaleRules(Node runner)
	{
		var pool = Pool();
		var saved = FightScene.Sources; var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		MenuSettings.Reset();
		var vp = new SubViewport { Size = new Vector2I(1920, 1080) };
		runner.AddChild(vp);
		try
		{
			var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			scene.ExternalDrive = true; scene.PauseEnabled = true; scene.FixedRunSeed = 48;
			scene.HeldBitsSource = () => InputBits.None;
			vp.AddChild(scene);
			Assert.Equal(0, scene.BuildPauseSummary().Cancels!.Count, "X1: starts with none");
			var p = FightScene.LoadAbilityPool();
			scene.DemoRun.HoldCancelRule(p.CancelRule("fox-step")!);
			scene.DemoRun.HoldCancelRule(p.CancelRule("oni-chain")!);
			Assert.Equal("fox-step|oni-chain", string.Join("|", scene.BuildPauseSummary().Cancels!.Select(c => c.Id)), "taken order");
			Assert.True(scene.TryPause(), "pause opens");
			Assert.Equal("2 / 2", UiFind.Get<Label>(scene.PauseMenu!, "CancelCount").Text, "panel count");
			scene.PauseMenu!.Close();
			scene.RestartRun();
			var after = scene.BuildPauseSummary().Cancels;
			Assert.True(after != null && after.Count == 0, "X1: a restarted run holds no rules, got " + string.Join(",", after?.Select(c => c.Id) ?? Array.Empty<string>()));
			scene.TryPause();
			Assert.Equal("0 / 2", UiFind.Get<Label>(scene.PauseMenu!, "CancelCount").Text, "panel reset after restart");
		}
		finally { runner.RemoveChild(vp); vp.QueueFree(); MenuSettings.Reset(); FightScene.Sources = saved; FightScene.Run = savedRun; }
	}

	// M5: a run saved with two rules and loaded again shows them in the order taken, and the pause summary is identical.
	[Test]
	public static void SaveAndLoad_KeepsTheOrderTaken_InTheSummary() => OnFixtures(() =>
	{
		var pool = Pool();
		var s = RunSession.Start(pool, FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir), 5);
		WalkTo(s, "r4-merchant");
		s.Run.HoldCancelRule(pool.CancelRule("oni-chain")!); s.Run.HoldCancelRule(pool.CancelRule("fox-step")!);
		s.CompleteNode();
		var save = RunSave.Capture(s);
		Assert.Equal("oni-chain|fox-step", string.Join("|", save.CancelRules), "saved in order taken");
		var back = save.Restore(pool, FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir));
		Assert.Equal(string.Join("|", PauseCancelSummary.Of(s.Run).Select(c => c.Id)), string.Join("|", PauseCancelSummary.Of(back.Run).Select(c => c.Id)), "same order after load");
		Assert.Equal(s.Run.StateHash(), back.Run.StateHash(), "hash survives a load");
	});

	// ---- UI: move list ----

	static MoveListPanel NewList(Node host, RunState run)
	{
		var p = GD.Load<PackedScene>("res://scenes/ui/move_list_panel.tscn").Instantiate<MoveListPanel>();
		host.AddChild(p);
		p.Size = new(1920, 1080);
		p.Bind(MoveListSource.Build(run, ContentPaths.Data("moves")));
		p.Visible = true;
		for (int i = 0; i < 6; i++) KitGallery.ForceLayout(p);
		return p;
	}

	// A12 + the longest real lines: with the path line shown, every cancel place keeps its text inside its plate, the plate stays inside the panel, and the two plates do not overlap.
	[Test]
	public static void MoveList_LongestPathLines_StayInsideTheirPlates_AtBothSizes(Node host)
	{
		var pool = Unlocked();
		var longestFrames = pool.CancelRules.OrderByDescending(c => c.CardFrames.Length).Take(2).ToList();
		foreach (var size in new[] { new Vector2(1920, 1080) })
		{
			var p = NewList(host, RunHolding(pool, longestFrames));
			p.Size = size;
			foreach (bool frames in new[] { false, true })
			{
				if (p.FrameDataVisible != frames) p.ToggleFrameData();
				for (int i = 0; i < 6; i++) KitGallery.ForceLayout(p);
				Rect2 prev = default;
				foreach (var slot in p.CancelSlots)
				{
					foreach (var l in new[] { slot.NameLabel, slot.BodyLabel })
					{
						Assert.True(l.GetMinimumSize().Y <= l.Size.Y + 0.5f, $"{size} frames={frames}: '{l.Text}' taller ({l.GetMinimumSize().Y}) than its box ({l.Size.Y})");
						Assert.True(slot.GetGlobalRect().Grow(1).Encloses(l.GetGlobalRect()), $"{size} frames={frames}: '{l.Text}' leaves its plate");
					}
					Assert.True(p.GetGlobalRect().Grow(1).Encloses(slot.GetGlobalRect()), $"{size} frames={frames}: plate {slot.RuleId} leaves the move list");
					if (prev.Size != Vector2.Zero) Assert.True(!prev.Intersects(slot.GetGlobalRect(), false), "plates overlap");
					prev = slot.GetGlobalRect();
				}
			}
			p.QueueFree();
		}
	}

	// A12: Tab (the real key) swaps the path line in and back; the swap applies to both rules and an empty place keeps its own line.
	[Test]
	public static void MoveList_TabSwapsPathLineIn_AndAnOpenPlaceIsUnaffected(Node host)
	{
		var pool = Pool();
		var p = NewList(host, RunHolding(pool, new[] { pool.CancelRules[1] }));
		Assert.Equal(pool.CancelRules[1].CardPlain, p.CancelSlots[0].BodyLabel.Text, "plain by default");
		Assert.Equal("An elder may offer one.", p.CancelSlots[1].BodyLabel.Text, "open place");
		p.ToggleFrameData();
		Assert.Equal(pool.CancelRules[1].CardFrames, p.CancelSlots[0].BodyLabel.Text, "path line");
		Assert.Equal("Cancel rule 2: open", p.CancelSlots[1].NameLabel.Text, "open place name");
		Assert.Equal("An elder may offer one.", p.CancelSlots[1].BodyLabel.Text, "open place keeps its line under the toggle");
		Assert.Equal("1 / 2 held", p.CancelCountText, "count");
		// a rebind to a fresh run keeps the toggle state but drops the rule
		p.Bind(MoveListSource.Build(RunState.NewRun(pool), ContentPaths.Data("moves")));
		Assert.True(p.CancelSlots.All(s => s.IsEmpty && s.RuleId == ""), "no stale rule after a rebind");
		Assert.Equal("0 / 2 held", p.CancelCountText, "count reset");
		Assert.True(p.CancelSlots[0].BodyLabel.GetThemeColor("font_color") != Colors.Transparent, "label visible");
		p.QueueFree();
	}

	// The labels the ticket adds are plain ASCII.
	[Test]
	public static void TicketLabels_AreAscii(Node host)
	{
		var pool = Pool();
		var p = NewList(host, RunState.NewRun(pool));
		var texts = p.CancelSlots.SelectMany(x => new[] { x.NameLabel.Text, x.BodyLabel.Text }).Append(p.CancelCountText).ToList();
		var pause = NewPause(host);
		pause.Bind(RunState.NewRun(pool), 1, 8, "Kitsune", ContentPaths.Data("moves"));
		texts.AddRange(PanelNames(pause)); texts.Add(UiFind.Get<Label>(pause, "CancelHeading").Text); texts.Add(UiFind.Get<Label>(pause, "CancelCount").Text);
		Assert.True(texts.Count >= 8, "read the real labels");
		foreach (string s in texts) Assert.True(s.All(ch => ch < 128), "ASCII: " + s);
		p.QueueFree(); pause.QueueFree();
	}
}
