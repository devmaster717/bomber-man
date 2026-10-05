namespace BombArena.Core
{
    /// <summary>
    /// Fixed-point scale and clock shared by every rule. All positions and speeds are integers so the
    /// same inputs give the same result on every device and runtime.
    /// </summary>
    public static class Units
    {
        /// <summary>Position units per tile. A tile's centre sits on a multiple of this value.</summary>
        public const int PerTile = 1000;

        /// <summary>Fixed logic ticks per second.</summary>
        public const int TicksPerSecond = 20;

        /// <summary>Converts a speed in tiles per second to position units per tick.</summary>
        public static int SpeedPerTick(int tilesPerSecond) => tilesPerSecond * PerTile / TicksPerSecond;

        /// <summary>The tile whose centre is nearest to a position on one axis (positions are never negative).</summary>
        public static int NearestTile(int position) => (position + PerTile / 2) / PerTile;
    }
}
