using System;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Ui;

/// <summary>One special slot a modifier card can attach to (<c>scenes/ui/target_card.tscn</c>), filled from a <see cref="TargetView"/>.</summary>
public partial class TargetCard : Control
{
	public static readonly Color Grey = new(0.45f, 0.45f, 0.45f);
	[Export] public float RaiseWhenSelected { get; set; } = 14f;

	public event Action<TargetCard>? Hovered, Clicked;

	private TalismanPanel _body = null!;
	private SealStamp _crest = null!;
	private Label _name = null!, _level = null!, _note = null!;
	private bool _eligible = true;

	public override void _Ready()
	{
		_body = UiFind.Get<TalismanPanel>(this, "Body");
		_crest = UiFind.Get<SealStamp>(this, "Crest");
		_name = UiFind.Get<Label>(this, "Name");
		_level = UiFind.Get<Label>(this, "Level");
		_note = UiFind.Get<Label>(this, "Note");
		MouseEntered += () => Hovered?.Invoke(this);
		GuiInput += e =>
		{
			if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { AcceptEvent(); Clicked?.Invoke(this); }
		};
		SetSelected(false);
	}

	public void Bind(TargetView t)
	{
		_eligible = t.Eligible;
		_crest.Glyph = t.Slot;
		_crest.Fill = t.Eligible ? FightHud.Seal : Grey;
		_name.Text = t.Name;
		_level.Visible = t.Level > 0;
		_level.Text = $"Lv {t.Level}";
		_note.Text = t.Eligible ? (t.CurrentModifier != null ? $"Replaces {t.CurrentModifier}" : "No modifier yet") : (t.WhyNot ?? "Cannot carry it");
		foreach (Label l in new[] { _name, _level, _note })
		{
			if (t.Eligible) l.RemoveThemeColorOverride("font_color"); else l.AddThemeColorOverride("font_color", Grey);
		}
		SetSelected(false);
	}

	public void SetSelected(bool selected)
	{
		_body.Position = new Vector2(0f, selected ? 0f : RaiseWhenSelected);
		_body.Paper = _eligible ? FightHud.Paper : FightHud.Paper.Darkened(0.25f);
		_body.Border = selected ? FightHud.Persimmon : FightHud.Ink;
		_body.Highlight = selected;
	}
}
