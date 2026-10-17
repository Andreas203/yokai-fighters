using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-74: read-only view of the run the HUD needs for the copy-target talisman and the twist strip. The
/// answers come from the sim (<see cref="CopyRule"/>, <see cref="RunSession"/>); the HUD holds no rule of its own.
/// </summary>
public interface IHudRunView
{
	/// <summary>T1: true from Ryo's entry to the Tanuki's node on; the target no longer changes.</summary>
	bool CopyLocked { get; }
	/// <summary>T2-T4: the live target before the lock, the locked copy after it; null when no slot holds a special.</summary>
	CopyTarget? Copy { get; }
	/// <summary>The node being fought is the copy-lock (Tanuki) node.</summary>
	bool InTanukiFight { get; }
	/// <summary>Y3: the node being fought is an elder's.</summary>
	bool ElderFight { get; }
}

/// <summary>The live view over a <see cref="RunSession"/> (null session = a fight with no run: nothing to show).</summary>
public sealed class SessionHudRunView : IHudRunView
{
	private readonly RunSession? _session;

	public SessionHudRunView(RunSession? session) => _session = session;

	public bool CopyLocked => _session?.CopyLocked ?? false;
	public CopyTarget? Copy => _session is null ? null : _session.CopyLocked ? _session.LockedCopy : CopyRule.Target(_session.Run);
	public bool InTanukiFight => _session?.Node is { } n && RunSession.IsCopyLockNode(n);
	public bool ElderFight => _session?.Node?.Elder ?? false;
}
