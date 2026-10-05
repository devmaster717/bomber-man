using System.IO;
using BombArena.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Renders still pictures of the arena views to Builds/preview-*.png, for checking the look without a device:
/// Unity.exe -batchmode -projectPath Game -executeMethod Previews.RenderBatch
/// </summary>
public static class Previews
{
    private const int Width = 1600, Height = 900;

    [MenuItem("Bomb Arena/Render View Previews")]
    public static void Render()
    {
        // One scene for all pictures: opening another would unload the placeholder art cached in code.
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        foreach (bool threeD in new[] { true, false })
        {
            string suffix = threeD ? "3d" : "2d";
            Capture(StageScene(), threeD, 0, $"Builds/preview-stage-{suffix}.png");
            Capture(RoundScene(), threeD, 1, $"Builds/preview-round-{suffix}.png");
        }
    }

    public static void RenderBatch()
    {
        Render();
        EditorApplication.Exit(0);
    }

    // Stage 1 a few seconds in, with a bomb exploding next to the bomber's start.
    private static Game StageScene()
    {
        var g = Game.ForStage(StageLibrary.Load(1), 7);
        for (int i = 0; i < 30; i++) g.Step(Direction.None);
        g.Step(Direction.None, placeBomb: true);
        while (g.Bombs.Count > 0 && g.Outcome == Outcome.Playing && g.Tick < 400) g.Step(Direction.None);
        return g;
    }

    // A three-player round with enemies, everyone on the move and two bombs down.
    private static Game RoundScene()
    {
        var g = Game.ForRound(19, 11, 3, 0, 5, enemiesOn: true);
        var moves = new[] { Direction.Right, Direction.Left, Direction.Up };
        for (int i = 0; i < 16; i++)
            g.Step(new BomberInput(moves[0], i == 0), new BomberInput(moves[1], i == 0), new BomberInput(moves[2], false));
        return g;
    }

    private static void Capture(Game game, bool threeD, int follow, string path)
    {
        var before = new System.Collections.Generic.HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
        // The 3D view tunes shadows at runtime; in the editor that would be saved into the project's quality settings.
        float shadowDistance = QualitySettings.shadowDistance;
        var view = ArenaView.Create(game, threeD);
        var camera = Camera.main;
        camera.aspect = (float)Width / Height; // the picture's shape, not the editor window's
        view.OnTick();
        view.Draw(1f);
        view.Follow(follow);

        var rt = new RenderTexture(Width, Height, 24);
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        image.Apply();
        camera.targetTexture = null;
        RenderTexture.active = null;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, image.EncodeToPNG());
        Debug.Log("PREVIEW: " + path);
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (!before.Contains(root)) Object.DestroyImmediate(root);
        QualitySettings.shadowDistance = shadowDistance;
    }
}
