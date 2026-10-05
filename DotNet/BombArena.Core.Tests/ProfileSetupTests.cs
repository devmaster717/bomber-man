using System;
using BombArena.Core;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class ProfileSetupTests
{
    [TestCase("Sasha", "Sasha")]
    [TestCase("  Big   Bomber  ", "Big Bomber")]
    [TestCase("ABCDEFGHIJKLMNOP", "ABCDEFGHIJKL")]
    [TestCase("Line\nbreak\t!", "Line break !")]
    public void Nicknames_are_cleaned_and_capped_at_twelve_characters(string raw, string saved)
    {
        Assert.That(PlayerProfile.CleanNickname(raw), Is.EqualTo(saved));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\n\t")]
    public void Empty_nicknames_are_not_allowed(string raw)
    {
        Assert.That(PlayerProfile.IsValidNickname(raw), Is.False);
        Assert.Throws<ArgumentException>(() => new PlayerProfile().CompleteSetup(raw, 0));
    }

    [Test]
    public void First_launch_needs_setup_until_a_nickname_is_chosen()
    {
        var p = new PlayerProfile();
        Assert.That(p.NeedsSetup);
        p.CompleteSetup("Ana", 4);
        Assert.That(p.NeedsSetup, Is.False);
        Assert.That(p.Avatar, Is.EqualTo(4));
        Assert.That(p.OwnedAvatars, Is.EquivalentTo(new[] { 4 }), "the chosen starting avatar is free");
        var reloaded = PlayerProfile.Parse(p.Serialize());
        Assert.That((reloaded.Nickname, reloaded.Avatar, reloaded.NeedsSetup), Is.EqualTo(("Ana", 4, false)));
    }

    [Test]
    public void There_are_ten_avatars()
    {
        Assert.That(PlayerProfile.AvatarCount, Is.EqualTo(10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerProfile().CompleteSetup("Ana", 10));
    }
}
