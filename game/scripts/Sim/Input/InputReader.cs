namespace YokaiFighters.Sim;

/// <summary>
/// One fighter's input layer: raw history (InputBuffer), the scheme's parser (Kata or Kihon,
/// <see cref="Scheme"/>) and the command buffer. Call Update once per sim tick with that tick's raw input and
/// the fighter's facing; the move system then calls TryConsume. A parsed command waits
/// CommandBuffer ticks, so a press during recovery or hitstop still comes out on the first
/// actionable tick. A newer command replaces a waiting one, except that a normal never replaces
/// a waiting special. Pure C#, deterministic: same inputs, same commands.
/// </summary>
public sealed class InputReader
{
	public InputConfig Config { get; }
	public InputBuffer Buffer { get; }
	public ICommandParser Parser { get; set; }

	InputCommand _pending = InputCommand.None;
	int _pendingAge;

	public InputReader(InputConfig? cfg = null, ICommandParser? parser = null)
	{
		Config = cfg ?? InputConfig.Default;
		Buffer = new InputBuffer(Config.HistoryTicks);
		Parser = parser ?? new KataParser(Config);
	}

	/// <summary>
	/// YOK-23: Kata (K1) or Kihon (K2). Setting it swaps only the parser; history, the waiting command
	/// and everything else stay, so a mid-session switch changes parsing and nothing more. Reset keeps it.
	/// </summary>
	public ControlScheme Scheme
	{
		get => Parser is KihonParser ? ControlScheme.Kihon : ControlScheme.Kata;
		set
		{
			if (value == Scheme) return;
			Parser = value == ControlScheme.Kihon ? new KihonParser(Config) : new KataParser(Config);
		}
	}

	/// <summary>The command parsed on this tick (None if no button went down).</summary>
	public InputCommand Latest { get; private set; } = InputCommand.None;
	/// <summary>The command waiting to be consumed, if any.</summary>
	public InputCommand Pending => _pending;
	/// <summary>Numpad direction this tick, relative to the facing last passed in.</summary>
	public int Direction { get; private set; } = 5;

	/// <summary>Dash request this tick, relative to facing: +1 = 66, -1 = 44, 0 = none (C2).</summary>
	public int Dash { get; private set; }

	public void Update(FighterInput raw, int facing)
	{
		Buffer.Push(raw);
		Direction = Buffer.Dir(0, facing);
		Dash = DetectDash(facing);
		if (_pending.Kind != CommandKind.None && ++_pendingAge > Config.CommandBuffer)
			_pending = InputCommand.None;
		Latest = Parser.Parse(Buffer, facing);
		if (Latest.Kind == CommandKind.None) return;
		if (_pending.Kind == CommandKind.Special && Latest.Kind == CommandKind.Normal) return;
		_pending = Latest;
		_pendingAge = 0;
	}

	/// <summary>
	/// 66 / 44: a fresh 6 (or 4) now, something else on the tick before, and the same direction again
	/// within DashWindow ticks. Exact 6/4 only, so a diagonal never dashes (9 jumps, 3 crouches).
	/// </summary>
	int DetectDash(int facing)
	{
		int d = Direction;
		if (d != 6 && d != 4) return 0;
		if (Buffer.Dir(1, facing) == d) return 0;
		for (int age = 2; age <= Config.DashWindow && age < Buffer.Count; age++)
			if (Buffer.Dir(age, facing) == d) return d == 6 ? 1 : -1;
		return 0;
	}

	public bool TryConsume(out InputCommand cmd)
	{
		cmd = _pending;
		if (cmd.Kind == CommandKind.None) return false;
		_pending = InputCommand.None;
		return true;
	}

	public void Reset()
	{
		Buffer.Clear();
		_pending = Latest = InputCommand.None;
		_pendingAge = 0;
		Direction = 5;
		Dash = 0;
	}
}
