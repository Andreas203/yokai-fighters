using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

/// <summary>What the talisman shows (YOK-74 mockup states A, B, E).</summary>
public enum CopyTalismanState { None, Defined, Locked }

/// <summary>
/// YOK-74, T6: the copy-target talisman in the HUD's top-centre gap. <see cref="Apply"/> takes the target and the
/// lock flag from <see cref="IHudRunView"/>; this control only shows them, pulses when the same special gains a level
/// and slides in a toast when another special overtakes it. It sits on the HUD layer, so camera shake and hitstop never move it.
/// Children (<c>Caption</c>, <c>Icon</c>, <c>IconLetter</c>, <c>MoveName</c>, <c>Level</c>, <c>Toast</c>) are in the scene.
/// </summary>
public partial class CopyTalisman : Control
{
	private Label _caption = null!, _letter = null!, _name = null!, _level = null!, _toast = null!;
	private TextureRect _icon = null!;
	private CopyTarget? _shown;
	private bool _first = true;
	private Tween? _pulse, _toastTween;

	/// <summary>Glow rings, seal and shadow stay inside this reach (px) of the control rect; the HUD strip ends at y=138.</summary>
	public const float GlowReach = 10f;
	public const int NameSize = 26, NameSizeMin = 20;
	private static readonly Vector2 ShadowOffset = new(6, 8);

	/// <summary>Everything this control can draw (rect, glow, seal, shadow), in parent coordinates. The pulse is a brightness flash, so it never grows past this.</summary>
	public Rect2 DrawnBounds => new Rect2(Position, Size).Grow(GlowReach).Merge(new Rect2(Position + ShadowOffset, Size));

	/// <summary>Forget what was shown (a new run or session): the next <see cref="Apply"/> is a first show, with no pulse or toast.</summary>
	public void Reset()
	{
		_pulse?.Kill(); _toastTween?.Kill();
		Modulate = Colors.White;
		_shown = null; _first = true; LastEvent = "";
		if (_toast != null) { _toast.Text = ""; _toast.Modulate = Colors.Transparent; }
	}

	public CopyTalismanState State { get; private set; } = CopyTalismanState.None;
	/// <summary>"" / "levelled" / "changed": what the last <see cref="Apply"/> animated.</summary>
	public string LastEvent { get; private set; } = "";
	public string ToastText => _toast?.Text ?? "";
	public string MoveName => _name?.Text ?? "";
	public string LevelText => _level?.Text ?? "";
	public string CaptionText => _caption?.Text ?? "";

	public override void _Ready()
	{
		_caption = UiFind.Get<Label>(this, "Caption");
		_icon = UiFind.Get<TextureRect>(this, "Icon");
		_letter = UiFind.Get<Label>(this, "IconLetter");
		_name = UiFind.Get<Label>(this, "MoveName");
		_level = UiFind.Get<Label>(this, "Level");
		_toast = UiFind.Get<Label>(GetParent(), "Toast"); // a HUD sibling: it sits in the bottom band, clear of the jump envelope
		_toast.Modulate = Colors.Transparent;
		Redisplay(null, false);
	}

	public void Apply(CopyTarget? target, bool locked)
	{
		LastEvent = "";
		bool sameMove = _shown is not null && target is not null && _shown.Data.Id == target.Data.Id;
		if (!_first && !locked && target is not null && _shown is not null)
		{
			if (sameMove && target.Level > _shown.Level) { LastEvent = "levelled"; ClearToast(); Pulse(); }
			else if (!sameMove) { LastEvent = "changed"; Announce($"Copy target: {target.Data.Name}, Lv {target.Level}"); }
		}
		if (locked) ClearToast();
		if (_first || State != StateFor(target, locked) || _shown?.Data.Id != target?.Data.Id || _shown?.Level != target?.Level) Redisplay(target, locked);
		_first = false;
	}

	private static CopyTalismanState StateFor(CopyTarget? t, bool locked) => t is null ? CopyTalismanState.None : locked ? CopyTalismanState.Locked : CopyTalismanState.Defined;

	private void Redisplay(CopyTarget? t, bool locked)
	{
		_shown = t;
		State = StateFor(t, locked);
		_caption.Text = State == CopyTalismanState.Locked ? "LOCKED" : "COPY TARGET";
		_name.Text = t is null ? "-" : t.Data.Name.ToUpperInvariant();
		_level.Text = t is null ? "" : $"Lv {t.Level}";
		var art = t is null ? null : RewardArt.ForId(t.Data.Id);
		_icon.Texture = art;
		_icon.Visible = art != null;
		_letter.Visible = art == null && t != null;
		_letter.Text = t is null || t.Data.Name.Length == 0 ? "" : t.Data.Name[..1].ToUpperInvariant();
		FitName();
		QueueRedraw();
	}

	/// <summary>
	/// Names wrap at spaces and hyphens, two lines at most, at <see cref="NameSize"/> (the 720p floor). Only if a single
	/// word is still wider than the slot does it shrink, never below <see cref="NameSizeMin"/>.
	/// </summary>
	private void FitName()
	{
		var font = _name.GetThemeFont("font");
		int size = NameSize;
		foreach (string w in _name.Text.Replace("-", "- ").Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
			while (size > NameSizeMin && font.GetStringSize(w.TrimEnd(), fontSize: size).X > _name.Size.X) size--;
		_name.AddThemeFontSizeOverride("font_size", size);
	}

	private void ClearToast()
	{
		_toastTween?.Kill();
		_toast.Text = "";
		_toast.Modulate = Colors.Transparent;
	}

	private void Pulse()
	{
		_pulse?.Kill();
		Modulate = new Color(1.6f, 1.45f, 1.2f);
		_pulse = CreateTween();
		_pulse.TweenProperty(this, "modulate", Colors.White, 0.4);
	}

	private void Announce(string text)
	{
		_toast.Text = text;
		_toastTween?.Kill();
		_toast.Modulate = Colors.White;
		_toastTween = CreateTween();
		_toastTween.TweenInterval(2.2);
		_toastTween.TweenProperty(_toast, "modulate:a", 0f, 0.5);
	}

	public override void _Draw()
	{
		var r = new Rect2(Vector2.Zero, Size);
		bool none = State == CopyTalismanState.None;
		if (!none) // persimmon glow, steady (T6)
			for (int i = 2; i >= 1; i--) DrawRect(r.Grow(i * 4), new Color(FightHud.Persimmon, 0.2f), false, 4f); // reaches GlowReach
		DrawRect(new Rect2(r.Position + ShadowOffset, r.Size), new Color(0, 0, 0, 0.35f));
		DrawRect(r, none ? new Color(0.6f, 0.58f, 0.5f) : FightHud.Paper);
		DrawRect(r, none ? new Color(0.35f, 0.34f, 0.3f) : FightHud.Ink, false, 5f);
		DrawRect(r.Grow(-8), FightHud.Seal, false, 2f);
		if (!none) DrawRect(r.Grow(2), FightHud.Persimmon, false, 4f);
		if (State == CopyTalismanState.Locked) Paint.Seal(this, new Vector2(Size.X - 6, Size.Y - 6), 16, "S"); // T1 red seal stamp
	}
}
