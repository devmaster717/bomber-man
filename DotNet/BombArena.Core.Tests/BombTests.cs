using System.Linq;
using BombArena.Core;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class BombTests
{
    private const int T = Units.PerTile;

    private static Game Play(int x, int y, params string[] rows) => new Game(Arena.FromRows(rows), new TilePos(x, y));

    private static void Wait(Game g, int ticks, Direction move = Direction.None)
    {
        for (int i = 0; i < ticks; i++) g.Step(move);
    }

    private static readonly string[] Corridor =
    {
        "#########",
        "#.......#",
        "#.#.#.#.#",
        "#.......#",
        "#########",
    };

    [Test]
    public void Bomb_is_placed_on_the_bombers_tile()
    {
        var g = Play(3, 1, Corridor);
        g.Step(Direction.None, placeBomb: true);
        Assert.That(g.Bombs.Single().Tile, Is.EqualTo(new TilePos(3, 1)));
    }

    [Test]
    public void Only_one_bomb_by_default_and_never_two_on_a_tile()
    {
        var g = Play(1, 1, Corridor);
        g.Step(Direction.None, placeBomb: true);
        g.Step(Direction.None, placeBomb: true);
        Assert.That(g.Bombs, Has.Count.EqualTo(1));
    }

    [Test]
    public void Bomb_explodes_exactly_two_and_a_half_seconds_after_placement()
    {
        var g = Play(1, 1, Corridor);
        g.Step(Direction.None, placeBomb: true); // placed this tick, fuse starts counting
        Wait(g, Bomb.FuseTicks - 2, Direction.Right);
        Assert.That(g.Bombs, Has.Count.EqualTo(1), "still ticking one tick before the end");
        g.Step(Direction.Right);
        Assert.That(g.Bombs, Is.Empty);
        Assert.That(g.IsBurning(1, 1));
        Assert.That(Bomb.FuseTicks, Is.EqualTo(50));
    }

    [Test]
    public void Owner_can_walk_off_its_bomb_but_not_back_on()
    {
        var g = Play(1, 1, Corridor);
        g.Step(Direction.None, placeBomb: true);
        Wait(g, 2, Direction.Right); // 0.4 tile off: still overlapping, may come back
        Wait(g, 1, Direction.Left);
        Assert.That(g.Bomber.X, Is.EqualTo(1 * T + 200), "allowed back while still on the bomb");
        Wait(g, 4, Direction.Right); // fully off at x = 2.0
        Assert.That(g.Bomber.X, Is.EqualTo(2 * T));
        Wait(g, 3, Direction.Left);
        Assert.That(g.Bomber.X, Is.EqualTo(2 * T), "the bomb is solid now");
    }

    [Test]
    public void Fire_is_a_plus_of_range_one_and_stops_before_hard_blocks()
    {
        var g = Play(3, 1, Corridor);
        g.Step(Direction.None, placeBomb: true);
        Wait(g, 4, Direction.Down); // partway down column 3; only the fire's shape is checked here
        Wait(g, Bomb.FuseTicks - 5);
        var burning = Enumerable.Range(0, 5).SelectMany(y => Enumerable.Range(0, 9).Select(x => new TilePos(x, y)))
            .Where(g.IsBurning).ToHashSet();
        Assert.That(burning, Is.EquivalentTo(new[] { new TilePos(3, 1), new TilePos(2, 1), new TilePos(4, 1), new TilePos(3, 2) }));
    }

    [Test]
    public void Soft_block_is_destroyed_and_stops_the_fire()
    {
        var g = Play(1, 1,
            "#######",
            "#..+..#",
            "#.#####",
            "#.....#",
            "#######");
        // Range is 1 by default, so place the bomb next to the soft block and leave.
        Wait(g, 5, Direction.Right); // (2,1)
        g.Step(Direction.None, placeBomb: true);
        Wait(g, 5, Direction.Left);
        Wait(g, 10, Direction.Down);
        Wait(g, Bomb.FuseTicks);
        Assert.That(g.Arena[3, 1], Is.EqualTo(Tile.Floor));
        Assert.That(g.Outcome, Is.EqualTo(Outcome.Playing));
    }

    [Test]
    public void A_bomb_reached_by_fire_explodes_in_the_same_tick()
    {
        var g = new Game(Arena.FromRows(Corridor), new TilePos(3, 1), new TilePos(5, 1));
        g.Step(new BomberInput(Direction.None, placeBomb: true), BomberInput.None);  // A at (3,1), tick 1
        for (int i = 0; i < 10; i++)                                                  // bomber 0 hides at (3,3)
            g.Step(new BomberInput(Direction.Down), new BomberInput(i < 5 ? Direction.Left : Direction.None));
        g.Step(BomberInput.None, new BomberInput(Direction.None, placeBomb: true));   // B at (4,1), tick 12
        for (int i = 0; i < 15; i++)                                                  // bomber 1 hides at (5,3)
            g.Step(BomberInput.None, new BomberInput(i < 5 ? Direction.Right : Direction.Down));
        Assert.That(g.Bombs.Select(b => b.Tile), Is.EquivalentTo(new[] { new TilePos(3, 1), new TilePos(4, 1) }));

        while (g.Tick < Bomb.FuseTicks) g.Step(BomberInput.None, BomberInput.None);

        Assert.That(g.Bombs, Is.Empty, "B went off with A although its own fuse had 11 ticks left");
        Assert.That(g.IsBurning(5, 1), "B's own blast happened");
        Assert.That(g.Bombers.All(b => b.Alive));
    }

    [Test]
    public void Walking_into_fire_kills_and_ends_the_attempt()
    {
        var g = Play(1, 1, Corridor);
        g.Step(Direction.None, placeBomb: true);
        Wait(g, Bomb.FuseTicks - 1);
        Assert.That(g.Bomber.Alive, Is.False, "standing on its own bomb when it explodes");
        Assert.That(g.Outcome, Is.EqualTo(Outcome.Failed));
    }

    [Test]
    public void Fire_stays_deadly_for_half_a_second()
    {
        var arena = Arena.FromRows(
            "#########",
            "#.......#",
            "#########");
        var g = new Game(arena, new TilePos(1, 1), new TilePos(7, 1));
        g.Step(new BomberInput(Direction.None, placeBomb: true), BomberInput.None);
        for (int i = 0; i < 5; i++) g.Step(new BomberInput(Direction.Right), BomberInput.None); // bomber 0 to (2,1)
        for (int i = 0; i < 5; i++) g.Step(new BomberInput(Direction.Right), BomberInput.None); // (3,1)
        while (g.Bombs.Count > 0 && g.Outcome == Outcome.Playing) g.Step(BomberInput.None, BomberInput.None);
        Assert.That(g.IsBurning(2, 1));
        for (int i = 0; i < Game.FireTicks - 1; i++) g.Step(BomberInput.None, BomberInput.None);
        Assert.That(g.IsBurning(2, 1), Is.False, "fire is gone after 0.5 s");
        Assert.That(Game.FireTicks, Is.EqualTo(10));
    }

    [Test]
    public void Bomber_entering_a_burning_tile_dies()
    {
        var arena = Arena.FromRows(
            "#########",
            "#.......#",
            "#########");
        var g = new Game(arena, new TilePos(1, 1), new TilePos(7, 1));
        g.Step(new BomberInput(Direction.None, placeBomb: true), BomberInput.None);
        for (int i = 0; i < 10; i++) g.Step(new BomberInput(Direction.Right), BomberInput.None); // to (3,1)
        while (g.Bombs.Count > 0 && g.Outcome == Outcome.Playing) g.Step(BomberInput.None, BomberInput.None);
        Assert.That(g.Bomber.Alive, "out of range at (3,1)");
        g.Step(new BomberInput(Direction.Left), BomberInput.None); // its edge now overlaps (2,1), still burning
        Assert.That(g.Bomber.Alive, "only the edge of its body is in the flames");
        for (int i = 0; i < 3; i++) g.Step(new BomberInput(Direction.Left), BomberInput.None); // its middle reaches (2,1)
        Assert.That(g.IsBurning(new TilePos(2, 1)), "the fire is still burning");
        Assert.That(g.Bomber.Alive, Is.False);
    }

    [Test]
    public void Same_inputs_give_the_same_result_with_bombs()
    {
        string Run()
        {
            var g = Game.Create(23, 11, seed: 9);
            var rng = new Rng(3);
            for (int i = 0; i < 600 && g.Outcome == Outcome.Playing; i++)
                g.Step((Direction)rng.Next(5), placeBomb: rng.Next(10) == 0);
            return $"{g.Tick} {g.Outcome} {g.Bomber.X},{g.Bomber.Y} " +
                   string.Concat(Enumerable.Range(0, 11 * 23).Select(i => (int)g.Arena[i % 23, i / 23]));
        }

        Assert.That(Run(), Is.EqualTo(Run()));
    }
}
