using System.Collections.Generic;
using System.Linq;
using BombArena.Core;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class SpecialEnemyTests
{
    private const int T = Units.PerTile;

    private static readonly string[] Open =
    {
        "###############",
        "#.............#",
        "#.#.#.#.#.#.#.#",
        "#.............#",
        "#.#.#.#.#.#.#.#",
        "#.............#",
        "#.#.#.#.#.#.#.#",
        "#.............#",
        "###############",
    };

    private static void Wait(Game g, int ticks)
    {
        for (int i = 0; i < ticks; i++) g.Step(Direction.None);
    }

    [Test]
    public void Phantom_and_Wall_passer_move_at_three_tiles_per_second()
    {
        Assert.That(Enemy.TilesPerSecond(EnemyKind.Phantom), Is.EqualTo(3));
        Assert.That(Enemy.TilesPerSecond(EnemyKind.WallPasser), Is.EqualTo(3));
    }

    [Test]
    public void Phantom_fades_out_where_it_stands_then_fades_in_elsewhere()
    {
        var g = new Game(Arena.FromRows(Open), 3UL, new TilePos(1, 1));
        var p = g.AddEnemy(EnemyKind.Phantom, new TilePos(13, 7));
        int firstFade = -1;
        for (int i = 0; i < 200 && firstFade < 0; i++)
        {
            g.Step(Direction.None);
            if (p.Phase == PhantomPhase.FadingOut) firstFade = i + 1;
        }
        Assert.That(firstFade, Is.InRange(4 * Units.TicksPerSecond, 6 * Units.TicksPerSecond), "every 4–6 s");

        var fadeSpot = (p.X, p.Y);
        while (p.Phase == PhantomPhase.FadingOut)
        {
            g.Step(Direction.None);
            Assert.That((p.X, p.Y), Is.EqualTo(fadeSpot), "does not move while fading out");
        }
        Assert.That(p.Phase, Is.EqualTo(PhantomPhase.Vanished));
        Wait(g, Game.PhantomVanishedTicks);
        Assert.That(p.Phase, Is.EqualTo(PhantomPhase.FadingIn));
        var landed = (p.X, p.Y);
        Assert.That(p.AtTileCentre);
        while (p.Phase == PhantomPhase.FadingIn)
        {
            Assert.That((p.X, p.Y), Is.EqualTo(landed), "does not move while fading in");
            g.Step(Direction.None);
        }
        Assert.That(p.Phase, Is.EqualTo(PhantomPhase.Visible));
    }

    [Test]
    public void Fades_last_half_a_second_to_a_second()
    {
        var g = new Game(Arena.FromRows(Open), 9UL, new TilePos(1, 1));
        var p = g.AddEnemy(EnemyKind.Phantom, new TilePos(13, 7));
        var lengths = new List<int>();
        for (int i = 0; i < 3000; i++)
        {
            var before = p.Phase;
            g.Step(Direction.None);
            if (p.Phase != before && (p.Phase == PhantomPhase.FadingOut || p.Phase == PhantomPhase.FadingIn))
                lengths.Add(p.PhaseLength);
        }
        Assert.That(lengths, Is.Not.Empty);
        Assert.That(lengths, Is.All.InRange(Units.TicksPerSecond / 2, Units.TicksPerSecond));
    }

    [Test]
    public void Phantom_reappears_on_empty_floor_at_least_three_tiles_from_the_bomber()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var g = new Game(Arena.FromRows(Open), seed, new TilePos(7, 3));
            var p = g.AddEnemy(EnemyKind.Phantom, new TilePos(1, 7));
            for (int i = 0; i < 400; i++)
            {
                var before = p.Phase;
                g.Step(Direction.None);
                if (!g.Bomber.Alive) break;
                if (before == PhantomPhase.Vanished && p.Phase == PhantomPhase.FadingIn)
                {
                    Assert.That(g.Arena[p.Tile], Is.EqualTo(Tile.Floor));
                    int d = System.Math.Abs(p.Tile.X - g.Bomber.Tile.X) + System.Math.Abs(p.Tile.Y - g.Bomber.Tile.Y);
                    Assert.That(d, Is.GreaterThanOrEqualTo(Game.PhantomMinDistanceFromBomber), $"seed {seed}");
                }
            }
        }
    }

    [Test]
    public void Vanished_phantom_cannot_be_harmed_or_touched_but_a_fading_one_can()
    {
        var g = new Game(Arena.FromRows(Open), 1UL, new TilePos(1, 1));
        var p = g.AddEnemy(EnemyKind.Phantom, new TilePos(3, 1));
        Assert.That(p.Present);

        p.Phase = PhantomPhase.Vanished; p.PhaseTicksLeft = 1000; p.PhaseLength = 1000;
        p.X = 1 * T; p.Y = 1 * T; // right on top of the bomber
        g.Step(Direction.None);
        Assert.That(g.Bomber.Alive, "a vanished Phantom does not kill on touch");

        var g2 = new Game(Arena.FromRows(Open), 1UL, new TilePos(1, 1));
        var p2 = g2.AddEnemy(EnemyKind.Phantom, new TilePos(1, 3));
        p2.Phase = PhantomPhase.FadingOut; p2.PhaseTicksLeft = 1000; p2.PhaseLength = 1000;
        Wait(g2, 1);
        p2.X = 1 * T; p2.Y = 1 * T + 400;
        g2.Step(Direction.None);
        Assert.That(g2.Bomber.Alive, Is.False, "a fading Phantom kills on touch");
    }

    [Test]
    public void Fire_cannot_reach_a_vanished_phantom_but_kills_a_fading_one()
    {
        foreach (var phase in new[] { PhantomPhase.Vanished, PhantomPhase.FadingOut })
        {
            var g = new Game(Arena.FromRows(Open), 1UL, new TilePos(3, 1));
            g.Step(Direction.None, placeBomb: true);
            for (int i = 0; i < 10; i++) g.Step(Direction.Down); // bomber hides at (3,3)
            var p = g.AddEnemy(EnemyKind.Phantom, new TilePos(4, 1));
            p.Phase = phase; p.PhaseTicksLeft = 1000; p.PhaseLength = 1000;
            while (g.Bombs.Count > 0 && g.Outcome == Outcome.Playing) g.Step(Direction.None);
            Assert.That(p.Alive, Is.EqualTo(phase == PhantomPhase.Vanished), phase.ToString());
        }
    }

    [Test]
    public void Wall_passer_moves_through_soft_blocks_but_not_hard_blocks_or_bombs()
    {
        var g = new Game(Arena.FromRows(
            "#########",
            "#.+++++.#",
            "#########"), new TilePos(7, 1));
        var w = g.AddEnemy(EnemyKind.WallPasser, new TilePos(1, 1));
        var seen = new HashSet<int>();
        for (int i = 0; i < 40; i++) { g.Step(Direction.None); seen.Add(w.Tile.X); if (!g.Bomber.Alive) break; }
        Assert.That(seen, Does.Contain(4), "went through the soft blocks");

        Assert.That(g.IsOpenForEnemy(w, 0, 1), Is.False, "hard block");
        var walker = new Enemy(EnemyKind.Walker, new TilePos(1, 1));
        Assert.That(g.IsOpenForEnemy(walker, 3, 1), Is.False, "Walkers still cannot");
    }

    [Test]
    public void Wall_passer_inside_a_soft_block_survives_the_blast_that_destroys_it()
    {
        var g = new Game(Arena.FromRows(
            "#######",
            "#..+..#",
            "#.###.#",
            "#.....#",
            "#######"), new TilePos(2, 1));
        var w = g.AddEnemy(EnemyKind.WallPasser, new TilePos(3, 1));
        w.Heading = Direction.None;
        g.Step(Direction.None, placeBomb: true);
        for (int i = 0; i < 5; i++) g.Step(Direction.Left);
        for (int i = 0; i < 10; i++) g.Step(Direction.Down); // bomber hides at (1,3)
        while (g.Bombs.Count > 0 && g.Outcome == Outcome.Playing)
        {
            w.X = 3 * T; w.Y = 1 * T; w.Heading = Direction.None; // keep it inside the block
            g.Step(Direction.None);
        }
        Assert.That(g.Arena[3, 1], Is.EqualTo(Tile.Floor), "block destroyed");
        Assert.That(w.Alive, "survived inside the soft block");
    }

    [Test]
    public void Wall_passer_on_open_floor_is_killed_by_fire()
    {
        var g = new Game(Arena.FromRows(Open), new TilePos(3, 1));
        g.Step(Direction.None, placeBomb: true);
        for (int i = 0; i < 10; i++) g.Step(Direction.Down);
        var w = g.AddEnemy(EnemyKind.WallPasser, new TilePos(4, 1));
        while (g.Bombs.Count > 0 && g.Outcome == Outcome.Playing)
        {
            w.X = 4 * T; w.Y = 1 * T; w.Heading = Direction.None;
            g.Step(Direction.None);
        }
        Assert.That(w.Alive, Is.False);
    }

    [Test]
    public void Same_inputs_give_the_same_result_with_all_enemy_kinds()
    {
        string Run()
        {
            var g = Game.ForStage(new StageSpec
            {
                Seed = 21, SoftBlockPercent = 40,
                Enemies = { new EnemyGroup(EnemyKind.Walker, 2), new EnemyGroup(EnemyKind.Phantom, 2), new EnemyGroup(EnemyKind.WallPasser, 2) },
            });
            var rng = new Rng(4);
            for (int i = 0; i < 1500 && g.Outcome == Outcome.Playing; i++)
                g.Step((Direction)rng.Next(5), placeBomb: rng.Next(15) == 0);
            return $"{g.Tick} {g.Outcome} " + string.Join(";", g.Enemies.Select(e => $"{e.Kind}:{e.X},{e.Y},{e.Alive},{e.Phase}"));
        }

        Assert.That(Run(), Is.EqualTo(Run()));
    }
}
