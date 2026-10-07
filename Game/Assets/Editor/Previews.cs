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
        PalaceSetup.Run();
        // One scene for all pictures: opening another would unload the placeholder art cached in code.
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        foreach (bool threeD in new[] { true, false })
        {
            string suffix = threeD ? "3d" : "2d";
            foreach (var theme in ArenaTheme.All)
                Capture(StageScene(), threeD, 0, theme, $"Builds/preview-{theme.Key}-stage-{suffix}.png");
            Capture(RoundScene(), threeD, 1, ArenaTheme.Palace, $"Builds/preview-round-{suffix}.png");
        }
    }

    /// <summary>Each palace material on a bevelled box, a cube and a sphere, for checking materials and meshes.</summary>
    public static void MaterialsBatch()
    {
        PalaceSetup.Run();
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var root = new GameObject("Test").transform;
        var names = new[] { "Paving", "Brick", "Earth", "Lawn", "Rock", "Plaster", "Roof", "Bamboo", "Pebbles", "Ice", "Snow", "Bronze", "Silver", "Lacquer" };
        for (int i = 0; i < names.Length; i++)
        {
            var m = PalaceArt.Mat(names[i]);
            PalaceArt.Part(root, PalaceArt.ChamferBox(Vector3.one * 0.8f, 0.05f), m, new Vector3(i * 1.2f, 0.4f, 0), Vector3.one);
            PalaceArt.Part(root, PalaceArt.Primitive(PrimitiveType.Cube), m, new Vector3(i * 1.2f, 0.4f, -1.4f), Vector3.one * 0.8f);
            PalaceArt.Part(root, PalaceArt.Primitive(PrimitiveType.Sphere), m, new Vector3(i * 1.2f, 0.4f, -2.8f), Vector3.one * 0.8f);
        }
        PalaceArt.Part(root, PalaceArt.ChamferBox(Vector3.one * 0.8f, 0.05f), PalaceArt.Glossy(new Color(0.8f, 0.2f, 0.2f)), new Vector3(-1.2f, 0.4f, 0), Vector3.one);
        PalaceArt.Part(root, PalaceArt.ChamferBox(Vector3.one * 0.8f, 0.05f), PalaceArt.Tint("Matte", new Color(0.2f, 0.6f, 0.2f)), new Vector3(-1.2f, 0.4f, -1.4f), Vector3.one);
        var mesh = PalaceArt.ChamferBox(Vector3.one * 0.8f, 0.05f);
        var nrm = mesh.normals; var vtx = mesh.vertices; var tri = mesh.triangles;
        for (int t = 0; t < tri.Length; t += 3)
        {
            var a = vtx[tri[t]]; var b = vtx[tri[t + 1]]; var c = vtx[tri[t + 2]];
            var geo = Vector3.Cross(b - a, c - a).normalized; // Unity front faces wind clockwise: this points inward
            Debug.Log($"FACET centre={(a + b + c) / 3f:F2} normal={nrm[tri[t]]:F2} windingNormal={-geo:F2}");
        }
        PalaceArt.Light();
        var camera = Camera.main;
        camera.transform.SetPositionAndRotation(new Vector3(7.8f, 11f, -9.5f), Quaternion.Euler(50f, 0f, 0f));
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
        camera.aspect = (float)Width / Height;
        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt });
        RenderTexture.active = rt;
        var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        File.WriteAllBytes("Builds/preview-materials.png", image.EncodeToPNG());
        EditorApplication.Exit(0);
    }

    /// <summary>Every character model in Resources/Palace/Characters as a bomber, in a row, for choosing the slots' characters.</summary>
    public static void CharactersBatch()
    {
        PalaceSetup.Run();
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var root = new GameObject("Line-up").transform;
        var models = PalaceArt.BomberModels;
        var names = new System.Collections.Generic.List<string>();
        foreach (var path in Directory.GetFiles("Assets/Resources/Palace/Characters", "*.fbx"))
            names.Add(Path.GetFileNameWithoutExtension(path));
        for (int i = 0; i < names.Count; i++)
        {
            PalaceArt.BomberModels = new[] { names[i] };
            var rig = PalaceArt.Bomber(root, 0);
            rig.Root.SetPositionAndRotation(new Vector3(i % 6 * 1.3f, 0f, -(i / 6) * 1.6f), Quaternion.Euler(0f, 180f, 0f));
            rig.Root.localScale = Vector3.one * 1.25f;
            Debug.Log($"CHARACTER {i}: {names[i]}");
        }
        PalaceArt.BomberModels = models;
        var floor = PalaceArt.Part(root, PalaceArt.Primitive(PrimitiveType.Quad), PalaceArt.Mat("Floor"), new Vector3(3.2f, 0f, -0.8f), new Vector3(10f, 5f, 1f), false);
        floor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        PalaceArt.Light();
        var camera = Camera.main;
        camera.transform.SetPositionAndRotation(new Vector3(3.25f, 4.6f, -5.6f), Quaternion.Euler(38f, 0f, 0f));
        camera.fieldOfView = 40f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
        camera.aspect = (float)Width / Height;
        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt });
        var resolved = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(rt, resolved);
        RenderTexture.active = resolved;
        var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        File.WriteAllBytes("Builds/preview-characters.png", image.EncodeToPNG());
        EditorApplication.Exit(0);
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

    private static void Capture(Game game, bool threeD, int follow, ArenaTheme theme, string path)
    {
        var before = new System.Collections.Generic.HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
        // The 3D view tunes shadows at runtime; in the editor that would be saved into the project's quality settings.
        float shadowDistance = QualitySettings.shadowDistance;
        var view = ArenaView.Create(game, threeD, theme);
        var camera = Camera.main;
        camera.aspect = (float)Width / Height; // the picture's shape, not the editor window's
        view.OnTick();
        view.Draw(1f);
        view.Follow(follow);

        // Particles don't run on their own in the editor: move any that were started a little way in.
        foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            if (ps.isPlaying || ps.isEmitting) ps.Simulate(0.4f, true, true);

        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
        if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera, request))
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
        else
        {
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = null;
        }
        RenderTexture.active = rt;
        var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, image.EncodeToPNG());
        Debug.Log("PREVIEW: " + path);
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (!before.Contains(root)) Object.DestroyImmediate(root);
        QualitySettings.shadowDistance = shadowDistance;
    }
}
