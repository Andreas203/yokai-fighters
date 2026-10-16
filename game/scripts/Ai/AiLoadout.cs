using System.Collections.Generic;
using YokaiFighters.Sim;

namespace YokaiFighters.Ai;

/// <summary>
/// The AI fighter's own kit: which slot holds which move id, so a profile's "normal:id" / "special:id" can be
/// pressed. Built once from the fighter's move list and equipped specials (never from input state).
/// </summary>
public sealed class AiLoadout
{
	private readonly Dictionary<string, (FighterInput Input, int Frames)> _moves = new();
	/// <summary>YOK-57: specials with an EX version: the EX slot request and its meter cost (ex.meter_cost).</summary>
	private readonly Dictionary<string, (FighterInput Input, int Cost)> _ex = new();
	public InputBits ThrowButtons { get; private set; }
	public int ThrowFrames { get; private set; }

	public static AiLoadout From(Fighter f)
	{
		var l = new AiLoadout();
		for (int i = 0; i < f.Moves.Length; i++)
		{
			var m = f.Moves[i];
			if (m.IsThrow) { if (l.ThrowButtons == InputBits.None) { l.ThrowButtons = m.ThrowButtons; l.ThrowFrames = m.TotalFrames; } }
			else if (m.IsNormal && !m.Air) l._moves.TryAdd("normal:" + m.Id, (FighterInput.Attack(i), m.TotalFrames));
		}
		for (int s = 1; s < f.Specials.Length; s++)
			if (f.Specials[s] is { } eq && l._moves.TryAdd("special:" + eq.Data.Id, (FighterInput.Special((SpecialSlot)s), eq.Move.TotalFrames)))
				l.AddEx("special:" + eq.Data.Id, (SpecialSlot)s, eq);
		// YOK-94, T0: "special:copied" = the slot the Tanuki's copy sits in (none on any other fighter: the action is skipped).
		string copied = "special:" + BehaviourProfile.CopiedSpecial;
		l._moves.Remove(copied);
		l._ex.Remove(copied);
		if (f.CopiedSlot != SpecialSlot.None && f.Specials[(int)f.CopiedSlot] is { } c)
		{
			l._moves[copied] = (FighterInput.Special(f.CopiedSlot), c.Move.TotalFrames);
			l.AddEx(copied, f.CopiedSlot, c);
		}
		return l;
	}

	private void AddEx(string key, SpecialSlot slot, EquippedSpecial eq)
	{
		if (eq.ExMove is not null && eq.Data.HasEx) _ex[key] = (FighterInput.Special(slot, ex: true), eq.Data.ExCost);
	}

	/// <summary>YOK-57: the EX request for a special action and its meter cost; false if it has no EX version.</summary>
	public bool TryGetEx(AiAction a, out FighterInput input, out int cost)
	{
		input = default; cost = 0;
		if (a.Kind != ActionKind.Special || !_ex.TryGetValue(a.ToString(), out var v)) return false;
		(input, cost) = v;
		return true;
	}

	public bool TryGet(AiAction a, out FighterInput input, out int frames)
	{
		input = default; frames = 0;
		if (!_moves.TryGetValue(a.ToString(), out var v)) return false;
		(input, frames) = v;
		return true;
	}
}
