using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Ui;

/// <summary>One special slot on the move list (<c>scenes/ui/move_slot_card.tscn</c>): slot seal, name, level pips, input, plain line, frame line.</summary>
public partial class MoveSlotCard : Control
{
	public static readonly Color Grey = new(0.45f, 0.45f, 0.45f);

	public void Bind(SlotRow row, bool kata, bool frames)
	{
		var crest = UiFind.Get<SealStamp>(this, "Crest");
		var name = UiFind.Get<Label>(this, "Name");
		var pips = UiFind.Get<LevelPips>(this, "Pips");
		var input = UiFind.Get<Label>(this, "Input");
		var plain = UiFind.Get<Label>(this, "Plain");
		var fr = UiFind.Get<Label>(this, "Frames");
		crest.Glyph = row.Slot;
		crest.Fill = row.Empty ? Grey : FightHud.Seal;
		name.Text = row.Empty ? $"Slot {row.Slot}: empty" : row.Name;
		pips.Visible = !row.Empty;
		pips.Level = row.Level; pips.Max = row.MaxLevel;
		input.Text = (kata ? "Kata: " : "Kihon: ") + (kata ? row.KataInput : row.KihonInput);
		plain.Text = row.Plain;
		fr.Text = row.Frames;
		fr.Visible = frames && !row.Empty;
		// Frame data swaps in for the description (same space), so a card never outgrows its box: numbers need up to four lines.
		bool swap = frames && !row.Empty;
		plain.Visible = !swap;
		fr.OffsetTop = swap ? 68 : 124; fr.OffsetBottom = swap ? 172 : 174;
		// The equipped modifier (A2): name and plain effect under the description; with frame data on, its line moves into the frame text.
		var mod = UiFind.Get<Label>(this, "Mod");
		mod.Text = row.ModifierName.Length > 0 ? $"Modifier, {row.ModifierName}: {row.ModifierPlain}" : "";
		mod.Visible = !frames && mod.Text.Length > 0;
		foreach (Label l in new[] { name, plain }) { if (row.Empty) l.AddThemeColorOverride("font_color", Grey); else l.RemoveThemeColorOverride("font_color"); }
	}
}
