using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-218 (X1, X2, X4, A12): Ryo's held cancel rules are shown on the pause run panel and the move list (name, plain
/// line, path line behind the frame-data toggle), and the reward screen says so when the elder's rule is held out
/// because he already holds two. The screens only render what the run and the draft give them.
/// </summary>
public static class HeldCancelRuleUiTests
{
	static AbilityPool Pool() => FightScene.LoadAbilityPool();

	static RunState RunHolding(AbilityPool pool, int n)
	{
		var run = RunState.NewRun(pool);
		foreach (var c in pool.CancelRules.Take(n)) run.HoldCancelRule(c);
		return run;
	}

	static PauseMenu Pause(Node host, RunState run)
	{
		var p = GD.Load<PackedScene>("res://scenes/ui/pause_menu.tscn").Instantiate<PauseMenu>();
		host.AddChild(p);
		p.FreezeTree = false;
		p.Size = new(1920, 1080);
		p.Bind(run, 1, 8, "Kitsune", ContentPaths.Data("moves"));
		p.Open();
		for (int i = 0; i < 8; i++) KitGallery.ForceLayout(p);
		return p;
	}

	[Test]
	public static void PauseRunPanel_ListsHeldRulesAndOpenPlaces_ForZeroOneAndTwo(Node host)
	{
		var pool = Pool();
		Assert.True(pool.CancelRules.Count >= 2, "setup: real data has the three rules");
		for (int n = 0; n <= 2; n++)
		{
			var run = RunHolding(pool, n);
			var p = Pause(host, run);
			var box = UiFind.Get<VBoxContainer>(p, "CancelRules");
			Assert.Equal($"{n} / 2", UiFind.Get<Label>(p, "CancelCount").Text, "count against the cap (X2)");
			var slots = box.GetChildren().OfType<CancelRuleSlot>().ToList();
			Assert.Equal(2, slots.Count, "always two places");
			for (int i = 0; i < 2; i++)
			{
				Assert.Equal(i >= n, slots[i].IsEmpty, $"place {i + 1} empty or held");
				if (i < n)
				{
					Assert.Equal(pool.CancelRules[i].Name, slots[i].NameLabel.Text, "rule name");
					Assert.Equal(pool.CancelRules[i].CardPlain, slots[i].BodyLabel.Text, "plain line from data (A12)");
					Assert.True(slots[i].IconRect.Visible && slots[i].IconRect.Texture == AbilityArt.ForId(pool.CancelRules[i].Id), "icon by id, fallback if none");
				}
				else Assert.True(slots[i].IconRect.Texture == null, "an open place has no icon");
				Assert.True(new Rect2(0, 0, 1920, 1080).Encloses(slots[i].GetGlobalRect()), $"place stays on screen: {slots[i].GetGlobalRect()} (n={n})");
				Assert.True(slots[i].GetGlobalRect().End.Y <= UiFind.Get<Control>(p, "Footer").GetGlobalRect().Position.Y + 1, "clear of the footer");
				foreach (var l in new[] { slots[i].NameLabel, slots[i].BodyLabel })
					Assert.True(l.GetMinimumSize().Y <= l.Size.Y + 1, "copy fits its label");
			}
			// the slots A-D above are not pushed over the rules
			var slotBox = UiFind.Get<VBoxContainer>(p, "Slots");
			Assert.True(slotBox.GetGlobalRect().End.Y <= box.GetGlobalRect().Position.Y + 1, "specials end before the rules begin");
			p.Close(); p.QueueFree();
		}
	}

	[Test]
	public static void MoveList_ShowsHeldRules_PlainFirst_FramesBehindTheToggle(Node host)
	{
		var pool = Pool();
		var run = RunHolding(pool, 2);
		var p = Pause(host, run);
		var list = p.MoveList;
		list.Visible = true;
		for (int i = 0; i < 3; i++) KitGallery.ForceLayout(list);
		Assert.Equal("2 / 2 held", list.CancelCountText, "cap shown");
		Assert.Equal(pool.CancelRules[0].CardPlain, list.CancelSlots[0].BodyLabel.Text, "plain first");
		list.ToggleFrameData();
		Assert.Equal(pool.CancelRules[0].CardFrames, list.CancelSlots[0].BodyLabel.Text, "path line behind the toggle");
		Assert.Equal(pool.CancelRules[1].CardFrames, list.CancelSlots[1].BodyLabel.Text, "second rule too");
		list.ToggleFrameData();
		Assert.Equal(pool.CancelRules[0].CardPlain, list.CancelSlots[0].BodyLabel.Text, "toggled back");
		p.Close(); p.QueueFree();

		var none = Pause(host, RunHolding(pool, 0));
		none.MoveList.Visible = true;
		Assert.Equal("0 / 2 held", none.MoveList.CancelCountText, "X1: none at the start");
		Assert.True(none.MoveList.CancelSlots.All(s => s.IsEmpty), "both places open");
		none.Close(); none.QueueFree();
	}

	[Test]
	public static void MoveListSource_AndPauseSummary_FollowTheRunInTheOrderTaken()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		Assert.Equal(0, MoveListSource.Build(run, ContentPaths.Data("moves")).Cancels.Count, "X1");
		Assert.Equal(0, PauseCancelSummary.Of(run).Length, "X1, summary");
		var second = pool.CancelRules[1]; var first = pool.CancelRules[0];
		run.HoldCancelRule(second); run.HoldCancelRule(first);
		var view = MoveListSource.Build(run, ContentPaths.Data("moves"));
		Assert.Equal($"{second.Id}|{first.Id}", string.Join("|", view.Cancels.Select(c => c.Id)), "order taken");
		Assert.Equal(second.CardFrames, view.Cancels[0].Frames, "frames from data");
		Assert.Equal(MoveListView.CancelCap, 2, "X2");
	}

	[Test]
	public static void RewardScreen_SaysTheCapIsReached_WhereTheEldersRuleWouldBe(Node host)
	{
		var pool = Pool();
		var elder = pool.CancelRules[0];
		string yokai = elder.Source;
		// Holding two other rules: build needs two held rules not equal to the elder's own.
		var held = pool.CancelRules.Where(c => c.Id != elder.Id).Take(2).ToList();
		Assert.Equal(2, held.Count, "setup: three rules in data");
		var full = RunState.NewRun(pool);
		foreach (var c in held) full.HoldCancelRule(c);
		var draft = RewardDraft.Build(full, pool, yokai, 3, elder: true, elderId: elder.OfferedBy);
		Assert.True(draft.Offer.CancelRuleHeldOut, "the draft reports the rule held out");
		Assert.True(!draft.Offer.Cards.Any(c => c.Kind == Sim.CardKind.CancelRule), "no cancel card (X2)");
		var source = new DraftRewardSource(draft, full, new StubRewardSource());
		Assert.Equal(DraftRewardSource.FullNotice, source.Offer.Notice, "the notice reaches the offer");
		Assert.True(DraftRewardSource.FullNotice.All(ch => ch < 128), "plain ASCII, no emoji, ZWJ or combining marks");

		var s = GD.Load<PackedScene>("res://scenes/ui/reward_screen.tscn").Instantiate<RewardScreen>();
		host.AddChild(s);
		s.Present(source);
		var notice = UiFind.Get<Label>(s, "Notice");
		Assert.True(notice.Visible && notice.Text == DraftRewardSource.FullNotice, "notice shown");
		var title = UiFind.Get<Label>(s, "Title");
		Assert.True(title.GetGlobalRect().End.Y <= notice.GetGlobalRect().Position.Y + 1, "title and notice do not overlap");
		s.Free();

		// With room (holds one), the card is offered and no notice shows; same for a non-elder node.
		var one = RunState.NewRun(pool); one.HoldCancelRule(held[0]);
		var open = RewardDraft.Build(one, pool, yokai, 3, elder: true, elderId: elder.OfferedBy);
		Assert.True(!open.Offer.CancelRuleHeldOut && open.Offer.Cards.Any(c => c.Kind == Sim.CardKind.CancelRule), "room: the card is offered");
		Assert.True(new DraftRewardSource(open, one, new StubRewardSource()).Offer.Notice == null, "no notice with room");
		var duel = RewardDraft.Build(full, pool, yokai, 3);
		Assert.True(!duel.Offer.CancelRuleHeldOut, "a duel never talks about cancel rules (X1)");
		var holdsIt = RunState.NewRun(pool); holdsIt.HoldCancelRule(elder); holdsIt.HoldCancelRule(held[0]);
		Assert.True(!RewardDraft.Build(holdsIt, pool, yokai, 3, elder: true, elderId: elder.OfferedBy).Offer.CancelRuleHeldOut, "already holding this elder's own rule: nothing is held out");
	}
}
