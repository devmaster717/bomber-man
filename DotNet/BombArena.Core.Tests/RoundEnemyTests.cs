using System.Linq;
using BombArena.Core;
using BombArena.Core.Net;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class RoundEnemyTests
{
    [TestCase(2)]
    [TestCase(3)]
    public void Enemies_on_means_two_per_player_without_Runners_away_from_every_spawn(int players)
    {
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var g = Game.ForRound(23, 11, players, 0, seed, enemiesOn: true);
            Assert.That(g.Enemies, Has.Count.EqualTo(2 * players), $"seed {seed}");
            Assert.That(g.Enemies.Select(e => e.Kind).Distinct(), Is.SubsetOf(new[] { EnemyKind.Walker, EnemyKind.WallPasser, EnemyKind.Phantom }));
            foreach (var e in g.Enemies)
            foreach (var s in Arena.SpawnTiles(23, 11, players))
                Assert.That(System.Math.Abs(e.Tile.X - s.X) + System.Math.Abs(e.Tile.Y - s.Y), Is.GreaterThanOrEqualTo(5));
        }
    }

    [Test]
    public void Kinds_vary_between_rounds()
    {
        var kinds = Enumerable.Range(1, 30).SelectMany(s => Game.ForRound(23, 11, 3, 0, (ulong)s, true).Enemies.Select(e => e.Kind)).Distinct();
        Assert.That(kinds.Count(), Is.EqualTo(3));
    }

    [Test]
    public void Enemies_off_means_none()
    {
        Assert.That(Game.ForRound(23, 11, 3, 0, 1, enemiesOn: false).Enemies, Is.Empty);
    }

    [Test]
    public void Enemies_can_kill_any_bomber_in_a_round()
    {
        var g = Game.ForRound(23, 11, 2, 0, 1, enemiesOn: true);
        var e = g.Enemies[0];
        e.X = g.Bombers[1].X;
        e.Y = g.Bombers[1].Y;
        e.Heading = Direction.None;
        g.Step(BomberInput.None, BomberInput.None);
        Assert.That(g.Bombers[1].Alive, Is.False);
        Assert.That(g.Winner, Is.EqualTo(0));
    }

    [Test]
    public void Bought_power_ups_start_the_round_with_their_owner_and_every_phone_sees_them()
    {
        var link = new MemoryLink();
        var host = new LobbyHost(link, new PlayerInfo { Nickname = "H", Jewels = 1000, Inventory = new PowerUpLoadout { FireUp = true } }, new RoundSettings { EnemiesOn = true });
        var gLink = link.Connect();
        byte[] start = null;
        var lg = new LobbyGuest(gLink, new PlayerInfo { Nickname = "G", Jewels = 1000, Inventory = new PowerUpLoadout { BombUps = 2, SpeedUpTicksLeft = Bomber.SpeedUpTicks } });
        lg.Other += m => { if (Protocol.TypeOf(m) == MessageType.Start) start = m; };
        link.Pump();
        host.Detach();
        var round = new RoundHost(link, host.Players, host.GuestPeers, host.Settings, seed: 3);
        link.Pump();
        var guest = new RoundGuest(gLink, start);

        Assert.That(round.Game.Bombers[0].BlastRange, Is.EqualTo(2));
        Assert.That(round.Game.Bombers[1].MaxBombs, Is.EqualTo(3));
        Assert.That(round.Game.Bombers[1].SpeedUpTicksLeft, Is.EqualTo(Bomber.SpeedUpTicks));
        round.Tick();
        link.Pump();
        Assert.That(guest.Game.SaveState(), Is.EqualTo(round.Game.SaveState()), "the guest mirrors enemies and power-ups too");
        Assert.That(guest.Game.Enemies, Has.Count.EqualTo(4));
    }
}
