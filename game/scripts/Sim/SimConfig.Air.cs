namespace YokaiFighters.Sim;

/// <summary>YOK-55: jump-in normals (E11).</summary>
public sealed partial record SimConfig
{
	/// <summary>
	/// **Designer proposal**: frames an air normal's landing costs when the data gives no
	/// <c>landing_recovery</c>. Touchdown ends the move; the fighter stands, can't act or block, and can be thrown.
	/// </summary>
	public int AirLandingRecovery { get; init; } = 3;
}
