namespace BombArena.Core
{
    /// <summary>The character a player controls inside the arena.</summary>
    public sealed class Bomber
    {
        /// <summary>Base speed: 4 tiles per second.</summary>
        public const int BaseTilesPerSecond = 4;

        /// <summary>Centre position in units (see <see cref="Units"/>).</summary>
        public int X { get; private set; }
        public int Y { get; private set; }

        public int SpeedPerTick { get; } = Units.SpeedPerTick(BaseTilesPerSecond);

        public Bomber(TilePos spawn)
        {
            X = spawn.X * Units.PerTile;
            Y = spawn.Y * Units.PerTile;
        }

        /// <summary>The tile the bomber's centre is in.</summary>
        public TilePos Tile => new TilePos(Units.NearestTile(X), Units.NearestTile(Y));

        internal void Move(Direction direction, Arena arena)
        {
            int x = X, y = Y;
            Movement.Step(ref x, ref y, direction, SpeedPerTick, arena.IsWalkable);
            X = x;
            Y = y;
        }
    }
}
