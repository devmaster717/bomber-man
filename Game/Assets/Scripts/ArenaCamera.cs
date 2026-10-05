using BombArena.Core;
using UnityEngine;

/// <summary>The follow camera shared by stage mode and rounds: it never shows space outside the walls.</summary>
public static class ArenaCamera
{
    /// <summary>Most tiles shown vertically; larger arenas scroll.</summary>
    private const float MaxVisibleTilesHigh = 11f;

    public static Camera SetUp(Arena arena)
    {
        var camera = Camera.main;
        if (camera == null)
        {
            camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
        }
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        float tilesHigh = Mathf.Min(MaxVisibleTilesHigh, arena.Height, arena.Width / camera.aspect);
        camera.orthographicSize = tilesHigh / 2f;
        return camera;
    }

    public static void Follow(Camera camera, Arena arena, Vector2 target)
    {
        float halfH = camera.orthographicSize, halfW = halfH * camera.aspect;
        int w = arena.Width, h = arena.Height;
        float x = Clamp(target.x, -0.5f + halfW, w - 0.5f - halfW, (w - 1) / 2f);
        float y = Clamp(target.y, -(h - 0.5f) + halfH, 0.5f - halfH, -(h - 1) / 2f);
        camera.transform.position = new Vector3(x, y, -10f);
    }

    private static float Clamp(float value, float min, float max, float centreIfTooSmall) =>
        min > max ? centreIfTooSmall : Mathf.Clamp(value, min, max);
}
