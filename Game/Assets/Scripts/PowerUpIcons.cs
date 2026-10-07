using System;
using BombArena.Core;
using UnityEngine;

/// <summary>
/// Smooth white power-up symbols on a transparent background, drawn in code with 4x4 supersampling: a flame (Fire Up),
/// a bomb with a plus (Bomb Up), a remote with an antenna (Remote) and a winged arrow (Speed Up).
/// </summary>
public static class PowerUpIcons
{
    private const int Size = 96;
    private static readonly Texture2D[] Cache = new Texture2D[4];

    public static Texture2D For(PowerUpKind kind)
    {
        int i = (int)kind;
        if (Cache[i] != null) return Cache[i];
        Func<float, float, bool> inside = kind switch
        {
            PowerUpKind.FireUp => Flame,
            PowerUpKind.BombUp => BombPlus,
            PowerUpKind.RemoteControl => Remote,
            _ => WingedArrow,
        };
        var t = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Icon " + kind };
        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            int hits = 0;
            for (int sy = 0; sy < 4; sy++)
            for (int sx = 0; sx < 4; sx++)
            {
                // u, v in -1..1 with v up.
                float u = (x + (sx + 0.5f) / 4f) / Size * 2f - 1f, v = (y + (sy + 0.5f) / 4f) / Size * 2f - 1f;
                if (inside(u, v)) hits++;
            }
            pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
        }
        t.SetPixels32(pixels);
        t.Apply(true, true);
        return Cache[i] = t;
    }

    private static bool Circle(float u, float v, float cx, float cy, float r) => (u - cx) * (u - cx) + (v - cy) * (v - cy) <= r * r;

    // A teardrop flame with a hollow inner tongue.
    private static bool Flame(float u, float v)
    {
        bool outer = Circle(u, v, 0f, -0.25f, 0.5f) || (v > -0.25f && v < 0.85f && Mathf.Abs(u + 0.12f * Mathf.Sin(v * 3f)) < 0.5f * (0.85f - v) / 1.1f);
        bool inner = Circle(u, v, 0f, -0.35f, 0.22f) || (v > -0.35f && v < 0.25f && Mathf.Abs(u) < 0.22f * (0.25f - v) / 0.6f);
        return outer && !inner;
    }

    // A round bomb with a fuse, and a plus sign.
    private static bool BombPlus(float u, float v)
    {
        bool bomb = Circle(u, v, -0.18f, -0.15f, 0.55f) && !Circle(u, v, -0.35f, 0.05f, 0.12f);
        bool fuse = Mathf.Abs(u - 0.2f) < 0.07f && v > 0.3f && v < 0.55f;
        bool plus = (Mathf.Abs(u - 0.6f) < 0.08f && Mathf.Abs(v - 0.55f) < 0.3f) || (Mathf.Abs(v - 0.55f) < 0.08f && Mathf.Abs(u - 0.6f) < 0.3f);
        return bomb || fuse || plus;
    }

    // A remote: a rounded box with a button, an antenna with a ball.
    private static bool Remote(float u, float v)
    {
        bool box = Mathf.Abs(u) < 0.32f && v > -0.8f && v < 0.15f;
        bool button = Circle(u, v, 0f, -0.2f, 0.13f);
        bool antenna = Mathf.Abs(u - 0.18f) < 0.05f && v >= 0.15f && v < 0.6f;
        bool ball = Circle(u, v, 0.18f, 0.68f, 0.12f);
        return (box && !button) || antenna || ball;
    }

    // An arrow pointing right with two speed lines.
    private static bool WingedArrow(float u, float v)
    {
        bool shaft = Mathf.Abs(v) < 0.12f && u > -0.35f && u < 0.3f;
        bool head = u >= 0.2f && u < 0.75f && Mathf.Abs(v) < (0.75f - u) * 0.95f;
        bool lines = (Mathf.Abs(v - 0.35f) < 0.06f || Mathf.Abs(v + 0.35f) < 0.06f) && u > -0.8f && u < -0.1f;
        return shaft || head || lines;
    }
}
