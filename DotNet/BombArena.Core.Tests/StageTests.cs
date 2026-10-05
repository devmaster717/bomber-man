using System.Linq;
using BombArena.Core;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class StageTests
{
    private const int T = Units.PerTile;

    // Bomber at (1,1). The exit is hidden under the soft block at (3,1); a bomb dropped at (2,1) uncovers it.
    private static Game ExitCorridor(int runners = 0)
    {
        var g = new Game(Arena.FromRows(
            "#########",
            "#..+....#",
            "#.#.#.#.#",
            "#.......#",
            "#########"), new TilePos(1, 1));
        g.SetExit(new TilePos(3, 1), runners);
        return g;
    }

    private static void Wait(Game g, int ticks, Direction d = Direction.None)
    {
        for (int i = 0; i < ticks && g.Outcome == Outcome.Playing; i++) g.Step(d);
    }

    /// <summary>Drops a bomb at (2,1), hides at (1,3) and waits for the blast and its fire to end.</summary>
    private static void BombFromTwoOneAndHide(Game g)
    {
        Wait(g, 10, Direction.Up);   // back to row 1 if hiding from a previous call
        Wait(g, 5, Direction.Right);
        g.Step(Direction.None, placeBomb: true);
        Wait(g, 5, Direction.Left);
        Wait(g, 10, Direction.Down);
        while (g.Bombs.Count > 0 && g.Outcome == Outcome.Playing) g.Step(Direction.None);
    }

    [Test]
    public void Exit_is_hidden_under_a_soft_block_chosen_from_the_seed()
    {
        var spec = new StageSpec { Seed = 11, Enemies = { new EnemyGroup(EnemyKind.Walker, 3) } };
        var a = Game.ForStage(spec);
        var b = Game.ForStage(spec);
        Assert.That(a.ExitTile, Is.Not.Null);
        Assert.That(a.Arena[a.ExitTile!.Value], Is.EqualTo(Tile.SoftBlock));
        Assert.That(a.ExitRevealed, Is.False);
        Assert.That(b.ExitTile, Is.EqualTo(a.ExitTile));
    }

    [Test]
    public void Exit_appears_only_after_the_fire_that_uncovered_it_ends()
    {
        var g = ExitCorridor();
        Wait(g, 5, Direction.Right);
        g.Step(Direction.None, placeBomb: true);
        Wait(g, 5, Direction.Left);
        Wait(g, 10, Direction.Down);
        while (g.Bombs.Count > 0 && g.Outcome == Outcome.Playing) g.Step(Direction.None);

        Assert.That(g.Arena[3, 1], Is.EqualTo(Tile.Floor), "soft block destroyed");
        Assert.That(g.ExitRevealed, Is.False, "still burning");
        Wait(g, Game.FireTicks);
        Assert.That(g.ExitRevealed, Is.True);
    }

    [Test]
    public void Exit_opens_only_when_every_enemy_is_dead()
    {
        var g = new Game(Arena.FromRows(
            "#######",
            "#.....#",
            "#######",
            "#.....#",
            "#######"), new TilePos(1, 1));
        g.SetExit(new TilePos(3, 1));
        var walker = g.AddEnemy(EnemyKind.Walker, new TilePos(3, 3)); // sealed off in the lower corridor
        Wait(g, 10, Direction.Right);
        Assert.That(g.Bomber.Tile, Is.EqualTo(new TilePos(3, 1)));
        Assert.That(g.Outcome, Is.EqualTo(Outcome.Playing), "standing on the exit while an enemy lives");

        walker.Alive = false;
        g.Step(Direction.None);
        Assert.That(g.Outcome, Is.EqualTo(Outcome.Cleared));
    }

    [Test]
    public void The_uncovering_blast_does_not_release_Runners_but_the_next_one_does()
    {
        var g = ExitCorridor(runners: 3);
        BombFromTwoOneAndHide(g);
        Wait(g, Game.FireTicks);
        Assert.That(g.ExitRevealed);
        Assert.That(g.Enemies, Is.Empty, "the blast that uncovered the exit released nothing");

        BombFromTwoOneAndHide(g); // this blast's fire reaches the uncovered exit at (3,1)
        Assert.That(g.Enemies, Is.Empty, "Runners wait for the fire on the exit to go out");
        Assert.That(g.ExitOpen, Is.False, "the exit stays shut while Runners are waiting");
        Wait(g, Game.FireTicks);
        Assert.That(g.Enemies.Count(e => e.Kind == EnemyKind.Runner), Is.EqualTo(3));
        Assert.That(g.Enemies.All(e => e.Tile == new TilePos(3, 1) || e.Alive));
    }

    [Test]
    public void Runners_are_released_only_once()
    {
        var g = ExitCorridor(runners: 2);
        BombFromTwoOneAndHide(g);
        Wait(g, Game.FireTicks);
        BombFromTwoOneAndHide(g);
        Wait(g, Game.FireTicks);
        int released = g.Enemies.Count;
        foreach (var e in g.Enemies) e.Alive = false; // clear them out of the way
        BombFromTwoOneAndHide(g);
        Wait(g, Game.FireTicks);
        Assert.That(released, Is.EqualTo(2));
        Assert.That(g.Enemies, Has.Count.EqualTo(2), "no new Runners from later blasts");
    }

    [Test]
    public void Runners_move_at_eight_tiles_per_second()
    {
        Assert.That(new Enemy(EnemyKind.Runner, new TilePos(1, 1)).SpeedPerTick, Is.EqualTo(400));
        Assert.That(Enemy.TilesPerSecond(EnemyKind.Runner), Is.EqualTo(2 * Bomber.BaseTilesPerSecond));
    }

    [Test]
    public void Walking_into_the_open_exit_clears_the_stage_with_stars_by_time()
    {
        int StarsAfter(int seconds, int targetSeconds)
        {
            var g = new Game(Arena.FromRows(
                "#######",
                "#.....#",
                "#######"), new TilePos(1, 1));
            g.SetExit(new TilePos(5, 1));
            g.SetTargetSeconds(targetSeconds);
            // Wait so that the clear lands at the given time (walking takes 1 s for 4 tiles).
            Wait(g, (seconds - 1) * Units.TicksPerSecond);
            Wait(g, 40, Direction.Right);
            Assert.That(g.Outcome, Is.EqualTo(Outcome.Cleared));
            return g.Stars;
        }

        Assert.That(StarsAfter(seconds: 50, targetSeconds: 90), Is.EqualTo(3));
        Assert.That(StarsAfter(seconds: 100, targetSeconds: 90), Is.EqualTo(2));
        Assert.That(StarsAfter(seconds: 200, targetSeconds: 90), Is.EqualTo(1));
    }

    [Test]
    public void Attempt_fails_at_three_times_the_target_time()
    {
        var g = ExitCorridor();
        g.SetTargetSeconds(10);
        Wait(g, 30 * Units.TicksPerSecond - 1);
        Assert.That(g.Outcome, Is.EqualTo(Outcome.Playing));
        g.Step(Direction.None);
        Assert.That(g.Outcome, Is.EqualTo(Outcome.Failed));
        Assert.That(g.FailReason, Is.EqualTo(FailReason.TimeUp));
    }

    [Test]
    public void Dying_fails_with_reason_died()
    {
        var g = ExitCorridor();
        g.Step(Direction.None, placeBomb: true);
        Wait(g, Bomb.FuseTicks);
        Assert.That(g.FailReason, Is.EqualTo(FailReason.Died));
    }

    [Test]
    public void Stage_spec_places_its_enemies_and_settings()
    {
        var spec = new StageSpec
        {
            Width = 19, Height = 9, Seed = 3, TargetSeconds = 90, RunnersFromExit = 2,
            Enemies = { new EnemyGroup(EnemyKind.Walker, 4) },
        };
        var g = Game.ForStage(spec);
        Assert.That((g.Arena.Width, g.Arena.Height), Is.EqualTo((19, 9)));
        Assert.That(g.Enemies.Count(e => e.Kind == EnemyKind.Walker), Is.EqualTo(4));
        Assert.That(g.TargetTicks, Is.EqualTo(90 * Units.TicksPerSecond));
        Assert.That(g.RunnersFromExit, Is.EqualTo(2));
    }
}
