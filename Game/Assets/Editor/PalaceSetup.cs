using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Sets up the palace look: the Universal Render Pipeline (for bloom, colour grading and soft shadows) and the
/// material assets the arena views load at runtime. Materials must be assets, not made in code, so the build keeps
/// the shader variants they use. Runs before every build (see Builds.cs) and can be run from the menu.
/// </summary>
public static class PalaceSetup
{
    private const string Root = "Assets/Resources/Palace";
    private const string Textures = Root + "/Textures/";
    private const string Materials = Root + "/Materials/";
    private const string Sprites = Root + "/Sprites/";
    private const string Avatars = Root + "/Avatars/";
    private const string AvatarSources = "Assets/Art/Avatars/";
    private const string SettingsFolder = "Assets/Settings";

    [MenuItem("Bomb Arena/Set Up Palace Look")]
    public static void Run()
    {
        // Finish importing new or changed textures first, or materials would see them as missing.
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EnsurePipeline();
        EnsureMaterials();
        AssetDatabase.SaveAssets();
        BakeSprites();
        BakeAvatars();
    }

    /// <summary>
    /// Cuts the avatar pictures in Assets/Art/Avatars into round portraits with a gold ring (see
    /// docs/art/avatar-prompts.md). The pictures are one sheet of all ten (sheet.png: 5 columns by 2 rows, in
    /// PlayerAvatars.Keys order) and/or one file per avatar named by its key, which wins over its sheet cell.
    /// An avatar with neither gets a plain disc in its colour.
    /// </summary>
    private static void BakeAvatars()
    {
        const int size = 256;
        Directory.CreateDirectory(Avatars);
        var gold = new Color(0.91f, 0.76f, 0.38f);
        var sheet = LoadSource("sheet");
        for (int i = 0; i < PlayerAvatars.Count; i++)
        {
            var own = LoadSource(PlayerAvatars.Keys[i]);
            var source = own ?? sheet;
            // The part of the source this avatar takes: the whole picture, or its cell of the sheet (row 0 on top).
            float cw = source == null ? 0 : (own != null ? source.width : source.width / 5f);
            float ch = source == null ? 0 : (own != null ? source.height : source.height / 2f);
            float cx = own != null ? 0 : i % 5 * cw;
            float cy = source == null ? 0 : (own != null ? 0 : (1 - i / 5) * ch);
            if (own == null) { cx += cw * 0.03f; cy += ch * 0.03f; cw *= 0.94f; ch *= 0.94f; } // skip any lines between cells
            // A square across the middle of that part; in a tall one, centred 38% of the way down, where a headshot's
            // face is (so hair and chin both fit).
            float side = Mathf.Min(cw, ch);
            float x0 = cx + (cw - side) / 2f;
            float y0 = Mathf.Clamp(cy + ch * 0.62f - side / 2f, cy, cy + ch - side);
            var back = PlayerAvatars.Backdrop(i);
            var portrait = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                var c = Color.Lerp(back * 1.15f, back * 0.6f, Mathf.Clamp01(r * 2f - dy)); // lit from above
                if (source != null)
                {
                    var p = source.GetPixelBilinear((x0 + (x + 0.5f) / size * side) / source.width,
                        (y0 + (y + 0.5f) / size * side) / source.height);
                    c = Color.Lerp(c, new Color(p.r, p.g, p.b, 1f), p.a);
                }
                float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.475f) * size / 3f);
                c = Color.Lerp(c, gold, ring);
                c.a = Mathf.Clamp01((0.5f - r) * size); // round, with a soft edge
                px[y * size + x] = c;
            }
            portrait.SetPixels(px);
            portrait.Apply();
            var path = Avatars + "avatar-" + i + ".png";
            if (!File.Exists(path) || VisiblyDifferent(portrait, File.ReadAllBytes(path))) File.WriteAllBytes(path, portrait.EncodeToPNG());
            Object.DestroyImmediate(portrait);
            if (own != null) Object.DestroyImmediate(own);
        }
        if (sheet != null) Object.DestroyImmediate(sheet);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    /// <summary>Reads a hero picture straight from disk (so its import settings don't matter), or null if there's none.</summary>
    private static Texture2D LoadSource(string key)
    {
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
        {
            var path = AvatarSources + key + ext;
            if (!File.Exists(path)) continue;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (t.LoadImage(File.ReadAllBytes(path))) return t;
            Debug.LogError("Avatar picture can't be read (use PNG or JPG): " + path);
            Object.DestroyImmediate(t);
        }
        return null;
    }

    /// <summary>
    /// Renders the 2D view's sprites from the 3D models (PalaceSprites.All) in a scratch scene and saves them as PNGs;
    /// a file is only rewritten when its picture changed, so builds don't touch unchanged sprites.
    /// </summary>
    private static void BakeSprites()
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Directory.CreateDirectory(Sprites);
        foreach (var (key, build, top, theme) in PalaceSprites.All())
        {
            Texture2D texture;
            using (theme.Use()) texture = PalaceSprites.Render(build, top);
            var path = Sprites + key + ".png";
            if (!File.Exists(path) || VisiblyDifferent(texture, File.ReadAllBytes(path))) File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    // Rendering the same model twice can differ by a few levels in a few pixels (anti-aliasing); ignore that.
    private static bool VisiblyDifferent(Texture2D now, byte[] savedPng)
    {
        var saved = new Texture2D(2, 2);
        bool different = !saved.LoadImage(savedPng) || saved.width != now.width || saved.height != now.height;
        if (!different)
        {
            var a = now.GetPixels32();
            var b = saved.GetPixels32();
            int changed = 0;
            for (int i = 0; i < a.Length; i++)
                if (System.Math.Abs(a[i].r - b[i].r) > 8 || System.Math.Abs(a[i].g - b[i].g) > 8 ||
                    System.Math.Abs(a[i].b - b[i].b) > 8 || System.Math.Abs(a[i].a - b[i].a) > 8)
                    changed++;
            different = changed > a.Length / 200; // more than 0.5% of pixels
        }
        Object.DestroyImmediate(saved);
        return different;
    }

    private static void EnsurePipeline()
    {
        Directory.CreateDirectory(SettingsFolder);
        const string rendererPath = SettingsFolder + "/PalaceRenderer.asset", pipelinePath = SettingsFolder + "/PalaceURP.asset";
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            AssetDatabase.CreateAsset(renderer, rendererPath);
        }
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if (pipeline == null)
        {
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
        }

        // Phone-friendly settings: HDR for bloom on gold and fire, 2x MSAA, one shadow cascade over the visible arena.
        pipeline.supportsHDR = true;
        pipeline.msaaSampleCount = 2;
        pipeline.renderScale = 1f;
        pipeline.shadowDistance = 30f;
        pipeline.shadowCascadeCount = 1;
        pipeline.mainLightShadowmapResolution = 2048;
        var so = new SerializedObject(pipeline);
        so.FindProperty("m_SoftShadowsSupported").boolValue = true;
        // The SRP Batcher drew objects with another object's material in testing (Unity 6.0, URP 17.0.4); the arena's
        // blocks are combined with static batching instead (see ArenaRenderer3D).
        so.FindProperty("m_UseSRPBatcher").boolValue = false;
        so.FindProperty("m_ReflectionProbeBoxProjection").boolValue = true; // floor reflections line up with the arena
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);

        GraphicsSettings.defaultRenderPipeline = pipeline;
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = null; // every level uses the default above
            QualitySettings.realtimeReflectionProbes = true; // the polished floor and gold reflect the arena
        }
    }

    private static void EnsureMaterials()
    {
        Directory.CreateDirectory(Materials);
        // Palace, then the fortress, garden and frozen citadel themes (ArenaTheme), each from its own textures.
        foreach (var name in new[] { "Floor", "Marble", "Stone", "Wood", "Gold", "Carpet",
                     "Paving", "Brick", "Earth", "Lawn", "Rock", "Plaster", "Roof", "Bamboo", "Pebbles", "Ice", "Snow" })
            SetUp(Lit(name), name);
        // Variants built from other textures: bronze and silver from the gold's surface, and red lacquer with the
        // wood's grain under the marble's polish.
        SetUp(Lit("Bronze"), "Gold", "Gold", "Gold", new Color(0.85f, 0.55f, 0.34f));
        SetUp(Lit("Silver"), null, "Gold", "Gold", new Color(0.86f, 0.89f, 0.95f));
        SetUp(Lit("Lacquer"), "Wood", "Wood", "Marble", new Color(0.72f, 0.1f, 0.08f));

        // The bombers' cartoon characters (Kenney's Mini Characters) all share one colour-swatch texture.
        var character = Lit("Character");
        character.SetTexture("_BaseMap", Load(Root + "/Characters/colormap.png"));
        character.SetTexture("_BumpMap", null);
        character.SetTexture("_MetallicGlossMap", null);
        character.SetFloat("_Metallic", 0f);
        character.SetFloat("_Smoothness", 0.15f);
        character.SetColor("_BaseColor", Color.white);
        Finish(character);

        // Plain glossy and matte surfaces, tinted per use at runtime (same shader variant, so nothing is stripped).
        var glossy = Lit("Glossy");
        glossy.SetFloat("_Smoothness", 0.85f);
        Finish(glossy);
        var matte = Lit("Matte");
        matte.SetFloat("_Smoothness", 0.25f);
        Finish(matte);

        // See-through, for the Phantom.
        var ghost = Lit("Ghost");
        ghost.SetFloat("_Surface", 1f);
        ghost.SetFloat("_Blend", 0f);
        ghost.SetFloat("_Smoothness", 0.8f);
        ghost.SetColor("_BaseColor", new Color(0.85f, 0.92f, 1f, 0.7f));
        Finish(ghost);

        // Unlit glow (eyes, sparks, portal): colours above 1 bloom.
        var glow = Get(Materials + "Glow.mat", "Universal Render Pipeline/Unlit");
        glow.SetColor("_BaseColor", Color.white);
        Finish(glow, null);

        // Additive particles and floor glows.
        foreach (var (name, texture) in new[] { ("Flame", "Fx_Flame"), ("SoftGlow", "Fx_SoftDot"), ("Sparkle", "Fx_Sparkle"), ("Ring", "Fx_Ring") })
        {
            var m = Get(Materials + name + ".mat", "Universal Render Pipeline/Particles/Unlit");
            m.SetTexture("_BaseMap", Load(Textures + texture + ".png"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f); // additive
            m.SetColor("_BaseColor", Color.white);
            Finish(m, ParticleGUI.SetMaterialKeywords);
        }

        // A sky only for reflections and ambient light: the camera itself clears to a dark colour.
        var sky = Get(Materials + "Sky.mat", "Skybox/Procedural");
        sky.SetColor("_SkyTint", new Color(0.55f, 0.45f, 0.35f));
        sky.SetColor("_GroundColor", new Color(0.25f, 0.12f, 0.1f));
        sky.SetFloat("_Exposure", 1.1f);
        sky.SetFloat("_AtmosphereThickness", 0.8f);
        EditorUtility.SetDirty(sky);
    }

    private static Material Lit(string name) => Get(Materials + name + ".mat", "Universal Render Pipeline/Lit");

    private static void SetUp(Material m, string name) => SetUp(m, name, name, name, Color.white);

    /// <summary>A textured material: colour, normal and metallic/smoothness maps from the named sets (no colour map: plain).</summary>
    private static void SetUp(Material m, string colour, string normal, string surface, Color tint)
    {
        m.SetTexture("_BaseMap", colour == null ? null : Load(Textures + colour + "_Color.jpg"));
        m.SetTexture("_BumpMap", Load(Textures + normal + "_Normal.jpg"));
        m.SetFloat("_BumpScale", 1f);
        m.SetTexture("_MetallicGlossMap", Load(Textures + surface + "_MetallicSmoothness.png"));
        m.SetFloat("_Smoothness", 1f); // scales the map's smoothness
        m.SetColor("_BaseColor", tint);
        Finish(m);
    }

    private static void Finish(Material m) => Finish(m, LitGUI.SetMaterialKeywords);

    private static void Finish(Material m, System.Action<Material> shadingModel)
    {
        // URP's own keyword setup, as its material inspector would do.
        BaseShaderGUI.SetMaterialKeywords(m, shadingModel);
        m.enableInstancing = false;
        EditorUtility.SetDirty(m);
    }

    private static Material Get(string path, string shaderName)
    {
        var shader = Shader.Find(shaderName);
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader)
        {
            m.shader = shader;
        }
        return m;
    }

    private static Texture Load(string path)
    {
        var t = AssetDatabase.LoadAssetAtPath<Texture>(path);
        if (t == null) Debug.LogError("Palace texture missing: " + path);
        return t;
    }
}

/// <summary>
/// Import settings for the palace textures (normal maps as normal maps, packed maps as linear data) and the bombers'
/// character models.
/// </summary>
public sealed class PalaceTextureImport : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        if (!assetPath.Contains("/Resources/Palace/Characters/")) return;
        // Legacy animation: the game plays the clips by name (idle, walk, sprint) with no animator assets. The
        // material comes from PalaceSetup (Materials/Character), so none is imported.
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Legacy;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importCameras = false;
        importer.importLights = false;
    }

    private void OnPreprocessTexture()
    {
        var importer = (TextureImporter)assetImporter;
        if (assetPath.Contains("/Resources/Palace/Characters/"))
        {
            // A palette of flat colour swatches: sampled exactly, so neighbouring swatches don't bleed in.
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            return;
        }
        if (assetPath.Contains("/Resources/Palace/Sprites/"))
        {
            // Sprites rendered by PalaceSetup: one tile per PalaceSprites.PixelsPerUnit, pivot at the tile centre or
            // (characters and bombs) at the feet.
            var name = Path.GetFileNameWithoutExtension(assetPath);
            bool top = !(name.StartsWith("bomb") || name.StartsWith("enemy"));
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PalaceSprites.PixelsPerUnit;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = PalaceSprites.Pivot(top);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            return;
        }
        if (assetPath.Contains("/Resources/Palace/Avatars/"))
        {
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            return;
        }
        if (!assetPath.Contains("/Resources/Palace/Textures/")) return;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.anisoLevel = 4;
        if (assetPath.EndsWith("_Normal.jpg")) importer.textureType = TextureImporterType.NormalMap;
        if (assetPath.EndsWith("_MetallicSmoothness.png")) importer.sRGBTexture = false;
        if (assetPath.Contains("/Fx_"))
        {
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
