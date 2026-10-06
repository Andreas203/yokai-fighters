namespace YokaiFighters.Sim;

public enum CommandKind : byte { None, Normal, Special }

/// <summary>Special slots shared by both schemes (K1 motions, K2 directions).</summary>
public enum SpecialSlot : byte { None, A, B, C, D }

/// <summary>
/// One parsed command, identical for Kata and Kihon (K3) so the move system never sees the
/// scheme. Precision marks a Kata motion input (+10% damage bonus, K1). Pressed is every
/// button that went down that tick (two-button throws, C4). Direction is the numpad direction
/// on the press tick, relative to facing.
/// </summary>
public readonly record struct InputCommand(
	CommandKind Kind,
	SpecialSlot Slot,
	Motion Motion,
	InputBits Button,
	InputBits Pressed,
	int Direction,
	bool Precision)
{
	public static readonly InputCommand None = new(CommandKind.None, SpecialSlot.None, Motion.None,
		InputBits.None, InputBits.None, 5, false);
}

/// <summary>Turns the raw buffer into this tick's command: KataParser (K1) or KihonParser (K2, YOK-23).</summary>
public interface ICommandParser
{
	InputCommand Parse(InputBuffer buffer, int facing);
}
