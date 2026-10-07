using BombArena.Core;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class WalletTests
{
    private long _now = 1_800_000_000;
    private int _saves;

    private LocalWallet Wallet(PlayerProfile p = null) => new LocalWallet(p ?? new PlayerProfile(), () => _now, () => _saves++);

    [Test]
    public void New_player_starts_with_1000_jewels_and_10_lives()
    {
        var w = Wallet();
        Assert.That((w.Jewels, w.Lives), Is.EqualTo((1000L, 10)));
        Assert.That(w.SecondsToNextLife, Is.Null);
    }

    [Test]
    public void A_cleared_attempt_costs_no_life_and_a_failed_one_costs_one()
    {
        var w = Wallet();
        Assert.That(w.TryStartAttempt());
        w.EndAttempt(cleared: true);
        Assert.That(w.Lives, Is.EqualTo(10));
        Assert.That(w.TryStartAttempt());
        w.EndAttempt(cleared: false);
        Assert.That(w.Lives, Is.EqualTo(9));
    }

    [Test]
    public void No_attempt_without_a_life()
    {
        var w = Wallet(new PlayerProfile { Lives = 0, RegenStartedAt = _now });
        Assert.That(w.TryStartAttempt(), Is.False);
    }

    [Test]
    public void One_life_regenerates_every_five_minutes_up_to_ten()
    {
        var w = Wallet(new PlayerProfile { Lives = 7 });
        Assert.That(w.SecondsToNextLife, Is.EqualTo(300));
        _now += 299; w.Refresh();
        Assert.That(w.Lives, Is.EqualTo(7));
        Assert.That(w.SecondsToNextLife, Is.EqualTo(1));
        _now += 1; w.Refresh();
        Assert.That(w.Lives, Is.EqualTo(8));
        _now += 10_000; w.Refresh();
        Assert.That(w.Lives, Is.EqualTo(10), "stops at ten");
        Assert.That(w.SecondsToNextLife, Is.Null);
    }

    [Test]
    public void Regeneration_counts_time_while_the_app_was_closed()
    {
        var saved = new PlayerProfile { Lives = 4, RegenStartedAt = _now }.Serialize();
        _now += 3 * 300 + 120; // closed for 17 minutes
        var w = Wallet(PlayerProfile.Parse(saved));
        Assert.That(w.Lives, Is.EqualTo(7));
        Assert.That(w.SecondsToNextLife, Is.EqualTo(180));
    }

    [Test]
    public void Losing_a_life_from_ten_starts_the_countdown()
    {
        var w = Wallet();
        w.TryStartAttempt();
        w.EndAttempt(false);
        Assert.That(w.SecondsToNextLife, Is.EqualTo(300));
    }

    [Test]
    public void Life_pack_adds_ten_even_beyond_the_cap_and_stops_regeneration()
    {
        var w = Wallet(new PlayerProfile { Lives = 7 });
        w.AddLives(10);
        Assert.That(w.Lives, Is.EqualTo(17));
        Assert.That(w.SecondsToNextLife, Is.Null);
        _now += 3600; w.Refresh();
        Assert.That(w.Lives, Is.EqualTo(17), "no regeneration above ten");
        for (int i = 0; i < 7; i++) { w.TryStartAttempt(); w.EndAttempt(false); }
        Assert.That(w.Lives, Is.EqualTo(10));
        Assert.That(w.SecondsToNextLife, Is.Null);
        w.TryStartAttempt(); w.EndAttempt(false);
        Assert.That(w.SecondsToNextLife, Is.EqualTo(300), "regenerates again once below ten");
    }

    [Test]
    public void An_attempt_cut_short_by_closing_the_app_costs_a_life_on_next_launch()
    {
        var p = new PlayerProfile();
        var w = Wallet(p);
        w.TryStartAttempt();
        var saved = p.Serialize(); // app killed here

        var after = PlayerProfile.Parse(saved);
        var w2 = Wallet(after);
        Assert.That(w2.RecoverInterruptedAttempt(), Is.True);
        Assert.That(w2.Lives, Is.EqualTo(9));
        Assert.That(w2.RecoverInterruptedAttempt(), Is.False, "only once");
    }

    [Test]
    public void Spending_jewels_needs_enough_balance()
    {
        var w = Wallet();
        Assert.That(w.TrySpendJewels(1500), Is.False);
        Assert.That(w.TrySpendJewels(500), Is.True);
        Assert.That(w.Jewels, Is.EqualTo(500));
        w.AddJewels(290);
        Assert.That(w.Jewels, Is.EqualTo(790));
    }

    [Test]
    public void Every_change_is_saved()
    {
        var w = Wallet();
        int before = _saves;
        w.AddJewels(1);
        w.TryStartAttempt();
        w.EndAttempt(false);
        Assert.That(_saves, Is.GreaterThanOrEqualTo(before + 3));
    }

    [Test]
    public void Checking_the_lives_saves_only_when_something_changed()
    {
        // The home screen refreshes every frame; that must not rewrite the save every frame.
        var w = Wallet(new PlayerProfile { Lives = 7 });
        int before = _saves;
        for (int i = 0; i < 100; i++) { _now += 1; w.Refresh(); }
        Assert.That(_saves, Is.EqualTo(before), "100 seconds in, no life has come back yet");
        _now += 200; w.Refresh();
        Assert.That(w.Lives, Is.EqualTo(8));
        Assert.That(_saves, Is.EqualTo(before + 1), "a life came back: saved once");
    }

    [Test]
    public void Profile_round_trips_through_its_save_format()
    {
        var p = new PlayerProfile { Jewels = 1234, Lives = 13, Nickname = "Sasha", Avatar = 3, PadOnRight = true, View3D = false };
        p.BestStars[1] = 3; p.BestStars[2] = 1;
        p.ThreeStarBonusPaid.Add(1);
        p.OwnedAvatars.Add(0); p.OwnedAvatars.Add(3);
        p.CarriedPowerUps = new PowerUpLoadout { FireUp = true, BombUps = 2, SpeedUpTicksLeft = 99 };
        p.Inventory = new PowerUpLoadout { RemoteControl = true };
        var q = PlayerProfile.Parse(p.Serialize());
        Assert.That(q.Serialize(), Is.EqualTo(p.Serialize()));
        Assert.That(q.BestStars[1], Is.EqualTo(3));
        Assert.That(q.CarriedPowerUps.BombUps, Is.EqualTo(2));
        Assert.That(q.Inventory.RemoteControl);
        Assert.That(q.View3D, Is.False);
    }

    [Test]
    public void Pad_side_round_trips_and_old_left_handed_saves_keep_their_layout()
    {
        Assert.That(new PlayerProfile().PadOnRight, Is.False, "the pad starts on the left");
        Assert.That(PlayerProfile.Parse(new PlayerProfile { PadOnRight = true }.Serialize()).PadOnRight, Is.True);
        Assert.That(PlayerProfile.Parse("leftHanded = True\n").PadOnRight, Is.True, "Left-handed meant pad on the right");
        Assert.That(PlayerProfile.Parse("leftHanded = False\n").PadOnRight, Is.False);
    }

    [Test]
    public void A_damaged_save_still_loads()
    {
        var p = PlayerProfile.Parse("jewels = 50\ngarbage line\nlives = x\nunknown = 3\n");
        Assert.That(p.Jewels, Is.EqualTo(50));
        Assert.That(p.Lives, Is.EqualTo(PlayerProfile.StartingLives), "a garbled number keeps its default");
        Assert.That(p.View3D, Is.True, "saves from before the 3D view open in 3D");
    }
}
