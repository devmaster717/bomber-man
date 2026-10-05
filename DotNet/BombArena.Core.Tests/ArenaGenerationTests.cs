using System;
using System.Collections.Generic;
using System.Linq;
using BombArena.Core;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class ArenaGenerationTests
{
    private static IEnumerable<(int w, int h)> AllValidSizes()
    {
        for (int w = Arena.MinWidth; w <= Arena.MaxWidth; w += 2)
        for (int h = Arena.MinHeight; h <= Arena.MaxHeight; h += 2)
            yield return (w, h);
    }

    private static IEnumerable<TilePos> AllTiles(Arena a)
    {
        for (int y = 0; y < a.Height; y++)
        for (int x = 0; x < a.Width; x++)
            yield return new TilePos(x, y);
    }

    [Test]
    public void Border_and_even_even_tiles_are_hard_blocks_and_nothing_else_is()
    {
        foreach (var (w, h) in AllValidSizes())
        {
            var arena = Arena.Generate(w, h, seed: 42, playerCount: 3);
            foreach (var p in AllTiles(arena))
            {
                bool shouldBeHard = p.X == 0 || p.Y == 0 || p.X == w - 1 || p.Y == h - 1 || (p.X % 2 == 0 && p.Y % 2 == 0);
                Assert.That(arena[p] == Tile.HardBlock, Is.EqualTo(shouldBeHard), $"{w}x{h} at {p}");
            }
        }
    }

    [Test]
    public void Same_seed_gives_the_same_arena()
    {
        var a = Arena.Generate(23, 11, seed: 1234);
        var b = Arena.Generate(23, 11, seed: 1234);
        Assert.That(AllTiles(a).Select(p => a[p]), Is.EqualTo(AllTiles(b).Select(p => b[p])));
    }

    [Test]
    public void Different_seeds_give_different_soft_block_layouts()
    {
        var a = Arena.Generate(23, 11, seed: 1);
        var b = Arena.Generate(23, 11, seed: 2);
        Assert.That(AllTiles(a).Select(p => a[p]), Is.Not.EqualTo(AllTiles(b).Select(p => b[p])));
    }

    [Test]
    public void Generation_is_pinned_for_a_known_seed()
    {
        // Guards against accidental changes to the generator: stage files rely on seeds staying stable.
        var arena = Arena.Generate(19, 9, seed: 7);
        var rows = Enumerable.Range(0, arena.Height)
            .Select(y => new string(Enumerable.Range(0, arena.Width)
                .Select(x => arena[x, y] switch { Tile.HardBlock => '#', Tile.SoftBlock => '+', _ => '.' })
                .ToArray()))
            .ToArray();
        TestContext.Out.WriteLine(string.Join("\n", rows));
        Assert.That(rows, Is.EqualTo(PinnedSeed7));
    }

    private static readonly string[] PinnedSeed7 =
    {
        "###################",
        "#.....+.+.++++++..#",
        "#.#+#.#.#.#+#.#+#+#",
        "#..+.++++.++++++++#",
        "#.#+#+#+#+#+#+#+#.#",
        "#++..++.+.+.++++..#",
        "#+#+#.#.#+#.#+#.#+#",
        "#+++.....+.++.+.+.#",
        "###################",
    };

    [Test]
    public void Soft_blocks_fill_sixty_percent_of_the_free_tiles()
    {
        var arena = Arena.Generate(23, 11, seed: 99);
        int notHard = AllTiles(arena).Count(p => arena[p] != Tile.HardBlock);
        int clear = 3; // single-player spawn (1,1) and its two open neighbours
        int soft = AllTiles(arena).Count(p => arena[p] == Tile.SoftBlock);
        Assert.That(soft, Is.EqualTo(((notHard - clear) * 60 + 50) / 100));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void Each_spawn_and_its_open_neighbours_stay_clear(int players)
    {
        foreach (var (w, h) in AllValidSizes())
        foreach (ulong seed in new ulong[] { 1, 2, 3, 99, 12345 })
        {
            var arena = Arena.Generate(w, h, seed, players);
            foreach (var s in Arena.SpawnTiles(w, h, players))
            {
                Assert.That(arena[s], Is.EqualTo(Tile.Floor), $"{w}x{h} seed {seed} spawn {s}");
                foreach (var n in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Select(d => new TilePos(s.X + d.Item1, s.Y + d.Item2)))
                    Assert.That(arena[n], Is.Not.EqualTo(Tile.SoftBlock), $"{w}x{h} seed {seed} next to {s} at {n}");
            }
        }
    }

    [Test]
    public void Spawn_positions_follow_the_spec()
    {
        Assert.That(Arena.SpawnTiles(23, 11, 1), Is.EqualTo(new[] { new TilePos(1, 1) }));
        Assert.That(Arena.SpawnTiles(23, 11, 2), Is.EqualTo(new[] { new TilePos(1, 1), new TilePos(21, 9) }));
        Assert.That(Arena.SpawnTiles(23, 11, 3), Is.EqualTo(new[] { new TilePos(1, 1), new TilePos(21, 9), new TilePos(11, 5) }));
    }

    [Test]
    public void Centre_spawn_moves_off_a_pillar()
    {
        // 25 x 13: the exact centre (12, 6) is a pillar, so the spawn moves one tile left.
        Assert.That(Arena.SpawnTiles(25, 13, 3)[2], Is.EqualTo(new TilePos(11, 6)));
        foreach (var (w, h) in AllValidSizes())
        {
            var centre = Arena.SpawnTiles(w, h, 3)[2];
            Assert.That(Arena.IsHardByLayout(centre.X, centre.Y, w, h), Is.False, $"{w}x{h} centre {centre}");
        }
    }

    [TestCase(22, 11)]
    [TestCase(23, 10)]
    [TestCase(17, 11)]
    [TestCase(29, 11)]
    [TestCase(23, 7)]
    [TestCase(23, 17)]
    public void Invalid_sizes_are_rejected(int w, int h)
    {
        Assert.Throws<ArgumentException>(() => Arena.Generate(w, h, seed: 1));
    }

    [Test]
    public void Default_size_is_23_by_11()
    {
        Assert.That((Arena.DefaultWidth, Arena.DefaultHeight), Is.EqualTo((23, 11)));
    }
}
