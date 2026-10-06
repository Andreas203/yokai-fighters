using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-22 debug overlay + frame-step, debug builds only (FightScene adds it when OS.IsDebugBuild()).
/// F1 toggles the box overlay, F2 pauses/resumes, F3 advances exactly one tick (pausing first if running),
/// F4 switches P1 between Kihon and Kata (YOK-23; the box labels name each player's scheme).
/// Keys avoid the E9 game layouts. Boxes come from <see cref="DebugBoxes"/> (the sim's own data for the
/// current frame); this node only projects them from the Z = 0 fight plane to the screen.
/// Colours: red hit (dim once the move connected), green hurt, yellow throw, blue push.
/// </summary>
public partial class DebugOverlay : CanvasLayer
{
	public const Key ToggleKey = Key.F1, PauseKey = Key.F2, StepKey = Key.F3, SchemeKey = Key.F4;

	/// <summary>The fight this overlay reads: its parent <see cref="FightScene"/> (the overlay is an instance of
	/// <c>scenes/ui/debug_overlay.tscn</c> in fight.tscn), or set it before adding the node.</summary>
	public FightScene Scene { get; set; } = null!;
	private FightScene _scene => Scene;
	private Control _canvas = null!;

	public bool ShowBoxes { get; set; }
	public FrameStepper Stepper => _scene.Stepper;

	public override void _Ready()
	{
		Scene ??= GetParent<FightScene>();
		_canvas = Ui.UiFind.Get<Control>(this, "Canvas");
		_canvas.Draw += DrawOverlay;
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (e is InputEventKey k && k.Pressed && !k.Echo && HandleKey(k.PhysicalKeycode))
			GetViewport().SetInputAsHandled();
	}

	/// <summary>Key handling, public so tests can press keys without an input event.</summary>
	public bool HandleKey(Key key)
	{
		switch (key)
		{
			case ToggleKey: ShowBoxes = !ShowBoxes; break;
			case PauseKey: Stepper.TogglePause(); break;
			case StepKey: Stepper.RequestStep(); break;
			case SchemeKey: _scene.ToggleScheme(0); break;
			default: return false;
		}
		_canvas.QueueRedraw();
		return true;
	}

	// Children process after their parent, so this redraws after FightScene ran the tick's sim steps.
	public override void _Process(double delta) => _canvas.QueueRedraw();

	private static Color ColorOf(DebugBox b) => b.Kind switch
	{
		DebugBoxKind.Hit => b.Spent ? new Color(1f, 0.2f, 0.2f, 0.4f) : new Color(1f, 0.15f, 0.15f),
		DebugBoxKind.Hurt => new Color(0.2f, 1f, 0.3f),
		DebugBoxKind.Throw => new Color(1f, 0.9f, 0.1f),
		DebugBoxKind.Projectile => new Color(1f, 0.3f, 0.9f),
		_ => new Color(0.3f, 0.6f, 1f),
	};

	/// <summary>World centi-units on the fight plane (Z = 0, F4) to screen pixels.</summary>
	private static Vector2 Screen(Camera3D cam, int x, int y) =>
		cam.UnprojectPosition(new Vector3(FightScene.ToMeters(x), FightScene.ToMeters(y), 0f));

	private void DrawOverlay()
	{
		var cam = GetViewport()?.GetCamera3D();
		Font font = ThemeDB.FallbackFont;
		if (Stepper.Paused)
			_canvas.DrawString(font, new Vector2(60, 300), $"PAUSED  tick {_scene.Match.Tick}  (F2 resume, F3 step)",
				HorizontalAlignment.Left, -1, 20, Colors.White);
		if (!ShowBoxes || cam == null) return;

		Match m = _scene.Match;
		for (int i = 0; i < 2; i++)
		{
			foreach (var b in DebugBoxes.For(m, i))
			{
				Vector2 a = Screen(cam, b.X0, b.Y0), c = Screen(cam, b.X1, b.Y1);
				var r = new Rect2(a, Vector2.Zero).Expand(c);
				var col = ColorOf(b);
				_canvas.DrawRect(r, new Color(col, col.A * 0.25f));
				_canvas.DrawRect(r, col, false, 2f);
			}
			// Labels sit under the HUD on each player's side so they never overlap when fighters touch.
			string text = $"P{i + 1} {m.Fighters[i].Input.Scheme}  {DebugBoxes.Label(m.Fighters[i])}";
			Vector2 size = font.GetStringSize(text, HorizontalAlignment.Left, -1, 20);
			float width = _canvas.Size.X;
			var pos = new Vector2(i == 0 ? 60 : width - 60 - size.X, 260);
			_canvas.DrawRect(new Rect2(pos - new Vector2(6, 22), size + new Vector2(12, 10)), new Color(0, 0, 0, 0.6f));
			_canvas.DrawString(font, pos, text, HorizontalAlignment.Left, -1, 20, Colors.White);
		}
	}
}
