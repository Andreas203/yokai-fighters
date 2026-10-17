using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-26: the whole-run bot at the merchant (R6, YOK-95), on the shop fixtures of <see cref="MerchantShopTests"/>
/// (same class, so the cloned pool and the row-4 sessions are shared). The bot spends only through
/// <see cref="MerchantShop"/>: every purchase is one the shop accepted.
/// </summary>
public static partial class MerchantShopTests
{
	static RunBot BotWith(MerchantPlan plan, int healBelowPct = 100) =>
		new(BotConfig.LoadDefault(ContentPaths.DataDir).Run with { Merchant = plan, HealBelowPct = healBelowPct }, 1);

	[Test]
	public static void Bot_Shop_BuysShelfSpecialsIntoFreeSlots_WhileCoinsLast() => OnFixtures(() =>
	{
		var pool = ShopPool();
		var s = At(pool, 120, true, Starters);
		var shelf = MerchantShop.ShelfFor(s.Run, pool, s.BoundYokai()).Select(x => x.Id).ToList();
		var bought = new List<string>();
		Assert.Equal("shop", BotWith(MerchantPlan.Shop).AtMerchant(s, bought), "R6: the stop's choice went to the shop");
		Assert.Equal(MerchantChoice.Shop, s.Merchant!.Choice, "the heal is closed");
		Assert.Equal(2, bought.Count, "120 coins buy two commons at 50 (C and D are free)");
		Assert.True(bought.All(shelf.Contains), $"only shelf items bought ({string.Join(", ", bought)})");
		Assert.Equal(20, s.Run.Coins, "R6: 50 each");
		foreach (string id in bought)
		{
			var held = s.Run.Owned.Single(o => o.Special.Data.Id == id).Special;
			Assert.True(held.Borrowed && held.Level == 1, $"{id}: Lv 1, borrowed ink");
		}
		Assert.Equal(600, s.Run.Health, "shopping heals nothing");
	});

	[Test]
	public static void Bot_Shop_NoFreeSlot_BuysAModifierOnTheLowestSlotThatTakesIt() => OnFixtures(() =>
	{
		var pool = ShopPool();
		var s = At(pool, 60, true, StartersAnd((SpecialSlot.C, "test-kappa-grab", 1), (SpecialSlot.D, "test-kappa-spin", 1)));
		var bought = new List<string>();
		var bot = BotWith(MerchantPlan.Shop);
		s.Merchant!.Choose(MerchantChoice.Shop);
		var mods = s.Merchant.Shop.ModifierShelf.Where(m => s.Merchant.Shop.ModifierTargets(m.Id).Count > 0).ToList();
		Assert.True(s.Merchant.Shop.Shelf.All(x => !s.Merchant.Shop.CanBuy(x.Id)), "no special fits: every slot is full");
		Assert.True(mods.Count > 0, "a modifier with a target is on the shelf");
		var want = s.Merchant.Shop.ModifierTargets(mods[0].Id).Min();
		// The choice is already made, so AtMerchant would skip; call the same buying path through a fresh session.
		s = At(pool, 60, true, StartersAnd((SpecialSlot.C, "test-kappa-grab", 1), (SpecialSlot.D, "test-kappa-spin", 1)));
		Assert.Equal("shop", bot.AtMerchant(s, bought), "shop");
		Assert.Equal(mods[0].Id, string.Join(",", bought), "the first shelf modifier with a target, once (60 coins)");
		Assert.Equal(10, s.Run.Coins, "R6: 50");
		Assert.True(s.Run[want]!.Modifier?.Id == mods[0].Id, $"A2: on the lowest slot that takes it ({want})");
	});

	[Test]
	public static void Bot_HealIfHurtElseShop_HealsBelowTheLine_ShopsAboveIt() => OnFixtures(() =>
	{
		var pool = ShopPool();
		var hurt = At(pool, 120, true, Starters); // 600 of 1,000
		Assert.Equal("heal", BotWith(MerchantPlan.HealIfHurtElseShop, 70).AtMerchant(hurt), "60% < 70%: heal");
		Assert.Equal(600 + Merchant.HealAmount, hurt.Run.Health, "R6: +25%");
		var fine = At(pool, 120, true, Starters);
		Assert.Equal("shop", BotWith(MerchantPlan.HealIfHurtElseShop, 60).AtMerchant(fine), "60% is not below 60%: shop");
		Assert.Equal(600, fine.Run.Health, "no heal");
		var skip = At(pool, 120, true, Starters);
		Assert.Equal("skip", BotWith(MerchantPlan.Skip).AtMerchant(skip), "skip");
		Assert.Equal(MerchantChoice.None, skip.Merchant!.Choice, "nothing chosen");
	});
}
