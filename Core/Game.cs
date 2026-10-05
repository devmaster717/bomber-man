using System;
using System.Collections.Generic;

namespace BombArena.Core
{
    public enum Outcome
    {
        Playing,
        Failed,
        Cleared,
    }

    /// <summary>
    /// The state of one stage attempt or round, advanced one fixed tick at a time. The view reads this state
    /// and never changes it; all rules live here. Tick order (spec section 11): inputs → movement → bomb
    /// placement → fuses → explosions and chains → deaths → timers → win/lose checks.
    /// </summary>
    public sealed class Game
    {
        /// <summary>Fire stays deadly for 0.5 seconds.</summary>
        public const int FireTicks = Units.TicksPerSecond / 2;

        private readonly List<Bomber> _bombers = new List<Bomber>();
        private readonly List<Bomb> _bombs = new List<Bomb>();
        private readonly int[] _fire;

        public Arena Arena { get; }
        public IReadOnlyList<Bomber> Bombers => _bombers;
        public IReadOnlyList<Bomb> Bombs => _bombs;

        /// <summary>The single-player bomber (player 0).</summary>
        public Bomber Bomber => _bombers[0];

        /// <summary>Ticks run so far.</summary>
        public long Tick { get; private set; }

        public Outcome Outcome { get; private set; } = Outcome.Playing;

        /// <summary>Raised when a bomb explodes (for sound, vibration and effects in the view).</summary>
        public event Action<Bomb> BombExploded;

        public Game(Arena arena, params TilePos[] spawns)
        {
            Arena = arena;
            _fire = new int[arena.Width * arena.Height];
            for (int i = 0; i < spawns.Length; i++)
                _bombers.Add(new Bomber(i, spawns[i]));
        }

        /// <summary>A single-player game on a freshly generated arena.</summary>
        public static Game Create(int width, int height, ulong seed, int softBlockPercent = Arena.DefaultSoftBlockPercent)
        {
            var arena = Arena.Generate(width, height, seed, playerCount: 1, softBlockPercent);
            return new Game(arena, Arena.SpawnTiles(width, height, 1)[0]);
        }

        public bool IsBurning(TilePos tile) => _fire[tile.Y * Arena.Width + tile.X] > 0;
        public bool IsBurning(int x, int y) => _fire[y * Arena.Width + x] > 0;

        public Bomb BombAt(TilePos tile)
        {
            foreach (var b in _bombs)
                if (b.Tile == tile) return b;
            return null;
        }

        /// <summary>Whether a mover could enter the tile: open floor with no bomb, except a bomb its owner is still on.</summary>
        public bool IsWalkableFor(Bomber mover, int x, int y)
        {
            if (!Arena.IsWalkable(x, y)) return false;
            var bomb = BombAt(new TilePos(x, y));
            return bomb == null || (bomb.Owner == mover && bomb.OwnerStillOn);
        }

        /// <summary>Single-player convenience.</summary>
        public void Step(Direction move, bool placeBomb = false) => Step(new BomberInput(move, placeBomb));

        /// <summary>Advances the game by one tick. Inputs are per bomber, in bomber order.</summary>
        public void Step(params BomberInput[] inputs)
        {
            if (Outcome != Outcome.Playing) return;

            // Movement
            for (int i = 0; i < _bombers.Count; i++)
            {
                var bomber = _bombers[i];
                if (!bomber.Alive) continue;
                var input = i < inputs.Length ? inputs[i] : BomberInput.None;
                bomber.Move(input.Move, (x, y) => IsWalkableFor(bomber, x, y));
            }
            ReleaseBombsOwnersLeft();

            // Bomb placement
            for (int i = 0; i < _bombers.Count && i < inputs.Length; i++)
                if (inputs[i].PlaceBomb && _bombers[i].Alive)
                    TryPlaceBomb(_bombers[i]);

            // Fuses, explosions and chains
            var exploding = new List<Bomb>();
            foreach (var bomb in _bombs)
            {
                if (bomb.FuseLeft == null) continue;
                bomb.FuseLeft--;
                if (bomb.FuseLeft <= 0) exploding.Add(bomb);
            }
            Explode(exploding);

            // Deaths
            foreach (var bomber in _bombers)
                if (bomber.Alive && TouchesFire(bomber))
                    Kill(bomber);

            // Timers
            for (int i = 0; i < _fire.Length; i++)
                if (_fire[i] > 0) _fire[i]--;

            Tick++;

            // Win/lose checks
            if (!_bombers[0].Alive)
                Outcome = Outcome.Failed;
        }

        private void ReleaseBombsOwnersLeft()
        {
            foreach (var bomb in _bombs)
                if (bomb.OwnerStillOn && !bomb.Owner.BodyOverlaps(bomb.Tile))
                    bomb.OwnerStillOn = false;
        }

        /// <summary>Places a bomb on the bomber's tile; fails if the tile holds a bomb or the bomber has its maximum out.</summary>
        internal bool TryPlaceBomb(Bomber bomber)
        {
            var tile = bomber.Tile;
            if (BombAt(tile) != null) return false;
            int mine = 0;
            foreach (var b in _bombs)
                if (b.Owner == bomber) mine++;
            if (mine >= bomber.MaxBombs) return false;

            _bombs.Add(new Bomb(bomber, tile, bomber.BlastRange, Bomb.FuseTicks));
            return true;
        }

        /// <summary>
        /// Explodes bombs and every bomb their fire reaches, all within this tick. Soft blocks hit by any blast
        /// stop that blast and are removed once every chained explosion has been resolved.
        /// </summary>
        private void Explode(List<Bomb> initial)
        {
            if (initial.Count == 0) return;

            var queue = new Queue<Bomb>(initial);
            var done = new HashSet<Bomb>();
            var softHit = new List<TilePos>();

            while (queue.Count > 0)
            {
                var bomb = queue.Dequeue();
                if (!done.Add(bomb)) continue;

                SetFire(bomb.Tile);
                foreach (var (dx, dy) in Directions)
                {
                    for (int step = 1; step <= bomb.Range; step++)
                    {
                        var t = new TilePos(bomb.Tile.X + dx * step, bomb.Tile.Y + dy * step);
                        if (!Arena.InBounds(t.X, t.Y)) break;
                        var tile = Arena[t];
                        if (tile == Tile.HardBlock) break;
                        SetFire(t);
                        if (tile == Tile.SoftBlock)
                        {
                            softHit.Add(t);
                            break;
                        }
                        var other = BombAt(t);
                        if (other != null && !done.Contains(other))
                            queue.Enqueue(other);
                    }
                }
            }

            foreach (var t in softHit)
                Arena.DestroySoftBlock(t);

            foreach (var bomb in done)
            {
                _bombs.Remove(bomb);
                BombExploded?.Invoke(bomb);
            }
        }

        private void SetFire(TilePos t) => _fire[t.Y * Arena.Width + t.X] = FireTicks;

        private bool TouchesFire(Bomber bomber)
        {
            var c = bomber.Tile;
            for (int y = c.Y - 1; y <= c.Y + 1; y++)
            for (int x = c.X - 1; x <= c.X + 1; x++)
                if (Arena.InBounds(x, y) && IsBurning(x, y) && bomber.HitboxOverlaps(new TilePos(x, y)))
                    return true;
            return false;
        }

        private void Kill(Bomber bomber)
        {
            bomber.Alive = false;
            bomber.DiedOnTick = Tick;
        }

        private static readonly (int dx, int dy)[] Directions = { (1, 0), (-1, 0), (0, 1), (0, -1) };
    }
}
