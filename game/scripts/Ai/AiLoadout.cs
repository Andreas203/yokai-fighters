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
			if (f.Specials[s] is { } eq) l._moves.TryAdd("special:" + eq.Data.Id, (FighterInput.Special((SpecialSlot)s), eq.Move.TotalFrames));
		return l;
	}

	public bool TryGet(AiAction a, out FighterInput input, out int frames)
	{
		input = default; frames = 0;
		if (!_moves.TryGetValue(a.ToString(), out var v)) return false;
		(input, frames) = v;
		return true;
	}
}
