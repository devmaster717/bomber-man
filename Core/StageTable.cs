using System;
using System.Collections.Generic;

namespace BombArena.Core
{
    /// <summary>
    /// The difficulty curve from spec section 7, used to generate the 100 stage files. After generation the
    /// files are the source of truth and can be tuned by hand.
    /// </summary>
    public static class StageTable
    {
        public const int StageCount = 100;

        private sealed class Band
        {
            public int First, Last, MinEnemies, MaxEnemies, Runners, TargetSeconds;
            public Func<int, (int w, int h)> Size;
            public EnemyKind[] Kinds;
        }

        private static readonly Band[] Bands =
        {
            new Band { First = 1, Last = 10, Size = _ => (19, 9), MinEnemies = 3, MaxEnemies = 4,
                Kinds = new[] { EnemyKind.Walker }, Runners = 2, TargetSeconds = 90 },
            new Band { First = 11, Last = 30, Size = _ => (21, 11), MinEnemies = 4, MaxEnemies = 6,
                Kinds = new[] { EnemyKind.Walker, EnemyKind.WallPasser }, Runners = 3, TargetSeconds = 120 },
            new Band { First = 31, Last = 60, Size = n => n <= 45 ? (23, 11) : (23, 13), MinEnemies = 5, MaxEnemies = 7,
                Kinds = new[] { EnemyKind.Walker, EnemyKind.WallPasser, EnemyKind.Phantom }, Runners = 4, TargetSeconds = 150 },
            new Band { First = 61, Last = 90, Size = _ => (25, 13), MinEnemies = 6, MaxEnemies = 8,
                Kinds = new[] { EnemyKind.Walker, EnemyKind.WallPasser, EnemyKind.Phantom, EnemyKind.Runner }, Runners = 5, TargetSeconds = 180 },
            new Band { First = 91, Last = 100, Size = _ => (27, 15), MinEnemies = 8, MaxEnemies = 10,
                Kinds = new[] { EnemyKind.Walker, EnemyKind.WallPasser, EnemyKind.Phantom, EnemyKind.Runner }, Runners = 6, TargetSeconds = 210 },
        };

        /// <summary>The generated spec for stage <paramref name="number"/> (1–100).</summary>
        public static StageSpec Spec(int number)
        {
            if (number < 1 || number > StageCount) throw new ArgumentOutOfRangeException(nameof(number));
            var band = Array.Find(Bands, b => number >= b.First && number <= b.Last);

            // Enemy count rises evenly across the band.
            int span = band.Last - band.First;
            int count = band.MinEnemies + (span == 0 ? 0 : ((number - band.First) * (band.MaxEnemies - band.MinEnemies) + span / 2) / span);

            var (w, h) = band.Size(number);
            return new StageSpec
            {
                Number = number,
                Width = w,
                Height = h,
                Seed = (ulong)(1000 + number * 7919),
                SoftBlockPercent = Arena.DefaultSoftBlockPercent,
                Enemies = Mix(band.Kinds, count, number - band.First, span),
                TargetSeconds = band.TargetSeconds,
                RunnersFromExit = band.Runners,
            };
        }

        /// <summary>
        /// Splits the enemy count over the band's kinds: the newest kind starts at one and grows through the
        /// band; Walkers fill the rest. In the last band every kind gets an even share.
        /// </summary>
        private static List<EnemyGroup> Mix(EnemyKind[] kinds, int count, int position, int span)
        {
            var counts = new int[kinds.Length];
            if (kinds.Length == 1)
                counts[0] = count;
            else if (kinds.Length == 4 && count >= 8)
            {
                for (int i = 0; i < count; i++) counts[i % 4]++;
            }
            else
            {
                // Older special kinds keep one each; the newest grows from 1 to about a third of the enemies.
                int newest = kinds.Length - 1;
                for (int i = 1; i < newest; i++) counts[i] = 1;
                int maxNewest = Math.Max(1, count / 3);
                counts[newest] = 1 + (span == 0 ? 0 : position * (maxNewest - 1) / span);
                int used = 0;
                for (int i = 1; i < kinds.Length; i++) used += counts[i];
                counts[0] = Math.Max(1, count - used);
            }

            var groups = new List<EnemyGroup>();
            for (int i = 0; i < kinds.Length; i++)
                if (counts[i] > 0) groups.Add(new EnemyGroup(kinds[i], counts[i]));
            return groups;
        }
    }
}
