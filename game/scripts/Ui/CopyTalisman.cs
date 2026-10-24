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

	// Drawing constants: _Draw and DrawnBounds both use these, so the bounds cannot drift from what is drawn.
	/// <summary>Persimmon glow: rings at 4 and 8 px outside the rect, 4 px wide (a line reaches half its width past its edge).</summary>
	public const float GlowRingStep = 4f, GlowRingWidth = 4f;
	public const int GlowRings = 2;
	public const float GlowReach = GlowRings * GlowRingStep + GlowRingWidth / 2;
	/// <summary>The T1 seal stamp: a square of half-side <see cref="SealRadius"/> centred <see cref="SealInset"/> px inside the top-right corner.</summary>
	public const float SealRadius = 16f, SealInset = 6f;
	/// <summary>The name is never drawn under 26 px at 1080 (17 px at 720p): longer words break across lines, then end in an ellipsis.</summary>
	public const int NameSize = 26, NameSizeMin = NameSize, NameMaxLines = 2;
	private static readonly Vector2 ShadowOffset = new(6, 8);

	/// <summary>The seal in local coordinates (it pokes out of the top-right corner, clear of the name slot).</summary>
	public Rect2 SealRect => new(Size.X - SealInset - SealRadius, SealInset - SealRadius, SealRadius * 2, SealRadius * 2);

	/// <summary>Everything this control can draw (rect, glow, seal, shadow), in parent coordinates. The pulse is a brightness flash, so it never grows past this.</summary>
	public Rect2 DrawnBounds => new Rect2(Position, Size).Grow(Mathf.Max(GlowReach, SealRadius - SealInset)).Merge(new Rect2(Position + ShadowOffset, Size));

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
		_caption.AddThemeColorOverride("font_color", State == CopyTalismanState.None ? FightHud.Ink : FightHud.Seal); // red on the grey plate would be unreadable
		_name.Text = t is null ? "-" : t.Data.Name.ToUpperInvariant();
		_level.Text = t is null ? "" : $"Lv {t.Level}";
		var art = t is null ? null : AbilityArt.ForId(t.Data.Id);
		_icon.Texture = art;
		_icon.Visible = art != null;
		_letter.Visible = art == null && t != null;
		_letter.Text = t is null || t.Data.Name.Length == 0 ? "" : t.Data.Name[..Cuts(t.Data.Name)[1]].ToUpperInvariant();
		FitName();
		QueueRedraw();
	}

	/// <summary>
	/// YOK-162: the offsets the name may be cut at: 0, the start of every text element (grapheme cluster) and
	/// <c>s.Length</c>. A cut anywhere else hands Godot half a surrogate pair, and Godot 4.7.2 turns a lone surrogate
	/// from C# into a string with garbage code points ("Invalid unicode codepoint (ffffffff)", then
	/// U_ILLEGAL_ARGUMENT_ERROR in text_server_adv's ScriptIterator), which crashed the process at random. Cutting here
	/// also keeps a combining mark with its letter.
	/// </summary>
	public static int[] Cuts(string s)
	{
		var cuts = new System.Collections.Generic.List<int> { 0 };
		for (int i = 0; i < s.Length;) cuts.Add(i += System.Globalization.StringInfo.GetNextTextElementLength(s, i));
		return cuts.ToArray();
	}

	/// <summary>
	/// The name at <see cref="NameSize"/>, always. Spaces and hyphens wrap it (two lines). Only when a word is wider than
	/// the slot, or the name needs more than two lines, is the text rewritten: the word breaks with a hyphen, and a name
	/// still too long ends in an ellipsis. Names that fit keep their text unchanged.
	/// </summary>
	private void FitName()
	{
		_name.AddThemeFontSizeOverride("font_size", NameSize);
		var font = _name.GetThemeFont("font");
		float width = _name.Size.X;
		float W(string t) => font.GetStringSize(t, fontSize: NameSize).X;
		string text = _name.Text;
		var tokens = text.Replace("-", "- ").Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
		var lines = new System.Collections.Generic.List<string>();
		string line = "";
		bool rewrite = false;
		void Flush() { if (line.Length > 0) lines.Add(line); line = ""; }
		foreach (string tok in tokens)
		{
			string t = tok;
			string joined = line.Length == 0 ? t : (line.EndsWith('-') ? line + t : line + " " + t);
			if (W(joined) <= width) { line = joined; continue; }
			Flush();
			int[] cuts;
			while (width > 0 && (cuts = Cuts(t)).Length > 2 && W(t) > width) // a single word wider than the slot: break it with a hyphen
			{
				rewrite = true;
				int k = cuts.Length - 2; // text elements kept on this line: as many as fit, at least two, unless the word has only two
				while (k > 2 && W(t[..cuts[k]] + "-") > width) k--;
				lines.Add(t[..cuts[k]] + "-");
				t = t[cuts[k]..];
			}
			line = t;
		}
		Flush();
		if (lines.Count > NameMaxLines)
		{
			rewrite = true;
			string last = lines[NameMaxLines - 1].TrimEnd('-');
			int[] lastCuts;
			while ((lastCuts = Cuts(last)).Length > 2 && W(last + "…") > width) last = last[..lastCuts[^2]];
			lines = lines.GetRange(0, NameMaxLines - 1);
			lines.Add(last + "…");
		}
		if (rewrite) _name.Text = string.Join("\n", lines);
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
			for (int i = GlowRings; i >= 1; i--) DrawRect(r.Grow(i * GlowRingStep), new Color(FightHud.Persimmon, 0.2f), false, GlowRingWidth);
		DrawRect(new Rect2(r.Position + ShadowOffset, r.Size), new Color(0, 0, 0, 0.35f));
		DrawRect(r, none ? new Color(0.6f, 0.58f, 0.5f) : FightHud.Paper);
		DrawRect(r, none ? new Color(0.35f, 0.34f, 0.3f) : FightHud.Ink, false, 5f);
		DrawRect(r.Grow(-8), FightHud.Seal, false, 2f);
		if (!none) DrawRect(r.Grow(2), FightHud.Persimmon, false, 4f);
		if (State == CopyTalismanState.Locked) Paint.Seal(this, SealRect.GetCenter(), SealRadius, "S"); // T1 red seal stamp
	}
}
