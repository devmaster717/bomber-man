using BombArena.Core;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class StageProgressTests
{
    private PlayerProfile _profile;
    private LocalWallet _wallet;
    private StageProgress _progress;

    [SetUp]
    public void SetUp()
    {
        _profile = new PlayerProfile();
        _wallet = new LocalWallet(_profile, () => 1_800_000_000, null);
        _progress = new StageProgress(_profile, _wallet, null);
    }

    [Test]
    public void Only_stage_one_is_open_at_first()
    {
        Assert.That(_progress.IsUnlocked(1));
        Assert.That(_progress.IsUnlocked(2), Is.False);
        Assert.That(_progress.HighestUnlocked, Is.EqualTo(1));
    }

    [Test]
    public void Clearing_with_any_stars_unlocks_the_next_stage()
    {
        var r = _progress.RecordClear(1, stars: 1, default);
        Assert.That(r.UnlockedNext);
        Assert.That(_progress.IsUnlocked(2));
        Assert.That(_progress.IsUnlocked(3), Is.False);
        Assert.That(_progress.RecordClear(1, 3, default).UnlockedNext, Is.False, "already open");
    }

    [TestCase(1, 100)]
    [TestCase(50, 590)]
    [TestCase(100, 1090)]
    public void Clear_reward_grows_by_ten_per_stage(int stage, long jewels)
    {
        Assert.That(StageProgress.ClearJewels(stage), Is.EqualTo(jewels));
    }

    [Test]
    public void Spec_example_first_three_star_clear_then_a_two_star_clear_of_stage_ten()
    {
        Assert.That(_progress.RecordClear(10, 3, default).Total, Is.EqualTo(290));
        Assert.That(_progress.RecordClear(10, 2, default).Total, Is.EqualTo(230));
        Assert.That(_progress.RecordClear(10, 3, default).Total, Is.EqualTo(190 + 60), "the 100 bonus is paid only once");
        Assert.That(_wallet.Jewels, Is.EqualTo(1000 + 290 + 230 + 250));
    }

    [Test]
    public void Best_stars_are_kept()
    {
        _progress.RecordClear(4, 3, default);
        _progress.RecordClear(4, 1, default);
        Assert.That(_progress.BestStars(4), Is.EqualTo(3));
        Assert.That(_progress.BestStars(5), Is.Zero);
    }

    [Test]
    public void Power_ups_held_at_a_clear_carry_into_the_next_attempt_once()
    {
        var held = new PowerUpLoadout { FireUp = true, BombUps = 1, SpeedUpTicksLeft = 40 * Units.TicksPerSecond };
        _progress.RecordClear(12, 2, held);
        var start = _progress.TakeStartingLoadout();
        Assert.That((start.FireUp, start.BombUps, start.SpeedUpTicksLeft), Is.EqualTo((true, 1, 800)), "Speed Up keeps its 40 s");
        Assert.That(_progress.TakeStartingLoadout().IsEmpty, "taken: a failed attempt loses them");
    }

    [Test]
    public void Bought_power_ups_join_the_carried_ones()
    {
        _progress.RecordClear(1, 1, new PowerUpLoadout { BombUps = 1 });
        _profile.Inventory = new PowerUpLoadout { RemoteControl = true, BombUps = 1 };
        var start = _progress.TakeStartingLoadout();
        Assert.That((start.RemoteControl, start.BombUps), Is.EqualTo((true, 2)));
        Assert.That(_profile.Inventory.IsEmpty);
    }

    [Test]
    public void Carry_over_survives_closing_the_app()
    {
        _progress.RecordClear(3, 2, new PowerUpLoadout { FireUp = true });
        var reloaded = PlayerProfile.Parse(_profile.Serialize());
        var p2 = new StageProgress(reloaded, new LocalWallet(reloaded, () => 1_800_000_000, null), null);
        Assert.That(p2.TakeStartingLoadout().FireUp);
        Assert.That(p2.IsUnlocked(4));
    }
}
