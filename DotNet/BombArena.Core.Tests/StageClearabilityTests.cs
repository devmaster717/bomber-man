using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BombArena.Core;
using NUnit.Framework;

namespace BombArena.Core.Tests;

/// <summary>
/// Checks every shipped stage file can be cleared: nothing the player needs is walled off, the bomber can escape its
/// first bomb, the exit and the power-up are hidden under soft blocks, and no enemy starts on top of the bomber.
/// </summary>
public class StageClearabilityTests
{
    private static string StagesDirectory()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Game", "Assets", "Resources", "Stages")))
            dir = Path.GetDirectoryName(dir);
        Assert.That(dir, Is.Not.Null, "stage files not found above the test directory");
        return Path.Combine(dir!, "Game", "Assets", "Resources", "Stages");
    }

    private static IEnumerable<int> Stages() => Enumerable.Range(1, StageTable.StageCount);

    private static Game Load(int number)
    {
        var path = Path.Combine(StagesDirectory(), $"stage-{number:000}.txt");
        return Game.ForStage(StageFile.Parse(File.ReadAllText(path), number));
    }

    private static readonly (int dx, int dy)[] Steps = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    /// <summary>Tiles reachable from <paramref name="start"/> over tiles accepted by <paramref name="passable"/>, with distances.</summary>
    private static Dictionary<TilePos, int> Reach(Arena arena, TilePos start, Func<Tile, bool> passable)
    {
        var dist = new Dictionary<TilePos, int> { [start] = 0 };
        var queue = new Queue<TilePos>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            foreach (var (dx, dy) in Steps)
            {
                var n = new TilePos(t.X + dx, t.Y + dy);
                if (!arena.InBounds(n.X, n.Y) || dist.ContainsKey(n) || !passable(arena[n])) continue;
                dist[n] = dist[t] + 1;
                queue.Enqueue(n);
            }
        }
        return dist;
    }

    [TestCaseSource(nameof(Stages))]
    public void Every_soft_block_and_floor_tile_can_be_reached_by_bombing_through(int number)
    {
        var g = Load(number);
        // Soft blocks can be blown up, so only hard blocks really divide the arena.
        var reach = Reach(g.Arena, g.Bomber.Spawn, t => t != Tile.HardBlock);
        for (int y = 0; y < g.Arena.Height; y++)
        for (int x = 0; x < g.Arena.Width; x++)
            if (g.Arena[x, y] != Tile.HardBlock)
                Assert.That(reach.ContainsKey(new TilePos(x, y)), $"stage {number}: ({x},{y}) is sealed off by hard blocks");
    }

    [TestCaseSource(nameof(Stages))]
    public void The_bomber_can_break_a_first_block_and_survive(int number)
    {
        Assert.That(CanBreakFirstBlock(Load(number)), $"stage {number}: no reachable spot where a first bomb breaks a block and the bomber can get clear");
    }

    [Test]
    public void A_start_boxed_in_by_soft_blocks_is_caught()
    {
        // The bomber can only stand still or step one tile, and every bomb catches it.
        var boxed = new Game(Arena.FromRows(
            "#######",
            "#..+..#",
            "#+#+#.#",
            "#.....#",
            "#######"), new TilePos(1, 1));
        Assert.That(CanBreakFirstBlock(boxed), Is.False);
        var open = new Game(Arena.FromRows(
            "#######",
            "#...+.#",
            "#.#+#.#",
            "#.....#",
            "#######"), new TilePos(1, 1));
        Assert.That(CanBreakFirstBlock(open), Is.True);
    }

    private static bool CanBreakFirstBlock(Game g)
    {
        var arena = g.Arena;
        // Walking over open floor, a safe tile must be reached well before the fuse runs out (with a second spare).
        int tilesInTime = (Bomb.FuseTicks - Units.TicksPerSecond) * g.Bomber.SpeedPerTick / Units.PerTile;

        // Somewhere the bomber can walk to, a range-1 bomb must hit a soft block and leave a safe tile in reach.
        foreach (var bombTile in Reach(arena, g.Bomber.Spawn, t => t == Tile.Floor).Keys)
        {
            var blast = new HashSet<TilePos> { bombTile };
            bool hitsSoftBlock = false;
            foreach (var (dx, dy) in Steps)
            {
                var t = new TilePos(bombTile.X + dx, bombTile.Y + dy);
                if (!arena.InBounds(t.X, t.Y) || arena[t] == Tile.HardBlock) continue;
                blast.Add(t);
                hitsSoftBlock |= arena[t] == Tile.SoftBlock;
            }
            if (!hitsSoftBlock) continue;
            if (Reach(arena, bombTile, t => t == Tile.Floor).Any(kv => !blast.Contains(kv.Key) && kv.Value <= tilesInTime))
                return true;
        }
        return false;
    }

    [TestCaseSource(nameof(Stages))]
    public void The_exit_and_the_power_up_are_hidden_under_soft_blocks(int number)
    {
        var g = Load(number);
        Assert.That(g.ExitTile, Is.Not.Null, $"stage {number} has no exit");
        Assert.That(g.Arena[g.ExitTile!.Value], Is.EqualTo(Tile.SoftBlock), $"stage {number}: the exit is not under a soft block");
        Assert.That(g.PowerUps, Has.Count.EqualTo(1), $"stage {number}: one power-up per attempt");
        var p = g.PowerUps[0];
        Assert.That(g.Arena[p.Tile], Is.EqualTo(Tile.SoftBlock), $"stage {number}: the power-up is not under a soft block");
        Assert.That(p.Tile, Is.Not.EqualTo(g.ExitTile.Value), $"stage {number}: the power-up shares the exit's block");
        Assert.That(p.Revealed, Is.False);
    }

    [TestCaseSource(nameof(Stages))]
    public void Enemies_start_away_from_the_bomber(int number)
    {
        var g = Load(number);
        Assert.That(g.Enemies, Is.Not.Empty, $"stage {number} has no enemies");
        foreach (var e in g.Enemies)
        {
            int d = Math.Abs(e.Tile.X - g.Bomber.Spawn.X) + Math.Abs(e.Tile.Y - g.Bomber.Spawn.Y);
            Assert.That(d, Is.GreaterThanOrEqualTo(Game.EnemySpawnDistance), $"stage {number}: a {e.Kind} starts {d} tiles from the bomber");
        }
    }
}
