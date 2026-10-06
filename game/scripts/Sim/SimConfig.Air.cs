namespace YokaiFighters.Sim;

/// <summary>YOK-55: jump-in normals (E11, E19).</summary>
public sealed partial record SimConfig
{
	/// <summary>
	/// E19 (designer-accepted): frames an air normal's landing costs when the data gives no
	/// <c>landing_recovery</c>. Touchdown ends the move; the fighter stands, can't act, block or cancel, and can be thrown.
	/// </summary>
	public int AirLandingRecovery { get; init; } = 3;
}
