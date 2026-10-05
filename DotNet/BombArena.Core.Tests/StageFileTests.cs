using System;
using System.IO;
using System.Linq;
using BombArena.Core;
using NUnit.Framework;

namespace BombArena.Core.Tests;

public class StageFileTests
{
    private static string StagesDirectory()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Game", "Assets", "Resources", "Stages")))
            dir = Path.GetDirectoryName(dir);
        Assert.That(dir, Is.Not.Null, "stage files not found above the test directory");
        return Path.Combine(dir!, "Game", "Assets", "Resources", "Stages");
    }

    [Test]
    public void Format_and_parse_round_trip()
    {
        var spec = StageTable.Spec(57);
        var back = StageFile.Parse(StageFile.Format(spec), 57);
        Assert.That(StageFile.Format(back), Is.EqualTo(StageFile.Format(spec)));
        Assert.That(back.Enemies.Sum(e => e.Count), Is.EqualTo(spec.Enemies.Sum(e => e.Count)));
    }

    [Test]
    public void Comments_blank_lines_and_spacing_are_allowed()
    {
        var spec = StageFile.Parse("# tuned by hand\r\n\r\nwidth=21\r\nheight = 11 # comment\r\nseed = 5\r\nenemies = Walker:2,Phantom:1\r\n", 3);
        Assert.That((spec.Width, spec.Height, spec.Seed), Is.EqualTo((21, 11, 5UL)));
        Assert.That(spec.Enemies.Select(e => (e.Kind, e.Count)), Is.EqualTo(new[] { (EnemyKind.Walker, 2), (EnemyKind.Phantom, 1) }));
    }

    [TestCase("width = 20\nheight = 11", TestName = "Even width is rejected")]
    [TestCase("colour = red", TestName = "Unknown keys are rejected")]
    [TestCase("enemies = Dragon:2", TestName = "Unknown enemy kinds are rejected")]
    [TestCase("targetSeconds = soon", TestName = "Non-numbers are rejected")]
    public void Bad_files_give_a_clear_error(string text)
    {
        var ex = Assert.Throws<FormatException>(() => StageFile.Parse(text, 9));
        Assert.That(ex!.Message, Does.Contain("Stage 9"));
    }

    [Test]
    public void Table_follows_the_difficulty_curve()
    {
        void Check(int n, int w, int h, int min, int max, int runners, int target, params EnemyKind[] kinds)
        {
            var s = StageTable.Spec(n);
            int total = s.Enemies.Sum(e => e.Count);
            Assert.That((s.Width, s.Height), Is.EqualTo((w, h)), $"stage {n} size");
            Assert.That(total, Is.InRange(min, max), $"stage {n} enemy count");
            Assert.That(s.RunnersFromExit, Is.EqualTo(runners), $"stage {n} runners");
            Assert.That(s.TargetSeconds, Is.EqualTo(target), $"stage {n} T");
            Assert.That(s.Enemies.Select(e => e.Kind), Is.EquivalentTo(kinds), $"stage {n} kinds");
        }

        for (int n = 1; n <= 10; n++) Check(n, 19, 9, 3, 4, 2, 90, EnemyKind.Walker);
        for (int n = 11; n <= 30; n++) Check(n, 21, 11, 4, 6, 3, 120, EnemyKind.Walker, EnemyKind.WallPasser);
        for (int n = 31; n <= 60; n++) Check(n, 23, n <= 45 ? 11 : 13, 5, 7, 4, 150, EnemyKind.Walker, EnemyKind.WallPasser, EnemyKind.Phantom);
        for (int n = 61; n <= 90; n++) Check(n, 25, 13, 6, 8, 5, 180, EnemyKind.Walker, EnemyKind.WallPasser, EnemyKind.Phantom, EnemyKind.Runner);
        for (int n = 91; n <= 100; n++) Check(n, 27, 15, 8, 10, 6, 210, EnemyKind.Walker, EnemyKind.WallPasser, EnemyKind.Phantom, EnemyKind.Runner);
    }

    [Test]
    public void Every_stage_file_loads_and_builds_a_valid_game()
    {
        var dir = StagesDirectory();
        for (int n = 1; n <= StageTable.StageCount; n++)
        {
            var path = Path.Combine(dir, StageFile.FileName(n) + ".txt");
            Assert.That(File.Exists(path), $"missing {path}");
            var spec = StageFile.Parse(File.ReadAllText(path), n);
            var game = Game.ForStage(spec, attemptSeed: 1);
            Assert.That(game.Enemies, Has.Count.EqualTo(spec.Enemies.Sum(e => e.Count)), $"stage {n}: every enemy found a starting tile");
            Assert.That(game.ExitTile, Is.Not.Null, $"stage {n} has an exit");
            Assert.That(game.Arena[game.ExitTile!.Value], Is.EqualTo(Tile.SoftBlock), $"stage {n} exit is hidden");
            Assert.That(game.PowerUps, Has.Count.EqualTo(1), $"stage {n} hides a power-up");
            Assert.That(game.Arena[game.Bomber.Tile], Is.EqualTo(Tile.Floor));
        }
    }
}
