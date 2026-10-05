using System;
using System.IO;
using BombArena.Core;
using UnityEngine;

/// <summary>Keeps the player profile in a file on the phone (ADR 0002). Writes go to a temp file first so a crash cannot corrupt the save.</summary>
public static class ProfileStore
{
    private static string PathOnDevice => Path.Combine(Application.persistentDataPath, "profile.txt");

    public static PlayerProfile Load()
    {
        try
        {
            return File.Exists(PathOnDevice) ? PlayerProfile.Parse(File.ReadAllText(PathOnDevice)) : NewProfile();
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not read the profile, starting fresh: {e.Message}");
            return NewProfile();
        }
    }

    public static void Save(PlayerProfile profile)
    {
        try
        {
            var tmp = PathOnDevice + ".tmp";
            File.WriteAllText(tmp, profile.Serialize());
            if (File.Exists(PathOnDevice)) File.Replace(tmp, PathOnDevice, null);
            else File.Move(tmp, PathOnDevice);
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not save the profile: {e.Message}");
        }
    }

    public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static PlayerProfile NewProfile()
    {
        var p = new PlayerProfile();
        p.OwnedAvatars.Add(0);
        return p;
    }
}
