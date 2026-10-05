using System.Linq;
using BombArena.Core;
using BombArena.Core.Net;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class LobbyTests
{
    private static PlayerInfo P(string name, int avatar = 0, long jewels = 1000) => new PlayerInfo { Nickname = name, Avatar = avatar, Jewels = jewels };

    [Test]
    public void Guests_join_and_every_phone_sees_every_nickname_and_avatar()
    {
        var link = new MemoryLink();
        var host = new LobbyHost(link, P("Host", 1), new RoundSettings());
        var a = new LobbyGuest(link.Connect(), P("Ana", 4));
        var b = new LobbyGuest(link.Connect(), P("Bo", 7));
        link.Pump();

        Assert.That(host.Players.Select(p => (p.Nickname, p.Avatar)), Is.EqualTo(new[] { ("Host", 1), ("Ana", 4), ("Bo", 7) }));
        Assert.That(a.Players.Select(p => p.Nickname), Is.EqualTo(new[] { "Host", "Ana", "Bo" }));
        Assert.That(b.Players.Select(p => p.Avatar), Is.EqualTo(new[] { 1, 4, 7 }));
        Assert.That((a.YourIndex, b.YourIndex), Is.EqualTo((1, 2)));
    }

    [Test]
    public void The_host_accepts_at_most_two_guests()
    {
        var link = new MemoryLink();
        var host = new LobbyHost(link, P("Host"), new RoundSettings());
        new LobbyGuest(link.Connect(), P("A"));
        new LobbyGuest(link.Connect(), P("B"));
        var c = new LobbyGuest(link.Connect(), P("C"));
        link.Pump();
        Assert.That(host.Players, Has.Count.EqualTo(3));
        Assert.That(c.Rejection, Is.EqualTo(LobbyHost.RoomFull));
        Assert.That(c.Connected, Is.False);
    }

    [Test]
    public void A_guest_leaving_the_lobby_is_removed_for_everyone()
    {
        var link = new MemoryLink();
        var host = new LobbyHost(link, P("Host"), new RoundSettings());
        var aLink = link.Connect();
        var a = new LobbyGuest(aLink, P("A"));
        var b = new LobbyGuest(link.Connect(), P("B"));
        link.Pump();
        a.Leave();
        link.Pump();
        Assert.That(host.Players.Select(p => p.Nickname), Is.EqualTo(new[] { "Host", "B" }));
        Assert.That(b.Players.Select(p => p.Nickname), Is.EqualTo(new[] { "Host", "B" }));
        Assert.That(b.YourIndex, Is.EqualTo(1), "moves up to fill the gap");
    }

    [Test]
    public void Settings_changes_reach_the_guests()
    {
        var link = new MemoryLink();
        var host = new LobbyHost(link, P("Host"), new RoundSettings());
        var a = new LobbyGuest(link.Connect(), P("A"));
        link.Pump();
        host.ChangeSettings(new RoundSettings { Width = 27, Height = 15, TimeLimitSeconds = 180, EntryFee = 500, EnemiesOn = true });
        link.Pump();
        Assert.That((a.Settings.Width, a.Settings.Height, a.Settings.TimeLimitSeconds, a.Settings.EntryFee, a.Settings.EnemiesOn),
            Is.EqualTo((27, 15, 180, 500L, true)));
    }

    [Test]
    public void A_different_protocol_version_is_refused()
    {
        var link = new MemoryLink();
        var host = new LobbyHost(link, P("Host"), new RoundSettings());
        var g = link.Connect();
        var hello = Protocol.Hello(P("Old"));
        hello[1] = Protocol.Version + 1;
        string reason = null;
        g.Received += m => { if (Protocol.TypeOf(m) == MessageType.Rejected) reason = Protocol.ReadRejected(m); };
        g.Send(hello);
        link.Pump();
        Assert.That(host.Players, Has.Count.EqualTo(1));
        Assert.That(reason, Is.EqualTo(LobbyHost.WrongVersion));
    }

    [Test]
    public void Nicknames_arriving_over_the_air_are_cleaned()
    {
        var link = new MemoryLink();
        var host = new LobbyHost(link, P("Host"), new RoundSettings());
        new LobbyGuest(link.Connect(), P("  A very long nickname indeed  "));
        link.Pump();
        Assert.That(host.Players[1].Nickname, Is.EqualTo("A very long"));
    }

    [Test]
    public void Settings_offer_the_spec_choices()
    {
        Assert.That(RoundSettings.TimeLimitChoices, Is.EqualTo(new[] { 0, 120, 180, 300 }));
        Assert.That(RoundSettings.EntryFeeChoices, Is.EqualTo(new long[] { 100, 250, 500, 1000 }));
        Assert.That(new RoundSettings().TimeLimitSeconds, Is.Zero, "Unlimited by default");
    }
}
