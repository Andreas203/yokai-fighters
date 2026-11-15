using System;
using System.Collections.Generic;
using System.Linq;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-95: the shop half of a merchant stop (R6; pure C#, deterministic, no RNG, no Godot). Ryo spends the stop's
/// one choice on it with <see cref="Merchant.Choose"/>(<see cref="MerchantChoice.Shop"/>), which closes the free
/// heal; until then, and once the stop is left, every purchase and release is refused.
/// <list type="bullet">
/// <item>Buy: a common special (A9) from a yokai not bound this run, 50 coins, into its own empty slot at Lv 1 (A3),
/// in borrowed ink (never carried over, M3). AM27: when yokai not bound this run leave fewer than <see cref="MinStock"/>
/// items, commons from bound yokai top the shelf up, so the merchant always has something to sell. Never a rare, never a cancel rule, never a locked ability (A11) the save has not
/// unlocked (M1, <see cref="AbilityPool.IsAvailable(SpecialData)"/>) or a starter (A1), never one Ryo already holds.</item>
/// <item>Buy a modifier (designer, 2026-10-11: modifiers are common abilities, A9/A11): same price and the same
/// shelf rule; the player picks the special it goes on and <see cref="RunState.CanAttach"/> decides (A2: one per
/// special, <c>applies_to</c>), the rule the reward draft uses. Borrowed ink too.</item>
/// <item>Release a binding: 40 coins, empties a slot holding a yokai's special. Refused when it holds Ryo's last
/// special (T5), so the Tanuki's copy target is always defined; the released move no longer counts for it (T2).</item>
/// </list>
/// Purchases change only the run's slots and coins, so the save written when the node completes holds them; a
/// quit inside the stop restarts it from the entry save (M5). The shelf is every eligible special in pool order
/// (file name), recomputed from the run (with the AM27 top-up), so it needs no state of its own and no seed.
/// </summary>
public sealed class MerchantShop
{
	public const int BuyCost = 50;     // R6
	public const int ReleaseCost = 40; // R6
	/// <summary>R6/A9: the only rarity the shop sells.</summary>
	public const string SoldRarity = "common";
	/// <summary>AM27: the fewest items a stop sells (one shelf, R6: 3 per shelf) while enough commons exist to fill it.</summary>
	public const int MinStock = 3;

	readonly RunSession _session;
	readonly Merchant _merchant;
	/// <summary>AM27: what this stop sold this visit. Released again, it goes to the back of the top-up so no shown card is pushed off. Not saved: a quit restarts the stop (M5).</summary>
	readonly HashSet<string> _soldHere = new(StringComparer.Ordinal);

	internal MerchantShop(RunSession session, Merchant merchant)
	{
		_session = session;
		_merchant = merchant;
	}

	RunState Run => _session.Run;

	/// <summary>Ryo chose the shop at this stop and still stands at it: buying and releasing are allowed.</summary>
	public bool Open => _merchant.Open && _merchant.Choice == MerchantChoice.Shop;

	/// <summary>
	/// What the shop offers now (also before the shop is chosen, so a screen can show it): the pool's specials in
	/// file-name order that are common, unlocked, not starters, not held by Ryo and sourced from a yokai not bound
	/// this run (<see cref="RunSession.BoundYokai"/>), plus any AM27 top-up from bound yokai. Modifiers are on <see cref="ModifierShelf"/>; cancel rules
	/// are never sold.
	/// </summary>
	public IReadOnlyList<SpecialData> Shelf => Stock(Run, _session.Pool, _session.BoundYokai(), _merchant.TopUpOrder, _soldHere).Specials;

	/// <summary>The R6 shelf for a run, a pool and the yokai already bound (see <see cref="Shelf"/>), topped up by AM27 in the order the run gives now.</summary>
	public static IReadOnlyList<SpecialData> ShelfFor(RunState run, AbilityPool pool, IEnumerable<string> boundYokai) =>
		Stock(run, pool, boundYokai).Specials;

	/// <summary>
	/// The modifiers the shop offers now, in pool (file-name) order: common, unlocked, from a yokai not bound this
	/// run (or an AM27 top-up), and not already on one of Ryo's specials. Listed whether or not a special can take it now
	/// (<see cref="ModifierTargets"/> says which can).
	/// </summary>
	public IReadOnlyList<ModifierData> ModifierShelf => Stock(Run, _session.Pool, _session.BoundYokai(), _merchant.TopUpOrder, _soldHere).Modifiers;

	/// <summary>The R6 modifier shelf for a run, a pool and the yokai already bound (see <see cref="ModifierShelf"/>), topped up by AM27 in the order the run gives now.</summary>
	public static IReadOnlyList<ModifierData> ModifierShelfFor(RunState run, AbilityPool pool, IEnumerable<string> boundYokai) =>
		Stock(run, pool, boundYokai).Modifiers;

	/// <summary>
	/// R6 + AM27: what the stop sells. First every sellable common from a yokai not bound this run (R6). When that
	/// comes to fewer than <see cref="MinStock"/> items (specials and modifiers together), the shelf is topped up to
	/// <see cref="MinStock"/> with sellable commons from bound yokai, taken in <paramref name="topUpOrder"/> (see
	/// <see cref="TopUpOrder"/>); a common missing from that order, or sold at this stop (<paramref name="soldHere"/>)
	/// and released again, comes after it, in pool order. A stop passes the order it fixed when Ryo walked in, so a buy only frees its own place for the next
	/// item and a release never reshuffles the shelf; with none, the order is taken from the run now. Coins never change
	/// the stock. Both lists keep pool order. Deterministic: run state, pool and order only.
	/// </summary>
	static (SpecialData[] Specials, ModifierData[] Modifiers) Stock(RunState run, AbilityPool pool, IEnumerable<string> boundYokai,
		IReadOnlyList<string>? topUpOrder = null, IReadOnlyCollection<string>? soldHere = null)
	{
		var bound = new HashSet<string>(boundYokai, StringComparer.Ordinal);
		var specials = pool.Specials.Where(s => Sellable(run, pool, s)).ToArray();
		var modifiers = pool.Modifiers.Where(m => Sellable(run, pool, m)).ToArray();
		int missing = MinStock - specials.Count(s => !bound.Contains(s.Source)) - modifiers.Count(m => !bound.Contains(m.Source));
		var extra = new HashSet<string>(StringComparer.Ordinal);
		if (missing > 0)
		{
			var order = topUpOrder ?? TopUpOrder(run, pool);
			var candidates = specials.Where(s => bound.Contains(s.Source)).Select(s => SpecialKey(s.Id))
				.Concat(modifiers.Where(m => bound.Contains(m.Source)).Select(m => ModifierKey(m.Id)))
				.Select((key, poolIndex) => (Key: key, Rank: soldHere?.Contains(key) != true && IndexOf(order, key) is int i and >= 0 ? i : order.Count + poolIndex))
				.OrderBy(x => x.Rank);
			foreach (var (key, _) in candidates.Take(missing)) extra.Add(key);
		}
		return (specials.Where(s => !bound.Contains(s.Source) || extra.Contains(SpecialKey(s.Id))).ToArray(),
			modifiers.Where(m => !bound.Contains(m.Source) || extra.Contains(ModifierKey(m.Id))).ToArray());
	}

	/// <summary>
	/// AM27: the order a short shelf is topped up in, for the run as it stands: every sellable common, first those Ryo
	/// could buy now (a special whose slot is empty, a modifier some held special can take), then the rest, each group in
	/// pool order, specials before modifiers. A <see cref="Merchant"/> takes it once, when Ryo walks in (M5 restarts a stop
	/// from that same state, so a restored stop takes the same order).
	/// </summary>
	internal static IReadOnlyList<string> TopUpOrder(RunState run, AbilityPool pool)
	{
		var all = pool.Specials.Where(s => Sellable(run, pool, s)).Select(s => (Key: SpecialKey(s.Id), Now: run[s.Slot] is null))
			.Concat(pool.Modifiers.Where(m => Sellable(run, pool, m)).Select(m => (Key: ModifierKey(m.Id), Now: run.Owned.Any(o => run.CanAttach(o.Slot, m)))))
			.ToArray();
		return all.Where(x => x.Now).Concat(all.Where(x => !x.Now)).Select(x => x.Key).ToArray();
	}

	static bool Sellable(RunState run, AbilityPool pool, SpecialData s) =>
		!s.Starter && pool.IsAvailable(s) && s.Rarity == SoldRarity && !run.Owns(s.Id);
	static bool Sellable(RunState run, AbilityPool pool, ModifierData m) =>
		pool.IsAvailable(m) && m.Rarity == SoldRarity && !run.HasModifier(m.Id);
	static string SpecialKey(string id) => "special:" + id;
	static string ModifierKey(string id) => "modifier:" + id;
	static int IndexOf(IReadOnlyList<string> list, string key)
	{
		for (int i = 0; i < list.Count; i++) if (list[i] == key) return i;
		return -1;
	}

	/// <summary>A2: the slots whose special can take <paramref name="modifierId"/> now (none for an id off the shelf).</summary>
	public IReadOnlyList<SpecialSlot> ModifierTargets(string modifierId)
	{
		var m = ModifierShelf.FirstOrDefault(x => x.Id == modifierId);
		if (m is null) return Array.Empty<SpecialSlot>();
		return Run.Owned.Where(o => Run.CanAttach(o.Slot, m)).Select(o => o.Slot).ToArray();
	}

	/// <summary>Why <paramref name="modifierId"/> can't be bought onto the special in <paramref name="slot"/> now, or null when it can.</summary>
	public string? WhyNotBuyModifier(string modifierId, SpecialSlot slot)
	{
		if (!Open) return ClosedReason();
		var m = ModifierShelf.FirstOrDefault(x => x.Id == modifierId);
		if (m is null) return $"'{modifierId}' is not on the merchant's shelf (R6)";
		if (slot is < SpecialSlot.A or > SpecialSlot.D) return $"'{slot}' is not a slot";
		if (Run[slot] is not { } held) return $"slot {slot} is empty";
		if (held.Modifier is { } worn) return $"{held.Data.Id} already has {worn.Id} (A2: one modifier per special)";
		if (!Run.CanAttach(slot, m)) return $"{m.Id} does not apply to {held.Data.Id} (A2)";
		if (!Run.CanAfford(BuyCost)) return $"{BuyCost} coins needed, Ryo has {Run.Coins} (R6)";
		return null;
	}

	public bool CanBuyModifier(string modifierId, SpecialSlot slot) => WhyNotBuyModifier(modifierId, slot) is null;

	/// <summary>
	/// R6: buys a modifier from the shelf for 50 coins and attaches it to the special in <paramref name="slot"/>
	/// (<see cref="RunState.Attach"/>, A2), in borrowed ink. A refused buy changes nothing and spends nothing.
	/// </summary>
	public MapResult BuyModifier(string modifierId, SpecialSlot slot)
	{
		if (WhyNotBuyModifier(modifierId, slot) is { } why) return MapResult.Fail($"merchant '{_merchant.NodeId}': {why}");
		var m = ModifierShelf.First(x => x.Id == modifierId);
		if (!Run.TrySpend(BuyCost)) return MapResult.Fail($"merchant '{_merchant.NodeId}': not enough coins (R6)");
		Run.Attach(slot, m, borrowed: true);
		_soldHere.Add(ModifierKey(m.Id));
		return MapResult.Done;
	}

	/// <summary>Why <paramref name="specialId"/> can't be bought now, or null when it can.</summary>
	public string? WhyNotBuy(string specialId)
	{
		if (!Open) return ClosedReason();
		var data = Shelf.FirstOrDefault(s => s.Id == specialId);
		if (data is null) return $"'{specialId}' is not on the merchant's shelf (R6)";
		if (Run[data.Slot] is { } held) return $"slot {data.Slot} holds {held.Data.Id}: release it first (R6)";
		if (!Run.CanAfford(BuyCost)) return $"{BuyCost} coins needed, Ryo has {Run.Coins} (R6)";
		return null;
	}

	public bool CanBuy(string specialId) => WhyNotBuy(specialId) is null;

	/// <summary>R6: buys a special from the shelf for 50 coins, Lv 1, borrowed ink. A refused buy changes nothing.</summary>
	public MapResult Buy(string specialId)
	{
		if (WhyNotBuy(specialId) is { } why) return MapResult.Fail($"merchant '{_merchant.NodeId}': {why}");
		var data = Shelf.First(s => s.Id == specialId);
		if (!Run.TrySpend(BuyCost)) return MapResult.Fail($"merchant '{_merchant.NodeId}': not enough coins (R6)");
		Run.Borrow(data);
		_soldHere.Add(SpecialKey(data.Id));
		return MapResult.Done;
	}

	/// <summary>
	/// Why the special in <paramref name="slot"/> can't be released now, or null when it can. A binding is a
	/// yokai's special: Ryo's starters are his own (A1) and are not released.
	/// </summary>
	public string? WhyNotRelease(SpecialSlot slot)
	{
		if (!Open) return ClosedReason();
		if (slot is < SpecialSlot.A or > SpecialSlot.D) return $"'{slot}' is not a slot";
		if (Run[slot] is not { } held) return $"slot {slot} is empty";
		if (held.Data.Starter) return $"{held.Data.Id} is Ryo's own starter, not a binding (A1, R6)";
		if (Run.SpecialCount == 1) return $"{held.Data.Id} is Ryo's last special (T5)";
		if (!Run.CanAfford(ReleaseCost)) return $"{ReleaseCost} coins needed, Ryo has {Run.Coins} (R6)";
		return null;
	}

	public bool CanRelease(SpecialSlot slot) => WhyNotRelease(slot) is null;

	/// <summary>R6: releases the binding in <paramref name="slot"/> for 40 coins, freeing the slot. A refused release changes nothing.</summary>
	public MapResult Release(SpecialSlot slot)
	{
		if (WhyNotRelease(slot) is { } why) return MapResult.Fail($"merchant '{_merchant.NodeId}': {why}");
		if (!Run.TrySpend(ReleaseCost)) return MapResult.Fail($"merchant '{_merchant.NodeId}': not enough coins (R6)");
		Run.Release(slot);
		return MapResult.Done;
	}

	string ClosedReason() =>
		!_merchant.Open ? "Ryo is not at this stop"
		: _merchant.Choice == MerchantChoice.Heal ? "the heal was taken here, the shop is closed (R6)"
		: "the shop is not chosen yet (R6: Choose(Shop) first)";
}
