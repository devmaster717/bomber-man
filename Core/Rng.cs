namespace BombArena.Core
{
    /// <summary>
    /// Deterministic random numbers (SplitMix64). Used instead of System.Random so a seed produces the
    /// same sequence on .NET and on Unity's runtimes.
    /// </summary>
    public sealed class Rng
    {
        private ulong _state;

        public Rng(ulong seed)
        {
            _state = seed;
        }

        public ulong NextULong()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>A number in [0, maxExclusive).</summary>
        public int Next(int maxExclusive) => (int)(NextULong() % (ulong)maxExclusive);
    }
}
