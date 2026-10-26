using System;
using System.Collections.Generic;
using System.IO;
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
/// Test Breaker probes for YOK-175 (Esc / pad B on a rest stop goes back to the map; P / Start pauses it). Expected values
/// come from R1 (the pick stays committed), R6 / R7 (one choice per stop, no heal or coins unless taken), R8 (coins only
/// from duels), M5 (saves at node boundaries; Continue restarts the node as entered), E23 (draft seed = run seed + step),
/// T1 (the copy locks on entering the Tanuki's node) and the ticket's provisional defaults (leaving allowed, stop spent).
/// </summary>
public static class RestStopLeaveBreakerProbes
{
	const string Scene = "res://scenes/ui/rest_stop.tscn";
	static string MapDir => ContentPaths.Data("map");

	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		try { body(); }
		finally { FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe; }
	}

	static RunSession NewSession(uint seed = 5) =>
		RunSession.Start(FightScene.LoadAbilityPool(), FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir), seed);

	static bool Leads(RunSession s, string from, string to) => from == to || s.Map.Graph.Next(from).Any(n => Leads(s, n, to));

	static void Pick(RewardDraft? d) { if (d is { Picked: false }) { var c = d.Offer.Cards[0]; Assert.True(d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null).Ok, "picked"); } }

	static void Finish(RunSession s, int healthLeft) { Pick(s.WinFight(healthLeft)); Assert.True(s.CompleteNode().Ok, "completed"); }

	/// <summary>Standing at the named stop (earlier stops healed through the rules, fights won), health set to <paramref name="health"/>.</summary>
	static RunSession AtNode(string nodeId, int health = 300, uint seed = 5)
	{
		var s = NewSession(seed);
		for (; ; )
		{
			string pick = s.Reachable().First(id => Leads(s, id, nodeId));
			Assert.True(s.Enter(pick).Ok, "entered " + pick);
			if (pick == nodeId) break;
			if (s.Node!.Type is "merchant" or "dojo") Assert.True(s.CompleteNode().Ok, "a stop passed");
			else Finish(s, 400);
		}
		s.Run.SetHealth(health);
		return s;
	}

	static string Slots(RunSession s) => string.Join("|", s.Run.Owned.Select(o => $"{o.Slot}:{o.Special.Data.Id}:{o.Special.Level}:{o.Special.Modifier?.Id}"));

	sealed record Snap(ulong Hash, int Health, int Coins, string Node, int Visited, string Reach, bool Over, string Slots);
	static Snap Take(RunSession s) => new(s.StateHash(), s.Run.Health, s.Run.Coins, s.Node?.NodeId ?? "-", s.Map.Visited.Count, string.Join(",", s.Reachable()), s.IsOver, Slots(s));

	// ---- sim: LeaveNode in every state ----

	// R1 / M5: the stop is spent once left; a second leave is a refused call, not a second node boundary.
	[Test]
	public static void LeaveNode_Twice_SecondRefused_OneNodeCompleted_NothingMoves() => OnFixtures(() =>
	{
		foreach (string stop in new[] { "r4-merchant", "r7-dojo" })
		{
			var s = AtNode(stop);
			int done = 0, ended = 0; s.NodeCompleted += _ => done++; s.RunEnded += _ => ended++;
			Assert.True(s.LeaveNode().Ok, $"{stop}: first leave");
			var after = Take(s);
			Assert.True(!s.LeaveNode().Ok, $"{stop}: the second leave is refused");
			Assert.True(Take(s) == after, $"{stop}: nothing moved on the second leave");
			Assert.True(!s.CompleteNode().Ok && Take(s) == after, $"{stop}: Complete after Leave is refused too");
			Assert.Equal(1, done, $"{stop}: NodeCompleted raised once (one save point)");
			Assert.Equal(0, ended, $"{stop}: the run did not end");
		}
	});

	static List<(string Name, RunSession S)> States()
	{
		var l = new List<(string, RunSession)>();
		l.Add(("new run, no node", NewSession()));
		{ var s = NewSession(); Assert.True(s.Enter(s.Reachable()[0]).Ok, "duel"); l.Add(("duel entered", s)); }
		{ var s = NewSession(); Assert.True(s.Enter(s.Reachable()[0]).Ok, "duel"); var d = s.WinFight(500); Assert.True(d is { Picked: false }, "owes a pick"); l.Add(("duel won, reward unpicked", s)); }
		{ var s = NewSession(); Assert.True(s.Enter(s.Reachable()[0]).Ok, "duel"); Pick(s.WinFight(500)); l.Add(("duel won, reward picked, not completed", s)); }
		{ var s = NewSession(); Assert.True(s.Enter(s.Reachable()[0]).Ok, "duel"); Assert.True(s.LoseFight().Ok, "lost"); l.Add(("run lost", s)); }
		{ var s = AtBoss(5, false); l.Add(("Tanuki entered", s)); }
		{ var s = AtBoss(5, false); s.WinFight(500); Assert.True(s.CompleteNode().Ok && s.IsOver, "won"); l.Add(("run won", s)); }
		{ var s = AtNode("r4-merchant"); Assert.True(s.Merchant!.TakeHeal().Ok, "heal"); l.Add(("merchant, healed", s)); }
		{ var s = AtNode("r4-merchant"); Assert.True(s.Merchant!.Choose(MerchantChoice.Shop).Ok, "shop"); l.Add(("merchant, shop chosen", s)); }
		{ var s = AtNode("r4-merchant"); Assert.True(s.Merchant!.Choose(MerchantChoice.Shop).Ok, "shop"); var shop = s.Merchant.Shop; var id = shop.Shelf.Select(x => x.Id).FirstOrDefault(x => shop.WhyNotBuy(x) is null); if (id != null) Assert.True(shop.Buy(id).Ok, "bought"); l.Add(("merchant, after a purchase", s)); }
		{ var s = AtNode("r7-dojo"); Assert.True(s.Dojo!.Rest().Ok, "rest"); l.Add(("dojo, rested", s)); }
		{ var s = DojoTrialTests.AtDojo(600); Assert.True(s.Dojo!.ChooseTrial(s.Dojo.Trials[0].Trial.Id).Ok, "trial"); l.Add(("dojo, trial running", s)); }
		{ var s = DojoTrialTests.AtDojo(600); Assert.True(s.Dojo!.ChooseTrial(s.Dojo.Trials[0].Trial.Id).Ok && s.Dojo.RecordAttempt(false).Ok, "one miss"); l.Add(("dojo, trial one miss in", s)); }
		{ var s = DojoTrialTests.AtDojo(600); var d = s.Dojo!; Assert.True(d.ChooseTrial(d.Trials[0].Trial.Id).Ok && d.RecordAttempt(false).Ok && d.RecordAttempt(false).Ok && d.RecordAttempt(false).Ok, "three misses"); l.Add(("dojo, trial failed", s)); }
		{ var s = DojoTrialTests.AtDojo(600); var d = s.Dojo!; Assert.True(d.ChooseTrial(d.Trials[0].Trial.Id).Ok && d.RecordAttempt(true).Ok, "landed"); l.Add(("dojo, trial landed", s)); }
		{ var s = AtNode("r4-merchant"); Assert.True(s.LeaveNode().Ok, "left"); l.Add(("stop already left", s)); }
		return l;
	}

	static RunSession AtBoss(uint seed, bool leaveStops, string row7 = "r7-merchant")
	{
		var s = NewSession(seed);
		for (; ; )
		{
			string pick = s.Reachable().FirstOrDefault(id => id == row7) ?? s.Reachable()[0];
			Assert.True(s.Enter(pick).Ok, "entered " + pick);
			if (s.Node!.Type == "boss") return s;
			if (s.Node.Type is "merchant" or "dojo") Assert.True(leaveStops ? s.LeaveNode().Ok : s.CompleteNode().Ok, "stop passed");
			else Finish(s, 400);
		}
	}

	// Refused actions: hash, fields and raised events identical before and after, in every state a leave is not allowed.
	[Test]
	public static void LeaveNode_RefusedInEveryOtherState_HashFieldsAndEventsIdentical() => OnFixtures(() =>
	{
		foreach (var (name, s) in States())
		{
			var before = Take(s);
			int entered = 0, done = 0, ended = 0; s.NodeEntered += _ => entered++; s.NodeCompleted += _ => done++; s.RunEnded += _ => ended++;
			Assert.True(!s.LeaveNode().Ok, $"{name}: a leave is refused here");
			Assert.True(Take(s) == before, $"{name}: hash, health, coins, node, walk and slots unchanged");
			Assert.True(entered == 0 && done == 0 && ended == 0, $"{name}: no event raised");
		}
	});

	// R6: heal only when taken. Boundaries: health 1, 1000 (cap), a middling value.
	[Test]
	public static void LeaveNode_AtHealthBoundaries_NeverHeals_HashIsCompletingTheStop() => OnFixtures(() =>
	{
		foreach (string stop in new[] { "r4-merchant", "r7-dojo" })
			foreach (int hp in new[] { 1, 750, 1000 })
			{
				var a = AtNode(stop, hp, 11); var b = AtNode(stop, hp, 11);
				Assert.True(a.LeaveNode().Ok && b.CompleteNode().Ok, $"{stop}/{hp}: left / completed");
				Assert.Equal(hp, a.Run.Health, $"{stop}/{hp}: health as entered");
				Assert.Equal(b.StateHash(), a.StateHash(), $"{stop}/{hp}: same state as completing with no choice");
				var c = AtNode(stop, hp, 11);
				Assert.True((stop == "r4-merchant" ? c.Merchant!.TakeHeal().Ok : c.Dojo!.Rest().Ok) && c.CompleteNode().Ok, "healed twin");
				Assert.True((c.StateHash() == a.StateHash()) == (hp == 1000), $"{stop}/{hp}: leaving differs from healing unless Ryo was already full");
			}
	});

	// R1, R8, T1, M5: a whole run that leaves every stop walks the same eight rows to the Tanuki; coins come from duels only; the lock is taken.
	[Test]
	public static void WholeRun_LeavingEveryStop_ReachesTheBoss_CoinsOnlyFromDuels_CopyLocksOnEntering() => OnFixtures(() =>
	{
		foreach (uint seed in new uint[] { 1, 5, 42 })
			foreach (string row7 in new[] { "r7-merchant", "r7-dojo" })
			{
				var s = NewSession(seed); int hp = 700; int stops = 0; var spent = new List<string>();
				for (; ; )
				{
					string pick = s.Reachable().FirstOrDefault(id => id == row7) ?? s.Reachable()[0];
					Assert.True(s.Enter(pick).Ok, "entered " + pick);
					Assert.Equal(s.Map.Visited.Count + 1, s.Step, $"{seed}/{row7}: step counter");
					var n = s.Node!;
					if (n.Type == "boss")
					{
						Assert.True(s.CopyLocked, $"{seed}/{row7}: T1 the lock is taken on entering the Tanuki");
						Assert.Equal(150, s.Run.Coins, $"{seed}/{row7}: R8 coins by the boss are the duels' alone (25x3 + 50 + 25)");
						Assert.Equal(8, s.Map.Visited.Count, $"{seed}/{row7}: eight rows walked, none skipped by leaving");
						Assert.True(spent.Count == 2, $"{seed}/{row7}: both stops were left");
						break;
					}
					if (n.Type is "merchant" or "dojo")
					{
						int coins = s.Run.Coins, health = s.Run.Health; stops++;
						Assert.True(s.LeaveNode().Ok, $"{seed}/{row7}: leave {n.NodeId}");
						Assert.True(s.Run.Coins == coins && s.Run.Health == health, $"{seed}/{row7}: no coins, no heal");
						Assert.True(!s.Enter(n.NodeId).Ok, $"{seed}/{row7}: {n.NodeId} cannot be re-entered (R1)");
						Assert.True(s.Reachable().All(id => s.Map.Graph.Node(id).Row == n.Row + 1), $"{seed}/{row7}: only the next row is open");
						spent.Add(n.NodeId);
					}
					else { Pick(s.WinFight(Math.Max(1, hp - 100))); Assert.True(s.CompleteNode().Ok, "complete"); hp = s.Run.Health; }
				}
			}
	});

	// Determinism / refused actions: refused leaves sprinkled through a run change neither the hash trace nor the draft offers.
	[Test]
	public static void RefusedLeaves_SprinkledThroughARun_DoNotPerturbHashesOrOffers() => OnFixtures(() =>
	{
		(List<ulong>, List<string>) Play(bool poke, uint seed)
		{
			var s = NewSession(seed); var h = new List<ulong>(); var offers = new List<string>(); int hp = 600;
			void Poke(string where) { if (!poke) return; var snap = Take(s); Assert.True(!s.LeaveNode().Ok, "refused " + where); Assert.True(Take(s) == snap, "unchanged " + where); }
			Poke("before any node");
			for (; ; )
			{
				string pick = s.Reachable()[0];
				Assert.True(s.Enter(pick).Ok, "entered");
				h.Add(s.StateHash()); if (s.Node!.Type is not ("merchant" or "dojo")) Poke("on entering " + pick); // a stop with no choice made is the one place a leave is allowed
				if (s.Node!.Type == "boss") { Poke("at the Tanuki"); Pick(s.WinFight(500)); Assert.True(s.CompleteNode().Ok, "done"); break; }
				if (s.Node.Type == "merchant") { Assert.True(s.Merchant!.TakeHeal().Ok, "heal"); Poke("healed"); Assert.True(s.CompleteNode().Ok, "leave"); }
				else if (s.Node.Type == "dojo") { Assert.True(s.Dojo!.Rest().Ok, "rest"); Poke("rested"); Assert.True(s.CompleteNode().Ok, "leave"); }
				else
				{
					var d = s.WinFight(Math.Max(1, hp - 100)); Poke("won, reward owed");
					if (d != null) offers.Add(string.Join(",", d.Offer.Cards.Select(c => c.ToString())));
					Pick(d); Poke("reward picked"); Assert.True(s.CompleteNode().Ok, "complete");
				}
				hp = s.Run.Health; h.Add(s.StateHash()); Poke("between nodes");
			}
			return (h, offers);
		}
		foreach (uint seed in new uint[] { 3, 77 })
		{
			var a = Play(false, seed); var b = Play(true, seed);
			Assert.True(a.Item1.SequenceEqual(b.Item1), $"seed {seed}: hash trace identical with refused leaves in between");
			Assert.True(a.Item2.SequenceEqual(b.Item2), $"seed {seed}: draft offers identical");
		}
	});

	// E23 / M5: after a leave, the next duel's draft (seed + step) is what completing the stop gives, also after a save round trip.
	[Test]
	public static void Leave_NextDraftIsTheSameAsCompletingTheStop_AndAfterASaveRoundTrip_E23() => OnFixtures(() =>
	{
		string Offer(RunSession s) { Assert.True(s.Enter(s.Reachable()[0]).Ok, "next row"); var d = s.WinFight(300)!; return string.Join(",", d.Offer.Cards.Select(c => c.ToString())); }
		foreach (uint seed in new uint[] { 5, 42 })
		{
			var left = AtNode("r4-merchant", 400, seed); var viaSave = AtNode("r4-merchant", 400, seed); var done = AtNode("r4-merchant", 400, seed);
			Assert.True(left.LeaveNode().Ok && done.CompleteNode().Ok && viaSave.LeaveNode().Ok, "left / completed");
			var back = RunSave.Capture(viaSave).Restore(FightScene.LoadAbilityPool(), FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir));
			Assert.Equal(viaSave.StateHash(), back.StateHash(), $"{seed}: the saved leave restores to the same state");
			string want = Offer(done);
			Assert.Equal(want, Offer(left), $"{seed}: leaving offers what completing offers");
			Assert.Equal(want, Offer(back), $"{seed}: a continued run offers the same cards (E23)");
		}
	});

	// ---- scene: input on the stop ----

	static void Push(Node n, InputEvent e) => n.GetViewport().PushInput(e);
	static void Tap(Node n, Key k) { Push(n, new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true }); Push(n, new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false }); }
	static void Pad(Node n, JoyButton b) { Push(n, new InputEventJoypadButton { ButtonIndex = b, Pressed = true }); Push(n, new InputEventJoypadButton { ButtonIndex = b, Pressed = false }); }
	static Control? Focus(Node n) => n.GetViewport().GuiGetFocusOwner();
	static void Press(Button b) => b.EmitSignal(BaseButton.SignalName.Pressed);
	static Button Yes(PauseMenu m) => ((PaperDialog)m.ConfirmBox).ConfirmButton!;

	sealed class Rig
	{
		public RestStopScreen Screen = null!; public Control Host = null!; public RunSession S = null!;
		public int Left, Titles, Restarts, Completed;
	}

	static Rig Open(Node runner, RunSession s)
	{
		var r = new Rig { S = s };
		r.Host = new Control { Size = new Vector2(1920, 1080) };
		runner.AddChild(r.Host);
		r.Host.Size = new Vector2(1920, 1080);
		r.Screen = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
		r.Screen.LineSource = _ => "A line.";
		r.Screen.BindRun(s, ControlScheme.Kihon);
		r.Host.AddChild(r.Screen);
		r.Screen.Size = new Vector2(1920, 1080);
		r.Screen.Left += () => r.Left++; r.Screen.TitleRequested += () => r.Titles++; r.Screen.RunRestartRequested += () => r.Restarts++;
		s.NodeCompleted += _ => r.Completed++;
		return r;
	}

	static void Close(Node runner, Rig r) { runner.RemoveChild(r.Host); r.Host.Free(); }

	// R1, R6, R7: Enter then Esc / B takes the choice and Esc is ignored; Esc first leaves, and the Enter / A that follows heals nothing.
	[Test]
	public static void Input_EnterAndEscOnTheSameFrame_EitherOrder_NeverBothHealAndLeave(Node runner) => OnFixtures(() =>
	{
		foreach (string stop in new[] { "r4-merchant", "r7-dojo" })
		{
			var s = AtNode(stop, 300); var r = Open(runner, s);
			try
			{
				Tap(runner, Key.Enter);
				Assert.True(r.Screen.Model!.ChoiceMade && s.Run.Health > 300, $"{stop}: Enter took the free card");
				int hp = s.Run.Health; ulong h = s.StateHash();
				Tap(runner, Key.Escape); Pad(runner, JoyButton.B);
				Assert.True(r.Left == 0 && r.Titles == 0 && r.Completed == 0 && s.Node != null && s.Run.Health == hp && s.StateHash() == h, $"{stop}: Esc / B after Enter change nothing");
			}
			finally { Close(runner, r); }

			var s2 = AtNode(stop, 300); var r2 = Open(runner, s2);
			try
			{
				int coins = s2.Run.Coins;
				Tap(runner, Key.Escape); Tap(runner, Key.Enter); Pad(runner, JoyButton.A); Pad(runner, JoyButton.B); Tap(runner, Key.Escape);
				Assert.True(r2.Left == 1 && r2.Completed == 1 && r2.Titles == 0, $"{stop}: left once ({r2.Left}), completed once ({r2.Completed})");
				Assert.True(s2.Run.Health == 300 && s2.Run.Coins == coins && s2.Node == null, $"{stop}: Enter / A after Esc heal and pay nothing");
				for (int i = 0; i < 4; i++) { Tap(runner, Key.Escape); Pad(runner, JoyButton.B); }
				Assert.True(r2.Left == 1 && r2.Completed == 1, $"{stop}: repeated Esc / B leave nothing more");
			}
			finally { Close(runner, r2); }
		}
	});

	// R7: once the trial is the choice (running, failed or landed) Esc / B do nothing; Continue (when finished) still moves on.
	[Test]
	public static void Input_EscAfterATrial_RunningFailedLanded_DoesNothing(Node runner) => OnFixtures(() =>
	{
		foreach (string state in new[] { "running", "one miss", "failed", "landed" })
		{
			var s = DojoTrialTests.AtDojo(640); var r = Open(runner, s);
			try
			{
				Assert.True(r.Screen.OpenSecond(), "trial chosen");
				var d = s.Dojo!;
				if (state == "one miss") Assert.True(d.RecordAttempt(false).Ok, "miss");
				if (state == "failed") for (int i = 0; i < 3; i++) Assert.True(d.RecordAttempt(false).Ok, "miss");
				if (state == "landed") Assert.True(d.RecordAttempt(true).Ok, "landed");
				r.Screen.TrialClosed();
				ulong h = s.StateHash(); int hp = s.Run.Health;
				Tap(runner, Key.Escape); Pad(runner, JoyButton.B);
				Assert.True(r.Left == 0 && r.Titles == 0 && r.Completed == 0 && s.Node != null && s.StateHash() == h && s.Run.Health == hp, $"{state}: Esc / B change nothing");
				if (state is "failed" or "landed") Assert.True(r.Screen.Leave() && s.Node == null, $"{state}: Continue moves on");
			}
			finally { Close(runner, r); }
		}
	});

	// R6 / YOK-104: opening the shop is the choice; backing out, or buying, leaves Esc inert.
	[Test]
	public static void Input_EscAfterTheShop_BackedOutOrPurchased_DoesNothing(Node runner) => OnFixtures(() =>
	{
		foreach (bool buy in new[] { false, true })
		{
			var s = AtNode("r4-merchant", 300); var r = Open(runner, s);
			try
			{
				Assert.True(r.Screen.OpenSecond(), "shop chosen");
				if (buy)
				{
					var shop = s.Merchant!.Shop; string? id = shop.Shelf.Select(x => x.Id).FirstOrDefault(x => shop.WhyNotBuy(x) is null);
					if (id != null) Assert.True(shop.Buy(id).Ok, "bought " + id);
				}
				r.Screen.ShopClosed();
				ulong h = s.StateHash();
				Tap(runner, Key.Escape); Pad(runner, JoyButton.B);
				Assert.True(r.Left == 0 && r.Titles == 0 && s.Node != null && s.StateHash() == h, $"buy={buy}: Esc / B change nothing");
				Assert.True(!s.LeaveNode().Ok && s.StateHash() == h, $"buy={buy}: the session refuses the leave too");
			}
			finally { Close(runner, r); }
		}
	});

	// A hidden stop (the shop or a trial is over it) reads no Esc, B, P or Start.
	[Test]
	public static void Input_HiddenStop_EscBPAndStart_NeitherLeaveNorPause(Node runner) => OnFixtures(() =>
	{
		var s = AtNode("r4-merchant"); var r = Open(runner, s);
		try
		{
			r.Screen.Visible = false; var snap = Take(s);
			Tap(runner, Key.Escape); Pad(runner, JoyButton.B); Tap(runner, Key.P); Pad(runner, JoyButton.Start);
			Assert.True(r.Left == 0 && r.Titles == 0 && Take(s) == snap && !r.Screen.Paused && r.Screen.PauseMenu == null, "hidden: no leave, no pause menu made");
			Assert.True(!r.Screen.OpenPause(), "OpenPause is refused while hidden");
		}
		finally { Close(runner, r); }
	});

	// ---- scene: the pause menu over the stop ----

	// P opens, Esc resumes, repeated: no leak into the stop, one menu, focus back on the stop every time, P / Start while open do not close it.
	[Test]
	public static void Pause_OpenedAndClosedRapidly_NeverLeaks_OneMenu_FocusBack(Node runner) => OnFixtures(() =>
	{
		foreach (string stop in new[] { "r4-merchant", "r7-dojo" })
		{
			var s = AtNode(stop); var r = Open(runner, s);
			try
			{
				var snap = Take(s);
				for (int i = 0; i < 6; i++)
				{
					if (i % 2 == 0) Tap(runner, Key.P); else Pad(runner, JoyButton.Start);
					Assert.True(r.Screen.Paused, $"{stop}/{i}: opens");
					Tap(runner, Key.P);
					Assert.True(r.Screen.Paused, $"{stop}/{i}: P while open does not close it");
					if (i % 3 == 2) Pad(runner, JoyButton.Start); else Tap(runner, Key.Escape);
					Assert.True(!r.Screen.Paused, $"{stop}/{i}: closes");
					var f = Focus(runner);
					Assert.True(f != null && r.Screen.IsAncestorOf(f) && !r.Screen.PauseMenu!.IsAncestorOf(f), $"{stop}/{i}: focus back on the stop: {f?.Name}");
				}
				Assert.True(r.Left == 0 && r.Titles == 0 && r.Restarts == 0 && r.Completed == 0 && Take(s) == snap, $"{stop}: nothing leaked to the stop or the run");
				Assert.Equal(1, r.Screen.GetChildren().OfType<PauseMenu>().Count(), $"{stop}: one pause menu, reused");
			}
			finally { Close(runner, r); }
		}
	});

	// Esc inside the pause's confirm box, settings sheet and move list closes that layer only, never the stop.
	[Test]
	public static void Pause_EscInsideConfirmSettingsAndMoveList_ClosesThatLayerOnly_StopStaysEntered(Node runner) => OnFixtures(() =>
	{
		var s = AtNode("r4-merchant"); var r = Open(runner, s);
		try
		{
			var snap = Take(s); var pm = (Func<PauseMenu>)(() => r.Screen.PauseMenu!);
			Tap(runner, Key.P);
			Press(pm().QuitButton); Assert.True(pm().ConfirmBox.Visible, "quit asks first");
			Tap(runner, Key.Escape);
			Assert.True(!pm().ConfirmBox.Visible && r.Screen.Paused && r.Titles == 0 && r.Left == 0, "Esc closes the confirm box only");
			Press(pm().RestartButton); Assert.True(pm().ConfirmBox.Visible, "restart asks first");
			Pad(runner, JoyButton.B);
			Assert.True(!pm().ConfirmBox.Visible && r.Screen.Paused && r.Restarts == 0 && r.Left == 0, "pad B closes the confirm box only");
			Press(pm().SettingsButton); Assert.True(pm().Settings.Visible, "settings open");
			Tap(runner, Key.Escape);
			Assert.True(!pm().Settings.Visible && r.Screen.Paused && r.Left == 0 && s.Node != null, "Esc closes the settings sheet only");
			Press(pm().MoveListButton); Assert.True(pm().MoveList.Visible, "move list open");
			Tap(runner, Key.Escape);
			Assert.True(!pm().MoveList.Visible && r.Screen.Paused && r.Left == 0 && s.Node != null, "Esc closes the move list only");
			Tap(runner, Key.Escape);
			Assert.True(!r.Screen.Paused && r.Left == 0 && r.Completed == 0 && Take(s) == snap, "the next Esc resumes; the stop is still entered");
		}
		finally { Close(runner, r); }
	});

	// Keys under the pause never reach the stop: not the cards, the detail toggle, the trial selector, nor the bumpers.
	[Test]
	public static void Pause_KeysNeverReachTheStop_CardsDetailSelector(Node runner) => OnFixtures(() =>
	{
		var s = DojoTrialTests.AtDojo(640); var r = Open(runner, s);
		try
		{
			var snap = Take(s); int pick = r.Screen.Model!.SelectedIndex;
			Assert.True(r.Screen.Model.CanSelect, "the dojo offers a selector");
			Tap(runner, Key.P);
			foreach (var k in new[] { Key.Down, Key.Up, Key.Left, Key.Right, Key.A, Key.D, Key.Tab, Key.Q, Key.E })
			{
				Tap(runner, k);
				Assert.True(r.Screen.Paused, $"{k}: the pause is still open");
			}
			foreach (var b in new[] { JoyButton.DpadLeft, JoyButton.DpadRight, JoyButton.LeftShoulder, JoyButton.RightShoulder, JoyButton.Y, JoyButton.X })
				Pad(runner, b);
			Assert.True(r.Screen.Paused && !r.Screen.DetailVisible && r.Screen.Model.SelectedIndex == pick, "no detail toggle, no selector move under the pause");
			Assert.True(r.Left == 0 && Take(s) == snap, "the run is untouched");
			Assert.True(r.Screen.Model.Kind == RestStopKind.Dojo && !r.Screen.Model.ChoiceMade, "no choice made under the pause");
		}
		finally { Close(runner, r); }
	});

	// The pause summary on odd runs: nothing owned, one special, an empty slot between two filled ones; a stop already chosen.
	[Test]
	public static void Pause_OnOddRuns_NoSpecialsOneSpecialGapBetween_OpensAndResumes(Node runner) => OnFixtures(() =>
	{
		var walked = AtNode("r4-merchant", 500);
		var owned = walked.Run.Owned.ToList();
		Assert.True(owned.Count >= 3, "the walked run owns three specials");
		var cases = new (string, IEnumerable<(SpecialSlot, OwnedSpecial)>)[]
		{
			("none", Array.Empty<(SpecialSlot, OwnedSpecial)>()),
			("one", new[] { owned[0] }),
			("gap", new[] { owned[0], owned[2] }),
		};
		foreach (var (name, slots) in cases)
		{
			var run = RunState.Restore(500, slots.ToList());
			run.SetMap(walked.Map);
			var bare = RunSession.Restore(run, walked.Pool, NodeSceneTable.Default(MapDir), 5, false, null);
			var r = Open(runner, bare);
			try
			{
				Assert.True(r.Screen.OpenPause() && r.Screen.Paused, $"{name}: the pause opens");
				Tap(runner, Key.Escape);
				Assert.True(!r.Screen.Paused && r.Left == 0 && bare.Node != null, $"{name}: Esc resumes, the stop is not left");
				Assert.True(r.Screen.TakeFree(), $"{name}: the stop still works after the pause");
				Assert.True(r.Screen.OpenPause(), $"{name}: and the pause opens again on a chosen stop");
			}
			finally { Close(runner, r); }
		}
	});

	// The strip names the dojo as well as the merchant (author tests the merchant only).
	[Test]
	public static void Pause_RunStrip_NamesTheDojo_AndTheRow(Node runner) => OnFixtures(() =>
	{
		var s = AtNode("r7-dojo", 640); var r = Open(runner, s);
		try
		{
			Tap(runner, Key.P);
			Assert.Equal($"Row 7 of {s.Steps} · Dojo", UiFind.Get<Label>(r.Screen.PauseMenu!, "RunSub").Text, "the run strip");
		}
		finally { Close(runner, r); }
	});

	// Held keys and releases are not presses: an Esc auto-repeating from the previous screen, or a bare release, neither leaves nor pauses.
	[Test]
	public static void Input_EchoAndReleaseOnly_EscBPStart_DoNothing(Node runner) => OnFixtures(() =>
	{
		var s = AtNode("r4-merchant"); var r = Open(runner, s);
		try
		{
			var snap = Take(s);
			foreach (var k in new[] { Key.Escape, Key.P })
			{
				Push(runner, new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true, Echo = true });
				Push(runner, new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
			}
			foreach (var b in new[] { JoyButton.B, JoyButton.Start }) Push(runner, new InputEventJoypadButton { ButtonIndex = b, Pressed = false });
			Assert.True(r.Left == 0 && r.Titles == 0 && !r.Screen.Paused && Take(s) == snap, "echo and release events do nothing");
		}
		finally { Close(runner, r); }
	});

	// Footer (720p = the 1080p design scaled 2/3): the selector variant's six chips stay on the parchment, on both devices.
	[Test]
	public static void Footer_DojoSelectorSixChips_FitTheScreenAtBothDevices(Node runner) => OnFixtures(() =>
	{
		var s = DojoTrialTests.AtDojo(640); var r = Open(runner, s);
		try
		{
			Assert.True(r.Screen.Model!.CanSelect, "the selector variant");
			var footer = r.Screen.GetNode<KeyChipFooter>("Footer");
			Assert.Equal(6, footer.ChipTexts().Length, "six chips");
			foreach (var dev in new[] { InputDevice.Keyboard, InputDevice.Pad })
			{
				InputGlyphs.Set(dev);
				float right = footer.GlobalPosition.X + footer.GetCombinedMinimumSize().X;
				Assert.True(right <= 1920 - 150, $"{dev}: footer content ends at {right}px of the 1920 design width (left margin 150)");
			}
		}
		finally { InputGlyphs.Set(InputDevice.Keyboard); Close(runner, r); }
	});

	// ---- boot flow ----

	static GameFlow NewFlow(Node runner, string file, uint? seed)
	{
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { }; flow.FixedRunSeed = seed; flow.SaveFile = file;
		flow.ConfigureFight = f => f.ExternalDrive = true;
		runner.AddChild(flow);
		return flow;
	}

	static void Drop(Node runner, GameFlow flow) { runner.RemoveChild(flow); flow.QueueFree(); }

	static void WithFlow(Node runner, uint? seed, Action<GameFlow, string> body)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-leavebreak-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		GameFlow? flow = null;
		try
		{
			flow = NewFlow(runner, Path.Combine(dir, "run.dat"), seed);
			flow.Title.StartButton.EmitSignal(BaseButton.SignalName.Pressed);
			flow.ControlSelect.Start.EmitSignal(BaseButton.SignalName.Pressed);
			body(flow, dir);
		}
		finally
		{
			if (flow != null && GodotObject.IsInstanceValid(flow) && flow.GetParent() == runner) Drop(runner, flow);
			MenuSettings.Reset();
			FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe;
			try { Directory.Delete(dir, true); } catch (IOException) { }
		}
	}

	static RestStopScreen WalkTo(GameFlow flow, string nodeId)
	{
		var s = flow.Session!;
		for (; ; )
		{
			string id = s.Reachable().First(n => Leads(s, n, nodeId));
			Assert.True(flow.ChooseNode(id).Ok, "entered " + id);
			if (id == nodeId) return (RestStopScreen)flow.NodeScene!;
			if (flow.NodeScene is RestStopScreen early) Assert.True(early.TakeFree() && early.Leave(), "a stop passed");
			else Finish(s, 300);
		}
	}

	static void LeaveThenReload(Node runner, string stop) => WithFlow(runner, 77, (flow, dir) =>
	{
		string file = Path.Combine(dir, "run.dat");
		WalkTo(flow, stop); var s = flow.Session!;
		Tap(flow, Key.Escape);
		Assert.True(flow.Router.Current == Screen.Map && s.Node == null && !flow.GetTree().Paused, $"{stop}: on the map after Esc");
		ulong h = s.StateHash(); int hp = s.Run.Health, coins = s.Run.Coins; string slots = Slots(s); string reach = string.Join(",", s.Reachable()); uint seed = s.Seed;
		Drop(runner, flow);
		var again = NewFlow(runner, file, 77);
		try
		{
			Assert.True(again.ContinueRun(), $"{stop}: Continue from the file");
			var b = again.Session!;
			Assert.True(again.Router.Current == Screen.Map && again.NodeScene == null && !b.Map.InNode, $"{stop}: Continue lands on the map");
			Assert.Equal(reach, string.Join(",", b.Reachable()), $"{stop}: the next-row choice");
			Assert.True(b.Run.Health == hp && b.Run.Coins == coins && Slots(b) == slots && b.Seed == seed, $"{stop}: health, coins, slots, seed");
			Assert.Equal(h, b.StateHash(), $"{stop}: the file's run hashes as the run that left");
			Assert.True(!again.ChooseNode(stop).Ok, $"{stop}: the spent stop cannot be re-entered after Continue (R1)");
			Assert.True(again.ChooseNode(b.Reachable()[0]).Ok, $"{stop}: the next row can be entered");
		}
		finally { Drop(runner, again); }
	});

	// M5: the leave is saved to disk; a fresh flow reading the file lands on the next-row choice with the same state, and the stop cannot be re-entered.
	[Test]
	public static void Boot_MerchantLeaveSavesToDisk_FreshFlowContinuesAtNextRow_SameHash(Node runner) => LeaveThenReload(runner, "r4-merchant");

	[Test]
	public static void Boot_DojoLeaveSavesToDisk_FreshFlowContinuesAtNextRow_SameHash(Node runner) => LeaveThenReload(runner, "r7-dojo");

	// M5: Quit to title after each kind of choice; Continue brings the stop back unspent with entry health; Esc then leaves it with no heal.
	[Test]
	public static void Boot_PauseQuit_AfterShopRestOrHeal_ContinueIsUnspent_ThenEscLeavesWithEntryHealth(Node runner) => WithFlow(runner, 77, (flow, dir) =>
	{
		var screen = WalkTo(flow, "r4-merchant"); var s = flow.Session!; int entry = s.Run.Health;
		Assert.True(screen.OpenSecond() && flow.Shop != null, "shop open");
		Tap(flow, Key.Escape);
		Assert.True(flow.Shop == null && screen.Visible && screen.Model!.ChoiceMade, "back on the stop, shop chosen");
		Tap(flow, Key.P);
		Assert.True(screen.Paused, "the pause is open on a stop whose shop was chosen");
		Press(screen.PauseMenu!.QuitButton); Press(Yes(screen.PauseMenu));
		Assert.True(flow.Router.Current == Screen.Title, "Quit to title");
		Assert.True(flow.ContinueRun() && flow.NodeScene is RestStopScreen, "Continue");
		var again = (RestStopScreen)flow.NodeScene!;
		Assert.True(!again.Model!.ChoiceMade && flow.Session!.Merchant!.Choice == MerchantChoice.None && flow.Session.Run.Health == entry, "the stop is unspent: no choice, entry health (M5)");
		Tap(flow, Key.Escape);
		Assert.True(flow.Router.Current == Screen.Map && flow.Session!.Run.Health == entry && flow.Session.Map.Visited.Last() == "r4-merchant", "Esc leaves it: spent, no heal");
	});

	// M5 / R7: a rested dojo, quit from the pause, Continue: unspent, entry health, rest available again.
	[Test]
	public static void Boot_PauseQuit_AfterRest_DojoContinuesUnspent(Node runner) => WithFlow(runner, 77, (flow, dir) =>
	{
		var screen = WalkTo(flow, "r7-dojo"); var s = flow.Session!; int entry = s.Run.Health;
		Assert.True(screen.TakeFree() && s.Run.Health > entry, "rested");
		Tap(flow, Key.P);
		Press(screen.PauseMenu!.QuitButton); Press(Yes(screen.PauseMenu));
		Assert.True(flow.ContinueRun() && flow.NodeScene is RestStopScreen, "Continue");
		var again = (RestStopScreen)flow.NodeScene!;
		Assert.True(!again.Model!.ChoiceMade && flow.Session!.Dojo!.CanRest && flow.Session.Run.Health == entry, "the dojo is unspent with entry health");
		Assert.True(again.Model.CanLeaveToMap, "and Esc / B can still leave it");
	});

	// M5 / R1 / E23: Restart run from the stop's pause: a new seed, a new walk, coins and slots reset, the old save replaced (Continue gives the new run).
	[Test]
	public static void Boot_PauseRestart_FromEitherStop_FreshSeedAndState_OldSaveReplaced(Node runner) => WithFlow(runner, null, (flow, dir) =>
	{
		foreach (string stop in new[] { "r4-merchant" })
		{
			var screen = WalkTo(flow, stop); var old = flow.Session!;
			uint oldSeed = old.Seed; Assert.True(old.Run.Coins > 0 && old.Map.Visited.Count >= 3, "the old run has progress");
			Assert.True(screen.TakeFree(), "healed before restarting");
			Tap(flow, Key.P);
			Press(screen.PauseMenu!.RestartButton); Press(Yes(screen.PauseMenu));
			Assert.True(flow.Router.Current == Screen.Map && flow.NodeScene == null, $"{stop}: on the map");
			var fresh = flow.Session!;
			Assert.True(!ReferenceEquals(fresh, old), $"{stop}: a new session");
			Assert.True(fresh.Seed != oldSeed, $"{stop}: E23 a new run draws a new seed ({fresh.Seed} vs {oldSeed})");
			Assert.True(fresh.Map.Visited.Count == 0 && !fresh.Map.InNode && fresh.Run.Coins == 0 && fresh.Run.Health == RunState.MaxHealth, $"{stop}: no walk, no coins, full health");
			Assert.True(!fresh.CopyLocked && fresh.Run.Owned.All(o => o.Special.Level >= 1 && o.Special.Data.Starter), $"{stop}: starters only, copy unlocked");
			Assert.True(!flow.GetTree().Paused, $"{stop}: the tree is not left paused");
			ulong hFresh = fresh.StateHash(); string reach = string.Join(",", fresh.Reachable());
			Assert.True(flow.ReturnToTitle() && flow.ContinueRun(), $"{stop}: quit and Continue");
			var back = flow.Session!;
			Assert.True(back.Seed == fresh.Seed && back.Map.Visited.Count == 0 && string.Join(",", back.Reachable()) == reach && back.StateHash() == hFresh, $"{stop}: the save holds the new run, not the old stop");
		}
	});
}
