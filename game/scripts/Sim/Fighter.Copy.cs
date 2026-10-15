namespace YokaiFighters.Sim;

public sealed partial class Fighter
{
	/// <summary>
	/// YOK-94, T0: the slot holding the special this fighter copied from Ryo (the Tanuki only), else None. Set by
	/// <see cref="TanukiKit.Equip"/>; read by the AI loadout so a profile's <c>special:copied</c> presses it.
	/// Run state like <see cref="Specials"/>: Reset keeps it. It changes no sim outcome on its own.
	/// </summary>
	public SpecialSlot CopiedSlot { get; internal set; }
}
