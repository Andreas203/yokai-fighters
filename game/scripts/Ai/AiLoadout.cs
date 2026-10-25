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
	/// <summary>YOK-165: every move id in the kit (normals, air normals, throw, plain specials) → total frames, so the AI can
	/// tell from its own move on screen when it acts again. EX versions share their special's id, so they are kept apart.</summary>
	private readonly Dictionary<string, int> _frames = new(), _exFrames = new();
	/// <summary>YOK-165: per special action ("special:id"), its EX version's total frames.</summary>
	private readonly Dictionary<string, int> _exActionFrames = new();
	public InputBits ThrowButtons { get; private set; }

	/// <summary>YOK-165: total frames of one of this fighter's own moves by id (<paramref name="ex"/> = its EX version, as the
	/// screen shows it, <see cref="FighterView.MoveIsEx"/>); false for an id not in the kit.</summary>
	public bool TryGetTotalFrames(string moveId, bool ex, out int frames) =>
		ex ? _exFrames.TryGetValue(moveId, out frames) || _frames.TryGetValue(moveId, out frames) : _frames.TryGetValue(moveId, out frames);

	/// <summary>YOK-165: total frames of a special action's EX version; false if it has none.</summary>
	public bool TryGetExFrames(AiAction a, out int frames) => _exActionFrames.TryGetValue(a.ToString(), out frames);
	public int ThrowFrames { get; private set; }
	/// <summary>YOK-177: the throw's move id and its slot in the kit (null / -1 = no throw).</summary>
	public string? ThrowMoveId { get; private set; }
	public int ThrowSlot { get; private set; } = -1;
	/// <summary>
	/// YOK-177: the throw as a direct slot request (like the normals' <see cref="FighterInput.Attack"/>), for held presses.
	/// A request on a tick the fighter cannot act is ignored and buffers nothing, unlike the raw throw buttons, whose press
	/// edge buffers a normal command that comes out instead once the fighter can act (Match.cs: the throw needs a fresh edge).
	/// </summary>
	public FighterInput ThrowRequest => ThrowSlot < 0 ? FighterInput.None : FighterInput.Attack(ThrowSlot);

	public static AiLoadout From(Fighter f)
	{
		var l = new AiLoadout();
		for (int i = 0; i < f.Moves.Length; i++)
		{
			var m = f.Moves[i];
			l._frames.TryAdd(m.Id, m.TotalFrames);
			if (m.IsThrow) { if (l.ThrowButtons == InputBits.None) { l.ThrowButtons = m.ThrowButtons; l.ThrowFrames = m.TotalFrames; l.ThrowMoveId = m.Id; l.ThrowSlot = i; } }
			else if (m.IsNormal && !m.Air && l._moves.TryAdd("normal:" + m.Id, (FighterInput.Attack(i), m.TotalFrames))) l._screen["normal:" + m.Id] = (m.Id, null);
		}
		for (int s = 1; s < f.Specials.Length; s++)
			if (f.Specials[s] is { } sp)
			{
				l._frames.TryAdd(sp.Move.Id, sp.Move.TotalFrames);
				if (sp.ExMove is { } exm) l._exFrames.TryAdd(exm.Id, exm.TotalFrames);
			}
		for (int s = 1; s < f.Specials.Length; s++)
			if (f.Specials[s] is { } eq && l._moves.TryAdd("special:" + eq.Data.Id, (FighterInput.Special((SpecialSlot)s), eq.Move.TotalFrames)))
			{
				l.AddEx("special:" + eq.Data.Id, (SpecialSlot)s, eq);
				l._screen["special:" + eq.Data.Id] = (eq.Move.Id, eq.ExMove?.Id);
			}
		// YOK-94, T0: "special:copied" = the slot the Tanuki's copy sits in (none on any other fighter: the action is skipped).
		string copied = "special:" + BehaviourProfile.CopiedSpecial;
		l._moves.Remove(copied);
		l._ex.Remove(copied);
		l._exActionFrames.Remove(copied);
		l._screen.Remove(copied);
		if (f.CopiedSlot != SpecialSlot.None && f.Specials[(int)f.CopiedSlot] is { } c)
		{
			l._moves[copied] = (FighterInput.Special(f.CopiedSlot), c.Move.TotalFrames);
			l.AddEx(copied, f.CopiedSlot, c);
			l._screen[copied] = (c.Move.Id, c.ExMove?.Id);
		}
		if (l.ThrowMoveId is { } t) l._screen["throw"] = (t, null);
		return l;
	}

	private readonly Dictionary<string, (string Plain, string? Ex)> _screen = new();

	/// <summary>
	/// YOK-177: the move id(s) an action shows on screen when it starts: a normal's id, a special's move (and its EX
	/// move), the copied special's, the throw's. False for anything else (walk, block...) or an action the kit lacks.
	/// </summary>
	public bool IsScreenMoveOf(AiAction a, string? moveId) =>
		moveId is not null && _screen.TryGetValue(a.ToString(), out var ids) && (ids.Plain == moveId || ids.Ex == moveId);

	private void AddEx(string key, SpecialSlot slot, EquippedSpecial eq)
	{
		if (eq.ExMove is not null && eq.Data.HasEx)
		{
			_ex[key] = (FighterInput.Special(slot, ex: true), eq.Data.ExCost);
			_exActionFrames[key] = eq.ExMove.TotalFrames; // YOK-165
		}
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
