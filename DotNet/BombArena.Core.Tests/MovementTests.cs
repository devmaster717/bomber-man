using System.Linq;
using BombArena.Core;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class MovementTests
{
    private const int T = Units.PerTile;

    // A small open test arena: corridors on odd rows/columns, pillars on even/even.
    private static Arena Open() => Arena.FromRows(
        "#########",
        "#.......#",
        "#.#.#.#.#",
        "#.......#",
        "#.#.#.#.#",
        "#.......#",
        "#########");

    private static Game At(Arena arena, int x, int y) => new Game(arena, new TilePos(x, y));

    private static void Run(Game g, Direction d, int ticks)
    {
        for (int i = 0; i < ticks; i++) g.Step(d);
    }

    [Test]
    public void Bomber_moves_four_tiles_per_second()
    {
        var g = At(Open(), 1, 1);
        Run(g, Direction.Right, Units.TicksPerSecond);
        Assert.That((g.Bomber.X, g.Bomber.Y), Is.EqualTo((5 * T, 1 * T)));
    }

    [Test]
    public void Movement_is_smooth_between_tiles()
    {
        var g = At(Open(), 1, 1);
        g.Step(Direction.Right);
        Assert.That(g.Bomber.X, Is.EqualTo(1 * T + T / 5));
    }

    [Test]
    public void Hard_block_stops_the_bomber_at_the_tile_centre()
    {
        var g = At(Open(), 1, 1);
        Run(g, Direction.Up, 10);
        Assert.That((g.Bomber.X, g.Bomber.Y), Is.EqualTo((1 * T, 1 * T)));

        Run(g, Direction.Right, 100);
        Assert.That(g.Bomber.X, Is.EqualTo(7 * T), "stops before the right border");
    }

    [Test]
    public void Soft_block_stops_the_bomber()
    {
        var arena = Arena.FromRows(
            "#######",
            "#..+..#",
            "#######");
        var g = At(arena, 1, 1);
        Run(g, Direction.Right, 20);
        Assert.That(g.Bomber.X, Is.EqualTo(2 * T));
    }

    [Test]
    public void Cannot_turn_into_a_pillar()
    {
        var g = At(Open(), 1, 1);
        Run(g, Direction.Right, 5); // now centred on (2,1); below is a pillar at (2,2)
        Assert.That(g.Bomber.X, Is.EqualTo(2 * T));
        Run(g, Direction.Down, 5);
        Assert.That((g.Bomber.X, g.Bomber.Y), Is.EqualTo((2 * T, 1 * T)));
    }

    [Test]
    public void Corner_assist_nudges_onto_the_lane_when_close()
    {
        // 200 units past column 3 (inside the 35% assist window), turning down into the corridor at x = 3.
        var g = At(Open(), 1, 1);
        Run(g, Direction.Right, 11); // 1 + 11 × 0.2 = 3.2 tiles
        Assert.That(g.Bomber.X, Is.EqualTo(3 * T + 200));
        g.Step(Direction.Down);
        Assert.That((g.Bomber.X, g.Bomber.Y), Is.EqualTo((3 * T, 1 * T)), "first tick spends the budget nudging onto the lane");
        g.Step(Direction.Down);
        Assert.That((g.Bomber.X, g.Bomber.Y), Is.EqualTo((3 * T, 1 * T + 200)));
    }

    [Test]
    public void Corner_assist_works_from_either_side_of_the_lane()
    {
        var g = At(Open(), 1, 1);
        Run(g, Direction.Right, 9); // x = 2.8 tiles: 200 units short of column 3
        Assert.That(g.Bomber.X, Is.EqualTo(3 * T - 200));
        g.Step(Direction.Down);     // nearest lane is column 3: nudged right onto it
        Assert.That((g.Bomber.X, g.Bomber.Y), Is.EqualTo((3 * T, 1 * T)));
    }

    [Test]
    public void No_corner_assist_beyond_35_percent()
    {
        var g = At(Open(), 1, 1);
        Run(g, Direction.Right, 7); // x = 2.4 tiles: nearest lane is column 2 (a pillar below), 400 from column 3
        Assert.That(g.Bomber.X, Is.EqualTo(2 * T + 400));
        Run(g, Direction.Down, 5);
        Assert.That((g.Bomber.X, g.Bomber.Y), Is.EqualTo((2 * T + 400, 1 * T)), "too far from any open lane to turn");
    }

    [Test]
    public void Same_inputs_give_the_same_result()
    {
        var inputs = Enumerable.Range(0, 400)
            .Select(i => (Direction)(1 + (i * 7 + i / 13) % 4))
            .ToArray();

        (int, int)[] Play()
        {
            var g = Game.Create(23, 11, seed: 2026);
            return inputs.Select(d => { g.Step(d); return (g.Bomber.X, g.Bomber.Y); }).ToArray();
        }

        Assert.That(Play(), Is.EqualTo(Play()));
    }

    [Test]
    public void Bomber_never_overlaps_a_blocked_tile()
    {
        var g = Game.Create(23, 11, seed: 77);
        var rng = new Rng(5);
        for (int i = 0; i < 2000; i++)
        {
            g.Step((Direction)(1 + rng.Next(4)));
            int x0 = g.Bomber.X / T, x1 = (g.Bomber.X + T - 1) / T;
            int y0 = g.Bomber.Y / T, y1 = (g.Bomber.Y + T - 1) / T;
            for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                Assert.That(g.Arena.IsWalkable(x, y), $"tick {i}: bomber at ({g.Bomber.X}, {g.Bomber.Y}) overlaps ({x}, {y})");
        }
    }
}
