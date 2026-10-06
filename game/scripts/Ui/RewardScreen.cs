using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Ui;

public enum RewardStep { Cards, Target }

/// <summary>
/// Reward screen (GDD fig. 5, A7, A12): three talisman cards from the beaten yokai, plain language first,
/// frame data behind a toggle (Tab / pad Y / click the toggle). A modifier card opens a second step to pick
/// the special that carries it. Layout lives in <c>scenes/ui/reward_screen.tscn</c> (cards are instances of
/// <c>reward_card.tscn</c> / <c>target_card.tscn</c>); this script only binds <see cref="IRewardSource"/> data and
/// handles input. No game rules here.
/// Input: Left/Right (arrows only) + d-pad/stick to move, Enter / pad A to pick, Esc / pad B to go back,
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
	private Label _title = null!, _targetHeading = null!;
	private Control _cardsBox = null!, _targetsBox = null!, _hintCards = null!, _hintTarget = null!;
	private Button _toggle = null!;
	private readonly List<RewardCard> _cards = new();
	private readonly List<TargetCard> _targets = new();
	private bool _stickHeld;

	public override void _Ready()
	{
		_title = UiFind.Get<Label>(this, "Title");
		_targetHeading = UiFind.Get<Label>(this, "TargetHeading");
		_cardsBox = UiFind.Get<Control>(this, "Cards");
		_targetsBox = UiFind.Get<Control>(this, "Targets");
		_hintCards = UiFind.Get<Control>(this, "HintCards");
		_hintTarget = UiFind.Get<Control>(this, "HintTarget");
		_toggle = UiFind.Get<Button>(this, "FrameToggle");
		_toggle.Pressed += ToggleFrameData;
		foreach (Node n in _cardsBox.GetChildren())
			if (n is RewardCard c) { _cards.Add(c); c.Hovered += OnCardHover; c.Clicked += OnCardClick; }
		foreach (Node n in _targetsBox.GetChildren())
			if (n is TargetCard t) { _targets.Add(t); t.Hovered += OnTargetHover; t.Clicked += OnTargetClick; }
		Visible = Showing;
	}

	public void Present(IRewardSource source)
	{
		_source = source; Step = RewardStep.Cards; CardCursor = 0; TargetCursor = 0; FrameDataVisible = false;
		Showing = true; Visible = true;
		for (int i = 0; i < _cards.Count && i < source.Offer.Cards.Count; i++) _cards[i].Bind(source.Offer.Cards[i]);
		for (int i = 0; i < _targets.Count && i < source.Offer.Targets.Count; i++) _targets[i].Bind(source.Offer.Targets[i]);
		Refresh();
	}

	/// <summary>YOK-48: hide without picking (the flow restarted underneath it).</summary>
	public void Dismiss() { Showing = false; Visible = false; _source = null; }

	private RewardOffer? Offer => _source?.Offer;

	public void Move(int dir)
	{
		if (!Showing || Offer == null) return;
		if (Step == RewardStep.Cards) CardCursor = Mod(CardCursor + dir, Offer.Cards.Count);
		else TargetCursor = Mod(TargetCursor + dir, Offer.Targets.Count);
		Refresh();
	}

	public void ToggleFrameData() { if (Showing && Step == RewardStep.Cards) { FrameDataVisible = !FrameDataVisible; Refresh(); } }

	public void Back() { if (Showing && Step == RewardStep.Target) { Step = RewardStep.Cards; Refresh(); } }

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
				Refresh();
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
			default: handled = false; break;
		}
		if (handled) GetViewport().SetInputAsHandled();
	}

	// Mouse: the cards are Controls, so hover and click arrive as their signals.
	private void OnCardHover(RewardCard c) { if (Showing && Step == RewardStep.Cards) { CardCursor = _cards.IndexOf(c); Refresh(); } }
	private void OnCardClick(RewardCard c) { if (Showing && Step == RewardStep.Cards) { CardCursor = _cards.IndexOf(c); Confirm(); } }
	private void OnTargetHover(TargetCard t) { if (Showing && Step == RewardStep.Target) { TargetCursor = _targets.IndexOf(t); Refresh(); } }
	private void OnTargetClick(TargetCard t) { if (Showing && Step == RewardStep.Target) { TargetCursor = _targets.IndexOf(t); Confirm(); } }

	/// <summary>Pushes the cursor, step and toggle state into the scene's nodes.</summary>
	private void Refresh()
	{
		if (!Showing || Offer == null || _title == null) return;
		bool cards = Step == RewardStep.Cards;
		_title.Text = cards ? $"{Offer.Yokai} yields three talismans" : $"Where does {Offer.Cards[CardCursor].Name} go?";
		_cardsBox.Visible = cards; _hintCards.Visible = cards; _toggle.Visible = cards;
		_targetsBox.Visible = !cards; _hintTarget.Visible = !cards; _targetHeading.Visible = !cards;
		for (int i = 0; i < _cards.Count; i++)
		{
			_cards[i].Visible = i < Offer.Cards.Count;
			_cards[i].SetState(i == CardCursor, FrameDataVisible);
		}
		if (!cards)
		{
			CardView card = Offer.Cards[CardCursor];
			_targetHeading.Text = $"{card.Name}: {card.Plain}";
			for (int i = 0; i < _targets.Count; i++)
			{
				_targets[i].Visible = i < Offer.Targets.Count;
				_targets[i].SetSelected(i == TargetCursor);
			}
		}
		_toggle.Text = FrameDataVisible ? "Frame data: ON  (Tab / Y)" : "Frame data: off  (Tab / Y)";
		_toggle.ThemeTypeVariation = FrameDataVisible ? "ToggleButtonOn" : "ToggleButton";
	}
}
