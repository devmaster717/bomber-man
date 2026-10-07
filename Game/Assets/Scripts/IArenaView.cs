using BombArena.Core;

/// <summary>
/// Draws a game's state and points the camera at it. The flat 2D view and the 3D view are interchangeable
/// (Settings > View); neither holds any rules.
/// </summary>
public interface IArenaView
{
    /// <summary>Call after each core tick, so movement can be interpolated between ticks.</summary>
    void OnTick();

    /// <summary>Redraws the state; <paramref name="t"/> is the fraction of the way to the next tick.</summary>
    void Draw(float t);

    /// <summary>Moves the camera to follow a bomber, never showing more than needed outside the walls.</summary>
    void Follow(int bomber);

    void Destroy();
}

public static class ArenaView
{
    /// <summary>A view of <paramref name="game"/> in <paramref name="theme"/>, which becomes the current theme.</summary>
    public static IArenaView Create(Game game, bool threeD, ArenaTheme theme)
    {
        ArenaTheme.Current = theme;
        return threeD ? new ArenaRenderer3D(game) : (IArenaView)new ArenaRenderer(game);
    }
}
