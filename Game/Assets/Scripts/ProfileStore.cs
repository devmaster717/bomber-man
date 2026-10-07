using System;
using System.IO;
using BombArena.Core;
using UnityEngine;

/// <summary>
/// Keeps the player profile in a file on the phone (ADR 0002). Each save is sealed with a checksum
/// (<see cref="ProfileFile"/>), written to a temporary file and swapped in, and the previous save is kept as a backup;
/// if the main file is damaged, the backup loads instead.
/// </summary>
public static class ProfileStore
{
    private static string MainPath => Path.Combine(Application.persistentDataPath, "profile.txt");
    private static string BackupPath => MainPath + ".bak";

    public static PlayerProfile Load()
    {
        foreach (var path in new[] { MainPath, BackupPath })
        {
            try
            {
                if (!File.Exists(path)) continue;
                if (ProfileFile.TryOpen(File.ReadAllText(path), out var body)) return PlayerProfile.Parse(body);
                Debug.LogError($"The save {Path.GetFileName(path)} is damaged; trying the backup.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not read {Path.GetFileName(path)}: {e.Message}");
            }
        }
        return NewProfile();
    }

    public static void Save(PlayerProfile profile)
    {
        try
        {
            var tmp = MainPath + ".tmp";
            File.WriteAllText(tmp, ProfileFile.Seal(profile.Serialize()));
            if (File.Exists(MainPath)) File.Replace(tmp, MainPath, BackupPath);
            else File.Move(tmp, MainPath);
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
