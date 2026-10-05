using System.Collections.Generic;
using BombArena.Core;
using BombArena.Core.Net;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class RoundInterruptionTests
{
    private MemoryLink _link;
    private RoundHost _host;
    private List<RoundGuest> _guests;
    private List<MemoryLink.Guest> _links;
    private double _clock;

    private void StartRound(int players = 3)
    {
        _link = new MemoryLink();
        _clock = 0;
        var lobby = new LobbyHost(_link, new PlayerInfo { Nickname = "Host", Jewels = 1000 }, new RoundSettings());
        _links = new List<MemoryLink.Guest>();
        var starts = new Dictionary<MemoryLink.Guest, byte[]>();
        for (int i = 1; i < players; i++)
        {
            var g = _link.Connect();
            _links.Add(g);
            var lg = new LobbyGuest(g, new PlayerInfo { Nickname = "G" + i, Jewels = 1000 });
            var captured = g;
            lg.Other += m => { if (Protocol.TypeOf(m) == MessageType.Start) starts[captured] = m; };
        }
        _link.Pump();
        lobby.Detach();
        _host = new RoundHost(_link, lobby.Players, lobby.GuestPeers, lobby.Settings, seed: 11) { Clock = () => _clock };
        _link.Pump();
        _guests = new List<RoundGuest>();
        foreach (var g in _links) _guests.Add(new RoundGuest(g, starts[g]));
    }

    private void Tick(int n = 1)
    {
        for (int i = 0; i < n; i++)
        {
            _host.Tick();
            _link.Pump();
        }
    }

    [Test]
    public void A_guest_forfeit_takes_their_bomber_out_and_the_round_goes_on()
    {
        StartRound();
        _guests[0].Forfeit();
        _link.Pump();
        Assert.That(_host.Game.Bombers[1].Alive, Is.False);
        Assert.That(_host.Game.Outcome, Is.EqualTo(Outcome.Playing));
        Assert.That(_guests[1].Game.Bombers[1].Alive, Is.False, "the others see it");
    }

    [Test]
    public void The_host_can_forfeit_and_its_phone_keeps_running_the_round()
    {
        StartRound();
        _host.ForfeitHost();
        _link.Pump();
        Assert.That(_host.Game.Bombers[0].Alive, Is.False);
        long before = _host.Game.Tick;
        Tick(5);
        Assert.That(_host.Game.Tick, Is.EqualTo(before + 5), "still ticking for the guests");
    }

    [Test]
    public void Forfeiting_down_to_one_player_ends_the_round()
    {
        StartRound(players: 2);
        _guests[0].Forfeit();
        _link.Pump();
        Assert.That(_host.Game.Winner, Is.EqualTo(0));
        Assert.That(_guests[0].Finished);
    }

    [Test]
    public void A_guest_leaving_the_app_pauses_the_round_for_everyone_until_they_are_back()
    {
        StartRound();
        _guests[0].SetAway(true);
        _link.Pump();
        Assert.That(_host.Paused);
        Assert.That(_guests[1].PausedFor, Is.EqualTo(1), "others see who they are waiting for");
        long t = _host.Game.Tick;
        Tick(10);
        Assert.That(_host.Game.Tick, Is.EqualTo(t), "nothing moves while paused");

        _guests[0].SetAway(false);
        _link.Pump();
        Assert.That(_host.Paused, Is.False);
        Assert.That(_guests[1].PausedFor, Is.Null);
        Tick();
        Assert.That(_host.Game.Tick, Is.EqualTo(t + 1));
    }

    [Test]
    public void A_dropped_guest_pauses_the_round_and_can_rejoin()
    {
        StartRound();
        Tick(20);
        _links[0].Drop(notifyHost: true);
        _link.Pump();
        Assert.That(_host.Paused);
        Assert.That(_guests[0].LinkLost);
        Assert.That(_guests[1].PausedFor, Is.EqualTo(1));

        var newLink = _link.Connect();
        _guests[0].Rejoin(newLink);
        _link.Pump();
        Assert.That(_host.Paused, Is.False);
        Assert.That(_guests[0].LinkLost, Is.False);
        Tick(3);
        Assert.That(_guests[0].Game.SaveState(), Is.EqualTo(_host.Game.SaveState()), "back in step with the host");

        _guests[0].SendInput(Direction.Up, bomb: true, detonate: false);
        _link.Pump();
        Tick();
        Assert.That(_host.Game.Bombs, Has.Some.Matches<Bomb>(b => b.Owner.Index == 1), "controls the same bomber again");
    }

    [Test]
    public void A_wrong_rejoin_token_is_refused()
    {
        StartRound();
        _links[0].Drop(notifyHost: true);
        _link.Pump();
        var stranger = _link.Connect();
        string refusal = null;
        stranger.Received += m => { if (Protocol.TypeOf(m) == MessageType.Rejected) refusal = Protocol.ReadRejected(m); };
        stranger.Send(RoundMessages.Rejoin(12345));
        _link.Pump();
        Assert.That(refusal, Is.EqualTo(RoundHost.RejoinRefused));
        Assert.That(_host.Paused, "still waiting for the real player");
    }

    [Test]
    public void After_sixty_seconds_the_host_may_count_a_missing_guest_as_a_forfeit()
    {
        StartRound();
        _links[1].Drop(notifyHost: true);
        _link.Pump();
        _clock = 59;
        Assert.That(_host.CanForfeitMissing, Is.False);
        Assert.That(_host.ForfeitMissing(), Is.False);
        _clock = 60;
        Assert.That(_host.CanForfeitMissing);
        Assert.That(_host.ForfeitMissing());
        _link.Pump();
        Assert.That(_host.Game.Bombers[2].Alive, Is.False);
        Assert.That(_host.Paused, Is.False, "the round continues");
        Assert.That(_guests[0].PausedFor, Is.Null);
    }

    [Test]
    public void The_host_leaving_the_app_pauses_the_guests()
    {
        StartRound();
        _host.SetHostAway(true);
        _link.Pump();
        Assert.That(_guests[0].PausedFor, Is.EqualTo(0));
        _host.SetHostAway(false);
        _link.Pump();
        Assert.That(_guests[0].PausedFor, Is.Null);
    }

    [Test]
    public void There_is_no_way_for_a_player_to_pause_on_purpose()
    {
        // Only Away/Back (sent when the app leaves the screen) and a dropped link pause a round.
        Assert.That(System.Enum.GetNames(typeof(MessageType)), Has.None.EqualTo("Pause"));
    }
}
