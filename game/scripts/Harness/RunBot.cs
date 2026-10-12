using System;
using System.Collections.Generic;
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

	public RunBot(BotRunConfig cfg, uint seed)
	{
		_cfg = cfg;
		_rng = new SimRng(seed == 0 ? 1u : seed);
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

	/// <summary>The card and target to pick: the first card whose kind comes first in the order; several targets = the lowest slot.</summary>
	public (int Card, SpecialSlot? Target) ChooseCard(RewardOffer offer)
	{
		foreach (var kind in _cfg.RewardOrder)
			foreach (var c in offer.Cards)
				if (c.Kind == kind) return (c.Index, LowestTarget(c));
		if (offer.Cards.Count == 0) throw new InvalidOperationException("the offer has no cards");
		return (offer.Cards[0].Index, LowestTarget(offer.Cards[0]));
	}

	static SpecialSlot? LowestTarget(CardView c)
	{
		if (c.Targets.Count == 0) return null;
		var best = c.Targets[0].Slot;
		foreach (var t in c.Targets) if (t.Slot < best) best = t.Slot;
		return best;
	}

	/// <summary>
	/// The merchant stop (R6). The bot takes the free heal. SHOP SEAM (YOK-95 / PR #108): when the shop lands on
	/// this base, add <c>MerchantPlan.Shop</c> to <see cref="BotRunConfig"/> and buy here through the shop's API
	/// (<c>RunState.TrySpend</c>) instead of <see cref="Merchant.TakeHeal"/>. Returns what the stop did.
	/// </summary>
	public string AtMerchant(RunSession session)
	{
		if (_cfg.Merchant == MerchantPlan.Heal && session.Merchant is { CanHeal: true } m)
		{
			var r = m.TakeHeal();
			if (!r.Ok) throw new InvalidOperationException($"merchant heal refused: {r.Error}");
			return "heal";
		}
		return "skip";
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
