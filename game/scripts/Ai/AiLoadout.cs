using System.Collections.Generic;
using System.Linq;
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
	/// <summary>YOK-165: every move id in the kit (normals, air normals, throw, specials and their EX) → total frames, so the
	/// AI can tell from its own move on screen when it acts again.</summary>
	private readonly Dictionary<string, int> _frames = new();
	public InputBits ThrowButtons { get; private set; }

	/// <summary>YOK-165: total frames of one of this fighter's own moves by id; false for an id not in the kit.</summary>
	public bool TryGetTotalFrames(string moveId, out int frames) => _frames.TryGetValue(moveId, out frames);
	public int ThrowFrames { get; private set; }

	public static AiLoadout From(Fighter f)
	{
		var l = new AiLoadout();
		for (int i = 0; i < f.Moves.Length; i++)
		{
			var m = f.Moves[i];
			l._frames.TryAdd(m.Id, m.TotalFrames);
			if (m.IsThrow) { if (l.ThrowButtons == InputBits.None) { l.ThrowButtons = m.ThrowButtons; l.ThrowFrames = m.TotalFrames; } }
			else if (m.IsNormal && !m.Air) l._moves.TryAdd("normal:" + m.Id, (FighterInput.Attack(i), m.TotalFrames));
		}
		for (int s = 1; s < f.Specials.Length; s++)
			if (f.Specials[s] is { } sp)
			{
				l._frames.TryAdd(sp.Move.Id, sp.Move.TotalFrames);
				if (sp.ExMove is { } exm) l._frames.TryAdd(exm.Id, exm.TotalFrames);
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

	/// <summary>
	/// YOK-174: the actions <paramref name="p"/> names (habit and behaviours) that this fighter cannot do: a
	/// <c>normal:</c> or <c>special:</c> id it does not have, or <c>throw</c> with no throw. <c>special:copied</c> is
	/// left out (it exists only after the Tanuki's copy lock, T0). Empty = every action resolves.
	/// </summary>
	public IReadOnlyList<string> Missing(BehaviourProfile p)
	{
		var missing = new List<string>();
		foreach (var a in p.Behaviours.Select(b => b.Do).Prepend(p.Habit.Do))
		{
			bool ok = a.Kind switch
			{
				ActionKind.Normal => _moves.ContainsKey(a.ToString()),
				ActionKind.Special => a.MoveId == BehaviourProfile.CopiedSpecial || _moves.ContainsKey(a.ToString()),
				ActionKind.Throw => ThrowButtons != InputBits.None,
				_ => true,
			};
			if (!ok && !missing.Contains(a.ToString())) missing.Add(a.ToString());
		}
		return missing;
	}

	/// <summary>YOK-165: this kit can perform the move action (a throw needs throw buttons; a normal or special must be in
	/// the loadout). No side effects: the AI checks it before a weighted pick, without drawing.</summary>
	public bool CanDo(AiAction a) => a.Kind == ActionKind.Throw ? ThrowButtons != InputBits.None : TryGet(a, out _, out _);

	public bool TryGet(AiAction a, out FighterInput input, out int frames)
	{
		input = default; frames = 0;
		if (!_moves.TryGetValue(a.ToString(), out var v)) return false;
		(input, frames) = v;
		return true;
	}
}
