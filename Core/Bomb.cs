namespace BombArena.Core
{
    /// <summary>A solid object a bomber places on its tile; it explodes when its fuse runs out or fire reaches it.</summary>
    public sealed class Bomb
    {
        /// <summary>2.5 seconds.</summary>
        public const int FuseTicks = Units.TicksPerSecond * 5 / 2;

        public Bomber Owner { get; }
        public TilePos Tile { get; }
        public int Range { get; }

        /// <summary>Ticks until it explodes on its own; null for a remote bomb, which waits for Detonate.</summary>
        public int? FuseLeft { get; internal set; }

        /// <summary>
        /// True while the owner has not yet stepped fully off the bomb; until then the owner may walk on it.
        /// </summary>
        public bool OwnerStillOn { get; internal set; } = true;

        public bool IsRemote => FuseLeft == null;

        internal Bomb(Bomber owner, TilePos tile, int range, int? fuse)
        {
            Owner = owner;
            Tile = tile;
            Range = range;
            FuseLeft = fuse;
        }
    }
}
