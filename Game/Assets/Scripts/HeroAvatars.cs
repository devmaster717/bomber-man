using UnityEngine;

/// <summary>
/// The 10 avatars: portraits of Three Kingdoms heroes, shown next to nicknames (display only). The pictures are made
/// with an image tool (docs/art/avatar-prompts.md), saved under Assets/Art/Avatars, and cut into round, gold-ringed
/// portraits under Resources/Palace/Avatars before each build (PalaceSetup).
/// </summary>
public static class HeroAvatars
{
    public const string Folder = "Palace/Avatars/";
    public const int Count = 10;

    /// <summary>File names of the source pictures in Assets/Art/Avatars, without the extension.</summary>
    public static readonly string[] Keys =
    {
        "0-liubei", "1-guanyu", "2-zhangfei", "3-zhugeliang", "4-zhaoyun",
        "5-caocao", "6-sunquan", "7-lubu", "8-sunshangxiang", "9-diaochan",
    };

    /// <summary>Each hero's kingdom colour: green for Shu, blue for Wei, red for Wu, purple and rose for the others.</summary>
    private static readonly Color[] Backdrops =
    {
        Hex("#1E6B4A"), Hex("#1E6B4A"), Hex("#1E6B4A"), Hex("#1E6B4A"), Hex("#1E6B4A"),
        Hex("#22407A"), Hex("#8E1F28"), Hex("#56307A"), Hex("#8E1F28"), Hex("#B05A7A"),
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
