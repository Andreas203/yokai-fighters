namespace YokaiFighters.Sim;

/// <summary>YOK-55 partial: jump-in (air) normals (E11).</summary>
public sealed partial record MoveData
{
	/// <summary>
	/// E11: a jump-in normal (normal file <c>"air": true</c>). Picked only while airborne, one per jump;
	/// ground normals are never picked in the air. Not an overhead (E6): either guard blocks it.
	/// </summary>
	public bool Air { get; init; }
	/// <summary>Frames of landing recovery when touchdown ends the air normal; null = SimConfig.AirLandingRecovery.</summary>
	public int? LandingRecovery { get; init; }
}
