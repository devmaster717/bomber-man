using System.Collections.Generic;
using BombArena.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The 2D view's sprites, rendered once from the palace's 3D models (<see cref="PalaceArt"/>) so both views match:
/// blocks, the exit and power-ups from straight above, characters and bombs from a classic three-quarter angle.
/// One tile is one unit; character sprites are 1.25 units tall with their feet at the pivot.
/// </summary>
public static class PalaceSprites
{
    private const int PixelsPerUnit = 192;
    private const float CharacterHeight = 1.25f, CharacterPitch = 50f;
    private const int BakeLayer = 31;

    // Where a character's feet fall in its sprite, as a fraction of the height (see Character()).
    private static readonly float FeetPivot = (CharacterHeight / 2f - 0.45f * Mathf.Cos(CharacterPitch * Mathf.Deg2Rad)) / CharacterHeight;

    private static readonly Dictionary<string, Sprite> Baked = new Dictionary<string, Sprite>();
    /// <summary>A floor square of the marble checker, cream or black, lit like the 3D floor.</summary>
    public static Sprite Floor(bool dark) => Get(dark ? "floor-dark" : "floor-light", t =>
    {
        // The floor texture holds 6 x 6 squares; its bottom-left square is black, the one beside it cream.
        var material = new Material(PalaceArt.Mat("Floor"))
        {
            mainTextureScale = new Vector2(1f / 6f, 1f / 6f),
            mainTextureOffset = new Vector2(dark ? 0f : 1f / 6f, 0f),
        };
        var quad = PalaceArt.Part(t, PalaceArt.Primitive(PrimitiveType.Quad), material, Vector3.zero, Vector3.one, false);
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        return quad.transform;
    }, top: true);

    public static Sprite Pillar => Get("pillar", t => PalaceArt.Pillar(t), top: true);
    public static Sprite Wall => Get("wall", t => PalaceArt.Wall(t), top: true);
    public static Sprite Crate => Get("crate", t => PalaceArt.Crate(t), top: true);

    public static Sprite Exit(bool open) => Get(open ? "exit-open" : "exit-shut", t =>
    {
        var exit = PalaceArt.Exit(t, out var inner);
        inner.sharedMaterial = PalaceArt.ExitInner(open);
        return exit;
    }, top: true);

    public static Sprite PowerUp(PowerUpKind kind) => Get("power-" + kind, t => PalaceArt.PowerUp(t, kind), top: true);

    public static Sprite Bomb(bool remote) => Get(remote ? "bomb-remote" : "bomb", t =>
    {
        var bomb = PalaceArt.Bomb(t, out var body);
        body.sharedMaterial = PalaceArt.BombMaterial(remote);
        return bomb;
    }, top: false);

    public static Sprite Bomber(int slot) => Get("bomber-" + slot, t =>
    {
        var rig = PalaceArt.Bomber(t, slot);
        rig.Root.localScale = Vector3.one * 1.15f;
        rig.Root.localRotation = Quaternion.Euler(0f, 180f, 0f); // facing the viewer
        return rig.Root;
    }, top: false);

    public static Sprite Enemy(EnemyKind kind) => Get("enemy-" + kind, t =>
    {
        var rig = PalaceArt.Enemy(t, kind);
        rig.Root.localRotation = Quaternion.Euler(0f, 180f, 0f);
        return rig.Root;
    }, top: false);

    private static Sprite Get(string key, System.Func<Transform, Transform> build, bool top)
    {
        if (Baked.TryGetValue(key, out var sprite) && sprite != null) return sprite;
        return Baked[key] = Render(build, top);
    }

    /// <summary>
    /// Builds a model far from the arena on its own layer and photographs it with an orthographic camera onto a
    /// transparent background.
    /// </summary>
    private static Sprite Render(System.Func<Transform, Transform> build, bool top)
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
        camera.allowMSAA = true;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 20f;
        var data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = false;

        int width = PixelsPerUnit, height;
        if (top)
        {
            // Straight down over one tile.
            camera.orthographicSize = 0.5f;
            camera.transform.localPosition = new Vector3(0f, 10f, 0f);
            camera.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            height = PixelsPerUnit;
        }
        else
        {
            // Tilted down at the model's middle, framing a tile's width and a little more than its height.
            camera.orthographicSize = CharacterHeight / 2f;
            var aim = new Vector3(0f, 0.45f, 0f);
            var rotation = Quaternion.Euler(CharacterPitch, 0f, 0f);
            camera.transform.localPosition = aim - rotation * Vector3.forward * 10f;
            camera.transform.localRotation = rotation;
            height = Mathf.RoundToInt(PixelsPerUnit * CharacterHeight);
        }

        var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        camera.aspect = (float)width / height;
        var request = new RenderPipeline.StandardRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
        else
        {
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = null;
        }

        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply(true, true);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        PalaceArt.Remove(stage.gameObject);

        var pivot = top ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, FeetPivot);
        return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, PixelsPerUnit);
    }

    private static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t) SetLayer(child, layer);
    }
}
