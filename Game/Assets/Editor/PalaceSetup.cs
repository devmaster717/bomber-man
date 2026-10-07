using System.IO;
using System.Linq;
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
    }

    /// <summary>
    /// Renders the 2D view's sprites from the 3D models (PalaceSprites.All) in a scratch scene and saves them as PNGs;
    /// a file is only rewritten when its picture changed, so builds don't touch unchanged sprites.
    /// </summary>
    private static void BakeSprites()
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Directory.CreateDirectory(Sprites);
        foreach (var (key, build, top) in PalaceSprites.All())
        {
            var texture = PalaceSprites.Render(build, top);
            var png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            var path = Sprites + key + ".png";
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(png)) File.WriteAllBytes(path, png);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
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
        foreach (var name in new[] { "Floor", "Marble", "Stone", "Wood", "Gold", "Carpet" })
            SetUp(Lit(name), name);

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

    private static void SetUp(Material m, string name)
    {
        m.SetTexture("_BaseMap", Load(Textures + name + "_Color.jpg"));
        m.SetTexture("_BumpMap", Load(Textures + name + "_Normal.jpg"));
        m.SetFloat("_BumpScale", 1f);
        m.SetTexture("_MetallicGlossMap", Load(Textures + name + "_MetallicSmoothness.png"));
        m.SetFloat("_Smoothness", 1f); // scales the map's smoothness
        m.SetColor("_BaseColor", Color.white);
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

/// <summary>Import settings for the palace textures: normal maps as normal maps, packed maps as linear data.</summary>
public sealed class PalaceTextureImport : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        var importer = (TextureImporter)assetImporter;
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
