using System;
using System.IO;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-84, R7: the dojo's rest. Sim only: a <see cref="RunSession"/> on the default map (data/map/), walked to the
/// row-7 dojo with no fight scene (each duel is reported won), abilities from the TEST FIXTURES only
/// (<see cref="AbilityPool.LoadFixtures"/>).
/// </summary>
public static class DojoTests
{
	const string DojoNode = "r7-dojo";

	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static string MapDir => ContentPaths.Data("map");

	static RunSession NewSession(uint seed = 84) =>
		RunSession.Start(AbilityPool.LoadFixtures(RepoRoot), FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir), seed);

	/// <summary>Wins the entered node with <paramref name="healthLeft"/>, takes the first card if cards are offered, completes it.</summary>
	static void WinAndLeave(RunSession s, int healthLeft)
	{
		var draft = s.WinFight(healthLeft);
		if (draft is { Picked: false })
		{
			var card = draft.Offer.Cards[0];
			Assert.True(draft.Pick(0, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, $"{s.Node!.NodeId}: picked {card.AbilityId}");
		}
		Assert.True(s.CompleteNode().Ok, "node completed");
	}

	/// <summary>A run walked through rows 1-6 and standing in the row-7 dojo with <paramref name="health"/>.</summary>
	static RunSession AtDojo(int health, uint seed = 84)
	{
		var s = NewSession(seed);
		for (int row = 1; row <= 6; row++)
		{
			Assert.True(s.Enter(s.Reachable()[0]).Ok, $"row {row} entered");
			Assert.True(s.Dojo is null, $"row {row} ({s.Node!.Type}) is not a dojo stop");
			WinAndLeave(s, 500);
		}
		Assert.True(s.Enter(DojoNode).Ok, "the dojo is reachable from row 6 (R1)");
		s.Run.SetHealth(health);
		return s;
	}

	static bool Throws<T>(Action body) where T : Exception
	{
		try { body(); }
		catch (T) { return true; }
		return false;
	}

	// ---- R7: rest heals 30% ----

	[Test]
	public static void Rest_Heals30PercentOfFullHealth()
	{
		Assert.Equal(300, Dojo.RestHeal, "R7: 30% of Ryo's 1,000 (C1)");
		var s = AtDojo(400);
		var dojo = s.Dojo!;
		Assert.True(dojo.NodeId == DojoNode && dojo.Choice == DojoChoice.None && dojo.CanRest && dojo.Healed == 0, "the stop opens with its choice unmade");
		Assert.True(dojo.Rest().Ok, "Ryo rests");
		Assert.Equal(700, s.Run.Health, "R7: 400 + 30% of 1,000");
		Assert.Equal(300, dojo.Healed, "what the rest gave back");
		Assert.Equal(DojoChoice.Rest, dojo.Choice, "the choice is recorded");

		// the heal is a share of full health, not of what Ryo has left
		var low = AtDojo(1);
		low.Dojo!.Rest();
		Assert.Equal(301, low.Run.Health, "R7: 1 + 300");
		var mid = AtDojo(700);
		mid.Dojo!.Rest();
		Assert.Equal(1000, mid.Run.Health, "R7: 700 + 300 is exactly full");
	}

	[Test]
	public static void Rest_IsCappedAtFullHealth()
	{
		var s = AtDojo(850);
		Assert.True(s.Dojo!.Rest().Ok, "Ryo rests");
		Assert.Equal(RunState.MaxHealth, s.Run.Health, "capped at 1,000 (C1)");
		Assert.Equal(150, s.Dojo!.Healed, "only the missing health is given back");

		var full = AtDojo(RunState.MaxHealth);
		Assert.True(full.Dojo!.Rest().Ok, "resting at full health is allowed");
		Assert.True(full.Run.Health == RunState.MaxHealth && full.Dojo!.Healed == 0, "and heals nothing");
		Assert.True(!full.Dojo!.CanRest, "but it still was the stop's choice (R7)");
	}

	// ---- R7: once, as the stop's one choice ----

	[Test]
	public static void Rest_IsOncePerStop_ASecondRestChangesNothing()
	{
		var s = AtDojo(200);
		Assert.True(s.Dojo!.Rest().Ok, "first rest");
		ulong hash = s.StateHash();
		var again = s.Dojo!.Rest();
		Assert.True(!again.Ok && again.Error!.Contains("R7"), "the second rest is refused, naming the rule");
		Assert.Equal(500, s.Run.Health, "health is healed once: 200 + 300");
		Assert.Equal(300, s.Dojo!.Healed, "the first rest's figure stands");
		Assert.Equal(hash, s.StateHash(), "a refused rest changes nothing");
		Assert.True(!s.Dojo!.CanRest, "the choice is spent");
	}

	/// <summary>A refused call on a kept stop: not Ok, and health and the session's hash exactly as before.</summary>
	static void RefusesUnchanged(RunSession s, Dojo kept, string when)
	{
		int health = s.Run.Health;
		ulong hash = s.StateHash();
		Assert.True(!kept.IsOpen && !kept.CanRest, $"{when}: the kept stop is closed");
		var r = kept.Rest();
		Assert.True(!r.Ok && r.Error!.Contains("R7"), $"{when}: the rest is refused, naming the rule");
		Assert.True(Throws<InvalidOperationException>(() => kept.Restore(DojoChoice.Rest)), $"{when}: a restore is refused too");
		Assert.Equal(health, s.Run.Health, $"{when}: health unchanged");
		Assert.Equal(hash, s.StateHash(), $"{when}: state hash unchanged");
		Assert.True(kept.Choice == DojoChoice.None && kept.Healed == 0, $"{when}: the stop itself is unchanged");
	}

	[Test]
	public static void StaleStop_KeptPastTheNode_RefusesOnTheMap()
	{
		var s = AtDojo(400);
		var kept = s.Dojo!;
		Assert.True(s.WinFight(400) is null && s.CompleteNode().Ok, "the dojo node is left unrested");
		RefusesUnchanged(s, kept, "back on the map");
		Assert.Equal(400, s.Run.Health, "no heal off the stop");
	}

	[Test]
	public static void StaleStop_KeptIntoTheNextNode_Refuses()
	{
		var s = AtDojo(400);
		var kept = s.Dojo!;
		s.WinFight(400);
		s.CompleteNode();
		Assert.True(s.Enter("r8-elder-kappa").Ok, "row 8 entered");
		RefusesUnchanged(s, kept, "inside the row-8 elder");
		Assert.Equal(400, s.Run.Health, "no heal inside another node");
	}

	[Test]
	public static void StaleStop_KeptPastALostFight_Refuses()
	{
		var s = AtDojo(250);
		var kept = s.Dojo!;
		Assert.True(s.LoseFight().Ok && s.IsOver, "the run ends at the dojo");
		RefusesUnchanged(s, kept, "run over");
		Assert.Equal(250, s.Run.Health, "no heal on an ended run");
	}

	[Test]
	public static void Rest_AfterTheStandInFight_StillCountsOnce()
	{
		var s = AtDojo(900);
		Assert.True(s.WinFight(401) is null, "the stand-in fight is won first");
		Assert.True(s.Dojo!.Rest().Ok, "resting after the fight, before leaving, is the stop's choice");
		Assert.Equal(701, s.Run.Health, "R7: 401 + 300");
		Assert.True(!s.Dojo!.Rest().Ok && s.Run.Health == 701, "once");
		var over = AtDojo(701);
		over.Dojo!.Rest();
		Assert.True(over.Run.Health == RunState.MaxHealth && over.Dojo!.Healed == 299, "701 + 300 is capped: 299 given back");
	}

	[Test]
	public static void Restore_CannotTakeAChoiceBack()
	{
		var s = AtDojo(100);
		s.Dojo!.Rest();
		ulong hash = s.StateHash();
		Assert.True(Throws<InvalidOperationException>(() => s.Dojo!.Restore(DojoChoice.None)), "a made choice can't be restored away (R7)");
		Assert.True(Throws<InvalidOperationException>(() => s.Dojo!.Restore(DojoChoice.Rest)), "nor restored over");
		Assert.True(!s.Dojo!.Rest().Ok && s.Run.Health == 400 && s.Dojo!.Healed == 300, "the stop stays spent: 100 + 300, once");
		Assert.Equal(hash, s.StateHash(), "nothing changed");

		var loaded = AtDojo(400);
		loaded.Dojo!.Restore(DojoChoice.Rest);
		Assert.True(Throws<InvalidOperationException>(() => loaded.Dojo!.Restore(DojoChoice.None)) && !loaded.Dojo!.CanRest, "a restored choice can't be taken back either");
	}

	[Test]
	public static void RebuiltSession_InsideTheDojo_TakesTheSavedChoiceThroughRestore()
	{
		var played = AtDojo(400);
		var old = played.Dojo!;
		old.Rest();
		// a later Continue: a new session over the same run state, still inside the dojo node
		var rebuilt = new RunSession(played.Run, played.Pool, played.Scenes, played.Seed);
		Assert.True(rebuilt.Node!.NodeId == DojoNode && rebuilt.Dojo is { NodeId: DojoNode }, "the rebuilt session is at the dojo stop");
		Assert.True(rebuilt.Dojo!.Choice == DojoChoice.None, "the choice is saved state: it is not there until restored");
		rebuilt.Dojo!.Restore(old.Choice);
		Assert.True(!rebuilt.Dojo!.Rest().Ok && rebuilt.Run.Health == 700, "restored: no second rest (R7)");
		Assert.Equal(played.StateHash(), rebuilt.StateHash(), "the rebuilt session is the played one");
	}

	[Test]
	public static void UnrestedRun_HashesAsBeforeTheDojoExisted()
	{
		// the session hash of main: run state, seed, ended; a stop with no choice adds nothing to it
		var s = AtDojo(400);
		ulong r = s.Run.StateHash();
		ulong h = Fnv.Mix(Fnv.Offset, unchecked((int)r));
		h = Fnv.Mix(h, unchecked((int)(r >> 32)));
		h = Fnv.Mix(h, unchecked((int)s.Seed));
		h = Fnv.Mix(h, 0);
		Assert.Equal(h, s.StateHash(), "at the dojo, unrested: the hash recorded runs already have");
		s.WinFight(400);
		s.CompleteNode();
		r = s.Run.StateHash();
		h = Fnv.Mix(Fnv.Mix(Fnv.Mix(Fnv.Mix(Fnv.Offset, unchecked((int)r)), unchecked((int)(r >> 32))), unchecked((int)s.Seed)), 0);
		Assert.Equal(h, s.StateHash(), "after leaving unrested: the same");
	}

	[Test]
	public static void Rest_TouchesOnlyHealth_AndTheRunGoesOn()
	{
		var s = AtDojo(300);
		string Slots() => string.Join(",", System.Linq.Enumerable.Select(s.Run.Owned, o => $"{o.Slot}:{o.Special.Data.Id}:{o.Special.Level}:{o.Special.Modifier?.Id}"));
		string slots = Slots();
		int visited = s.Map.Visited.Count;
		s.Dojo!.Rest();
		Assert.True(Slots() == slots && s.Map.Visited.Count == visited && s.Node!.NodeId == DojoNode && s.Draft is null, "slots, map and node are as they were");

		// the stop is still a stand-in fight today (node-scenes.json): the rested health is what Ryo carries in (C1)
		Assert.Equal(600, s.Run.MatchConfig().P1StartHealth, "the next fight starts on the rested health");
		Assert.True(s.WinFight(600) is null && s.Run.Health == 600, "a dojo binds nothing: no R3 heal, no draft");
		Assert.True(s.CompleteNode().Ok && s.Dojo is null, "leaving the node closes the stop");
		Assert.True(s.Reachable().Count > 0 && s.Enter(s.Reachable()[0]).Ok && s.Dojo is null, "row 8 opens and is not a dojo");
		Assert.Equal(600, s.Run.Health, "the rest's health carries on (C1)");
	}

	// ---- the session opens and closes the stop ----

	[Test]
	public static void Session_HasADojoOnlyWhileADojoNodeIsEntered()
	{
		var s = NewSession();
		Assert.True(s.Dojo is null, "no stop on the map");
		for (int row = 1; row <= 6; row++)
		{
			s.Enter(s.Reachable()[0]);
			Assert.True(s.Dojo is null, $"row {row}: {s.Node!.Type} is not a dojo");
			WinAndLeave(s, 500);
		}
		var other = NewSession();
		for (int row = 1; row <= 6; row++) { other.Enter(other.Reachable()[0]); WinAndLeave(other, 500); }
		Assert.True(other.Enter("r7-merchant").Ok && other.Dojo is null, "the merchant in the same row is not a dojo");

		Assert.True(s.Enter(DojoNode).Ok && s.Dojo is { NodeId: DojoNode }, "entering the dojo opens its stop");
		Assert.True(!s.Enter(DojoNode).Ok && s.Dojo!.CanRest, "a refused pick leaves the stop alone");
		WinAndLeave(s, 500);
		Assert.True(s.Dojo is null, "completed: the stop is closed");
	}

	[Test]
	public static void Session_NoRestOnceTheRunIsOver()
	{
		var s = AtDojo(250);
		var dojo = s.Dojo!;
		Assert.True(s.LoseFight().Ok && s.IsOver, "Ryo loses the stand-in fight at the dojo");
		Assert.True(s.Dojo is null, "a finished run offers no dojo stop");
		Assert.True(!dojo.Rest().Ok, "and the stop held from before refuses the rest");
		Assert.Equal(DojoChoice.None, dojo.Choice, "and nothing was chosen");
		Assert.Equal(250, s.Run.Health, "health as it was");
	}

	// ---- determinism and saved state ----

	[Test]
	public static void Rest_IsDeterministic_AndTheChoiceIsRunState()
	{
		var a = AtDojo(400);
		var b = AtDojo(400);
		Assert.Equal(a.StateHash(), b.StateHash(), "same seed, same walk, same state");
		ulong unrested = a.StateHash();
		a.Dojo!.Rest();
		b.Dojo!.Rest();
		Assert.Equal(a.StateHash(), b.StateHash(), "same rest, same state");
		Assert.True(a.StateHash() != unrested, "resting changes the run's state");

		// at full health the heal is 0, but the spent choice still tells the two runs apart
		var full = AtDojo(RunState.MaxHealth);
		ulong before = full.StateHash();
		full.Dojo!.Rest();
		Assert.True(full.StateHash() != before, "a spent choice is state even when it healed nothing");
	}

	[Test]
	public static void Restore_PutsBackTheChoice_WithoutHealingAgain()
	{
		var played = AtDojo(400);
		played.Dojo!.Rest();

		var loaded = AtDojo(400);
		loaded.Run.SetHealth(played.Run.Health); // a save holds the health after the rest
		loaded.Dojo!.Restore(played.Dojo!.Choice);
		Assert.Equal(700, loaded.Run.Health, "restoring does not heal a second time");
		Assert.True(!loaded.Dojo!.CanRest && !loaded.Dojo!.Rest().Ok && loaded.Run.Health == 700, "the restored stop's choice is spent (R7)");
		Assert.Equal(played.StateHash(), loaded.StateHash(), "the restored run is the played run");

		var open = AtDojo(400);
		open.Dojo!.Restore(DojoChoice.None);
		Assert.True(open.Dojo!.CanRest, "a stop saved before the choice is still open");
		Assert.True(Throws<ArgumentException>(() => open.Dojo!.Restore((DojoChoice)7)) && open.Dojo!.CanRest, "an unknown choice is refused and changes nothing");
		Assert.True(open.Dojo!.Rest().Ok && open.Run.Health == 700, "and the open stop still rests");
	}

	[Test]
	public static void Dojo_OnItsOwn_NeedsOnlyTheRunState()
	{
		var run = RunState.NewRun(AbilityPool.LoadFixtures(RepoRoot));
		run.SetHealth(0);
		var dojo = new Dojo(run, "any");
		Assert.True(dojo.Rest().Ok && run.Health == Dojo.RestHeal, "from 0: exactly the rest's 30%");
		Assert.True(Throws<ArgumentNullException>(() => new Dojo(null!, "any")), "a stop needs a run");
		Assert.True(Throws<ArgumentNullException>(() => new Dojo(run, null!)), "and a node");
	}
}
