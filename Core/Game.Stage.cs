using System;
using System.Collections.Generic;

namespace BombArena.Core
{
    /// <summary>Stage-mode rules (spec section 7): the hidden exit, Runners from the exit, target time and stars.</summary>
    public sealed partial class Game
    {
        /// <summary>The exit's tile, hidden under a soft block until uncovered. Null when the game has no exit.</summary>
        public TilePos? ExitTile { get; private set; }

        /// <summary>True once the soft block over the exit is destroyed and that blast's fire has ended.</summary>
        public bool ExitRevealed { get; private set; }

        /// <summary>The exit can be entered: uncovered, and every enemy (including Runners still to come) is dead.</summary>
        public bool ExitOpen => ExitRevealed && EnemiesRemaining == 0 && _runnersWaiting == 0;

        /// <summary>Target time T in ticks; 0 means no time limit. The attempt fails at 3T.</summary>
        public int TargetTicks { get; private set; }

        public int RunnersFromExit { get; private set; }

        /// <summary>The tick on which the stage was cleared.</summary>
        public long? ClearedOnTick { get; private set; }

        private bool _exitUncovering, _runnersTriggered;
        private int _runnersWaiting;

        /// <summary>
        /// A single-player stage attempt built from its spec. The layout, enemies and exit come from the stage seed
        /// and are the same on every attempt; the hidden power-up and enemy decisions come from <paramref name="attemptSeed"/>.
        /// </summary>
        public static Game ForStage(StageSpec spec, ulong? attemptSeed = null)
        {
            ulong attempt = attemptSeed ?? spec.Seed;
            var arena = Arena.Generate(spec.Width, spec.Height, spec.Seed, playerCount: 1, spec.SoftBlockPercent);
            var game = new Game(arena, attempt, Arena.SpawnTiles(spec.Width, spec.Height, 1)[0])
            {
                TargetTicks = spec.TargetSeconds * Units.TicksPerSecond,
                RunnersFromExit = spec.RunnersFromExit,
            };
            var groups = new List<(EnemyKind, int)>();
            foreach (var g in spec.Enemies) groups.Add((g.Kind, g.Count));
            game.PlaceEnemies(spec.Seed, groups);
            game.PlaceExit(spec.Seed);
            game.HidePowerUp(attempt);
            return game;
        }

        /// <summary>Stars for a clear (spec section 7): under T = 3, under 1.5T = 2, otherwise 1. Zero if not cleared.</summary>
        public int Stars
        {
            get
            {
                if (ClearedOnTick is not long t) return 0;
                if (TargetTicks <= 0 || t < TargetTicks) return 3;
                if (2 * t < 3L * TargetTicks) return 2;
                return 1;
            }
        }

        /// <summary>Hides the exit under a soft block chosen from the seed (or, with no soft blocks, uncovered on the floor).</summary>
        public void PlaceExit(ulong seed)
        {
            var rng = new Rng(seed ^ 0xE817_D00DUL);
            var soft = new List<TilePos>();
            var floor = new List<TilePos>();
            for (int y = 0; y < Arena.Height; y++)
            for (int x = 0; x < Arena.Width; x++)
            {
                var t = new TilePos(x, y);
                if (Arena[t] == Tile.SoftBlock) soft.Add(t);
                else if (Arena[t] == Tile.Floor && Math.Abs(t.X - Bomber.Spawn.X) + Math.Abs(t.Y - Bomber.Spawn.Y) >= EnemySpawnDistance)
                    floor.Add(t);
            }

            if (soft.Count > 0)
                ExitTile = soft[rng.Next(soft.Count)];
            else if (floor.Count > 0)
            {
                ExitTile = floor[rng.Next(floor.Count)];
                ExitRevealed = true;
            }
        }

        /// <summary>Puts the exit on a specific tile (for tests and hand-made layouts).</summary>
        public void SetExit(TilePos tile, int runners = 0)
        {
            ExitTile = tile;
            RunnersFromExit = runners;
            ExitRevealed = Arena[tile] != Tile.SoftBlock;
        }

        /// <summary>Called every tick after fire burns down: reveals the exit and releases waiting Runners once its fire is out.</summary>
        private void UpdateExit()
        {
            if (ExitTile is not TilePos exit || IsBurning(exit)) return;

            if (_exitUncovering)
            {
                _exitUncovering = false;
                ExitRevealed = true;
            }

            if (_runnersWaiting > 0)
            {
                for (int i = 0; i < _runnersWaiting; i++)
                    AddEnemy(EnemyKind.Runner, exit);
                _runnersWaiting = 0;
            }
        }

        internal void SetTargetSeconds(int seconds) => TargetTicks = seconds * Units.TicksPerSecond;

        private void Clear()
        {
            Outcome = Outcome.Cleared;
            ClearedOnTick = Tick;
        }
    }
}
