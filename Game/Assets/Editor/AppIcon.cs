using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Makes the Android app icon from the palace models: a crowned pearl bomber (the adaptive icon's foreground, kept
/// inside Android's safe zone) on navy with gold rings (its background), plus a combined legacy/round icon. Run before
/// every build (Builds.cs) so the icon always matches the models.
/// </summary>
public static class AppIcon
{
    private const string Folder = "Assets/Art/Icon/";
    private const int Size = 432; // adaptive icon layers are 108 dp, i.e. 432 px at xxxhdpi

    public static void Ensure()
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Directory.CreateDirectory(Folder);

        // Foreground: the whole crowned bomber inside the middle 60% (the part every launcher shows, whatever its mask).
        var foreground = PalaceSprites.Render(t =>
        {
            var rig = PalaceArt.Bomber(t, 0);
            rig.Root.localRotation = Quaternion.Euler(0f, 180f, 0f);
            return rig.Root;
        }, Size, Size, 1.2f, new Vector3(0f, 0.6f, 0f), 12f);
        var background = Background();
        var legacy = Composite(background, foreground);

        Save(foreground, "Foreground");
        Save(background, "Background");
        Save(legacy, "Legacy");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var fg = Load("Foreground");
        var bg = Load("Background");
        var all = Load("Legacy");
        SetIcons(AndroidPlatformIconKind.Adaptive, bg, fg);
        SetIcons(AndroidPlatformIconKind.Round, all);
        SetIcons(AndroidPlatformIconKind.Legacy, all);
    }

    private static void SetIcons(PlatformIconKind kind, params Texture2D[] layers)
    {
        var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
        foreach (var icon in icons) icon.SetTextures(layers);
        PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
    }

    /// <summary>Navy lit from the centre, with a double gold ring around the safe zone.</summary>
    private static Texture2D Background()
    {
        var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var centre = new Color(0.12f, 0.19f, 0.36f);
        var edge = new Color(0.02f, 0.03f, 0.08f);
        var gold = new Color(0.91f, 0.76f, 0.38f);
        var px = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float dx = (x + 0.5f) / Size - 0.5f, dy = (y + 0.5f) / Size - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
            var c = Color.Lerp(centre, edge, Mathf.SmoothStep(0f, 1f, r * 1.7f));
            float ring = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(r - 0.335f) * Size / 2.5f), 0.6f * Mathf.Clamp01(1f - Mathf.Abs(r - 0.31f) * Size / 1.2f));
            px[y * Size + x] = Color.Lerp(c, gold, ring);
        }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    private static Texture2D Composite(Texture2D under, Texture2D over)
    {
        var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var a = under.GetPixels();
        var b = over.GetPixels();
        for (int i = 0; i < a.Length; i++) a[i] = Color.Lerp(a[i], new Color(b[i].r, b[i].g, b[i].b, 1f), b[i].a);
        t.SetPixels(a);
        t.Apply();
        return t;
    }

    private static void Save(Texture2D t, string name) => File.WriteAllBytes(Folder + name + ".png", t.EncodeToPNG());

    private static Texture2D Load(string name)
    {
        var path = Folder + name + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
