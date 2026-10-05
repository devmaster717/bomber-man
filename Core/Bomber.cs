using System;

namespace BombArena.Core
{
    /// <summary>The character a player controls inside the arena.</summary>
    public sealed class Bomber
    {
        /// <summary>Base speed: 4 tiles per second.</summary>
        public const int BaseTilesPerSecond = 4;

        /// <summary>
        /// Half the side of the square used for touching enemies and fire: about 0.6 tile wide (spec section 4).
        /// </summary>
        public const int HitboxHalf = Units.PerTile * 3 / 10;

        /// <summary>Index of the bomber in its game (player 0, 1, 2).</summary>
        public int Index { get; }

        public TilePos Spawn { get; }

        /// <summary>Centre position in units (see <see cref="Units"/>).</summary>
        public int X { get; private set; }
        public int Y { get; private set; }

        public bool Alive { get; internal set; } = true;

        /// <summary>The tick on which the bomber died, if it has.</summary>
        public long? DiedOnTick { get; internal set; }

        public int SpeedPerTick => Units.SpeedPerTick(BaseTilesPerSecond);

        /// <summary>Bombs the bomber may have on the arena at once.</summary>
        public int MaxBombs => 1;

        /// <summary>Tiles the fire reaches in each direction.</summary>
        public int BlastRange => 1;

        public Bomber(int index, TilePos spawn)
        {
            Index = index;
            Spawn = spawn;
            X = spawn.X * Units.PerTile;
            Y = spawn.Y * Units.PerTile;
        }

        /// <summary>The tile the bomber's centre is in.</summary>
        public TilePos Tile => new TilePos(Units.NearestTile(X), Units.NearestTile(Y));

        internal void Move(Direction direction, Func<int, int, bool> walkable)
        {
            int x = X, y = Y;
            Movement.Step(ref x, ref y, direction, SpeedPerTick, walkable);
            X = x;
            Y = y;
        }

        /// <summary>Whether the bomber's full tile-sized body overlaps the tile.</summary>
        public bool BodyOverlaps(TilePos tile) => Overlaps(X, Y, Units.PerTile / 2, tile);

        /// <summary>Whether the bomber's hitbox (about 0.6 tile) overlaps the tile.</summary>
        public bool HitboxOverlaps(TilePos tile) => Overlaps(X, Y, HitboxHalf, tile);

        internal static bool Overlaps(int x, int y, int half, TilePos tile)
        {
            int tx = tile.X * Units.PerTile, ty = tile.Y * Units.PerTile, h = Units.PerTile / 2;
            return x - half < tx + h && x + half > tx - h && y - half < ty + h && y + half > ty - h;
        }
    }
}
