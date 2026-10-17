using System;

namespace YokaiFighters.Ai;

/// <summary>
/// YOK-99, Y5: each Tanuki win (max 3) makes yokai react 1 frame faster and pushes them toward aggressive.
/// Pure and deterministic: the count of Tanuki wins (kept across runs in the save file, read by the host and handed
/// to the fight) turns into the fight's reaction frames and an aggression bonus on the profile's
/// <see cref="BehaviourProfile.AggressionBias"/>. Applied to every AI opponent of a run, the Tanuki included.
/// The count itself is never capped here; only its effect is (<see cref="MaxWins"/>).
/// </summary>
public readonly record struct TanukiPressure(int Wins)
{
	/// <summary>Y5: the pressure stops growing after three wins.</summary>
	public const int MaxWins = 3;
	/// <summary>Y5: one frame faster per win.</summary>
	public const int ReactionFramesPerWin = 1;
	/// <summary>
	/// PROVISIONAL (YOK-99, awaiting the designer): Y5 says "pushes them toward aggressive" without a number.
	/// Aggression-bias points added per win (bias is 0-100, see <see cref="ProfileAi"/>'s neutral weights).
	/// </summary>
	public const int AggressionPerWin = 10;
	/// <summary>The fastest any AI may react, whatever the pressure (a reaction buffer needs one frame).</summary>
	public const int MinReactionFrames = 1;

	public static readonly TanukiPressure None = new(0);

	/// <summary>The wins that count toward the pressure: 0 to <see cref="MaxWins"/> (a negative count reads as 0).</summary>
	public int Steps => Math.Clamp(Wins, 0, MaxWins);

	/// <summary>Reaction frames after the pressure: <paramref name="baseFrames"/> (Y4 tier) minus one per win, never below <see cref="MinReactionFrames"/>.</summary>
	public int ReactionFrames(int baseFrames) => Math.Max(MinReactionFrames, baseFrames - Steps * ReactionFramesPerWin);

	/// <summary>Aggression-bias points added to the profile's own bias (the total is capped at 100 by <see cref="ProfileAi"/>).</summary>
	public int AggressionBonus => Steps * AggressionPerWin;
}
