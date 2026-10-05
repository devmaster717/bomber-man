using System.Collections.Generic;
using BombArena.Core;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class GameEventTests
{
    [Test]
    public void Bomb_placed_exploded_and_bomber_died_are_announced_in_order()
    {
        var g = new Game(Arena.FromRows(
            "#######",
            "#.....#",
            "#######"), new TilePos(1, 1));
        var log = new List<string>();
        g.BombPlaced += b => log.Add($"placed {b.Tile}");
        g.BombExploded += b => log.Add($"exploded {b.Tile}");
        g.BomberDied += b => log.Add($"died {b.Index}");
        g.Step(Direction.None, placeBomb: true);
        g.Step(Direction.None, placeBomb: true); // refused: no second event
        for (int i = 0; i < Bomb.FuseTicks; i++) g.Step(Direction.None);
        Assert.That(log, Is.EqualTo(new[] { "placed (1, 1)", "exploded (1, 1)", "died 0" }));
    }
}
