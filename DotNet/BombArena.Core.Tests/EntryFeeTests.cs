using System.Collections.Generic;
using System.Linq;
using BombArena.Core;
using BombArena.Core.Net;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class EntryFeeTests
{
    [Test]
    public void Winner_takes_the_whole_pot()
    {
        Assert.That(EntryFees.Pot(250, 3), Is.EqualTo(750));
        Assert.That(EntryFees.Payout(winner: 2, me: 2, fee: 250, players: 3, hostLost: false), Is.EqualTo(750));
        Assert.That(EntryFees.Payout(winner: 2, me: 0, fee: 250, players: 3, hostLost: false), Is.Zero);
    }

    [Test]
    public void A_draw_refunds_every_fee()
    {
        for (int me = 0; me < 3; me++)
            Assert.That(EntryFees.Payout(null, me, 500, 3, false), Is.EqualTo(500));
    }

    [Test]
    public void Losing_the_host_refunds_nobody()
    {
        Assert.That(EntryFees.Payout(1, 1, 500, 2, hostLost: true), Is.Zero);
        Assert.That(EntryFees.Payout(null, 1, 500, 2, hostLost: true), Is.Zero);
    }

    [Test]
    public void A_player_without_the_fee_cannot_join()
    {
        var link = new MemoryLink();
        var host = new LobbyHost(link, new PlayerInfo { Nickname = "H", Jewels = 2000 }, new RoundSettings { EntryFee = 500 });
        var poor = new LobbyGuest(link.Connect(), new PlayerInfo { Nickname = "P", Jewels = 499 });
        var rich = new LobbyGuest(link.Connect(), new PlayerInfo { Nickname = "R", Jewels = 500 });
        link.Pump();
        Assert.That(poor.Rejection, Is.EqualTo(EntryFees.NotEnoughJewels));
        Assert.That(host.Players.Select(p => p.Nickname), Is.EqualTo(new[] { "H", "R" }));
        Assert.That(host.CanStart);
    }

    [Test]
    public void The_round_cannot_start_while_someone_cannot_pay_a_raised_fee()
    {
        var link = new MemoryLink();
        var host = new LobbyHost(link, new PlayerInfo { Nickname = "H", Jewels = 2000 }, new RoundSettings { EntryFee = 100 });
        new LobbyGuest(link.Connect(), new PlayerInfo { Nickname = "G", Jewels = 300 });
        link.Pump();
        Assert.That(host.CanStart);
        host.ChangeSettings(new RoundSettings { EntryFee = 500 });
        Assert.That(host.CanStart, Is.False);
    }

    [Test]
    public void Full_round_settles_every_wallet()
    {
        // Three phones, each with its own wallet; fee 250.
        var wallets = Enumerable.Range(0, 3).Select(_ => new LocalWallet(new PlayerProfile(), () => 1_800_000_000, null)).ToArray();
        long fee = 250;
        foreach (var w in wallets) Assert.That(w.TrySpendJewels(fee), "everyone pays when the round starts");
        int winner = 1;
        for (int me = 0; me < 3; me++) wallets[me].AddJewels(EntryFees.Payout(winner, me, fee, 3, false));
        Assert.That(wallets.Select(w => w.Jewels), Is.EqualTo(new long[] { 750, 1500, 750 }));
        Assert.That(wallets.Sum(w => w.Jewels), Is.EqualTo(3000), "no jewels created or lost");
    }
}
