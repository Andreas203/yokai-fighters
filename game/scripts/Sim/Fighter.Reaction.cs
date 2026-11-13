namespace YokaiFighters.Sim;

/// <summary>
/// YOK-226: hit-reaction state for <see cref="MoveData.Launch"/>, <see cref="MoveData.WallBounce"/> and
/// <see cref="MoveData.MultiHit"/>. Hashed only once set, so fights without them keep their replay hashes.
/// </summary>
public sealed partial class Fighter
{
	/// <summary>Launched or wall-bounced and not yet landed: juggleable, can't be launched again, lands in a knockdown.</summary>
	public bool Launched;
	/// <summary>Body connects of the current use of a multi-hit move (0 for every other move).</summary>
	public int HitsLanded;
	/// <summary>Move frame of the last of those connects.</summary>
	public int LastHitFrame;

	internal ulong HashReaction(ulong h)
	{
		if (Launched) h = Fnv.Mix(h, 0x1A);
		if (HitsLanded != 0) h = Fnv.Mix(h, HitsLanded | LastHitFrame << 8);
		return h;
	}
}
