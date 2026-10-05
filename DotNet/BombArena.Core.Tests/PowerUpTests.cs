using System.Linq;
using BombArena.Core;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class PowerUpTests
{
    private const int T = Units.PerTile;

    private static readonly string[] Open =
    {
        "###########",
        "#.........#",
        "#.#.#.#.#.#",
        "#.........#",
        "#.#.#.#.#.#",
        "#.........#",
        "###########",
    };

    private static Game At(int x, int y, params string[] rows) => new Game(Arena.FromRows(rows.Length > 0 ? rows : Open), 1UL, new TilePos(x, y));

    private static void Wait(Game g, int ticks, Direction d = Direction.None, bool detonate = false)
    {
        for (int i = 0; i < ticks && g.Outcome == Outcome.Playing; i++) g.Step(new BomberInput(d, false, detonate));
    }

    private static void PickUp(Game g, PowerUpKind kind)
    {
        g.AddPowerUp(g.Bomber.Tile, kind);
        g.Step(Direction.None);
    }

    [Test]
    public void Fire_Up_extends_the_blast_to_two_tiles()
    {
        var g = At(1, 1);
        PickUp(g, PowerUpKind.FireUp);
        Assert.That(g.Bomber.BlastRange, Is.EqualTo(2));
        g.Step(Direction.None, placeBomb: true);
        Wait(g, 10, Direction.Down);  // (1,3)
        Wait(g, 10, Direction.Right); // (3,3): out of range
        while (g.Bombs.Count > 0 && g.Outcome == Outcome.Playing) g.Step(Direction.None);
        Assert.That(g.IsBurning(3, 1) && g.IsBurning(1, 3), "two tiles right and down");
        Assert.That(g.IsBurning(4, 1), Is.False);
    }

    [Test]
    public void Bomb_Up_adds_a_bomb_and_is_capped_at_four()
    {
        var g = At(1, 1);
        Assert.That(g.Bomber.MaxBombs, Is.EqualTo(1));
        for (int i = 0; i < 5; i++) PickUp(g, PowerUpKind.BombUp);
        Assert.That(g.Bomber.MaxBombs, Is.EqualTo(Bomber.BombCap));
        Assert.That(g.Bomber.CanUse(PowerUpKind.BombUp), Is.False);

        for (int x = 1; x <= 5; x++)
        {
            g.Step(Direction.None, placeBomb: true);
            Wait(g, 5, Direction.Right);
        }
        Assert.That(g.Bombs, Has.Count.EqualTo(4), "a fifth bomb is refused");
    }

    [Test]
    public void Speed_Up_doubles_speed_for_sixty_seconds_and_a_duplicate_resets_it()
    {
        var g = At(1, 1);
        PickUp(g, PowerUpKind.SpeedUp);
        Assert.That(g.Bomber.SpeedPerTick, Is.EqualTo(400));
        Wait(g, 30 * Units.TicksPerSecond);
        PickUp(g, PowerUpKind.SpeedUp);
        Assert.That(g.Bomber.SpeedUpTicksLeft, Is.EqualTo(Bomber.SpeedUpTicks - 1), "reset to 60 s");
        Wait(g, Bomber.SpeedUpTicks - 1);
        Assert.That(g.Bomber.SpeedUpTicksLeft, Is.Zero);
        Assert.That(g.Bomber.SpeedPerTick, Is.EqualTo(200));
    }

    [Test]
    public void Remote_bomb_waits_for_Detonate_and_counts_toward_the_limit()
    {
        var g = At(1, 1);
        PickUp(g, PowerUpKind.RemoteControl);
        g.Step(Direction.None, placeBomb: true);
        var remote = g.Bombs.Single();
        Assert.That(remote.IsRemote);
        Wait(g, 10, Direction.Down);
        Wait(g, 100);
        Assert.That(g.Bombs, Has.Count.EqualTo(1), "no fuse");

        g.Step(Direction.None, placeBomb: true); // limit 1: refused
        Assert.That(g.Bombs, Has.Count.EqualTo(1));

        Wait(g, 1, detonate: true);
        Assert.That(g.Bombs, Is.Empty);
        Assert.That(g.IsBurning(1, 1));
    }

    [Test]
    public void With_a_remote_bomb_out_the_next_bomb_is_normal()
    {
        var g = At(1, 1);
        PickUp(g, PowerUpKind.RemoteControl);
        PickUp(g, PowerUpKind.BombUp);
        g.Step(Direction.None, placeBomb: true);
        Wait(g, 10, Direction.Right);
        g.Step(Direction.None, placeBomb: true);
        Assert.That(g.Bombs.Count(b => b.IsRemote), Is.EqualTo(1));
        Assert.That(g.Bombs.Count(b => !b.IsRemote), Is.EqualTo(1));
    }

    [Test]
    public void Remote_bomb_goes_off_in_a_chain_reaction()
    {
        var g = new Game(Arena.FromRows(Open), 1UL, new TilePos(1, 1), new TilePos(5, 5));
        g.AddPowerUp(new TilePos(1, 1), PowerUpKind.RemoteControl);
        g.Step(BomberInput.None, BomberInput.None);
        g.Step(new BomberInput(Direction.None, placeBomb: true), BomberInput.None); // remote at (1,1)
        for (int i = 0; i < 10; i++) g.Step(new BomberInput(Direction.Down), new BomberInput(Direction.Up)); // b0 (1,3), b1 (5,3)
        for (int i = 0; i < 15; i++) g.Step(BomberInput.None, new BomberInput(i < 10 ? Direction.Up : Direction.Left)); // b1 (4,1)
        g.Step(BomberInput.None, BomberInput.None);
        // b1 at (4,1)… walk to (2,1) and drop a normal bomb next to the remote one
        for (int i = 0; i < 10; i++) g.Step(BomberInput.None, new BomberInput(Direction.Left));
        g.Step(BomberInput.None, new BomberInput(Direction.None, placeBomb: true));
        for (int i = 0; i < 5; i++) g.Step(BomberInput.None, new BomberInput(Direction.Right));
        for (int i = 0; i < 10; i++) g.Step(BomberInput.None, new BomberInput(Direction.Down));
        Assert.That(g.Bombs, Has.Count.EqualTo(2));
        while (g.Bombs.Any(b => !b.IsRemote) && g.Tick < 2000) g.Step(BomberInput.None, BomberInput.None);
        Assert.That(g.Bombs, Is.Empty, "the remote bomb went off with its neighbour");
    }

    [Test]
    public void Death_loses_every_power_up_and_starts_the_remote_bombs_fuse()
    {
        var g = new Game(Arena.FromRows(Open), 1UL, new TilePos(1, 1), new TilePos(9, 5));
        g.AddPowerUp(new TilePos(1, 1), PowerUpKind.RemoteControl);
        g.Step(BomberInput.None, BomberInput.None);
        g.ApplyLoadout(0, new PowerUpLoadout { FireUp = true, BombUps = 1, SpeedUpTicksLeft = 100 });
        g.Step(new BomberInput(Direction.None, placeBomb: true), BomberInput.None);
        for (int i = 0; i < 10; i++) g.Step(new BomberInput(Direction.Right), BomberInput.None);
        g.AddEnemy(EnemyKind.Walker, g.Bombers[0].Tile); // right on the bomber
        g.Step(BomberInput.None, BomberInput.None);
        var b0 = g.Bombers[0];
        Assert.That(b0.Alive, Is.False);
        Assert.That(b0.Loadout.IsEmpty);
        Assert.That(g.Bombs.Single().FuseLeft, Is.EqualTo(Bomb.FuseTicks - 1 + 1).Or.EqualTo(Bomb.FuseTicks));
    }

    [Test]
    public void Stage_hides_one_power_up_under_a_soft_block_other_than_the_exit()
    {
        for (ulong attempt = 1; attempt <= 30; attempt++)
        {
            var g = Game.ForStage(new StageSpec { Seed = 7, Enemies = { new EnemyGroup(EnemyKind.Walker, 3) } }, attempt);
            var p = g.PowerUps.Single();
            Assert.That(g.Arena[p.Tile], Is.EqualTo(Tile.SoftBlock));
            Assert.That(p.Tile, Is.Not.EqualTo(g.ExitTile));
            Assert.That(p.Revealed, Is.False);
            Assert.That(p.Kind, Is.Null, "type is drawn when it appears");
        }
    }

    [Test]
    public void Power_up_position_varies_between_attempts_but_the_layout_does_not()
    {
        var tiles = Enumerable.Range(1, 20)
            .Select(a => Game.ForStage(new StageSpec { Seed = 7 }, (ulong)a))
            .ToList();
        Assert.That(tiles.Select(g => g.PowerUps.Single().Tile).Distinct().Count(), Is.GreaterThan(5));
        Assert.That(tiles.Select(g => g.ExitTile).Distinct().Count(), Is.EqualTo(1));
    }

    // Bomber at (1,1); a power-up hidden under the soft block at (3,1); bomb from (2,1) uncovers it.
    private static Game HiddenPowerUp(out PowerUp p)
    {
        var g = At(1, 1,
            "#########",
            "#..+....#",
            "#.#.#.#.#",
            "#.......#",
            "#########");
        p = g.AddPowerUp(new TilePos(3, 1), kind: null);
        return g;
    }

    private static void UncoverFromTwoOne(Game g)
    {
        Wait(g, 10, Direction.Up);
        Wait(g, 5, Direction.Right);
        g.Step(Direction.None, placeBomb: true);
        Wait(g, 5, Direction.Left);
        Wait(g, 10, Direction.Down);
        // Detonate does nothing to normal bombs and sets off a remote one.
        while (g.Bombs.Count > 0 && g.Outcome == Outcome.Playing) g.Step(new BomberInput(Direction.None, detonate: true));
    }

    [Test]
    public void Power_up_appears_after_the_uncovering_fire_ends_so_that_blast_cannot_destroy_it()
    {
        var g = HiddenPowerUp(out var p);
        UncoverFromTwoOne(g);
        Assert.That(p.Revealed, Is.False);
        Assert.That(g.PowerUps, Has.Count.EqualTo(1), "survived the blast that uncovered it");
        Wait(g, Game.FireTicks);
        Assert.That(p.Revealed);
        Assert.That(p.Kind, Is.Not.Null);
    }

    [Test]
    public void Fire_destroys_a_power_up_that_is_showing()
    {
        var g = HiddenPowerUp(out var p);
        UncoverFromTwoOne(g);
        Wait(g, Game.FireTicks);
        UncoverFromTwoOne(g); // second blast reaches (3,1)
        Assert.That(g.PowerUps, Is.Empty);
    }

    [Test]
    public void Walking_onto_a_power_up_picks_it_up()
    {
        var g = HiddenPowerUp(out var p);
        UncoverFromTwoOne(g);
        Wait(g, Game.FireTicks);
        var kind = p.Kind!.Value;
        Wait(g, 10, Direction.Up);
        Wait(g, 10, Direction.Right);
        Assert.That(g.PowerUps, Is.Empty);
        Assert.That(g.Bomber.Loadout.IsEmpty, Is.False, $"now holds the {kind}");
    }

    [Test]
    public void Unusable_types_are_left_out_of_the_draw()
    {
        for (ulong seed = 1; seed <= 25; seed++)
        {
            var g = new Game(Arena.FromRows(
                "#########",
                "#..+....#",
                "#.#.#.#.#",
                "#.......#",
                "#########"), seed, new TilePos(1, 1));
            g.ApplyLoadout(0, new PowerUpLoadout { FireUp = true, BombUps = 3, RemoteControl = true });
            var p = g.AddPowerUp(new TilePos(3, 1), kind: null);
            UncoverFromTwoOne(g);
            Wait(g, Game.FireTicks);
            Assert.That(p.Kind, Is.EqualTo(PowerUpKind.SpeedUp), "the only usable type");
        }
    }

    [Test]
    public void Loadout_is_applied_at_the_start_and_read_back_at_the_end()
    {
        var g = At(1, 1);
        g.ApplyLoadout(0, new PowerUpLoadout { FireUp = true, BombUps = 2, RemoteControl = true, SpeedUpTicksLeft = 400 });
        var l = g.Bomber.Loadout;
        Assert.That((l.FireUp, l.BombUps, l.RemoteControl, l.SpeedUpTicksLeft), Is.EqualTo((true, 2, true, 400)));
        Assert.That(g.Bomber.MaxBombs, Is.EqualTo(3));
    }

    [Test]
    public void In_rounds_destroyed_soft_blocks_drop_power_ups_about_forty_percent_of_the_time()
    {
        int blocks = 0, drops = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var g = new Game(Arena.FromRows(
                "#######",
                "#..+..#",
                "#.###.#",
                "#.....#",
                "#######"), seed, new TilePos(2, 1)) { SoftBlockDropPercent = 40 };
            g.Step(Direction.None, placeBomb: true);
            Wait(g, 5, Direction.Left);
            Wait(g, 10, Direction.Down);
            while (g.Bombs.Count > 0 && g.Outcome == Outcome.Playing) g.Step(Direction.None);
            Wait(g, Game.FireTicks);
            blocks++;
            drops += g.PowerUps.Count(p => p.Revealed);
        }
        Assert.That(drops, Is.InRange(blocks * 2 / 10, blocks * 6 / 10));
    }
}
