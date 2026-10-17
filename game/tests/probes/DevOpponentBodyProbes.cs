using System;
using System.Linq;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-135 (dev menu Oni/Kappa). Dev tool: no rule ID beyond determinism (sim untouched).</summary>
public static class DevOpponentBodyProbes
{
	// HasBody is exact: kitsune, tanuki, ryo, case variants, padding and unknown ids are not bodies; elders are.
	[Test]
	public static void HasBody_ExactIds_Only()
	{
		foreach (var id in new[] { "oni", "kappa", "elder-oni", "elder-kappa" }) Assert.True(FighterAnimSet.HasBody(id), id);
		foreach (var id in new[] { "kitsune", "tanuki", "ryo", "Oni", "KAPPA", " oni", "oni ", "elder", "", "human-guises", "nine-tailed-kitsune" })
			Assert.True(!FighterAnimSet.HasBody(id), $"'{id}' is not a body");
	}

	// Label for every opponent: built plain, stand-in body noted, greyed shows its reason, never both.
	[Test]
	public static void Labels_EveryOpponent()
	{
		var foes = DevJump.Opponents();
		Assert.Equal(DevJump.OpponentIds.Length + 1, foes.Count, "four yokai plus human guises");
		foreach (var o in foes)
		{
			if (o.Built) Assert.True(o.NotBuilt == null, o.Yokai);
			else Assert.True(o.Note == null && o.Label == o.Name + " (" + o.NotBuilt + ")", o.Yokai + ": greyed label " + o.Label);
		}
		Assert.Equal("Kitsune", foes.First(o => o.Yokai == "kitsune").Label, "kitsune label");
		Assert.Equal("Tanuki", foes.First(o => o.Yokai == "tanuki").Label, "tanuki label");
		Assert.Equal("Oni (own body, Kitsune's moves)", foes.First(o => o.Yokai == "oni").Label, "oni label");
		Assert.Equal("Kappa (own body, Kitsune's moves)", foes.First(o => o.Yokai == "kappa").Label, "kappa label");
		Assert.True(!foes.First(o => o.Yokai == "human-guises").Built, "guises greyed");
		Assert.True(foes.First(o => o.Yokai == "human-guises").Label.Contains(DevJump.NotBuiltYet), "guises reason in label");
	}

	// Elder entries: the Oni/Kappa elder fights carry an own body too (the node's yokai is elder-oni/elder-kappa).
	[Test]
	public static void ElderOniAndKappa_NodesResolveToABody()
	{
		foreach (var (id, elderId) in new[] { ("oni", "elder-oni"), ("kappa", "elder-kappa") })
		{
			Assert.Equal(elderId, DevJump.ElderOf(id)!.ElderYokai, id + " elder id");
			Assert.True(FighterAnimSet.HasBody(elderId), elderId + " has a body");
			Assert.Equal(FighterAnimSet.For(id), FighterAnimSet.ForNode("kitsune", elderId), elderId + " shows the same body as its yokai");
		}
	}

	// Greyed opponent through the page path: refused, nothing opens.
	[Test]
	public static void GreyedOpponent_CannotBeBuilt()
	{
		var pool = FightScene.LoadAbilityPool();
		bool threw = false;
		try { DevJump.BuildFight(new DevFightSetup("human-guises"), pool); } catch (ArgumentException) { threw = true; }
		Assert.True(threw, "human-guises refused by BuildFight");
		foreach (var bad in new[] { "Oni", "elder-oni", "" })
		{
			threw = false;
			try { DevJump.BuildFight(new DevFightSetup(bad), pool); } catch (ArgumentException) { threw = true; }
			Assert.True(threw, $"'{bad}' refused");
		}
	}

	// Determinism: the same Oni/Kappa setup twice builds the same run state hash; kit stays Kitsune.
	[Test]
	public static void BuildFight_OniKappa_Deterministic_KitUnchanged()
	{
		var pool = FightScene.LoadAbilityPool();
		foreach (var id in new[] { "oni", "kappa" })
			foreach (bool elder in new[] { false, true })
			{
				var s = new DevFightSetup(id, Elder: elder);
				var a = DevJump.BuildFight(s, pool);
				var b = DevJump.BuildFight(s, pool);
				Assert.Equal("kitsune", DevJump.FighterOf(s), id + " kit");
				Assert.Equal(a.Session.Run.StateHash(), b.Session.Run.StateHash(), $"{id} elder={elder}: same run hash");
				var m1 = FightScene.NewMatch(a.Session.Run, a.Session);
				var m2 = FightScene.NewMatch(b.Session.Run, b.Session);
				for (int i = 0; i < 120; i++) { m1.Step(FighterInput.None, FighterInput.None); m2.Step(FighterInput.None, FighterInput.None); }
				Assert.Equal(m1.StateHash(), m2.StateHash(), $"{id} elder={elder}: same match hash");
			}
	}

	// Page path: Perform with a greyed opponent is refused with an error and opens nothing.
	[Test]
	public static void Perform_GreyedOpponent_IsRefused_NothingOpens()
	{
		var target = DevJump.Targets().First(t => t.Kind == DevJumpKind.Fight);
		var ok = YokaiFighters.Dev.DevJumpPage.Perform(null!, target, new DevFightSetup("human-guises"), new DevDraftSetup(), null, out var error);
		Assert.True(!ok && !string.IsNullOrEmpty(error), "refused with a reason: " + error);
	}
}
