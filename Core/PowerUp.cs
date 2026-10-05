namespace BombArena.Core
{
    public enum PowerUpKind
    {
        FireUp,
        BombUp,
        RemoteControl,
        SpeedUp,
    }

    /// <summary>A power-up lying in the arena (spec section 5): hidden under a soft block until uncovered.</summary>
    public sealed class PowerUp
    {
        public TilePos Tile { get; }

        /// <summary>Null until it appears; in stage mode the type is drawn when it is revealed.</summary>
        public PowerUpKind? Kind { get; internal set; }

        /// <summary>True once the soft block over it is gone and that blast's fire has ended.</summary>
        public bool Revealed { get; internal set; }

        internal bool Uncovering { get; set; }

        internal PowerUp(TilePos tile, PowerUpKind? kind)
        {
            Tile = tile;
            Kind = kind;
        }
    }

    /// <summary>Power-ups a bomber starts with: carried over from the last stage or bought in the shop.</summary>
    public struct PowerUpLoadout
    {
        public bool FireUp;
        public int BombUps;
        public bool RemoteControl;
        public int SpeedUpTicksLeft;

        public bool IsEmpty => !FireUp && BombUps == 0 && !RemoteControl && SpeedUpTicksLeft == 0;
    }
}
