using Godot;

namespace YokaiFighters.Ui.Kit;

public partial class KitGallery
{
	private static Texture2D Plate(string name) => GD.Load<Texture2D>($"res://assets/generated/ui/{name}.png");
	private static TextureRect Art(Control parent, string name, Vector2 pos, Vector2 size)
	{
		var art = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Name = name, Texture = Plate(name), Position = pos, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore };
		parent.AddChild(art); art.Size = size; return art;
	}
	private static Label Copy(Control parent, string text, string variation, Vector2 pos, Vector2 size)
	{
		var label = Text(text, variation); label.Position = pos; label.Size = size; parent.AddChild(label); return label;
	}
	private static KeyChipFooter Footer(Control parent, Vector2 pos, params KeyHint[] hints)
	{
		var footer = Scene("key_chip_footer").Instantiate<KeyChipFooter>();
		footer.Position = pos; footer.SetHints(hints); parent.AddChild(footer); return footer;
	}
	private Control BuildTitleProof()
	{
		var page = new Control { Name = "TitleProof" };
		var backdrop = Scene("kit_backdrop").Instantiate<KitBackdrop>();
		backdrop.Art = Plate("title-shrine"); page.AddChild(backdrop);
		Art(page, "title-wordmark", new Vector2(45, 32), new Vector2(770, 425));
		var menu = new VBoxContainer { Position = new Vector2(95, 450), Size = new Vector2(500, 390) };
		menu.AddThemeConstantOverride("separation", 0); page.AddChild(menu);
		foreach (var text in new[] { "Start", "Continue", "Practice", "Settings", "Quit" })
		{
			var button = Btn("menu_row", text, text == "Start" ? KitState.Focused : KitState.Normal);
			button.CustomMinimumSize = new Vector2(500, 78); menu.AddChild(button);
		}
		Art(page, "ink-underline", new Vector2(118, 926), new Vector2(290, 10));
		Copy(page, "Choose your path.", "BodyLabel", new Vector2(118, 942), new Vector2(410, 50));
		Footer(page, new Vector2(1510, 980), new KeyHint("confirm", "Confirm"));
		return page;
	}
	private Control BuildRewardProof()
	{
		var page = new Control { Name = "RewardProof" };
		var sheet = Sheet(new Vector2(174, 150), new Vector2(1550, 800)); sheet.Ornaments = false; page.AddChild(sheet);
		var heading = Scene("kit_heading").Instantiate<KitHeading>(); heading.Text = "Reward"; heading.UnderlineWidth = 1390;
		heading.Position = new Vector2(220, 158); page.AddChild(heading);
		Copy(page, "Kitsune yields three talismans", "BodyLabel", new Vector2(835, 245), new Vector2(720, 50));
		string[] names = { "Foxfire", "Spirit Wave", "Will-o’-wisp" };
		string[] sources = { "Kitsune special", "Your special", "Kitsune modifier" };
		string[] tags = { "NEW · Lv 1", "UPGRADE · Lv 1 to 2", "MODIFIER" };
		string[] art = { "reward-foxfire", "reward-spirit-wave", "reward-will-o-wisp" };
		string[] body = { "A slow, drifting flame.\nIt leaves fading copies\nbehind it.", "Your wave hits harder\nand recovers faster.", "Your projectiles fly\n30% faster.\nPick the special to carry it." };
		for (int i = 0; i < 3; i++)
		{
			var card = Scene("kit_card").Instantiate<KitCard>();
			card.Position = new Vector2(284 + i * 449, 345); card.Size = new Vector2(420, 535);
			card.Preview = i == 0 ? KitState.Focused : KitState.Normal;
			var box = card.GetNode<VBoxContainer>("Box");
			card.GetNode<Label>("Box/Title").Text = names[i];
			card.GetNode<Label>("Box/Title").AddThemeFontSizeOverride("font_size", 42);
			var description = card.GetNode<Label>("Box/Body"); description.Text = body[i]; description.AddThemeFontSizeOverride("font_size", 30);
			var source = Text(sources[i], "CaptionLabel"); box.AddChild(source); box.MoveChild(source, 1);
			var tag = Btn("red_button", tags[i], KitState.Normal); tag.CustomMinimumSize = new Vector2(0, 48);
			tag.AddThemeFontSizeOverride("font_size", 26); tag.FocusMode = FocusModeEnum.None; tag.MouseFilter = MouseFilterEnum.Ignore;
			box.AddChild(tag); box.MoveChild(tag, 2);
			var illustration = new TextureRect { Texture = Plate(art[i]), CustomMinimumSize = new Vector2(0, 208), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
			box.AddChild(illustration); box.MoveChild(illustration, 3);
			page.AddChild(card);
		}
		Art(page, "fox-mask", new Vector2(1530, 158), new Vector2(270, 250));
		Footer(page, new Vector2(290, 975), new KeyHint("frames", "Frame data: off"));
		Footer(page, new Vector2(1210, 975), new KeyHint("choose", "Choose"), new KeyHint("confirm", "Take"));
		return page;
	}
	private Control BuildPlatesPage()
	{
		var page = new Control { Name = "Plates" };
		var left = Sheet(new Vector2(50, 60), new Vector2(800, 240)); left.Ornaments = false; page.AddChild(left);
		Art(page, "ryo-portrait", new Vector2(40, 40), new Vector2(265, 240));
		Copy(page, "RYO · 720 / 1000", "BodyLabel", new Vector2(305, 86), new Vector2(470, 48));
		var hp = new ProgressBar { Position = new Vector2(310, 146), Size = new Vector2(470, 32), Value = 72, ShowPercentage = false, ThemeTypeVariation = "HealthBar" }; page.AddChild(hp);
		for (int i = 0; i < 3; i++) page.AddChild(new ProgressBar { Position = new Vector2(310 + 128 * i, 194), Size = new Vector2(116, 24), Value = i == 0 ? 100 : i == 1 ? 40 : 0, ShowPercentage = false, ThemeTypeVariation = "MeterBar" });
		Art(page, "burst-blank", new Vector2(715, 198), new Vector2(72, 72));
		var burst = Copy(page, "B", "FooterLabel", new Vector2(715, 211), new Vector2(72, 40)); burst.HorizontalAlignment = HorizontalAlignment.Center;
		var right = Sheet(new Vector2(1060, 60), new Vector2(800, 200)); right.Ornaments = false; page.AddChild(right);
		Copy(page, "KITSUNE · 600 / 1000", "BodyLabel", new Vector2(1120, 90), new Vector2(510, 48));
		page.AddChild(new ProgressBar { Position = new Vector2(1120, 148), Size = new Vector2(520, 32), Value = 60, ShowPercentage = false, ThemeTypeVariation = "HealthBarEnemy" });
		Art(page, "fox-mask", new Vector2(1650, 30), new Vector2(215, 240));
		var samples = Sheet(new Vector2(60, 410), new Vector2(1800, 540)); samples.Ornaments = false; page.AddChild(samples);
		Copy(page, "Shared plates", "SubheadingLabel", new Vector2(120, 445), new Vector2(800, 70));
		Art(page, "rice-sheet", new Vector2(120, 550), new Vector2(350, 240));
		Art(page, "rice-card", new Vector2(510, 545), new Vector2(150, 255));
		Art(page, "selection-frame", new Vector2(720, 545), new Vector2(150, 255));
		Art(page, "red-brush", new Vector2(970, 565), new Vector2(700, 110));
		Art(page, "ink-underline", new Vector2(970, 695), new Vector2(700, 24));
		Art(page, "blossom-corner", new Vector2(1650, 690), new Vector2(150, 150));
		Copy(page, "Nine-slice paper / card / selection     ·     Alpha brush / underline / corner", "CaptionLabel", new Vector2(130, 855), new Vector2(1600, 48));
		return page;
	}
}
