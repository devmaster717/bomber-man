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
        public int X { get; internal set; }
        public int Y { get; internal set; }

        public bool Alive { get; internal set; } = true;

        /// <summary>The tick on which the bomber died, if it has.</summary>
        public long? DiedOnTick { get; internal set; }

        /// <summary>Most bombs a bomber can have on the arena at once, however many Bomb Ups it holds.</summary>
        public const int BombCap = 4;

        /// <summary>Speed Up lasts 60 seconds.</summary>
        public const int SpeedUpTicks = 60 * Units.TicksPerSecond;

        public bool HasFireUp { get; internal set; }
        public int BombUps { get; internal set; }
        public bool HasRemoteControl { get; internal set; }

        /// <summary>Ticks of Speed Up left; 0 when not active.</summary>
        public int SpeedUpTicksLeft { get; internal set; }

        /// <summary>4 tiles/s, doubled to 8 while Speed Up is active.</summary>
        public int SpeedPerTick => Units.SpeedPerTick(SpeedUpTicksLeft > 0 ? 2 * BaseTilesPerSecond : BaseTilesPerSecond);

        /// <summary>Bombs the bomber may have on the arena at once: 1 plus one per Bomb Up, at most 4.</summary>
        public int MaxBombs => System.Math.Min(BombCap, 1 + BombUps);

        /// <summary>Tiles the fire reaches in each direction: 1, or 2 with Fire Up.</summary>
        public int BlastRange => HasFireUp ? 2 : 1;

        /// <summary>The power-ups the bomber holds now, e.g. to carry into the next stage.</summary>
        public PowerUpLoadout Loadout => new PowerUpLoadout
        {
            FireUp = HasFireUp,
            BombUps = BombUps,
            RemoteControl = HasRemoteControl,
            SpeedUpTicksLeft = SpeedUpTicksLeft,
        };

        /// <summary>Whether picking up a power-up of this kind would change anything.</summary>
        public bool CanUse(PowerUpKind kind) => kind switch
        {
            PowerUpKind.FireUp => !HasFireUp,
            PowerUpKind.BombUp => MaxBombs < BombCap,
            PowerUpKind.RemoteControl => !HasRemoteControl,
            _ => true,
        };

        internal void Apply(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.FireUp: HasFireUp = true; break;
                case PowerUpKind.BombUp: if (MaxBombs < BombCap) BombUps++; break;
                case PowerUpKind.RemoteControl: HasRemoteControl = true; break;
                case PowerUpKind.SpeedUp: SpeedUpTicksLeft = SpeedUpTicks; break;
            }
        }

        internal void Apply(PowerUpLoadout loadout)
        {
            HasFireUp |= loadout.FireUp;
            BombUps = System.Math.Min(BombCap - 1, BombUps + loadout.BombUps);
            HasRemoteControl |= loadout.RemoteControl;
            SpeedUpTicksLeft = System.Math.Max(SpeedUpTicksLeft, loadout.SpeedUpTicksLeft);
        }

        internal void LoseAllPowerUps()
        {
            HasFireUp = false;
            BombUps = 0;
            HasRemoteControl = false;
            SpeedUpTicksLeft = 0;
        }

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
