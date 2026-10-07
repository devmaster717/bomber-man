using UnityEngine;

/// <summary>
/// The 10 avatars: cartoon headshots shown next to nicknames (display only). The pictures come from an image tool
/// (docs/art/avatar-prompts.md) as one sheet of ten under Assets/Art/Avatars, cut into round, gold-ringed portraits
/// under Resources/Palace/Avatars before each build (PalaceSetup).
/// </summary>
public static class PlayerAvatars
{
    public const string Folder = "Palace/Avatars/";
    public const int Count = 10;

    /// <summary>
    /// File names (without extension) of single pictures in Assets/Art/Avatars that replace one cell of the sheet:
    /// avatar 0 is the sheet's top-left cell, 4 its top-right, 5 bottom-left, 9 bottom-right.
    /// </summary>
    public static readonly string[] Keys = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

    /// <summary>Each avatar's background colour on the sheet, used as a plain disc when its picture is missing.</summary>
    private static readonly Color[] Backdrops =
    {
        Hex("#3E6FC9"), Hex("#E08A4A"), Hex("#4FA36A"), Hex("#8A63C9"), Hex("#3E7FD0"),
        Hex("#E3B84F"), Hex("#D9675F"), Hex("#3E6FC9"), Hex("#3FA39A"), Hex("#6F63C9"),
    };

    /// <summary>The colour shown for avatar <paramref name="i"/> while it has no picture.</summary>
    public static Color Backdrop(int i) => Backdrops[Mathf.Clamp(i, 0, Count - 1)];

    private static readonly Texture2D[] Loaded = new Texture2D[Count];

    /// <summary>Avatar <paramref name="i"/>'s round portrait (a plain disc if it hasn't been made).</summary>
    public static Texture2D Portrait(int i)
    {
        i = Mathf.Clamp(i, 0, Count - 1);
        if (Loaded[i] != null) return Loaded[i];
        var t = Resources.Load<Texture2D>(Folder + "avatar-" + i);
        if (t == null)
        {
            t = new Texture2D(2, 2);
            t.SetPixels(new[] { Backdrops[i], Backdrops[i], Backdrops[i], Backdrops[i] });
            t.Apply();
        }
        return Loaded[i] = t;
    }

    private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
}
