using BombArena.Core;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class ProfileFileTests
{
    [Test]
    public void A_sealed_save_opens_to_the_same_profile()
    {
        var p = new PlayerProfile { Jewels = 777, Lives = 4, Nickname = "Ana" };
        Assert.That(ProfileFile.TryOpen(ProfileFile.Seal(p.Serialize()), out var body), Is.True);
        Assert.That(PlayerProfile.Parse(body).Jewels, Is.EqualTo(777));
    }

    [Test]
    public void A_cut_short_or_altered_save_is_rejected()
    {
        var sealedText = ProfileFile.Seal(new PlayerProfile { Jewels = 777 }.Serialize());
        Assert.That(ProfileFile.TryOpen(sealedText.Substring(0, sealedText.Length / 2), out _), Is.False, "cut short");
        Assert.That(ProfileFile.TryOpen(sealedText.Replace("777", "999"), out _), Is.False, "altered");
        Assert.That(ProfileFile.TryOpen("", out _), Is.False, "empty");
    }

    [Test]
    public void Saves_from_before_sealing_still_open()
    {
        var old = new PlayerProfile { Jewels = 50 }.Serialize();
        Assert.That(ProfileFile.TryOpen(old, out var body), Is.True);
        Assert.That(PlayerProfile.Parse(body).Jewels, Is.EqualTo(50));
    }

    [Test]
    public void Graphics_choice_round_trips_and_starts_undecided()
    {
        Assert.That(new PlayerProfile().HighGraphics, Is.Null, "decided from the device on first launch");
        Assert.That(PlayerProfile.Parse(new PlayerProfile { HighGraphics = false }.Serialize()).HighGraphics, Is.False);
        Assert.That(PlayerProfile.Parse(new PlayerProfile { HighGraphics = true }.Serialize()).HighGraphics, Is.True);
    }
}
