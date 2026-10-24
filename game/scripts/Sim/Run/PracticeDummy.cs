namespace YokaiFighters.Sim;

/// <summary>The training dummy's settings in practice mode (YOK-197): what it holds every tick.</summary>
public enum PracticeStance
{
	/// <summary>Stands idle, holding nothing.</summary>
	Stand,
	/// <summary>Holds back: guards standing.</summary>
	Block,
	/// <summary>Holds down.</summary>
	Crouch,
	/// <summary>Holds down-back: guards crouching.</summary>
	CrouchBlock,
}

/// <summary>
/// YOK-197, GDD 8: practice mode's training dummy, pure C#. It holds one stance for as long as it is set and never
/// attacks, jumps or reads anything on screen, so it needs no behaviour profile and no Y4 reaction row. The stances
/// are the held ones of the dojo trial's dummy (R7, <see cref="TrialRun.HeldInput"/>), so the dummy guards exactly
/// as a trial's does. Deterministic: the input depends only on the stance and the dummy's facing.
/// </summary>
public sealed class PracticeDummy
{
	public const int StanceCount = 4;

	public PracticeStance Stance { get; set; } = PracticeStance.Stand;

	/// <summary>The next stance in menu order (Stand → Block → Crouch → Crouch block → Stand).</summary>
	public PracticeStance Next()
	{
		Stance = (PracticeStance)(((int)Stance + 1) % StanceCount);
		return Stance;
	}

	/// <summary>The trial dummy's held action for <paramref name="stance"/>.</summary>
	public static TrialDummyAction ActionOf(PracticeStance stance) => stance switch
	{
		PracticeStance.Block => TrialDummyAction.Guard,
		PracticeStance.Crouch => TrialDummyAction.Crouch,
		PracticeStance.CrouchBlock => TrialDummyAction.CrouchGuard,
		_ => TrialDummyAction.Idle,
	};

	/// <summary>The dummy's input this tick.</summary>
	public FighterInput Input(Fighter dummy) => TrialRun.HeldInput(ActionOf(Stance), dummy.Facing);

	public static string Name(PracticeStance stance) => stance switch
	{
		PracticeStance.Block => "Block",
		PracticeStance.Crouch => "Crouch",
		PracticeStance.CrouchBlock => "Crouch block",
		_ => "Stand",
	};
}
