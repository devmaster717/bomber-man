using System;
using System.Collections.Generic;

namespace BombArena.Core
{
    /// <summary>Bluetooth round rules (spec section 8): last bomber standing wins; no one left or time up is a draw.</summary>
    public sealed partial class Game
    {
        /// <summary>Destroyed soft blocks reveal a power-up this often in a round.</summary>
        public const int RoundDropPercent = 40;

        /// <summary>True for a Bluetooth round, false for a stage attempt.</summary>
        public bool IsRound { get; internal set; }

        /// <summary>Round time limit in ticks; 0 means unlimited.</summary>
        public int TimeLimitTicks { get; private set; }

        /// <summary>The winning bomber's index once the round is over; null while playing or for a draw.</summary>
        public int? Winner { get; private set; }

        public bool IsDraw => Outcome == Outcome.RoundOver && Winner == null;

        /// <summary>
        /// A round for 2–3 bombers. The arena (size from the host's settings) is generated from <paramref name="seed"/>,
        /// which every phone receives, so guests build the same starting arena.
        /// </summary>
        public static Game ForRound(int width, int height, int players, int timeLimitSeconds, ulong seed, bool enemiesOn = false)
        {
            if (players < 2 || players > 3) throw new ArgumentOutOfRangeException(nameof(players), "2–3 players");
            var arena = Arena.Generate(width, height, seed, players);
            var game = new Game(arena, seed, Arena.SpawnTiles(width, height, players))
            {
                IsRound = true,
                SoftBlockDropPercent = RoundDropPercent,
                TimeLimitTicks = timeLimitSeconds * Units.TicksPerSecond,
            };
            if (enemiesOn) game.PlaceRoundEnemies(seed);
            return game;
        }

        /// <summary>Enemies per player when the host switches enemies on.</summary>
        public const int RoundEnemiesPerPlayer = 2;

        private static readonly EnemyKind[] RoundEnemyKinds = { EnemyKind.Walker, EnemyKind.WallPasser, EnemyKind.Phantom };

        /// <summary>
        /// Two enemies per player, each a random Walker, Wall-passer or Phantom (never Runners, which would decide
        /// rounds instead of the players), at least 5 tiles from every spawn.
        /// </summary>
        private void PlaceRoundEnemies(ulong seed)
        {
            var rng = new Rng(seed ^ 0x2B0_E7E3UL);
            var counts = new int[RoundEnemyKinds.Length];
            for (int i = 0; i < RoundEnemiesPerPlayer * _bombers.Count; i++) counts[rng.Next(counts.Length)]++;
            var groups = new List<(EnemyKind, int)>();
            for (int k = 0; k < counts.Length; k++) groups.Add((RoundEnemyKinds[k], counts[k]));
            PlaceEnemies(seed, groups);
        }

        public int BombersAlive
        {
            get
            {
                int n = 0;
                foreach (var b in _bombers)
                    if (b.Alive) n++;
                return n;
            }
        }

        private void CheckRoundOver()
        {
            int alive = BombersAlive;
            if (alive <= 1)
            {
                Outcome = Outcome.RoundOver;
                Winner = alive == 1 ? _bombers.Find(b => b.Alive).Index : (int?)null;
            }
            else if (TimeLimitTicks > 0 && Tick >= TimeLimitTicks)
            {
                Outcome = Outcome.RoundOver; // time up with two or more standing: a draw
                Winner = null;
            }
        }

        /// <summary>A player leaves the round on purpose: their bomber counts as dead.</summary>
        public void Forfeit(int bomberIndex)
        {
            var b = _bombers[bomberIndex];
            if (!b.Alive || Outcome != Outcome.Playing) return;
            Kill(b);
            if (IsRound) CheckRoundOver();
        }
    }
}
