using BombArena.Core;
using UnityEngine;

/// <summary>
/// Flat placeholder art generated in code, so the first builds need no downloaded assets.
/// Swap for real sprites later without touching the game core.
/// </summary>
public static class PlaceholderSprites
{
    private const int Size = 16;

    private static Sprite _floor, _hard, _soft, _bomber;

    public static Sprite For(Tile tile) => tile switch
    {
        Tile.HardBlock => _hard ??= Make(HardPixel),
        Tile.SoftBlock => _soft ??= Make(SoftPixel),
        _ => _floor ??= Make(FloorPixel),
    };

    public static Sprite Bomber => _bomber ??= Make(BomberPixel);
    public static Sprite Bomb => _bomb ??= Make(BombPixel);
    public static Sprite Fire => _fire ??= Make(FirePixel);

    private static Sprite _bomb, _fire;

    private static Color BombPixel(int x, int y)
    {
        float dx = x - 7.5f, dy = y - 6.5f;
        if (x >= 9 && x <= 10 && y >= 12 && y <= 14) return Hex("#E04B2A"); // fuse
        if (dx * dx + dy * dy <= 30f)
            return dx < -1.5f && dy > 1.5f ? Hex("#5A5A6A") : Hex("#1E1E26");
        return Color.clear;
    }

    private static Color FirePixel(int x, int y)
    {
        float dx = x - 7.5f, dy = y - 7.5f, d = dx * dx + dy * dy;
        if (d <= 12f) return Hex("#FFF2A8");
        if (d <= 34f) return Hex("#FFB627");
        return new Color(0.95f, 0.35f, 0.1f, 0.9f);
    }

    private delegate Color Painter(int x, int y);

    private static Sprite Make(Painter paint)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            texture.SetPixel(x, y, paint(x, y));
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
    }

    private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

    // Texture y runs bottom-up.
    private static Color FloorPixel(int x, int y) =>
        ((x / 8 + y / 8) % 2 == 0) ? Hex("#3E8E4A") : Hex("#3A8545");

    private static Color HardPixel(int x, int y)
    {
        if (x == 0 || y == Size - 1) return Hex("#A3A3A3");
        if (x == Size - 1 || y == 0) return Hex("#3F3F3F");
        return Hex("#6E6E6E");
    }

    private static Color SoftPixel(int x, int y)
    {
        bool mortarRow = y % 4 == 0;
        bool mortarCol = (x + ((y / 4) % 2 == 0 ? 0 : 4)) % 8 == 0;
        return mortarRow || mortarCol ? Hex("#5C3518") : Hex("#A8642F");
    }

    private static Color BomberPixel(int x, int y)
    {
        float cx = 7.5f, dx = x - cx;
        // head
        float hy = y - 11f;
        if (dx * dx + hy * hy <= 12f)
        {
            bool eye = y == 11 && (x == 6 || x == 9);
            return eye ? Hex("#1B1B1B") : Hex("#FFE0C2");
        }
        // body
        if (y >= 3 && y <= 7 && x >= 4 && x <= 11) return Hex("#F2F2F2");
        // feet
        if (y >= 1 && y <= 2 && (x is >= 4 and <= 6 || x is >= 9 and <= 11)) return Hex("#2D5BD8");
        return Color.clear;
    }
}
