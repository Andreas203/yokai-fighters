using System.Collections.Generic;

namespace YokaiFighters.Ui;

public enum CardKind { NewMove, Upgrade, Modifier }

/// <summary>One reward card as the UI shows it. All text comes from card data (A12): plain language first, frame data behind a toggle.</summary>
public sealed record CardView(
	CardKind Kind, string Id, string Name, string Source, string Plain, string Frames,
	string Tag, string? TargetPrompt = null);

/// <summary>A special slot the modifier card can attach to (A2: one modifier per special).</summary>
public sealed record TargetView(string Id, string Slot, string Name, int Level, string? CurrentModifier, bool Eligible, string? WhyNot = null);

public sealed record RewardOffer(string Yokai, IReadOnlyList<CardView> Cards, IReadOnlyList<TargetView> Targets);

/// <summary>
/// What the reward screen needs from the draft system (YOK-47). Adapter needed: wrap the draft system's
/// RewardOffer/CardView/Pick(cardIndex, targetSpecial?) in this shape; the screen holds no game rules.
/// </summary>
public interface IRewardSource
{
	RewardOffer Offer { get; }
	/// <summary>Commit a card. <paramref name="targetId"/> is required for modifier cards (a <see cref="TargetView.Id"/>), null otherwise.</summary>
	void Pick(int cardIndex, string? targetId);
}

/// <summary>Story-card text source (habit-writer owns data/story, YOK-44). S3 template; {yokai} is filled by the caller.</summary>
public interface IStorySource { string BindingLine(string yokai); }

/// <summary>TEST FIXTURE: Kitsune offer matching rules A7/A12 and the sample card data. Replace with the YOK-47 adapter and YOK-44 story data.</summary>
public sealed class StubRewardSource : IRewardSource, IStorySource
{
	public (int Card, string? Target)? LastPick { get; private set; }

	public RewardOffer Offer { get; } = new("Kitsune", new[]
	{
		new CardView(CardKind.NewMove, "foxfire", "Foxfire", "Kitsune - special", "A slow, drifting flame. It leaves fading copies behind it.", "Startup 15 / Active 4 / Recovery 30\nDamage 70 / Slow projectile", "NEW - Lv 1"),
		new CardView(CardKind.Upgrade, "spirit-wave", "Spirit Wave", "Your special", "Your wave hits harder and recovers faster.", "Lv 1 -> Lv 2\nRecovery -4 frames (30 -> 26)", "UPGRADE - Lv 1 to 2"),
		new CardView(CardKind.Modifier, "will-o-wisp", "Will-o'-wisp", "Kitsune - modifier", "Your projectiles fly 30% faster.", "Projectile speed x1.3", "MODIFIER",
			"Pick the special to carry it"),
	}, new[]
	{
		new TargetView("spirit-wave", "A", "Spirit Wave", 1, null, true),
		new TargetView("rising-talisman", "B", "Rising Talisman", 1, null, false, "Needs a projectile"),
		new TargetView("", "C", "Empty", 0, null, false, "Empty slot"),
		new TargetView("", "D", "Empty", 0, null, false, "Empty slot"),
	});

	public void Pick(int cardIndex, string? targetId) => LastPick = (cardIndex, targetId);

	public string BindingLine(string yokai) => $"Forgive me, {yokai}. I'll give it back.";
}
