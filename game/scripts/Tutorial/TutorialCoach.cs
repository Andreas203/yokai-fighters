using System;
using YokaiFighters.Ai;
using YokaiFighters.Sim;

namespace YokaiFighters.Tutorial;

/// <summary>
/// YOK-92, Y6: the apprentice's side of each lesson, so the player has something to block, throw, break and
/// burst out of. Like <see cref="ProfileAi"/> it sees only <see cref="AiView"/> snapshots through a delay of the
/// apprentice's reaction frames (P6, Y4: 30), never the player's input, and it presses buttons through the same
/// sim code as any fighter (K3). Its kit (lightest grounded normal, throw buttons, throw reach) is read once from
/// the fighter's move data at construction.
///
/// Per lesson: Block and Burst = walk in and swing the light normal every <see cref="TutorialOptions.AttackEvery"/>
/// ticks; Throw, Meter and Cancel = stand still (the player acts); ThrowBreak = walk into throw reach and throw,
/// again every <see cref="TutorialOptions.ThrowRetryTicks"/> ticks until the player breaks one. With no lesson on
/// screen <see cref="Next"/> returns null and the behaviour-profile AI fights as usual.
/// </summary>
public sealed class TutorialCoach
{
	public int Self { get; }
	public int ReactionFrames { get; }
	/// <summary>The normal swung in the Block and Burst lessons (fewest startup frames among punches/kicks), or null.</summary>
	public string? LightNormal { get; }
	/// <summary>Distance (centi-units) at which the throw's box reaches the opponent's idle hurtbox.</summary>
	public int ThrowRange { get; }
	/// <summary>Distance (centi-units) at which the light normal's box reaches the opponent's idle hurtbox.</summary>
	public int StrikeRange { get; }

	private readonly TutorialOptions _options;
	private readonly FighterInput _attack;
	private readonly InputBits _throw;
	/// <summary>Each tick's screen with the lesson on screen at that tick: both are acted on ReactionFrames later (P6, Y4).</summary>
	private readonly (AiView View, TutorialLesson? Lesson)?[] _ring;
	private int _head, _wait;
	private TutorialLesson? _lesson;

	public TutorialCoach(Fighter self, int selfIndex, SimConfig config, TutorialOptions? options = null, int reactionFrames = 30)
	{
		Self = selfIndex;
		ReactionFrames = Math.Max(1, reactionFrames);
		_options = options ?? new TutorialOptions();
		_ring = new (AiView, TutorialLesson?)?[ReactionFrames];
		int hurtNear = -config.IdleHurtbox.X; // the defender's hurtbox edge toward the attacker, in units
		MoveData? light = null, grab = null;
		int lightIndex = -1;
		for (int i = 0; i < self.Moves.Length; i++)
		{
			MoveData m = self.Moves[i];
			if (m.IsThrow) grab ??= m;
			// a standing mid (C3: "hold back" blocks it), no direction needed, fewest startup frames
			else if (m.IsNormal && !m.Air && !m.Low && m.DirectionMask == 0 && (light is null || m.Startup < light.Startup)) { light = m; lightIndex = i; }
		}
		LightNormal = light?.Id;
		_attack = light is null ? FighterInput.None : FighterInput.Attack(lightIndex);
		_throw = grab?.ThrowButtons ?? InputBits.None;
		// Never closer than the push boxes allow (walking on would only push the opponent along).
		int touching = config.BodyWidth + 5 * SimConfig.Scale;
		ThrowRange = Math.Max(Reach(grab, hurtNear), touching);
		StrikeRange = Math.Max(Reach(light, hurtNear), touching);
	}

	/// <summary>The furthest any of the move's boxes reaches, plus the opponent's hurtbox half, minus 10 units of slack.</summary>
	private static int Reach(MoveData? m, int hurtNear)
	{
		if (m is null) return 0;
		int reach = 0;
		foreach (var b in m.Hitboxes) reach = Math.Max(reach, b.Box.X + b.Box.W);
		return Math.Max(0, reach + hurtNear - 10) * SimConfig.Scale;
	}

	/// <summary>
	/// Stores this tick's screen and lesson, and decides from the pair <see cref="ReactionFrames"/> old: a new lesson,
	/// like anything else on screen, reaches the apprentice only after its reaction delay. Null = the delayed lesson
	/// needs no apprentice, or nothing is that old yet and no lesson is up (let the profile AI play).
	/// </summary>
	public FighterInput? Next(AiView now, TutorialLesson? lesson)
	{
		var old = _ring[_head];
		_ring[_head] = (now, lesson);
		_head = (_head + 1) % _ring.Length;
		if (old is not { } entry) return lesson is null ? null : FighterInput.None; // the first ReactionFrames ticks
		AiView seen = entry.View;
		TutorialLesson? seenLesson = entry.Lesson;
		if (seenLesson != _lesson) { _lesson = seenLesson; _wait = 0; }
		if (seenLesson is null) return null;
		if (_wait > 0) { _wait--; return FighterInput.None; }
		if (seen.Phase != MatchPhase.Fighting) return FighterInput.None;

		FighterView me = seen.Fighter(Self), them = seen.Fighter(1 - Self);
		int gap = Math.Abs(them.X - me.X);
		InputBits forward = them.X >= me.X ? InputBits.Right : InputBits.Left;
		bool free = me.State == FighterState.Idle && !me.Airborne;
		switch (seenLesson)
		{
			case TutorialLesson.Block:
			case TutorialLesson.Burst:
				if (_attack.Move == 0) return FighterInput.None;
				if (gap > StrikeRange) return new FighterInput(forward);
				if (!free) return FighterInput.None;
				_wait = _options.AttackEvery - 1;
				return _attack;
			case TutorialLesson.ThrowBreak:
				if (_throw == InputBits.None) return FighterInput.None;
				if (gap > ThrowRange) return new FighterInput(forward);
				if (!free || them.Airborne || them.State.IsDown()) return FighterInput.None;
				_wait = _options.ThrowRetryTicks - 1;
				return new FighterInput(_throw);
			default:
				return FighterInput.None; // Throw, Meter, Cancel: the player acts on a still apprentice
		}
	}
}
