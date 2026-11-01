using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-218 (X1, X2, A12): one of Ryo's two cancel-rule places, on the pause run panel and under the move list. A held
/// rule shows its icon (<see cref="AbilityArt.ForId"/>: the generic diamond until a rule has art), name and plain line;
/// frame data, when asked for, swaps in for the plain line. An empty place is grey and says an elder may offer one.
/// Presentation only: <see cref="MoveListSource"/> and <c>PauseCancelSummary</c> hand it text from data/cancels.
/// </summary>
public partial class CancelRuleSlot : PanelContainer
{
	public static readonly Color Grey = new(0.45f, 0.45f, 0.45f);

	/// <summary>True after <see cref="BindEmpty"/>: nothing is held in this place.</summary>
	public bool IsEmpty { get; private set; } = true;
	public string RuleId { get; private set; } = "";
	public Label NameLabel => UiFind.Get<Label>(this, "Name");
	public Label BodyLabel => UiFind.Get<Label>(this, "Body");
	public TextureRect IconRect => UiFind.Get<TextureRect>(this, "Icon");

	/// <param name="frames">The path line from the rule's data (A12); null or empty = none, the plain line stays.</param>
	/// <param name="showFrames">Frame data toggle: shows <paramref name="frames"/> in place of <paramref name="plain"/>.</param>
	public void Bind(string id, string name, string plain, string? frames = null, bool showFrames = false)
	{
		IsEmpty = false; RuleId = id;
		var icon = IconRect;
		icon.Visible = true; icon.Texture = AbilityArt.ForId(id);
		bool swap = showFrames && !string.IsNullOrEmpty(frames);
		NameLabel.Text = name;
		BodyLabel.Text = swap ? frames! : plain;
		BodyLabel.ThemeTypeVariation = swap ? "PersimmonLabel" : "";
		foreach (var l in new[] { NameLabel, BodyLabel }) l.RemoveThemeColorOverride("font_color");
	}

	/// <summary>An open place (<paramref name="number"/> is 1-based): grey, no icon.</summary>
	public void BindEmpty(int number)
	{
		IsEmpty = true; RuleId = "";
		var icon = IconRect;
		icon.Visible = true; icon.Texture = null; // keeps the icon column, so the text lines up with a held rule
		NameLabel.Text = $"Cancel rule {number}: open";
		BodyLabel.Text = "An elder may offer one.";
		BodyLabel.ThemeTypeVariation = "";
		foreach (var l in new[] { NameLabel, BodyLabel }) l.AddThemeColorOverride("font_color", Grey);
	}

	/// <summary>Squeezes the card for the pause run panel (smaller margins and icon). Call before the first bind.</summary>
	public void Dense(float icon = 52f, float vMargin = 8f, float minHeight = 0f)
	{
		var sb = (StyleBoxTexture)GetThemeStylebox("panel").Duplicate();
		sb.ContentMarginTop = sb.ContentMarginBottom = vMargin;
		sb.ContentMarginLeft = sb.ContentMarginRight = 20f;
		AddThemeStyleboxOverride("panel", sb);
		IconRect.CustomMinimumSize = new Vector2(icon, icon);
		CustomMinimumSize = new Vector2(0, minHeight);
	}
}
