namespace YokaiFighters.Fight;

/// <summary>
/// Read-only HUD view of Ryo's meter and burst (C5, C6). The sim does not track these yet (YOK-20):
/// until then <see cref="StubHudView"/> supplies values; YOK-20 implements this interface over the
/// real fighter state and assigns it to <see cref="FightHud.View"/>. The HUD never writes through it.
/// </summary>
public interface IHudView
{
	/// <summary>Meter in points, 0..MeterMax (3 bars x 100, C5).</summary>
	int Meter { get; }
	int MeterMax { get; }
	/// <summary>True while the once-per-fight burst is unspent (C6).</summary>
	bool BurstAvailable { get; }
}

/// <summary>Fixed stub values so the layout can be reviewed before YOK-20.</summary>
public sealed class StubHudView : IHudView
{
	public int Meter { get; set; } = 140;
	public int MeterMax => 300;
	public bool BurstAvailable { get; set; } = true;
}
