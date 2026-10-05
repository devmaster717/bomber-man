using BombArena.Core;
using UnityEngine;

/// <summary>
/// Flat placeholder art generated in code, so the first builds need no downloaded assets.
/// Swap for real sprites later without touching the game core.
/// </summary>
public static class PlaceholderSprites
{
    private const int Size = 16;

    private static Sprite _floor, _hard, _soft;

    public static Sprite For(Tile tile) => tile switch
    {
        Tile.HardBlock => _hard ??= Make(HardPixel),
        Tile.SoftBlock => _soft ??= Make(SoftPixel),
        _ => _floor ??= Make(FloorPixel),
    };

    public static Sprite Bomber => BomberAvatar(0);

    private static readonly Sprite[] _avatars = new Sprite[PlayerProfile.AvatarCount];

    // Shirt, feet and hair colours for the 10 avatars.
    private static readonly string[,] AvatarColours =
    {
        { "#F2F2F2", "#2D5BD8", "#FFE0C2" }, { "#E04B4B", "#3A2A1A", "#FFE0C2" }, { "#2FA37A", "#1B4332", "#6B4226" },
        { "#F2C230", "#7A4E00", "#C68642" }, { "#8E44C9", "#F2F2F2", "#FFE0C2" }, { "#FF8C42", "#1D3557", "#C68642" },
        { "#1D3557", "#E63946", "#FFE0C2" }, { "#EC6FB1", "#5A189A", "#8D5524" }, { "#4CC9F0", "#3A0CA3", "#FFE0C2" },
        { "#2B2B2B", "#F2C230", "#E0AC69" },
    };

    public static Sprite BomberAvatar(int avatar)
    {
        int a = Mathf.Clamp(avatar, 0, PlayerProfile.AvatarCount - 1);
        return _avatars[a] ??= Make((x, y) => BomberPixel(x, y, Hex(AvatarColours[a, 0]), Hex(AvatarColours[a, 1]), Hex(AvatarColours[a, 2])));
    }
    public static Sprite Bomb => _bomb ??= Make(BombPixel);
    public static Sprite Fire => _fire ??= Make(FirePixel);

    private static Sprite _bomb, _fire, _exitShut, _exitOpen;
    private static readonly Sprite[] _powerUps = new Sprite[4];

    public static Sprite PowerUp(PowerUpKind kind) =>
        _powerUps[(int)kind] ??= Make((x, y) => PowerUpPixel(x, y, kind));

    // A rounded panel with a symbol per kind.
    private static Color PowerUpPixel(int x, int y, PowerUpKind kind)
    {
        if (x == 0 || y == 0 || x == Size - 1 || y == Size - 1) return Color.clear;
        bool border = x == 1 || y == 1 || x == Size - 2 || y == Size - 2;
        var panel = kind switch
        {
            PowerUpKind.FireUp => Hex("#C2410C"),
            PowerUpKind.BombUp => Hex("#1D4ED8"),
            PowerUpKind.RemoteControl => Hex("#7E22CE"),
            _ => Hex("#15803D"),
        };
        if (border) return Hex("#FDE68A");
        bool symbol = kind switch
        {
            // flame: a teardrop
            PowerUpKind.FireUp => (x - 7.5f) * (x - 7.5f) + (y - 6f) * (y - 6f) <= 9f || (y > 6 && y < 12 && System.Math.Abs(x - 7.5f) <= (12 - y) * 0.6f),
            // bomb with a plus
            PowerUpKind.BombUp => (x - 6f) * (x - 6f) + (y - 6f) * (y - 6f) <= 9f || (x == 11 && y >= 9 && y <= 13) || (y == 11 && x >= 9 && x <= 13),
            // antenna and box
            PowerUpKind.RemoteControl => (x >= 5 && x <= 10 && y >= 3 && y <= 8) || (x == 8 && y > 8 && y <= 12) || (y == 12 && x >= 7 && x <= 9),
            // arrow pointing right
            _ => (y >= 6 && y <= 9 && x >= 3 && x <= 9) || (x >= 9 && x <= 12 && System.Math.Abs(y - 7.5f) <= 12 - x + 0.5f),
        };
        return symbol ? Color.white : panel;
    }

    public static Sprite Exit(bool open) => open ? _exitOpen ??= Make((x, y) => ExitPixel(x, y, true)) : _exitShut ??= Make((x, y) => ExitPixel(x, y, false));

    private static Color ExitPixel(int x, int y, bool open)
    {
        bool frame = x <= 1 || x >= 14 || y >= 14;
        if (frame) return Hex("#D9C27A");
        if (open) return (x + y) % 3 == 0 ? Hex("#FFF6C2") : Hex("#F2D45C");
        return x == 10 && y == 7 ? Hex("#D9C27A") : Hex("#3B2A1A");
    }
    private static readonly Sprite[] _enemies = new Sprite[4];

    public static Sprite Enemy(EnemyKind kind)
    {
        int i = (int)kind;
        if (_enemies[i] != null) return _enemies[i];
        var body = kind switch
        {
            EnemyKind.Runner => Hex("#E0383E"),
            EnemyKind.Phantom => new Color(0.85f, 0.9f, 1f, 0.85f),
            EnemyKind.WallPasser => Hex("#2FA37A"),
            _ => Hex("#8E44C9"),
        };
        return _enemies[i] = Make((x, y) => EnemyPixel(x, y, body, kind == EnemyKind.Phantom));
    }

    private static Color EnemyPixel(int x, int y, Color body, bool ghostTail)
    {
        float dx = x - 7.5f, dy = y - 8f;
        bool inBody = dx * dx + dy * dy <= 36f || (y >= 2 && y <= 8 && x >= 2 && x <= 13);
        if (ghostTail && y < 4 && (x % 4 == 1 || x % 4 == 2)) inBody = false;
        if (!inBody || y < 2) return Color.clear;
        if (y >= 9 && y <= 10 && (x == 5 || x == 6 || x == 9 || x == 10)) return Color.white;
        if (y == 9 && (x == 6 || x == 10)) return Hex("#111111");
        return body;
    }

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

    private static Color BomberPixel(int x, int y, Color shirt, Color feet, Color skin)
    {
        float cx = 7.5f, dx = x - cx;
        // head
        float hy = y - 11f;
        if (dx * dx + hy * hy <= 12f)
        {
            bool eye = y == 11 && (x == 6 || x == 9);
            return eye ? Hex("#1B1B1B") : skin;
        }
        // body
        if (y >= 3 && y <= 7 && x >= 4 && x <= 11) return shirt;
        // feet
        if (y >= 1 && y <= 2 && (x is >= 4 and <= 6 || x is >= 9 and <= 11)) return feet;
        return Color.clear;
    }
}
