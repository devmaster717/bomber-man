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
    private const string ReleaseApkPath = "Builds/BombArena-release.apk";
    private const string BundlePath = "Builds/BombArena.aab";

    /// <summary>The app's version, shown in Settings. Raise <see cref="VersionCode"/> for every Google Play upload.</summary>
    public const string Version = "1.0.0";
    public const int VersionCode = 1;

    [MenuItem("Bomb Arena/Apply Android Player Settings")]
    public static void ApplyPlayerSettings()
    {
        PlayerSettings.companyName = "Bomb Arena";
        PlayerSettings.productName = "Bomb Arena";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.bombarena.game");
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.Android.bundleVersionCode = VersionCode;

        // No Unity splash: Android 12+ shows the app icon on navy while the game loads.
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.backgroundColor = new Color(0.04f, 0.07f, 0.15f);

        // Landscape only (spec section 1).
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        // Android 8.0+, IL2CPP for ARM64 phones and x86_64 emulators (LDPlayer).
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        // Pinned rather than "highest installed": finding the highest makes Unity query sdkmanager online before every
        // build (which hangs when the network does). 36 is installed with Unity and meets Google Play's requirement.
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64;

        // OpenGL ES 3 only: Unity 6's default Vulkan stalls at startup on LDPlayer (see issue #1).
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

        // Everything is created from code at runtime, so engine stripping cannot see which classes are used;
        // with it on, AddComponent fails on device ("Could not produce class with ID 115").
        PlayerSettings.stripEngineCode = false;

        EditorUserBuildSettings.buildAppBundle = false;
        PalaceSetup.Run();
        AppIcon.Ensure();
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
        if (System.Environment.GetEnvironmentVariable("BOMBARENA_RELEASE") == "1") return BuildRelease();
        return Build(ApkPath, bundle: false);
    }

    /// <summary>
    /// A release: a signed .aab for Google Play (with debug symbols for its crash reports) and a signed .apk for
    /// side-loading. The keystore and passwords come from environment variables (see issue #42) and are cleared from
    /// the project's settings afterwards, so nothing secret is saved.
    /// </summary>
    private static bool BuildRelease()
    {
        string keystore = System.Environment.GetEnvironmentVariable("BOMBARENA_KEYSTORE");
        string storePass = System.Environment.GetEnvironmentVariable("BOMBARENA_KEYSTORE_PASS");
        string alias = System.Environment.GetEnvironmentVariable("BOMBARENA_KEY_ALIAS");
        string keyPass = System.Environment.GetEnvironmentVariable("BOMBARENA_KEY_PASS");
        if (string.IsNullOrEmpty(keystore) || !File.Exists(keystore) || string.IsNullOrEmpty(storePass) || string.IsNullOrEmpty(alias) || string.IsNullOrEmpty(keyPass))
        {
            Debug.LogError("BUILD RESULT: Failed -> a release needs BOMBARENA_KEYSTORE (an existing file), BOMBARENA_KEYSTORE_PASS, BOMBARENA_KEY_ALIAS and BOMBARENA_KEY_PASS");
            return false;
        }
        try
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = keyPass;
            EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;
            return Build(BundlePath, bundle: true) && Build(ReleaseApkPath, bundle: false);
        }
        finally
        {
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.keystoreName = "";
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasName = "";
            PlayerSettings.Android.keyaliasPass = "";
            EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Disabled;
            EditorUserBuildSettings.buildAppBundle = false;
            AssetDatabase.SaveAssets();
        }
    }

    private static bool Build(string path, bool bundle)
    {
        EditorUserBuildSettings.buildAppBundle = bundle;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = path,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        });
        Debug.Log($"BUILD RESULT: {report.summary.result} -> {path} ({report.summary.totalErrors} errors)");
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
