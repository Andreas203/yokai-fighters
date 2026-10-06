using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Ui;

public enum RewardStep { Cards, Target }

/// <summary>
/// Reward screen (GDD fig. 5, A7, A12): three talisman cards from the beaten yokai, plain language first,
/// frame data behind a toggle (Tab / pad Y / click the toggle). A modifier card opens a second step to pick
/// the special that carries it. Everything shown comes from <see cref="IRewardSource"/>; no game rules here.
/// Input: Left/Right (or A/D-free: arrows only) + d-pad/stick to move, Enter / pad A to pick, Esc / pad B to go back,
/// Tab / pad Y toggles frame data. None of these are fight keys (Space/WASD/UIOJKL are), and the screen only reads input while shown.
/// Raises <see cref="Picked"/> after <c>IRewardSource.Pick</c>; routing afterwards is YOK-48.
/// </summary>
public partial class RewardScreen : Control
{
	/// <summary>(cardIndex, targetId or null) once the choice has been sent to the draft system.</summary>
	public event Action<int, string?>? Picked;

	public RewardStep Step { get; private set; } = RewardStep.Cards;
	public int CardCursor { get; private set; }
	public int TargetCursor { get; private set; }
	public bool FrameDataVisible { get; private set; }
	public bool Showing { get; private set; }

	private IRewardSource? _source;
	private Rect2[] _cardRects = Array.Empty<Rect2>(), _targetRects = Array.Empty<Rect2>();
	private Rect2 _toggleRect;

	private static readonly Color Indigo = new(0.2f, 0.25f, 0.5f), Grey = new(0.45f, 0.45f, 0.45f);

	private void FitViewport() { Position = Vector2.Zero; Size = GetViewportRect().Size; QueueRedraw(); }

	public override void _Ready()
	{
		FitViewport(); GetViewport().SizeChanged += FitViewport;
		MouseFilter = MouseFilterEnum.Stop;
		Visible = Showing;
	}

	public void Present(IRewardSource source)
	{
		_source = source; Step = RewardStep.Cards; CardCursor = 0; TargetCursor = 0; FrameDataVisible = false;
		Showing = true; Visible = true; QueueRedraw();
	}

	/// <summary>YOK-48: hide without picking (the flow restarted underneath it).</summary>
	public void Dismiss() { Showing = false; Visible = false; _source = null; }

	private RewardOffer? Offer => _source?.Offer;

	public void Move(int dir)
	{
		if (!Showing || Offer == null) return;
		if (Step == RewardStep.Cards) CardCursor = Mod(CardCursor + dir, Offer.Cards.Count);
		else TargetCursor = Mod(TargetCursor + dir, Offer.Targets.Count);
		QueueRedraw();
	}

	public void ToggleFrameData() { if (Showing && Step == RewardStep.Cards) { FrameDataVisible = !FrameDataVisible; QueueRedraw(); } }

	public void Back() { if (Showing && Step == RewardStep.Target) { Step = RewardStep.Cards; QueueRedraw(); } }

	/// <summary>Pick the highlighted card; a modifier card first opens the target step, and an ineligible target is refused.</summary>
	public void Confirm()
	{
		if (!Showing || Offer == null) return;
		if (Step == RewardStep.Cards)
		{
			if (Offer.Cards[CardCursor].Kind == CardKind.Modifier)
			{
				Step = RewardStep.Target;
				TargetCursor = Math.Max(0, Array.FindIndex(Offer.Targets.ToArray(), t => t.Eligible));
				QueueRedraw();
			}
			else Commit(CardCursor, null);
		}
		else
		{
			TargetView t = Offer.Targets[TargetCursor];
			if (t.Eligible) Commit(CardCursor, t.Id);
		}
	}

	private void Commit(int card, string? target)
	{
		_source!.Pick(card, target);
		Showing = false; Visible = false;
		Picked?.Invoke(card, target);
	}

	private static int Mod(int a, int n) => n == 0 ? 0 : ((a % n) + n) % n;

	public override void _UnhandledInput(InputEvent e)
	{
		if (!Showing) return;
		bool handled = true;
		switch (e)
		{
			case InputEventKey { Pressed: true, Echo: false } k:
				switch (k.Keycode)
				{
					case Key.Left: Move(-1); break;
					case Key.Right: Move(1); break;
					case Key.Enter: case Key.KpEnter: Confirm(); break;
					case Key.Escape: Back(); break;
					case Key.Tab: ToggleFrameData(); break;
					default: handled = false; break;
				}
				break;
			case InputEventJoypadButton { Pressed: true } j:
				switch (j.ButtonIndex)
				{
					case JoyButton.DpadLeft: Move(-1); break;
					case JoyButton.DpadRight: Move(1); break;
					case JoyButton.A: Confirm(); break;
					case JoyButton.B: Back(); break;
					case JoyButton.Y: ToggleFrameData(); break;
					default: handled = false; break;
				}
				break;
			case InputEventJoypadMotion { AxisValue: var v } m when m.Axis == JoyAxis.LeftX && Math.Abs(v) >= InputDevices.StickDeadzone && !_stickHeld:
				_stickHeld = true; Move(v < 0 ? -1 : 1);
				break;
			case InputEventJoypadMotion m2 when m2.Axis == JoyAxis.LeftX && Math.Abs(m2.AxisValue) < 0.3f:
				_stickHeld = false; handled = false; break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb:
				Click(mb.Position); break;
			case InputEventMouseMotion mm:
				Hover(mm.Position); handled = false; break;
			default: handled = false; break;
		}
		if (handled) GetViewport().SetInputAsHandled();
	}

	private bool _stickHeld;

	private void Hover(Vector2 p)
	{
		Rect2[] rects = Step == RewardStep.Cards ? _cardRects : _targetRects;
		for (int i = 0; i < rects.Length; i++)
			if (rects[i].HasPoint(p))
			{
				if (Step == RewardStep.Cards) CardCursor = i; else TargetCursor = i;
				QueueRedraw();
			}
	}

	private void Click(Vector2 p)
	{
		if (Step == RewardStep.Cards && _toggleRect.HasPoint(p)) { ToggleFrameData(); return; }
		Rect2[] rects = Step == RewardStep.Cards ? _cardRects : _targetRects;
		for (int i = 0; i < rects.Length; i++)
			if (rects[i].HasPoint(p))
			{
				if (Step == RewardStep.Cards) CardCursor = i; else TargetCursor = i;
				Confirm();
				return;
			}
	}

	private static Color TagColour(CardKind k) => k switch { CardKind.NewMove => FightHud.Persimmon, CardKind.Upgrade => FightHud.Pine, _ => Indigo };

	public override void _Draw()
	{
		if (!Showing || Offer == null) return;
		Vector2 s = Size;
		Font f = ThemeDB.FallbackFont;
		DrawRect(new Rect2(Vector2.Zero, s), new Color(FightHud.Ink, 0.94f));
		string title = Step == RewardStep.Cards ? $"{Offer.Yokai} yields three talismans" : $"Where does {Offer.Cards[CardCursor].Name} go?";
		DrawString(f, new Vector2(0, 120), title, HorizontalAlignment.Center, s.X, 64, FightHud.Paper);
		Paint.Brush(this, new Vector2(s.X / 2 - 360, 150), 720, 8, FightHud.Seal);
		if (Step == RewardStep.Cards) DrawCards(s, f); else DrawTargets(s, f);
	}

	private void DrawCards(Vector2 s, Font f)
	{
		int n = Offer!.Cards.Count;
		float w = 520f, gap = 60f, h = FrameDataVisible ? 700f : 640f, x0 = (s.X - (n * w + (n - 1) * gap)) / 2f, y = 215f;
		_cardRects = new Rect2[n];
		for (int i = 0; i < n; i++)
		{
			CardView c = Offer.Cards[i];
			bool sel = i == CardCursor;
			var r = new Rect2(x0 + i * (w + gap), sel ? y - 14 : y, w, h);
			_cardRects[i] = r;
			Paint.Talisman(this, r, sel ? FightHud.Paper.Lightened(0.08f) : FightHud.Paper.Darkened(0.06f), sel ? FightHud.Persimmon : null);
			if (sel) DrawRect(r.Grow(8), FightHud.Persimmon, false, 6f);
			// crest + name
			Paint.Seal(this, r.Position + new Vector2(80, 90), 44, c.Name[..1].ToUpperInvariant(), c.Kind == CardKind.Modifier ? Indigo : FightHud.Seal);
			DrawString(f, r.Position + new Vector2(140, 82), c.Name, HorizontalAlignment.Left, w - 160, 44, FightHud.Ink);
			DrawString(f, r.Position + new Vector2(140, 120), c.Source, HorizontalAlignment.Left, w - 160, 26, Grey.Darkened(0.3f));
			Paint.Brush(this, r.Position + new Vector2(30, 160), w - 60, 6, FightHud.Ink);
			// tag
			var tag = new Rect2(r.Position + new Vector2(30, 185), new Vector2(w - 60, 48));
			DrawRect(tag, TagColour(c.Kind));
			DrawString(f, tag.Position + new Vector2(0, 34), c.Tag, HorizontalAlignment.Center, tag.Size.X, 30, FightHud.Paper);
			// plain language first
			float ty = 290f;
			DrawMultilineString(f, r.Position + new Vector2(36, ty), c.Plain, HorizontalAlignment.Left, w - 72, 32, 5, FightHud.Ink);
			if (c.Kind == CardKind.Modifier)
				DrawMultilineString(f, r.Position + new Vector2(36, ty + 190), c.TargetPrompt ?? "Pick a special", HorizontalAlignment.Left, w - 72, 26, 2, Indigo);
			if (FrameDataVisible)
			{
				var box = new Rect2(r.Position + new Vector2(30, h - 190), new Vector2(w - 60, 150));
				DrawRect(box, new Color(FightHud.Ink, 0.1f));
				DrawRect(box, FightHud.Ink, false, 2f);
				DrawString(f, box.Position + new Vector2(12, 30), "FRAME DATA", HorizontalAlignment.Left, -1, 22, FightHud.Seal);
				DrawMultilineString(f, box.Position + new Vector2(12, 64), c.Frames, HorizontalAlignment.Left, box.Size.X - 24, 24, 3, FightHud.Ink);
			}
		}
		// toggle + hints
		_toggleRect = new Rect2(s.X / 2 - 220, 940, 440, 60);
		DrawRect(_toggleRect, FrameDataVisible ? FightHud.Seal : FightHud.Paper);
		DrawRect(_toggleRect, FightHud.Ink, false, 4f);
		DrawString(f, _toggleRect.Position + new Vector2(0, 42), FrameDataVisible ? "Frame data: ON  (Tab / Y)" : "Frame data: off  (Tab / Y)", HorizontalAlignment.Center, _toggleRect.Size.X, 28, FrameDataVisible ? FightHud.Paper : FightHud.Ink);
		DrawString(f, new Vector2(0, 1040), "Left / Right  choose      Enter / A  take", HorizontalAlignment.Center, s.X, 26, FightHud.Paper);
	}

	private void DrawTargets(Vector2 s, Font f)
	{
		var targets = Offer!.Targets;
		CardView card = Offer.Cards[CardCursor];
		float w = 400f, gap = 40f, h = 440f, x0 = (s.X - (targets.Count * w + (targets.Count - 1) * gap)) / 2f, y = 280f;
		_targetRects = new Rect2[targets.Count];
		DrawString(f, new Vector2(0, 215), $"{card.Name}: {card.Plain}", HorizontalAlignment.Center, s.X, 32, FightHud.Paper);
		for (int i = 0; i < targets.Count; i++)
		{
			TargetView t = targets[i];
			bool sel = i == TargetCursor;
			var r = new Rect2(x0 + i * (w + gap), sel ? y - 14 : y, w, h);
			_targetRects[i] = r;
			Color ink = t.Eligible ? FightHud.Ink : Grey;
			Paint.Talisman(this, r, t.Eligible ? FightHud.Paper : FightHud.Paper.Darkened(0.25f), sel ? FightHud.Persimmon : null);
			if (sel) DrawRect(r.Grow(8), FightHud.Persimmon, false, 6f);
			Paint.Seal(this, r.Position + new Vector2(w / 2, 90), 44, t.Slot, t.Eligible ? FightHud.Seal : Grey);
			DrawString(f, r.Position + new Vector2(0, 190), t.Name, HorizontalAlignment.Center, w, 36, ink);
			if (t.Level > 0) DrawString(f, r.Position + new Vector2(0, 232), $"Lv {t.Level}", HorizontalAlignment.Center, w, 28, ink);
			string note = t.Eligible ? (t.CurrentModifier != null ? $"Replaces {t.CurrentModifier}" : "No modifier yet") : (t.WhyNot ?? "Cannot carry it");
			DrawMultilineString(f, r.Position + new Vector2(24, 300), note, HorizontalAlignment.Center, w - 48, 26, 2, ink);
		}
		DrawString(f, new Vector2(0, 1040), "Left / Right  choose      Enter / A  attach      Esc / B  back", HorizontalAlignment.Center, s.X, 26, FightHud.Paper);
	}
}
