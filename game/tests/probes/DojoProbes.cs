using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-84 probes (R7 rest, C1 cap). Fixtures only.</summary>
public static class DojoProbes
{
	const string DojoNode = "r7-dojo";
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static string MapDir => ContentPaths.Data("map");

	static RunSession Make(MapGraph g, uint seed = 84) =>
		RunSession.Start(AbilityPool.LoadFixtures(RepoRoot), g, NodeSceneTable.Default(MapDir), seed);

	static RunSession NewSession(uint seed = 84) => Make(FileMapSource.Default(MapDir).Load(), seed);

	static void WinAndLeave(RunSession s, int healthLeft)
	{
		var draft = s.WinFight(healthLeft);
		if (draft is { Picked: false })
		{
			var card = draft.Offer.Cards[0];
			Assert.True(draft.Pick(0, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, "picked");
		}
		Assert.True(s.CompleteNode().Ok, "completed");
	}

	/// <summary>Walks until the next reachable node is on row <paramref name="row"/> (does not enter it).</summary>
	static void WalkTo(RunSession s, int row)
	{
		while (s.Step < row - 1)
		{
			Assert.True(s.Enter(s.Reachable()[0]).Ok, "walk enter");
			WinAndLeave(s, 500);
		}
	}

	static RunSession AtDojo(int health, uint seed = 84)
	{
		var s = NewSession(seed);
		WalkTo(s, 7);
		Assert.True(s.Enter(DojoNode).Ok, "dojo entered");
		s.Run.SetHealth(health);
		return s;
	}

	static bool Throws<T>(Action a) where T : Exception { try { a(); } catch (T) { return true; } return false; }

	static void Unchanged(RunSession s, Dojo d, string when, int health, ulong hash)
	{
		var r = d.Rest();
		Assert.True(!r.Ok, $"{when}: rest refused");
		Assert.True(Throws<InvalidOperationException>(() => d.Restore(DojoChoice.Rest)), $"{when}: restore throws");
		Assert.Equal(health, s.Run.Health, $"{when}: health");
		Assert.Equal(hash, s.StateHash(), $"{when}: hash");
	}

	// ---- 1. stale references (R7); author covers rest on map / next node / lost fight, not Restore after loss ----
	[Test]
	public static void Stale_AfterLostFight_RestoreThrows_AndNothingMoves()
	{
		var s = AtDojo(400);
		var d = s.Dojo!;
		s.LoseFight();
		Unchanged(s, d, "after loss", 400, s.StateHash());
		Assert.True(s.Dojo is null, "no stop on a finished run");
	}

	[Test]
	public static void Stale_OnMapAndNextNode_RestoreThrows_HashSame()
	{
		var s = AtDojo(400);
		var d = s.Dojo!;
		s.WinFight(400); s.CompleteNode();
		Unchanged(s, d, "map", 400, s.StateHash());
		s.Enter("r8-elder-kappa");
		Unchanged(s, d, "next node", s.Run.Health, s.StateHash());
	}

	// ---- 2. handlers (R7) ----
	[Test]
	public static void Handler_RestInNodeCompleted_IsRefused()
	{
		var s = AtDojo(400);
		var d = s.Dojo!;
		MapResult? got = null;
		s.NodeCompleted += _ => got = d.Rest();
		s.WinFight(400); s.CompleteNode();
		Assert.True(got is { Ok: false }, "rest from NodeCompleted handler refused");
		Assert.Equal(400, s.Run.Health, "no heal");
	}

	[Test]
	public static void Handler_RestInRunEnded_IsRefused()
	{
		var s = AtDojo(400);
		var d = s.Dojo!;
		MapResult? got = null;
		s.RunEnded += _ => got = d.Rest();
		s.LoseFight();
		Assert.True(got is { Ok: false }, "rest from RunEnded handler refused");
		Assert.Equal(400, s.Run.Health, "no heal");
	}

	// ---- 3. Restore edge cases ----
	[Test]
	public static void Restore_None_LeavesStopRestorable_RestoreRestThenNone_StaysSpent()
	{
		var s = AtDojo(400);
		var d = s.Dojo!;
		ulong h0 = s.StateHash();
		d.Restore(DojoChoice.None);
		Assert.True(d.CanRest && s.StateHash() == h0, "Restore(None) is a no-op");
		d.Restore(DojoChoice.Rest);
		Assert.True(Throws<InvalidOperationException>(() => d.Restore(DojoChoice.None)), "no reopening");
		Assert.True(!d.Rest().Ok && s.Run.Health == 400 && d.Choice == DojoChoice.Rest, "still spent, no heal");
	}

	[Test]
	public static void Restore_UndefinedValue_Throws_ChangesNothing()
	{
		var s = AtDojo(400);
		var d = s.Dojo!;
		ulong h = s.StateHash();
		Assert.True(Throws<ArgumentException>(() => d.Restore((DojoChoice)7)), "undefined choice rejected");
		Assert.True(d.Choice == DojoChoice.None && d.CanRest && s.StateHash() == h, "unchanged");
	}

	// ---- 4. ownerless stop: same exposure as public RunState.SetHealth (health is already freely settable) ----
	[Test]
	public static void KnownOpen_HandBuiltDojo_HealsAnywhereAndRepeatedly()
	{
		// Confirms: a Dojo built with no owner check is always open. Not a new hole: RunState.SetHealth is public too.
		// Flip if the designer wants Dojo's constructor internal to RunSession.
		var s = AtDojo(400);
		s.WinFight(400); s.CompleteNode();
		s.Enter("r8-elder-kappa");
		s.Run.SetHealth(400);
		for (int i = 0; i < 3; i++) Assert.True(new Dojo(s.Run, "anything").Rest().Ok, "ownerless rest");
		Assert.Equal(1000, s.Run.Health, "400 -> 1000 inside an elder node");
	}

	// ---- 5. save gap ----
	[Test]
	public static void KnownOpen_SessionRebuiltInRestedDojo_RestsAgain_NeedsSaveTicket()
	{
		// Today: a rebuilt session without Restore rests again (400 -> 700 -> 1000). Flip to refusal once the
		// save ticket calls Restore(saved choice) when it rebuilds.
		var s = AtDojo(400);
		s.Dojo!.Rest();
		Assert.Equal(700, s.Run.Health, "first rest");
		var s2 = new RunSession(s.Run, s.Pool, s.Scenes, s.Seed);
		Assert.True(s2.Dojo is not null && s2.Dojo.CanRest, "rebuilt stop is open");
		Assert.True(s2.Dojo!.Rest().Ok, "rests again");
		Assert.Equal(1000, s.Run.Health, "700 -> 1000");
	}

	[Test]
	public static void RebuiltSession_WithRestore_RefusesSecondRest_AndHashMatches()
	{
		var s = AtDojo(400);
		s.Dojo!.Rest();
		var s2 = new RunSession(s.Run, s.Pool, s.Scenes, s.Seed);
		s2.Dojo!.Restore(DojoChoice.Rest);
		Assert.True(!s2.Dojo.Rest().Ok && s.Run.Health == 700, "refused");
		Assert.Equal(s.StateHash(), s2.StateHash(), "same hash as the original");
	}

	// ---- 6. two sessions over one RunState ----
	[Test]
	public static void KnownOpen_TwoSessionsOverOneRun_EachRestsOnce()
	{
		// Today each session owns its own stop, so the rest can be taken once through each. Misuse of the API
		// (one session per run is the contract); flip only if RunState ever owns the choice.
		var s = AtDojo(100);
		var s2 = new RunSession(s.Run, s.Pool, s.Scenes, s.Seed);
		Assert.True(s.Dojo!.Rest().Ok, "first");
		Assert.True(s2.Dojo!.Rest().Ok, "second through the other session");
		Assert.Equal(700, s.Run.Health, "100 + 300 + 300");
	}

	// ---- 7. stand-in fight ----
	[Test]
	public static void KnownOpen_StandInFight_RestThenWinOverwritesHeal()
	{
		// R7/C1: health carries between fights (C1). The stand-in dojo fight reports 300 left after a rest to 700:
		// carry-over is the fight's result, so the heal is overwritten and the choice is spent. Artefact of the
		// stand-in fight; remove when the rest-stop screen (YOK-96) replaces the duel at the dojo.
		var s = AtDojo(400);
		var d = s.Dojo!;
		Assert.True(d.Rest().Ok, "rest");
		s.WinFight(300);
		Assert.Equal(300, s.Run.Health, "heal overwritten by the stand-in fight result");
		Assert.True(d.Choice == DojoChoice.Rest && d.Healed == 300, "choice spent");
	}

	// ---- own: boundaries (C1) ----
	[Test]
	public static void Rest_BoundaryHealths()
	{
		int[] from = { 0, 1, 699, 700, 701, 999, 1000 };
		int[] want = { 300, 301, 999, 1000, 1000, 1000, 1000 };
		for (int i = 0; i < from.Length; i++)
		{
			var s = AtDojo(from[i]);
			Assert.True(s.Dojo!.Rest().Ok, $"rest at {from[i]}");
			Assert.Equal(want[i], s.Run.Health, $"health after rest from {from[i]}");
			Assert.Equal(want[i] - from[i], s.Dojo.Healed, $"healed from {from[i]}");
		}
	}

	// ---- own: hashes ----
	[Test]
	public static void Hash_IdenticalUnrestedSessionsMatch_RestAtFullStillChangesIt()
	{
		var a = AtDojo(1000); var b = AtDojo(1000);
		Assert.Equal(a.StateHash(), b.StateHash(), "identical sessions");
		ulong before = a.StateHash();
		a.Dojo!.Rest();
		Assert.True(a.Run.Health == 1000 && a.StateHash() != before, "rest at full health changes hash (choice made)");
	}

	[Test]
	public static void Hash_AfterCompleteNode_DropsTheChoice_MatchesUnrestedWalkOnlyIfHealthEqual()
	{
		var a = AtDojo(1000); var b = AtDojo(1000);
		a.Dojo!.Rest();
		a.WinFight(1000); a.CompleteNode();
		b.WinFight(1000); b.CompleteNode();
		Assert.Equal(b.StateHash(), a.StateHash(), "choice leaves the hash with the stop (state equal otherwise)");
	}

	[Test]
	public static void Enter_RefusedAtDojo_KeepsSameStopAndState()
	{
		var s = AtDojo(400);
		var d = s.Dojo!;
		d.Rest();
		ulong h = s.StateHash();
		Assert.True(!s.Enter("r8-elder-kappa").Ok, "enter while in a node refused");
		Assert.True(ReferenceEquals(d, s.Dojo) && d.Choice == DojoChoice.Rest && s.StateHash() == h, "same stop, same hash");
		Assert.True(!s.Enter(DojoNode).Ok && ReferenceEquals(d, s.Dojo), "re-enter refused, stop kept");
	}

	[Test]
	public static void NonDojoNodes_HaveNoStop()
	{
		var s = NewSession();
		WalkTo(s, 8);
		Assert.True(s.Dojo is null, "map");
		Assert.True(s.Enter(s.Reachable()[0]).Ok && s.Dojo is null, "non-dojo node has no stop");
	}

	[Test]
	public static void TwoDojoNodes_EachGivesOneRest()
	{
		var baseG = FileMapSource.Default(MapDir).Load();
		var types = FileMapSource.Default(MapDir).Types;
		var nodes = baseG.Nodes.Select(n => n.Id == "r5-elder-oni" || n.Id == "r5-nine-tails" ? n with { Type = "dojo", Elder = false, Yokai = null } : n).ToList();
		var g = new MapGraph("two-dojos", nodes, baseG.Routes, types);
		var s = Make(g);
		WalkTo(s, 5);
		Assert.True(s.Enter("r5-elder-oni").Ok && s.Dojo is not null, "first dojo");
		s.Run.SetHealth(100);
		var d1 = s.Dojo!;
		Assert.True(d1.Rest().Ok, "rest 1");
		s.WinFight(s.Run.Health); s.CompleteNode();
		WalkTo(s, 7);
		Assert.True(s.Enter(DojoNode).Ok, "second dojo");
		var d2 = s.Dojo!;
		Assert.True(!ReferenceEquals(d1, d2) && d2.CanRest, "fresh stop");
		int h = s.Run.Health;
		Assert.True(d2.Rest().Ok && s.Run.Health == Math.Min(1000, h + 300), "second rest works");
		Assert.True(!d2.Rest().Ok, "but only once");
	}

	[Test]
	public static void Determinism_SameSeedAndInputs_SameHashSequence()
	{
		ulong[] Run(uint seed)
		{
			var s = AtDojo(333, seed);
			var l = new System.Collections.Generic.List<ulong> { s.StateHash() };
			s.Dojo!.Rest(); l.Add(s.StateHash());
			s.WinFight(s.Run.Health); l.Add(s.StateHash());
			s.CompleteNode(); l.Add(s.StateHash());
			return l.ToArray();
		}
		Assert.True(Run(5).SequenceEqual(Run(5)), "same seed, same hashes");
		var a = AtDojo(333); var b = AtDojo(333); b.Dojo!.Rest();
		Assert.True(a.StateHash() != b.StateHash(), "a different choice hashes differently");
	}
}
