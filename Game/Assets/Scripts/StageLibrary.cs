using BombArena.Core;
using UnityEngine;

/// <summary>Loads stage data files from Resources/Stages (written by Tools/StageGenerator, tuned by hand).</summary>
public static class StageLibrary
{
    public static int Count => StageTable.StageCount;

    public static StageSpec Load(int number)
    {
        var asset = Resources.Load<TextAsset>("Stages/" + StageFile.FileName(number));
        if (asset == null)
        {
            Debug.LogError($"Stage file {StageFile.FileName(number)} is missing; using the generated default.");
            return StageTable.Spec(number);
        }
        return StageFile.Parse(asset.text, number);
    }
}
