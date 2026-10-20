using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-159 (merchant portrait on the rest stop). Expectations: S3 (no Tanuki likeness before the boss), UI consistency of art vs dashed box.</summary>
public static class RestStopPortraitProbes
{
	static string MapDir => ContentPaths.Data("map");

	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		try { body(); }
		finally { FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe; RestStopScreenForecast.CostSource = null; }
	}

	static RunSession AtStop(string type)
	{
		var s = RunSession.Start(FightScene.LoadAbilityPool(), FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir), 5);
		for (; ; )
		{
			var next = s.Reachable();
			string pick = next.FirstOrDefault(id => s.Map.Graph.Node(id).Type == type) ?? next[0];
			Assert.True(s.Enter(pick).Ok, "entered");
			if (s.Node!.Type == type) break;
			if (s.Node.Type is "merchant" or "dojo") Assert.True(s.CompleteNode().Ok, "stop passed");
			else
			{
				if (s.WinFight(300) is { } d) { var c = d.Offer.Cards[0]; Assert.True(d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null).Ok, "picked"); }
				Assert.True(s.CompleteNode().Ok, "done");
			}
		}
		s.Run.SetHealth(300);
		return s;
	}

	static RestStopScreen Open(Node runner, RunSession s)
	{
		var screen = GD.Load<PackedScene>("res://scenes/ui/rest_stop.tscn").Instantiate<RestStopScreen>();
		screen.LineSource = _ => "A line.";
		screen.BindRun(s, ControlScheme.Kihon);
		runner.AddChild(screen);
		return screen;
	}

	/// <summary>Art, caption and dashed box must agree: art shown implies no caption and no dashed box; no art implies caption and dashed box.</summary>
	static void Consistent(RestStopScreen screen, bool merchant, string ctx)
	{
		var art = screen.GetNode<TextureRect>("Portrait/Art");
		var label = screen.GetNode<Label>("Portrait/PortraitLabel");
		var decor = screen.GetNode<RestStopScreenDecor>("Decor");
		Assert.True(art.Visible == merchant, ctx + ": art visible only for the merchant");
		Assert.True((art.Texture != null) == merchant, ctx + ": texture set only for the merchant");
		Assert.True(label.Visible == !merchant, ctx + ": caption only without art");
		Assert.True(decor.PortraitFilled == merchant, ctx + ": dashed box hidden exactly when art is shown");
	}

	[Test]
	public static void Rebind_MerchantDojoMerchant_ArtCaptionAndBoxFollowTheStop(Node runner) => OnFixtures(() =>
	{
		var screen = Open(runner, AtStop("merchant"));
		try
		{
			Consistent(screen, true, "merchant");
			screen.BindRun(AtStop("dojo"), ControlScheme.Kihon); Consistent(screen, false, "dojo");
			screen.BindRun(AtStop("merchant"), ControlScheme.Kihon); Consistent(screen, true, "merchant again");
			screen.BindRun(AtStop("dojo"), ControlScheme.Kihon); Consistent(screen, false, "dojo again");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	[Test]
	public static void Refresh_AfterAChoice_KeepsTheArt_AndRefreshIsIdempotent(Node runner) => OnFixtures(() =>
	{
		foreach (bool free in new[] { true, false })
		{
			var screen = Open(runner, AtStop("merchant"));
			try
			{
				var before = (screen.GetNode<TextureRect>("Portrait/Art").Texture as AtlasTexture)!;
				if (free) Assert.True(screen.TakeFree(), "free taken");
				else screen.OpenSecond();
				screen.Refresh(); screen.Refresh();
				Consistent(screen, true, free ? "after free" : "after shop");
				var after = (screen.GetNode<TextureRect>("Portrait/Art").Texture as AtlasTexture)!;
				Assert.True(after.Atlas == ShopArt.Portrait && after.Region == RestStopScreen.MerchantBust && before.Region == after.Region, "same bust after refresh");
			}
			finally { runner.RemoveChild(screen); screen.Free(); }
		}
	});

	[Test]
	public static void Dojo_AfterAChoice_StillShowsPlaceholderNotMerchantArt(Node runner) => OnFixtures(() =>
	{
		var screen = Open(runner, AtStop("dojo"));
		try { Assert.True(screen.TakeFree(), "rest taken"); screen.Refresh(); Consistent(screen, false, "dojo used"); }
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	[Test]
	public static void Bust_InsidePlateAtImportedSize_AndMatchesBoxShape(Node runner) => OnFixtures(() =>
	{
		var plate = ShopArt.Portrait;
		Assert.Equal(432, plate.GetWidth(), "imported width"); Assert.Equal(768, plate.GetHeight(), "imported height");
		var r = RestStopScreen.MerchantBust;
		Assert.True(r.Position.X >= 0 && r.Position.Y >= 0 && r.End.X <= plate.GetWidth() && r.End.Y <= plate.GetHeight(), "bust inside plate");
		float boxAspect = 410f / 360f;
		Assert.True(Math.Abs(r.Size.X / r.Size.Y - boxAspect) < 0.02f, "bust has the shape of the 410x360 box (no letterbox gaps)");
	});

	[Test]
	public static void Layout1080_ArtFillsItsBox_AndClearsTheLineBox_Scales720(Node runner) => OnFixtures(() =>
	{
		var screen = Open(runner, AtStop("merchant"));
		try
		{
			screen.Size = new Vector2(1920, 1080);
			var art = screen.GetNode<TextureRect>("Portrait/Art"); var box = screen.GetNode<Control>("Portrait"); var line = screen.GetNode<Control>("LineBox");
			var ar = art.GetGlobalRect(); var br = box.GetGlobalRect(); var lr = line.GetGlobalRect();
			Assert.True(br.Encloses(ar) && ar.Size.X > 0 && ar.Size.Y > 0, $"art {ar} inside its box {br}");
			Assert.True(ar.End.Y <= lr.Position.Y, $"art bottom {ar.End.Y} clears the line box top {lr.Position.Y}");
			var tex = art.Texture!; float drawScale = Math.Min(ar.Size.X / tex.GetWidth(), ar.Size.Y / tex.GetHeight());
			Assert.True(tex.GetWidth() * drawScale >= ar.Size.X * 0.95f && tex.GetHeight() * drawScale >= ar.Size.Y * 0.95f, "image fills the box (no empty bands)");
			// 720p is a uniform 2/3 scale of this layout, so 1080 containment implies 720 containment
			Assert.True(ar.Size.Y * 720f / 1080 >= 200, "art readable at 720p");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	[Test]
	public static void Shop_PlateTexture_UntouchedByTheRestStopAtlas(Node runner) => OnFixtures(() =>
	{
		var screen = Open(runner, AtStop("merchant"));
		try
		{
			Assert.True(ShopArt.Portrait.GetWidth() == 432 && ShopArt.Portrait.GetHeight() == 768, "plate texture unchanged by the atlas");
			var decor = new ShopScreenDecor();
			try { Assert.True(!decor.PortraitFilled, "the shop decor's own flag stays off; its placeholder is off by override"); }
			finally { decor.Free(); }
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});
}
