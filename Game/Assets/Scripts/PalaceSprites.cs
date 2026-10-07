using System;
using System.Collections.Generic;
using BombArena.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The 2D view's sprites, rendered from the palace's 3D models (<see cref="PalaceArt"/>) so both views match: blocks,
/// the exit and power-ups from straight above, characters and bombs from a classic three-quarter angle. They are
/// rendered in the editor before each build (PalaceSetup) and saved under Resources/Palace/Sprites, because quick
/// back-to-back render-to-texture is unreliable on some phones; the game only loads them.
/// One tile is one unit; character sprites are 1.25 units tall with their feet at the pivot.
/// </summary>
public static class PalaceSprites
{
    public const string Folder = "Palace/Sprites/";
    public const int PixelsPerUnit = 192;
    private const float CharacterHeight = 1.25f, CharacterPitch = 50f;
    private const int BakeLayer = 31;

    /// <summary>Where a sprite's pivot sits: tile centre for top views, the feet for characters and bombs.</summary>
    public static Vector2 Pivot(bool top) =>
        top ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, (CharacterHeight / 2f - 0.45f * Mathf.Cos(CharacterPitch * Mathf.Deg2Rad)) / CharacterHeight);

    /// <summary>Every sprite the 2D view uses: its name, how to build its model, and whether it is seen from above.</summary>
    public static IEnumerable<(string key, Func<Transform, Transform> build, bool top)> All()
    {
        yield return ("floor-dark", t => FloorSquare(t, true), true);
        yield return ("floor-light", t => FloorSquare(t, false), true);
        yield return ("pillar", PalaceArt.Pillar, true);
        yield return ("wall", PalaceArt.Wall, true);
        yield return ("crate", PalaceArt.Crate, true);
        foreach (bool open in new[] { false, true })
            yield return (open ? "exit-open" : "exit-shut", t =>
            {
                var exit = PalaceArt.Exit(t, out var inner);
                inner.sharedMaterial = PalaceArt.ExitInner(open);
                return exit;
            }, true);
        foreach (PowerUpKind kind in Enum.GetValues(typeof(PowerUpKind)))
            yield return ("power-" + kind, t => PalaceArt.PowerUp(t, kind), true);
        foreach (bool remote in new[] { false, true })
            yield return (remote ? "bomb-remote" : "bomb", t =>
            {
                var bomb = PalaceArt.Bomb(t, out var body);
                body.sharedMaterial = PalaceArt.BombMaterial(remote);
                return bomb;
            }, false);
        for (int slot = 0; slot < 3; slot++)
        {
            int s = slot;
            yield return ("bomber-" + s, t =>
            {
                var rig = PalaceArt.Bomber(t, s);
                rig.Root.localScale = Vector3.one * 1.15f;
                rig.Root.localRotation = Quaternion.Euler(0f, 180f, 0f); // facing the viewer
                return rig.Root;
            }, false);
        }
        foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
            yield return ("enemy-" + kind, t =>
            {
                var rig = PalaceArt.Enemy(t, kind);
                rig.Root.localRotation = Quaternion.Euler(0f, 180f, 0f);
                return rig.Root;
            }, false);
    }

    public static Sprite Floor(bool dark) => Get(dark ? "floor-dark" : "floor-light");
    public static Sprite Pillar => Get("pillar");
    public static Sprite Wall => Get("wall");
    public static Sprite Crate => Get("crate");
    public static Sprite Exit(bool open) => Get(open ? "exit-open" : "exit-shut");
    public static Sprite PowerUp(PowerUpKind kind) => Get("power-" + kind);
    public static Sprite Bomb(bool remote) => Get(remote ? "bomb-remote" : "bomb");
    public static Sprite Bomber(int slot) => Get("bomber-" + Mathf.Clamp(slot, 0, 2));
    public static Sprite Enemy(EnemyKind kind) => Get("enemy-" + kind);

    private static readonly Dictionary<string, Sprite> Loaded = new Dictionary<string, Sprite>();

    private static Sprite Get(string key)
    {
        if (Loaded.TryGetValue(key, out var sprite) && sprite != null) return sprite;
        sprite = Resources.Load<Sprite>(Folder + key);
        if (sprite == null)
        {
            // Not rendered yet (a fresh checkout in the editor): render it now so the view still works.
            foreach (var (k, build, top) in All())
                if (k == key)
                {
                    var texture = Render(build, top);
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Pivot(top), PixelsPerUnit);
                }
        }
        return Loaded[key] = sprite;
    }

    // The floor texture holds 6 x 6 squares; its bottom-left square is black, the one beside it cream.
    private static Transform FloorSquare(Transform parent, bool dark)
    {
        var material = new Material(PalaceArt.Mat("Floor"))
        {
            mainTextureScale = new Vector2(1f / 6f, 1f / 6f),
            mainTextureOffset = new Vector2(dark ? 0f : 1f / 6f, 0f),
        };
        var quad = PalaceArt.Part(parent, PalaceArt.Primitive(PrimitiveType.Quad), material, Vector3.zero, Vector3.one, false);
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        return quad.transform;
    }

    /// <summary>
    /// Builds a model far from everything on its own layer and photographs it with an orthographic camera onto a
    /// transparent background: from straight above for <paramref name="top"/>, else from the three-quarter angle.
    /// </summary>
    public static Texture2D Render(Func<Transform, Transform> build, bool top) => top
        ? Render(build, PixelsPerUnit, PixelsPerUnit, 0.5f, Vector3.zero, 90f)
        : Render(build, PixelsPerUnit, Mathf.RoundToInt(PixelsPerUnit * CharacterHeight), CharacterHeight / 2f, new Vector3(0f, 0.45f, 0f), CharacterPitch);

    /// <summary>
    /// Photographs a model: the camera looks at <paramref name="aim"/>, tilted down by <paramref name="pitch"/>
    /// degrees, showing <paramref name="orthographicSize"/> units above and below it.
    /// </summary>
    public static Texture2D Render(Func<Transform, Transform> build, int width, int height, float orthographicSize, Vector3 aim, float pitch)
    {
        PalaceArt.Light();
        var stage = new GameObject("Sprite bake").transform;
        stage.position = new Vector3(5000f, 5000f, 5000f);
        build(stage);
        SetLayer(stage, BakeLayer);

        var camera = new GameObject("Sprite camera").AddComponent<Camera>();
        camera.transform.SetParent(stage, false);
        camera.orthographic = true;
        camera.cullingMask = 1 << BakeLayer;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.allowHDR = false;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 20f;
        var data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = false;
        camera.orthographicSize = orthographicSize;
        var rotation = Quaternion.Euler(pitch, 0f, 0f);
        camera.transform.localPosition = aim - rotation * Vector3.forward * 10f;
        camera.transform.localRotation = rotation;
        camera.aspect = (float)width / height;

        // Render with 4x MSAA, then resolve into a plain texture before reading it back.
        var msaa = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var resolved = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        var request = new RenderPipeline.StandardRequest { destination = msaa };
        if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
        else
        {
            camera.targetTexture = msaa;
            camera.Render();
            camera.targetTexture = null;
        }
        Graphics.Blit(msaa, resolved);

        var previous = RenderTexture.active;
        RenderTexture.active = resolved;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply(true);
        RenderTexture.active = previous;
        msaa.Release();
        resolved.Release();
        PalaceArt.Remove(msaa);
        PalaceArt.Remove(resolved);
        PalaceArt.Remove(stage.gameObject);
        return texture;
    }

    private static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t) SetLayer(child, layer);
    }
}
