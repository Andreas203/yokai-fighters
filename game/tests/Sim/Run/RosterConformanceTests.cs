using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using RestStopScreenForecast = YokaiFighters.Ui.RestStopScreenForecast;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-226: the whole roster through the run systems. Every test runs over the real data folders (data/moves,
/// data/modifiers, data/cancels), never a hand-listed id, so a special added later is covered without touching this
/// file. Copy rule T0-T4, T6 (AM4); draft A7-A9; shop R6, A6; unlocks M1-M3; count A11; inputs E16, E18 (K3).
/// </summary>
public static class RosterConformanceTests
{
	static string Root => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static string Dir(string kind) => Path.Combine(Root, "data", kind);
	static SpecialData[] AllSpecials => SpecialLoader.LoadDirectory(Dir("moves"));
	static AbilityPool Pool => AbilityPool.LoadRepo(Root);
	static AbilityPool Unlocked(AbilityPool p) =>
		p.WithUnlocks(p.Specials.Select(s => s.Id).Concat(p.Modifiers.Select(m => m.Id)).Concat(p.CancelRules.Select(c => c.Id)));
	static string Sig(MoveData m) => EvolutionBehaviourTests.Signature(m);

	/// <summary>A new run with <paramref name="sp"/> in its slot at <paramref name="level"/> (A3).</summary>
	static RunState RunWith(AbilityPool pool, SpecialData sp, int level)
	{
		var run = RunState.NewRun(pool);
		if (run[sp.Slot]?.Data.Id != sp.Id) run.Equip(sp);
		while (run[sp.Slot]!.Level < level) run.LevelUp(sp.Slot);
		return run;
	}

	// --- A11 ---------------------------------------------------------------------------------------

	[Test]
	public static void Count_A11_EightSpecialsEightEvolutionsFourteenModifiersThreeCancelRules_TwelveLocked()
	{
		var specials = AllSpecials;
		var mods = ModifierLoader.LoadDirectory(Dir("modifiers"));
		var cancels = CancelRuleLoader.LoadDataDirectory(Dir("cancels"));
		Assert.Equal(8, specials.Length, "A11: 8 specials (" + string.Join(", ", specials.Select(s => s.Id)) + ")");
		Assert.Equal(8, specials.Count(s => s.MaxLevel == 3 && s.EvolutionName.Length > 0), "A11: 8 evolutions");
		Assert.Equal(14, mods.Length, "A11: 14 modifiers (" + string.Join(", ", mods.Select(m => m.Id)) + ")");
		Assert.Equal(3, cancels.Length, "A11: 3 cancel rules");
		Assert.Equal(3, specials.Count(s => s.Locked), "A11: 3 locked specials");
		Assert.Equal(8, mods.Count(m => m.Locked), "A11: 8 locked modifiers");
		Assert.Equal(1, cancels.Count(c => c.Locked), "A11: 1 locked cancel rule");
		Assert.Equal(2, specials.Count(s => s.Starter), "A1: two starters");
	}

	// --- Copy rule ---------------------------------------------------------------------------------

	[Test]
	public static void Copy_EverySpecialAtEveryLevel_RyosLevel_NoModifierNoEvolution_OriginalSlot_Named()
	{
		var pool = Unlocked(Pool);
		var ryo = Kit.Load(Kit.Ryo, KitSources.Repo(Root));
		foreach (var sp in AllSpecials)
			for (int lv = 1; lv <= sp.MaxLevel; lv++)
			{
				string at = $"{sp.Id} Lv {lv}";
				var t = new CopyTarget(sp.Slot, sp, lv);
				int moveLevel = Math.Min(lv, 2);
				Assert.Equal(moveLevel, t.MoveLevel, $"{at}: T4 runs Lv {moveLevel}");
				Assert.Equal(Sig(sp.Build(moveLevel, false)), Sig(t.Build()), $"{at}: T4 plain build");
				if (sp.HasEx) Assert.Equal(Sig(sp.Build(moveLevel, true)), Sig(t.Build(true)), $"{at}: T4 EX build");
				if (lv == 3) Assert.True(Sig(t.Build()) != Sig(sp.Build(3, false)), $"{at}: T4 the copy never has the evolution");

				// T0 / AM4: the Tanuki's fighter holds the copy in Ryo's slot, the other starter in its own.
				var m = new Match();
				var kit = TanukiKit.Build(ryo, t);
				kit.Equip(m.P2);
				var eq = m.P2.Specials[(int)sp.Slot]!;
				Assert.True(eq.Data.Id == sp.Id && eq.Level == moveLevel && eq.Modifier is null, $"{at}: copy in slot {sp.Slot}");
				Assert.Equal(sp.Slot, m.P2.CopiedSlot, $"{at}: copied slot");
				foreach (var st in ryo.Specials.Where(s => s.Slot != sp.Slot))
					Assert.Equal(st.Id, m.P2.Specials[(int)st.Slot]?.Data.Id, $"{at}: the other starter stays in {st.Slot}");

				// T6: the forecast cost is a figure (0 for a move that never hits) and the HUD strip names the move.
				int cost = RestStopScreenForecast.ExpectedCost(t);
				Assert.Equal(t.Build().Damage, cost, $"{at}: forecast cost = damage per hit at Ryo's level");
				Assert.True(cost >= 0, $"{at}: cost is a figure");
				string line = FightHud.CopyLine(t);
				Assert.True(line.Contains(sp.Name) && line.Contains($"Lv {lv}"), $"{at}: HUD copy line '{line}'");

				// T2 + T4 from a real run: at Lv 2+ it is the target, and a modifier Ryo put on it never reaches the copy.
				if (lv < 2) continue;
				var run = RunWith(pool, sp, lv);
				var mod = pool.Modifiers.FirstOrDefault(md => run.CanAttach(sp.Slot, md));
				if (mod != null) run.Attach(sp.Slot, mod);
				var target = CopyRule.Target(run);
				Assert.True(target is { } tt && tt.Data.Id == sp.Id && tt.Level == lv && tt.Slot == sp.Slot, $"{at}: T2 target");
				Assert.Equal(Sig(sp.Build(moveLevel, false)), Sig(target!.Build()), $"{at}: T4 no modifier ({mod?.Id})");
			}
	}

	// --- Draft --------------------------------------------------------------------------------------

	static string ElderIdOf(AbilityPool pool, string yokai) =>
		pool.CancelRules.FirstOrDefault(c => c.Source == yokai)?.OfferedBy ?? $"elder-{yokai}";

	[Test]
	public static void Draft_ThreeCardsFromTheYokaiBeaten_OneUpgrade_RaresOnlyFromElders()
	{
		foreach (var pool in new[] { Pool, Unlocked(Pool) })
			foreach (string yokai in AllSpecials.Where(s => !s.Starter).Select(s => s.Source).Distinct())
				foreach (bool elder in new[] { false, true })
					for (uint seed = 1; seed <= 12; seed++)
					{
						string at = $"{yokai} elder={elder} seed={seed}";
						var run = RunState.NewRun(pool);
						var cards = RewardDraft.Build(run, pool, yokai, seed, elder, elderId: elder ? ElderIdOf(pool, yokai) : null).Offer.Cards;
						Assert.Equal(3, cards.Count, $"{at}: A7 three cards");
						Assert.True(cards.Any(c => c.Kind == CardKind.Upgrade), $"{at}: A7 one upgrade");
						foreach (var c in cards.Where(c => c.Kind is CardKind.NewSpecial or CardKind.Modifier))
							Assert.Equal(yokai, c.Source, $"{at}: A8 {c.AbilityId} from the yokai beaten");
						foreach (var c in cards.Where(c => c.Kind == CardKind.NewSpecial))
						{
							var sp = pool.Special(c.AbilityId)!;
							Assert.True(pool.IsAvailable(sp), $"{at}: M1 {sp.Id} is unlocked");
							Assert.Equal(sp.Rarity == "rare", c.Rare, $"{at}: {sp.Id} rare flag");
						}
						if (!elder) Assert.True(cards.All(c => !c.Rare && c.Kind != CardKind.CancelRule), $"{at}: A9 no rares from a duel");
					}
	}

	[Test]
	public static void Draft_EveryElderCanOfferARare_WhenUnlocked_AndTheRow8RewardIsNeverEmpty()
	{
		var all = Unlocked(Pool);
		foreach (string yokai in AllSpecials.Where(s => !s.Starter).Select(s => s.Source).Distinct())
		{
			bool any = false;
			for (uint seed = 1; seed <= 24 && !any; seed++)
				any = RewardDraft.Build(RunState.NewRun(all), all, yokai, seed, true, elderId: ElderIdOf(all, yokai)).Offer.Cards.Any(c => c.Rare);
			Assert.True(any, $"A9: the {yokai} elder offers a rare once unlocked");
		}
		// R8 with nothing unlocked: a draft with no rare pays the provisional coin fallback instead (YOK-186).
		var graph = MapLoader.LoadFile(Path.Combine(Root, "data", "map", "default-map.json"));
		var scenes = NodeSceneTable.Default(Path.Combine(Root, "data", "map"));
		Assert.True(RunSession.R8FallbackCoins(graph, scenes) > 0, "R8: the fallback is a real payout");
	}

	// --- Shop and A6 -------------------------------------------------------------------------------

	[Test]
	public static void Shop_SellsUnboundCommonsOnly_NeverRaresStartersOrLocked_AndReplacedMovesLoseLevels()
	{
		var pool = Unlocked(Pool);
		var yokai = AllSpecials.Where(s => !s.Starter).Select(s => s.Source).Distinct().ToArray();
		foreach (var bound in new[] { Array.Empty<string>() }.Concat(yokai.Select(y => new[] { y })))
		{
			var run = RunState.NewRun(pool);
			var shelf = MerchantShop.ShelfFor(run, pool, bound);
			foreach (var sp in shelf)
				Assert.True(sp.Rarity == "common" && !sp.Starter && !bound.Contains(sp.Source), $"R6: {sp.Id} on the shelf (bound {string.Join(",", bound)})");
			foreach (var sp in AllSpecials.Where(s => s.Rarity == "common" && !s.Starter && !bound.Contains(s.Source)))
				Assert.True(shelf.Any(s => s.Id == sp.Id), $"R6: unbound common {sp.Id} is for sale");
			foreach (var m in MerchantShop.ModifierShelfFor(run, pool, bound))
				Assert.True(m.Rarity == "common" && !bound.Contains(m.Source), $"R6: modifier {m.Id} on the shelf");
		}
		var locked = MerchantShop.ShelfFor(RunState.NewRun(Pool), Pool, Array.Empty<string>());
		Assert.True(locked.All(s => !s.Locked), "M1: a locked special is never sold");

		// A6: each special levelled, then replaced in its slot, comes back at Lv 1; a release (sale) loses it too.
		foreach (var sp in AllSpecials.Where(s => !s.Starter))
		{
			var other = AllSpecials.FirstOrDefault(o => o.Slot == sp.Slot && o.Id != sp.Id);
			var run = RunWith(pool, sp, 2);
			if (other != null)
			{
				run.Equip(other);
				Assert.Equal(1, run[sp.Slot]!.Level, $"A6: {other.Id} replaced {sp.Id} at Lv 1");
				run.Equip(sp);
				Assert.Equal(1, run[sp.Slot]!.Level, $"A6: {sp.Id} drafted again starts at Lv 1");
			}
			run = RunWith(pool, sp, 2);
			run.Release(sp.Slot);
			Assert.True(run[sp.Slot] is null, $"A6: {sp.Id} released");
			run.Equip(sp);
			Assert.Equal(1, run[sp.Slot]!.Level, $"A6: {sp.Id} after a release starts at Lv 1");
		}
	}

	// --- Unlocks and carry-over --------------------------------------------------------------------

	[Test]
	public static void Unlocks_TwelveInTheFixedOrder_AndEverySpecialCarriesOverAtLv2()
	{
		var pool = Pool;
		var lockedCancels = pool.CancelRules.Where(c => c.Locked).Select(c => c.Id).ToArray();
		var u = Unlocks.FromData(pool, lockedCancels);
		Assert.Equal(12, u.Order.Count, "M2/A11: 12 locked (" + string.Join(", ", u.Order) + ")");
		var kinds = u.Order.Select(id => pool.Special(id) != null ? 0 : pool.Modifiers.Any(m => m.Id == id) ? 1 : 2).ToArray();
		Assert.Equal("3,8,1", $"{kinds.Count(k => k == 0)},{kinds.Count(k => k == 1)},{kinds.Count(k => k == 2)}", "3 specials, 8 modifiers, 1 cancel rule");
		Assert.True(kinds.SequenceEqual(kinds.OrderBy(k => k)), "M1: the fixed order is specials, modifiers, cancel rules");

		var all = Unlocked(pool);
		foreach (var sp in AllSpecials)
		{
			var run = RunWith(all, sp, Math.Min(3, sp.MaxLevel));
			Assert.Equal(sp.Id, CarryOver.Best(run)?.Data.Id, $"M3: {sp.Id} is the best special");
			var next = CarryOver.NewRun(all, sp.Id, null, out var dropped);
			Assert.True(dropped is null, $"M3: {sp.Id} not dropped ({dropped})");
			var carried = next[sp.Slot]!;
			Assert.True(carried.Data.Id == sp.Id && carried.Level == 2 && carried.Modifier is null, $"M3: {sp.Id} at Lv 2, no modifier");
		}
	}

	// --- Inputs (E16, E18, K3) ---------------------------------------------------------------------

	static InputBits Dir(int numpad, int facing)
	{
		InputBits fwd = facing > 0 ? InputBits.Right : InputBits.Left, back = facing > 0 ? InputBits.Left : InputBits.Right;
		InputBits b = InputBits.None;
		if (numpad <= 3) b |= InputBits.Down;
		if (numpad >= 7) b |= InputBits.Up;
		if (numpad % 3 == 0) b |= fwd;
		if (numpad % 3 == 1) b |= back;
		return b;
	}

	static readonly Dictionary<string, int> KihonDir = new() { ["neutral"] = 5, ["forward"] = 6, ["back"] = 4, ["down"] = 2 };

	/// <summary>The special's own <c>input</c> block, read from its file (Kata motion, Kihon direction).</summary>
	static (string Motion, string Direction) InputOf(SpecialData sp)
	{
		var doc = JsonNode.Parse(File.ReadAllText(Path.Combine(Dir("moves"), sp.Id + ".json")))!;
		return (doc["input"]!["kata"]!["motion"]!.GetValue<string>(), doc["input"]!["kihon"]!["direction"]!.GetValue<string>());
	}

	static List<SpecialEvent> Perform(SpecialData sp, ControlScheme scheme, bool ex)
	{
		var ryo = Kit.Load(Kit.Ryo, KitSources.Repo(Root));
		var m = new Match(null, ryo.Moves, ryo.Moves);
		Kit.EquipSpecials(m.P1, ryo.Specials);
		m.P1.Equip(sp.Slot, sp, 1);
		m.P1.Input.Scheme = scheme;
		m.P1.Meter = 300;
		m.P1.X = -300 * SimConfig.Scale;
		m.P2.X = 300 * SimConfig.Scale;
		var events = new List<SpecialEvent>();
		m.SpecialStarted += (_, e) => { if (e.Fighter == 0) events.Add(e); };
		var (motion, direction) = InputOf(sp);
		var ticks = new List<InputBits>();
		if (scheme == ControlScheme.Kata)
		{
			for (int k = 0; k < motion.Length; k++)
			{
				int d = motion[k] - '0';
				if (k > 0 && motion[k] == motion[k - 1]) ticks.Add(Dir(5, 1)); // 22: back to neutral between the taps
				ticks.Add(Dir(d, 1));
			}
			ticks[^1] |= InputBits.LightPunch | (ex ? InputBits.MediumPunch : InputBits.None);
		}
		else ticks.Add(Dir(KihonDir[direction], 1) | InputBits.Special | (ex ? InputBits.LightPunch : InputBits.None));
		foreach (var b in ticks) m.Step(new FighterInput(b), FighterInput.None);
		for (int t = 0; t < 10; t++) m.Step(FighterInput.None, FighterInput.None);
		return events;
	}

	[Test]
	public static void Inputs_EverySpecialPlaysInKataAndKihon_AndAsEx()
	{
		foreach (var sp in AllSpecials)
		{
			var (motion, direction) = InputOf(sp);
			Assert.True(KihonDir.ContainsKey(direction), $"{sp.Id}: Kihon direction '{direction}'");
			foreach (var scheme in new[] { ControlScheme.Kata, ControlScheme.Kihon })
				foreach (bool ex in sp.HasEx ? new[] { false, true } : new[] { false })
				{
					string at = $"{sp.Id} {scheme}{(ex ? " EX" : "")} ({(scheme == ControlScheme.Kata ? motion : direction)})";
					var events = Perform(sp, scheme, ex);
					Assert.True(events.Count > 0, $"{at}: no special started");
					var last = events[^1];
					Assert.True(last.Slot == sp.Slot && last.MoveId == sp.Build(1, ex).Id, $"{at}: started {last.MoveId} in {last.Slot}");
					Assert.Equal(ex, last.Ex, $"{at}: EX");
				}
		}
	}
}
