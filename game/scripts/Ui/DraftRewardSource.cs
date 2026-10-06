using System;
using System.Linq;
using YokaiFighters.Sim;
using SimCard = YokaiFighters.Sim.CardView;
using SimKind = YokaiFighters.Sim.CardKind;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-47 adapter: the reward draft (<see cref="IRewardDraft"/>, pure C#) in the reward screen's shape
/// (<see cref="IRewardSource"/>, YOK-52). No rules here: eligibility and text come from the draft.
/// The offer-level <see cref="RewardOffer.Targets"/> are slots A-D for the offer's modifier card
/// (eligible = the draft lists that special as a target). <see cref="Pick"/> throws when the draft refuses.
/// </summary>
public sealed class DraftRewardSource : IRewardSource
{
	readonly IRewardDraft _draft;
	readonly RunState _run;

	public RewardOffer Offer { get; }
	public PickResult? LastResult { get; private set; }

	public DraftRewardSource(IRewardDraft draft, RunState run)
	{
		_draft = draft;
		_run = run;
		var cards = draft.Offer.Cards.Select(ToView).ToArray();
		var mod = draft.Offer.Cards.FirstOrDefault(c => c.Kind == SimKind.Modifier);
		var targets = Enumerable.Range(1, 4).Select(i =>
		{
			var slot = (SpecialSlot)i;
			var owned = run[slot];
			if (owned is null) return new TargetView("", slot.ToString(), "Empty", 0, null, false, "Empty slot");
			bool ok = mod?.Targets.Any(t => t.Slot == slot) == true;
			string? why = ok || mod is null ? null
				: owned.Modifier != null ? $"Already carries {owned.Modifier.Name}"
				: "Can't carry this modifier";
			return new TargetView(owned.Data.Id, slot.ToString(), owned.Data.Name, owned.Level, owned.Modifier?.Name, ok, why);
		}).ToArray();
		Offer = new RewardOffer(Title(draft.Offer.Yokai), cards, targets);
	}

	static string Title(string id) => id.Length == 0 ? id : char.ToUpperInvariant(id[0]) + id[1..];

	static CardView ToView(SimCard c) => c.Kind switch
	{
		SimKind.NewSpecial => new CardView(CardKind.NewMove, c.AbilityId, c.Name, $"{Title(c.Source)} - special",
			c.Plain, c.Frames, "NEW - Lv 1"),
		SimKind.Upgrade => new CardView(CardKind.Upgrade, c.AbilityId, c.Name, "Your special", c.Plain, c.Frames,
			c.Targets.Count == 1 ? $"UPGRADE - Lv {c.Targets[0].Level} to {c.Targets[0].Level + 1}" : "UPGRADE",
			c.NeedsTarget ? "Pick the special to raise" : null),
		_ => new CardView(CardKind.Modifier, c.AbilityId, c.Name, $"{Title(c.Source)} - modifier", c.Plain, c.Frames,
			"MODIFIER", "Pick the special to carry it"),
	};

	public void Pick(int cardIndex, string? targetId)
	{
		SpecialSlot? slot = null;
		if (!string.IsNullOrEmpty(targetId))
		{
			var hit = _run.Owned.FirstOrDefault(o => o.Special.Data.Id == targetId);
			if (hit.Special is null) throw new ArgumentException($"no owned special '{targetId}'", nameof(targetId));
			slot = hit.Slot;
		}
		LastResult = _draft.Pick(cardIndex, slot);
		if (!LastResult.Ok) throw new InvalidOperationException(LastResult.Error);
	}
}
