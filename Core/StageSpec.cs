using System.Collections.Generic;

namespace BombArena.Core
{
    /// <summary>Everything that defines one stage (spec section 7); stage data files describe these.</summary>
    public sealed class StageSpec
    {
        public int Number { get; set; } = 1;
        public int Width { get; set; } = Arena.DefaultWidth;
        public int Height { get; set; } = Arena.DefaultHeight;
        public ulong Seed { get; set; } = 1;
        public int SoftBlockPercent { get; set; } = Arena.DefaultSoftBlockPercent;

        /// <summary>Enemies placed at the start, by kind.</summary>
        public List<EnemyGroup> Enemies { get; set; } = new List<EnemyGroup>();

        /// <summary>Target time T in seconds: under T earns 3 stars; the attempt fails at 3T.</summary>
        public int TargetSeconds { get; set; } = 90;

        /// <summary>Runners released the first time fire reaches the uncovered exit.</summary>
        public int RunnersFromExit { get; set; } = 2;
    }

    public sealed class EnemyGroup
    {
        public EnemyKind Kind { get; set; }
        public int Count { get; set; }

        public EnemyGroup()
        {
        }

        public EnemyGroup(EnemyKind kind, int count)
        {
            Kind = kind;
            Count = count;
        }
    }
}
