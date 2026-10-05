using BombArena.Core;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class ShopTests
{
    private PlayerProfile _p;
    private LocalWallet _w;
    private Shop _shop;

    [SetUp]
    public void SetUp()
    {
        _p = new PlayerProfile();
        _p.OwnedAvatars.Add(0);
        _w = new LocalWallet(_p, () => 1_800_000_000, null);
        _shop = new Shop(_p, _w, null);
    }

    [Test]
    public void Life_pack_adds_ten_lives_for_500_even_beyond_ten()
    {
        Assert.That(_shop.BuyLifePack(), Is.EqualTo(PurchaseResult.Bought));
        Assert.That((_w.Lives, _w.Jewels), Is.EqualTo((20, 500L)));
    }

    [Test]
    public void Prices_follow_the_spec()
    {
        Assert.That(Shop.PowerUpPrice(PowerUpKind.FireUp), Is.EqualTo(300));
        Assert.That(Shop.PowerUpPrice(PowerUpKind.BombUp), Is.EqualTo(300));
        Assert.That(Shop.PowerUpPrice(PowerUpKind.RemoteControl), Is.EqualTo(300));
        Assert.That(Shop.PowerUpPrice(PowerUpKind.SpeedUp), Is.EqualTo(100));
        Assert.That(Shop.AvatarPrice, Is.EqualTo(1000));
    }

    [Test]
    public void Bought_power_ups_go_into_the_inventory_and_start_the_next_attempt()
    {
        Assert.That(_shop.BuyPowerUp(PowerUpKind.FireUp), Is.EqualTo(PurchaseResult.Bought));
        Assert.That(_shop.BuyPowerUp(PowerUpKind.SpeedUp), Is.EqualTo(PurchaseResult.Bought));
        Assert.That(_w.Jewels, Is.EqualTo(600));
        Assert.That(_p.Inventory.FireUp && _p.Inventory.SpeedUpTicksLeft == Bomber.SpeedUpTicks);

        var start = new StageProgress(_p, _w, null).TakeStartingLoadout();
        Assert.That(start.FireUp);
        var g = Game.ForStage(new StageSpec(), 1);
        g.ApplyLoadout(0, start);
        Assert.That(g.Bomber.BlastRange, Is.EqualTo(2));
        Assert.That(_p.Inventory.IsEmpty, "used up by the attempt");
    }

    [Test]
    public void A_type_already_held_or_in_the_inventory_is_not_sold_except_Speed_Up()
    {
        _shop.BuyPowerUp(PowerUpKind.RemoteControl);
        Assert.That(_shop.CanBuy(PowerUpKind.RemoteControl), Is.False);
        Assert.That(_shop.BuyPowerUp(PowerUpKind.RemoteControl), Is.EqualTo(PurchaseResult.NotForSale));

        _p.CarriedPowerUps = new PowerUpLoadout { FireUp = true };
        Assert.That(_shop.CanBuy(PowerUpKind.FireUp), Is.False, "carried from the last clear");

        _shop.BuyPowerUp(PowerUpKind.SpeedUp);
        Assert.That(_shop.CanBuy(PowerUpKind.SpeedUp));
    }

    [Test]
    public void Bomb_Up_is_not_sold_beyond_four_bombs()
    {
        _w.AddJewels(10_000);
        _p.CarriedPowerUps = new PowerUpLoadout { BombUps = 1 };
        Assert.That(_shop.BuyPowerUp(PowerUpKind.BombUp), Is.EqualTo(PurchaseResult.Bought));
        Assert.That(_shop.BuyPowerUp(PowerUpKind.BombUp), Is.EqualTo(PurchaseResult.Bought));
        Assert.That(_shop.BuyPowerUp(PowerUpKind.BombUp), Is.EqualTo(PurchaseResult.NotForSale), "1 + 1 carried + 2 bought = 4");
    }

    [Test]
    public void Avatars_cost_1000_and_cannot_be_bought_twice()
    {
        Assert.That(_shop.BuyAvatar(0), Is.EqualTo(PurchaseResult.NotForSale), "already owned");
        Assert.That(_shop.BuyAvatar(5), Is.EqualTo(PurchaseResult.Bought));
        Assert.That(_shop.Owns(5));
        Assert.That(_w.Jewels, Is.EqualTo(0));
        Assert.That(_shop.BuyAvatar(5), Is.EqualTo(PurchaseResult.NotForSale));
    }

    [Test]
    public void Purchases_fail_cleanly_when_the_balance_is_too_low()
    {
        _w.TrySpendJewels(950);
        Assert.That(_shop.BuyLifePack(), Is.EqualTo(PurchaseResult.NotEnoughJewels));
        Assert.That(_shop.BuyPowerUp(PowerUpKind.FireUp), Is.EqualTo(PurchaseResult.NotEnoughJewels));
        Assert.That(_shop.BuyAvatar(3), Is.EqualTo(PurchaseResult.NotEnoughJewels));
        Assert.That((_w.Jewels, _w.Lives), Is.EqualTo((50L, 10)));
        Assert.That(_p.Inventory.IsEmpty);
    }

    [Test]
    public void Jewel_packs_are_a_placeholder_that_credits_nothing()
    {
        Assert.That(_shop.BuyJewelPack(0), Is.False);
        Assert.That(_w.Jewels, Is.EqualTo(1000));
    }
}
