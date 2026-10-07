using System.Collections.Generic;
using System.Linq;
using BombArena.Core;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class EnemyTests
{
    private const int T = Units.PerTile;

    private static readonly string[] Row =
    {
        "###########",
        "#.........#",
        "###########",
    };

    private static readonly string[] Grid =
    {
        "#########",
        "#.......#",
        "#.#.#.#.#",
        "#.......#",
        "#.#.#.#.#",
        "#.......#",
        "#########",
    };

    [Test]
    public void Walker_moves_three_tiles_per_second()
    {
        var g = new Game(Arena.FromRows(Row), new TilePos(9, 1));
        var walker = g.AddEnemy(EnemyKind.Walker, new TilePos(1, 1));
        for (int i = 0; i < Units.TicksPerSecond; i++) g.Step(Direction.None);
        Assert.That((walker.X, walker.Y), Is.EqualTo((4 * T, 1 * T)), "the only way out of (1,1) is right");
    }

    [Test]
    public void Walker_keeps_going_and_only_reverses_at_a_dead_end()
    {
        var g = new Game(Arena.FromRows(
            "############",
            "#..........#",
            "############"), new TilePos(10, 1));
        var walker = g.AddEnemy(EnemyKind.Walker, new TilePos(2, 1));
        walker.Heading = Direction.Left;
        var xs = new List<int>();
        for (int i = 0; i < 80; i++) { g.Step(Direction.None); xs.Add(walker.X); }
        // Goes left to the wall at x = 1, then turns and heads right all the way.
        int turn = xs.IndexOf(xs.Min());
        Assert.That(xs.Min(), Is.LessThan(1 * T + walker.SpeedPerTick), "reached the wall within a tick");
        Assert.That(xs.Skip(turn).Zip(xs.Skip(turn + 1), (a, b) => b >= a).All(x => x), "no reversal mid-corridor");
    }

    [Test]
    public void Walker_never_reverses_at_a_junction()
    {
        var arena = Arena.FromRows(Grid);
        var walker = new Enemy(EnemyKind.Walker, new TilePos(1, 1));
        var rng = new Rng(7);
        var visited = new List<TilePos> { walker.Tile };
        for (int i = 0; i < 3000; i++)
        {
            walker.Move(arena.IsWalkable, rng);
            if (walker.Tile != visited[^1]) visited.Add(walker.Tile);
        }

        int Exits(TilePos t) => new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Count(d => arena.IsWalkable(t.X + d.Item1, t.Y + d.Item2));
        for (int i = 2; i < visited.Count; i++)
            if (visited[i] == visited[i - 2])
                Assert.That(Exits(visited[i - 1]), Is.EqualTo(1), $"turned back at {visited[i - 1]}, which is not a dead end");

        var junctionsVisited = visited.Where(t => Exits(t) >= 3).Distinct().Count();
        Assert.That(junctionsVisited, Is.GreaterThan(5), "wanders through many junctions");
    }

    [Test]
    public void Enemies_cannot_pass_soft_blocks_hard_blocks_or_bombs()
    {
        var g = new Game(Arena.FromRows(
            "#########",
            "#...+...#",
            "#########"), new TilePos(7, 1));
        var walker = g.AddEnemy(EnemyKind.Walker, new TilePos(1, 1));
        for (int i = 0; i < 200; i++) g.Step(Direction.None);
        Assert.That(walker.X, Is.InRange(1 * T, 3 * T), "stays left of the soft block");

        var g2 = new Game(Arena.FromRows(Row), new TilePos(9, 1));
        var w2 = g2.AddEnemy(EnemyKind.Walker, new TilePos(1, 1));
        g2.Step(Direction.None, placeBomb: true); // bomb at (9,1) under the bomber
        for (int i = 0; i < 40; i++) g2.Step(Direction.None);
        Assert.That(w2.X, Is.LessThan(9 * T), "turned back before the bomb");
    }

    [Test]
    public void Walker_turns_back_when_a_bomb_appears_ahead()
    {
        var enemyOpen = new HashSet<TilePos>(Enumerable.Range(1, 5).Select(x => new TilePos(x, 1)));
        var walker = new Enemy(EnemyKind.Walker, new TilePos(1, 1)) { Heading = Direction.Right };
        var rng = new Rng(1);
        walker.Move((x, y) => enemyOpen.Contains(new TilePos(x, y)), rng); // now between (1,1) and (2,1)
        Assert.That(walker.X, Is.EqualTo(1 * T + 150));
        enemyOpen.Remove(new TilePos(2, 1)); // a bomb lands on (2,1)
        walker.Move((x, y) => enemyOpen.Contains(new TilePos(x, y)), rng);
        Assert.That(walker.Heading, Is.EqualTo(Direction.Left));
        Assert.That(walker.X, Is.EqualTo(1 * T));
    }

    [Test]
    public void Fire_kills_enemies()
    {
        var g = new Game(Arena.FromRows(Grid), new TilePos(1, 1));
        var walker = g.AddEnemy(EnemyKind.Walker, new TilePos(3, 1));
        walker.Heading = Direction.Right;
        g.Step(Direction.None, placeBomb: true);
        for (int i = 0; i < 10; i++) g.Step(Direction.Down); // bomber escapes to (1,3)
        // Keep the walker in range: put it back next to the bomb just before the blast.
        while (g.Bombs.Count > 0 && g.Outcome == Outcome.Playing)
        {
            walker.X = 2 * T; walker.Y = 1 * T; walker.Heading = Direction.None;
            g.Step(Direction.None);
        }
        Assert.That(walker.Alive, Is.False);
        Assert.That(g.EnemiesRemaining, Is.Zero);
    }

    // Places a range-1 bomb at (1,1), takes the bomber to safety at (1,3) and holds the walker at (3,1), heading for
    // the bomb, until it goes off: the fire then covers (1,1) and (2,1), one tile short of the walker.
    private static (Game g, Enemy walker) WalkerJustOutsideABlast()
    {
        var g = new Game(Arena.FromRows(Grid), new TilePos(1, 1));
        var walker = g.AddEnemy(EnemyKind.Walker, new TilePos(3, 1));
        g.Step(Direction.None, placeBomb: true);
        for (int i = 0; i < 10; i++) g.Step(Direction.Down);
        while (g.Bombs.Count > 0 && g.Outcome == Outcome.Playing)
        {
            walker.X = 3 * T; walker.Y = 1 * T; walker.Heading = Direction.Left;
            g.Step(Direction.None);
        }
        Assert.That(g.IsBurning(new TilePos(2, 1)), Is.True);
        Assert.That(walker.Alive, Is.True, "out of range when the bomb went off");
        return (g, walker);
    }

    [Test]
    public void An_enemy_out_of_range_survives_the_blast_but_dies_walking_into_the_flames()
    {
        var (g, walker) = WalkerJustOutsideABlast();
        // Like the bomber, an enemy doesn't avoid fire: it keeps walking into the still-burning tile and dies there.
        for (int i = 0; i < Game.FireTicks && walker.Alive; i++) g.Step(Direction.None);
        Assert.That(walker.Alive, Is.False);
    }

    [Test]
    public void Only_an_enemy_whose_middle_is_in_the_flames_is_hit()
    {
        var (g, walker) = WalkerJustOutsideABlast();
        // Walking away with its edge over the burning (2,1) but its middle on (3,1): not hit.
        walker.X = 3 * T - 400; walker.Heading = Direction.Right;
        g.Step(Direction.None);
        Assert.That(walker.HitboxOverlaps(new TilePos(2, 1)), Is.True);
        Assert.That(walker.Alive, Is.True);
    }

    [Test]
    public void Touching_an_enemy_kills_the_bomber()
    {
        var g = new Game(Arena.FromRows(Row), new TilePos(5, 1));
        g.AddEnemy(EnemyKind.Walker, new TilePos(1, 1));
        int ticks = 0;
        while (g.Bomber.Alive && ticks++ < 100) g.Step(Direction.None);
        Assert.That(g.Bomber.Alive, Is.False);
        Assert.That(g.Outcome, Is.EqualTo(Outcome.Failed));
        // Hitboxes are 0.6 tile wide, so contact happens once centres are less than 0.6 tile apart.
        var walker = g.Enemies[0];
        Assert.That(System.Math.Abs(walker.X - g.Bomber.X), Is.LessThan(600));
    }

    [Test]
    public void Enemies_start_on_floor_at_least_five_tiles_from_the_spawn()
    {
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5 })
        {
            var g = Game.Create(23, 11, seed, walkers: 4);
            Assert.That(g.Enemies, Has.Count.EqualTo(4));
            foreach (var e in g.Enemies)
            {
                Assert.That(g.Arena[e.Tile], Is.EqualTo(Tile.Floor));
                Assert.That(e.Tile.X - 1 + e.Tile.Y - 1, Is.GreaterThanOrEqualTo(Game.EnemySpawnDistance));
            }
            Assert.That(g.Enemies.Select(e => e.Tile).Distinct().Count(), Is.EqualTo(4));
        }
    }

    [Test]
    public void Enemy_placement_is_the_same_on_every_attempt()
    {
        var a = Game.Create(23, 11, 42, walkers: 4).Enemies.Select(e => e.Tile);
        var b = Game.Create(23, 11, 42, walkers: 4).Enemies.Select(e => e.Tile);
        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void Same_inputs_give_the_same_result_with_enemies()
    {
        string Run()
        {
            var g = Game.Create(23, 11, seed: 5, softBlockPercent: 30, walkers: 4);
            var rng = new Rng(8);
            for (int i = 0; i < 800 && g.Outcome == Outcome.Playing; i++)
                g.Step((Direction)rng.Next(5), placeBomb: rng.Next(12) == 0);
            return $"{g.Tick} {g.Outcome} " + string.Join(";", g.Enemies.Select(e => $"{e.X},{e.Y},{e.Alive}"));
        }

        Assert.That(Run(), Is.EqualTo(Run()));
    }
}
