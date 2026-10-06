using System.Collections.Generic;

namespace YokaiFighters.Sim;

/// <summary>YOK-47: what a reward card does when picked (A3, A4, A2).</summary>
public enum CardKind
{
	/// <summary>A special Ryo doesn't own: equipped at Lv 1 in its own slot (A3).</summary>
	NewSpecial,
	/// <summary>An owned special goes up one level; the step comes from its data (A4).</summary>
	Upgrade,
	/// <summary>A modifier attached to one owned special (A2: one per special).</summary>
	Modifier,
}

/// <summary>
/// One special a card can be applied to. <see cref="Plain"/>/<see cref="Frames"/> describe the result on
/// that special (e.g. an upgrade's "Spirit Wave Lv 1 → Lv 2: recovery 30 → 26").
/// </summary>
public sealed record CardTarget(SpecialSlot Slot, string SpecialId, string SpecialName, int Level, string Plain, string Frames);

/// <summary>
/// A reward card as the reward screen (YOK-52) shows it. A12: <see cref="Plain"/> first, <see cref="Frames"/>
/// behind the frame-data toggle. <see cref="Targets"/>: where the card can go; when there is more than one,
/// the screen asks the player and passes the chosen slot to <see cref="IRewardDraft.Pick"/>.
/// Badge = "NEW" | "UPGRADE" | "MODIFIER"; AbilityId = the special/modifier id from data; Name = its display
/// name; Source = "kitsune" | "oni" | "kappa" | "ryo".
/// </summary>
public sealed record CardView(
	int Index,
	CardKind Kind,
	string Badge,
	string AbilityId,
	string Name,
	string Plain,
	string Frames,
	string Source,
	IReadOnlyList<CardTarget> Targets)
{
	public bool NeedsTarget => Targets.Count > 1;
}

/// <summary>The three cards after a duel (A7), from the yokai beaten.</summary>
public sealed record RewardOffer(string Yokai, IReadOnlyList<CardView> Cards);

/// <summary>Outcome of <see cref="IRewardDraft.Pick"/>. On failure nothing in the run changed.</summary>
public sealed record PickResult(bool Ok, string Error, CardView? Card, SpecialSlot Slot, int Level)
{
	public static PickResult Fail(string error) => new(false, error, null, SpecialSlot.None, 0);
}

/// <summary>
/// YOK-47 ↔ YOK-52 contract. Build with <see cref="RewardDraft.Build"/>; the screen shows <see cref="Offer"/>
/// and calls <see cref="Pick"/> once. <paramref name="target"/> may be null when the card has one target.
/// </summary>
public interface IRewardDraft
{
	RewardOffer Offer { get; }
	bool Picked { get; }
	PickResult Pick(int cardIndex, SpecialSlot? target = null);
}
