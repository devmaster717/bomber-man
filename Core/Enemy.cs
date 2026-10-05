using System;

namespace BombArena.Core
{
    public enum EnemyKind
    {
        Walker,
        Runner,
        Phantom,
        WallPasser,
    }

    /// <summary>A computer-controlled creature that kills a bomber on touch (spec section 6).</summary>
    public sealed class Enemy
    {
        public EnemyKind Kind { get; }

        /// <summary>Centre position in units.</summary>
        public int X { get; internal set; }
        public int Y { get; internal set; }

        public bool Alive { get; internal set; } = true;

        /// <summary>The direction it is travelling; chosen afresh at every tile centre.</summary>
        public Direction Heading { get; internal set; } = Direction.None;

        public int SpeedPerTick => Units.SpeedPerTick(TilesPerSecond(Kind));

        public Enemy(EnemyKind kind, TilePos tile)
        {
            Kind = kind;
            X = tile.X * Units.PerTile;
            Y = tile.Y * Units.PerTile;
        }

        public TilePos Tile => new TilePos(Units.NearestTile(X), Units.NearestTile(Y));

        public bool AtTileCentre => X % Units.PerTile == 0 && Y % Units.PerTile == 0;

        /// <summary>Speeds in tiles per second; the bomber's base speed is 4.</summary>
        public static int TilesPerSecond(EnemyKind kind) => kind switch
        {
            EnemyKind.Runner => 8,
            _ => 3,
        };

        /// <summary>Hitboxes are about 0.6 tile wide, centred on the character.</summary>
        public bool HitboxOverlaps(TilePos tile) => Bomber.Overlaps(X, Y, Bomber.HitboxHalf, tile);

        public bool Touches(Bomber bomber) =>
            Math.Abs(X - bomber.X) < 2 * Bomber.HitboxHalf && Math.Abs(Y - bomber.Y) < 2 * Bomber.HitboxHalf;

        /// <summary>
        /// Moves along the grid for one tick: at each tile centre it picks a random open direction, never
        /// reversing unless in a dead end; between centres, if the tile ahead has become blocked (a bomb), it
        /// turns back.
        /// </summary>
        internal void Move(Func<int, int, bool> open, Rng rng)
        {
            int budget = SpeedPerTick;
            int guard = 0;
            while (budget > 0 && guard++ < 8)
            {
                if (AtTileCentre)
                {
                    Heading = ChooseHeading(open, rng);
                    if (Heading == Direction.None) return;
                }

                var here = Tile;
                int dx = Heading.Dx(), dy = Heading.Dy();
                // The centre we are heading for: the next one along the heading.
                int targetX, targetY;
                if (dx != 0)
                {
                    int t = dx > 0 ? FloorDiv(X, Units.PerTile) + 1 : CeilDiv(X, Units.PerTile) - 1;
                    targetX = t * Units.PerTile;
                    targetY = Y;
                    if (!open(t, here.Y)) { Heading = Opposite(Heading); continue; }
                }
                else
                {
                    int t = dy > 0 ? FloorDiv(Y, Units.PerTile) + 1 : CeilDiv(Y, Units.PerTile) - 1;
                    targetX = X;
                    targetY = t * Units.PerTile;
                    if (!open(here.X, t)) { Heading = Opposite(Heading); continue; }
                }

                int distance = Math.Abs(targetX - X) + Math.Abs(targetY - Y);
                int step = Math.Min(budget, distance);
                X += dx * step;
                Y += dy * step;
                budget -= step;
            }
        }

        private Direction ChooseHeading(Func<int, int, bool> open, Rng rng)
        {
            var t = Tile;
            var reverse = Opposite(Heading);
            Span<Direction> options = stackalloc Direction[4];
            int count = 0;
            foreach (var d in All)
                if (d != reverse && open(t.X + d.Dx(), t.Y + d.Dy()))
                    options[count++] = d;
            if (count == 0)
                return reverse != Direction.None && open(t.X + reverse.Dx(), t.Y + reverse.Dy()) ? reverse : Direction.None;
            return options[rng.Next(count)];
        }

        private static readonly Direction[] All = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

        internal static Direction Opposite(Direction d) => d switch
        {
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            Direction.Left => Direction.Right,
            Direction.Right => Direction.Left,
            _ => Direction.None,
        };

        private static int FloorDiv(int a, int b) => a / b;
        private static int CeilDiv(int a, int b) => (a + b - 1) / b;
    }
}
