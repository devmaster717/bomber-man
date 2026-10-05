namespace BombArena.Core
{
    /// <summary>What one player's controls ask for during a tick.</summary>
    public readonly struct BomberInput
    {
        public readonly Direction Move;
        public readonly bool PlaceBomb;
        public readonly bool Detonate;

        public BomberInput(Direction move, bool placeBomb = false, bool detonate = false)
        {
            Move = move;
            PlaceBomb = placeBomb;
            Detonate = detonate;
        }

        public static readonly BomberInput None = new BomberInput(Direction.None);
    }
}
