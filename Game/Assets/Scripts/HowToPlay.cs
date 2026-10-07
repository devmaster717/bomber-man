using System;
using BombArena.Core;
using UnityEngine;

/// <summary>
/// The How to play screen: a few illustrated pages (sprites from the palace models) on moving and bombing, crates and
/// the exit, power-ups, enemies, lives and stars, and Bluetooth battles. Offered once after first launch and always
/// reachable from Home.
/// </summary>
public sealed class HowToPlay
{
    private int _page;

    private static readonly (string title, string text, Func<Sprite[]> pictures)[] Pages =
    {
        (Text.HowMoveTitle, Text.HowMove, () => new[] { PalaceSprites.Bomber(0), PalaceSprites.Bomb(false) }),
        (Text.HowExitTitle, Text.HowExit, () => new[] { PalaceSprites.Crate, PalaceSprites.Exit(true) }),
        (Text.HowPowerTitle, Text.HowPower, () => new[]
        {
            PalaceSprites.PowerUp(PowerUpKind.FireUp), PalaceSprites.PowerUp(PowerUpKind.BombUp),
            PalaceSprites.PowerUp(PowerUpKind.RemoteControl), PalaceSprites.PowerUp(PowerUpKind.SpeedUp),
        }),
        (Text.HowEnemiesTitle, Text.HowEnemies, () => new[]
        {
            PalaceSprites.Enemy(EnemyKind.Walker), PalaceSprites.Enemy(EnemyKind.Runner),
            PalaceSprites.Enemy(EnemyKind.Phantom), PalaceSprites.Enemy(EnemyKind.WallPasser),
        }),
        (Text.HowLivesTitle, Text.HowLives, () => new[] { PalaceSprites.Pillar }),
        (Text.HowBattleTitle, Text.HowBattle, () => new[] { PalaceSprites.Bomber(0), PalaceSprites.Bomber(1), PalaceSprites.Bomber(2) }),
    };

    /// <summary>Starts again from the first page.</summary>
    public void Reset() => _page = 0;

    /// <summary>Draws the current page; returns true when the player is done.</summary>
    public bool Draw()
    {
        float w = Ui.W, u = Ui.U;
        var (title, text, pictures) = Pages[_page];
        GUI.Label(new Rect(0, 3 * u, w, 10 * u), Text.HowToPlay, Ui.Title);
        GUI.Label(new Rect(0, 14 * u, w, 7 * u), title, Ui.Label);

        // The pictures in a row, each in a gold frame.
        var sprites = pictures();
        float size = 20 * u, gap = 3 * u, total = sprites.Length * size + (sprites.Length - 1) * gap;
        for (int i = 0; i < sprites.Length; i++)
        {
            var r = new Rect((w - total) / 2 + i * (size + gap), 23 * u, size, size);
            GUI.Box(r, GUIContent.none);
            if (sprites[i] != null)
                GUI.DrawTexture(new Rect(r.x + size * 0.1f, r.y + size * 0.1f, size * 0.8f, size * 0.8f), sprites[i].texture, ScaleMode.ScaleToFit);
        }

        GUI.Label(new Rect(w * 0.12f, 46 * u, w * 0.76f, 30 * u), text, Ui.Small);
        GUI.Label(new Rect(0, 77 * u, w, 5 * u), $"{_page + 1} / {Pages.Length}", Ui.Small);

        GUI.enabled = _page > 0;
        if (GUI.Button(new Rect(w * 0.18f, 85 * u, w * 0.18f, 10 * u), Text.Prev, Ui.Button)) _page--;
        GUI.enabled = true;
        bool last = _page == Pages.Length - 1;
        if (GUI.Button(new Rect(w * 0.64f, 85 * u, w * 0.18f, 10 * u), last ? Text.Done : Text.Next, Ui.Button))
        {
            if (last) return true;
            _page++;
        }
        return GUI.Button(new Rect(w * 0.41f, 86 * u, w * 0.18f, 8 * u), Text.Skip, Ui.SmallButton);
    }
}
