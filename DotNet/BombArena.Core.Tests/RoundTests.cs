using System.Collections.Generic;
using System.Linq;
using BombArena.Core;
using BombArena.Core.Net;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class RoundTests
{
    private static PlayerInfo P(string n) => new PlayerInfo { Nickname = n, Jewels = 1000 };

    /// <summary>A host and guests that have gone through the lobby and started a round.</summary>
    private sealed class Room
    {
        public readonly MemoryLink Link = new MemoryLink();
        public RoundHost Host;
        public readonly List<RoundGuest> Guests = new List<RoundGuest>();

        public Room(int players, RoundSettings settings, ulong seed = 7)
        {
            var lobby = new LobbyHost(Link, P("Host"), settings);
            var links = new List<MemoryLink.Guest>();
            var starts = new Dictionary<MemoryLink.Guest, byte[]>();
            for (int i = 1; i < players; i++)
            {
                var g = Link.Connect();
                links.Add(g);
                var lg = new LobbyGuest(g, P("G" + i));
                var captured = g;
                lg.Other += m => { if (Protocol.TypeOf(m) == MessageType.Start) starts[captured] = m; };
            }
            Link.Pump();
            lobby.Detach();
            Host = new RoundHost(Link, lobby.Players, lobby.GuestPeers, settings, seed);
            Link.Pump();
            foreach (var g in links) Guests.Add(new RoundGuest(g, starts[g]));
        }

        public void Tick(int n = 1)
        {
            for (int i = 0; i < n; i++)
            {
                Host.Tick();
                Link.Pump();
            }
        }
    }

    [Test]
    public void A_round_starts_with_two_or_three_spawns_and_random_soft_blocks()
    {
        var two = Game.ForRound(23, 11, 2, 0, seed: 1);
        var three = Game.ForRound(23, 11, 3, 0, seed: 1);
        Assert.That(two.Bombers.Select(b => b.Spawn), Is.EqualTo(Arena.SpawnTiles(23, 11, 2)));
        Assert.That(three.Bombers.Select(b => b.Spawn), Is.EqualTo(Arena.SpawnTiles(23, 11, 3)));
        Assert.That(two.SoftBlockDropPercent, Is.EqualTo(40));
        Assert.That(two.IsRound && two.TimeLimitTicks == 0, "unlimited by default");
        var other = Game.ForRound(23, 11, 2, 0, seed: 2);
        Assert.That(Enumerable.Range(0, 23 * 11).Any(i => other.Arena[i % 23, i / 23] != two.Arena[i % 23, i / 23]),
            "a different seed gives a different layout");
    }

    [Test]
    public void Last_bomber_standing_wins()
    {
        var g = Game.ForRound(23, 11, 3, 0, seed: 3);
        g.Forfeit(1);
        Assert.That(g.Outcome, Is.EqualTo(Outcome.Playing));
        g.Forfeit(2);
        Assert.That(g.Outcome, Is.EqualTo(Outcome.RoundOver));
        Assert.That(g.Winner, Is.EqualTo(0));
    }

    [Test]
    public void Everyone_left_dying_together_is_a_draw()
    {
        var g = new Game(Arena.FromRows(
            "#######",
            "#.....#",
            "#######"), 1UL, new TilePos(2, 1), new TilePos(3, 1));
        g.IsRound = true;
        g.Step(new BomberInput(Direction.None, placeBomb: true), BomberInput.None);
        while (g.Outcome == Outcome.Playing) g.Step(BomberInput.None, BomberInput.None);
        Assert.That(g.IsDraw, "both caught by the same blast");
    }

    [Test]
    public void Time_up_with_two_standing_is_a_draw()
    {
        var g = Game.ForRound(23, 11, 2, timeLimitSeconds: 120, seed: 4);
        for (int i = 0; i < 120 * Units.TicksPerSecond; i++) g.Step(BomberInput.None, BomberInput.None);
        Assert.That(g.Outcome, Is.EqualTo(Outcome.RoundOver));
        Assert.That(g.Winner, Is.Null);
    }

    [Test]
    public void Guests_see_exactly_the_hosts_state_every_tick()
    {
        var room = new Room(3, new RoundSettings { Width = 21, Height = 11 });
        var rng = new Rng(9);
        for (int t = 0; t < 600 && room.Host.Game.Outcome == Outcome.Playing; t++)
        {
            room.Host.SetHostInput((Direction)rng.Next(5), rng.Next(25) == 0, false);
            foreach (var g in room.Guests) g.SendInput((Direction)rng.Next(5), rng.Next(25) == 0, false);
            room.Link.Pump();
            room.Tick();
            var hostState = room.Host.Game.SaveState();
            foreach (var g in room.Guests)
                Assert.That(g.Game.SaveState(), Is.EqualTo(hostState), $"tick {t}");
        }
    }

    [Test]
    public void A_guests_bomb_press_is_placed_by_the_host()
    {
        var room = new Room(2, new RoundSettings());
        room.Guests[0].SendInput(Direction.None, bomb: true, detonate: false);
        room.Link.Pump();
        room.Tick();
        Assert.That(room.Host.Game.Bombs.Single().Owner.Index, Is.EqualTo(1));
        Assert.That(room.Guests[0].Game.Bombs.Single().Tile, Is.EqualTo(room.Guests[0].Game.Bombers[1].Tile));
    }

    [Test]
    public void Guest_moves_reach_the_host_and_come_back_in_the_snapshot()
    {
        var room = new Room(2, new RoundSettings { Width = 23, Height = 11 });
        var guest = room.Guests[0];
        int startY = guest.Game.Bombers[1].Y;
        guest.SendInput(Direction.Up, false, false); // bottom-right spawn: up is open (spawn area kept clear)
        room.Link.Pump();
        room.Tick(5);
        Assert.That(guest.Game.Bombers[1].Y, Is.LessThan(startY));
        Assert.That(guest.YourIndex, Is.EqualTo(1));
    }

    [Test]
    public void Result_reaches_every_guest()
    {
        var room = new Room(3, new RoundSettings());
        room.Host.Game.Forfeit(1);
        room.Host.Game.Forfeit(2);
        room.Tick();
        Assert.That(room.Guests.All(g => g.Finished && g.Winner == 0));
    }

    [Test]
    public void A_guest_whose_link_drops_is_out_of_the_round()
    {
        var room = new Room(3, new RoundSettings());
        room.Link.GuestFor(1).Drop(notifyHost: true);
        room.Link.Pump();
        Assert.That(room.Host.Game.Bombers[1].Alive, Is.False);
        Assert.That(room.Host.Game.Outcome, Is.EqualTo(Outcome.Playing), "two still standing");
    }

    [Test]
    public void Guests_know_when_the_host_is_lost()
    {
        var room = new Room(2, new RoundSettings());
        room.Link.GuestFor(1).Drop(notifyHost: false);
        room.Link.Pump();
        Assert.That(room.Guests[0].HostLost);
    }

    [Test]
    public void A_snapshot_is_small_enough_for_bluetooth_twenty_times_a_second()
    {
        var g = Game.ForRound(27, 15, 3, 0, seed: 5);
        int bytes = RoundMessages.Snapshot(g).Length;
        Assert.That(bytes * Units.TicksPerSecond, Is.LessThan(30_000), $"{bytes} bytes per snapshot");
    }
}
