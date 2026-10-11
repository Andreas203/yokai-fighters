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
/// in borrowed ink (never carried over, M3). Never a rare, never a cancel rule, never a locked ability (A11) or a
/// starter (A1), never one Ryo already holds.</item>
/// <item>Release a binding: 40 coins, empties a slot holding a yokai's special. Refused when it holds Ryo's last
/// special (T5), so the Tanuki's copy target is always defined; the released move no longer counts for it (T2).</item>
/// </list>
/// Purchases change only the run's slots and coins, so the save written when the node completes holds them; a
/// quit inside the stop restarts it from the entry save (M5). The shelf is every eligible special in pool order
/// (file name), recomputed from the run, so it needs no state of its own and no seed.
/// </summary>
public sealed class MerchantShop
{
	public const int BuyCost = 50;     // R6
	public const int ReleaseCost = 40; // R6
	/// <summary>R6/A9: the only rarity the shop sells.</summary>
	public const string SoldRarity = "common";

	readonly RunSession _session;
	readonly Merchant _merchant;

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
	/// this run (<see cref="RunSession.BoundYokai"/>). Modifiers and cancel rules are not sold.
	/// </summary>
	public IReadOnlyList<SpecialData> Shelf => ShelfFor(Run, _session.Pool, _session.BoundYokai());

	/// <summary>The R6 shelf for a run, a pool and the yokai already bound (see <see cref="Shelf"/>).</summary>
	public static IReadOnlyList<SpecialData> ShelfFor(RunState run, AbilityPool pool, IEnumerable<string> boundYokai)
	{
		var bound = new HashSet<string>(boundYokai, StringComparer.Ordinal);
		return pool.Specials
			.Where(s => !s.Starter && !s.Locked && s.Rarity == SoldRarity && !bound.Contains(s.Source) && !run.Owns(s.Id))
			.ToArray();
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
