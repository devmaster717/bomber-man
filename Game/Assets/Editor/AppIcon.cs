using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Makes the Android app icon from the palace models: the game's bomb, black with a gold band and a lit fuse (the
/// adaptive icon's foreground, kept inside Android's safe zone) on navy with gold rings (its background), plus a
/// combined legacy/round icon. Run before every build (Builds.cs) so the icon always matches the models.
/// </summary>
public static class AppIcon
{
    private const string Folder = "Assets/Art/Icon/";
    private const int Size = 432; // adaptive icon layers are 108 dp, i.e. 432 px at xxxhdpi

    public static void Ensure()
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Directory.CreateDirectory(Folder);

        // Foreground: the whole bomb, fuse and all, inside the middle 60% (the part every launcher shows, whatever its
        // mask), turned a little so its band and cap catch the light.
        var foreground = PalaceSprites.Render(t =>
        {
            var bomb = PalaceArt.Bomb(t, out _);
            bomb.localRotation = Quaternion.Euler(0f, 25f, -12f);
            return bomb;
        }, Size, Size, 0.78f, new Vector3(0f, 0.47f, 0f), 14f);
        AddSpark(foreground);
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

    /// <summary>
    /// A burning spark over the fuse's glowing tip (the highest bright yellow in the picture, above the gold cap): a hot white core in
    /// an orange glow with eight rays, so the fuse reads as lit even at launcher size.
    /// </summary>
    private static void AddSpark(Texture2D t)
    {
        var px = t.GetPixels();
        bool Bright(Color c) => c.a > 0.5f && c.r > 0.9f && c.g > 0.8f && c.b < 0.8f;
        int top = -1;
        for (int i = 0; i < px.Length; i++) if (Bright(px[i])) top = Mathf.Max(top, i / Size);
        float sx = 0, sy = 0, n = 0;
        for (int y = Mathf.Max(0, top - Size / 20); y <= top; y++)
        for (int x = 0; x < Size; x++)
            if (Bright(px[y * Size + x])) { sx += x; sy += y; n++; }
        if (n == 0) return;
        float cx = sx / n, cy = sy / n;
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float dx = x - cx, dy = y - cy, r = Mathf.Sqrt(dx * dx + dy * dy) / Size;
            float angle = Mathf.Atan2(dy, dx);
            float rays = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 4f)), 24f) * Mathf.Clamp01(1f - r / 0.13f);
            float glow = Mathf.Exp(-r * r / (0.045f * 0.045f));
            float core = Mathf.Exp(-r * r / (0.014f * 0.014f));
            var add = new Color(1f, 0.55f, 0.12f) * glow * 0.9f + new Color(1f, 0.85f, 0.5f) * rays + new Color(1f, 1f, 0.92f) * core;
            float a = Mathf.Clamp01(Mathf.Max(add.r, Mathf.Max(add.g, add.b)));
            if (a <= 0.003f) continue;
            var c = px[y * Size + x];
            // Light added over whatever is there (the bomb or transparency), kept as straight alpha.
            var rgb = new Color(c.r * c.a + add.r, c.g * c.a + add.g, c.b * c.a + add.b);
            float alpha = Mathf.Max(c.a, a);
            px[y * Size + x] = new Color(Mathf.Clamp01(rgb.r / alpha), Mathf.Clamp01(rgb.g / alpha), Mathf.Clamp01(rgb.b / alpha), alpha);
        }
        t.SetPixels(px);
        t.Apply();
    }

    /// <summary>Navy lit from the centre with a warm glow behind the bomb, and a double gold ring around the safe zone.</summary>
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
            // Warm firelight behind the bomb, so its dark body stands out.
            c += new Color(0.75f, 0.34f, 0.08f) * Mathf.Exp(-r * r / (0.25f * 0.25f));
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
