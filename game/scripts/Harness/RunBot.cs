using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Sim;

namespace YokaiFighters.Harness;

/// <summary>
/// YOK-26: the newcomer's choices between fights, every one a stated rule from <see cref="BotRunConfig"/>:
/// the route (a preferred node type when one is reachable, else a seeded random pick), the reward card
/// (first card of the first kind in the order, on its lowest slot) and the stops (merchant heal, dojo rest).
/// Deterministic: one seeded <see cref="SimRng"/> per run.
/// </summary>
public sealed class RunBot
{
	readonly BotRunConfig _cfg;
	readonly SimRng _rng;
	/// <summary>YOK-253: the fight's special_slot_only slot, invested in first; null = off.</summary>
	readonly SpecialSlot? _slotOnly;

	public RunBot(BotRunConfig cfg, uint seed, SpecialSlot? slotOnly = null)
	{
		_cfg = cfg;
		_rng = new SimRng(seed == 0 ? 1u : seed);
		_slotOnly = slotOnly;
	}

	/// <summary>The node to enter from <paramref name="reachable"/> (in map order).</summary>
	public string ChooseRoute(RunSession session, IReadOnlyList<string> reachable)
	{
		if (reachable.Count == 0) throw new InvalidOperationException("no reachable node");
		if (reachable.Count == 1) return reachable[0];
		if (_cfg.Route == RouteRule.PreferTypesElseRandom)
			foreach (string type in _cfg.PreferTypes)
				foreach (string id in reachable)
					if (session.Map.Graph.Node(id).Type == type) return id;
		return reachable[_rng.Next(reachable.Count)];
	}

	/// <summary>
	/// The card and target to pick: the first card whose kind comes first in the order; several targets = the lowest slot.
	/// YOK-253, both off by default:
	/// <list type="bullet">
	/// <item><c>prefer_special</c>'s NEW card comes before the order; it goes on the special_slot_only slot when that is one
	/// of its targets (the one NEW card that may replace the spammed special), else on its lowest target.</item>
	/// <item>With a special_slot_only slot (and <paramref name="run"/>), the first card in the order that can go on that
	/// slot goes there. No other NEW card goes on that slot while it is filled, nor at all with <c>prefer_special</c> (the
	/// slot then waits empty, the usual bot, for the preferred one); such a card goes on another of its targets or is passed
	/// over. Only when every card on offer is such a card does A7 (a reward is always picked) force it, onto the slot.</item>
	/// </list>
	/// </summary>
	public (int Card, SpecialSlot? Target) ChooseCard(RewardOffer offer, RunState? run = null)
	{
		if (_cfg.PreferSpecial is { } want)
			foreach (var c in offer.Cards)
				if (c.Kind == CardKind.NewSpecial && c.AbilityId == want)
					return (c.Index, _slotOnly is { } spam && c.Targets.Any(t => t.Slot == spam) ? spam : LowestTarget(c));
		if (_slotOnly is { } only && run is not null)
			foreach (var kind in _cfg.RewardOrder)
				foreach (var c in offer.Cards)
					if (c.Kind == kind && c.Targets.Any(t => t.Slot == only) && !KeptOff(c, only, run))
						return (c.Index, only);
		foreach (var kind in _cfg.RewardOrder)
			foreach (var c in offer.Cards)
				if (c.Kind == kind && Target(c, run) is var (ok, target) && ok) return (c.Index, target);
		if (offer.Cards.Count == 0) throw new InvalidOperationException("the offer has no cards");
		foreach (var c in offer.Cards)
			if (Target(c, run) is var (ok, target) && ok) return (c.Index, target);
		return (offer.Cards[0].Index, LowestTarget(offer.Cards[0])); // A7: every card would go on the spam slot; one must be picked
	}

	/// <summary>YOK-253: a NEW card the spam slot must not take (it is filled, or prefer_special keeps it for one special).</summary>
	bool KeptOff(CardView c, SpecialSlot only, RunState run) =>
		c.Kind == CardKind.NewSpecial && c.Targets.Any(t => t.Slot == only) && (run[only] is not null || _cfg.PreferSpecial is not null);

	/// <summary>The card's lowest target; YOK-253: a kept-off card's lowest target that is not the spam slot, or none (not ok).</summary>
	(bool Ok, SpecialSlot? Target) Target(CardView c, RunState? run)
	{
		if (_slotOnly is not { } only || run is null || !KeptOff(c, only, run)) return (true, LowestTarget(c));
		SpecialSlot? best = null;
		foreach (var t in c.Targets) if (t.Slot != only && (best is null || t.Slot < best)) best = t.Slot;
		return (best is not null, best);
	}

	static SpecialSlot? LowestTarget(CardView c)
	{
		if (c.Targets.Count == 0) return null;
		var best = c.Targets[0].Slot;
		foreach (var t in c.Targets) if (t.Slot < best) best = t.Slot;
		return best;
	}

	/// <summary>
	/// The merchant stop (R6): one choice, the free heal or the shop (YOK-95), per <see cref="BotRunConfig.Merchant"/>.
	/// In the shop the bot buys through <see cref="MerchantShop"/> only (no rule here): shelf specials in shelf order
	/// into a free slot, then shelf modifiers in shelf order on the lowest slot that can take one, until nothing on
	/// the shelf is affordable or fits. It never releases (a newcomer keeps what it has). Bought ids go in
	/// <paramref name="bought"/>. Returns what the stop did: heal, shop or skip.
	/// </summary>
	public string AtMerchant(RunSession session, List<string>? bought = null)
	{
		if (_cfg.Merchant == MerchantPlan.Skip || session.Merchant is not { CanChoose: true } m) return "skip";
		bool shop = _cfg.Merchant switch
		{
			MerchantPlan.Shop => true,
			MerchantPlan.HealIfHurtElseShop => session.Run.Health * 100 >= _cfg.HealBelowPct * RunState.MaxHealth,
			_ => false,
		};
		if (!shop)
		{
			var r = m.TakeHeal();
			if (!r.Ok) throw new InvalidOperationException($"merchant heal refused: {r.Error}");
			return "heal";
		}
		var c = m.Choose(MerchantChoice.Shop);
		if (!c.Ok) throw new InvalidOperationException($"merchant shop refused: {c.Error}");
		Shop(m.Shop, bought);
		return "shop";
	}

	static void Shop(MerchantShop shop, List<string>? bought)
	{
		for (bool any = true; any;)
		{
			any = false;
			foreach (var s in shop.Shelf)
				if (shop.CanBuy(s.Id) && shop.Buy(s.Id).Ok) { bought?.Add(s.Id); any = true; break; }
			if (any) continue;
			foreach (var mod in shop.ModifierShelf)
			{
				foreach (var slot in shop.ModifierTargets(mod.Id).OrderBy(x => x))
					if (shop.CanBuyModifier(mod.Id, slot) && shop.BuyModifier(mod.Id, slot).Ok) { bought?.Add(mod.Id); any = true; break; }
				if (any) break;
			}
		}
	}

	/// <summary>The dojo stop (R7): rest (the trial, YOK-98, is not built). Returns what the stop did.</summary>
	public string AtDojo(RunSession session)
	{
		if (_cfg.Dojo == DojoPlan.Rest && session.Dojo is { CanRest: true } d)
		{
			var r = d.Rest();
			if (!r.Ok) throw new InvalidOperationException($"dojo rest refused: {r.Error}");
			return "rest";
		}
		return "skip";
	}
}
