using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// Read-only HUD view of Ryo's meter and burst (C5, C6). <see cref="MatchHudView"/> is the live one
/// over the sim (YOK-20), assigned to <see cref="FightHud.View"/>. The HUD never writes through it.
/// </summary>
public interface IHudView
{
	/// <summary>Meter in points, 0..MeterMax (3 bars x 100, C5).</summary>
	int Meter { get; }
	int MeterMax { get; }
	/// <summary>True while the once-per-fight burst is unspent (C6).</summary>
	bool BurstAvailable { get; }
}

/// <summary>YOK-20: live meter and burst of one fighter (0 = Ryo), read straight from the Match each frame.</summary>
public sealed class MatchHudView : IHudView
{
	private readonly Match _match;
	private readonly int _fighter;

	public MatchHudView(Match match, int fighter = 0)
	{
		_match = match;
		_fighter = fighter;
	}

	public int Meter => _match.Fighters[_fighter].Meter;
	public int MeterMax => _match.Config.MeterMax;
	public bool BurstAvailable => !_match.Fighters[_fighter].BurstUsed;
}
