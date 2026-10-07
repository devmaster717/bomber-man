using System;
using UnityEngine;

/// <summary>
/// An arena's look (Settings > Arena): its floor, surroundings and lighting. The blocks and the exit that go with it
/// are built by <see cref="PalaceArt"/>, which reads <see cref="Current"/>; bombers, bombs, enemies and power-ups look
/// the same in every theme. Purely visual: the rules never see it.
/// </summary>
public sealed class ArenaTheme
{
    /// <summary>The royal palace: checkered marble, black-marble pillars, gold, a red carpet under warm candlelight.</summary>
    public static readonly ArenaTheme Palace = new ArenaTheme
    {
        Index = 0, Key = "palace", Name = "Palace",
        Floor = "Floor", FloorTiles = 6, LightSquare = new Vector2(1, 0), // the texture is a 6 x 6 marble checker
        Ground = "Carpet", GroundTiles = 2.5f,
        Backdrop = new Color(0.05f, 0.07f, 0.14f),
        Sun = new Color(1f, 0.92f, 0.8f), SunIntensity = 1.25f, SunAngles = new Vector2(55f, -32f),
        Ambient = new Color(0.36f, 0.3f, 0.27f), AmbientFromAbove = new Color(0.3f, 0.27f, 0.25f),
        SkyTop = new Color(0.95f, 0.78f, 0.58f), SkyHorizon = new Color(0.3f, 0.17f, 0.1f), SkyBelow = new Color(0.08f, 0.03f, 0.02f),
        Indoors = true,
        BloomTint = new Color(1f, 0.93f, 0.82f), VignetteColour = new Color(0.02f, 0.02f, 0.06f),
        Burst = new Color(1.8f, 1.4f, 0.7f),
    };

    /// <summary>A Three Kingdoms fortress: grey stone courtyard, grey-brick ramparts, red lacquered columns, bronze.</summary>
    public static readonly ArenaTheme Fortress = new ArenaTheme
    {
        Index = 1, Key = "fortress", Name = "Fortress",
        Floor = "Paving", FloorTiles = 2, DarkTint = new Color(0.8f, 0.78f, 0.76f),
        Ground = "Earth", GroundTiles = 3f,
        Backdrop = new Color(0.12f, 0.09f, 0.07f),
        Sun = new Color(1f, 0.84f, 0.64f), SunIntensity = 1.35f, SunAngles = new Vector2(48f, -40f),
        Ambient = new Color(0.34f, 0.3f, 0.26f), AmbientFromAbove = new Color(0.3f, 0.27f, 0.22f),
        SkyTop = new Color(0.9f, 0.8f, 0.64f), SkyHorizon = new Color(0.5f, 0.36f, 0.24f), SkyBelow = new Color(0.14f, 0.09f, 0.06f),
        BloomTint = new Color(1f, 0.9f, 0.75f), VignetteColour = new Color(0.06f, 0.03f, 0.02f),
        Burst = new Color(1.6f, 1.2f, 0.75f),
    };

    /// <summary>A classical Chinese garden: striped lawn, scholar rocks, white walls under grey tiles, bamboo planters.</summary>
    public static readonly ArenaTheme Garden = new ArenaTheme
    {
        Index = 2, Key = "garden", Name = "Garden",
        Floor = "Lawn", FloorTiles = 2, LightTint = new Color(0.74f, 0.8f, 0.72f), DarkTint = new Color(0.6f, 0.7f, 0.58f),
        Ground = "Pebbles", GroundTiles = 2f,
        Backdrop = new Color(0.05f, 0.1f, 0.07f),
        Sun = new Color(1f, 0.97f, 0.9f), SunIntensity = 1.3f, SunAngles = new Vector2(55f, -28f),
        Ambient = new Color(0.33f, 0.36f, 0.35f), AmbientFromAbove = new Color(0.26f, 0.3f, 0.33f),
        SkyTop = new Color(0.72f, 0.86f, 1f), SkyHorizon = new Color(0.36f, 0.46f, 0.36f), SkyBelow = new Color(0.08f, 0.12f, 0.06f),
        BloomTint = new Color(1f, 1f, 0.92f), VignetteColour = new Color(0.02f, 0.05f, 0.03f),
        Burst = new Color(0.9f, 1.7f, 0.6f),
    };

    /// <summary>A frozen citadel: frosted flagstones, ice crystals, snow-capped walls, snowy crates, silver.</summary>
    public static readonly ArenaTheme Frozen = new ArenaTheme
    {
        Index = 3, Key = "frozen", Name = "Frozen",
        // Frosted flagstones: the courtyard's paving in a cold tint (a sheet of ice read as a swimming pool).
        Floor = "Paving", FloorTiles = 1, LightTint = new Color(0.66f, 0.76f, 0.95f), DarkTint = new Color(0.56f, 0.66f, 0.86f),
        Ground = "Snow", GroundTiles = 2f,
        Backdrop = new Color(0.06f, 0.09f, 0.15f),
        Sun = new Color(0.9f, 0.95f, 1.05f), SunIntensity = 1.2f, SunAngles = new Vector2(45f, -25f),
        Ambient = new Color(0.33f, 0.37f, 0.46f), AmbientFromAbove = new Color(0.3f, 0.34f, 0.42f),
        SkyTop = new Color(0.86f, 0.93f, 1.05f), SkyHorizon = new Color(0.4f, 0.5f, 0.66f), SkyBelow = new Color(0.24f, 0.29f, 0.38f),
        BloomTint = new Color(0.86f, 0.93f, 1f), VignetteColour = new Color(0.02f, 0.03f, 0.09f),
        Burst = new Color(1.2f, 1.6f, 2.1f),
    };

    public static readonly ArenaTheme[] All = { Palace, Fortress, Garden, Frozen };

    /// <summary>The theme the arena views and <see cref="PalaceArt"/> build with.</summary>
    public static ArenaTheme Current { get; set; } = Palace;

    /// <summary>The theme saved as <paramref name="index"/> (Palace for anything unknown).</summary>
    public static ArenaTheme At(int index) => index >= 0 && index < All.Length ? All[index] : Palace;

    /// <summary>Makes this the current theme until the returned scope is disposed.</summary>
    public IDisposable Use()
    {
        var previous = Current;
        Current = this;
        return new Scope(() => Current = previous);
    }

    public int Index { get; private set; }
    public string Key { get; private set; }
    public string Name { get; private set; }

    /// <summary>Prefix of this theme's 2D sprites: none for the palace, whose sprites came first.</summary>
    public string SpritePrefix => this == Palace ? "" : Key + "-";

    // Floor: a material asset whose texture spans FloorTiles tiles; the 2D sprites show the squares at LightSquare and
    // DarkSquare (in tiles), and both views tint alternate tiles for a checker.
    public string Floor { get; private set; }
    public int FloorTiles { get; private set; }
    public Vector2 LightSquare { get; private set; }
    public Vector2 DarkSquare { get; private set; }
    public Color LightTint { get; private set; } = Color.white;
    public Color DarkTint { get; private set; } = Color.white;

    /// <summary>What lies around the arena in the 3D view, and how many tiles one copy of its texture spans.</summary>
    public string Ground { get; private set; }
    public float GroundTiles { get; private set; }

    /// <summary>The colour beyond everything (the 2D view's margins, the 3D view's far distance).</summary>
    public Color Backdrop { get; private set; }

    // Lighting: the sun, ambient light, and the surroundings that metal and polish reflect.
    public Color Sun { get; private set; }
    public float SunIntensity { get; private set; }
    public Vector2 SunAngles { get; private set; }
    public Color Ambient { get; private set; }
    public Color AmbientFromAbove { get; private set; }
    public Color SkyTop { get; private set; }
    public Color SkyHorizon { get; private set; }
    public Color SkyBelow { get; private set; }
    /// <summary>Indoors, reflections show chandeliers and a band of windows; outdoors, open sky.</summary>
    public bool Indoors { get; private set; }

    public Color BloomTint { get; private set; }
    public Color VignetteColour { get; private set; }

    /// <summary>The sparkle when a breakable block goes (colours above 1 bloom).</summary>
    public Color Burst { get; private set; }

    private sealed class Scope : IDisposable
    {
        private Action _end;
        public Scope(Action end) => _end = end;

        public void Dispose()
        {
            _end?.Invoke();
            _end = null;
        }
    }
}
