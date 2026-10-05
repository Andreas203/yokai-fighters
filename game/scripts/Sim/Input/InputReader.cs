namespace YokaiFighters.Sim;

/// <summary>
/// One fighter's input layer: raw history (InputBuffer), a scheme parser (Kata now, Kihon in
/// YOK-23) and the command buffer. Call Update once per sim tick with that tick's raw input and
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

	/// <summary>The command parsed on this tick (None if no button went down).</summary>
	public InputCommand Latest { get; private set; } = InputCommand.None;
	/// <summary>The command waiting to be consumed, if any.</summary>
	public InputCommand Pending => _pending;
	/// <summary>Numpad direction this tick, relative to the facing last passed in.</summary>
	public int Direction { get; private set; } = 5;

	public void Update(FighterInput raw, int facing)
	{
		Buffer.Push(raw);
		Direction = Buffer.Dir(0, facing);
		if (_pending.Kind != CommandKind.None && ++_pendingAge > Config.CommandBuffer)
			_pending = InputCommand.None;
		Latest = Parser.Parse(Buffer, facing);
		if (Latest.Kind == CommandKind.None) return;
		if (_pending.Kind == CommandKind.Special && Latest.Kind == CommandKind.Normal) return;
		_pending = Latest;
		_pendingAge = 0;
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
	}
}
