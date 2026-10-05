using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Android build settings and builds, usable from the menu or from the command line:
/// Unity.exe -batchmode -projectPath Game -buildTarget Android -executeMethod Builds.AndroidBatch
/// </summary>
public static class Builds
{
    private const string ScenePath = "Assets/Scenes/Main.unity";
    private const string ApkPath = "Builds/BombArena.apk";

    [MenuItem("Bomb Arena/Apply Android Player Settings")]
    public static void ApplyPlayerSettings()
    {
        PlayerSettings.companyName = "Bomb Arena";
        PlayerSettings.productName = "Bomb Arena";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.bombarena.game");

        // Landscape only (spec section 1).
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        // Android 8.0+, IL2CPP for ARM64 phones and x86_64 emulators (LDPlayer).
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64;

        // OpenGL ES 3 only: Unity 6's default Vulkan stalls at startup on LDPlayer (see issue #1).
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

        // Everything is created from code at runtime, so engine stripping cannot see which classes are used;
        // with it on, AddComponent fails on device ("Could not produce class with ID 115").
        PlayerSettings.stripEngineCode = false;

        EditorUserBuildSettings.buildAppBundle = false;
        EnsureScene();
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Bomb Arena/Build Android APK")]
    public static void Android() => BuildAndroid();

    /// <summary>Command-line entry point; exits with 0 on success.</summary>
    public static void AndroidBatch() => EditorApplication.Exit(BuildAndroid() ? 0 : 1);

    private static bool BuildAndroid()
    {
        ApplyPlayerSettings();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        });
        Debug.Log($"BUILD RESULT: {report.summary.result} -> {ApkPath} ({report.summary.totalErrors} errors)");
        return report.summary.result == BuildResult.Succeeded;
    }

    /// <summary>The main scene holds the camera and the AppController (which builds everything else).</summary>
    private static void EnsureScene()
    {
        var scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        bool changed = false;
        // GameView is created per attempt by AppController; an old copy in the scene would start a game on its own.
        foreach (var stray in Object.FindObjectsByType<GameView>(FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(stray.gameObject);
            changed = true;
        }
        if (Object.FindFirstObjectByType<AppController>() == null)
        {
            new GameObject("Bomb Arena").AddComponent<AppController>();
            changed = true;
        }
        if (changed || !File.Exists(ScenePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }
}
